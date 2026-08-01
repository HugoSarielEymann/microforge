using System.Text.Json;

namespace SnippetForge.Metrics;

/// <summary>Projet raccordé à la bibliothèque par « forge init ».</summary>
public sealed record RegisteredConsumer(string Path, DateTime RegisteredUtc);

/// <summary>
/// Liste des projets raccordés (registry/consumers.json). Sert uniquement aux
/// métriques : sans elle, « forge stats » ne saurait pas où chercher les
/// réutilisations. Régénérable en relançant « forge init » dans chaque projet.
/// </summary>
public sealed class ConsumerRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly List<RegisteredConsumer> _entries;

    private ConsumerRegistry(string path, List<RegisteredConsumer> entries)
    {
        _path = path;
        _entries = entries;
    }

    /// <summary>Projets enregistrés.</summary>
    public IReadOnlyList<RegisteredConsumer> Entries => _entries;

    /// <summary>Charge le registre (vide s'il est absent ou illisible).</summary>
    public static ConsumerRegistry Load(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var path = Path.Combine(root.RegistryDir, "consumers.json");
        if (!File.Exists(path))
        {
            return new ConsumerRegistry(path, []);
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<RegisteredConsumer>>(File.ReadAllText(path), JsonOptions);
            return new ConsumerRegistry(path, entries ?? []);
        }
        catch (JsonException)
        {
            return new ConsumerRegistry(path, []);
        }
    }

    /// <summary>Enregistre un projet, sans doublon. Retourne vrai s'il était nouveau.</summary>
    public bool Register(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);

        var normalized = Path.GetFullPath(projectDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (_entries.Any(e => string.Equals(e.Path, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        _entries.Add(new RegisteredConsumer(normalized, DateTime.UtcNow));
        return true;
    }

    /// <summary>Retire les projets dont le dossier n'existe plus. Retourne le nombre retiré.</summary>
    public int PruneMissing() => _entries.RemoveAll(e => !Directory.Exists(e.Path));

    /// <summary>
    /// Projets réellement extérieurs à la bibliothèque. Le projet <c>demo/</c> de
    /// MicroForge est un exemple livré avec l'outil : le compter comme consommateur
    /// gonflerait le bilan d'une réutilisation que personne n'a décidée.
    /// </summary>
    public IReadOnlyList<RegisteredConsumer> ExternalTo(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return _entries.Where(e => !IsInside(root.Path, e.Path)).ToList();
    }

    /// <summary>
    /// Sensibilité à la casse des chemins, selon le système de fichiers.
    ///
    /// Comparer sans tenir compte de la casse sur Linux est faux : <c>/a/Projet</c> et
    /// <c>/a/projet</c> y sont deux dossiers distincts, et les confondre exclurait à
    /// tort un consommateur du bilan. Windows et macOS sont insensibles par défaut.
    /// </summary>
    private static StringComparison PathComparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Indique si <paramref name="candidate"/> est situé sous <paramref name="parent"/>.
    ///
    /// Les deux chemins sont résolus par le système courant : cette comparaison n'a de
    /// sens qu'entre chemins de la même machine, ce qui est le seul usage — le registre
    /// des consommateurs est local et n'est jamais partagé.
    /// </summary>
    public static bool IsInside(string parent, string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));

        if (normalizedCandidate.Equals(normalizedParent, PathComparison))
        {
            return true;
        }

        // Le séparateur final est indispensable : sans lui, « /a/forgerie » passerait
        // pour un sous-dossier de « /a/forge ».
        return normalizedCandidate.StartsWith(normalizedParent + Path.DirectorySeparatorChar, PathComparison);
    }

    /// <summary>Écrit le registre sur disque.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
    }
}
