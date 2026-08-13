# Micro.Schema.SampleShape

## Description

Déduit l'arbre de forme — noms, natures, répétitions, imbrications — d'un document d'exemple
JSON ou XML, et réémet un exemple à partir d'une forme. Les deux opérations sont réciproques :
c'est ce qui permet d'afficher une forme sous les deux visages à la fois, et de laisser
quelqu'un la modifier par l'un ou par l'autre.

La forme dit ce que l'échantillon **contient**, jamais ce qu'il **vaut** : les valeurs sont lues
pour deviner les natures, puis jetées. Coller une charge utile réelle n'en emporte donc pas les
données.

## Mode d'emploi

Utiliser ce package pour proposer un schéma à partir d'une charge utile que quelqu'un connaît
mieux que la documentation, ou pour offrir une vue « texte » synchronisée à côté d'un
constructeur graphique de structures.

Ce que l'échantillon montre est pris pour argent comptant : un champ absent de l'exemple est
absent de la forme, un tableau vide ne dit rien de ses éléments, un `null` ne dit rien de sa
nature. C'est une **proposition à relire**, pas une vérité — ne pas s'en servir pour valider
une charge utile entrante, ni pour générer un contrat sans relecture humaine.

**Ne pas** l'utiliser comme analyseur JSON ou XML à part entière : il ne rend pas les valeurs.
Ne pas non plus attendre de lui qu'il devine une union (« ce champ est un nombre *ou* une
chaîne ») : deux natures étrangères se réconcilient en texte, faute de terrain d'entente.

| Méthode | Rôle |
|---------|------|
| `SampleShapeReader.FromJson(sample, options)` | Forme d'un échantillon JSON. Lève `FormatException` si le document est illisible. |
| `SampleShapeReader.TryFromJson(sample, out shape, out error, options)` | Même chose sans lever ; `error` est rédigé pour être affiché. |
| `SampleShapeReader.FromXml` / `TryFromXml` | Idem pour XML. La racine garde son nom d'élément. |
| `SampleShapeReader.Merge(left, right)` | Réunit deux lectures d'un même champ en une forme qui les décrit toutes les deux. |
| `SampleShapeWriter.ToJson(root, options)` | Échantillon JSON représentant la forme. |
| `SampleShapeWriter.ToXml(root, options)` | Échantillon XML ; un champ répété y est écrit plusieurs fois. |
| `ShapeNode.Leaf` / `ShapeNode.Structure` | Construction d'une forme à la main. |

Préférer les variantes `TryFromJson` / `TryFromXml` partout où l'échantillon vient d'un champ
de saisie : quelqu'un qui tape du JSON passe l'essentiel de son temps dans un état invalide,
et chaque frappe ne doit pas coûter une exception.

### Ce que l'aller-retour conserve

JSON porte des types : un aller-retour y conserve la forme entière, à ceci près que **JSON ne
nomme pas sa racine** — elle reprend `RootName` à la relecture.

XML n'en porte pas : tout y est texte, et un entier écrit `0` se relit comme du texte. La
répétition, elle, survit — c'est à cela que sert `RepeatedSampleCount`. Les attributs sont lus
comme des champs mais réécrits en éléments : la forme survit, la syntaxe exacte non.

### Sûreté

Les DTD sont refusées et le résolveur XML est coupé : un échantillon collé peut venir de
n'importe où, et une entité externe irait lire un fichier local. Un document imbriqué au-delà
de **64 niveaux** est déclaré illisible — une borne fixe, qui protège l'analyseur avant que la
moindre forme n'existe.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `RootName` | `string` | `"racine"` | Nom donné à la racine quand l'échantillon n'en porte pas (JSON), et repli à l'écriture XML. |
| `MaxDepth` | `int` | `24` | Profondeur au-delà de laquelle la **forme** cesse de descendre ; les nœuds atteints sont rendus sans enfants. Au-delà de 64, sans effet : le **document**, lui, est refusé. |
| `MaxFieldsPerObject` | `int` | `512` | Nombre maximal de champs retenus par structure. |
| `DetectTimestamps` | `bool` | `true` | Reconnaître les instants ISO 8601 dans les chaînes. À couper quand des chaînes ressemblent à des dates sans en être. |
| `ReadXmlAttributes` | `bool` | `true` | Lire les attributs XML comme des champs. Un attribut homonyme d'un élément frère est écarté — l'élément fait foi. |
| `Indent` | `bool` | `true` | Indenter l'échantillon écrit. |
| `TextSample` | `string` | `"texte"` | Valeur d'exemple écrite pour un champ texte. |
| `TimestampSample` | `string` | `"2026-01-01T00:00:00Z"` | Valeur d'exemple écrite pour un instant. |
| `RepeatedSampleCount` | `int` | `2` | Occurrences écrites pour un champ répété en XML. Doit valoir au moins 2 : en deçà, la répétition ne se relirait plus. |

`SampleShapeOptions.Validate()` refuse une borne nulle ou négative, un `RepeatedSampleCount`
inférieur à 2 (`ArgumentOutOfRangeException`) et un texte d'exemple nul (`ArgumentNullException`).

## Exemple

```csharp
using Micro.Schema.SampleShape;

// Quelqu'un colle une charge utile réelle.
const string colle = """
    {
      "client": { "nom": "Dupont", "age": 42 },
      "lignes": [ { "libelle": "Écrou", "prix": 1.5 } ],
      "cree": "2026-08-05T14:30:00Z"
    }
    """;

if (SampleShapeReader.TryFromJson(colle, out ShapeNode? forme, out string? motif))
{
    // forme.Children : client (structure), lignes (structure répétée), cree (instant)
    string xml = SampleShapeWriter.ToXml(forme!);
    string json = SampleShapeWriter.ToJson(forme!);
}
else
{
    // motif : « Le document JSON est illisible à la ligne 3, position 12 : … »
}

// Construire une forme à la main, puis la montrer.
ShapeNode facture = ShapeNode.Structure("Facture",
[
    ShapeNode.Leaf("numéro", ShapeKind.WholeNumber),
    ShapeNode.Leaf("total", ShapeKind.FractionalNumber),
    ShapeNode.Structure("ligne", [ShapeNode.Leaf("libelle", ShapeKind.Text)], isRepeated: true),
]);

string exemple = SampleShapeWriter.ToJson(facture);
```
