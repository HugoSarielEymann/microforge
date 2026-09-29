# Micro.Net.MacAddress

## Description

Analyse une adresse MAC écrite dans l'un des formats que produisent les systèmes et les
équipements, et la ramène à une forme unique. C'est la condition pour rapprocher sans faux
négatifs une entrée de table ARP (`aa-bb-cc-dd-ee-ff`), une trame DHCP (`AABBCCDDEEFF`),
une configuration Cisco (`aabb.ccdd.eeff`) et une saisie humaine.

L'analyse expose en outre ce que le premier octet encode, et que la simple chaîne cache :

| Attribut | Ce qu'il révèle |
|----------|-----------------|
| `Oui` | Le préfixe fabricant, clé de recherche dans les catalogues d'attributions IEEE. |
| `IsLocallyAdministered` | L'adresse n'a pas été attribuée par l'IEEE : adresse **aléatoire** tirée pour la vie privée, ou forgée. Chercher un fabricant n'a alors aucun sens. |
| `IsMulticast` | La trame vise un groupe. Une adresse *source* ne devrait jamais porter ce bit. |
| `IsBroadcast` / `IsUnspecified` | `FF:…:FF` et `00:…:00`, que les piles réseau rendent à la place d'une absence de réponse. |

**Formats acceptés en entrée**, quelle que soit la casse : six groupes de deux chiffres
séparés par `:`, `-` ou une espace ; trois groupes de quatre séparés par des points ;
douze chiffres accolés. Le découpage est vérifié — une chaîne dont les groupes n'ont pas
la taille attendue est refusée plutôt que recollée au hasard — et seuls les chiffres
hexadécimaux **ASCII** sont admis : un homoglyphe cyrillique ou un chiffre pleine chasse
est rejeté, jamais confondu avec son sosie latin.

## Mode d'emploi

**Quand l'utiliser** — dès qu'une adresse MAC vient d'ailleurs que de votre propre code :
sortie de commande système, table ARP, réponse SNMP, journal d'équipement, formulaire.
Normaliser à l'entrée, puis ne comparer que des formes normalisées.

**Quand ne pas l'utiliser** — si l'adresse vient déjà d'un
`System.Net.NetworkInformation.PhysicalAddress`, la bibliothèque standard suffit pour la
rendre en chaîne. Ce package sert à *entrer* dans ce monde, pas à le remplacer. Il ne
traite pas non plus les adresses EUI-64 (huit octets, Bluetooth et 802.15.4) : elles sont
refusées explicitement plutôt que tronquées.

Trois points d'entrée sur `MacAddressParser` :

- `Parse(text, options)` — l'adresse analysée, attributs compris ; lève sur entrée invalide.
- `TryParse(text, options, out info)` — la même chose sans jamais lever, y compris sur nul.
- `Normalize(text, options)` — la forme normalisée seule, le cas d'usage le plus fréquent.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `Format` | `MacAddressFormat` | `Colon` | Convention d'écriture du résultat : `Colon` (`AA:BB:CC:DD:EE:FF`), `Hyphen` (`AA-BB-CC-DD-EE-FF`, convention Windows), `Bare` (`AABBCCDDEEFF`), `Cisco` (`aabb.ccdd.eeff`). |
| `Uppercase` | `bool` | `true` | Chiffres hexadécimaux en majuscules. |

`MacAddressOptions.Validate()` refuse une convention d'écriture hors énumération par
`ArgumentOutOfRangeException`.

## Exemple

```csharp
using Micro.Net.MacAddress;

// Rapprocher deux sources qui n'écrivent pas pareil.
string cleArp = MacAddressParser.Normalize("3c-22-fb-01-02-03");   // « 3C:22:FB:01:02:03 »
string cleDhcp = MacAddressParser.Normalize("3C22FB010203");        // la même
bool memeAppareil = string.Equals(cleArp, cleDhcp, StringComparison.Ordinal);

// Décider s'il vaut la peine de chercher un fabricant.
if (MacAddressParser.TryParse(brut, null, out MacAddressInfo? info) && !info!.IsLocallyAdministered)
{
    string? fabricant = catalogueIeee.Lookup(info.Oui);
}
```
