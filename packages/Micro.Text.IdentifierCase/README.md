# Micro.Text.IdentifierCase

## Description

Transforme un libellé humain en identifiant de programmation dans la convention choisie : PascalCase, camelCase, snake_case, kebab-case ou SCREAMING_SNAKE_CASE, avec découpage en mots et gestion des mots réservés.

Le libellé est d'abord découpé en mots — sur la ponctuation, les espaces **et** les changements de
casse — puis recomposé. Les diacritiques sont retirés, et les lettres sans forme décomposée
(`ß`, `ø`, `æ`, `œ`, `ł`, `đ`, `þ`…) sont translittérées : « Straße » donne `Strasse`, pas `Strae`.

Fonction pure et déterministe, sans dépendance.

## Mode d'emploi

Utiliser ce package dans tout générateur de code ou de schéma qui doit dériver un nom technique
d'un libellé saisi par un humain : propriétés C# depuis un modèle métier, champs Protobuf en
`snake_case`, colonnes SQL, clés JSON, noms de variables. Le découpage en mots est ce qui distingue
ce package d'un simple remplacement de caractères — c'est lui qui permet le PascalCase.

Deux points d'entrée :

- `IdentifierCaser.ToIdentifier(label, options)` — lève si le libellé ne donne aucun identifiant.
- `IdentifierCaser.TryToIdentifier(label, options, out identifier)` — **ne lève jamais**, y compris
  sur un paramétrage incohérent. C'est la variante à utiliser derrière un champ de saisie, où un
  libellé transitoirement vide ou réduit à de la ponctuation est normal et non une erreur.

Ne pas utiliser ce package pour produire un slug d'URL : `Micro.Text.Slugify` répond à ce besoin,
qui obéit à d'autres règles (longueur, unicité, canonicalisation). Ne pas l'utiliser non plus pour
garantir l'unicité : deux libellés distincts peuvent produire le même identifiant, à l'appelant
d'ajouter un discriminant. Enfin, le package ignore la grammaire du langage cible — il ne connaît
que la liste de mots réservés qu'on lui fournit.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Style` | `IdentifierStyle` | `Pascal` | Convention appliquée : `Pascal`, `Camel`, `Snake`, `Kebab`, `ScreamingSnake`. |
| `AsciiOnly` | `bool` | `true` | Restreint le résultat à `[A-Za-z0-9]`. Décide du sort des écritures sans forme ASCII (cyrillique, grec, idéogrammes) : écartées si vrai, conservées sinon. Les diacritiques latins sont retirés dans les deux cas. |
| `LeadingDigitPrefix` | `string` | `"_"` | Préfixe ajouté si l'identifiant commencerait par un chiffre. Chaîne vide pour ne rien ajouter. |
| `MaxLength` | `int?` | `null` | Longueur maximale ; la coupe ne laisse pas de séparateur final. |
| `ReservedWords` | `IReadOnlySet<string>?` | `null` | Mots réservés du langage cible. Le comparateur porté par le jeu décide de la sensibilité à la casse. |
| `ReservedWordSuffix` | `string` | `"_"` | Suffixe ajouté à un identifiant entrant en collision avec un mot réservé. |

Le suffixe de mot réservé est appliqué **après** la coupe et peut donc dépasser `MaxLength` :
un identifiant trop long reste valide, un identifiant en collision ne l'est pas.

## Exemple

```csharp
using Micro.Text.IdentifierCase;

// Propriété C#
var propriete = IdentifierCaser.ToIdentifier("Numéro de TVA intracommunautaire");
// → "NumeroDeTvaIntracommunautaire"

// Champ Protobuf, avec les mots réservés du langage
var champ = IdentifierCaser.ToIdentifier("Nom du client", new IdentifierCaseOptions
{
    Style = IdentifierStyle.Snake,
    ReservedWords = new HashSet<string>(StringComparer.Ordinal) { "message", "enum", "reserved" },
});
// → "nom_du_client"

// Derrière un champ de saisie : rien ne doit lever pendant la frappe.
if (IdentifierCaser.TryToIdentifier(saisieEnCours, null, out var apercu))
{
    AfficherApercu(apercu);
}
```
