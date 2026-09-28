# Micro.Geometry.PoissonDisc

## Description

Tire un nombre **exact** de points dans une ellipse ou un rectangle, deux à deux espacés d'une
distance minimale — un tirage par disques de Poisson. Le semis est régulier sans être une grille :
ni amas ni trous, ce qu'un tirage uniforme ne garantit jamais (deux étoiles superposées, un coin
vide). Déterministe : l'aléa vient du `Random` fourni, et `new Random(graine)` redonne le même
semis à chaque lancement.

Ce qui justifie un package plutôt que dix lignes de boucle :

- **Le nombre de points est garanti.** La distance de départ découle de la surface et du nombre de
  points ; si les points ne tiennent pas, elle se resserre et le tirage reprend. Une boucle naïve
  rend « à peu près » le nombre demandé, ou ne se termine pas.
- **La distance réellement tenue est rendue** (`SampleResult.Distance`) : l'appelant sait quel
  écart il peut compter entre deux points (taille maximale d'un halo, cible de survol).
- **Linéaire** : une grille de cellules (côté = distance / √2) limite chaque vérification aux
  voisins proches ; des milliers de points se tirent en quelques millisecondes.
- **Les cas dégénérés se terminent** : surface nulle (un segment, un point), dimensions qui
  débordent (refusées), nombre démesuré (borné par `MaximumCount`).

## Mode d'emploi

**Quand l'utiliser** — pour poser des objets visuels sans chevauchement : un champ d'étoiles, les
étoiles d'une constellation, des repères sur une carte, des bulles, des particules de départ ;
partout où l'on veut un semis naturel et stable. Deux points d'entrée sur `PoissonDiscSampler` :

- `SampleEllipse(count, centerX, centerY, radiusX, radiusY, random, options)`
- `SampleRectangle(count, left, top, width, height, random, options)`

Les points sont rendus dans l'ordre du tirage : les relier dans un ordre qui a du sens (un
chemin, un tri) revient à l'appelant.

**Quand ne pas l'utiliser** — pour un placement qui doit respecter des contraintes métier
(labels qui ne se recouvrent pas, graphe à forces) : c'est un tirage, pas une optimisation. Pour
un semis de millions de points ou en 3D : l'algorithme de Bridson, dédié, fera mieux. Pour de
l'aléa cryptographique : `Random` n'en est pas.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `AttemptsPerPoint` | `int` | `60` | Essais par point avant de conclure que la distance est trop grande (≥ 1). |
| `ShrinkFactor` | `double` | `0.88` | Resserrement de la distance quand les points ne tiennent pas (strictement entre 0 et 1). |
| `AreaPerPoint` | `double` | `1.15` | Surface réservée à chaque point, en multiple du carré de la distance de départ (> 0) : plus grand, points plus serrés dès le départ. |
| `MaximumCount` | `int` | `10 000` | Nombre maximal de points d'un tirage (≥ 1) ; au-delà, `ArgumentOutOfRangeException`. |

`Validate()` lève `ArgumentOutOfRangeException` sur un réglage hors bornes. Les deux méthodes
lèvent `ArgumentNullException` pour un `Random` nul, et `ArgumentOutOfRangeException` pour un
nombre de points négatif ou excessif, une coordonnée non finie, une dimension négative ou non
finie, ou une surface qui déborde.

Le résultat, `SampleResult` : `Points` (liste de `SamplePoint(X, Y)`, exactement `count`
éléments) et `Distance` (écart minimal garanti entre deux points ; 0 pour une surface nulle).

## Exemple

```csharp
using Micro.Geometry.PoissonDisc;

// Les 26 étoiles d'une constellation, au même endroit à chaque lancement.
Random random = new(2026);
SampleResult stars = PoissonDiscSampler.SampleEllipse(26, centerX: 1.2, centerY: 0.5, radiusX: 0.3, radiusY: 0.35, random);

foreach (SamplePoint star in stars.Points)
{
    // Dessiner l'étoile en (star.X, star.Y) ; son halo peut atteindre stars.Distance / 2 sans toucher sa voisine.
}

// Un champ d'étoiles plus serré dans un rectangle de 1920 × 1080.
SampleResult field = PoissonDiscSampler.SampleRectangle(400, 0, 0, 1920, 1080, new Random(7), new PoissonDiscOptions { AreaPerPoint = 2 });
```
