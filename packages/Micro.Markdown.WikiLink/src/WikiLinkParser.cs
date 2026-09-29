using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Micro.Markdown.CodeSpans;

namespace Micro.Markdown.WikiLink;

/// <summary>
/// Analyse, compose et réécrit les liens wiki à la manière d'Obsidian :
/// <c>[[cible]]</c>, <c>[[cible|alias]]</c>, <c>[[cible#section]]</c>, <c>[[cible#^bloc]]</c>,
/// <c>[[#section]]</c> et les intégrations <c>![[...]]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Un lien tient sur une seule ligne et se referme au premier <c>]]</c>. La cible s'arrête au
/// premier <c>#</c> ou <c>|</c> ; l'alias commence au premier <c>|</c>. Un <c>\|</c> — la façon
/// d'écrire un alias dans un tableau Markdown — sépare aussi l'alias, antislash exclu de la cible.
/// </para>
/// <para>
/// Fonctions pures et déterministes. Toutes les positions sont des index UTF-16.
/// </para>
/// </remarks>
public static class WikiLinkParser
{
    /// <summary>Caractères qu'une cible ne peut pas contenir sans casser la syntaxe du lien.</summary>
    private static readonly SearchValues<char> ForbiddenTargetCharacters = SearchValues.Create("[]|#^\r\n");

    /// <summary>Tente d'analyser un lien wiki qui commence exactement à une position.</summary>
    /// <param name="text">Texte à analyser ; <see langword="null"/> ne contient aucun lien.</param>
    /// <param name="index">Position du <c>!</c> d'une intégration ou du premier <c>[</c>.</param>
    /// <param name="link">Le lien analysé, ou <see langword="null"/> en cas d'échec.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si un lien valide commence à <paramref name="index"/>.</returns>
    /// <remarks>
    /// Ne lève jamais : une position hors du texte, des réglages incohérents, un lien vide ou
    /// jamais refermé rendent simplement <see langword="false"/>. Un <c>[[</c> rencontré avant la
    /// fermeture fait échouer l'analyse : c'est le lien intérieur qui compte, à l'appelant de
    /// réessayer à sa position.
    /// </remarks>
    public static bool TryParseAt(string? text, int index, [NotNullWhen(true)] out WikiLinkMatch? link, WikiLinkOptions? options = null)
    {
        link = null;
        WikiLinkOptions settings = options ?? WikiLinkOptions.Default;

        if (text is null || !settings.IsValid || index < 0 || index >= text.Length)
        {
            return false;
        }

        bool embed = false;
        int bracket = index;

        if (text[index] == '!')
        {
            if (!settings.AllowEmbeds)
            {
                return false;
            }

            embed = true;
            bracket = index + 1;
        }

        if (bracket + 1 >= text.Length || text[bracket] != '[' || text[bracket + 1] != '[')
        {
            return false;
        }

        int contentStart = bracket + 2;
        int close = FindClose(text, contentStart, settings.MaxLength);
        if (close < 0)
        {
            return false;
        }

        // Découpage : cible [#section] [|alias], la barre pouvant être échappée dans un tableau.
        int pipe = text.IndexOf('|', contentStart, close - contentStart);
        int beforeEnd = pipe < 0 ? close : pipe;
        if (pipe > contentStart && text[pipe - 1] == '\\')
        {
            beforeEnd = pipe - 1;
        }

        int hash = text.IndexOf('#', contentStart, beforeEnd - contentStart);
        int targetEnd = hash < 0 ? beforeEnd : hash;

        (int targetStart, int targetLength) = Trim(text, contentStart, targetEnd);

        // Un crochet dans la cible signe un crochet de trop avant le lien (« [[[Note]] ») :
        // c'est à la position suivante que le lien commence vraiment.
        if (text.AsSpan(targetStart, targetLength).IndexOfAny('[', ']') >= 0)
        {
            return false;
        }

        string target = text.Substring(targetStart, targetLength);

        string? heading = null;
        string? blockId = null;
        if (hash >= 0)
        {
            (int subStart, int subLength) = Trim(text, hash + 1, beforeEnd);
            if (subLength > 0 && text[subStart] == '^')
            {
                (int blockStart, int blockLength) = Trim(text, subStart + 1, subStart + subLength);
                blockId = blockLength > 0 ? text.Substring(blockStart, blockLength) : null;
            }
            else if (subLength > 0)
            {
                heading = text.Substring(subStart, subLength);
            }
        }

        string? alias = null;
        int aliasStart = -1;
        int aliasLength = 0;
        if (pipe >= 0)
        {
            (int start, int length) = Trim(text, pipe + 1, close);
            if (length > 0)
            {
                alias = text.Substring(start, length);
                aliasStart = start;
                aliasLength = length;
            }
        }

        if (target.Length == 0 && heading is null && blockId is null)
        {
            return false;
        }

        link = new WikiLinkMatch
        {
            Start = index,
            Length = close + 2 - index,
            Target = target,
            TargetStart = targetStart,
            TargetLength = targetLength,
            Heading = heading,
            BlockId = blockId,
            Alias = alias,
            AliasStart = aliasStart,
            AliasLength = aliasLength,
            IsEmbed = embed,
        };
        return true;
    }

    /// <summary>Relève tous les liens wiki d'un texte, dans l'ordre.</summary>
    /// <param name="text">Texte à analyser.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Les liens relevés, triés par position, sans chevauchement.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="text"/> est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    /// <remarks>
    /// Par défaut, les liens écrits dans du code sont ignorés, de même qu'un <c>[[</c> échappé
    /// par un antislash. Un <c>!</c> échappé laisse un renvoi simple, pas une intégration.
    /// </remarks>
    public static IReadOnlyList<WikiLinkMatch> FindAll(string text, WikiLinkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        WikiLinkOptions settings = options ?? WikiLinkOptions.Default;
        settings.Validate();

        IReadOnlyList<CodeSpan> code = settings.SkipCode ? CodeSpanScanner.Scan(text) : [];
        List<WikiLinkMatch> links = [];

        int position = 0;
        while (position < text.Length)
        {
            int open = text.IndexOf("[[", position, StringComparison.Ordinal);
            if (open < 0)
            {
                break;
            }

            if (IsEscaped(text, open))
            {
                position = open + 1;
                continue;
            }

            int start = open;
            if (settings.AllowEmbeds && open > 0 && text[open - 1] == '!' && !IsEscaped(text, open - 1))
            {
                start = open - 1;
            }

            if (code.Count > 0 && CodeSpanScanner.IsInside(code, open))
            {
                position = open + 2;
                continue;
            }

            if (TryParseAt(text, start, out WikiLinkMatch? link, settings)
                && (code.Count == 0 || !CodeSpanScanner.Overlaps(code, link.Start, link.Length)))
            {
                links.Add(link);
                position = link.End;
                continue;
            }

            // Un seul crochet de plus : « [[[a]] » retente à partir du second crochet.
            position = open + 1;
        }

        return links;
    }

    /// <summary>Réécrit la cible des liens wiki d'un texte, sans toucher au reste.</summary>
    /// <param name="text">Texte à réécrire.</param>
    /// <param name="retarget">
    /// Reçoit chaque lien relevé et rend sa nouvelle cible, ou <see langword="null"/> pour le
    /// laisser tel quel. La section, le bloc, l'alias et le <c>!</c> sont conservés.
    /// </param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Le texte réécrit ; l'instance reçue si aucun lien n'a changé.</returns>
    /// <exception cref="ArgumentNullException">Si le texte ou la fonction est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    /// <exception cref="ArgumentException">
    /// Si <paramref name="retarget"/> rend une cible qui casserait la syntaxe du lien
    /// (voir <see cref="IsValidTarget"/>).
    /// </exception>
    /// <remarks>
    /// C'est l'opération qui accompagne le renommage d'une note : chaque renvoi vers l'ancien
    /// titre doit pointer vers le nouveau, alias et sections compris, et rien d'autre ne doit
    /// bouger — pas même un blanc.
    /// </remarks>
    public static string Rewrite(string text, Func<WikiLinkMatch, string?> retarget, WikiLinkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(retarget);

        IReadOnlyList<WikiLinkMatch> links = FindAll(text, options);
        if (links.Count == 0)
        {
            return text;
        }

        StringBuilder builder = new(text.Length + 16);
        int last = 0;
        bool changed = false;

        foreach (WikiLinkMatch link in links)
        {
            string? replacement = retarget(link);
            if (replacement is null || string.Equals(replacement, link.Target, StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsValidTarget(replacement))
            {
                throw new ArgumentException(
                    "La cible « " + replacement + " » casserait la syntaxe du lien : crochets, barre, dièse, accent circonflexe et sauts de ligne y sont interdits.",
                    nameof(retarget));
            }

            builder.Append(text, last, link.TargetStart - last).Append(replacement);
            last = link.TargetStart + link.TargetLength;
            changed = true;
        }

        if (!changed)
        {
            return text;
        }

        builder.Append(text, last, text.Length - last);
        return builder.ToString();
    }

    /// <summary>Compose le texte d'un lien wiki.</summary>
    /// <param name="target">Cible : titre, chemin ou fichier ; vide pour une section de la note courante.</param>
    /// <param name="heading">Section visée, ou <see langword="null"/>.</param>
    /// <param name="blockId">Bloc visé (sans <c>^</c>), ou <see langword="null"/> ; ignoré si une section est donnée.</param>
    /// <param name="alias">Texte affiché, ou <see langword="null"/>.</param>
    /// <param name="embed">Composer une intégration <c>![[...]]</c>.</param>
    /// <returns>Le lien, que <see cref="TryParseAt"/> relit à l'identique.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="target"/> est nul.</exception>
    /// <exception cref="ArgumentException">
    /// Si la cible est invalide, si le lien serait vide, ou si une section ou un alias
    /// contient <c>]]</c> ou un saut de ligne.
    /// </exception>
    public static string Format(string target, string? heading = null, string? blockId = null, string? alias = null, bool embed = false)
    {
        ArgumentNullException.ThrowIfNull(target);

        string cible = target.Trim();
        if (cible.Length > 0 && !IsValidTarget(cible))
        {
            throw new ArgumentException("Cible invalide pour un lien wiki : « " + target + " ».", nameof(target));
        }

        string? section = Clean(heading, nameof(heading), forbidPipe: true);
        string? bloc = Clean(blockId, nameof(blockId), forbidPipe: true);
        string? affiche = Clean(alias, nameof(alias), forbidPipe: false);

        if (cible.Length == 0 && section is null && bloc is null)
        {
            throw new ArgumentException("Un lien wiki doit viser une note, une section ou un bloc.", nameof(target));
        }

        StringBuilder builder = new();
        if (embed)
        {
            builder.Append('!');
        }

        builder.Append("[[").Append(cible);

        if (section is not null)
        {
            builder.Append('#').Append(section);
        }
        else if (bloc is not null)
        {
            builder.Append("#^").Append(bloc.TrimStart('^'));
        }

        if (affiche is not null)
        {
            builder.Append('|').Append(affiche);
        }

        return builder.Append("]]").ToString();
    }

    /// <summary>Indique qu'un texte peut servir de cible à un lien wiki sans en casser la syntaxe.</summary>
    /// <param name="target">Cible candidate.</param>
    /// <returns>
    /// <see langword="true"/> si la cible est non vide, sans blanc de bord, et ne contient ni
    /// crochet, ni barre, ni dièse, ni accent circonflexe, ni saut de ligne.
    /// </returns>
    /// <remarks>Ne lève jamais : <see langword="null"/> rend <see langword="false"/>.</remarks>
    public static bool IsValidTarget(string? target)
        => !string.IsNullOrWhiteSpace(target)
            && target.Length == target.Trim().Length
            && target.AsSpan().IndexOfAny(ForbiddenTargetCharacters) < 0;

    private static string? Clean(string? value, string name, bool forbidPipe)
    {
        if (value is null)
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Contains("]]", StringComparison.Ordinal)
            || trimmed.Contains('\n', StringComparison.Ordinal)
            || trimmed.Contains('\r', StringComparison.Ordinal)
            || (forbidPipe && trimmed.Contains('|', StringComparison.Ordinal)))
        {
            throw new ArgumentException("« " + value + " » casserait la syntaxe du lien.", name);
        }

        return trimmed;
    }

    /// <summary>Cherche le <c>]]</c> fermant sur la même ligne, en deçà de la longueur permise.</summary>
    private static int FindClose(string text, int contentStart, int maxLength)
    {
        long limit = Math.Min((long)contentStart + maxLength, text.Length - 1);

        for (int position = contentStart; position < limit + 1 && position < text.Length - 1; position++)
        {
            char c = text[position];

            if (c is '\n' or '\r')
            {
                return -1;
            }

            if (c == '[' && text[position + 1] == '[')
            {
                return -1;
            }

            if (c == ']' && text[position + 1] == ']')
            {
                return position;
            }
        }

        return -1;
    }

    private static (int Start, int Length) Trim(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return (start, end - start);
    }

    private static bool IsEscaped(string text, int index)
    {
        int backslashes = 0;
        for (int position = index - 1; position >= 0 && text[position] == '\\'; position--)
        {
            backslashes++;
        }

        return backslashes % 2 == 1;
    }
}
