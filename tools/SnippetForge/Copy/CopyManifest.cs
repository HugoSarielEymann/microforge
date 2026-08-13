using System.Security.Cryptography;
using System.Text.Json;

namespace SnippetForge.Copy;

/// <summary>Un micropackage copié dans un projet, et l'empreinte de chaque fichier posé.</summary>
public sealed record CopiedPackage(
    string PackageId,
    string Version,
    string Directory,
    DateTime CopiedUtc,
    Dictionary<string, string> FileHashes);

/// <summary>État d'un fichier copié face à sa source.</summary>
public enum CopiedFileState
{
    /// <summary>Identique à ce qui a été posé.</summary>
    Untouched = 0,

    /// <summary>Modifié localement depuis la copie.</summary>
    Modified = 1,

    /// <summary>Supprimé du projet.</summary>
    Absent = 2,
}

/// <summary>
/// Provenance des micropackages copiés dans un projet
/// (<c>.microforge/copied.json</c>).
///
/// C'est ce qui distingue une copie d'un copier-coller. Sans manifeste, le code copié
/// devient orphelin : plus de mise à jour, plus de détection de divergence, et la
/// bibliothèque retrouve le problème qu'elle existe pour résoudre — N variantes du
/// même code. Avec, on sait toujours d'où vient chaque fichier, en quelle version, et
/// s'il a été retouché depuis.
/// </summary>
public sealed class CopyManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly Dictionary<string, CopiedPackage> _entries;

    private CopyManifest(string path, Dictionary<string, CopiedPackage> entries)
    {
        _path = path;
        _entries = entries;
    }

    /// <summary>Packages copiés dans ce projet.</summary>
    public IReadOnlyCollection<CopiedPackage> Entries => _entries.Values;

    /// <summary>Emplacement du manifeste dans un projet.</summary>
    public static string PathFor(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        return Path.Combine(projectDirectory, ".microforge", "copied.json");
    }

    /// <summary>Charge le manifeste d'un projet (vide s'il n'y en a pas).</summary>
    public static CopyManifest Load(string projectDirectory)
    {
        var path = PathFor(projectDirectory);
        if (!File.Exists(path))
        {
            return new CopyManifest(path, new Dictionary<string, CopiedPackage>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            var entries = JsonSerializer.Deserialize<Dictionary<string, CopiedPackage>>(File.ReadAllText(path), JsonOptions);
            return new CopyManifest(
                path,
                new Dictionary<string, CopiedPackage>(entries ?? [], StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return new CopyManifest(path, new Dictionary<string, CopiedPackage>(StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>Entrée d'un package, ou null s'il n'a jamais été copié.</summary>
    public CopiedPackage? Find(string packageId) =>
        string.IsNullOrWhiteSpace(packageId) ? null : _entries.GetValueOrDefault(packageId);

    /// <summary>Enregistre ou remplace la provenance d'un package.</summary>
    public void Record(CopiedPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        _entries[package.PackageId] = package;
    }

    /// <summary>Retire un package du manifeste.</summary>
    public bool Remove(string packageId) => _entries.Remove(packageId);

    /// <summary>Écrit le manifeste.</summary>
    public void Save()
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
    }

    /// <summary>
    /// Confronte les fichiers posés à leurs empreintes : ce qui a été retouché
    /// localement, ce qui a disparu.
    /// </summary>
    public static IReadOnlyList<(string RelativePath, CopiedFileState State)> Inspect(
        string projectDirectory,
        CopiedPackage package)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentNullException.ThrowIfNull(package);

        var results = new List<(string, CopiedFileState)>();

        foreach (var (relative, expected) in package.FileHashes.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var full = Path.Combine(projectDirectory, relative);
            var state = !File.Exists(full) ? CopiedFileState.Absent
                : Hash(full) != expected ? CopiedFileState.Modified
                : CopiedFileState.Untouched;

            results.Add((relative, state));
        }

        return results;
    }

    /// <summary>Empreinte d'un fichier, indépendante des fins de ligne du système.</summary>
    public static string Hash(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Les fins de ligne sont normalisées : un fichier réécrit par un éditeur qui
        // convertit LF en CRLF n'est pas une modification du code.
        var normalized = File.ReadAllText(filePath).ReplaceLineEndings("\n");
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)))[..16].ToLowerInvariant();
    }
}
