# Micro.Text.ByteSize

## Description

Met une quantité d'octets — ou un débit — sous la forme la plus courte qu'un humain lise
sans compter les chiffres. La valeur est mise à l'échelle jusqu'au préfixe qui la ramène
sous le facteur (1024 ou 1000), puis arrondie à trois chiffres significatifs :
`1,23 Mio`, `12,3 Mio`, `123 Mio`.

Deux détails que les implémentations rapides ratent, et qui justifient ce package :

- **L'arrondi précède le choix du préfixe.** 1 048 575 octets valent 1023,999 Kio ;
  arrondis à l'unité, ils donneraient « 1024 KiB ». Le préfixe est remonté d'un cran.
- **`Math.Abs(long.MinValue)` lève.** La mise à l'échelle passe par un `double` avant
  toute valeur absolue, et l'extrême négatif de `Int64` se formate comme les autres.

L'unité affichée se déduit du symbole d'octet : `"B"` donne `B, KiB, MiB`, `"o"` donne
`o, Kio, Mio`. Un seul paramètre couvre ainsi les deux conventions.

## Mode d'emploi

**Quand l'utiliser** — partout où un compteur d'octets est montré à un humain : taille de
fichier, volume transféré, débit instantané, quota. Le rendu par défaut est déterministe
(culture invariante) : il ne change pas selon la culture du thread appelant.

**Quand ne pas l'utiliser** — pour des octets destinés à une machine (journal structuré,
API, colonne de base) : la valeur brute se compare et s'agrège, la chaîne non. Pour un
alignement en colonne à largeur fixe, la précision adaptative fait varier la longueur du
nombre : désactiver `AdaptivePrecision` et fixer `MaxDecimals`.

Deux points d'entrée sur `ByteSizeFormatter` :

- `Format(bytes, options)` — une quantité (`Int64`, signe conservé).
- `FormatRate(bytesPerSecond, options)` — un débit (`Double`), suffixé `/s`. Une valeur
  non finie — `NaN` d'une division `0/0`, infini d'une division par une durée nulle —
  rend `NonFiniteText` plutôt qu'un texte aberrant.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `UnitSystem` | `ByteSizeUnitSystem` | `Binary` | `Binary` : facteur 1024, préfixes `Ki, Mi, Gi…`. `Metric` : facteur 1000, préfixes `k, M, G…` (convention des constructeurs de disques). |
| `ByteSymbol` | `string` | `"B"` | Symbole de l'unité de base, accolé au préfixe. `"o"` produit `o, Kio, Mio`. |
| `MaxDecimals` | `int` | `2` | Plafond de décimales, de 0 à 15. La précision adaptative ne peut que descendre en dessous. |
| `AdaptivePrecision` | `bool` | `true` | Vise trois chiffres significatifs. Désactivée, `MaxDecimals` s'applique systématiquement. Les octets non préfixés restent toujours entiers. |
| `Separator` | `string` | `" "` | Séparateur entre le nombre et l'unité ; espace insécable par défaut, pour qu'un retour à la ligne ne les sépare pas. |
| `RateSuffix` | `string` | `"/s"` | Suffixe ajouté par `FormatRate`. |
| `NonFiniteText` | `string` | `"—"` | Rendu d'un débit `NaN` ou infini. |
| `FormatProvider` | `IFormatProvider?` | `null` | Culture du nombre — elle décide du séparateur décimal. Nulle, la culture invariante s'applique. |

`ByteSizeOptions.Validate()` signale un paramétrage incohérent par
`ArgumentOutOfRangeException` (décimales hors bornes) ou `ArgumentNullException`
(chaîne nulle).

## Exemple

```csharp
using Micro.Text.ByteSize;
using System.Globalization;

// Convention française : octets, préfixes binaires, virgule décimale.
ByteSizeOptions fr = new()
{
    ByteSymbol = "o",
    FormatProvider = CultureInfo.GetCultureInfo("fr-FR"),
};

string taille = ByteSizeFormatter.Format(1_610_612_736, fr);   // « 1,50 Gio »
string debit = ByteSizeFormatter.FormatRate(octets / secondes, fr); // « 2,34 Mio/s »

// Durée nulle : le débit vaut l'infini, et le rendu reste présentable.
string inconnu = ByteSizeFormatter.FormatRate(1024d / 0d, fr); // « — »
```
