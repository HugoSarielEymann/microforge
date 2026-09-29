using System.Globalization;
using System.Text;
using Micro.Markdown.CodeSpans;

namespace Micro.Markdown.Hashtag;

/// <summary>
/// Repère les étiquettes <c>#tag</c> d'un texte Markdown, à la manière d'Obsidian, et donne
/// les ancêtres d'une étiquette imbriquée.
/// </summary>
/// <remarks>
/// <para>
/// Un <c>#</c> ouvre une étiquette s'il est en début de texte ou précédé d'un blanc, et s'il
/// est suivi d'au moins un caractère de nom : lettre de tout alphabet, chiffre, marque
/// combinante, <c>_</c>, <c>-</c> ou <c>/</c>. Le titre <c>#&#160;Titre</c> (dièse suivi d'un
/// blanc), l'ancre <c>page.html#section</c> et l'entité <c>&amp;#233;</c> n'en sont donc pas.
/// Une barre oblique finale n'appartient pas au nom.
/// </para>
/// <para>Fonctions pures et déterministes. Les positions sont des index UTF-16.</para>
/// </remarks>
public static class HashtagParser
{
    /// <summary>Tente de lire une étiquette dont le <c>#</c> est à une position donnée.</summary>
    /// <param name="text">Texte à analyser ; <see langword="null"/> ne contient aucune étiquette.</param>
    /// <param name="index">Position du <c>#</c>.</param>
    /// <param name="tag">L'étiquette lue, ou la valeur par défaut en cas d'échec.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si une étiquette valide commence à <paramref name="index"/>.</returns>
    /// <remarks>Ne lève jamais : position hors texte, réglages incohérents ou nom invalide rendent <see langword="false"/>.</remarks>
    public static bool TryParseAt(string? text, int index, out HashtagMatch tag, HashtagOptions? options = null)
    {
        tag = default;
        HashtagOptions settings = options ?? HashtagOptions.Default;

        if (text is null || !settings.IsValid || index < 0 || index >= text.Length || text[index] != '#')
        {
            return false;
        }

        if (index > 0 && !CanPrecede(text[index - 1], settings))
        {
            return false;
        }

        int nameStart = index + 1;
        int position = nameStart;

        while (position < text.Length && position - nameStart < settings.MaxLength)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(position), out Rune rune, out int consumed) != System.Buffers.OperationStatus.Done)
            {
                break;
            }

            if (!IsNameRune(rune, settings))
            {
                break;
            }

            if (position - nameStart + consumed > settings.MaxLength)
            {
                break;
            }

            position += consumed;
        }

        // Une barre oblique finale ferme l'imbrication sans nommer d'enfant.
        int nameEnd = position;
        while (nameEnd > nameStart && text[nameEnd - 1] == '/')
        {
            nameEnd--;
        }

        if (nameEnd == nameStart || text[nameStart] == '/')
        {
            return false;
        }

        // Le chiffre seul ne suffit pas : « le #12 » est un numéro, pas une étiquette.
        if (settings.RequireNonDigit && !HasNonDigit(text, nameStart, nameEnd))
        {
            return false;
        }

        tag = new HashtagMatch(index, nameEnd - index, text[nameStart..nameEnd]);
        return true;
    }

    /// <summary>Relève toutes les étiquettes d'un texte, dans l'ordre.</summary>
    /// <param name="text">Texte à analyser.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Les étiquettes relevées, triées par position.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    /// <exception cref="ArgumentNullException">Si <see cref="HashtagOptions.AllowedPrecedingCharacters"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public static IReadOnlyList<HashtagMatch> FindAll(string text, HashtagOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        HashtagOptions settings = options ?? HashtagOptions.Default;
        settings.Validate();

        IReadOnlyList<CodeSpan> code = settings.SkipCode ? CodeSpanScanner.Scan(text) : [];
        List<HashtagMatch> tags = [];

        int position = 0;
        while (position < text.Length)
        {
            int hash = text.IndexOf('#', position);
            if (hash < 0)
            {
                break;
            }

            if ((code.Count == 0 || !CodeSpanScanner.IsInside(code, hash))
                && TryParseAt(text, hash, out HashtagMatch tag, settings))
            {
                tags.Add(tag);
                position = tag.End;
                continue;
            }

            position = hash + 1;
        }

        return tags;
    }

    /// <summary>Indique qu'un nom (sans <c>#</c>) ferait une étiquette valide.</summary>
    /// <param name="name">Nom candidat.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si <c>#</c> suivi de ce nom serait relevé en entier.</returns>
    /// <remarks>Ne lève jamais : un nom nul ou des réglages incohérents rendent <see langword="false"/>.</remarks>
    public static bool IsValidName(string? name, HashtagOptions? options = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        string candidate = "#" + name;
        return TryParseAt(candidate, 0, out HashtagMatch tag, options) && tag.Length == candidate.Length;
    }

    /// <summary>Donne une étiquette imbriquée et tous ses ancêtres, du plus général au plus précis.</summary>
    /// <param name="name">Nom d'étiquette, avec ou sans <c>#</c> : <c>projet/almageste/v1</c>.</param>
    /// <returns><c>projet</c>, <c>projet/almageste</c>, <c>projet/almageste/v1</c>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="name"/> est nul.</exception>
    /// <remarks>
    /// Sert à compter une étiquette dans chacun de ses parents, comme le fait un panneau
    /// d'étiquettes arborescent. Les segments vides (<c>a//b</c>) sont ignorés.
    /// </remarks>
    public static IReadOnlyList<string> Ancestors(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string[] segments = name.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries);
        List<string> result = new(segments.Length);
        StringBuilder path = new();

        foreach (string segment in segments)
        {
            if (path.Length > 0)
            {
                path.Append('/');
            }

            path.Append(segment);
            result.Add(path.ToString());
        }

        return result;
    }

    private static bool CanPrecede(char previous, HashtagOptions options)
        => char.IsWhiteSpace(previous) || options.AllowedPrecedingCharacters.Contains(previous, StringComparison.Ordinal);

    private static bool IsNameRune(Rune rune, HashtagOptions options)
    {
        if (Rune.IsLetterOrDigit(rune))
        {
            return true;
        }

        if (rune.Value is '_' or '-')
        {
            return true;
        }

        if (rune.Value == '/')
        {
            return options.AllowNested;
        }

        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
    }

    private static bool HasNonDigit(string text, int start, int end)
    {
        int position = start;
        while (position < end)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(position, end - position), out Rune rune, out int consumed) != System.Buffers.OperationStatus.Done)
            {
                return true;
            }

            if (!Rune.IsDigit(rune))
            {
                return true;
            }

            position += consumed;
        }

        return false;
    }
}
