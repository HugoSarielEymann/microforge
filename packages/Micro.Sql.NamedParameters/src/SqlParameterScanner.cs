using System.Globalization;

namespace Micro.Sql.NamedParameters;

/// <summary>Une occurrence de paramètre nommé dans une requête.</summary>
/// <param name="Name">Nom du paramètre, sans son préfixe.</param>
/// <param name="Prefix">Préfixe rencontré (<c>@</c>, <c>:</c> ou <c>$</c>).</param>
/// <param name="Start">Position du préfixe dans la requête.</param>
/// <param name="Length">Longueur de l'occurrence, préfixe compris.</param>
public readonly record struct SqlParameterToken(string Name, char Prefix, int Start, int Length);

/// <summary>
/// Relève les paramètres nommés d'une requête SQL sans l'exécuter : ce qui suit un préfixe
/// (<c>@nom</c> par défaut), hors des chaînes, des identifiants délimités et des commentaires.
/// </summary>
/// <remarks>
/// Ce n'est pas un analyseur SQL : il ne valide rien et ne comprend pas la requête. Il sait
/// seulement où le texte cesse d'être du code (chaînes <c>'…'</c> et <c>E'…'</c>, identifiants
/// <c>"…"</c>, <c>[…]</c>, <c>`…`</c>, commentaires <c>--</c> et <c>/* */</c>, chaînes dollar de
/// PostgreSQL) et reconnaît les faux amis (<c>@@IDENTITY</c>, <c>a::int</c>, <c>nom@domaine</c>).
/// Une requête mal fermée ne lève jamais : ce qui suit l'ouverture non refermée est ignoré.
/// </remarks>
public static class SqlParameterScanner
{
    /// <summary>Toutes les occurrences de paramètres, dans l'ordre du texte.</summary>
    /// <param name="sql">Requête ; <see langword="null"/> ou vide rend une liste vide.</param>
    /// <param name="options">Paramétrage ; <see langword="null"/> applique les défauts.</param>
    /// <returns>Les occurrences, doublons compris.</returns>
    /// <exception cref="ArgumentException">Si <paramref name="options"/> est incohérent (voir <see cref="SqlParameterScanOptions.Validate"/>).</exception>
    public static IReadOnlyList<SqlParameterToken> Scan(string? sql, SqlParameterScanOptions? options = null)
    {
        SqlParameterScanOptions effective = options ?? new SqlParameterScanOptions();
        effective.Validate();

        if (string.IsNullOrEmpty(sql))
        {
            return [];
        }

        List<SqlParameterToken> tokens = [];
        int index = 0;
        int length = sql.Length;

        while (index < length)
        {
            char current = sql[index];
            char next = index + 1 < length ? sql[index + 1] : '\0';

            if (current == '\'')
            {
                bool backslashEscapes = index > 0 && sql[index - 1] is 'E' or 'e' && (index == 1 || !IsIdentifierPart(sql[index - 2]));
                index = SkipQuoted(sql, index, '\'', backslashEscapes);
            }
            else if (current is '"' or '`')
            {
                index = SkipQuoted(sql, index, current, backslashEscapes: false);
            }
            else if (current == '[')
            {
                index = SkipQuoted(sql, index, ']', backslashEscapes: false);
            }
            else if (current == '-' && next == '-')
            {
                int end = sql.IndexOf('\n', index + 2);
                index = end < 0 ? length : end + 1;
            }
            else if (current == '/' && next == '*')
            {
                index = SkipBlockComment(sql, index, effective.NestedBlockComments);
            }
            else if (current == '$' && effective.DollarQuotedStrings && TryReadDollarTag(sql, index, out string tag))
            {
                int end = sql.IndexOf(tag, index + tag.Length, StringComparison.Ordinal);
                index = end < 0 ? length : end + tag.Length;
            }
            else if (effective.Prefixes.Contains(current, StringComparison.Ordinal))
            {
                index = ReadParameter(sql, index, tokens);
            }
            else
            {
                index++;
            }
        }

        return tokens;
    }

    /// <summary>Noms des paramètres, dans l'ordre de première apparition, sans doublon.</summary>
    /// <param name="sql">Requête ; <see langword="null"/> ou vide rend une liste vide.</param>
    /// <param name="options">Paramétrage ; <see langword="null"/> applique les défauts.</param>
    /// <returns>Les noms, sans préfixe.</returns>
    /// <exception cref="ArgumentException">Si <paramref name="options"/> est incohérent.</exception>
    public static IReadOnlyList<string> Names(string? sql, SqlParameterScanOptions? options = null)
    {
        SqlParameterScanOptions effective = options ?? new SqlParameterScanOptions();
        IReadOnlyList<SqlParameterToken> tokens = Scan(sql, effective);

        HashSet<string> seen = new(effective.NameComparer);
        List<string> names = [];
        foreach (SqlParameterToken token in tokens)
        {
            if (seen.Add(token.Name))
            {
                names.Add(token.Name);
            }
        }

        return names;
    }

    private static int ReadParameter(string sql, int start, List<SqlParameterToken> tokens)
    {
        char prefix = sql[start];
        int length = sql.Length;
        char next = start + 1 < length ? sql[start + 1] : '\0';

        // « a::int » (conversion PostgreSQL) et « @@ROWCOUNT » (variable système) : faux amis.
        if (next == prefix)
        {
            int after = start + 2;
            while (after < length && IsIdentifierPart(sql[after]))
            {
                after++;
            }

            return after;
        }

        // « nom@domaine » : un préfixe collé à un identifiant n'ouvre rien.
        if (start > 0 && IsIdentifierPart(sql[start - 1]))
        {
            return start + 1;
        }

        if (start + 1 >= length || !IsIdentifierStart(sql[start + 1]))
        {
            return start + 1;
        }

        int end = start + 2;
        while (end < length && IsIdentifierPart(sql[end]))
        {
            end++;
        }

        tokens.Add(new SqlParameterToken(sql[(start + 1)..end], prefix, start, end - start));
        return end;
    }

    private static int SkipQuoted(string sql, int start, char close, bool backslashEscapes)
    {
        int index = start + 1;
        while (index < sql.Length)
        {
            char current = sql[index];
            if (backslashEscapes && current == '\\')
            {
                index += 2;
                continue;
            }

            if (current == close)
            {
                // Le délimiteur doublé est un délimiteur littéral : 'l''avion', "a""b", [a]]b].
                if (index + 1 < sql.Length && sql[index + 1] == close)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return sql.Length;
    }

    private static int SkipBlockComment(string sql, int start, bool nested)
    {
        int depth = 1;
        int index = start + 2;
        while (index < sql.Length)
        {
            if (sql[index] == '*' && index + 1 < sql.Length && sql[index + 1] == '/')
            {
                depth--;
                index += 2;
                if (depth == 0 || !nested)
                {
                    return index;
                }

                continue;
            }

            if (nested && sql[index] == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                depth++;
                index += 2;
                continue;
            }

            index++;
        }

        return sql.Length;
    }

    /// <summary>Reconnaît une étiquette de chaîne dollar : <c>$$</c> ou <c>$nom$</c> (pas <c>$1</c>).</summary>
    private static bool TryReadDollarTag(string sql, int start, out string tag)
    {
        tag = string.Empty;
        int index = start + 1;
        if (index < sql.Length && char.IsDigit(sql[index]))
        {
            return false;
        }

        while (index < sql.Length && IsIdentifierPart(sql[index]))
        {
            index++;
        }

        if (index >= sql.Length || sql[index] != '$')
        {
            return false;
        }

        tag = sql[start..(index + 1)];
        return true;
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value)
        => char.IsLetterOrDigit(value)
            || value == '_'
            || CharUnicodeInfo.GetUnicodeCategory(value) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
}
