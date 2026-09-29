# Micro.Markdown.WikiLink

## Description

Analyse, compose et réécrit les liens wiki à la manière d'Obsidian dans un texte Markdown :
`[[cible]]`, `[[cible|alias]]`, `[[cible#section]]`, `[[cible#^bloc]]`, `[[#section]]` et les
intégrations `![[...]]`. Chaque lien est rendu décomposé (cible, section, bloc, alias) avec
ses positions exactes en index UTF-16, ce qui permet de colorer chaque partie ou de réécrire
la seule cible. Les liens écrits dans du code sont ignorés.

## Mode d'emploi

Utiliser ce package dans tout outil qui manipule des notes liées : indexer les liens
sortants d'une note, calculer ses rétroliens, colorer la syntaxe dans un éditeur, proposer
une complétion après `[[`, ou **renommer une note** — `WikiLinkParser.Rewrite` remplace la
cible de chaque renvoi sans toucher ni à l'alias, ni à la section, ni au moindre blanc.

| Membre | Rôle |
|--------|------|
| `WikiLinkParser.TryParseAt(text, index, out link, options)` | Analyse un lien qui commence à `index` (le `!` ou le premier `[`). Ne lève jamais. |
| `WikiLinkParser.FindAll(text, options)` | Relève tous les liens, dans l'ordre, hors code et hors crochets échappés. |
| `WikiLinkParser.Rewrite(text, retarget, options)` | Réécrit les cibles choisies par la fonction ; rend l'instance reçue si rien ne change. |
| `WikiLinkParser.Format(target, heading, blockId, alias, embed)` | Compose un lien relisible à l'identique. |
| `WikiLinkParser.IsValidTarget(target)` | La cible passerait-elle dans un lien sans en casser la syntaxe ? |
| `WikiLinkMatch` | `Target`, `Heading`, `BlockId`, `Alias`, `IsEmbed`, `DisplayText` et positions (`Start`, `TargetStart`, `AliasStart`…). |

Règles de lecture : un lien tient sur une ligne et se ferme au premier `]]` ; la cible
s'arrête au premier `#` ou `|` ; un `\|` (alias dans un tableau) sépare aussi l'alias ; un
`[[` rencontré avant la fermeture fait échouer le lien extérieur au profit de l'intérieur.

**Ne pas** l'utiliser pour **résoudre** un lien vers un fichier : savoir que `[[Plan]]` désigne
`Projets/Plan.md` dépend de l'arborescence du coffre, et reste l'affaire de l'appelant. Ne pas
non plus s'en servir pour les liens Markdown classiques `[texte](url)` : un moteur Markdown
(Markdig) les lit déjà.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `AllowEmbeds` | `bool` | `true` | Reconnaître les intégrations `![[...]]` ; désactivé, le `!` reste de la prose. |
| `SkipCode` | `bool` | `true` | Ignorer les liens écrits dans du code (`FindAll` et `Rewrite` seulement). |
| `MaxLength` | `int` | `512` | Longueur maximale du contenu entre crochets ; borne un `[[` jamais refermé. |

`WikiLinkOptions.Validate()` refuse un `MaxLength` inférieur à 1 (`ArgumentOutOfRangeException`).
Passer `null` en options prend les défauts. `TryParseAt` et `IsValidTarget` ne lèvent jamais.

## Exemple

```csharp
using Micro.Markdown.WikiLink;

string note = "Voir [[Plan de vol#Décollage|le décollage]] et ![[carte.png]].";

foreach (WikiLinkMatch link in WikiLinkParser.FindAll(note))
{
    Console.WriteLine($"{link.Target} → {link.DisplayText} (intégration : {link.IsEmbed})");
}

// Renommer « Plan de vol » en « Checklist » partout, alias et sections conservés.
string renomme = WikiLinkParser.Rewrite(note, l => l.Target == "Plan de vol" ? "Checklist" : null);
// → "Voir [[Checklist#Décollage|le décollage]] et ![[carte.png]]."

string lien = WikiLinkParser.Format("Checklist", heading: "Atterrissage");   // "[[Checklist#Atterrissage]]"
```
