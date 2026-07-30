# RULES.md — Règles immuables de MicroForge

> **CE FICHIER EST IMMUABLE.** Ni un humain ni une IA ne le modifie dans le cadre du
> workflow normal. Il est appliqué mécaniquement par `forge validate` : un package
> qui viole une règle **ne peut pas être publié**. Le fichier sert aussi de marqueur
> de racine pour l'outillage — le supprimer casse le système.

## R1 — Granularité : un package = une responsabilité

- Un micropackage expose **une** capacité générique (une classe statique d'entrée,
  éventuellement accompagnée de son type d'options et de ses types de résultat).
- Aucune logique métier, aucun type du projet appelant, aucune connaissance du
  contexte d'utilisation. Si la méthode ne se paramètre pas pour un autre projet,
  elle n'a pas sa place ici.

## R2 — Nommage

- `PackageId` au format strict **`Micro.<Domaine>.<Action>`** en PascalCase
  (ex. `Micro.Text.Slugify`, `Micro.Flow.Retry`, `Micro.Collections.Chunk`).
- Le dossier sous `packages/` porte exactement le nom du `PackageId`.
- La classe d'entrée porte un nom dérivé de l'action (`Slugifier`, `RetryExecutor`).

## R3 — Pureté et injection des effets

Tout effet non déterministe est **injecté**, jamais ambiant. API bannies dans `src/`
(liste appliquée par le validateur) :

| Interdit | Remplacement |
|----------|--------------|
| `Console.*` | `Microsoft.Extensions.Logging.ILogger` injecté |
| `DateTime.Now / UtcNow / Today` | `TimeProvider` ou `Func<DateTime>` injecté |
| `Thread.Sleep` | Stratégie de délai asynchrone injectée |
| `new Random()` sans graine | Source d'aléa ou graine en paramètre |
| `File.* / Directory.*` | Recevoir des `Stream` / `string` en paramètres |
| `Environment.Exit / GetEnvironmentVariable` | Valeurs passées en paramètres |
| `Process.Start` | Hors périmètre d'un micropackage |
| `.GetAwaiter().GetResult()` | Async de bout en bout |

## R4 — Logging par interface

- Les opérations multi-étapes ou susceptibles d'échec reçoivent un
  `Microsoft.Extensions.Logging.ILogger` (seule dépendance de logging autorisée).
- Les fonctions pures triviales (transformation de données) n'en reçoivent pas :
  le logging y serait du bruit.
- Jamais d'écriture directe (console, fichier, réseau) depuis un package.

## R5 — Qualité de compilation (packages/Directory.Build.props, immuable)

- `TreatWarningsAsErrors`, `Nullable enable`, analyseurs .NET niveau
  `latest-recommended`, documentation XML obligatoire sur toute l'API publique.
- Une suppression d'avertissement (`NoWarn`) exige un commentaire de justification
  dans le `.csproj`.

## R6 — Tests obligatoires, sur le binaire livré

- `tests/` contient au moins un `[Fact]`/`[Theory]` xUnit ; `dotnet test` doit
  passer **avant toute publication** (le validateur l'exécute).
- Les tests couvrent le cas nominal, les cas limites et les erreurs de paramétrage.
- Aucune attente réelle en test : les stratégies de délai injectées le permettent.
- **Tests et empaquetage utilisent la même configuration `Release`.** Tester en
  Debug puis livrer du Release reviendrait à valider un binaire et à en publier un
  autre ; la promesse « le package récupéré est celui qui a été prouvé » exige
  l'identité des deux builds.

### Ce qu'un package du feed garantit

Un `.nupkg` présent dans `feed/` a nécessairement franchi, dans cet ordre :

1. `dotnet test tests -c Release` — donc `src/` **compile en Release** et ses tests passent ;
2. `dotnet pack src -c Release` — l'empaquetage a réussi ;
3. l'extraction du contrat public — l'assembly livrée est **chargeable et lisible**.

Un package qui ne compile pas ne peut pas entrer dans le feed. Et comme le `.nupkg`
embarque la **DLL déjà compilée**, un consommateur ne recompile jamais le package :
il reçoit le binaire exact qui a été testé, à l'octet près.

Ce que cela ne garantit pas : la compatibilité de framework cible (tous les packages
visent `net8.0`) et la disponibilité des dépendances tierces au moment du `restore`.

## R7 — Mode d'emploi embarqué, et réellement rédigé

- `README.md` obligatoire, empaqueté dans le `.nupkg`, avec les sections exactes :
  `## Description`, `## Mode d'emploi`, `## Paramétrage`, `## Exemple`.
- `Description` (csproj) ≥ 30 caractères ; `PackageTags` ≥ 3 tags séparés par `;`
  — ce sont les données du moteur de recherche : les rédiger pour être trouvé.

Le **contenu** est contrôlé, pas seulement la présence des titres. Publier le gabarit
tel quel est refusé :

| Contrôle | Exigence |
|----------|----------|
| Longueur utile | ≥ 40 caractères par section, commentaires HTML du gabarit exclus |
| Gabarit résiduel | Aucune occurrence de « à compléter », « TODO », « lorem ipsum » |
| Exemple | Un bloc ```` ```csharp ```` avec au moins deux instructions réelles |
| Exemple pertinent | Il doit citer le package qu'il illustre |
| Paramétrage | Un tableau des options, ou la mention explicite « aucun paramètre » |

Raison : le README est la **seule** chose qu'une IA lit avant de décider de réutiliser
un package. Un mode d'emploi vide rend le package invisible en pratique, et la
bibliothèque se remplit de coquilles qui polluent la recherche.

## R8 — Immutabilité des versions publiées

- Un `.nupkg` publié dans `feed/` **n'est jamais modifié ni supprimé**.
- Toute évolution passe par un nouveau numéro SemVer :
  - correctif de bug → patch (`1.0.1`)
  - ajout rétrocompatible (nouvelle option) → mineur (`1.1.0`)
  - rupture de contrat → majeur (`2.0.0`)
- Le validateur refuse la republication d'une version existante.

## R9 — Le contrat public dicte l'incrément

À chaque publication, le contrat public (types et membres visibles) est extrait de
l'assembly et comparé à celui du prédécesseur immédiat. L'incrément SemVer déclaré
doit être **au moins** celui qu'exige la différence :

| Différence de contrat | Incrément minimal exigé |
|-----------------------|-------------------------|
| Aucune | patch |
| Membres ajoutés seulement | mineur |
| Au moins un membre retiré ou modifié | majeur |

Un incrément insuffisant fait **échouer la publication**. Le SemVer cesse ainsi
d'être une convention déclarative pour devenir une propriété vérifiée : un
consommateur qui monte de `1.2.0` à `1.9.0` a la garantie mécanique que sa
compilation ne casse pas.

Le contrat de chaque version est enregistré sous forme d'empreinte
(`sha256:…`, le « digest ») dans `registry/api/`. Deux versions au même digest sont
interchangeables à la compilation.

## R10 — Pas de quasi-doublon

Avant publication, **deux signaux indépendants** sont confrontés :

1. **La description** est vectorisée et comparée à celle de tous les packages publiés.
   Au-delà du seuil de blocage calibré pour l'espace vectoriel courant, la publication
   est refusée.
2. **La forme du contrat public** est comparée aux contrats déjà enregistrés, une fois
   les identifiants effacés. `ToSlug(string, Options) : string` et
   `Create(string, Options) : string` ont la même forme.

Le second rattrape ce que le premier laisse passer : une description peut se
reformuler pour tromper la comparaison lexicale, une signature non. Une proximité de
description confirmée par une forme de contrat identique **devient bloquante**.

La forme d'API ne déclenche jamais un refus à elle seule : beaucoup de packages sans
rapport exposent `(string) : string`, et un tel signal produirait trop de faux
positifs.

C'est la garantie centrale du système : sans elle, la bibliothèque accumulerait des
variantes du même snippet et perdrait sa raison d'être. La dérogation
`--allow-similar` existe mais doit rester exceptionnelle et justifiée.

## R11 — Dépréciation sans altération

Un package obsolète, buggé ou fusionné dans un autre se **déprécie**, il ne se
supprime pas :

```
forge deprecate <Id> --reason "…" [--versions "<2.0.0"] [--replacement <Id>]
```

La déclaration vit dans `registry/deprecations.json`, **hors des artefacts**. Les
`.nupkg` publiés restent bit à bit identiques et installables : aucun build existant
n'est cassé rétroactivement. Les consommateurs sont alertés via `forge outdated`.
Une dépréciation est réversible (`forge undeprecate`) ; un artefact ne l'est pas.

## R12 — Modification humaine

Un humain peut ajouter ou faire évoluer un micropackage librement, **par le même
chemin que l'IA** : éditer les sources sous `packages/`, incrémenter la version,
puis `forge publish`. Personne ne contourne le validateur, personne n'écrit
directement dans `feed/` ni dans `registry/`.

## R13 — Ce qui est dérivé, et ce qui ne l'est pas

- **Source de vérité** : `feed/` (artefacts) et `packages/` (sources).
- **Entièrement dérivé et régénérable par `forge index`** : `registry/index.json`,
  `registry/api/`, `registry/embeddings.json`. Supprimer ces fichiers ne perd
  aucune information.
- **Seule exception** : `registry/deprecations.json` porte une décision humaine
  qu'aucun artefact ne contient. Il n'est pas régénérable — c'est le seul fichier
  du registre à sauvegarder.
