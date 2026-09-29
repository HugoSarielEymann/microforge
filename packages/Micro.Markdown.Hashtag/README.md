# Micro.Markdown.Hashtag

## Description

Repère les étiquettes `#tag` d'un texte Markdown à la manière d'Obsidian — lettres de tout
alphabet, chiffres, `_`, `-` et `/` pour l'imbrication (`#projet/almageste`) — avec leurs
positions exactes en index UTF-16. Écarte ce qui ressemble à une étiquette sans en être une :
titres (`# Titre`), ancres d'URL (`page#section`), entités HTML (`&#233;`), sections de lien
(`[[Note#Titre]]`), numéros (`#12`) et tout ce qui est écrit dans du code. Donne aussi les
ancêtres d'une étiquette imbriquée.

## Mode d'emploi

Utiliser ce package pour indexer les étiquettes d'un ensemble de notes, colorer la syntaxe
dans un éditeur, alimenter un panneau d'étiquettes arborescent (`Ancestors` compte une
étiquette dans chacun de ses parents) ou valider un nom saisi par l'utilisateur
(`IsValidName`).

| Membre | Rôle |
|--------|------|
| `HashtagParser.TryParseAt(text, index, out tag, options)` | Lit une étiquette dont le `#` est à `index`. Ne lève jamais. |
| `HashtagParser.FindAll(text, options)` | Relève toutes les étiquettes, dans l'ordre, hors code. |
| `HashtagParser.IsValidName(name, options)` | `#` + ce nom serait-il relevé en entier ? Ne lève jamais. |
| `HashtagParser.Ancestors(name)` | `a/b/c` → `a`, `a/b`, `a/b/c`. |
| `HashtagMatch` | `Start` (position du `#`), `Length` (`#` compris), `End`, `Name` (sans `#`). |

Règles : le `#` doit ouvrir le texte ou suivre un blanc ; le nom doit contenir au moins un
caractère non numérique ; une barre oblique finale n'en fait pas partie ; un emoji ou une
ponctuation arrête le nom.

**Ne pas** l'utiliser pour lire les étiquettes déclarées dans un en-tête YAML (`tags: [a, b]`) :
c'est de la donnée structurée, pas de la prose — un lecteur d'en-tête s'en charge. Ne pas non
plus s'en servir pour des mots-dièse de réseaux sociaux en langue libre sans revoir
`AllowedPrecedingCharacters` : la règle du blanc préalable y est trop stricte.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `SkipCode` | `bool` | `true` | Ignorer les étiquettes écrites dans du code (`FindAll` seulement). |
| `AllowNested` | `bool` | `true` | Accepter `/` pour les étiquettes imbriquées. |
| `RequireNonDigit` | `bool` | `true` | Exiger un caractère non numérique : `#2026` n'est pas une étiquette. |
| `AllowedPrecedingCharacters` | `string` | `""` | Caractères admis avant le `#` en plus d'un blanc ; `"("` accepte `(#idée)`. |
| `MaxLength` | `int` | `128` | Longueur maximale d'un nom ; au-delà, le nom est coupé. |

`HashtagOptions.Validate()` refuse un `AllowedPrecedingCharacters` nul (`ArgumentNullException`)
et un `MaxLength` inférieur à 1 (`ArgumentOutOfRangeException`). Passer `null` prend les défauts.

## Exemple

```csharp
using Micro.Markdown.Hashtag;

string note = "# Journal\nAvancé sur #projet/almageste, voir page.html#plan. `#pas-ici`";

IReadOnlyList<HashtagMatch> tags = HashtagParser.FindAll(note);
// → une seule étiquette : « projet/almageste »

foreach (string niveau in HashtagParser.Ancestors(tags[0].Name))
{
    Console.WriteLine(niveau);   // « projet », puis « projet/almageste »
}

bool ok = HashtagParser.IsValidName("à-lire");   // true
```
