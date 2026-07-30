# MicroForge — bibliothèque de micropackages NuGet pour IA

MicroForge transforme les snippets de code générique que les IA (re)génèrent sans
cesse en **micropackages NuGet réutilisables, testés, documentés et versionnés**.
Plus on code, plus la bibliothèque grandit ; moins on régénère, moins on consomme de
tokens et d'énergie.

Quatre garanties mécaniques, appliquées par l'outil et non par la discipline :

1. **On ne publie pas deux fois le même snippet** — détection de quasi-duplication
   par similarité vectorielle avant publication.
2. **On ne casse pas un consommateur par surprise** — le contrat public est extrait
   de l'assembly et dicte l'incrément SemVer exigé.
3. **On ne réécrit jamais l'histoire** — un artefact publié est immuable ; un package
   obsolète se déprécie, il ne disparaît pas.
4. **On ne publie rien sans preuve** — tests xUnit verts en `Release` et mode
   d'emploi complet ; le `.nupkg` embarque la DLL déjà compilée, si bien que le
   consommateur reçoit à l'octet près le binaire qui a été testé.

## Architecture

```
MicroForge/
├── RULES.md                  Règles immuables (R1–R13), appliquées par le validateur
├── AGENT.md                  Workflow obligatoire pour les IA (6 étapes)
├── INSTALL.md                Guide d'installation et chemins à manipuler
├── TESTING.md                Protocole de test et lecture des métriques
├── CLAUDE.md                 Chargé par Claude Code → pointe vers AGENT.md
├── nuget.config              Rend le feed visible aux projets sous ce dossier
├── forge.ps1                 CLI
├── setup.ps1                 Prépare la bibliothèque (aucun effet hors du dossier)
├── install.ps1               Installe sur la machine (lanceur, source NuGet, CLAUDE.md)
├── feed/                     Flux NuGet local : .nupkg publiés, IMMUABLES
├── registry/
│   ├── index.json            Index de recherche          ─┐
│   ├── api/<Id>/<v>.json     Contrats publics + digests   ├─ dérivés, régénérables
│   ├── embeddings.json       Cache des vecteurs          ─┘
│   └── deprecations.json     Décisions de dépréciation — NON régénérable
├── packages/                 Sources des micropackages
│   ├── Directory.Build.props Qualité imposée à tous (immuable)
│   └── Micro.<Domaine>.<Action>/{README.md, src/, tests/}
├── tools/
│   ├── SnippetForge/         Le CLI
│   └── SnippetForge.Tests/   199 tests — l'outil qui exige des tests en a
└── demo/                     Projet consommateur d'exemple
```

## Installation — deux commandes

Guide complet, chemins exacts et désinstallation : **[INSTALL.md](INSTALL.md)**.

```powershell
# 1. Une fois par machine (lance setup.ps1 tout seul)
cd C:\Users\hugoe\Documents\Widgets\MicroForge
.\install.ps1 -WhatIf    # montre ce qui serait modifié
.\install.ps1

# 2. Une fois par projet, dans un nouveau terminal
cd <votre projet>
forge init .
```

`forge init` écrit la source NuGet et les instructions agent (`CLAUDE.md` et
`.github\copilot-instructions.md`). Il est idempotent et ne touche jamais au contenu
existant : les autres sources NuGet sont préservées, et les instructions sont
insérées dans un bloc délimité.

Ensuite, plus rien à faire : l'agent lit ses instructions, qui le renvoient vers
[AGENT.md](AGENT.md), et applique le workflow seul.

### Recherche sémantique (optionnelle)

Sans rien installer, MicroForge utilise un **repli lexical déterministe** intégré
(projection par hachage de n-grammes) : la recherche vectorielle et la détection de
doublons fonctionnent immédiatement, hors ligne, sans dépendance.

Pour la vraie synonymie (« découper » ≈ « chunk »), installer un modèle local :

```bash
ollama serve
ollama pull nomic-embed-text
```

MicroForge le détecte automatiquement. Aucune donnée ne quitte la machine. Les seuils
de duplication sont recalibrés selon le provider actif — les cosinus d'une projection
lexicale sont structurellement plus bas que ceux d'un modèle sémantique, appliquer
la même constante aux deux masquerait tous les doublons.

Variables d'environnement : `MICROFORGE_EMBED_MODEL`, `MICROFORGE_EMBED_ENDPOINT`.

## Commandes

| Commande | Rôle |
|----------|------|
| `search <mots> [--tags a;b] [--lexical]` | Recherche hybride lexicale + vectorielle |
| `info <Id>` | Mode d'emploi, versions, digests de contrat, dépréciations |
| `list` | Inventaire du feed |
| `diff <Id> <v1> <v2>` | Différence de contrat public entre deux versions |
| `new <Id> --description … --tags …` | Scaffold conforme (src, tests, README) |
| `validate <Id> [--skip-tests]` | Vérification des règles immuables |
| `bump <Id> <major\|minor\|patch>` | Incrémente la version déclarée |
| `publish <Id> [--allow-similar]` | Valide → teste → packe → vérifie contrat et doublon → publie |
| `duplicates [--all]` | Quasi-doublons de la bibliothèque (`--all` : tous les scores) |
| `index` | Régénère index, contrats et vecteurs depuis le feed |
| `deprecate <Id> --reason … [--versions] [--replacement]` | Déprécie sans altérer les artefacts |
| `undeprecate <Id>` / `deprecations` | Retire / liste les dépréciations |
| `init [<dossier>]` | Raccorde un projet : source NuGet + instructions IA (idempotent) |
| `stats` | Investissement, réutilisation effective, économie estimée |
| `doctor` | Diagnostic de l'installation et corrections à appliquer |
| `remote --source <url>` | Configure le dépôt NuGet d'équipe |
| `push <Id> [--version <v>]` | Pousse une version publiée vers le dépôt d'équipe |
| `outdated <projet>` | Classe les références : sûres vs à relire |
| `update <projet> [--safe-only] [--test "…"]` | Applique et restaure si vos tests échouent |

## Le versionnage, façon Docker

L'analogie tient sur trois points :

**Digest immuable.** Chaque version publiée a un contrat public extrait de l'assembly
et résumé en empreinte (`sha256:769da6f1e0b2`). Deux versions au même digest sont
interchangeables à la compilation. L'artefact ne bouge jamais.

**Compatibilité vérifiée, pas déclarée.** À la publication, le contrat est comparé à
celui du prédécesseur. Retirer un membre et déclarer un patch fait échouer la
publication :

```
Contrat vs 1.0.0 : +1 / -0 membre(s) → Minor exigé, Patch appliqué.
    + method Micro.Text.Slugify.Slugifier.IsSlug(System.String, …) : System.Boolean
ERREUR : Incrément insuffisant : le contrat exige un incrément Minor.
```

**Mise à jour opt-in, validée par vos tests.** Les consommateurs épinglent une
version exacte ; rien ne bouge tout seul. `forge outdated` classe chaque référence,
et `forge update --test "dotnet test"` applique les montées puis **restaure le projet
à l'identique si votre suite échoue**. Le système prouve que la compilation ne casse
pas ; c'est votre suite de tests qui juge du comportement.

## Détection de quasi-duplication

Avant publication, la description du candidat est vectorisée et comparée à toute la
bibliothèque :

```
[QUASI-DOUBLON] Micro.Text.Slugify 1.1.0 : similarité 0.703
ERREUR : Publication refusée : Micro.Text.Slugify couvre déjà ce besoin.
         Réutiliser ou étendre ce package.
```

`forge duplicates --all` affiche tous les scores par paire — utile pour l'hygiène de
la bibliothèque et pour recalibrer les seuils sur un corpus réel.

## Dépréciation sans casse

Un package obsolète, buggé ou fusionné dans un autre se déprécie :

```powershell
.\forge.ps1 deprecate Micro.Text.Slugify --versions "<1.1.0" --reason "…" --replacement Micro.Text.Slugify
```

La déclaration vit **hors des artefacts** (`registry/deprecations.json`) : les
`.nupkg` restent bit à bit identiques et installables, aucun build existant ne casse.
Les consommateurs voient l'alerte dans `search`, `info` et `outdated`. C'est
réversible (`undeprecate`) — contrairement à une suppression.

## Consommer depuis un projet externe

```powershell
forge init .
dotnet add package Micro.Flow.Retry --version 1.0.0
```

Épingler la version exacte : c'est ce qui rend les builds reproductibles et les
montées délibérées.

## Partager la bibliothèque avec une équipe

En solo, le feed local suffit. À plusieurs, l'effet cumulatif change de nature : c'est
le nombre de projets qui réutilisent une brique qui fait la rentabilité.

```powershell
forge remote --source https://nuget.interne/v3/index.json
$env:MICROFORGE_API_KEY = "<votre clé>"
forge push Micro.Flow.Retry
```

N'importe quel serveur NuGet v3 convient — [BaGet](https://github.com/loic-sharma/BaGet)
est le plus simple à héberger, Azure Artifacts et GitHub Packages fonctionnent aussi.
La clé d'API n'est **jamais** écrite sur disque : seul le nom de la variable
d'environnement qui la porte est enregistré, et elle est masquée dans toutes les
sorties.

L'ordre est délibéré : un package est validé, testé et publié **localement** d'abord,
puis poussé. Le dépôt partagé ne reçoit que des artefacts déjà éprouvés.

Côté consommateurs, il suffit alors de pointer `nuget.config` vers le dépôt d'équipe
plutôt que vers le feed local.

## Licence et attribution

MicroForge est distribué sous **[Apache-2.0](LICENSE)**. Concrètement :

- N'importe qui peut l'utiliser, le modifier, le redistribuer et le commercialiser,
  y compris au sein d'un produit propriétaire fermé.
- Toute redistribution — y compris d'une version modifiée — doit conserver la mention
  d'auteur et inclure une copie du fichier [NOTICE](NOTICE).
- La licence inclut une concession de brevet explicite, assortie d'une clause de
  rétorsion : quiconque attaque le projet en contrefaçon de brevet perd ses droits.

Ce que la licence **ne peut pas** faire : empêcher quelqu'un de réimplémenter l'idée
depuis zéro sans vous citer. Une licence protège du code et du texte, pas un concept.

## Packages d'exemple inclus

- **Micro.Flow.Retry 1.0.0** — relances asynchrones : backoff exponentiel, prédicat
  de relance, délai injectable (tests instantanés), logging `ILogger`.
- **Micro.Text.Slugify 1.1.0** — slug URL pur et déterministe ; `1.0.0` est dépréciée
  pour illustrer le mécanisme.
