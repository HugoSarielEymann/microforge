# Installation de MicroForge

Prérequis unique : **.NET SDK 8 ou supérieur** (`dotnet --list-sdks`).

Deux notions à distinguer avant de commencer :

- **L'outil** (`forge`) s'installe depuis nuget.org, comme n'importe quel outil .NET.
- **La bibliothèque** (le dossier `MicroForge/` avec ses `packages/`, son `feed/` et
  son `registry/`) est *votre* corpus. L'outil doit savoir où elle se trouve.

Ce sont deux choses séparées : plusieurs bibliothèques peuvent coexister, et l'outil
en désigne une à la fois.

---

## PC neuf : installer l'outil

```bash
dotnet tool install --global MicroForge.Cli
forge --version
```

Rien d'autre : ni PATH à modifier, ni script à lancer. Fonctionne à l'identique sur
Windows, macOS et Linux.

---

## Puis : d'où vient votre bibliothèque ?

Deux situations. **Le cas courant est le premier.**

### A — Vous partez de zéro

```bash
forge create ~/microforge
```

Crée une bibliothèque vierge et la mémorise : dossiers `feed/`, `registry/`,
`packages/`, plus `RULES.md`, `Directory.Build.props` et un `.gitignore` adaptés.
Le catalogue des 12 aléas de test est disponible immédiatement.

Elle est vide, et c'est normal : le corpus est **propre à chaque organisation**. Le
« générique » d'une équipe finance n'est pas celui d'une équipe jeux. La bibliothèque
se remplit au fil du travail, chaque fois qu'un besoin générique n'y trouve pas de
réponse.

Pensez à renseigner `Authors` et `Copyright` dans `packages/Directory.Build.props` :
ils voyageront avec chaque artefact publié.

### B — Vous rejoignez une bibliothèque existante

```bash
git clone <dépôt de votre équipe>
cd <dépôt>
./setup.ps1          # reconstruit feed, index et contrats depuis packages/
forge use .          # désigne cette bibliothèque
```

Un dépôt ne contient que les **sources** : `feed/` et `registry/` sont ignorés par
Git parce qu'ils se reconstruisent. Si votre équipe a un dépôt NuGet d'artefacts,
`forge remote` puis `forge pull` évitent même la recompilation — voir
[SERVER.md](SERVER.md).

Le chemin retenu est mémorisé dans `~/.microforge/root` : `forge` fonctionne ensuite
depuis n'importe quel dossier. C'est **la** configuration qui répond à « où sont
stockés les micropackages ».

---

## Enfin : source NuGet et instructions IA globales

```powershell
.\install.ps1 -WhatIf     # montre ce qui serait fait
.\install.ps1
```

| Action | Fichier / réglage touché | Désactiver avec |
|--------|--------------------------|-----------------|
| Prépare la bibliothèque (`setup.ps1`) | rien hors du dossier | `-SkipSetup` |
| Installe/actualise l'outil et mémorise la racine | outil global, `~/.microforge/root` | `-SkipTool` |
| Source NuGet machine | config NuGet utilisateur | `-SkipNuGetSource` |
| Instructions IA globales | `~/.claude/CLAUDE.md` | `-SkipGlobalAgent` |

La dernière rend le réflexe automatique : ce `CLAUDE.md` est chargé par **toute**
session Claude Code, quel que soit le projet ouvert.

Vérification :

```bash
forge doctor
forge list
```

`forge doctor` contrôle chaque point et indique quoi corriger. C'est le premier
réflexe en cas de doute.

---

## Où sont stockés les micropackages

Trois emplacements, trois rôles :

| Quoi | Où | Configuré par |
|------|-----|---------------|
| **Sources** (`packages/<Id>/`) | dans la bibliothèque | l'emplacement du clone |
| **Artefacts** (`feed/*.nupkg`) | dans la bibliothèque | reconstruits par `setup.ps1` |
| **Quelle bibliothèque ?** | `~/.microforge/root` | `forge use <chemin>` |

Trois moyens de désigner la bibliothèque, par ordre de priorité :

1. La variable d'environnement `MICROFORGE_ROOT` (ponctuel, scripts, CI)
2. La remontée de dossiers depuis le répertoire courant (quand on travaille dedans)
3. Le chemin mémorisé par `forge use` (le cas courant)

Pour qu'un **projet** puisse installer les packages, il lui faut la source NuGet :
c'est `forge init .` qui l'écrit dans son `nuget.config`. Pour partager les artefacts
entre plusieurs machines, voir [SERVER.md](SERVER.md).

---

## Une fois par projet

Depuis le dossier du projet :

```powershell
forge init .
```

Idempotent : relancer ne duplique rien, et si vous déplacez MicroForge, un nouvel
`init` corrige les chemins. Trois fichiers sont écrits **dans le projet** :

| Fichier | Rôle | Si le fichier existe déjà |
|---------|------|---------------------------|
| `nuget.config` | Déclare la source `MicroForge` → `…\MicroForge\feed` | La source est ajoutée, les autres sont préservées |
| `CLAUDE.md` | Instructions pour Claude Code | Bloc `<!-- microforge:begin -->…<!-- microforge:end -->` inséré ou actualisé, le reste intact |
| `.github\copilot-instructions.md` | Instructions pour GitHub Copilot | Idem — contenu identique à `CLAUDE.md` |

Drapeaux : `--no-nuget-config`, `--no-agent-instructions`, `--agents "Claude"` (ou
`"Copilot"`) pour n'écrire les instructions que d'un seul outil.

Le bloc écrit est **autonome** : il contient l'intégralité du workflow actionnable
(chercher, réutiliser, forger, publier, maintenir), sans exiger l'ouverture d'un
autre fichier. Les agents ne suivent pas de façon fiable un chemin de fichier cité en
référence, surtout hors du dossier de travail.

### Projet hors .NET

`forge init` reconnaît l'écosystème du dossier (`pyproject.toml`, `go.mod`,
`Cargo.toml`, `tsconfig.json`, `package.json`…) et s'adapte **sans rien vous
demander** :

```
> forge init .
  écosystème    Python — profil de base : structure et tests validés,
                distribution par copie, SemVer déclaratif
                consommation par « forge copy » : pas de source NuGet à poser ici
```

Ce qui change :

| | Profil vérifié (.NET) | Profil de base (autres) |
|---|---|---|
| `nuget.config` | écrit | **non écrit** — sans objet |
| Consommation | `dotnet add package` | `forge copy <Id> --into .` |
| Instructions IA | référence NuGet, xUnit, `[Trait("hazard", …)]` | copie, conventions du langage, `# hazard: <id>` |
| Maintenance | `forge outdated` / `forge update` | `forge copied` |
| Dégradation | — | annoncée en tête du bloc d'instructions |

Un dossier dont l'écosystème n'est pas reconnu est traité comme .NET, et le dit.
`forge init . --language python` force le profil.

Un dépôt **polyglotte** se raccorde par sous-projet : `cd api && forge init .`,
`cd web && forge init .`. Chacun reçoit les instructions de son écosystème.

---

## Ensuite — rien à faire

L'agent lit son fichier d'instructions, qui le renvoie vers
[AGENT.md](AGENT.md) — le workflow complet et obligatoire. Il applique le cycle
seul : chercher avant d'écrire, réutiliser, forger si nécessaire, publier.

Vous pouvez formuler votre demande normalement (« écris-moi un service qui… ») ;
c'est l'agent qui interroge la forge.

### Pour un agent qui ne lit aucun de ces fichiers

`AGENT.md` est volontairement générique — il ne mentionne aucun outil particulier.
Coller son contenu dans les instructions système de l'agent suffit. Les seuls
prérequis côté agent : savoir exécuter des commandes shell (`forge`, `dotnet`) et
lire les fichiers du projet.

---

## Recherche sémantique (optionnelle)

**Rien n'est requis.** Sans installation, MicroForge utilise un repli lexical
déterministe intégré au CLI — pas un modèle, un algorithme de hachage de n-grammes
en C#. Il tourne sur CPU en microsecondes et fonctionne hors ligne.

Pour détecter la **synonymie** (« découper » ↔ « chunk »), qui échappe au repli :

```powershell
winget install Ollama.Ollama
ollama serve
ollama pull nomic-embed-text
```

`nomic-embed-text` : ~137 M paramètres, ~274 Mo, **conçu pour tourner sur CPU**.
Aucune donnée ne quitte la machine. MicroForge le détecte automatiquement et
recalibre ses seuils de duplication.

Pointer un autre modèle ou un autre hôte :

```powershell
$env:MICROFORGE_EMBED_MODEL = "mxbai-embed-large"
$env:MICROFORGE_EMBED_ENDPOINT = "http://localhost:11434"
```

Forcer le repli (utile en CI) : ajouter `--offline` aux commandes `search`,
`publish`, `index`, `duplicates`.

---

## Les fichiers que vous manipulez

### Dans MicroForge

| Chemin | Quand y toucher |
|--------|-----------------|
| `packages\<Id>\src\` | Écrire ou modifier le code d'un micropackage |
| `packages\<Id>\tests\` | Ses tests (obligatoires, doivent passer) |
| `packages\<Id>\README.md` | Son mode d'emploi (4 sections obligatoires) |
| `packages\<Id>\src\<Id>.csproj` | Version, description, tags (ou via `forge bump`) |
| `RULES.md` | **Jamais** — immuable, opposable par le validateur |
| `AGENT.md` | **Jamais** — workflow imposé aux IA |
| `packages\Directory.Build.props` | **Jamais** — qualité imposée à tous |
| `feed\*.nupkg` | **Jamais** — artefacts immuables |
| `registry\index.json`, `registry\api\`, `registry\embeddings.json` | Jamais à la main — dérivés, régénérés par `forge index` |
| `registry\deprecations.json` | Via `forge deprecate` / `undeprecate` uniquement. **Non régénérable : à sauvegarder.** |
| `registry\consumers.json` | Écrit par `forge init` ; sert aux métriques. Se reconstitue en relançant `forge init` dans chaque projet. |
| `registry\usage.log` | Journal des invocations (alimente `forge bench`). Purgeable sans risque. |
| `registry\artifacts.json` | Empreintes des artefacts (`forge verify`). Se reconstitue par `forge verify --adopt`. |
| `registry\bench\` | Manches de test capturées (`forge bench`). |
| `registry\remote.json` | Dépôt d'équipe (`forge remote`). Jamais de clé d'API dedans. |

### Où voir les packages créés

| Ce que vous cherchez | Où |
|----------------------|-----|
| Inventaire lisible | `forge list`, puis `forge info <Id>` pour le détail |
| Artefacts livrables | `MicroForge\feed\*.nupkg` — un fichier par version publiée |
| Code source d'un package | `MicroForge\packages\<Id>\src\` |
| Contrat public et digest | `forge info <Id>`, ou `MicroForge\registry\api\<Id>\<version>.json` |
| Rentabilité | `forge stats` |

Dans **Visual Studio** : rien à configurer. `install.ps1` a écrit la source dans
`~/AppData/Roaming/NuGet/NuGet.Config` (Windows) ou `~/.nuget/NuGet/NuGet.Config`, le fichier utilisateur que VS
lit au démarrage. Les packages apparaissent dans *Gérer les packages NuGet* en
choisissant la source **MicroForge** dans la liste déroulante. Si VS était ouvert
pendant l'installation, le redémarrer.

### Hors de MicroForge

| Chemin | Écrit par | Rôle |
|--------|-----------|------|
| `~\.microforge\root` | `forge use` | **Quelle bibliothèque l'outil utilise** |
| outil global `MicroForge.Cli` | `dotnet tool install` | La commande `forge` |
| `~\.claude\CLAUDE.md` | `install.ps1` | Réflexe MicroForge dans toute session IA |
| config NuGet utilisateur | `install.ps1` | Source `MicroForge` visible partout |
| `<projet>\nuget.config` | `forge init` | Source visible depuis ce projet |
| `<projet>\CLAUDE.md` | `forge init` | Instructions pour Claude Code |
| `<projet>\.github\copilot-instructions.md` | `forge init` | Instructions pour GitHub Copilot |
| `<projet>\*.csproj` | `forge update` | Versions épinglées des micropackages |

---

## Sauvegarde et migration

À sauvegarder : `packages/` (les sources) et `registry/deprecations.json`. Le reste
se régénère — `feed/` par `setup.ps1`, l'index et les contrats par `forge index`.

Si vous déplacez le dossier : `forge use <nouveau chemin>`, puis `forge init .` dans
chaque projet raccordé pour corriger le chemin de la source NuGet.

---

## Mettre à jour

```bash
dotnet tool update --global MicroForge.Cli   # l'outil
git pull && .\setup.ps1                      # la bibliothèque
```

---

## Désinstallation

```bash
dotnet tool uninstall --global MicroForge.Cli
dotnet nuget remove source MicroForge
```

Puis supprimer `~/.microforge/`, retirer le bloc
`<!-- microforge:begin -->…<!-- microforge:end -->` des fichiers d'instructions, et
l'entrée `MicroForge` des `nuget.config` de projet.

---

## Diagnostic

`forge doctor` répond à la plupart des questions. Sinon :

| Symptôme | Cause probable | Correction |
|----------|----------------|------------|
| `forge` : commande inconnue | Terminal ouvert avant l'installation | Ouvrir un nouveau terminal |
| `Racine MicroForge introuvable` | L'outil ne sait pas où est la bibliothèque | `forge use <chemin>` |
| `dotnet add package` : introuvable | Source NuGet non déclarée dans le projet | `forge init .` |
| `Contrat non extrait` | Dépendance non restaurée | `dotnet restore` puis `forge index` |
| Doublons non détectés entre synonymes | Repli lexical actif | Installer `nomic-embed-text` (voir plus haut) |
