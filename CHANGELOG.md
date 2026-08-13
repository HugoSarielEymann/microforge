# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/). Versions : SemVer.

## [0.9.1] — 2026-08-13

### Corrigé

- **`forge init` écrivait des instructions .NET dans tous les projets.** Un dépôt
  Python recevait « utiliser `dotnet add package` », une commande qui n'y existe pas,
  et une source NuGet sans consommateur. `forge init` reconnaît désormais l'écosystème
  du dossier et adapte le bloc : consommation par `forge copy`, conventions de test du
  langage, marqueur d'aléa `# hazard: <id>`, maintenance par `forge copied`, et la
  dégradation du profil annoncée en tête. `--language` force le profil.

- **La recherche mélangeait les écosystèmes.** L'index ne portait pas le langage
  (il était rangé dans le champ « auteurs »), si bien qu'un projet C# pouvait se
  voir proposer un package Python — inutilisable, et copiable à tort. `IndexEntry`
  porte désormais le langage ; `forge search` déduit l'écosystème du projet courant
  (`.csproj`, `pyproject.toml`, `go.mod`, `Cargo.toml`, `package.json`…), l'annonce,
  et `--language all` lève le filtre. Le langage apparaît dans chaque résultat.

  Un dossier non reconnu ne filtre pas : mieux vaut tout montrer que masquer à tort.


## [0.9.0] — 2026-08-02

### Ajouté

- **Multi-langage, par deux profils.** Un package déclare son écosystème dans
  `microforge.json` (Python, TypeScript, JavaScript, Go, Rust) ; le `.csproj`
  continue de tenir ce rôle en C#.

  - **Profil vérifié** (C#/.NET) : inchangé. NuGet, analyseur Roslyn, contrat public
    extrait, SemVer opposable, binaire déjà testé.
  - **Profil de base** : structure, README et tests validés ; recherche,
    anti-duplication et aléas fonctionnent normalement ; consommation par
    `forge copy`.

  **La dégradation est explicite** : `forge new --language python` énonce ce que
  l'écosystème ne garantit pas, et `forge publish` répète que le SemVer n'y est
  pas vérifié. Jamais de fausse assurance.
- `forge new --language <id>`, index conscient des packages sans artefact,
  détection de tests et d'aléas par convention d'écosystème
  (`# hazard: <id>` là où il n'y a pas d'attribut).

### Corrigé

- `forge review` signalait `Deconstruct`, `value__` et `GetHashCode` —
  générés par le compilateur, jamais écrits par l'auteur. Trois remarques sur cinq
  portaient sur du code que personne n'avait tapé, ce qui noyait les vraies.
- Le contrôle du README exigeait un bloc `csharp`, excluant de fait tout package
  écrit ailleurs qu'en .NET.


## [0.8.0] — 2026-08-02

### Ajouté

- **`forge copy <Id>`** : distribution par copie de source, pour les projets qui ne
  peuvent pas référencer un package. Un manifeste de **provenance**
  (`.microforge/copied.json`) enregistre version et empreinte de chaque fichier posé
  — c'est ce qui distingue une copie d'un copier-coller. Sans lui, le code devient
  orphelin : plus de mise à jour, plus de détection de divergence.
- **`forge copied [<projet>]`** : versions en retard et fichiers retouchés
  localement. Une recopie qui écraserait une modification est **refusée** sauf
  `--force` : c'est le seul moment où l'on peut encore arbitrer.
- Les dépendances NuGet du package sont annoncées à la copie : elle apporte le code,
  pas ce dont il dépend.

La référence NuGet reste préférable partout où elle est possible — binaire déjà
testé, contrat vérifié, correction propagée par une commande. La copie est un repli,
et les messages le disent.


## [0.7.0] — 2026-08-02

### Ajouté

- **`forge report [<projet>]`** : ce que la bibliothèque a apporté **à un projet** —
  code non réécrit, packages forgés depuis ce projet, activité de recherche. Distinct
  de `forge stats`, qui juge la rentabilité de la bibliothèque entière : vu d'un
  projet, tout package réutilisé compte dès le premier usage ; vu de la bibliothèque,
  ce premier usage ne fait rien économiser. Le rapport le dit explicitement.
- Le **journal d'usage enregistre le dossier courant**, ce qui permet d'attribuer une
  recherche ou une publication au projet qui l'a motivée. Les entrées antérieures sont
  ignorées plutôt qu'attribuées au hasard.

### Modifié

- Le bloc d'instructions agent couvre désormais **`forge hazards`** (déclarer les
  aléas et les prouver par des traits) et **`forge review`** (relire ce que le
  validateur ne sait pas juger), tous deux absents depuis leur introduction.


## [0.6.0] — 2026-08-02

### Ajouté

- **Analyseur Roslyn** (`MicroForge.Analyzers`, diagnostics `MFG001`–`MFG008`).
  Il remplace une détection par expression régulière qui se contournait trivialement :
  `using C = System.Console;` passait au travers. L'analyse porte sur le **symbole
  résolu**, si bien qu'alias, `using static` et noms partiellement qualifiés sont
  sans effet. Les violations apparaissent **dans l'IDE, à la frappe** — pour le
  développeur humain autant que pour l'agent. Vérifié : les sept motifs d'évasion sont
  attrapés, `new Random(42)` (avec graine) ne l'est pas, et les quatre micropackages
  existants ne produisent aucun faux positif.
- **`forge sign`** : signature RSA-3072 du registre d'empreintes. Sans elle, qui peut
  réécrire un artefact peut réécrire son empreinte ; avec, falsifier l'ensemble exige
  la clé privée — qui ne réside jamais dans la bibliothèque. Ce n'est pas une signature
  NuGet reconnue par l'écosystème, et RULES.md le dit explicitement.
- **Indexation incrémentale** : `forge index` ne décompresse plus que les artefacts
  nouveaux ou modifiés. La validité d'une entrée repose sur la taille et la date du
  fichier, jamais sur son seul nom. Le gain est nul à douze artefacts et se compte en
  minutes à plusieurs milliers.


## [0.5.0] — 2026-08-02

### Ajouté

- **`forge create [<dossier>]`** : crée une bibliothèque vierge et la mémorise.
  Adopter MicroForge imposait jusqu'ici de cloner le dépôt de référence, donc
  d'hériter d'un corpus qui n'est pas le sien. Or partir vide est le cas normal :
  le corpus est propre à chaque organisation.

### Sécurité

- **Traversée de répertoire lors d'un `forge pull`.** La version rapatriée provient
  de l'index de versions du **dépôt distant** ; elle servait à construire un chemin
  de fichier sans contrôle. Un dépôt compromis pouvait faire écrire hors du feed.
  `FeedMirror.ResolveArtifactPath` valide désormais le nom et vérifie que le chemin
  résolu reste dans le feed.
- **Secrets en clair dans le journal d'usage.** `forge remote --source
  https://user:motdepasse@depot` écrivait le mot de passe dans
  `registry/usage.log`. Les identifiants d'URL et les valeurs suivant
  `--api-key`, `--password` ou `--token` sont masqués avant écriture.

### Corrigé

- `demo/nuget.config`, `demo/CLAUDE.md` et `demo/.github/` étaient versionnés
  avec des **chemins absolus du poste de l'auteur** : un clone pointait vers un
  dossier inexistant. Ils sont désormais ignorés et régénérés par `forge init demo`.
- Chemins personnels retirés de la documentation.

## [0.4.1] — 2026-08-02

### Modifié

- **Attribution renforcée.** À défaut de marque déposable, la mention d'auteur portée
  par le NOTICE est la seule protection de l'auteur : `LICENSE` et `NOTICE` sont
  désormais embarqués dans **chaque** micropackage, et l'auteur, le copyright, la
  licence et l'URL du dépôt sont centralisés dans `packages/Directory.Build.props`.
  Les quatre packages ont été republiés (contrats identiques, montées sûres).
- **Débogage des micropackages.** `DebugType=embedded` et `EmbedAllSources` :
  symboles et sources sont dans l'assembly. On entre dans le code d'un micropackage
  au débogueur comme dans du code du projet — sans serveur de symboles, sans
  SourceLink, hors ligne.

### Ajouté

- **LICENSING.md** : pourquoi Apache-2.0 plutôt que l'AGPL, et ce qui reste réservé.
- **CLA.md** : accord de contribution, nécessaire pour préserver la possibilité d'une
  licence différente sur les évolutions futures.

## [0.4.0] — 2026-07-31

### Ajouté

- **Bibliothèque d'aléas de test** (`forge hazards`). Un aléa est une classe d'entrées
  dangereuses — `null-input`, `numeric-overflow`, `secret-leak`, `unicode-edge`… —
  éprouvée une fois et réutilisable partout. Douze aléas livrés d'origine ; le
  catalogue s'enrichit par `forge hazards add` et se versionne avec le dépôt.

  Un package déclare ses aléas (`forge hazards declare`) et doit les **prouver** par
  des tests portant `[Trait("hazard", "<id>")]` — sinon `forge validate` refuse la
  publication. C'est ce qui rend l'obligation mécanique plutôt que déclarative.

  `forge review` signale en outre les aléas éprouvés mais non déclarés : le corpus en
  sait parfois plus que ses métadonnées.
- **SERVER.md** : héberger un dépôt d'équipe, validé en conditions réelles (BaGet en
  Docker, cycle push/pull complet, artefacts bit à bit identiques après rapatriement).

### Corrigé

- `Cli.Run` lisait les flux de sortie l'un après l'autre : un échec de sous-processus
  pouvait être rapporté **sans aucun message**, masquant la cause réelle. Les deux
  flux sont désormais lus simultanément, et le code de sortie est affiché à défaut.

## [0.3.0] — 2026-07-31

### Ajouté

- **`forge review <Id>`** : compile le package, extrait son contrat public et le
  confronte aux tests. Signale les membres publics jamais cités, les exceptions
  documentées jamais provoquées, les méthodes `TryXxx` (qui ne doivent jamais lever)
  et les signatures numériques sans test aux bornes.

  Répond à la limite mesurée en manche 4 : le validateur garantit que des tests
  existent et passent, jamais qu'ils couvrent les bons cas. La commande **ne tranche
  pas** — elle rassemble les questions pour un relecteur, de préférence un agent
  distinct de celui qui a forgé.
- **ROADMAP.md** : ce qui bloque une release, ce qui est reporté, ce qui a été écarté.

## [0.2.1] — 2026-07-31

### Corrigé

- `forge init` et `forge bench start` refusent désormais un **dossier parent**
  (plus de 3 projets ou 5 000 fichiers) : lancés sur `source\repos`, ils déposaient
  des instructions valables pour des dizaines de dépôts sans rapport et tentaient
  d'empreindre 133 000 fichiers. `--force` reste possible en connaissance de cause.

## [0.2.0] — 2026-07-31

### Ajouté

- **`forge bench`** (`start` / `report` / `list`) : encadre une manche de test avec un
  agent IA. Capture l'état du projet, puis mesure fichiers et lignes produits,
  packages réutilisés, packages forgés, et la trace des commandes forge invoquées.
- **Journal d'usage** (`registry/usage.log`) : chaque invocation est consignée —
  la preuve que l'agent cherche avant d'écrire se lit dans la trace.
- **`forge pull`** : rapatrie les artefacts du dépôt d'équipe vers le feed local
  (dossier partagé : tout ou par package ; HTTP NuGet v3 : par package). Modèle du
  clone : chaque poste travaille sur sa copie, jamais de réécriture d'un artefact
  présent.
- **`forge verify [--adopt]`** : empreintes SHA-256 des artefacts, détection du dépôt
  manuel, de la falsification et de la suppression.
- **`forge doctor`**, **`forge remote`/`push`**, **`forge use`**, **`forge --version`**.
- **SPEC.md** : la méthode, spécifiée indépendamment du langage (MUST/SHOULD/MAY).
- Distribution en **outil .NET global** (`MicroForge.Cli`, commande `forge`) —
  Windows, macOS, Linux. Remplace lanceur .cmd et manipulation du PATH.
- Licence **Apache-2.0** + NOTICE (attribution préservée sur toute redistribution).

### Modifié

- La qualité du README est contrôlée (contenu, pas seulement titres) : gabarit non
  rédigé refusé à la publication (R7 durcie).
- L'anti-duplication confronte aussi la **forme du contrat public** ; une proximité
  de description confirmée par un contrat identique devient bloquante (R10 durcie).
- Les tests des micropackages s'exécutent en configuration Release — celle de
  l'artefact livré (R6 durcie).
- Le bloc d'instructions agent est autonome (plus de renvoi vers AGENT.md pour la
  moitié « forger »).

## [0.1.0] — 2026-07-25

Première version : CLI (search/info/new/validate/publish/bump/diff/deprecate/
outdated/update/stats), feed NuGet local immuable, contrats publics extraits et
digests, incrément SemVer vérifié, anti-duplication par description (Ollama ou repli
lexical calibré), dépréciation hors artefacts, mise à jour des consommateurs validée
par leur suite de tests avec restauration, métriques conservatrices, `forge init`,
règles R1–R13 appliquées par le validateur, 2 micropackages d'exemple.
