using System.Text.Json;

namespace SnippetForge.Lifecycle;

/// <summary>Déclaration de dépréciation portant sur une ou plusieurs versions d'un package.</summary>
public sealed record DeprecationEntry(
    string PackageId,
    string VersionSpec,
    string Reason,
    string? Replacement,
    DateTime DeclaredUtc);

/// <summary>
/// Registre des dépréciations, stocké hors des artefacts (registry/deprecations.json).
///
/// Un .nupkg publié reste bit à bit immuable (R8) : la dépréciation est une couche de
/// métadonnées à part, réversible, qui n'altère jamais le contenu déjà livré aux
/// consommateurs. Elle informe sans casser les builds existants.
/// </summary>
public sealed class DeprecationRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;
    private readonly List<DeprecationEntry> _entries;

    private DeprecationRegistry(string path, List<DeprecationEntry> entries)
    {
        _path = path;
        _entries = entries;
    }

    /// <summary>Toutes les dépréciations déclarées.</summary>
    public IReadOnlyList<DeprecationEntry> Entries => _entries;

    /// <summary>Charge le registre (vide si absent).</summary>
    public static DeprecationRegistry Load(ForgeRoot root)
    {
        var path = Path.Combine(root.RegistryDir, "deprecations.json");
        if (!File.Exists(path))
        {
            return new DeprecationRegistry(path, []);
        }

        var entries = JsonSerializer.Deserialize<List<DeprecationEntry>>(File.ReadAllText(path), JsonOptions);
        return new DeprecationRegistry(path, entries ?? []);
    }

    /// <summary>Déclare une dépréciation, en remplaçant une déclaration identique existante.</summary>
    /// <exception cref="ArgumentException">Si la spécification de version est invalide.</exception>
    public void Deprecate(string packageId, string versionSpec, string reason, string? replacement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!IsValidSpec(versionSpec))
        {
            throw new ArgumentException(
                $"Spécification de version invalide : « {versionSpec} ». " +
                "Formes acceptées : *, 1.2.3, <1.2.3, <=1.2.3.", nameof(versionSpec));
        }

        _entries.RemoveAll(e =>
            e.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) &&
            e.VersionSpec == versionSpec);

        _entries.Add(new DeprecationEntry(packageId, versionSpec, reason, replacement, DateTime.UtcNow));
    }

    /// <summary>Retire les dépréciations d'un package (toutes, ou celles d'une spécification précise).</summary>
    /// <returns>Le nombre de déclarations retirées.</returns>
    public int Undeprecate(string packageId, string? versionSpec = null) =>
        _entries.RemoveAll(e =>
            e.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) &&
            (versionSpec is null || e.VersionSpec == versionSpec));

    /// <summary>Retourne les dépréciations applicables à une version donnée.</summary>
    public IReadOnlyList<DeprecationEntry> For(string packageId, string version) =>
        _entries
            .Where(e => e.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) && Matches(e.VersionSpec, version))
            .ToList();

    /// <summary>Indique si au moins une version du package est dépréciée.</summary>
    public bool IsPackageAffected(string packageId) =>
        _entries.Any(e => e.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Écrit le registre sur disque.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
    }

    /// <summary>Valide une spécification de version.</summary>
    public static bool IsValidSpec(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        if (spec == "*")
        {
            return true;
        }

        if (spec.StartsWith("<=", StringComparison.Ordinal))
        {
            return SemVerLite.IsValid(spec[2..]);
        }

        if (spec.StartsWith('<'))
        {
            return SemVerLite.IsValid(spec[1..]);
        }

        return SemVerLite.IsValid(spec);
    }

    /// <summary>
    /// Évalue une spécification contre une version.
    /// Formes : <c>*</c> (toutes), <c>1.2.3</c> (exacte), <c>&lt;1.2.3</c>, <c>&lt;=1.2.3</c>.
    /// </summary>
    public static bool Matches(string spec, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (spec == "*")
        {
            return true;
        }

        if (spec.StartsWith("<=", StringComparison.Ordinal))
        {
            return SemVerLite.Compare(version, spec[2..]) <= 0;
        }

        if (spec.StartsWith('<'))
        {
            return SemVerLite.Compare(version, spec[1..]) < 0;
        }

        return SemVerLite.Compare(version, spec) == 0;
    }
}
