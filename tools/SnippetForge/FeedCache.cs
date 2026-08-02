using System.Text.Json;

namespace SnippetForge;

/// <summary>Métadonnées d'un artefact, et l'empreinte de fichier qui les valide.</summary>
public sealed record CachedPackage(string FileName, long Length, long LastWriteUtcTicks, PackageMeta Meta, string Readme);

/// <summary>
/// Mémorise ce qui a déjà été lu dans le feed, pour ne pas rouvrir chaque archive à
/// chaque réindexation.
///
/// <c>forge index</c> ouvrait et décompressait **tous** les .nupkg à chaque appel : à
/// huit artefacts c'est instantané, à quelques milliers c'est plusieurs minutes. Or un
/// artefact publié est immuable (R8) : une fois lu, il n'a aucune raison de changer.
///
/// La validité d'une entrée repose sur la taille et la date de modification du
/// fichier — jamais sur son seul nom. Un artefact remplacé sur disque est donc relu,
/// et <c>forge verify</c> reste seul juge de la légitimité de ce remplacement.
/// </summary>
public sealed class FeedCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly Dictionary<string, CachedPackage> _entries;

    private FeedCache(string path, Dictionary<string, CachedPackage> entries)
    {
        _path = path;
        _entries = entries;
    }

    /// <summary>Nombre d'artefacts en cache.</summary>
    public int Count => _entries.Count;

    /// <summary>Charge le cache (vide s'il est absent ou illisible).</summary>
    public static FeedCache Load(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var path = Path.Combine(root.RegistryDir, "feed-cache.json");
        if (!File.Exists(path))
        {
            return new FeedCache(path, new Dictionary<string, CachedPackage>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            var entries = JsonSerializer.Deserialize<Dictionary<string, CachedPackage>>(File.ReadAllText(path), JsonOptions);
            return new FeedCache(
                path,
                new Dictionary<string, CachedPackage>(entries ?? [], StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            // Cache corrompu : il est entièrement dérivé du feed, on le reconstruit.
            return new FeedCache(path, new Dictionary<string, CachedPackage>(StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Retourne l'entrée valide pour ce fichier, ou null s'il faut le relire.
    /// </summary>
    public CachedPackage? TryGet(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _entries.TryGetValue(file.Name, out var cached) &&
               cached.Length == file.Length &&
               cached.LastWriteUtcTicks == file.LastWriteTimeUtc.Ticks
            ? cached
            : null;
    }

    /// <summary>Mémorise le résultat de la lecture d'un artefact.</summary>
    public void Store(FileInfo file, PackageMeta meta, string readme)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(meta);

        _entries[file.Name] = new CachedPackage(
            file.Name, file.Length, file.LastWriteTimeUtc.Ticks, meta, readme);
    }

    /// <summary>Retire les entrées dont l'artefact n'est plus dans le feed.</summary>
    public void PruneTo(IEnumerable<string> presentFileNames)
    {
        ArgumentNullException.ThrowIfNull(presentFileNames);

        var present = new HashSet<string>(presentFileNames, StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _entries.Keys.Where(k => !present.Contains(k)).ToList())
        {
            _entries.Remove(stale);
        }
    }

    /// <summary>Écrit le cache sur disque.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
    }
}
