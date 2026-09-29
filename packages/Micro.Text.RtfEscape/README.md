# Micro.Text.RtfEscape

## Description

Échappe du texte brut pour l'insérer dans un document RTF : l'antislash et les accolades — la
syntaxe même du RTF — sont protégés, la tabulation devient `\tab`, chaque fin de ligne (`\n`,
`\r\n`, `\r`) devient `\par` (ou le mot de contrôle choisi), les caractères de contrôle sont
écartés, et tout caractère hors ASCII s'écrit `\uN?` sur des unités UTF-16 signées, comme le
veut la norme. Relu par un contrôle RTF, le texte redonne l'original caractère pour caractère.

## Mode d'emploi

Utiliser ce package pour produire du RTF à partir de texte : charger d'un seul appel un
document mis en forme dans un `RichEditBox` ou un `RichTextBox` (bien plus rapide que de
formater des milliers de plages une à une), copier du texte stylé dans le presse-papiers, ou
exporter un rapport lisible par Word et WordPad. `Append` écrit directement dans le document en
construction, sans copie intermédiaire.

| Membre | Rôle |
|--------|------|
| `RtfEscaper.Escape(text, options)` | Texte échappé, prêt à placer dans un groupe RTF. |
| `RtfEscaper.Append(builder, text, options)` | Même chose, ajouté au `StringBuilder` du document. |
| `RtfEscaper.Append(builder, span, options)` | Même chose pour une portion de texte (`ReadOnlySpan<char>`). |

Le document qui reçoit le texte doit déclarer `\uc1` dans son en-tête : un seul caractère de
repli suit chaque `\uN`.

**Ne pas** l'utiliser pour **lire** du RTF ni pour convertir du RTF en texte : c'est un
échappement dans un seul sens. Ne pas non plus s'en servir pour composer la mise en forme
(polices, couleurs, tables) : elle reste l'affaire de l'appelant, qui connaît son document.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `NewLine` | `string` | `"\par "` | Mot de contrôle écrit pour chaque fin de ligne ; `"\line "` pour un saut dans le paragraphe. |
| `Fallback` | `char` | `'?'` | Caractère de repli après chaque `\uN`, pour les lecteurs sans Unicode. |

`RtfEscapeOptions.Validate()` refuse un `NewLine` nul (`ArgumentNullException`) et un repli
qui n'est pas un caractère ASCII imprimable hors `\`, `{` et `}` (`ArgumentException`).

## Exemple

```csharp
using System.Text;
using Micro.Text.RtfEscape;

var rtf = new StringBuilder(@"{\rtf1\ansi\uc1{\colortbl ;\red224\green173\blue60;}");
rtf.Append(@"{\cf1\b ");
RtfEscaper.Append(rtf, "Été {doré}");          // « \u201?t\u233? \{dor\u233?\} »
rtf.Append('}');
rtf.Append(RtfEscaper.Escape("\nC:\\notes"));  // « \par C:\\notes »
rtf.Append('}');
```
