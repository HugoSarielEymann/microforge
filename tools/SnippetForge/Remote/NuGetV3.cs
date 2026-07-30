using System.Text.Json;

namespace SnippetForge.Remote;

/// <summary>
/// Lecture minimale du protocole NuGet v3 : juste ce qu'il faut pour localiser et
/// télécharger un artefact depuis un dépôt d'équipe (BaGet, Azure Artifacts, GitHub
/// Packages…). Les fonctions de parsing sont pures et testées sans réseau.
/// </summary>
public static class NuGetV3
{
    /// <summary>Type de ressource portant l'adresse de base des artefacts.</summary>
    public const string PackageBaseAddressType = "PackageBaseAddress/3.0.0";

    /// <summary>
    /// Extrait de l'index de service (le <c>index.json</c> de la source) l'adresse de
    /// base des artefacts, ou null si la source n'en expose pas.
    /// </summary>
    /// <exception cref="JsonException">Si le document n'est pas du JSON.</exception>
    public static string? ParsePackageBaseAddress(string serviceIndexJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceIndexJson);

        using var document = JsonDocument.Parse(serviceIndexJson);
        if (!document.RootElement.TryGetProperty("resources", out var resources))
        {
            return null;
        }

        foreach (var resource in resources.EnumerateArray())
        {
            if (resource.TryGetProperty("@type", out var type) &&
                type.GetString()?.StartsWith(PackageBaseAddressType, StringComparison.OrdinalIgnoreCase) == true &&
                resource.TryGetProperty("@id", out var id))
            {
                var address = id.GetString();
                return string.IsNullOrWhiteSpace(address) ? null : address.TrimEnd('/') + "/";
            }
        }

        return null;
    }

    /// <summary>Extrait la liste des versions publiées du document de versions d'un package.</summary>
    /// <exception cref="JsonException">Si le document n'est pas du JSON.</exception>
    public static IReadOnlyList<string> ParseVersions(string versionIndexJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionIndexJson);

        using var document = JsonDocument.Parse(versionIndexJson);
        if (!document.RootElement.TryGetProperty("versions", out var versions))
        {
            return [];
        }

        return versions.EnumerateArray()
            .Select(v => v.GetString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToList();
    }

    /// <summary>URL du document de versions d'un package.</summary>
    public static string VersionIndexUrl(string baseAddress, string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        return $"{baseAddress.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
    }

    /// <summary>URL de téléchargement d'un artefact précis.</summary>
    public static string DownloadUrl(string baseAddress, string packageId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var id = packageId.ToLowerInvariant();
        var v = version.ToLowerInvariant();
        return $"{baseAddress.TrimEnd('/')}/{id}/{v}/{id}.{v}.nupkg";
    }
}
