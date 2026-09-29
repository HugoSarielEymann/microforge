namespace Micro.Markdown.CodeSpans;

/// <summary>
/// Repère les plages de code d'un texte Markdown : blocs délimités, segments en ligne et,
/// sur demande, blocs indentés.
/// </summary>
/// <remarks>
/// <para>
/// Le but n'est pas de rendre du Markdown mais de dire à un analyseur « ici, ne cherche
/// rien » : un lien wiki, une étiquette ou une mention écrits dans un bloc de code sont
/// des exemples, pas des références. Le repérage est donc volontairement tolérant sur
/// l'indentation des clôtures — une clôture dans une liste ou une citation (<c>&gt; ```</c>)
/// est reconnue — parce que manquer un bloc coûte plus cher que d'en voir un de trop.
/// </para>
/// <para>
/// Fonction pure et déterministe. Les positions sont des index UTF-16 dans le texte reçu ;
/// les trois conventions de fin de ligne (<c>\n</c>, <c>\r\n</c>, <c>\r</c>) sont acceptées.
/// </para>
/// </remarks>
public static class CodeSpanScanner
{
    /// <summary>Relève les plages de code d'un texte.</summary>
    /// <param name="text">Texte Markdown à analyser.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Les plages relevées, triées par position et sans chevauchement.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    /// <remarks>
    /// Un bloc délimité jamais refermé court jusqu'à la fin du texte, comme dans tout rendu
    /// Markdown. Un backtick sans partenaire, lui, redevient de la prose.
    /// </remarks>
    public static IReadOnlyList<CodeSpan> Scan(string text, CodeSpanOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        CodeSpanOptions settings = options ?? CodeSpanOptions.Default;
        settings.Validate();

        List<CodeSpan> spans = [];
        List<(int Start, int End)> prose = [];

        ScanBlocks(text, settings, spans, prose);

        if (settings.IncludeInline)
        {
            foreach ((int start, int end) in prose)
            {
                ScanInline(text, start, end, settings, spans);
            }
        }

        spans.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }

    /// <summary>Indique qu'une position tombe dans l'une des plages relevées.</summary>
    /// <param name="spans">Plages issues de <see cref="Scan"/>, triées et disjointes.</param>
    /// <param name="index">Position à tester.</param>
    /// <returns><see langword="true"/> si la position appartient à une plage de code.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="spans"/> est nul.</exception>
    /// <remarks>Recherche dichotomique : à préférer à un parcours quand on teste beaucoup de positions.</remarks>
    public static bool IsInside(IReadOnlyList<CodeSpan> spans, int index)
    {
        ArgumentNullException.ThrowIfNull(spans);

        int low = 0;
        int high = spans.Count - 1;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            CodeSpan span = spans[middle];

            if (index < span.Start)
            {
                high = middle - 1;
            }
            else if (index >= (long)span.Start + span.Length)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Indique qu'une plage de texte touche l'une des plages de code.</summary>
    /// <param name="spans">Plages issues de <see cref="Scan"/>, triées et disjointes.</param>
    /// <param name="start">Début de la plage testée.</param>
    /// <param name="length">Longueur de la plage testée.</param>
    /// <returns><see langword="true"/> si les deux plages partagent au moins un caractère.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="spans"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="length"/> est négatif.</exception>
    public static bool Overlaps(IReadOnlyList<CodeSpan> spans, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        // En 64 bits : une plage testée tout contre int.MaxValue ne doit pas déborder.
        long end = (long)start + length;
        foreach (CodeSpan span in spans)
        {
            if (span.Start >= end)
            {
                break;
            }

            if ((long)span.Start + span.Length > start)
            {
                return true;
            }
        }

        return false;
    }

    // ==================== Blocs ====================

    private static void ScanBlocks(string text, CodeSpanOptions options, List<CodeSpan> spans, List<(int Start, int End)> prose)
    {
        int proseStart = 0;

        bool inFence = false;
        char fenceChar = '\0';
        int fenceLength = 0;
        int fenceStart = 0;

        bool previousBlank = true;
        bool inIndented = false;
        int indentedStart = 0;
        int indentedEnd = 0;

        int lineStart = 0;
        while (lineStart <= text.Length)
        {
            int contentEnd = FindLineEnd(text, lineStart);
            int nextStart = SkipLineBreak(text, contentEnd);
            bool last = nextStart == contentEnd;

            if (inFence)
            {
                if (IsClosingFence(text, lineStart, contentEnd, fenceChar, fenceLength))
                {
                    spans.Add(new CodeSpan(fenceStart, contentEnd - fenceStart, CodeSpanKind.Fenced));
                    inFence = false;
                    proseStart = nextStart;
                    previousBlank = true;
                }
            }
            else if (options.IncludeFenced && TryOpenFence(text, lineStart, contentEnd, out fenceChar, out fenceLength, out int markerStart))
            {
                if (inIndented)
                {
                    spans.Add(new CodeSpan(indentedStart, indentedEnd - indentedStart, CodeSpanKind.Indented));
                    inIndented = false;
                }

                AddProse(prose, proseStart, lineStart);
                inFence = true;
                fenceStart = markerStart;
            }
            else if (options.IncludeIndented)
            {
                bool blank = IsBlank(text, lineStart, contentEnd);

                if (!blank && (previousBlank || inIndented) && IsIndentedCode(text, lineStart, contentEnd))
                {
                    if (!inIndented)
                    {
                        AddProse(prose, proseStart, lineStart);
                        inIndented = true;
                        indentedStart = lineStart;
                    }

                    indentedEnd = contentEnd;
                }
                else if (inIndented && !blank)
                {
                    spans.Add(new CodeSpan(indentedStart, indentedEnd - indentedStart, CodeSpanKind.Indented));
                    inIndented = false;
                    proseStart = lineStart;
                }

                previousBlank = blank;
            }

            if (last)
            {
                break;
            }

            lineStart = nextStart;
        }

        if (inFence)
        {
            // Bloc jamais refermé : il court jusqu'à la fin, comme dans tout rendu Markdown.
            spans.Add(new CodeSpan(fenceStart, text.Length - fenceStart, CodeSpanKind.Fenced));
            return;
        }

        if (inIndented)
        {
            spans.Add(new CodeSpan(indentedStart, indentedEnd - indentedStart, CodeSpanKind.Indented));
            proseStart = Math.Max(proseStart, indentedEnd);
        }

        AddProse(prose, proseStart, text.Length);
    }

    private static void AddProse(List<(int Start, int End)> prose, int start, int end)
    {
        if (end > start)
        {
            prose.Add((start, end));
        }
    }

    /// <summary>
    /// Reconnaît une clôture ouvrante : blancs et marqueurs de citation tolérés en tête, puis
    /// trois backticks ou tildes au moins.
    /// </summary>
    private static bool TryOpenFence(string text, int start, int end, out char fenceChar, out int fenceLength, out int markerStart)
    {
        fenceChar = '\0';
        fenceLength = 0;
        markerStart = SkipContainerPrefix(text, start, end);

        if (markerStart >= end || text[markerStart] is not ('`' or '~'))
        {
            return false;
        }

        char candidate = text[markerStart];
        int count = CountRun(text, markerStart, end, candidate);
        if (count < 3)
        {
            return false;
        }

        // Une chaîne d'information qui contient un backtick signe un segment en ligne, pas une
        // clôture : « ```a``` » sur une ligne est du code en ligne.
        if (candidate == '`' && text.AsSpan(markerStart + count, end - markerStart - count).Contains('`'))
        {
            return false;
        }

        fenceChar = candidate;
        fenceLength = count;
        return true;
    }

    private static bool IsClosingFence(string text, int start, int end, char fenceChar, int fenceLength)
    {
        int position = SkipContainerPrefix(text, start, end);
        int count = CountRun(text, position, end, fenceChar);
        if (count < fenceLength)
        {
            return false;
        }

        return IsBlank(text, position + count, end);
    }

    /// <summary>Passe les blancs et les marqueurs de citation qui préfixent une ligne.</summary>
    private static int SkipContainerPrefix(string text, int start, int end)
    {
        int position = start;
        while (position < end)
        {
            char c = text[position];
            if (c is ' ' or '\t' or '>')
            {
                position++;
                continue;
            }

            break;
        }

        return position;
    }

    private static bool IsIndentedCode(string text, int start, int end)
    {
        int spaces = 0;
        for (int position = start; position < end; position++)
        {
            char c = text[position];
            if (c == '\t')
            {
                return true;
            }

            if (c != ' ')
            {
                return false;
            }

            spaces++;
            if (spaces >= 4)
            {
                return true;
            }
        }

        return false;
    }

    // ==================== Segments en ligne ====================

    private static void ScanInline(string text, int start, int end, CodeSpanOptions options, List<CodeSpan> spans)
    {
        int position = start;

        while (position < end)
        {
            int open = text.IndexOf('`', position, end - position);
            if (open < 0)
            {
                return;
            }

            if (IsEscaped(text, start, open))
            {
                position = open + 1;
                continue;
            }

            int runLength = CountRun(text, open, end, '`');
            int close = FindClosingRun(text, open + runLength, end, runLength, options.MaxInlineLines);

            if (close >= 0)
            {
                spans.Add(new CodeSpan(open, close + runLength - open, CodeSpanKind.Inline));
                position = close + runLength;
            }
            else
            {
                // Pas de partenaire : ces backticks sont littéraux.
                position = open + runLength;
            }
        }
    }

    /// <summary>
    /// Cherche une suite de backticks de même longueur exactement, sans franchir une ligne
    /// vide ni dépasser le nombre de lignes permis.
    /// </summary>
    private static int FindClosingRun(string text, int from, int end, int runLength, int maxLines)
    {
        int lines = 1;
        int position = from;

        while (position < end)
        {
            char c = text[position];

            if (c is '\n' or '\r')
            {
                int next = SkipLineBreak(text, position);
                lines++;

                if (lines > maxLines || IsBlank(text, next, FindLineEnd(text, next)))
                {
                    return -1;
                }

                position = next;
                continue;
            }

            if (c == '`')
            {
                int count = CountRun(text, position, end, '`');
                if (count == runLength)
                {
                    return position;
                }

                position += count;
                continue;
            }

            position++;
        }

        return -1;
    }

    /// <summary>Un caractère est échappé s'il est précédé d'un nombre impair d'antislashs.</summary>
    private static bool IsEscaped(string text, int floor, int index)
    {
        int backslashes = 0;
        for (int position = index - 1; position >= floor && text[position] == '\\'; position--)
        {
            backslashes++;
        }

        return backslashes % 2 == 1;
    }

    // ==================== Lignes ====================

    private static int CountRun(string text, int start, int end, char c)
    {
        int position = start;
        while (position < end && text[position] == c)
        {
            position++;
        }

        return position - start;
    }

    private static bool IsBlank(string text, int start, int end)
    {
        for (int position = start; position < end; position++)
        {
            if (text[position] is not (' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    private static int FindLineEnd(string text, int start)
    {
        int position = start;
        while (position < text.Length && text[position] is not ('\n' or '\r'))
        {
            position++;
        }

        return position;
    }

    private static int SkipLineBreak(string text, int position)
    {
        if (position >= text.Length)
        {
            return position;
        }

        if (text[position] == '\r')
        {
            return position + 1 < text.Length && text[position + 1] == '\n' ? position + 2 : position + 1;
        }

        return text[position] == '\n' ? position + 1 : position;
    }
}
