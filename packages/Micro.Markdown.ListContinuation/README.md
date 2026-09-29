# Micro.Markdown.ListContinuation

## Description

Calcule ce qu'un éditeur Markdown doit faire quand on appuie sur Entrée dans une liste ou une
citation : reprendre la même puce (`-`, `*`, `+`), passer au numéro suivant (`2.` après `1.`),
ouvrir une case à cocher vide après une tâche, garder le niveau de citation (`> `, `> > `) —
ou, sur un élément vide, terminer la liste en retirant son marqueur. L'indentation et
l'espacement d'origine sont conservés. Fonction pure et déterministe.

## Mode d'emploi

Utiliser ce package dans tout champ où l'on écrit du Markdown — éditeur de notes, zone de
commentaire, champ de description — pour intercepter Entrée : `OnEnter(ligne, curseur)` dit
s'il faut insérer un préfixe après le saut de ligne (`Continue`), retirer le marqueur au lieu
de sauter une ligne (`End`), ou laisser faire le contrôle (`None`). `TryParseMarker` lit le
marqueur d'une ligne, pour cocher une tâche ou indenter un élément.

| Membre | Rôle |
|--------|------|
| `ListContinuation.OnEnter(line, caret, options)` | Décision : `Kind`, `Prefix`, `RemoveStart`, `RemoveLength`. |
| `ListContinuation.TryParseMarker(line, out marker)` | Puce ou numéro, délimiteur, tâche cochée ou non, début du contenu. Ne lève jamais. |

Un curseur placé avant la fin du marqueur donne un saut ordinaire : on veut alors ouvrir une
ligne au-dessus de l'élément, pas une nouvelle puce.

**Ne pas** l'utiliser pour **renuméroter** toute une liste après une insertion : il ne calcule
que la ligne suivante. Ne pas non plus s'en servir pour analyser un document Markdown complet
(listes paresseuses, blocs imbriqués) : un moteur comme Markdig s'en charge.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `ContinueQuotes` | `bool` | `true` | Poursuivre aussi les citations `> `. |
| `ResetTasks` | `bool` | `true` | Une nouvelle tâche part décochée, même après une tâche cochée. |
| `Increment` | `bool` | `true` | Numéroter l'élément suivant ; sinon répéter le même numéro. |

`ListContinuationOptions.Validate()` n'a rien à refuser : aucun réglage ne peut être
incohérent. `OnEnter` refuse une ligne nulle (`ArgumentNullException`) et un curseur hors de la
ligne (`ArgumentOutOfRangeException`).

## Exemple

```csharp
using Micro.Markdown.ListContinuation;

string ligne = "  3. [x] Relire la note";
ListContinuationResult decision = ListContinuation.OnEnter(ligne, ligne.Length);
// decision.Kind == Continue, decision.Prefix == "  4. [ ] "

ListContinuationResult fin = ListContinuation.OnEnter("- ", 2);
// fin.Kind == End : retirer fin.RemoveLength caractères à partir de fin.RemoveStart
```
