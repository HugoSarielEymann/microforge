# Micro.Edit.History

## Description

Historique d'édition générique et borné : `EditHistory<TState>` enregistre les états successifs
d'une valeur, permet d'annuler et de rétablir, et oublie les plus anciens au-delà d'une
profondeur configurable. Le modèle est celui de l'**état complet**, pas celui de la commande
inversible.

## Mode d'emploi

Utiliser ce package pour brancher `Ctrl+Z` / `Ctrl+Y` sur un éditeur : formulaire, constructeur
de schéma, éditeur de texte, plan de travail graphique. À chaque changement, l'appelant appelle
`Record` avec l'état complet ; les raccourcis appellent `TryUndo` / `TryRedo`.

`TState` doit être **immuable**, ou recopié avant d'être enregistré : l'historique conserve les
références telles quelles, et une valeur mutée après coup réécrirait le passé. Un enregistrement
naturel est donc un instantané sérialisé, un `record` ou une structure figée.

**Ne pas** l'utiliser quand chaque état pèse lourd et que les changements sont nombreux — un
document de plusieurs mégaoctets modifié à la frappe demande des commandes inversibles, pas des
instantanés. Ne pas non plus s'en servir depuis plusieurs fils : la classe n'est pas synchronisée,
parce qu'une édition a un seul auteur à la fois.

Enregistrer **efface le futur** : après être revenu en arrière, repartir dans une autre direction
abandonne la branche quittée, comme dans tout éditeur. Un historique arborescent obligerait à
choisir quelle branche rétablir, question qu'aucun raccourci clavier ne sait poser.

| Membre | Rôle |
|--------|------|
| `Current` | État courant. |
| `Record(state)` | Enregistre un nouvel état. Rend `false` si l'état était égal au courant, donc ignoré. |
| `TryUndo(out state)` | Revient d'un pas. Rend `false` si le passé est épuisé ; ne lève jamais. |
| `TryRedo(out state)` | Repart d'un pas. Rend `false` si le futur est épuisé ; ne lève jamais. |
| `Reset(state)` | Repart d'un état neuf, en oubliant tout — à appeler quand l'objet édité change d'identité. |
| `CanUndo` / `CanRedo` | Pour griser les boutons. |
| `UndoDepth` / `RedoDepth` | Nombre de pas restants dans chaque sens. |

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `MaxDepth` | `int` | `100` | Nombre d'annulations conservées ; au-delà, les états les plus anciens sont oubliés. |
| `Comparer` | `IEqualityComparer<TState>` | `EqualityComparer<TState>.Default` | Décide que deux états sont le même, donc qu'un enregistrement ne change rien et doit être ignoré. |

`EditHistoryOptions<TState>.Validate()` refuse un `MaxDepth` nul ou négatif
(`ArgumentOutOfRangeException`) et un `Comparer` nul (`ArgumentNullException`). Passer
`null` en options prend les défauts.

## Exemple

```csharp
using Micro.Edit.History;

// L'état est un instantané immuable du document édité.
var histoire = new EditHistory<string>("");

histoire.Record("Bonjour");
histoire.Record("Bonjour tout le monde");

if (histoire.TryUndo(out string precedent))
{
    // precedent == "Bonjour"
}

if (histoire.TryRedo(out string suivant))
{
    // suivant == "Bonjour tout le monde"
}

// Historique court, pour un formulaire dont on ne remonte jamais loin.
var court = new EditHistory<int>(0, new EditHistoryOptions<int> { MaxDepth = 20 });
```
