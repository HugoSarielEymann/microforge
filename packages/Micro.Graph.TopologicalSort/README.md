# Micro.Graph.TopologicalSort

## Description

Ordonne les noeuds d'un graphe orienté par tri topologique et les regroupe en vagues parallèles, avec détection et report des cycles.

L'algorithme est celui de Kahn. La fonction est pure et déterministe : à collection d'entrée et arêtes identiques, la sortie est toujours la même — y compris l'ordre à l'intérieur d'une vague, qui reprend celui de la collection d'entrée.

## Mode d'emploi

Utiliser ce package dès qu'un ensemble d'éléments doit être traité dans un ordre imposé par des
dépendances : étapes d'un pipeline, tâches de build, migrations, initialisation de services,
résolution d'un ordre de chargement. La propriété `Waves` répond en plus à la question
« que puis-je lancer en parallèle ? » : les nœuds d'une même vague sont indépendants entre eux.

Point d'entrée unique : `TopologicalSorter.Order(nodes, edges, options, comparer)`.

**Un cycle est un résultat, pas une exception.** `Order` ne lève pas face à un graphe cyclique :
elle ordonne ce qu'elle peut et reporte le reste dans `CyclicNodes`. C'est ce qui permet
d'afficher à l'utilisateur *quels* éléments se bloquent mutuellement plutôt qu'un simple échec.
Les exceptions sont réservées aux erreurs de l'appelant : entrée nulle, nœud en double,
arête vers un nœud absent.

Ne pas utiliser ce package pour trouver le plus court chemin, les composantes fortement connexes,
ou le cycle minimal : `CyclicNodes` contient tout ce qui reste bloqué (le cycle **et** ce qui en
dépend), ce qui est l'information utile pour un diagnostic, pas pour une analyse de graphe fine.
Inutile également si les dépendances sont déjà connues sous forme d'ordre total : un simple tri suffit.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `EdgeMeaning` | `EdgeMeaning` | `Precedes` | Sens de lecture des arêtes. `Precedes` : les arêtes d'un nœud sont ses successeurs (« A précède B »). `Follows` : ce sont ses prédécesseurs (« A dépend de B »). Évite d'inverser soi-même le graphe avant l'appel. |
| `IgnoreUnknownNodes` | `bool` | `false` | Comportement face à une arête pointant hors de la collection fournie. `false` lève ; `true` l'ignore, ce qui permet de trier un sous-graphe sans filtrer les arêtes sortantes. |

Deux paramètres facultatifs sont passés directement à `Order` :

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `options` | `TopologicalSortOptions?` | `TopologicalSortOptions.Default` | Paramétrage ci-dessus. |
| `comparer` | `IEqualityComparer<T>?` | `EqualityComparer<T>.Default` | Identité des nœuds, pour trier par clé métier plutôt que par référence. |

Le résultat, `TopologicalOrder<T>` :

| Membre | Type | Rôle |
|--------|------|------|
| `IsComplete` | `bool` | Faux si le graphe contient un cycle. |
| `Sorted` | `IReadOnlyList<T>` | Ordre obtenu ; partiel en présence d'un cycle. |
| `Waves` | `IReadOnlyList<IReadOnlyList<T>>` | Même ordre, découpé en vagues parallélisables. Les vagues sont séquentielles entre elles. |
| `CyclicNodes` | `IReadOnlyList<T>` | Nœuds bloqués par un cycle, dans l'ordre d'entrée. Vide si acyclique. |

## Exemple

```csharp
using Micro.Graph.TopologicalSort;

string[] etapes = ["charger", "valider", "enrichir", "publier"];

// « charger » précède « valider » et « enrichir », qui précèdent tous deux « publier ».
var ordre = TopologicalSorter.Order(etapes, etape => etape switch
{
    "charger" => ["valider", "enrichir"],
    "valider" => ["publier"],
    "enrichir" => ["publier"],
    _ => Array.Empty<string>(),
});

if (!ordre.IsComplete)
{
    throw new InvalidOperationException(
        "Dépendances circulaires : " + string.Join(", ", ordre.CyclicNodes));
}

foreach (var vague in ordre.Waves)
{
    // « valider » et « enrichir » arrivent dans la même vague : lançables en parallèle.
    await Task.WhenAll(vague.Select(ExecuterAsync));
}
```
