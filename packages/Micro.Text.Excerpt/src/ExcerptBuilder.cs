using System.Globalization;
using System.Text;

namespace Micro.Text.Excerpt;

/// <summary>Un extrait prêt à afficher, avec la position du passage à surligner.</summary>
/// <param name="Text">Texte de l'extrait, points de suspension compris.</param>
/// <param name="HighlightStart">Position du passage dans <paramref name="Text"/>.</param>
/// <param name="HighlightLength">Longueur du passage dans <paramref name="Text"/>, après réduction des blancs.</param>
/// <param name="StartsTruncated">Du texte précède l'extrait et a été coupé.</param>
/// <param name="EndsTruncated">Du texte suit l'extrait et a été coupé.</param>
public readonly record struct TextExcerpt(string Text, int HighlightStart, int HighlightLength, bool StartsTruncated, bool EndsTruncated)
{
    /// <summary>Le passage tel qu'il figure dans l'extrait.</summary>
    public string Highlight => Text.Substring(HighlightStart, HighlightLength);
}

/// <summary>
/// Découpe un extrait de texte autour d'un passage : ce qu'affiche une liste de résultats de
/// recherche ou de mentions.
/// </summary>
/// <remarks>
/// Le contexte est borné de part et d'autre, recule jusqu'à une frontière de mot, reste par
/// défaut sur la ligne du passage, et ses blancs sont réduits. Les coupes sont signalées par
/// des points de suspension, et la position du passage est recalculée dans l'extrait final :
/// l'appelant surligne sans rien recompter. Fonction pure et déterministe.
/// </remarks>
public static class ExcerptBuilder
{
    /// <summary>Découpe l'extrait qui entoure un passage.</summary>
    /// <param name="text">Texte complet.</param>
    /// <param name="start">Position du passage, en index UTF-16.</param>
    /// <param name="length">Longueur du passage ; 0 pour un simple point de repère.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>L'extrait et la position du passage dans celui-ci.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> ou <see cref="ExcerptOptions.Ellipsis"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si le passage sort du texte, si sa longueur est négative, ou si un contexte est négatif.
    /// </exception>
    /// <remarks>Le passage lui-même n'est jamais raccourci, quelle que soit sa longueur.</remarks>
    public static TextExcerpt Around(string text, int start, int length, ExcerptOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, text.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((long)start + length, text.Length, nameof(length));

        ExcerptOptions settings = options ?? ExcerptOptions.Default;
        settings.Validate();

        int end = start + length;

        int scopeStart = 0;
        int scopeEnd = text.Length;
        if (settings.SingleLine)
        {
            scopeStart = LineStart(text, start);
            scopeEnd = LineEnd(text, end);
        }

        int left = (int)Math.Max(scopeStart, (long)start - settings.ContextBefore);
        int right = (int)Math.Min(scopeEnd, (long)end + settings.ContextAfter);

        if (settings.SnapToWords)
        {
            left = SnapLeft(text, left, scopeStart, start);
            right = SnapRight(text, right, scopeEnd, end);
        }

        // Jamais une demi-paire de substitution en bordure.
        if (left < start && char.IsLowSurrogate(text[left]))
        {
            left++;
        }

        if (right > end && char.IsHighSurrogate(text[right - 1]))
        {
            right--;
        }

        bool startsTruncated = HasContent(text, scopeStart, left);
        bool endsTruncated = HasContent(text, right, scopeEnd);

        StringBuilder builder = new();
        if (startsTruncated)
        {
            builder.Append(settings.Ellipsis);
        }

        int bodyStart = builder.Length;
        int highlightStart = -1;
        int highlightEnd = -1;

        for (int index = left; index < right; index++)
        {
            if (index == start)
            {
                highlightStart = builder.Length;
            }

            char c = text[index];
            if (settings.CollapseWhitespace && char.IsWhiteSpace(c))
            {
                // Les blancs de tête de l'extrait disparaissent, sauf s'ils appartiennent au passage.
                bool inHighlight = index >= start && index < end;
                if ((builder.Length > bodyStart || inHighlight) && (builder.Length == 0 || builder[^1] != ' '))
                {
                    builder.Append(' ');
                }
            }
            else
            {
                builder.Append(c);
            }

            if (index + 1 == end)
            {
                highlightEnd = builder.Length;
            }
        }

        if (highlightStart < 0)
        {
            highlightStart = builder.Length;
        }

        if (highlightEnd < highlightStart)
        {
            highlightEnd = highlightStart;
        }

        // Blancs de queue : retirés s'ils sont hors du passage.
        while (settings.CollapseWhitespace && builder.Length > Math.Max(bodyStart, highlightEnd) && builder[^1] == ' ')
        {
            builder.Length--;
        }

        if (endsTruncated)
        {
            builder.Append(settings.Ellipsis);
        }

        return new TextExcerpt(builder.ToString(), highlightStart, highlightEnd - highlightStart, startsTruncated, endsTruncated);
    }

    /// <summary>Recule le bord gauche jusqu'à une frontière de mot, sans entamer le passage.</summary>
    private static int SnapLeft(string text, int left, int scopeStart, int start)
    {
        if (left <= scopeStart || !IsWordChar(text, left - 1) || !IsWordChar(text, left))
        {
            return left;
        }

        int position = left;
        while (position < start && IsWordChar(text, position))
        {
            position++;
        }

        return position;
    }

    /// <summary>Avance le bord droit jusqu'à une frontière de mot, sans entamer le passage.</summary>
    private static int SnapRight(string text, int right, int scopeEnd, int end)
    {
        if (right >= scopeEnd || right == 0 || !IsWordChar(text, right - 1) || !IsWordChar(text, right))
        {
            return right;
        }

        int position = right;
        while (position > end && IsWordChar(text, position - 1))
        {
            position--;
        }

        return position;
    }

    private static bool IsWordChar(string text, int index)
    {
        char c = text[index];
        if (char.IsLetterOrDigit(c) || c is '_' or '\'' or '’' || char.IsSurrogate(c))
        {
            return true;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
    }

    private static bool HasContent(string text, int from, int to)
    {
        for (int index = from; index < to; index++)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static int LineStart(string text, int index)
    {
        int position = index;
        while (position > 0 && text[position - 1] is not ('\n' or '\r'))
        {
            position--;
        }

        return position;
    }

    private static int LineEnd(string text, int index)
    {
        int position = index;
        while (position < text.Length && text[position] is not ('\n' or '\r'))
        {
            position++;
        }

        return position;
    }
}
