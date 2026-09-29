# Micro.Text.FileNameSanitize

## Description

Transforme un titre saisi par un humain en nom de fichier valide sous Windows. Les
caractères interdits (`< > : " / \ | ? *`, caractères de contrôle, et ceux que l'appelant
ajoute) sont remplacés — ou simplement omis quand un blanc les jouxte, pour que « Note :
idée » donne « Note idée » et « 12/09 » donne « 12-09 ». Les blancs sont réduits, les points
finaux retirés, les noms réservés (`CON`, `NUL`, `COM1`…) suffixés, la longueur bornée sans
jamais couper un caractère. Fonction pure, déterministe et idempotente.

## Mode d'emploi

Utiliser ce package quand un nom de fichier dérive d'un texte libre : titre de note, nom de
document exporté, pièce jointe enregistrée, sujet d'un courriel archivé. `Sanitize` rend
toujours un nom utilisable (le repli si rien ne reste du titre) ; `IsValid` dit si un nom
saisi est déjà propre, pour refuser une saisie plutôt que la corriger en silence.

| Membre | Rôle |
|--------|------|
| `FileNameSanitizer.Sanitize(name, options)` | Nom de fichier valide, jamais vide. |
| `FileNameSanitizer.IsValid(name, options)` | `Sanitize` rendrait-il ce nom inchangé ? Ne lève jamais. |

**Ne pas** l'utiliser pour garantir l'**unicité** : deux titres différents peuvent donner le
même nom — dédoublonner ensuite (numérotation). Ne pas non plus s'en servir pour un **chemin**
complet : les séparateurs de dossier sont justement des caractères interdits dans un nom.
Enfin, ce n'est pas une slugification : accents, majuscules et espaces sont conservés, parce
qu'un nom de fichier se lit par un humain, pas dans une URL.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Replacement` | `string` | `"-"` | Mis à la place d'un caractère interdit ; omis près d'un blanc et en bordure, jamais doublé. Vide : on retire. |
| `ExtraInvalidCharacters` | `string` | `""` | Caractères refusés en plus de ceux de Windows (ex. `"#^[]"` pour des liens wiki). |
| `MaxLength` | `int` | `200` | Longueur maximale en unités UTF-16 ; coupe sur une frontière de caractère perçu. |
| `Fallback` | `string` | `"untitled"` | Nom rendu quand il ne reste rien ; doit être lui-même valide. |
| `ReservedNameSuffix` | `string` | `"_"` | Suffixe accolé à un nom réservé par Windows. |
| `CollapseWhitespace` | `bool` | `true` | Réduire chaque suite de blancs à une espace. |
| `AllowLeadingDot` | `bool` | `false` | Accepter un point en tête (sinon le nom deviendrait masqué). |

`FileNameSanitizeOptions.Validate()` refuse une chaîne nulle (`ArgumentNullException`), un
`MaxLength` inférieur à 1 (`ArgumentOutOfRangeException`), un remplacement ou un suffixe qui
contient un caractère interdit, un suffixe vide, un repli invalide ou plus long que
`MaxLength` (`ArgumentException`).

## Exemple

```csharp
using Micro.Text.FileNameSanitize;

var options = new FileNameSanitizeOptions
{
    ExtraInvalidCharacters = "#^[]",   // caractères qui casseraient un [[lien wiki]]
    Fallback = "Sans titre",
};

string nom = FileNameSanitizer.Sanitize("Réunion 12/09 : C# ?", options);   // "Réunion 12-09 C"
string repli = FileNameSanitizer.Sanitize("???", options);                  // "Sans titre"
bool propre = FileNameSanitizer.IsValid("Plan de vol", options);             // true
```
