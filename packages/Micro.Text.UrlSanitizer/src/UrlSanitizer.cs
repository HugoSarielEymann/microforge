using System.Text;

namespace Micro.Text.UrlSanitizer;

/// <summary>
/// Masque les valeurs sensibles dans une URL (paramètres de requête) pour journalisation sécurisée.
/// </summary>
public static class UrlSanitizer
{
    private static readonly UrlSanitizerOptions _defaultOptions = new();

    /// <summary>
    /// Retourne une version sûre à journaliser de l'<paramref name="url"/> fournie,
    /// en remplaçant les valeurs des paramètres sensibles par le masque configuré.
    /// </summary>
    /// <param name="url">URL à assainir. Peut être null ou vide (retournée telle quelle).</param>
    /// <param name="options">Options de masquage ; si null, les valeurs par défaut sont utilisées.</param>
    /// <returns>L'URL avec les valeurs sensibles masquées.</returns>
    public static string Sanitize(string? url, UrlSanitizerOptions? options = null)
    {
        if (string.IsNullOrEmpty(url)) return url ?? string.Empty;

        var opts = options ?? _defaultOptions;
        opts.Validate();

        if (opts.MaskUserInfo)
        {
            url = MaskUserInfo(url, opts.Mask);
        }

        // Séparer la partie avant et après le '?'
        var queryStart = url.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0) return url; // Pas de query string

        var baseUrl = url[..queryStart];
        var query   = url[(queryStart + 1)..];

        // Conserver le fragment (#) s'il est présent
        var fragmentStart = query.IndexOf('#', StringComparison.Ordinal);
        var fragment = string.Empty;
        if (fragmentStart >= 0)
        {
            fragment = query[fragmentStart..];
            query    = query[..fragmentStart];
        }

        var sensitiveSet = new HashSet<string>(opts.SensitiveQueryParams, StringComparer.OrdinalIgnoreCase);
        var pairs = query.Split('&');
        var sb    = new StringBuilder(url.Length);

        sb.Append(baseUrl);
        sb.Append('?');

        for (var i = 0; i < pairs.Length; i++)
        {
            if (i > 0) sb.Append('&');

            var pair = pairs[i];
            var eq   = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0)
            {
                sb.Append(pair);
                continue;
            }

            var name  = pair[..eq];
            var value = pair[(eq + 1)..];

            var decodedName = Uri.UnescapeDataString(name.Replace('+', ' '));
            sb.Append(name);
            sb.Append('=');
            sb.Append(sensitiveSet.Contains(decodedName) ? opts.Mask : value);
        }

        sb.Append(fragment);
        return sb.ToString();
    }

    /// <summary>
    /// Remplace le mot de passe des identifiants d'URL (<c>schéma://user:motdepasse@hôte</c>).
    /// Le nom d'utilisateur est conservé : il sert au diagnostic, il n'est pas le secret.
    /// </summary>
    private static string MaskUserInfo(string url, string mask)
    {
        var schemeEnd = url.IndexOf("//", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return url;
        }

        var authorityStart = schemeEnd + 2;

        // L'arobase doit précéder la fin de l'autorité, sinon il appartient au chemin
        // ou à la query (« /a@b », « ?to=x@y ») et ne délimite pas d'identifiants.
        var authorityEnd = url.AsSpan(authorityStart).IndexOfAny('/', '?', '#');
        var authorityLimit = authorityEnd < 0 ? url.Length : authorityStart + authorityEnd;

        var at = url.LastIndexOf('@', authorityLimit - 1 < 0 ? 0 : authorityLimit - 1);
        if (at < authorityStart)
        {
            return url;
        }

        var userInfo = url[authorityStart..at];
        var colon = userInfo.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            // « user@hôte » sans mot de passe : rien de secret à masquer.
            return url;
        }

        return string.Concat(url.AsSpan(0, authorityStart + colon + 1), mask, url.AsSpan(at));
    }
}