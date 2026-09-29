using System.Globalization;
using System.Text;

namespace Micro.Net.MacAddress;

/// <summary>
/// Analyse une adresse MAC écrite dans l'un des formats que produisent les systèmes et les
/// équipements, et la ramène à une forme unique — la seule façon de rapprocher sans faux
/// négatifs une entrée de table ARP, une trame DHCP et une saisie humaine.
/// </summary>
/// <remarks>
/// <para>Formes acceptées en entrée, quelle que soit la casse :</para>
/// <list type="bullet">
///   <item><description>Six groupes de deux chiffres séparés par <c>:</c>, <c>-</c> ou une espace.</description></item>
///   <item><description>Trois groupes de quatre chiffres séparés par des points (convention Cisco).</description></item>
///   <item><description>Douze chiffres accolés.</description></item>
/// </list>
/// <para>
/// Le découpage est vérifié : une chaîne dont les séparateurs ne tombent pas sur des
/// frontières de groupe régulières est refusée, plutôt que recollée au hasard. Seules les
/// adresses EUI-48 (six octets) sont traitées.
/// </para>
/// </remarks>
public static class MacAddressParser
{
    private static readonly char[] GroupSeparators = [':', '-', '.', ' '];

    /// <summary>Analyse une adresse MAC.</summary>
    /// <param name="text">Adresse à analyser, dans l'un des formats reconnus.</param>
    /// <param name="options">Paramétrage de la remise en forme ; les valeurs par défaut s'appliquent si l'argument est nul.</param>
    /// <returns>L'adresse analysée, avec sa forme normalisée et ses attributs.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    /// <exception cref="FormatException">Si la chaîne est vide, comporte un nombre de chiffres inattendu, un caractère non hexadécimal ASCII, ou un découpage irrégulier.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MacAddressOptions.Format"/> ne fait pas partie des valeurs définies.</exception>
    public static MacAddressInfo Parse(string text, MacAddressOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        MacAddressOptions effective = options ?? new MacAddressOptions();
        effective.Validate();

        byte[] bytes = ReadBytes(text);
        return new MacAddressInfo(bytes, Render(bytes, effective));
    }

    /// <summary>
    /// Variante non levante de <see cref="Parse"/> : renvoie faux au lieu de signaler l'échec
    /// par une exception, quelle qu'en soit la cause (chaîne nulle, mal formée, paramétrage invalide).
    /// </summary>
    /// <param name="text">Adresse à analyser ; peut être nulle.</param>
    /// <param name="options">Paramétrage de la remise en forme ; les valeurs par défaut s'appliquent si l'argument est nul.</param>
    /// <param name="info">L'adresse analysée, ou nulle en cas d'échec.</param>
    /// <returns>Vrai si la chaîne a pu être analysée.</returns>
    public static bool TryParse(string? text, MacAddressOptions? options, out MacAddressInfo? info)
    {
        if (text is null)
        {
            info = null;
            return false;
        }

        try
        {
            info = Parse(text, options);
            return true;
        }
        catch (FormatException)
        {
            info = null;
            return false;
        }
        catch (ArgumentException)
        {
            // Couvre ArgumentOutOfRangeException : convention d'écriture inconnue.
            info = null;
            return false;
        }
    }

    /// <summary>
    /// Remet une adresse en forme sans exposer le reste de l'analyse — le cas d'usage le plus
    /// fréquent, quand seule la clé de rapprochement compte.
    /// </summary>
    /// <param name="text">Adresse à normaliser.</param>
    /// <param name="options">Paramétrage de la remise en forme ; les valeurs par défaut s'appliquent si l'argument est nul.</param>
    /// <returns>L'adresse écrite selon la convention demandée.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    /// <exception cref="FormatException">Si la chaîne n'est pas une adresse MAC reconnaissable.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MacAddressOptions.Format"/> ne fait pas partie des valeurs définies.</exception>
    public static string Normalize(string text, MacAddressOptions? options = null) => Parse(text, options).Formatted;

    private static byte[] ReadBytes(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            throw new FormatException("L'adresse MAC est vide.");
        }

        string[] groups = trimmed.Split(GroupSeparators, StringSplitOptions.None);
        int digitsPerGroup = groups.Length switch
        {
            1 => 12,
            3 => 4,
            6 => 2,
            _ => throw new FormatException(
                $"« {trimmed} » ne se découpe pas en 1, 3 ou 6 groupes : découpage irrégulier."),
        };

        byte[] bytes = new byte[6];
        int index = 0;

        foreach (string group in groups)
        {
            if (group.Length != digitsPerGroup)
            {
                throw new FormatException(
                    $"« {trimmed} » comporte un groupe de {group.Length} chiffre(s) au lieu de {digitsPerGroup}.");
            }

            for (int i = 0; i < group.Length; i += 2)
            {
                if (!char.IsAsciiHexDigit(group[i]) || !char.IsAsciiHexDigit(group[i + 1]))
                {
                    throw new FormatException(
                        $"« {trimmed} » contient un caractère qui n'est pas un chiffre hexadécimal ASCII.");
                }

                bytes[index++] = byte.Parse(group.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
        }

        return bytes;
    }

    private static string Render(byte[] bytes, MacAddressOptions options)
    {
        string digits = options.Uppercase ? "X2" : "x2";
        StringBuilder builder = new(17);

        if (options.Format == MacAddressFormat.Cisco)
        {
            for (int i = 0; i < 6; i += 2)
            {
                if (i > 0)
                {
                    builder.Append('.');
                }

                builder.Append(bytes[i].ToString(digits, CultureInfo.InvariantCulture));
                builder.Append(bytes[i + 1].ToString(digits, CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        char? separator = options.Format switch
        {
            MacAddressFormat.Colon => ':',
            MacAddressFormat.Hyphen => '-',
            _ => null,
        };

        for (int i = 0; i < 6; i++)
        {
            if (i > 0 && separator is { } character)
            {
                builder.Append(character);
            }

            builder.Append(bytes[i].ToString(digits, CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
