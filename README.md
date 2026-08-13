# MicroForge — bibliothèque de micropackages pour IA

MicroForge transforme les snippets de code générique que les IA (re)génèrent sans
cesse en **micropackages réutilisables, testés, documentés et versionnés**.
Plus on code, plus la bibliothèque grandit ; moins on régénère, moins on consomme de
tokens et d'énergie.

Six écosystèmes : **C#/.NET, Python, TypeScript, JavaScript, Go, Rust** — avec deux
niveaux de garantie assumés, jamais confondus (voir [Multi-langage](#multi-langage)).

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
├── RULES.md                  Règles immuables (R1–R13 + R6 bis), appliquées par le validateur
├── AGENT.md                  Workflow obligatoire pour les IA (6 étapes)
├── INSTALL.md                Guide d'installation et chemins à manipuler
├── TESTING.md                Protocole de test et lecture des métriques
├── CLAUDE.md                 Chargé par Claude Code → pointe vers AGENT.md
├── nuget.config              Rend le feed visible aux projets sous ce dossier
├── forge.ps1                 Lance le CLI depuis les sources (sans l'outil global)
├── setup.ps1                 Prépare la bibliothèque (aucun effet hors du dossier)
├── install.ps1               Installe sur la machine (outil, source NuGet, CLAUDE.md)
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
│   ├── MicroForge.Analyzers/ Analyseur Roslyn (MFG001–MFG008), dans l'IDE
│   └── SnippetForge.Tests/   474 tests — l'outil qui exige des tests en a
└── demo/                     Projet consommateur d'exemple
```

Un package hors .NET remplace le `.csproj` par un `microforge.json` ; tout le reste
(README, `src/`, `tests/`) est identique.

## Installation

Guide complet, chemins exacts et désinstallation : **[INSTALL.md](INSTALL.md)**.

```bash
# 1. L'outil, depuis nuget.org
dotnet tool install --global MicroForge.Cli

# 2. Votre bibliothèque — vide, c'est le cas normal
forge create ~/microforge

#    (ou, pour rejoindre une bibliothèque existante :
#     git clone <dépôt> && cd <dépôt> && ./setup.ps1 && forge use .)

# 3. Une fois par projet
cd <votre projet>
forge init .
```

`forge init` écrit la source NuGet et les instructions agent (`CLAUDE.md` et
`.github\copilot-instructions.md`). Il est idempotent et ne touche jamais au contenu
existant : les autres sources NuGet sont préservées, et les instructions sont
insérées dans un bloc délimité.

Ensuite, plus rien à faire : l'agent lit ses instructions, qui le renvoient vers
[AGENT.md](AGENT.md), et applique le workflow seul.

## Configuration

Il n'y a **aucun fichier de configuration à écrire à la main**. Tout se règle par
trois commandes et, si besoin, quatre variables d'environnement.

### Les trois choses à régler

| Question | Commande | Où c'est mémorisé |
|----------|----------|-------------------|
| Où est ma bibliothèque ? | `forge use <chemin>` | `~/.microforge/root` |
| Comment ce projet la voit ? | `forge init .` | `<projet>/nuget.config` (.NET) |
| Comment l'IA sait quoi faire ? | `forge init .` | `<projet>/CLAUDE.md`, `.github/copilot-instructions.md` |

`forge use` ne sert qu'une fois par machine ; `forge init` une fois par projet. Les
deux sont **idempotents** : les relancer ne duplique rien et corrige les chemins si
vous déplacez la bibliothèque.

`forge doctor` vérifie l'ensemble et dit quoi corriger.

### Comment l'outil trouve la bibliothèque

Dans cet ordre, la première réponse gagne :

1. `MICROFORGE_ROOT` — variable d'environnement, prioritaire (utile en CI)
2. le dossier courant ou l'un de ses parents, s'il ressemble à une bibliothèque
3. `~/.microforge/root` — ce qu'a écrit `forge use`

### Variables d'environnement

Toutes optionnelles.

| Variable | Effet | Défaut |
|----------|-------|--------|
| `MICROFORGE_ROOT` | Force la bibliothèque à utiliser | résolution ci-dessus |
| `MICROFORGE_EMBED_ENDPOINT` | Serveur d'embeddings | `http://localhost:11434` (Ollama) |
| `MICROFORGE_EMBED_MODEL` | Modèle d'embeddings | `nomic-embed-text` |
| `MICROFORGE_API_KEY` | Clé du dépôt NuGet d'équipe — **jamais écrite sur disque** | — |

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
| `search <mots> [--tags a;b] [--lexical] [--language <id\|all>]` | Recherche hybride, filtrée sur l'écosystème du projet |
| `info <Id>` | Mode d'emploi, versions, digests de contrat, dépréciations |
| `list` | Inventaire du feed |
| `diff <Id> <v1> <v2>` | Différence de contrat public entre deux versions |
| `new <Id> --description … --tags … [--language <id>]` | Scaffold conforme (src, tests, README) |
| `validate <Id> [--skip-tests]` | Vérification des règles immuables |
| `review <Id>` | Prépare la relecture : ce que le validateur ne peut pas juger |
| `hazards [list\|add\|declare]` | Catalogue partagé des aléas de test, cumulatif |
| `bump <Id> <major\|minor\|patch>` | Incrémente la version déclarée |
| `publish <Id> [--allow-similar]` | Valide → teste → packe → vérifie contrat et doublon → publie |
| `duplicates [--all]` | Quasi-doublons de la bibliothèque (`--all` : tous les scores) |
| `index` | Régénère index, contrats et vecteurs depuis le feed |
| `deprecate <Id> --reason … [--versions] [--replacement]` | Déprécie sans altérer les artefacts |
| `undeprecate <Id>` / `deprecations` | Retire / liste les dépréciations |
| `init [<dossier>] [--language <id>]` | Raccorde un projet : source NuGet + instructions IA, adaptées à l'écosystème (idempotent) |
| `copy <Id> --into <dossier>` | Copie les sources avec provenance — repli hors .NET |
| `copied [<projet>]` | Packages copiés : versions en retard, fichiers retouchés |
| `report [<projet>]` | Ce que la bibliothèque a apporté à CE projet |
| `sign [--init <clé>]` | Signe le registre d'empreintes (RSA-3072) |
| `stats` | Investissement, réutilisation effective, économie estimée |
| `doctor` | Diagnostic de l'installation et corrections à appliquer |
| `remote --source <url>` | Configure le dépôt NuGet d'équipe |
| `push <Id> [--version <v>]` | Pousse une version publiée vers le dépôt d'équipe |
| `pull [<Id>]` | Rapatrie depuis le dépôt d'équipe |
| `outdated <projet>` | Classe les références : sûres vs à relire |
| `update <projet> [--safe-only] [--test "…"]` | Applique et restaure si vos tests échouent |
| `verify [--adopt]` | Empreintes des artefacts : dépôt manuel, falsification, suppression |
| `bench start\|report\|list` | Mesure une manche de test avec un agent IA |
| `use [<chemin>]` / `--version` | Désigne la bibliothèque / version de l'outil |

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

Le modèle est celui d'un clone Git — chaque poste travaille sur **sa copie locale**
(rapide, hors ligne), et se synchronise avec un point de partage :

- **Les sources** des micropackages (`packages/`) se partagent par Git : ce dépôt
  est la source de vérité, chaque poste reconstruit son feed via `setup.ps1`.
- **Les artefacts** se partagent par un dépôt NuGet d'équipe :

```powershell
forge remote --source https://nuget.interne/v3/index.json   # ou un dossier partagé
$env:MICROFORGE_API_KEY = "<votre clé>"
forge push Micro.Flow.Retry          # publier vers l'équipe (après validation locale)
forge pull Micro.Text.Slugify        # rapatrier ce que l'équipe a forgé
```

N'importe quel serveur NuGet v3 convient — [BaGet](https://github.com/loic-sharma/BaGet)
est le plus simple à héberger, Azure Artifacts et GitHub Packages fonctionnent aussi.
Un simple **dossier partagé** (UNC) marche également, et `forge pull` y rapatrie tout
d'un coup. La clé d'API n'est **jamais** écrite sur disque, et un artefact déjà
présent localement n'est jamais réécrit (immutabilité).

L'ordre est délibéré : un package est validé, testé et publié **localement** d'abord,
puis poussé. Le dépôt partagé ne reçoit que des artefacts déjà éprouvés.

## Mesurer une manche de test IA

```powershell
forge bench start manche4 --project . --prompt "« votre demande »"
# … faire travailler l'agent …
forge bench report manche4
```

Le rapport mesure ce que l'agent a produit (fichiers, lignes nettes), réutilisé
(packages installés), forgé (packages publiés), et surtout la **trace des commandes
forge invoquées** pendant la manche — la preuve directe que le workflow est suivi.
Protocole complet : [TESTING.md](TESTING.md).

## Multi-langage

Un micropackage déclare son écosystème ; l'outil en déduit **ce qu'il peut prouver**,
et le dit. Deux profils, jamais confondus :

| | **Profil vérifié** (C#/.NET) | **Profil de base** (Python, TS, JS, Go, Rust) |
|---|---|---|
| Recherche, anti-doublon, aléas, README | oui | oui |
| Tests exigés et exécutés | oui | oui |
| Effets interdits (`Console`, `DateTime.Now`…) | **analyseur Roslyn, dans l'IDE** | par convention, non vérifié |
| Contrat public extrait | oui | non |
| SemVer | **opposable** — le contrat dicte l'incrément | déclaratif |
| Distribution | NuGet (`dotnet add package`) | copie (`forge copy`) |
| Maintenance | `forge outdated` / `forge update` | `forge copied` |

**La dégradation est explicite** : `forge new --language python` énonce ce que
l'écosystème ne garantit pas, `forge publish` le répète, et le bloc d'instructions
écrit dans le projet commence par là. Jamais de fausse assurance.

La recherche est **filtrée sur l'écosystème du projet courant**, détecté par ses
fichiers (`*.csproj`, `pyproject.toml`, `go.mod`, `Cargo.toml`, `tsconfig.json`,
`package.json`). Proposer un package Python à un projet C# ferait perdre du temps et
pourrait être copié à tort. `--language all` lève le filtre.

```bash
cd mon-projet-python
forge init .                      # détecte Python, pas de nuget.config, instructions adaptées
forge search "découper en lots"   # ne montre que les packages Python
forge copy Micro.Py.Chunk --into .
forge copied .                    # versions en retard, fichiers retouchés localement
```

Un dépôt polyglotte se raccorde par sous-projet : `cd api && forge init .`,
`cd web && forge init .`.

[SPEC.md](SPEC.md) spécifie la méthode indépendamment du langage (MUST/SHOULD/MAY) :
micropackages purs et testés, artefacts immuables, contrat public dictant le SemVer,
anti-duplication à deux signaux, workflow d'agent opposable. Des implémentations
natives pour npm, PyPI ou Cargo restent possibles et bienvenues.

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

## Le corpus livré

Neuf micropackages, 233 tests. Ils ne sont **pas** publiés sur nuget.org — le corpus
est propre à chaque organisation (voir [SERVER.md](SERVER.md)).

| Package | Capacité |
|---------|----------|
| `Micro.Flow.Retry` | Relances asynchrones : backoff exponentiel, prédicat de relance, délai injectable, logging `ILogger` |
| `Micro.Text.Slugify` | Slug URL pur et déterministe |
| `Micro.Text.CompactDuration` | Parse `30s`, `5m`, `2h30m` en `TimeSpan` |
| `Micro.Text.UrlSanitizer` | Masque secrets et identifiants dans une URL avant journalisation |
| `Micro.Text.IdentifierCase` | Libellé humain → identifiant (camel, pascal, snake, kebab) |
| `Micro.Text.SearchMatch` | Un enregistrement répond-il à une recherche saisie par un humain |
| `Micro.Schema.SampleShape` | Arbre de forme d'un document JSON ou XML échantillon |
| `Micro.Graph.TopologicalSort` | Tri topologique d'un graphe orienté, regroupé en vagues |
| `Micro.Edit.History` | Historique d'édition borné : annuler / rétablir |

La plupart ont été **forgés par un agent IA** au cours de tests du workflow : il a
cherché, n'a rien trouvé, et les a créés avec tests et mode d'emploi. Deux comportaient
un défaut sur un cas limite non testé — corrigés depuis, et c'est ce constat qui a
donné naissance à `forge review` et au catalogue d'aléas.

Une version de `Micro.Text.Slugify` et une de `Micro.Text.UrlSanitizer` sont
dépréciées : les artefacts restent installables, l'alerte remonte aux consommateurs.
