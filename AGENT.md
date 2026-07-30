# AGENT.md — Workflow IA MicroForge

Instructions opérationnelles pour toute IA qui génère du code C# dans un
environnement équipé de MicroForge. Objectif : **ne jamais générer deux fois le
même code générique**. Chaque snippet réutilisable devient un micropackage NuGet ;
la prochaine demande le retrouve par recherche au lieu de le régénérer.

Outil : `forge` (wrapper `forge.ps1` à la racine MicroForge). Règles : `RULES.md`.

## Le workflow en 5 étapes (obligatoire)

### Étape 1 — Décomposer la demande

Découper la demande utilisateur en étapes élémentaires. Pour chacune, se demander :
« cette étape est-elle un besoin générique (indépendant du métier) ou spécifique
au projet ? » Exemples génériques : retry, slugification, pagination, découpage en
lots, validation d'email, mapping clé/valeur, parsing de durée…

### Étape 2 — Vérifier la bibliothèque standard, puis chercher AVANT d'écrire

**D'abord la BCL.** Avant toute recherche dans MicroForge, se demander si .NET couvre
déjà le besoin : `Enumerable.Chunk` (découpage en lots), `string.Split`,
`TimeSpan.TryParse`, `Convert`, `Path`, `Regex`, `System.Text.Json`, `HttpClient`…
Si oui, l'utiliser directement. **Un micropackage qui double le framework est une
dette, pas un actif** — il ajoute une dépendance sans rien apporter, et il polluera
les recherches futures.

Ensuite seulement, pour **chaque** étape générique restante, interroger la bibliothèque :

```
forge search retry backoff http
forge search slug url --tags text
```

La recherche est **hybride** : correspondance lexicale (identifiant, tags,
description, README) combinée à une similarité vectorielle. Formuler la requête en
langage naturel fonctionne donc aussi bien qu'une liste de mots-clés.

- Résultats pertinents → lire le mode d'emploi : `forge info <PackageId>`.
- Le package répond au besoin → l'installer et **le paramétrer, sans le recoder** :

```
dotnet add package Micro.Flow.Retry --version 1.1.0
```

  Toujours épingler une version exacte : les builds restent reproductibles et les
  montées deviennent des actes délibérés (voir étape 6).
- Le package répond *presque* (il manque une option) → ne pas dupliquer : étendre
  le package existant de façon rétrocompatible, publier une version mineure,
  puis l'utiliser.
- **`forge search` répond « Aucun micropackage ne correspond : candidat à la création »**
  → passer aux étapes 3 et 4 pour le forger. **Ne pas écrire ce code en ligne dans le
  projet.** Ce message n'est pas informatif, c'est une instruction : sans forge, le
  besoin sera régénéré au projet suivant, et la bibliothèque ne grandira jamais.

**Interdiction : écrire du code générique inline si un package existe.** Le coût
de recherche (quelques tokens) est toujours inférieur au coût de régénération.

### Étape 3 — Concevoir la méthode générique « parfaite »

Pour chaque étape générique sans package existant, concevoir une méthode idéale :

- signature générique (`<T>` quand pertinent), paramètres d'options dans un type
  `XxxOptions` avec défauts raisonnables et `Validate()` ;
- effets injectés (délais, horloge, aléa, logging via `ILogger`) — voir R3/R4 ;
- async avec `CancellationToken` si l'opération peut être longue ;
- documentation XML complète (contrat, exceptions, valeurs par défaut).

À l'issue de cette étape, la demande = **suite d'appels de méthodes génériques
paramétrées + un résidu de code spécifique au projet** (qui reste dans le projet).

### Étape 4 — Forger les micropackages

Pour chaque méthode générique conçue :

```
forge new Micro.<Domaine>.<Action> --description "…(≥30 car., rédigée pour la recherche)" --tags "tag1;tag2;tag3"
```

Puis : implémenter `src/`, écrire de **vrais tests** (le test scaffoldé échoue
volontairement tant qu'il n'est pas remplacé), compléter les 4 sections du
`README.md` (mode d'emploi = quand l'utiliser / quand ne pas l'utiliser ;
paramétrage = tableau exhaustif des options ; exemple = code compilable).

```
forge validate Micro.<Domaine>.<Action>
forge publish  Micro.<Domaine>.<Action>
```

`forge publish` oppose quatre refus possibles — les traiter, jamais les contourner :

| Refus | Signification | Action |
|-------|---------------|--------|
| Violation de `RULES.md` | API bannie, README incomplet, tests absents… | Corriger le package |
| Version déjà publiée | Les artefacts sont immuables | `forge bump <Id> patch\|minor\|major` |
| Incrément insuffisant | Le contrat public exige plus (R9) | `forge bump` au niveau annoncé |
| Quasi-doublon | Un package couvre déjà ce besoin (R10) | Réutiliser ou étendre l'existant |

Le dernier est le plus important : **un refus pour quasi-duplication signifie que
l'étape 2 a été mal faite.** Revenir chercher le package signalé et l'utiliser.
Ne jamais recourir à `--allow-similar` pour faire passer une publication.

### Modifier un package existant

L'incrément à appliquer est dicté par la différence de contrat public, pas par
l'intuition :

```
forge diff <Id> <version actuelle> <version cible>
```

- ajout de membres seulement → `forge bump <Id> minor`
- retrait ou modification de signature → `forge bump <Id> major`
- corps de méthode seul modifié → `forge bump <Id> patch`

Privilégier toujours l'ajout rétrocompatible (nouveau paramètre optionnel, nouvelle
surcharge) plutôt que la modification d'une signature : un incrément majeur oblige
chaque consommateur à une relecture manuelle.

### Étape 5 — Consommer ses propres packages

Vérifier que les packages forgés sont trouvables (`forge search <tags>`), puis
**les consommer via NuGet dans le projet cible** — le projet ne contient jamais
de copie du code générique :

```
dotnet add package Micro.<Domaine>.<Action>
```

Assembler : appels paramétrés des micropackages + code spécifique résiduel.

### Étape 6 — Maintenir les projets consommateurs

Les versions étant épinglées, un projet ne bouge jamais tout seul. Pour proposer une
montée :

```
forge outdated <projet>
```

Chaque référence est classée : `À JOUR`, `SÛRE` (même majeur, aucun membre retiré —
la compilation ne peut pas casser), `RELECTURE` (rupture de contrat ou version
dépréciée), `INTROUVABLE`.

**Une montée « sûre » garantit la compilation, pas le comportement.** C'est la suite
de tests du projet qui tranche :

```
forge update <projet> --safe-only --test "dotnet test"
```

Si les tests échouent, le `.csproj` est restauré à l'identique et l'ancienne version
reste installable (immuabilité du feed). Ne jamais appliquer une montée `RELECTURE`
sans examiner d'abord `forge diff`.

## Critères « générique ou pas » (arbitrage rapide)

Forger un micropackage si TOUTES ces conditions tiennent :
1. Le besoin peut se reformuler sans aucun mot du domaine métier du projet.
2. Un autre projet plausible l'utiliserait tel quel, juste paramétré autrement.
3. La capacité tient en une responsabilité (sinon : découper en plusieurs packages).

Sinon, le code reste dans le projet appelant.

## Taxonomie des tags (pour chercher et être trouvé)

- **Domaine** (obligatoire, 1er tag) : `text`, `flow`, `collections`, `validation`,
  `mapping`, `time`, `math`, `crypto`, `encoding`…
- **Capacité** : verbes/noms du besoin (`retry`, `slug`, `chunk`, `parse`…).
- **Qualificatifs** : `async`, `generic`, `pure`…

Toujours ≥ 3 tags. Choisir les mots qu'une autre IA taperait spontanément.

## Quand la bibliothèque contient déjà un doublon

Si `forge duplicates` signale deux packages qui couvrent le même besoin, ne rien
supprimer : garder le meilleur et déprécier l'autre en pointant vers lui.

```
forge deprecate <Id abandonné> --reason "Fusionné dans <Id conservé>." --replacement <Id conservé>
```

Les artefacts restent installables — aucun build existant ne casse — mais le package
abandonné est marqué `[DÉPRÉCIÉ]` dans la recherche et signalé aux consommateurs.

## Rappels d'immutabilité

- Version publiée = figée à jamais ; toute évolution = incrément SemVer (R8),
  au niveau que le contrat public exige (R9).
- Un package obsolète se **déprécie**, ne se supprime pas (R11).
- `RULES.md`, `packages/Directory.Build.props` et ce fichier ne se modifient pas.
- Modifier un package existant = éditer `packages/<Id>/`, bump de version,
  `forge publish`. Identique pour un humain et pour une IA (R12).
