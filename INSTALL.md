# Installation de MicroForge

**Deux commandes en tout** : une fois par machine, une fois par projet.

Prérequis : .NET SDK 8 ou supérieur (`dotnet --list-sdks`).

---

## Étape 1 — Une fois par machine

```powershell
cd C:\Users\hugoe\Documents\Widgets\MicroForge
.\install.ps1 -WhatIf     # montre ce qui serait fait, sans rien modifier
.\install.ps1             # applique
```

L'installateur enchaîne tout seul quatre choses. Seules les trois dernières sortent
du dossier MicroForge :

| # | Action | Fichier / réglage touché | Désactiver avec |
|---|--------|--------------------------|-----------------|
| 0 | Prépare la bibliothèque (`setup.ps1`) | rien hors de `MicroForge\` | `-SkipSetup` |
| 1 | Lanceur `forge` | `C:\Users\hugoe\.local\bin\forge.cmd` + PATH utilisateur | `-SkipShim` |
| 2 | Source NuGet globale | config NuGet utilisateur | `-SkipNuGetSource` |
| 3 | Instructions IA globales | `C:\Users\hugoe\.claude\CLAUDE.md` | `-SkipGlobalAgent` |

L'étape 0 compile le CLI, exécute ses 247 tests, publie les micropackages sources
dans le feed et régénère index, contrats et vecteurs. L'étape 3 rend le réflexe
automatique : le `CLAUDE.md` global est chargé par **toute** session Claude Code,
quel que soit le projet.

`C:\Users\hugoe\.local\bin` est déjà sur votre PATH. **Ouvrir un nouveau terminal**
ensuite, pour que `forge` soit reconnu.

Vérification :

```powershell
forge list
forge search "relancer un appel http qui echoue"
```

---

## Étape 2 — Une fois par projet

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

---

## Étape 3 — Rien à faire

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
`C:\Users\hugoe\AppData\Roaming\NuGet\NuGet.Config`, le fichier utilisateur que VS
lit au démarrage. Les packages apparaissent dans *Gérer les packages NuGet* en
choisissant la source **MicroForge** dans la liste déroulante. Si VS était ouvert
pendant l'installation, le redémarrer.

### Hors de MicroForge

| Chemin | Écrit par | Rôle |
|--------|-----------|------|
| `C:\Users\hugoe\.local\bin\forge.cmd` | `install.ps1` | Lanceur global |
| `C:\Users\hugoe\.claude\CLAUDE.md` | `install.ps1` | Réflexe MicroForge dans toute session IA |
| config NuGet utilisateur | `install.ps1` | Source `MicroForge` visible partout |
| `<projet>\nuget.config` | `forge init` | Source visible depuis ce projet |
| `<projet>\CLAUDE.md` | `forge init` | Instructions pour Claude Code |
| `<projet>\.github\copilot-instructions.md` | `forge init` | Instructions pour GitHub Copilot |
| `<projet>\*.csproj` | `forge update` | Versions épinglées des micropackages |

---

## Sauvegarde et migration

À sauvegarder : `packages\` (les sources), `feed\` (les artefacts) et
`registry\deprecations.json`. Tout le reste de `registry\` se régénère par
`forge index`.

Si vous déplacez le dossier MicroForge : relancer `.\install.ps1` puis `forge init .`
dans chaque projet raccordé — les chemins sont corrigés automatiquement.

---

## Désinstallation

```powershell
Remove-Item C:\Users\hugoe\.local\bin\forge.cmd
dotnet nuget remove source MicroForge
```

Puis retirer le bloc `<!-- microforge:begin -->…<!-- microforge:end -->` des
`CLAUDE.md` concernés, et l'entrée `MicroForge` des `nuget.config` de projet. Les
packages déjà installés continuent de fonctionner tant que le dossier `feed\` existe.

---

## Diagnostic

| Symptôme | Cause probable | Correction |
|----------|----------------|------------|
| `forge` : commande inconnue | Terminal ouvert avant l'installation | Ouvrir un nouveau terminal |
| `Racine MicroForge introuvable` | Lancement hors du dossier sans lanceur | `$env:MICROFORGE_ROOT = "C:\...\MicroForge"` |
| `dotnet add package` : introuvable | Source NuGet non déclarée | `forge init .` dans le projet |
| `Contrat non extrait` | Dépendance non restaurée | `dotnet restore` puis `forge index` |
| Doublons non détectés entre synonymes | Repli lexical actif | Installer `nomic-embed-text` (voir plus haut) |
