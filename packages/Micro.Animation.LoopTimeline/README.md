# Micro.Animation.LoopTimeline

## Description

Calcule où en est une animation en boucle à un instant donné : début différé, durée d'un
aller, aller-retour, nombre d'itérations (fractionnaire admis) ou boucle sans fin. La classe
rend un avancement linéaire entre 0 et 1 — ou `null` avant le début —, exactement comme le
ferait l'horloge d'une animation XAML, sans rien animer elle-même.

C'est ce qui permet d'animer **à la cadence de son choix**. Sous WinUI 3 (comme sous UWP), un
storyboard en boucle (`RepeatBehavior="Forever"`) fait recomposer toute la fenêtre soixante
fois par seconde, tant qu'il tourne : environ un quart de cœur de processeur, que l'élément
animé couvre l'écran ou deux pixels. Une minuterie lente qui interroge `ProgressAt` et pose
elle-même les valeurs reproduit la même courbe à cinq images par seconde, pour un vingtième du
prix, et s'arrête quand la fenêtre n'est plus regardée.

L'horloge est fournie par l'appelant : la classe est déterministe, immuable et sûre entre fils.

## Mode d'emploi

À utiliser pour remplacer une animation continue de décor (scintillement, respiration, dérive
lente) par une minuterie à cadence réduite, pour rejouer un storyboard sans le moteur XAML
(capture, tests, rendu hors écran), ou pour piloter plusieurs propriétés par une même horloge
qu'on peut mettre en pause (`Stopwatch.Stop()` suffit : l'horaire reprend là où il s'était
arrêté).

La transcription d'une animation XAML est directe :

| XAML | `LoopTimelineOptions` |
|------|-----------------------|
| `Duration` | `Duration` |
| `BeginTime` | `BeginTime` |
| `AutoReverse` | `AutoReverse` |
| `RepeatBehavior="Forever"` | `RepeatCount = null` |
| `RepeatBehavior="3x"` | `RepeatCount = 3` |

L'avancement rendu est linéaire : la courbe d'accélération se compose par-dessus
(`EasingFunctionBase.Ease(p)` sous WinUI, ou toute fonction de `[0, 1]` dans `[0, 1]`). La
valeur animée vaut alors `from + (to - from) * ease(p)`, et la valeur de base tant que
`ProgressAt` rend `null`. Pour un storyboard dont c'est le parent qui est en aller-retour,
faire passer le temps local du parent (`progression × durée du parent`) à l'horaire de
l'enfant, réglé sur une seule itération.

**Ne pas** l'utiliser pour un mouvement rapide qui doit rester fluide (une étoile filante, un
glisser à la souris) : là, l'animation doit suivre l'écran image par image, et un storyboard
bref — qui s'arrête de lui-même — ou `CompositionTarget.Rendering` conviennent mieux. Ne pas
non plus s'en servir pour les images clés d'une courbe composite : la classe dit où l'on en
est dans le temps, pas quelle valeur prendre entre deux clés.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Duration` | `TimeSpan` | 1 s | Durée d'un aller, strictement positive. |
| `BeginTime` | `TimeSpan` | `0` | Instant du premier aller. Avant lui, `ProgressAt` rend `null` ; négatif, il décale la phase. |
| `AutoReverse` | `bool` | `false` | Chaque aller est suivi d'un retour de même durée (0 → 1 → 0). |
| `RepeatCount` | `double?` | `null` | Nombre d'itérations, fractionnaire admis ; `null` = sans fin. Après la dernière, la valeur de fin reste figée (`HoldEnd`). |

`LoopTimelineOptions.Validate()` — appelé par le constructeur — refuse une durée nulle ou
négative et un nombre d'itérations nul, négatif ou non fini (`ArgumentOutOfRangeException`).
Des réglages nuls lèvent `ArgumentNullException`. `ProgressAt` accepte tout instant, y compris
`TimeSpan.MinValue` et `TimeSpan.MaxValue` : le calcul se fait sur 128 bits.

`ActiveDuration` donne le temps pendant lequel une boucle finie évolue (`null` si elle est
sans fin), plafonné à `TimeSpan.MaxValue`.

## Exemple

```csharp
using System.Diagnostics;
using Micro.Animation.LoopTimeline;

// Une étoile qui scintille : de 0,8 à 0,2 d'opacité en 7 s, puis retour, sans fin,
// en commençant 1,5 s après l'ouverture de la fenêtre.
LoopTimeline scintillement = new(new LoopTimelineOptions
{
    Duration = TimeSpan.FromSeconds(7),
    BeginTime = TimeSpan.FromSeconds(1.5),
    AutoReverse = true,
});

// L'horloge : un chronomètre qu'on arrête quand la fenêtre n'est plus regardée.
Stopwatch horloge = Stopwatch.StartNew();

// À chaque tic d'une minuterie à 200 ms :
double opacite = scintillement.ProgressAt(horloge.Elapsed) is { } p
    ? 0.8 + ((0.2 - 0.8) * (0.5 - (0.5 * Math.Cos(Math.PI * p))))   // sinusoïde, comme SineEase
    : 0.8;                                                          // pas encore commencé
```
