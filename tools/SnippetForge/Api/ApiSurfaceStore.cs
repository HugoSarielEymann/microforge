using System.Text.Json;

namespace SnippetForge.Api;

/// <summary>
/// Persiste les surfaces d'API par package et par version (registry/api/&lt;Id&gt;/&lt;Version&gt;.json).
/// Entièrement dérivé du feed : régénérable par « forge index ».
/// </summary>
public sealed class ApiSurfaceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _rootDir;

    /// <summary>Crée le magasin de surfaces sous le registre de <paramref name="root"/>.</summary>
    public ApiSurfaceStore(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _rootDir = Path.Combine(root.RegistryDir, "api");
    }

    /// <summary>Enregistre la surface d'une version.</summary>
    public void Save(ApiSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var dir = Path.Combine(_rootDir, surface.PackageId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{surface.Version}.json"), JsonSerializer.Serialize(surface, JsonOptions));
    }

    /// <summary>Charge la surface d'une version, ou null si elle n'a pas été enregistrée.</summary>
    public ApiSurface? Load(string packageId, string version)
    {
        var path = Path.Combine(_rootDir, packageId, $"{version}.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ApiSurface>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    /// <summary>Indique si la surface d'une version est déjà enregistrée.</summary>
    public bool Contains(string packageId, string version) =>
        File.Exists(Path.Combine(_rootDir, packageId, $"{version}.json"));
}
