using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Micro.Net.IpRange;

/// <summary>
/// Traduit une expression de plage IPv4 — notation CIDR, intervalle borné, adresse unique,
/// ou union de ces formes — en la liste ordonnée des adresses qu'elle désigne.
/// </summary>
/// <remarks>
/// <para>
/// Formes reconnues, séparées par une virgule ou un point-virgule quand on les combine :
/// </para>
/// <list type="bullet">
///   <item><description><c>192.168.1.0/24</c> — bloc CIDR ; les bits hôtes de l'adresse fournie sont ignorés.</description></item>
///   <item><description><c>192.168.1.10-192.168.1.50</c> — intervalle borné, bornes comprises.</description></item>
///   <item><description><c>192.168.1.10-50</c> — intervalle abrégé : la borne haute ne donne que le dernier octet.</description></item>
///   <item><description><c>192.168.1.42</c> — adresse unique.</description></item>
/// </list>
/// <para>
/// Le résultat est trié par valeur croissante et dédupliqué : deux segments qui se recouvrent
/// ne produisent pas de doublon.
/// </para>
/// </remarks>
public static class IpRangeExpander
{
    private static readonly char[] PartSeparators = [',', ';'];

    /// <summary>Énumère les adresses désignées par une expression de plage IPv4.</summary>
    /// <param name="expression">Expression à analyser (CIDR, intervalle, adresse, ou union séparée par des virgules).</param>
    /// <param name="options">Paramétrage ; les valeurs par défaut s'appliquent si l'argument est nul.</param>
    /// <returns>Les adresses de la plage, triées par valeur croissante et sans doublon.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="expression"/> est nul.</exception>
    /// <exception cref="FormatException">Si l'expression est vide, mal formée, hors IPv4, ou si la borne basse dépasse la borne haute.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <see cref="IpRangeOptions.MaxAddresses"/> est inférieur à 1, ou si l'expression couvre
    /// davantage d'adresses que cette borne ne l'autorise.
    /// </exception>
    public static IReadOnlyList<IPAddress> Expand(string expression, IpRangeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        IpRangeOptions effective = options ?? new IpRangeOptions();
        effective.Validate();

        List<Segment> segments = ParseSegments(expression, effective);
        long total = TotalOf(segments);
        if (total > effective.MaxAddresses)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expression),
                total,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "L'expression couvre {0} adresses, au-delà de la borne MaxAddresses ({1}).",
                    total,
                    effective.MaxAddresses));
        }

        SortedSet<uint> ordered = [];
        foreach (Segment segment in segments)
        {
            for (uint value = segment.Start; ; value++)
            {
                ordered.Add(value);
                if (value == segment.End)
                {
                    break;
                }
            }
        }

        List<IPAddress> addresses = new(ordered.Count);
        foreach (uint value in ordered)
        {
            addresses.Add(ToAddress(value));
        }

        return addresses;
    }

    /// <summary>
    /// Variante non levante de <see cref="Expand"/> : renvoie faux au lieu de signaler l'échec
    /// par une exception, quelle qu'en soit la cause (expression nulle, mal formée, ou trop large).
    /// </summary>
    /// <param name="expression">Expression à analyser ; peut être nulle.</param>
    /// <param name="options">Paramétrage ; les valeurs par défaut s'appliquent si l'argument est nul.</param>
    /// <param name="addresses">Les adresses obtenues, ou une liste vide en cas d'échec.</param>
    /// <returns>Vrai si l'expression a pu être développée.</returns>
    public static bool TryExpand(string? expression, IpRangeOptions? options, out IReadOnlyList<IPAddress> addresses)
    {
        if (expression is null)
        {
            addresses = [];
            return false;
        }

        try
        {
            addresses = Expand(expression, options);
            return true;
        }
        catch (FormatException)
        {
            addresses = [];
            return false;
        }
        catch (ArgumentException)
        {
            // Couvre ArgumentOutOfRangeException : paramétrage invalide ou plage trop large.
            addresses = [];
            return false;
        }
    }

    /// <summary>
    /// Compte les adresses d'une expression sans les matérialiser — de quoi dimensionner un
    /// balayage avant de le lancer. La borne <see cref="IpRangeOptions.MaxAddresses"/> ne
    /// s'applique pas ici : c'est précisément la mesure qui permet de la vérifier.
    /// </summary>
    /// <param name="expression">Expression à analyser.</param>
    /// <param name="options">Paramétrage ; seul <see cref="IpRangeOptions.ExcludeNetworkAndBroadcast"/> influe sur le compte.</param>
    /// <returns>Le nombre d'adresses, somme des tailles des segments avant déduplication.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="expression"/> est nul.</exception>
    /// <exception cref="FormatException">Si l'expression est vide ou mal formée.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="IpRangeOptions.MaxAddresses"/> est inférieur à 1.</exception>
    public static long CountAddresses(string expression, IpRangeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        IpRangeOptions effective = options ?? new IpRangeOptions();
        effective.Validate();

        return TotalOf(ParseSegments(expression, effective));
    }

    private static long TotalOf(List<Segment> segments)
    {
        long total = 0;
        foreach (Segment segment in segments)
        {
            total += (long)segment.End - segment.Start + 1;
        }

        return total;
    }

    private static List<Segment> ParseSegments(string expression, IpRangeOptions options)
    {
        string[] parts = expression.Split(PartSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            throw new FormatException("L'expression de plage est vide.");
        }

        List<Segment> segments = new(parts.Length);
        foreach (string part in parts)
        {
            segments.Add(ParseOne(part, options));
        }

        return segments;
    }

    private static Segment ParseOne(string part, IpRangeOptions options)
    {
        int slash = part.IndexOf('/', StringComparison.Ordinal);
        if (slash >= 0)
        {
            return ParseCidr(part, slash, options);
        }

        int dash = part.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            return ParseInterval(part, dash);
        }

        uint single = ParseAddress(part);
        return new Segment(single, single);
    }

    private static Segment ParseCidr(string part, int slash, IpRangeOptions options)
    {
        uint address = ParseAddress(part[..slash]);
        string suffix = part[(slash + 1)..].Trim();
        if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int prefix) || prefix > 32)
        {
            throw new FormatException($"Longueur de préfixe invalide dans « {part} » : attendu un entier de 0 à 32.");
        }

        uint mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        uint network = address & mask;
        uint broadcast = network | ~mask;

        // Un /31 (liaison point à point) et un /32 (hôte unique) n'ont ni adresse de réseau
        // ni adresse de diffusion utilisables : les exclure les viderait de toute adresse.
        return options.ExcludeNetworkAndBroadcast && prefix <= 30
            ? new Segment(network + 1, broadcast - 1)
            : new Segment(network, broadcast);
    }

    private static Segment ParseInterval(string part, int dash)
    {
        uint start = ParseAddress(part[..dash]);
        string upper = part[(dash + 1)..].Trim();

        uint end;
        if (upper.Contains('.', StringComparison.Ordinal))
        {
            end = ParseAddress(upper);
        }
        else if (byte.TryParse(upper, NumberStyles.None, CultureInfo.InvariantCulture, out byte lastOctet))
        {
            end = (start & 0xFFFFFF00u) | lastOctet;
        }
        else
        {
            throw new FormatException($"Borne haute invalide dans « {part} » : attendu une adresse IPv4 ou un dernier octet de 0 à 255.");
        }

        if (start > end)
        {
            throw new FormatException($"Intervalle inversé dans « {part} » : la borne basse dépasse la borne haute.");
        }

        return new Segment(start, end);
    }

    private static uint ParseAddress(string text)
    {
        string trimmed = text.Trim();
        if (!IPAddress.TryParse(trimmed, out IPAddress? address) || address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new FormatException($"« {trimmed} » n'est pas une adresse IPv4.");
        }

        Span<byte> bytes = stackalloc byte[4];
        address.TryWriteBytes(bytes, out _);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static IPAddress ToAddress(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return new IPAddress(bytes);
    }

    private readonly record struct Segment(uint Start, uint End);
}
