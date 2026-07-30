using System.Text.RegularExpressions;
using SnippetForge.Api;

namespace SnippetForge.Duplicates;

/// <summary>
/// Compare deux contrats publics par leur **forme**, indépendamment des noms choisis.
///
/// La détection par description se contourne en reformulant : deux packages au code
/// identique mais aux mots différents passaient au travers. La forme d'API, elle, ne
/// se reformule pas — <c>ToSlug(string, Options) : string</c> et
/// <c>Create(string, Options) : string</c> ont la même signature une fois les
/// identifiants effacés.
///
/// Ce signal est volontairement **secondaire** : beaucoup de packages sans rapport
/// exposent <c>(string) : string</c>. Il ne sert qu'à confirmer une proximité déjà
/// suspectée par la description, jamais à la déclencher seul.
/// </summary>
public static partial class ApiSimilarity
{
    /// <summary>Marqueur temporaire des types appartenant au package analysé.</summary>
    private const string LocalMarker = "<local>";

    [GeneratedRegex(@"<local>(\.[A-Za-z_][A-Za-z0-9_]*)+")]
    private static partial Regex LocalTypeRegex();

    /// <summary>
    /// Similarité de Jaccard entre les formes des deux contrats, dans [0, 1].
    /// </summary>
    public static double Compare(ApiSurface left, ApiSurface right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftShapes = Shapes(left);
        var rightShapes = Shapes(right);

        if (leftShapes.Count == 0 || rightShapes.Count == 0)
        {
            return 0;
        }

        var intersection = leftShapes.Intersect(rightShapes, StringComparer.Ordinal).Count();
        var union = leftShapes.Union(rightShapes, StringComparer.Ordinal).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    /// <summary>
    /// Réduit un contrat à l'ensemble de ses formes : types de paramètres et de retour,
    /// identifiants propres au package effacés.
    /// </summary>
    public static HashSet<string> Shapes(ApiSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        return surface.Members
            .Where(m => m.StartsWith("method ", StringComparison.Ordinal) ||
                        m.StartsWith("property ", StringComparison.Ordinal) ||
                        m.StartsWith("ctor ", StringComparison.Ordinal))
            .Select(m => Normalize(m, surface.PackageId))
            .Where(shape => shape.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalize(string member, string packageId)
    {
        // Les types du package lui-même deviennent un jeton neutre : seul compte le fait
        // qu'un type local est attendu, pas le nom qu'on lui a donné.
        var withoutOwnTypes = member.Replace(packageId + ".", LocalMarker + ".", StringComparison.OrdinalIgnoreCase);

        var kindEnd = withoutOwnTypes.IndexOf(' ', StringComparison.Ordinal);
        var kind = kindEnd < 0 ? withoutOwnTypes : withoutOwnTypes[..kindEnd];

        var signatureStart = withoutOwnTypes.IndexOf('(', StringComparison.Ordinal);
        if (signatureStart < 0)
        {
            // Propriété : « property X.Y : Type { get; } » → on garde le type.
            var colon = withoutOwnTypes.LastIndexOf(" : ", StringComparison.Ordinal);
            return colon < 0 ? string.Empty : $"{kind}{AnonymizeLocals(withoutOwnTypes[colon..])}";
        }

        return $"{kind}{AnonymizeLocals(withoutOwnTypes[signatureStart..])}";
    }

    /// <summary>
    /// Réduit « &lt;local&gt;.SlugifyOptions » à « &lt;local&gt; » : deux packages qui
    /// prennent leur propre type d'options ont la même forme, quel que soit son nom.
    /// Les types externes (System.String, ILogger…) sont conservés tels quels — ce sont
    /// eux qui portent le sens de la signature.
    /// </summary>
    private static string AnonymizeLocals(string signature) =>
        LocalTypeRegex().Replace(signature, LocalMarker);
}
