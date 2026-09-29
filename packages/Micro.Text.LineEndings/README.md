# Micro.Text.LineEndings

## Description

Détecte la convention de fin de ligne d'un texte — `\n` (LF), `\r\n` (CRLF) ou `\r` (CR) —,
compte chaque forme, signale les mélanges et donne la séquence dominante à réutiliser. Sert
à réécrire un fichier avec la convention qu'il avait à la lecture, pour qu'un enregistrement
ne transforme pas toutes les lignes d'un fichier suivi en version. Fonction pure et
déterministe.

## Mode d'emploi

Utiliser ce package dans un éditeur ou un outil de réécriture : lire le fichier, noter sa
convention avec `Detect`, travailler en interne avec une convention unique, puis réécrire
avec `string.ReplaceLineEndings(LineEndingDetector.ToSequence(convention))`. `Analyze` donne
le détail pour avertir d'un fichier mélangé (`IsMixed`).

| Membre | Rôle |
|--------|------|
| `LineEndingDetector.Analyze(text)` | Décompte `Lf`, `CrLf`, `Cr`, `Total`, `IsMixed`, `Dominant`. |
| `LineEndingDetector.Detect(text, options)` | Convention dominante, ou le repli si le texte n'a aucune fin de ligne. |
| `LineEndingDetector.ToSequence(ending)` | `"\n"`, `"\r\n"` ou `"\r"`. |

À égalité, CRLF l'emporte sur LF, qui l'emporte sur CR. Seules ces trois séquences comptent :
les séparateurs Unicode (`U+2028`, `U+0085`) ne sont pas des fins de ligne de fichier.

**Ne pas** l'utiliser pour **normaliser** : `string.ReplaceLineEndings` du framework le fait
déjà, et ce package ne le double pas. Ne pas non plus s'en servir pour découper un texte en
lignes : `StringReader.ReadLine` ou `string.Split` y suffisent.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Fallback` | `LineEnding` | `LineEnding.Lf` | Convention rendue par `Detect` pour un texte sans fin de ligne ; ne peut pas valoir `None`. |

`LineEndingOptions.Validate()` refuse un repli `None` ou non défini
(`ArgumentOutOfRangeException`). `ToSequence` refuse `None` de la même façon.

## Exemple

```csharp
using Micro.Text.LineEndings;

string lu = "Titre\r\nPremière ligne\r\n";
LineEnding convention = LineEndingDetector.Detect(lu);          // LineEnding.CrLf

string interne = lu.ReplaceLineEndings("\n") + "Ajout\n";
string aEcrire = interne.ReplaceLineEndings(LineEndingDetector.ToSequence(convention));
// « Titre\r\nPremière ligne\r\nAjout\r\n »
```
