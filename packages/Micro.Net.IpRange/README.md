# Micro.Net.IpRange

## Description

Traduit une expression de plage IPv4 en la liste ordonnée des adresses qu'elle désigne.
Quatre formes sont reconnues, combinables par virgule ou point-virgule :

| Forme | Exemple | Sens |
|-------|---------|------|
| CIDR | `192.168.1.0/24` | Le bloc entier ; les bits hôtes de l'adresse fournie sont ignorés (`192.168.1.42/24` désigne le même bloc). |
| Intervalle | `192.168.1.10-192.168.1.50` | Bornes comprises. |
| Intervalle abrégé | `192.168.1.10-50` | La borne haute ne donne que le dernier octet. |
| Adresse unique | `192.168.1.42` | Une seule adresse. |

Le résultat est trié par valeur croissante et dédupliqué : deux segments qui se recouvrent
ne produisent pas de doublon. IPv4 uniquement — énumérer un préfixe IPv6 n'a pas de sens
pratique, et une entrée IPv6 est refusée explicitement plutôt que silencieusement ignorée.

## Mode d'emploi

**Quand l'utiliser** — dès qu'une saisie humaine désignant un ensemble d'adresses doit
devenir une séquence à parcourir : balayage de découverte, liste d'autorisation, ciblage
d'un test d'intégration, pré-remplissage d'un formulaire à partir du sous-réseau courant.

**Quand ne pas l'utiliser** — pour tester l'appartenance d'une adresse à un préfixe, la
bibliothèque standard suffit et coûte infiniment moins cher : `System.Net.IPNetwork.Parse`
puis `Contains`. Ce package ne se justifie que si les adresses doivent être *énumérées*,
ce que `IPNetwork` ne fait pas. Il ne convient pas non plus au stockage d'un très grand
bloc : la borne `MaxAddresses` est là pour rappeler qu'un `/8` matérialisé, c'est seize
millions d'objets.

Trois points d'entrée, tous sur `IpRangeExpander` :

- `Expand(expression, options)` — la liste des adresses ; lève sur entrée invalide.
- `TryExpand(expression, options, out addresses)` — la même chose sans jamais lever,
  y compris sur une expression nulle ou une plage plus large que la borne.
- `CountAddresses(expression, options)` — le compte seul, sans matérialisation, porté
  par un `Int64` parce que l'espace IPv4 entier ne tient pas dans un `Int32`. La borne
  `MaxAddresses` ne s'y applique pas : c'est la mesure qui permet de la vérifier.

Le compte précède la déduplication : il majore le nombre d'adresses réellement produites
quand des segments se recouvrent.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `ExcludeNetworkAndBroadcast` | `bool` | `true` | Écarte l'adresse de réseau et l'adresse de diffusion d'un bloc CIDR. Sans effet sur `/31` (liaison point à point) et `/32` (hôte unique), qui n'en comportent pas au sens usuel. |
| `MaxAddresses` | `int` | `65536` | Nombre maximal d'adresses que l'expansion accepte de produire. Au-delà, `Expand` lève `ArgumentOutOfRangeException` et `TryExpand` renvoie faux. Doit valoir au moins 1. |

`IpRangeOptions.Validate()` signale un paramétrage incohérent par
`ArgumentOutOfRangeException`.

## Exemple

```csharp
using Micro.Net.IpRange;
using System.Net;

// Le sous-réseau local, hors adresse de réseau et de diffusion.
IReadOnlyList<IPAddress> cibles = IpRangeExpander.Expand("192.168.1.0/24");

// Union de formes, triée et dédupliquée : .1, .2, .3, .10, puis 10.0.0.5.
IReadOnlyList<IPAddress> mixte = IpRangeExpander.Expand("192.168.1.1-3, 192.168.1.10, 10.0.0.5");

// Dimensionner avant de lancer un balayage coûteux.
long combien = IpRangeExpander.CountAddresses("10.0.0.0/16");
if (combien <= 4096 && IpRangeExpander.TryExpand("10.0.0.0/16", null, out IReadOnlyList<IPAddress> lot))
{
    await Parallel.ForEachAsync(lot, (adresse, ct) => SonderAsync(adresse, ct));
}
```
