# Micro.Text.Excerpt

## Description

Découpe un extrait de texte autour d'un passage, tel qu'on l'affiche dans une liste de
résultats de recherche ou de mentions : contexte borné avant et après, coupé aux frontières
de mot, limité par défaut à la ligne du passage, blancs réduits, points de suspension là où
le texte est tronqué. La position du passage est recalculée **dans l'extrait final** :
l'appelant surligne sans rien recompter. Fonction pure et déterministe.

## Mode d'emploi

Utiliser ce package pour présenter une occurrence dans son contexte : résultat de recherche
plein texte, rétrolien (« cette note est citée ici : … »), ligne de journal autour d'une
erreur, aperçu de commentaire. Il suffit de connaître la position et la longueur du passage
dans le texte d'origine.

| Membre | Rôle |
|--------|------|
| `ExcerptBuilder.Around(text, start, length, options)` | Extrait autour du passage `[start, start + length)`. |
| `TextExcerpt.Text` | Texte à afficher, points de suspension compris. |
| `TextExcerpt.HighlightStart` / `HighlightLength` / `Highlight` | Le passage, repositionné dans `Text`. |
| `TextExcerpt.StartsTruncated` / `EndsTruncated` | Du texte a été coupé avant / après. |

Le passage n'est jamais raccourci ; seul le contexte l'est. Une paire de substitution
(émoji) n'est jamais coupée en deux. Un passage de longueur nulle marque un simple point.

**Ne pas** l'utiliser pour **trouver** le passage : c'est l'affaire du moteur de recherche,
qui connaît les règles de casse et d'accents. Ne pas non plus s'en servir pour résumer un
texte ou tronquer un titre à une largeur d'affichage : il n'y a ni coupe « intelligente » ni
mesure en pixels.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `ContextBefore` | `int` | `40` | Caractères gardés au plus avant le passage. |
| `ContextAfter` | `int` | `80` | Caractères gardés au plus après le passage. |
| `Ellipsis` | `string` | `"…"` | Marque posée à chaque coupe. |
| `SnapToWords` | `bool` | `true` | Ne jamais couper un mot en bordure : le contexte recule à la frontière de mot. |
| `SingleLine` | `bool` | `true` | Ne pas déborder de la ligne (ou des lignes) du passage. |
| `CollapseWhitespace` | `bool` | `true` | Réduire chaque suite de blancs à une espace. |

`ExcerptOptions.Validate()` refuse un contexte négatif (`ArgumentOutOfRangeException`) et
des points de suspension nuls (`ArgumentNullException`). `Around` refuse un passage qui sort
du texte (`ArgumentOutOfRangeException`).

## Exemple

```csharp
using Micro.Text.Excerpt;

string note = "Ce soir, le portail doré s'est ouvert au-dessus de l'observatoire abandonné.";
int position = note.IndexOf("portail", StringComparison.Ordinal);

TextExcerpt extrait = ExcerptBuilder.Around(note, position, "portail".Length,
    new ExcerptOptions { ContextBefore = 10, ContextAfter = 20 });

Console.WriteLine(extrait.Text);        // « …soir, le portail doré s'est ouvert… »
Console.WriteLine(extrait.Highlight);   // « portail »
```
