using System.Security.Cryptography;
using System.Text.Json;
using SnippetForge.Consumers;
using SnippetForge.Telemetry;

namespace SnippetForge.Bench;

/// <summary>État d'un projet capturé au début d'une manche de test.</summary>
public sealed record BenchSnapshot(
    string Name,
    DateTime StartedUtc,
    string ProjectPath,
    string? Prompt,
    Dictionary<string, string> FileHashes,
    Dictionary<string, int> CsLineCounts,
    List<string> FeedArtifacts,
    List<string> ForgeReferences,
    long UsageLogPosition);

/// <summary>Bilan mesuré d'une manche : tout ce qui a changé depuis la capture.</summary>
public sealed record BenchReport(
    string Name,
    TimeSpan Elapsed,
    IReadOnlyList<string> AddedFiles,
    IReadOnlyList<string> ModifiedFiles,
    int NetCsLines,
    IReadOnlyList<string> NewForgeReferences,
    IReadOnlyList<string> ForgedArtifacts,
    IReadOnlyList<UsageEntry> ForgeInvocations)
{
    /// <summary>Vrai si au moins une recherche a précédé la première écriture mesurée.</summary>
    public bool SearchedBeforeCoding =>
        ForgeInvocations.Any(e => e.Command is "search" or "info");
}

/// <summary>
/// Encadre une manche de test avec un agent IA. L'humain reste le déclencheur — on ne
/// pilote pas Copilot par API — mais tout le reste est mesuré mécaniquement : fichiers
/// et lignes produits, packages réutilisés, packages forgés, et surtout la trace des
/// commandes forge réellement invoquées pendant la manche.
/// </summary>
public static class BenchSession
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string[] ExcludedSegments = ["bin", "obj", ".git", ".vs", "node_modules"];

    /// <summary>Chemin du fichier de session d'une manche.</summary>
    public static string SessionPath(ForgeRoot root, string name)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(root.RegistryDir, "bench", name + ".json");
    }

    /// <summary>Capture l'état initial d'une manche et l'enregistre.</summary>
    public static BenchSnapshot Capture(ForgeRoot root, string name, string projectPath, string? prompt)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        projectPath = Path.GetFullPath(projectPath);
        if (!Directory.Exists(projectPath))
        {
            throw new ArgumentException($"Projet introuvable : {projectPath}", nameof(projectPath));
        }

        var (hashes, csLines) = ScanProject(projectPath);
        var snapshot = new BenchSnapshot(
            name,
            DateTime.UtcNow,
            projectPath,
            prompt,
            hashes,
            csLines,
            ListFeedArtifacts(root),
            ListForgeReferences(projectPath),
            UsageLog.CurrentPosition(root));

        var path = SessionPath(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions));
        return snapshot;
    }

    /// <summary>Recharge une manche capturée, ou null si elle n'existe pas.</summary>
    public static BenchSnapshot? Load(ForgeRoot root, string name)
    {
        var path = SessionPath(root, name);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<BenchSnapshot>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    /// <summary>Noms des manches capturées.</summary>
    public static IReadOnlyList<string> List(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var dir = Path.Combine(root.RegistryDir, "bench");
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n is not null)
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];
    }

    /// <summary>Mesure tout ce qui a changé depuis la capture.</summary>
    public static BenchReport Report(ForgeRoot root, BenchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(snapshot);

        var (hashesNow, csLinesNow) = ScanProject(snapshot.ProjectPath);

        var added = hashesNow.Keys.Except(snapshot.FileHashes.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        var modified = hashesNow
            .Where(kv => snapshot.FileHashes.TryGetValue(kv.Key, out var before) && before != kv.Value)
            .Select(kv => kv.Key)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        // Lignes C# nettes : total actuel moins total capturé. Approximation honnête —
        // une réécriture à volume constant compte pour zéro, ce qui est le but : on
        // mesure le code qu'il a fallu produire, pas l'agitation.
        var netCsLines = csLinesNow.Values.Sum() - snapshot.CsLineCounts.Values.Sum();

        var referencesNow = ListForgeReferences(snapshot.ProjectPath);
        var newReferences = referencesNow.Except(snapshot.ForgeReferences, StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();

        var forged = ListFeedArtifacts(root).Except(snapshot.FeedArtifacts, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();

        // Les invocations « bench » elles-mêmes sont de l'instrumentation, pas du
        // travail d'agent : les compter fausserait la trace de la manche.
        var invocations = UsageLog.ReadSince(root, snapshot.UsageLogPosition)
            .Where(e => e.Command != "bench")
            .ToList();

        return new BenchReport(
            snapshot.Name,
            DateTime.UtcNow - snapshot.StartedUtc,
            added,
            modified,
            netCsLines,
            newReferences,
            forged,
            invocations);
    }

    private static (Dictionary<string, string> Hashes, Dictionary<string, int> CsLines) ScanProject(string projectPath)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var csLines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(projectPath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(projectPath, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => ExcludedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            using var stream = File.OpenRead(file);
            hashes[relative] = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

            if (relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                csLines[relative] = File.ReadLines(file).Count(l => !string.IsNullOrWhiteSpace(l));
            }
        }

        return (hashes, csLines);
    }

    private static List<string> ListFeedArtifacts(ForgeRoot root) =>
        Directory.Exists(root.FeedDir)
            ? Directory.EnumerateFiles(root.FeedDir, "*.nupkg").Select(f => Path.GetFileName(f)!).ToList()
            : [];

    private static List<string> ListForgeReferences(string projectPath) =>
        Directory.EnumerateFiles(projectPath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar)
                .Any(s => ExcludedSegments.Contains(s, StringComparer.OrdinalIgnoreCase)))
            .SelectMany(f => ConsumerProject.ReadForgeReferences(f)
                .Select(r => $"{r.PackageId} {r.Version}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
