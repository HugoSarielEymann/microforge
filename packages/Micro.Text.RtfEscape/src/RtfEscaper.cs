using System.Globalization;
using System.Text;

namespace Micro.Text.RtfEscape;

/// <summary>Réglages de l'échappement RTF.</summary>
public sealed class RtfEscapeOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static RtfEscapeOptions Default { get; } = new();

    /// <summary>Mot de contrôle écrit pour chaque fin de ligne. Défaut : <c>"\par "</c> (nouveau paragraphe).</summary>
    /// <remarks><c>"\line "</c> donne un saut de ligne à l'intérieur du même paragraphe.</remarks>
    public string NewLine { get; init; } = "\\par ";

    /// <summary>Caractère de repli écrit après chaque <c>\uN</c>, pour les lecteurs qui ignorent Unicode. Défaut : <c>'?'</c>.</summary>
    /// <remarks>Le document doit déclarer <c>\uc1</c> : un seul caractère de repli suit chaque <c>\uN</c>.</remarks>
    public char Fallback { get; init; } = '?';

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentNullException">Si <see cref="NewLine"/> est nul.</exception>
    /// <exception cref="ArgumentException">Si le repli n'est pas un caractère ASCII imprimable hors syntaxe RTF.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(NewLine);

        if (Fallback is < ' ' or > '~' or '\\' or '{' or '}')
        {
            throw new ArgumentException("Le repli doit être un caractère ASCII imprimable, hors antislash et accolades.", nameof(Fallback));
        }
    }
}

/// <summary>
/// Échappe du texte brut pour l'insérer dans un document RTF.
/// </summary>
/// <remarks>
/// <para>
/// L'antislash et les accolades, qui sont la syntaxe même du RTF, sont protégés ; la tabulation
/// devient <c>\tab</c> ; chaque fin de ligne (<c>\n</c>, <c>\r\n</c> ou <c>\r</c>) devient le
/// mot de contrôle choisi ; les autres caractères de contrôle sont écartés, le RTF n'ayant pas
/// de sens à leur donner. Tout caractère hors ASCII s'écrit <c>\uN?</c>, N étant l'unité UTF-16
/// en entier signé : une paire de substitution donne donc deux <c>\u</c>, comme le veut la norme.
/// </para>
/// <para>
/// Le texte obtenu relu par un contrôle RTF redonne le texte d'origine, caractère pour
/// caractère, fins de ligne mises à part. Fonction pure et déterministe.
/// </para>
/// </remarks>
public static class RtfEscaper
{
    /// <summary>Échappe un texte.</summary>
    /// <param name="text">Texte brut.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Le texte prêt à être placé dans un groupe RTF.</returns>
    /// <exception cref="ArgumentNullException">Si le texte est nul.</exception>
    /// <exception cref="ArgumentException">Si les réglages sont incohérents.</exception>
    public static string Escape(string text, RtfEscapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder builder = new(text.Length + 16);
        Append(builder, text, options);
        return builder.ToString();
    }

    /// <summary>Échappe un texte à la suite d'un document en construction.</summary>
    /// <param name="builder">Document RTF en construction.</param>
    /// <param name="text">Texte brut à ajouter.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <exception cref="ArgumentNullException">Si le constructeur ou le texte est nul.</exception>
    /// <exception cref="ArgumentException">Si les réglages sont incohérents.</exception>
    /// <remarks>À préférer à <see cref="Escape"/> pour assembler un grand document sans copies intermédiaires.</remarks>
    public static void Append(StringBuilder builder, string text, RtfEscapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(text);

        RtfEscapeOptions settings = options ?? RtfEscapeOptions.Default;
        settings.Validate();

        Append(builder, text.AsSpan(), settings);
    }

    /// <summary>Échappe une portion de texte à la suite d'un document en construction.</summary>
    /// <param name="builder">Document RTF en construction.</param>
    /// <param name="text">Portion de texte brut.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <exception cref="ArgumentNullException">Si le constructeur est nul.</exception>
    /// <exception cref="ArgumentException">Si les réglages sont incohérents.</exception>
    public static void Append(StringBuilder builder, ReadOnlySpan<char> text, RtfEscapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        RtfEscapeOptions settings = options ?? RtfEscapeOptions.Default;
        settings.Validate();

        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '{':
                    builder.Append("\\{");
                    break;
                case '}':
                    builder.Append("\\}");
                    break;
                case '\t':
                    builder.Append("\\tab ");
                    break;
                case '\r':
                    builder.Append(settings.NewLine);
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    break;
                case '\n':
                    builder.Append(settings.NewLine);
                    break;
                default:
                    if (c < ' ' || c == '\u007F')
                    {
                        break;
                    }

                    if (c < 0x80)
                    {
                        builder.Append(c);
                    }
                    else
                    {
                        builder.Append("\\u")
                            .Append(((short)c).ToString(CultureInfo.InvariantCulture))
                            .Append(settings.Fallback);
                    }

                    break;
            }
        }
    }
}
