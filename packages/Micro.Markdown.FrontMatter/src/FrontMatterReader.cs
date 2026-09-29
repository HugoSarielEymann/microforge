using System.Text;

namespace Micro.Markdown.FrontMatter;

/// <summary>
/// Sépare l'en-tête YAML d'un document Markdown (le « front matter » entre deux lignes
/// <c>---</c>) et lit ses propriétés de premier niveau.
/// </summary>
/// <remarks>
/// <para>
/// Ce n'est pas un analyseur YAML complet, et c'est voulu : les en-têtes de notes sont écrits
/// à la main, avec une poignée de formes — valeur simple, liste en ligne <c>[a, b]</c>, liste
/// à tirets, texte multiligne <c>|</c> ou <c>&gt;</c>. Ce lecteur les couvre, retire les
/// guillemets et les commentaires, et rend toute structure plus riche telle qu'écrite
/// (<see cref="FrontMatterValueKind.Complex"/>) plutôt que d'échouer : une note mal formée doit
/// rester lisible.
/// </para>
/// <para>
/// L'en-tête doit ouvrir le document (une marque d'ordre des octets est tolérée) et être
/// refermé ; sinon, le <c>---</c> initial est un simple filet et le document n'a pas d'en-tête.
/// Fonctions pures et déterministes.
/// </para>
/// </remarks>
public static class FrontMatterReader
{
    /// <summary>Lit l'en-tête d'un document.</summary>
    /// <param name="text">Document complet.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>L'en-tête lu, ou <see cref="FrontMatterBlock.None"/> si le document n'en a pas.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public static FrontMatterBlock Read(string text, FrontMatterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        FrontMatterOptions settings = options ?? FrontMatterOptions.Default;
        settings.Validate();

        if (!Locate(text, settings, out int contentStart, out int contentLength, out int bodyStart))
        {
            return FrontMatterBlock.None;
        }

        string content = text.Substring(contentStart, contentLength);
        IReadOnlyList<FrontMatterProperty> properties = ParseProperties(content, firstLine: 1);

        return new FrontMatterBlock(
            exists: true,
            length: bodyStart,
            contentStart: contentStart,
            content: content,
            properties: properties,
            keyComparison: settings.IgnoreKeyCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>Situe l'en-tête sans lire ses propriétés.</summary>
    /// <param name="text">Document complet ; <see langword="null"/> n'a pas d'en-tête.</param>
    /// <param name="contentStart">Position du texte YAML.</param>
    /// <param name="contentLength">Longueur du texte YAML.</param>
    /// <param name="bodyStart">Position où commence le corps du document.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si le document commence par un en-tête refermé.</returns>
    /// <remarks>
    /// Ne lève jamais. Sert à l'appelant qui n'a besoin que des positions — pour sauter l'en-tête
    /// avant de chercher des liens, ou pour le colorer — sans payer la lecture des propriétés.
    /// </remarks>
    public static bool TryLocate(string? text, out int contentStart, out int contentLength, out int bodyStart, FrontMatterOptions? options = null)
    {
        contentStart = 0;
        contentLength = 0;
        bodyStart = 0;

        FrontMatterOptions settings = options ?? FrontMatterOptions.Default;
        return text is not null && settings.IsValid && Locate(text, settings, out contentStart, out contentLength, out bodyStart);
    }

    // ==================== Repérage ====================

    private static bool Locate(string text, FrontMatterOptions options, out int contentStart, out int contentLength, out int bodyStart)
    {
        contentStart = 0;
        contentLength = 0;
        bodyStart = 0;

        int position = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        int openingEnd = FindLineEnd(text, position);

        if (!IsFence(text, position, openingEnd, "---") || openingEnd == text.Length)
        {
            return false;
        }

        int start = SkipLineBreak(text, openingEnd);
        int lineStart = start;

        while (lineStart <= text.Length && lineStart - start <= options.MaxLength)
        {
            int lineEnd = FindLineEnd(text, lineStart);

            if (IsFence(text, lineStart, lineEnd, "---") || (options.AllowDotsClosing && IsFence(text, lineStart, lineEnd, "...")))
            {
                contentStart = start;
                contentLength = Math.Max(0, lineStart - start - PrecedingBreakLength(text, lineStart, start));
                bodyStart = SkipLineBreak(text, lineEnd);
                return true;
            }

            if (lineEnd >= text.Length)
            {
                break;
            }

            lineStart = SkipLineBreak(text, lineEnd);
        }

        return false;
    }

    private static bool IsFence(string text, int start, int end, string fence)
    {
        if (end - start < fence.Length || string.CompareOrdinal(text, start, fence, 0, fence.Length) != 0)
        {
            return false;
        }

        for (int position = start + fence.Length; position < end; position++)
        {
            if (text[position] is not (' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    private static int PrecedingBreakLength(string text, int lineStart, int floor)
    {
        if (lineStart <= floor)
        {
            return 0;
        }

        if (text[lineStart - 1] == '\n')
        {
            return lineStart - 2 >= floor && text[lineStart - 2] == '\r' ? 2 : 1;
        }

        return text[lineStart - 1] == '\r' ? 1 : 0;
    }

    // ==================== Propriétés ====================

    private static List<FrontMatterProperty> ParseProperties(string content, int firstLine)
    {
        List<string> lines = SplitLines(content);
        List<FrontMatterProperty> properties = [];

        int index = 0;
        while (index < lines.Count)
        {
            string line = lines[index];

            if (IsBlankOrComment(line) || char.IsWhiteSpace(line[0]) || line[0] == '-'
                || !TryParseKey(line, out string key, out string rest))
            {
                index++;
                continue;
            }

            int lineNumber = firstLine + index;
            string value = StripComment(rest).Trim();
            int next = index + 1;

            if (value.Length == 0)
            {
                next = CollectBlock(lines, index + 1, allowDashes: true);
                properties.Add(ReadBlock(key, lines, index + 1, next, lineNumber));
            }
            else if (IsBlockScalarIndicator(value))
            {
                next = CollectBlock(lines, index + 1, allowDashes: false);
                string texte = ReadBlockScalar(lines, index + 1, next, folded: value[0] == '>');
                properties.Add(new FrontMatterProperty(key, [texte], FrontMatterValueKind.Text, Join(lines, index + 1, next), lineNumber));
            }
            else if (value[0] == '[')
            {
                StringBuilder flow = new(value);
                while (!IsClosed(flow.ToString()) && next < lines.Count && lines[next].Length > 0 && char.IsWhiteSpace(lines[next][0]))
                {
                    flow.Append(' ').Append(lines[next].Trim());
                    next++;
                }

                string raw = flow.ToString();
                properties.Add(IsClosed(raw) && raw.EndsWith(']')
                    ? new FrontMatterProperty(key, SplitFlow(raw[1..^1]), FrontMatterValueKind.List, raw, lineNumber)
                    : new FrontMatterProperty(key, [], FrontMatterValueKind.Complex, raw, lineNumber));
            }
            else if (value[0] == '{')
            {
                properties.Add(new FrontMatterProperty(key, [], FrontMatterValueKind.Complex, value, lineNumber));
            }
            else if (value[0] is '"' or '\'' && TryUnquote(value, out string unquoted))
            {
                properties.Add(new FrontMatterProperty(key, [unquoted], FrontMatterValueKind.Scalar, value, lineNumber));
            }
            else
            {
                // Scalaire nu, éventuellement poursuivi sur des lignes indentées.
                StringBuilder scalar = new(value);
                while (next < lines.Count && !IsBlankOrComment(lines[next]) && char.IsWhiteSpace(lines[next][0])
                       && !lines[next].TrimStart().StartsWith('-'))
                {
                    scalar.Append(' ').Append(StripComment(lines[next]).Trim());
                    next++;
                }

                string texte = scalar.ToString();
                properties.Add(new FrontMatterProperty(key, [texte], FrontMatterValueKind.Scalar, texte, lineNumber));
            }

            index = next;
        }

        return properties;
    }

    /// <summary>Rassemble les lignes qui dépendent de la clé : indentées, vides, ou à tirets.</summary>
    private static int CollectBlock(List<string> lines, int from, bool allowDashes)
    {
        int end = from;
        int lastMeaningful = from;

        while (end < lines.Count)
        {
            string line = lines[end];

            if (line.Trim().Length == 0)
            {
                end++;
                continue;
            }

            bool indented = char.IsWhiteSpace(line[0]);
            bool dash = allowDashes && line[0] == '-';
            if (!indented && !dash)
            {
                break;
            }

            end++;
            lastMeaningful = end;
        }

        return lastMeaningful;
    }

    private static FrontMatterProperty ReadBlock(string key, List<string> lines, int from, int to, int lineNumber)
    {
        string raw = Join(lines, from, to);

        List<string> meaningful = [];
        for (int index = from; index < to; index++)
        {
            if (!IsBlankOrComment(lines[index]))
            {
                meaningful.Add(lines[index].Trim());
            }
        }

        if (meaningful.Count == 0)
        {
            return new FrontMatterProperty(key, [], FrontMatterValueKind.Empty, raw, lineNumber);
        }

        if (!meaningful.TrueForAll(static l => l == "-" || l.StartsWith("- ", StringComparison.Ordinal)))
        {
            return new FrontMatterProperty(key, [], FrontMatterValueKind.Complex, raw, lineNumber);
        }

        List<string> items = [];
        foreach (string entry in meaningful)
        {
            string item = StripComment(entry[1..]).Trim();

            // Une entrée « - clé: valeur » fait une liste d'objets : hors de portée de ce lecteur.
            if (LooksLikeMapping(item))
            {
                return new FrontMatterProperty(key, [], FrontMatterValueKind.Complex, raw, lineNumber);
            }

            if (item.Length > 0)
            {
                items.Add(TryUnquote(item, out string unquoted) ? unquoted : item);
            }
        }

        return new FrontMatterProperty(key, items, FrontMatterValueKind.List, raw, lineNumber);
    }

    private static string ReadBlockScalar(List<string> lines, int from, int to, bool folded)
    {
        int indent = int.MaxValue;
        for (int index = from; index < to; index++)
        {
            string line = lines[index];
            if (line.Trim().Length > 0)
            {
                indent = Math.Min(indent, line.Length - line.TrimStart().Length);
            }
        }

        if (indent == int.MaxValue)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        bool previousBlank = false;

        for (int index = from; index < to; index++)
        {
            string line = lines[index];
            string body = line.Trim().Length == 0 ? string.Empty : line[Math.Min(indent, line.Length)..].TrimEnd();

            if (builder.Length > 0)
            {
                if (!folded || body.Length == 0 || previousBlank)
                {
                    builder.Append('\n');
                }
                else
                {
                    builder.Append(' ');
                }
            }

            builder.Append(body);
            previousBlank = body.Length == 0;
        }

        return builder.ToString().Trim('\n');
    }

    // ==================== Morceaux de syntaxe ====================

    private static bool TryParseKey(string line, out string key, out string rest)
    {
        key = string.Empty;
        rest = string.Empty;

        int colon;
        if (line[0] is '"' or '\'')
        {
            int close = line.IndexOf(line[0], 1);
            if (close < 0)
            {
                return false;
            }

            key = line[1..close];
            colon = close + 1;
            while (colon < line.Length && line[colon] is ' ' or '\t')
            {
                colon++;
            }

            if (colon >= line.Length || line[colon] != ':')
            {
                return false;
            }
        }
        else
        {
            colon = -1;
            for (int position = 0; position < line.Length; position++)
            {
                if (line[position] == ':' && (position + 1 == line.Length || line[position + 1] is ' ' or '\t'))
                {
                    colon = position;
                    break;
                }
            }

            if (colon <= 0)
            {
                return false;
            }

            key = line[..colon].TrimEnd();
        }

        if (key.Length == 0)
        {
            return false;
        }

        rest = line[(colon + 1)..];
        return true;
    }

    private static bool IsBlockScalarIndicator(string value)
    {
        if (value[0] is not ('|' or '>'))
        {
            return false;
        }

        for (int position = 1; position < value.Length; position++)
        {
            if (value[position] is not ('-' or '+') && !char.IsAsciiDigit(value[position]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeMapping(string item)
    {
        if (item.Length == 0 || item[0] is '"' or '\'' or '[' or '{')
        {
            return false;
        }

        int colon = item.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && (colon + 1 == item.Length || item[colon + 1] is ' ' or '\t');
    }

    /// <summary>Retire un commentaire de fin de ligne : un <c>#</c> hors guillemets, précédé d'un blanc.</summary>
    private static string StripComment(string value)
    {
        char quote = '\0';

        for (int position = 0; position < value.Length; position++)
        {
            char c = value[position];

            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'' && (position == 0 || char.IsWhiteSpace(value[position - 1]) || value[position - 1] is '[' or ','))
            {
                quote = c;
                continue;
            }

            if (c == '#' && (position == 0 || char.IsWhiteSpace(value[position - 1])))
            {
                return value[..position];
            }
        }

        return value;
    }

    private static bool TryUnquote(string value, out string result)
    {
        result = value;
        if (value.Length < 2)
        {
            return false;
        }

        char quote = value[0];
        if (quote is not ('"' or '\'') || value[^1] != quote)
        {
            return false;
        }

        string inner = value[1..^1];
        if (quote == '\'')
        {
            result = inner.Replace("''", "'", StringComparison.Ordinal);
            return true;
        }

        StringBuilder builder = new(inner.Length);
        for (int position = 0; position < inner.Length; position++)
        {
            char c = inner[position];
            if (c == '\\' && position + 1 < inner.Length)
            {
                position++;
                builder.Append(inner[position] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '0' => '\0',
                    _ => inner[position],
                });
                continue;
            }

            builder.Append(c);
        }

        result = builder.ToString();
        return true;
    }

    private static bool IsClosed(string flow)
    {
        int depth = 0;
        char quote = '\0';

        foreach (char c in flow)
        {
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '"' or '\'':
                    quote = c;
                    break;
                case '[' or '{':
                    depth++;
                    break;
                case ']' or '}':
                    depth--;
                    break;
            }
        }

        return depth <= 0 && quote == '\0';
    }

    private static List<string> SplitFlow(string inner)
    {
        List<string> items = [];
        StringBuilder current = new();
        int depth = 0;
        char quote = '\0';

        foreach (char c in inner)
        {
            if (quote != '\0')
            {
                current.Append(c);
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'' && current.ToString().Trim().Length == 0)
            {
                quote = c;
                current.Append(c);
                continue;
            }

            if (c is '[' or '{')
            {
                depth++;
            }
            else if (c is ']' or '}')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                Flush();
                continue;
            }

            current.Append(c);
        }

        Flush();
        return items;

        void Flush()
        {
            string item = current.ToString().Trim();
            current.Clear();

            if (item.Length > 0)
            {
                items.Add(TryUnquote(item, out string unquoted) ? unquoted : item);
            }
        }
    }

    // ==================== Lignes ====================

    private static bool IsBlankOrComment(string line)
    {
        string trimmed = line.TrimStart();
        return trimmed.Length == 0 || trimmed[0] == '#';
    }

    private static string Join(List<string> lines, int from, int to)
        => from >= to ? string.Empty : string.Join('\n', lines.GetRange(from, to - from));

    private static List<string> SplitLines(string content)
    {
        List<string> lines = [];
        if (content.Length == 0)
        {
            return lines;
        }

        int start = 0;
        while (start <= content.Length)
        {
            int end = FindLineEnd(content, start);
            lines.Add(content[start..end]);

            if (end >= content.Length)
            {
                break;
            }

            start = SkipLineBreak(content, end);
        }

        return lines;
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
