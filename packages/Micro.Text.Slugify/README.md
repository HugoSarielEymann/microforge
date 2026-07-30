# Micro.Text.Slugify

## Description

Convertit une chaîne arbitraire en slug URL : suppression des diacritiques, minuscules, séparateur configurable, longueur maximale. Fonction pure et déterministe, sans dépendance.

## Mode d'emploi

Utiliser ce package pour produire des identifiants lisibles dans une URL, un nom de fichier ou une clé de cache à partir d'un titre saisi par un humain. Ne pas l'utiliser pour de l'anonymisation ni pour garantir l'unicité : deux titres proches peuvent produire le même slug (ajouter un suffixe unique côté appelant si nécessaire).

Point d'entrée principal : `Slugifier.ToSlug(input, options)`. Aucun effet de bord, aucun logging : la fonction est pure, la même entrée produit toujours la même sortie.

`Slugifier.IsSlug(value, options)` (depuis 1.1.0) indique si une chaîne est déjà un slug pour ces options — utile pour éviter une réécriture inutile en base ou dans une URL canonique.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Separator` | `char` | `'-'` | Caractère inséré entre les segments. |
| `MaxLength` | `int?` | `null` | Longueur maximale ; la coupe ne laisse pas de séparateur final. |
| `Lowercase` | `bool` | `true` | Passage en minuscules invariantes. |

## Exemple

```csharp
using Micro.Text.Slugify;

var slug = Slugifier.ToSlug("Écrire du C# : 10 astuces !");
// → "ecrire-du-c-10-astuces"

var court = Slugifier.ToSlug("Écrire du C# : 10 astuces !", new SlugifyOptions { MaxLength = 12 });
// → "ecrire-du-c"
```
