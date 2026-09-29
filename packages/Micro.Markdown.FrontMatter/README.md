# Micro.Markdown.FrontMatter

## Description

Sépare l'en-tête YAML d'un document Markdown — le « front matter » écrit entre deux lignes
`---` en tête de fichier — et lit ses propriétés de premier niveau : valeurs simples
(guillemets et commentaires retirés), listes en ligne `[a, b]`, listes à tirets, textes
multilignes `|` et `>`. Rend aussi les positions exactes de l'en-tête et du corps. Toute
structure plus riche (dictionnaire imbriqué, liste d'objets) est rendue telle qu'écrite
plutôt que de faire échouer la lecture.

## Mode d'emploi

Utiliser ce package pour lire les métadonnées d'une note ou d'une page : `tags`, `aliases`,
`title`, `date`, `draft`… dans un outil de notes, un générateur de site statique ou un
indexeur de documentation. `TryLocate` donne les seules positions, pour sauter l'en-tête
avant d'analyser le corps ou pour le colorer dans un éditeur, sans payer la lecture des
propriétés.

| Membre | Rôle |
|--------|------|
| `FrontMatterReader.Read(text, options)` | En-tête et propriétés, ou `FrontMatterBlock.None`. |
| `FrontMatterReader.TryLocate(text, out contentStart, out contentLength, out bodyStart, options)` | Positions seules. Ne lève jamais. |
| `FrontMatterBlock.Exists` / `BodyStart` / `Content` | Présence, début du corps, YAML brut. |
| `FrontMatterBlock.Find(key)` / `GetValues(key)` | Une propriété ; la dernière l'emporte si la clé est répétée. |
| `FrontMatterProperty` | `Key`, `Values`, `Value` (première), `Kind`, `RawValue`, `Line`. |

L'en-tête doit ouvrir le document — une marque d'ordre des octets est tolérée — et être
refermé par `---` (ou `...`) ; sinon le `---` initial est un filet et le document n'a pas
d'en-tête. Les trois conventions de fin de ligne sont acceptées.

**Ne pas** l'utiliser comme analyseur YAML général : ancres, références, types étiquetés,
dictionnaires imbriqués ne sont pas décomposés (`Kind = Complex`, voir `RawValue`). Pour un
fichier de configuration YAML complet, prendre YamlDotNet. Ne pas non plus s'en servir pour
un en-tête TOML (`+++`) : il n'est pas reconnu.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `AllowDotsClosing` | `bool` | `true` | Accepter `...` comme ligne fermante, en plus de `---`. |
| `IgnoreKeyCase` | `bool` | `true` | `Find("tags")` trouve aussi `Tags:`. |
| `MaxLength` | `int` | `65536` | Longueur explorée pour trouver la ligne fermante ; au-delà, pas d'en-tête. |

`FrontMatterOptions.Validate()` refuse un `MaxLength` inférieur à 3
(`ArgumentOutOfRangeException`). Passer `null` en options prend les défauts.

## Exemple

```csharp
using Micro.Markdown.FrontMatter;

string note = "---\ntags: [voyage, \"été 2026\"]\naliases:\n  - Carnet\n---\n# Départ\n";

FrontMatterBlock entete = FrontMatterReader.Read(note);
IReadOnlyList<string> tags = entete.GetValues("tags");        // « voyage », « été 2026 »
string? alias = entete.Find("aliases")?.Value;                  // « Carnet »
string corps = note[entete.BodyStart..];                        // « # Départ\n »
```
