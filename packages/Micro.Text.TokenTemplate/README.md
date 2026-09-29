# Micro.Text.TokenTemplate

## Description

Remplace les jetons nommés d'un gabarit — `{nom}` par défaut — par les valeurs d'un
dictionnaire. Le rendu dit aussi **ce qu'il a consommé** et **ce qui lui a manqué** : l'appelant
sait donc quelles valeurs ont déjà servi et lesquelles restent à placer ailleurs. Fonction pure,
déterministe, sans dépendance.

## Mode d'emploi

Utiliser ce package partout où une chaîne de configuration porte des trous à remplir : une
adresse d'API (`https://api/v2/clients/{id}`), un en-tête HTTP, un sujet de courriel, un message
de journal, un chemin de fichier. La liste des jetons consommés est ce qui distingue ce package
d'un simple `Replace` en boucle : elle permet de placer dans le corps d'une requête exactement
les champs que l'adresse n'a pas absorbés.

`Transform` est le point d'échappement contextuel : la même valeur s'insère telle quelle dans un
en-tête et encodée dans une URL. Le faire ici plutôt que chez l'appelant garantit qu'aucune
valeur ne passe au travers.

**Ne pas** l'utiliser comme moteur de gabarit à part entière : il n'y a ni condition, ni boucle,
ni expression — c'est délibéré, un gabarit de configuration qui sait brancher devient un langage
qu'il faut ensuite déboguer. Ne pas non plus s'en servir pour composer du SQL : une valeur
substituée dans une requête est une injection, pas un paramètre.

`string.Format` ne couvre pas ce besoin : il indexe par position, pas par nom, et ne dit rien de
ce qu'il a consommé.

| Membre | Rôle |
|--------|------|
| `TokenTemplate.Render(template, values, options)` | Rend le gabarit. Un gabarit nul ou vide rend un texte vide sans échouer. |
| `TokenTemplate.FindTokens(template, options)` | Relève les noms de jetons sans rien remplacer — pour proposer les cases à remplir, ou vérifier une configuration au démarrage. |
| `TokenTemplateResult.Text` | Le texte rendu. |
| `TokenTemplateResult.UsedTokens` | Jetons effectivement remplacés, sans doublon, dans l'ordre. |
| `TokenTemplateResult.MissingTokens` | Jetons présents au gabarit mais absents du dictionnaire. |
| `TokenTemplateResult.IsComplete` | Tous les jetons ont trouvé leur valeur. |

### Ce qui reste littéral

Un délimiteur qui n'ouvre rien de plausible est rendu tel quel plutôt que de faire échouer le
rendu : délimiteur jamais refermé, délimiteur fermant isolé, jeton sans nom, nom plus long que
`MaxTokenLength`. Un gabarit de configuration est écrit à la main ; il vaut mieux le voir
ressortir intact que perdre tout le reste sur une accolade oubliée.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Open` | `string` | `"{"` | Délimiteur ouvrant. |
| `Close` | `string` | `"}"` | Délimiteur fermant ; doit différer de l'ouvrant. |
| `OnMissing` | `MissingTokenBehavior` | `Leave` | Sort d'un jeton sans valeur : `Leave` le laisse en place, `Blank` l'efface, `Fail` lève `FormatException`. |
| `AllowEscape` | `bool` | `true` | Reconnaître le délimiteur ouvrant doublé comme un délimiteur littéral — sans quoi un gabarit ne pourrait jamais produire de JSON. |
| `Transform` | `Func<string,string>?` | `null` | Transformation appliquée à chaque valeur avant insertion : encodage d'URL, mise en majuscules, masquage. |
| `NameComparer` | `StringComparer` | `Ordinal` | Comparateur des noms de jetons. |
| `MaxTokenLength` | `int` | `128` | Longueur maximale d'un nom ; au-delà, le délimiteur est littéral. Borne un ouvrant jamais refermé. |

`TokenTemplateOptions.Validate()` refuse un délimiteur vide, deux délimiteurs identiques
(`ArgumentException`), un comparateur nul (`ArgumentNullException`) et une longueur maximale
nulle ou négative (`ArgumentOutOfRangeException`).

## Exemple

```csharp
using Micro.Text.TokenTemplate;

var valeurs = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["id"] = "417",
    ["nom"] = "Du Pont & Fils",
    ["ville"] = "Lyon",
};

// L'adresse absorbe « id » ; les valeurs encodées ne peuvent pas casser l'URL.
var adresse = TokenTemplate.Render(
    "https://api/v2/clients/{id}",
    valeurs,
    new TokenTemplateOptions { Transform = Uri.EscapeDataString });

// adresse.Text        → "https://api/v2/clients/417"
// adresse.UsedTokens  → ["id"]

// Ce que l'adresse n'a pas consommé part ailleurs — corps de requête, journal, courriel.
IEnumerable<string> restants = valeurs.Keys.Except(adresse.UsedTokens);
// → "nom", "ville"

// Savoir ce qu'un gabarit réclame, avant même d'avoir les valeurs.
IReadOnlyList<string> attendus = TokenTemplate.FindTokens("Bonjour {prenom}, votre commande {ref} est prête.");
// → ["prenom", "ref"]
```
