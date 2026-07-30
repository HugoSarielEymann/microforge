using System.Security.Cryptography;
using System.Text;

namespace SnippetForge.Api;

/// <summary>
/// Empreinte du contrat public d'une version publiée : la liste triée des membres
/// visibles par un consommateur. C'est le « digest » du package, au sens Docker :
/// deux versions au même digest d'API sont interchangeables à la compilation.
/// </summary>
public sealed record ApiSurface(string PackageId, string Version, IReadOnlyList<string> Members)
{
    /// <summary>Empreinte courte du contrat, affichable et comparable d'un coup d'œil.</summary>
    public string Digest => ComputeDigest(Members);

    /// <summary>Calcule l'empreinte d'un ensemble de membres.</summary>
    public static string ComputeDigest(IEnumerable<string> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var canonical = string.Join('\n', members.OrderBy(m => m, StringComparer.Ordinal));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..12].ToLowerInvariant();
    }
}

/// <summary>Bilan d'une régénération des contrats publics.</summary>
public sealed record ApiSurfaceRebuildResult(int Extracted, IReadOnlyList<string> Warnings);

/// <summary>Différence de contrat entre deux versions.</summary>
public sealed record ApiDiff(IReadOnlyList<string> Added, IReadOnlyList<string> Removed)
{
    /// <summary>Incrément SemVer minimal exigé par cette différence.</summary>
    public BumpLevel RequiredBump =>
        Removed.Count > 0 ? BumpLevel.Major
        : Added.Count > 0 ? BumpLevel.Minor
        : BumpLevel.Patch;

    /// <summary>Vrai si le contrat a perdu des membres : les consommateurs peuvent casser.</summary>
    public bool IsBreaking => Removed.Count > 0;

    /// <summary>Vrai si les deux contrats sont identiques.</summary>
    public bool IsIdentical => Added.Count == 0 && Removed.Count == 0;

    /// <summary>Compare deux surfaces : ce qui apparaît et ce qui disparaît de <paramref name="previous"/> vers <paramref name="candidate"/>.</summary>
    public static ApiDiff Between(ApiSurface previous, ApiSurface candidate)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);

        var before = new HashSet<string>(previous.Members, StringComparer.Ordinal);
        var after = new HashSet<string>(candidate.Members, StringComparer.Ordinal);

        return new ApiDiff(
            after.Except(before, StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList(),
            before.Except(after, StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList());
    }
}
