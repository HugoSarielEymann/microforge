using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SnippetForge.Embeddings;

/// <summary>
/// Cache persistant des vecteurs (registry/embeddings.json). Une entrée est invalidée
/// dès que le provider change ou que le texte source change : le vecteur est alors recalculé.
/// </summary>
public sealed class EmbeddingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly Dictionary<string, CacheEntry> _entries;

    private EmbeddingStore(string path, Dictionary<string, CacheEntry> entries)
    {
        _path = path;
        _entries = entries;
    }

    /// <summary>Entrée de cache : vecteur + empreintes permettant de détecter l'obsolescence.</summary>
    public sealed record CacheEntry(string Provider, string ContentHash, float[] Vector);

    /// <summary>Charge le cache depuis le registre (vide si absent ou illisible).</summary>
    public static EmbeddingStore Load(ForgeRoot root)
    {
        var path = Path.Combine(root.RegistryDir, "embeddings.json");
        if (!File.Exists(path))
        {
            return new EmbeddingStore(path, []);
        }

        try
        {
            var entries = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(File.ReadAllText(path), JsonOptions);
            return new EmbeddingStore(path, entries ?? []);
        }
        catch (JsonException)
        {
            // Cache corrompu : il est entièrement dérivé du feed, on le reconstruit.
            return new EmbeddingStore(path, []);
        }
    }

    /// <summary>
    /// Retourne le vecteur de <paramref name="key"/>, en le calculant via
    /// <paramref name="provider"/> si le cache est absent ou périmé.
    /// </summary>
    public async Task<float[]> GetOrComputeAsync(
        string key,
        string text,
        IEmbeddingProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(provider);

        var hash = ContentHash(text);
        if (_entries.TryGetValue(key, out var cached) &&
            cached.Provider == provider.Name &&
            cached.ContentHash == hash)
        {
            return cached.Vector;
        }

        var vector = await provider.EmbedAsync(text, cancellationToken).ConfigureAwait(false);
        _entries[key] = new CacheEntry(provider.Name, hash, vector);
        return vector;
    }

    /// <summary>Supprime les entrées dont la clé n'est plus présente dans le feed.</summary>
    public void PruneTo(IEnumerable<string> liveKeys)
    {
        var live = new HashSet<string>(liveKeys, StringComparer.Ordinal);
        foreach (var stale in _entries.Keys.Where(k => !live.Contains(k)).ToList())
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

    /// <summary>Empreinte stable du texte source d'un vecteur.</summary>
    public static string ContentHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
