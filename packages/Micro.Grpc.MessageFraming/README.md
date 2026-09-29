# Micro.Grpc.MessageFraming

## Description

Lit et écrit les **trames de messages gRPC** sur un flux : un octet d'indicateur de compression
(0 ou 1), la longueur du message sur quatre octets big-endian, puis le message. C'est la seule
chose qu'il faut savoir faire, au-dessus d'HTTP/2, pour servir ou appeler du gRPC sans la pile
gRPC d'ASP.NET Core — par exemple quand les méthodes ne sont connues qu'à l'exécution. La
lecture refuse un flux tronqué, un message trop grand (sans rien allouer) et une trame
compressée non négociée ; elle rend `null` à la fin propre du flux.

## Mode d'emploi

À utiliser pour lire le corps d'une requête gRPC (`application/grpc`) dans un point de
terminaison ASP.NET Core écrit à la main, et pour écrire la réponse avant ses en-têtes de fin
(`grpc-status`, `grpc-message`). Associé à un codec Protobuf dynamique, il sert des méthodes
gRPC décrites au moment de l'exécution.

**Ne pas** l'utiliser avec `Grpc.AspNetCore` ou `Grpc.Net.Client` : ils encadrent déjà leurs
messages. Il ne décompresse rien : une trame compressée admise (`AllowCompressed`) est rendue
telle quelle, à l'appelant d'appliquer l'encodage négocié. Il ne produit pas les en-têtes de fin
gRPC, qui relèvent du serveur HTTP.

| Membre | Rôle |
|--------|------|
| `GrpcFraming.Frame(message, compressed)` | La trame en mémoire (en-tête + message). |
| `GrpcFraming.WriteAsync(stream, message, compressed, ct)` | Écrit une trame sur un flux. |
| `GrpcFraming.ReadAsync(stream, options, ct)` | La trame suivante, ou `null` à la fin propre du flux ; `InvalidDataException` sur un flux tronqué ou refusé. |
| `GrpcFrame.IsCompressed`, `GrpcFrame.Payload` | L'indicateur et le message lus. |
| `GrpcFraming.HeaderSize` | 5 : un octet d'indicateur, quatre de longueur. |

## Paramétrage

| Paramètre | Type | Défaut | Rôle |
|-----------|------|--------|------|
| `MaxMessageSize` | `int` | `4194304` (4 Mio) | Taille maximale d'un message lu, borne incluse ; au-delà, `InvalidDataException` avant toute allocation. |
| `AllowCompressed` | `bool` | `false` | Admettre une trame dont l'indicateur de compression vaut 1. |

`GrpcFramingOptions.Validate()` refuse une taille maximale négative (`ArgumentOutOfRangeException`).
Passer `null` en options prend les défauts.

## Exemple

```csharp
using Micro.Grpc.MessageFraming;

// Dans un point de terminaison ASP.NET Core qui sert « /paquet.Service/Methode » en HTTP/2.
async Task RepondreAsync(HttpContext http, Func<byte[], byte[]> traiter)
{
    GrpcFrame? requete = await GrpcFraming.ReadAsync(http.Request.Body, new GrpcFramingOptions { MaxMessageSize = 1_000_000 }, http.RequestAborted);
    byte[] reponse = traiter(requete?.Payload.ToArray() ?? []);

    http.Response.ContentType = "application/grpc";
    await GrpcFraming.WriteAsync(http.Response.Body, reponse, cancellationToken: http.RequestAborted);
    http.Response.AppendTrailer("grpc-status", "0");
}

byte[] trame = GrpcFraming.Frame(new byte[] { 0x08, 0x96, 0x01 });   // 00 00 00 00 03 08 96 01
```
