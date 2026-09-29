# Micro.Protobuf.DynamicCodec

## Description

Encode et décode des messages Protobuf (proto3) **sans code généré** : la forme des messages
se décrit à l'exécution (`ProtoMessageDescriptor`, `ProtoField`), les valeurs se posent par
numéro de champ (`ProtoMessage`), et `ProtoCodec` écrit ou lit les octets du format binaire
officiel. Scalaires (varints, zigzag, tailles fixes, flottants), textes UTF-8, octets,
énumérations, messages imbriqués et récursifs, champs répétés « packés » ou non, présence
(`optional`), fusion des occurrences multiples d'un message, champs inconnus ignorés (groupes
compris). Aucune dépendance : le format est implémenté ici, et vérifié contre Google.Protobuf.

## Mode d'emploi

À utiliser quand la forme des messages **n'est connue qu'à l'exécution** : un outil qui
expose ou appelle des méthodes gRPC décrites par l'utilisateur, un proxy qui relit un message
pour en extraire un champ, un banc d'essai qui fabrique des charges utiles à la volée. Associé
à `Grpc.Net.Client` et un marshaller d'octets, il permet d'appeler n'importe quel serveur gRPC
sans `.proto` compilé.

**Ne pas** l'utiliser quand les messages sont connus à la compilation : le code généré par
`Grpc.Tools` est plus rapide et typé. Il ne produit ni ne lit de `.proto` (texte), ni de JSON
Protobuf ; les types bien connus (`google.protobuf.Timestamp`…) se décrivent comme n'importe
quel message (`seconds` int64 n° 1, `nanos` int32 n° 2).

| Membre | Rôle |
|--------|------|
| `ProtoMessageDescriptor(name).Add(field)` | Décrit un message ; se référence lui-même pour un message récursif. |
| `ProtoField(number, name, type, isRepeated, messageType, hasPresence)` | Un champ ; refuse les numéros hors de 1 à 536 870 911 et la plage réservée 19 000–19 999. |
| `ProtoMessage.Set / Get / TryGet / GetList` | Pose et lit les valeurs ; `Set` vérifie le type CLR (`long` pour int64, `int` pour int32 et les énumérations, `byte[]` pour bytes, `ProtoMessage` pour un message, une liste pour un champ répété). |
| `ProtoCodec.Encode(message, options)` | Les octets du message, sans préfixe de longueur. |
| `ProtoCodec.Decode(descriptor, data, options)` | Le message ; lève `FormatException` sur des octets invalides. |
| `ProtoCodec.TryDecode(descriptor, data, out message, out error, options)` | Même chose sans jamais lever : faux et le motif. |

Correspondance des types : `double`→`double`, `float`→`float`, `int64`/`sint64`/`sfixed64`→`long`,
`uint64`/`fixed64`→`ulong`, `int32`/`sint32`/`sfixed32`/énumération→`int`,
`uint32`/`fixed32`→`uint`, `bool`→`bool`, `string`→`string`, `bytes`→`byte[]`.

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `MaxDepth` | `int` | `64` | Profondeur maximale de messages imbriqués, à l'écriture comme à la lecture : un message hostile ne fait pas déborder la pile. |
| `EmitDefaultScalars` | `bool` | `false` | Écrire aussi 0, faux et texte vide pour les champs sans présence (proto3 les omet, les lecteurs les restituent). |
| `PackRepeatedScalars` | `bool` | `true` | Écrire les numériques répétés en bloc ; la lecture accepte les deux formes. |
| `StrictUtf8` | `bool` | `true` | Refuser à la lecture un texte qui n'est pas de l'UTF-8 valide (`FormatException`). |

`ProtoCodecOptions.Validate()` refuse un `MaxDepth` inférieur à 1 (`ArgumentOutOfRangeException`).
Passer `null` en options prend les défauts.

## Exemple

```csharp
using Micro.Protobuf.DynamicCodec;

// message Ligne { string reference = 1; int64 quantite = 2; }
// message Commande { int64 client = 1; repeated Ligne lignes = 2; }
var ligne = new ProtoMessageDescriptor("Ligne")
    .Add(new ProtoField(1, "reference", ProtoFieldType.String))
    .Add(new ProtoField(2, "quantite", ProtoFieldType.Int64));
var commande = new ProtoMessageDescriptor("Commande")
    .Add(new ProtoField(1, "client", ProtoFieldType.Int64))
    .Add(new ProtoField(2, "lignes", ProtoFieldType.Message, isRepeated: true, messageType: ligne));

var premiere = new ProtoMessage(ligne);
premiere.Set(1, "ECR-M8");
premiere.Set(2, 100L);

var message = new ProtoMessage(commande);
message.Set(1, 42L);
message.Set(2, new[] { premiere });

byte[] octets = ProtoCodec.Encode(message);

if (ProtoCodec.TryDecode(commande, octets, out ProtoMessage? relu, out string? motif))
{
    long client = (long)relu.Get(1)!;                         // 42
    var lignes = relu.GetList(2);                             // 1 élément
    string reference = (string)((ProtoMessage)lignes[0]).Get(1)!;
}
```
