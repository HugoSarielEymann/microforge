# Micro.Markdown.CodeSpans

## Description

Repère les plages de code d'un texte Markdown — blocs délimités par <code>```</code> ou
`~~~`, segments en ligne entre backticks et, sur demande, blocs indentés — et rend leurs
positions exactes (index UTF-16, délimiteurs compris). Fonction pure et déterministe, sans
dépendance.

## Mode d'emploi

Utiliser ce package **avant** de chercher quoi que ce soit dans une note Markdown : liens
wiki, étiquettes `#tag`, mentions, mots-clés à compter. Un `[[lien]]` écrit dans un bloc de
code est un exemple, pas une référence ; `CodeSpanScanner.Scan` dit où ne pas chercher, et
`IsInside` / `Overlaps` permettent d'écarter une trouvaille en une recherche dichotomique.

Le repérage est tolérant sur l'indentation des clôtures : un bloc placé dans une liste ou
dans une citation (`> ` + <code>```</code>) est reconnu. Un bloc jamais refermé court jusqu'à
la fin du texte, comme dans tout rendu Markdown ; un backtick orphelin redevient de la prose.
Un segment en ligne ne franchit jamais une ligne vide.

**Ne pas** l'utiliser pour rendre du Markdown ni pour obtenir un arbre syntaxique : c'est un
repérage de plages, pas un analyseur CommonMark complet (pas de listes, pas d'emphase, pas de
HTML). Pour du rendu, prendre un vrai moteur (Markdig). Les blocs indentés sont désactivés par
défaut, parce que dans une note la même indentation sert surtout à imbriquer des listes.

| Membre | Rôle |
|--------|------|
| `CodeSpanScanner.Scan(text, options)` | Plages de code, triées et disjointes. |
| `CodeSpanScanner.IsInside(spans, index)` | Une position tombe-t-elle dans du code ? |
| `CodeSpanScanner.Overlaps(spans, start, length)` | Une plage touche-t-elle du code ? |
| `CodeSpan.Start` / `Length` / `End` / `Kind` | Position et nature (`Fenced`, `Inline`, `Indented`). |

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `IncludeFenced` | `bool` | `true` | Relever les blocs délimités par <code>```</code> ou `~~~`. |
| `IncludeInline` | `bool` | `true` | Relever les segments en ligne entre backticks. |
| `IncludeIndented` | `bool` | `false` | Relever les blocs indentés de quatre espaces après une ligne vide. |
| `MaxInlineLines` | `int` | `8` | Nombre maximal de lignes couvertes par un segment en ligne ; borne un backtick orphelin. |

`CodeSpanOptions.Validate()` refuse un `MaxInlineLines` inférieur à 1
(`ArgumentOutOfRangeException`). Passer `null` en options prend les défauts.

## Exemple

```csharp
using Micro.Markdown.CodeSpans;

string note = "Voir [[Plan]].\n```\n[[Exemple]] dans du code\n```";
IReadOnlyList<CodeSpan> code = CodeSpanScanner.Scan(note);

int exemple = note.IndexOf("[[Exemple]]", StringComparison.Ordinal);
bool ignorer = CodeSpanScanner.IsInside(code, exemple);   // true : c'est du code
bool garder = CodeSpanScanner.IsInside(code, note.IndexOf("[[Plan]]", StringComparison.Ordinal)); // false
```
