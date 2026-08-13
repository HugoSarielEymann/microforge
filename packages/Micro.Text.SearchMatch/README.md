# Micro.Text.SearchMatch

## Description

Confronte une requête saisie par un humain aux champs interrogeables d'un enregistrement.
La requête est découpée en termes séparés par des blancs ; l'enregistrement répond quand
*chaque* terme apparaît dans *au moins un* de ses champs, casse et diacritiques ignorés.
Fonction pure, déterministe, sans dépendance.

## Mode d'emploi

Utiliser ce package pour le champ de recherche d'une liste, d'une palette de commandes,
d'un catalogue ou d'un registre : partout où quelqu'un tape quelques mots et attend que la
liste se réduise. Le filtrage est tolérant à l'ordre des mots (« client commande » et
« commande client » donnent le même résultat) et aux accents (« resume » trouve « Résumé »).

**Ne pas** l'utiliser pour classer par pertinence : ce package retranche, il ne score pas.
Un classement demande un arbitrage que seul l'appelant peut rendre. Ne pas non plus s'en
servir comme moteur plein texte sur de gros volumes : chaque appel replie les champs de
l'enregistrement, ce qui convient à quelques milliers de lignes en mémoire, pas à un index
persistant. Enfin, ce n'est pas une recherche approximative : une faute de frappe dans le
terme ne trouve rien.

Points d'entrée :

| Méthode | Rôle |
|---------|------|
| `SearchMatcher.Matches(query, fields, options)` | Un enregistrement répond-il ? Les champs nuls ou vides sont ignorés. |
| `SearchMatcher.Matches(query, field, options)` | Même chose pour un champ unique. |
| `SearchMatcher.Filter(source, query, fieldSelector, options)` | Filtre une séquence en conservant l'ordre d'origine. Exécution différée, arguments validés dès l'appel. |
| `SearchMatcher.SplitTerms(query, options)` | Découpe et replie la requête une seule fois. |
| `SearchMatcher.MatchesTerms(terms, fields, options)` | Confronte des termes déjà découpés — à préférer quand la même requête est confrontée à beaucoup d'enregistrements. |

Une requête nulle, vide ou entièrement blanche accepte tout : c'est l'état d'un champ de
recherche que l'on vient d'effacer, et la liste doit alors réapparaître entière. À l'inverse,
un enregistrement sans aucun champ interrogeable ne répond à aucune recherche non vide.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `IgnoreCase` | `bool` | `true` | Ignorer la casse. |
| `IgnoreDiacritics` | `bool` | `true` | Ignorer les accents : « resume » trouve « Résumé ». |
| `WholeWord` | `bool` | `false` | Exiger un mot entier plutôt qu'un fragment : « chat » cesse alors de trouver « achat ». |
| `MinimumTermLength` | `int` | `1` | Longueur en deçà de laquelle un terme est ignoré — sans rejeter la requête : les autres termes restent actifs. |
| `MaximumTerms` | `int` | `12` | Nombre maximal de termes retenus ; au-delà, les termes supplémentaires sont ignorés. Borne, pas validation. |

`SearchMatchOptions.Validate()` refuse un `MinimumTermLength` négatif et un `MaximumTerms`
nul ou négatif, en levant `ArgumentOutOfRangeException`.

## Exemple

```csharp
using Micro.Text.SearchMatch;

record Etape(string Nom, string? Description, string[] Tags);

Etape[] catalogue =
[
    new("Appel REST", "Interroge une API distante", ["api", "http"]),
    new("Requête SQL", "Interroge une base de données", ["base", "sql"]),
    new("Découper un texte", null, ["texte"]),
];

// « interroge base » : les deux termes doivent être présents, l'ordre est libre.
IEnumerable<Etape> retenus = SearchMatcher.Filter(
    catalogue,
    "interroge base",
    e => [e.Nom, e.Description, .. e.Tags]);
// → Requête SQL

// Accents et casse sont neutralisés dans les deux sens.
bool trouve = SearchMatcher.Matches("requete sql", ["Requête SQL"]);
// → true

// Même requête confrontée à beaucoup de lignes : découper une seule fois.
IReadOnlyList<string> termes = SearchMatcher.SplitTerms("interroge base");
foreach (Etape etape in catalogue)
{
    if (SearchMatcher.MatchesTerms(termes, [etape.Nom, etape.Description, .. etape.Tags]))
    {
        // …
    }
}
```
