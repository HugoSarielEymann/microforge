# Micro.Graph.ForceLayout

## Description

Dispose les nœuds d'un graphe dans le plan par simulation de forces, à la manière de
d3-force : chaque lien est un ressort qui tend vers sa longueur au repos, chaque paire de
nœuds se repousse (approximation de Barnes-Hut, `O(n log n)` par pas), et une légère gravité
empêche les composantes isolées de dériver. La simulation avance pas à pas, se refroidit
jusqu'à se figer, se relance à la demande, et accepte des nœuds épinglés — ce qu'il faut pour
une vue de graphe interactive où l'on glisse les nœuds à la souris. Entièrement
déterministe : même graphe, mêmes réglages, même disposition au bit près.

## Mode d'emploi

Utiliser ce package pour afficher un réseau dont la disposition n'est pas donnée : graphe de
notes liées, dépendances entre modules, relations entre personnes, carte d'un site. Le
graphe se décrit par un nombre de nœuds (numérotés de 0 à n − 1) et une liste de liens ; la
boucle d'animation appelle `Tick()` à chaque image tant que la simulation n'est pas figée,
puis lit `GetX` / `GetY` pour dessiner.

| Membre | Rôle |
|--------|------|
| `new ForceSimulation(nodeCount, links, options)` | Prépare la simulation ; départ en spirale de phyllotaxie. |
| `Tick()` / `Run(maxTicks)` | Un pas ; ou des pas jusqu'au figement (préchauffage avant affichage). |
| `Alpha`, `AlphaTarget`, `IsSettled`, `Reheat(alpha)` | Température ; cible (0,3 pendant un glisser) ; figement ; relance. |
| `GetX(node)` / `GetY(node)` / `GetBounds()` | Positions et rectangle englobant, pour cadrer la vue. |
| `SetPosition(node, x, y)` | Reprend une disposition précédente quand le graphe change. |
| `Pin(node, x, y)` / `Unpin(node)` / `IsPinned(node)` | Fixe un nœud (celui qu'on glisse) ; il agit toujours sur les autres. |
| `SetWeight(node, weight)` / `GetWeight(node)` | Multiplie la répulsion d'un nœud : un gros nœud réclame plus de place. |

Le geste de glisser, tel que d3 le pratique : au clic, `AlphaTarget = 0.3` et `Reheat()` ;
pendant le glisser, `Pin(node, x, y)` à chaque mouvement ; au lâcher, `AlphaTarget = 0` et
`Unpin(node)`.

**Ne pas** l'utiliser pour un graphe hiérarchique ou orienté où l'ordre compte (organigramme,
pipeline, dépendances à lire de gauche à droite) : une disposition en couches (Sugiyama) ou un
tri topologique y répondent mieux. Ne pas non plus s'en servir depuis plusieurs fils : la
simulation se pilote depuis celui qui dessine. Au-delà de quelques dizaines de milliers de
nœuds, un pas dépasse le temps d'une image : préchauffer hors de l'affichage.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `LinkDistance` | `double` | `60` | Longueur au repos d'un lien. |
| `LinkStrength` | `double` | `NaN` | Raideur entre 0 et 1 ; `NaN` = inverse du plus petit degré des deux bouts (règle de d3). |
| `ChargeStrength` | `double` | `-120` | Force entre chaque paire de nœuds ; négative, elle repousse. |
| `ChargeDistanceMax` | `double` | `+∞` | Portée de la répulsion. |
| `Theta` | `double` | `0.9` | Précision de Barnes-Hut ; `0` = calcul exact, quadratique. |
| `CenterStrength` | `double` | `0.05` | Attraction vers l'origine, entre 0 et 1. |
| `AlphaMin` | `double` | `0.001` | Température sous laquelle la simulation est figée. |
| `AlphaDecay` | `double` | `≈ 0.0228` | Refroidissement par pas (figée en 300 pas environ). |
| `AlphaTarget` | `double` | `0` | Température visée ; au-dessus de `AlphaMin`, la simulation reste vivante. |
| `VelocityDecay` | `double` | `0.4` | Frottement : part de vitesse perdue à chaque pas. |
| `InitialRadius` | `double` | `10` | Espacement de la spirale de départ. |
| `Seed` | `int` | `1` | Graine du minuscule aléa qui sépare deux nœuds confondus. |

`ForceLayoutOptions.Validate()` refuse toute valeur non finie (sauf `LinkStrength` automatique
et `ChargeDistanceMax` infinie), une distance, un rayon ou un `Theta` négatifs, une portée
nulle, et toute fraction hors de [0, 1] (`ArgumentOutOfRangeException`). Les coordonnées et
poids non finis sont refusés de la même façon.

## Exemple

```csharp
using Micro.Graph.ForceLayout;

// Cinq notes : 0 est liée à toutes les autres, 3 et 4 se citent entre elles.
ForceLink[] liens = [new(0, 1), new(0, 2), new(0, 3), new(0, 4), new(3, 4)];
var simulation = new ForceSimulation(5, liens, new ForceLayoutOptions { LinkDistance = 80 });

simulation.SetWeight(0, 3);   // le moyeu, dessiné plus gros, réclame plus de place
simulation.Run(120);          // préchauffage : on n'affiche pas la spirale de départ

while (simulation.Tick())     // puis une étape par image, jusqu'au figement
{
    for (int note = 0; note < simulation.NodeCount; note++)
    {
        double x = simulation.GetX(note), y = simulation.GetY(note);
        // dessiner l'étoile de la note en (x, y)
    }
}
```
