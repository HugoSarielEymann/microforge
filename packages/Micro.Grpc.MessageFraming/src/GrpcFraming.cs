using System.Buffers.Binary;

namespace Micro.Grpc.MessageFraming;

/// <summary>Une trame gRPC lue : le message, et s'il est déclaré compressé.</summary>
/// <param name="IsCompressed">L'indicateur de compression de la trame valait 1.</param>
/// <param name="Payload">Le message, tel quel (compressé s'il l'est).</param>
public readonly record struct GrpcFrame(bool IsCompressed, ReadOnlyMemory<byte> Payload);

/// <summary>Paramétrage de la lecture des trames.</summary>
public sealed class GrpcFramingOptions
{
    /// <summary>Taille maximale d'un message, en octets. Défaut : 4 Mio, la limite de réception par défaut de gRPC.</summary>
    public int MaxMessageSize { get; init; } = 4 * 1024 * 1024;

    /// <summary>
    /// Accepter une trame déclarée compressée. Défaut : <see langword="false"/> — sans négociation
    /// d'encodage (<c>grpc-encoding</c>), une trame compressée est une erreur de l'appelant.
    /// </summary>
    public bool AllowCompressed { get; init; }

    /// <summary>Vérifie la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxMessageSize"/> est négatif.</exception>
    public void Validate() => ArgumentOutOfRangeException.ThrowIfNegative(MaxMessageSize, nameof(MaxMessageSize));
}

/// <summary>
/// Les trames de messages gRPC sur un flux HTTP/2 : un octet d'indicateur de compression, la
/// longueur du message sur quatre octets big-endian, puis le message. C'est la seule chose qu'il
/// faut savoir faire pour servir ou appeler du gRPC sans la pile ASP.NET Core gRPC.
/// </summary>
public static class GrpcFraming
{
    /// <summary>Taille de l'en-tête d'une trame.</summary>
    public const int HeaderSize = 5;

    /// <summary>Construit une trame en mémoire.</summary>
    /// <param name="message">Message à encadrer.</param>
    /// <param name="compressed">Déclarer le message compressé.</param>
    /// <returns>L'en-tête suivi du message.</returns>
    public static byte[] Frame(ReadOnlySpan<byte> message, bool compressed = false)
    {
        byte[] frame = new byte[HeaderSize + message.Length];
        frame[0] = compressed ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(1, 4), (uint)message.Length);
        message.CopyTo(frame.AsSpan(HeaderSize));
        return frame;
    }

    /// <summary>Écrit une trame sur un flux.</summary>
    /// <param name="stream">Flux de destination.</param>
    /// <param name="message">Message à encadrer.</param>
    /// <param name="compressed">Déclarer le message compressé.</param>
    /// <param name="cancellationToken">Annulation.</param>
    /// <exception cref="ArgumentNullException">Si le flux est nul.</exception>
    /// <exception cref="OperationCanceledException">Si l'annulation est demandée.</exception>
    public static async ValueTask WriteAsync(Stream stream, ReadOnlyMemory<byte> message, bool compressed = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] header = new byte[HeaderSize];
        header[0] = compressed ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(1), (uint)message.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lit la trame suivante d'un flux.</summary>
    /// <param name="stream">Flux source.</param>
    /// <param name="options">Paramétrage ; <see langword="null"/> prend les défauts.</param>
    /// <param name="cancellationToken">Annulation.</param>
    /// <returns>La trame ; <see langword="null"/> si le flux se termine proprement, avant tout octet.</returns>
    /// <exception cref="ArgumentNullException">Si le flux est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si le paramétrage est incohérent.</exception>
    /// <exception cref="InvalidDataException">
    /// Si le flux se termine au milieu d'une trame, si le message dépasse la taille maximale, si
    /// l'indicateur de compression n'est ni 0 ni 1, ou si une trame compressée n'est pas admise.
    /// </exception>
    /// <exception cref="OperationCanceledException">Si l'annulation est demandée.</exception>
    public static async ValueTask<GrpcFrame?> ReadAsync(Stream stream, GrpcFramingOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        GrpcFramingOptions effective = options ?? new GrpcFramingOptions();
        effective.Validate();

        byte[] header = new byte[HeaderSize];
        int read = 0;
        while (read < HeaderSize)
        {
            int count = await stream.ReadAsync(header.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                if (read == 0)
                {
                    return null;
                }

                throw new InvalidDataException("Le flux s'arrête au milieu de l'en-tête d'une trame gRPC.");
            }

            read += count;
        }

        if (header[0] > 1)
        {
            throw new InvalidDataException($"Indicateur de compression invalide ({header[0]}) : 0 ou 1 attendu.");
        }

        bool compressed = header[0] == 1;
        if (compressed && !effective.AllowCompressed)
        {
            throw new InvalidDataException("Trame compressée refusée : aucun encodage n'a été négocié.");
        }

        uint length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(1));
        if (length > (uint)effective.MaxMessageSize)
        {
            throw new InvalidDataException($"Message de {length} octets : la taille maximale est de {effective.MaxMessageSize} octets.");
        }

        byte[] payload = new byte[length];
        try
        {
            await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException($"Le flux s'arrête avant la fin d'un message annoncé de {length} octets.", ex);
        }

        return new GrpcFrame(compressed, payload);
    }
}
