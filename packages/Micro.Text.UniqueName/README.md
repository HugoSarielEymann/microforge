# Micro.Text.UniqueName

## Description

Choisit un nom libre en numérotant le nom voulu quand il est déjà pris : « Sans titre »,
« Sans titre 2 », « Sans titre 3 »… Le format de numérotation se règle (« Copie (2) » à la
manière de l'Explorateur, « rapport-2 » pour un identifiant), la casse est ignorée par défaut
comme sur les systèmes de fichiers de Windows et macOS, et un nom déjà numéroté voit sa
numérotation **poursuivie** : dupliquer « Sans titre 2 » donne « Sans titre 3 », pas
« Sans titre 2 2 ». Fonction pure : la disponibilité est jugée par l'appelant.

## Mode d'emploi

Utiliser ce package chaque fois qu'un nom doit être créé sans écraser l'existant : nouvelle
note, duplication d'un fichier, image collée, onglet ou calque dupliqué, export répété.
L'appelant fournit soit la liste des noms pris, soit une fonction qui répond « ce candidat
est-il pris ? » — un test d'existence de fichier, une requête, un ensemble en mémoire.

| Membre | Rôle |
|--------|------|
| `UniqueNamer.MakeUnique(desired, existing, options)` | Nom libre au regard d'une liste (comparateur des réglages). |
| `UniqueNamer.MakeUnique(desired, isTaken, options)` | Nom libre au regard d'une fonction, interrogée dans l'ordre des candidats. |

**Ne pas** compter sur ce package pour une garantie d'exclusivité **concurrente** : entre le
choix du nom et sa création, un autre acteur peut le prendre. Pour un fichier, créer en mode
exclusif (`FileMode.CreateNew`) et recommencer en cas d'échec. Ne pas non plus l'utiliser
pour produire des identifiants opaques ou aléatoires : un GUID y répond mieux.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Format` | `string` | `"{0} {1}"` | Forme d'un nom numéroté : `{0}` le nom de base, `{1}` le numéro. |
| `FirstNumber` | `int` | `2` | Premier numéro essayé ; le nom nu compte comme le premier. |
| `ContinueNumbering` | `bool` | `true` | Relire un nom déjà numéroté selon `Format` et poursuivre sa numérotation. |
| `MaxAttempts` | `int` | `10000` | Candidats essayés, nom nu compris, avant `InvalidOperationException`. |
| `Comparer` | `IEqualityComparer<string>` | `StringComparer.OrdinalIgnoreCase` | Égalité des noms pour la surcharge à liste. |

`UniqueNameOptions.Validate()` refuse un `Format` ou un `Comparer` nul
(`ArgumentNullException`), un format sans `{0}` et `{1}` ou invalide (`ArgumentException`),
un `FirstNumber` négatif et un `MaxAttempts` inférieur à 1 (`ArgumentOutOfRangeException`).

## Exemple

```csharp
using Micro.Text.UniqueName;

string[] notes = ["Sans titre", "Sans titre 2"];
string nouvelle = UniqueNamer.MakeUnique("Sans titre", notes);          // "Sans titre 3"

var explorateur = new UniqueNameOptions { Format = "{0} ({1})" };
string copie = UniqueNamer.MakeUnique("Plan", n => File.Exists(n + ".md"), explorateur);
// "Plan", sinon "Plan (2)", "Plan (3)"…
```
