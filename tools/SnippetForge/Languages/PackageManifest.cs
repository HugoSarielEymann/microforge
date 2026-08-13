using System.Text.Json;

namespace SnippetForge.Languages;

/// <summary>
/// Métadonnées d'un micropackage hors .NET (<c>microforge.json</c>).
///
/// En C#, le <c>.csproj</c> tient ce rôle : il porte déjà identifiant, version,
/// description et tags. Ailleurs, il n'existe pas de fichier équivalent commun à tous
/// les écosystèmes — d'où ce manifeste, volontairement minimal et identique partout.
/// </summary>
public sealed record PackageManifest(
    string Id,
    string Version,
    string Language,
    string Description,
    IReadOnlyList<string> Tags)
{
    /// <summary>Nom du fichier, à la racine du package.</summary>
    public const string FileName = "microforge.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Chemin du manifeste dans un dossier de package.</summary>
    public static string PathFor(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        return Path.Combine(packageDirectory, FileName);
    }

    /// <summary>Charge le manifeste, ou null s'il est absent ou illisible.</summary>
    public static PackageManifest? Load(string packageDirectory)
    {
        var path = PathFor(packageDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Écrit le manifeste.</summary>
    public void Save(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(PathFor(packageDirectory), JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>Le profil correspondant au langage déclaré.</summary>
    public LanguageProfile Profile => LanguageProfiles.Resolve(Language);
}
