using SnippetForge.Embeddings;
using SnippetForge.Lifecycle;
using SnippetForge.Metrics;
using SnippetForge.Remote;

namespace SnippetForge.Commands;

/// <summary>Diagnostic de l'installation : ce qui fonctionne, ce qui manque, quoi faire.</summary>
public static class DoctorCommands
{
    /// <summary>Vérifie l'état de l'installation et retourne 1 si un point bloquant est trouvé.</summary>
    public static async Task<int> Doctor(ForgeRoot root, string[] args)
    {
        var blocking = 0;

        Console.WriteLine("=== MicroForge — diagnostic ===");
        Console.WriteLine($"Racine : {root.Path}");
        Console.WriteLine();

        blocking += Check(
            "SDK .NET",
            Cli.Run("dotnet", "--version", root.Path, out var sdk),
            sdk.Trim(),
            "Installer le SDK .NET 8 ou supérieur.");

        var packages = Directory.Exists(root.FeedDir)
            ? Directory.GetFiles(root.FeedDir, "*.nupkg").Length
            : 0;
        blocking += Check(
            "Feed local",
            packages > 0,
            $"{packages} artefact(s) dans {root.FeedDir}",
            "Lancer setup.ps1 pour publier les micropackages sources.");

        var index = File.Exists(root.IndexFile) ? FeedIndexer.Load(root) : null;
        var indexAge = File.Exists(root.IndexFile)
            ? DateTime.UtcNow - File.GetLastWriteTimeUtc(root.IndexFile)
            : TimeSpan.MaxValue;
        blocking += Check(
            "Index de recherche",
            index is not null,
            index is null ? "absent" : $"{index.Packages.Count} package(s), régénéré il y a {Describe(indexAge)}",
            "Lancer « forge index ».");

        var surfaceCount = CountSurfaces(root, index);
        var expectedSurfaces = index?.Packages.Sum(p => p.AllVersions.Count) ?? 0;
        blocking += Check(
            "Contrats publics",
            expectedSurfaces == 0 || surfaceCount == expectedSurfaces,
            $"{surfaceCount}/{expectedSurfaces} version(s) analysée(s)",
            "Lancer « forge index » ; si le problème persiste, « dotnet restore » d'abord.");

        var resolution = await EmbeddingProviderFactory
            .ResolveAsync(Cli.Flag(args, "--offline"))
            .ConfigureAwait(false);
        Report(
            resolution.IsSemantic ? Status.Ok : Status.Info,
            "Recherche vectorielle",
            resolution.Explanation,
            resolution.IsSemantic ? null : "Facultatif : ollama serve puis ollama pull nomic-embed-text.");
        (resolution.Provider as IDisposable)?.Dispose();

        ReportNuGetSource(root);

        var shim = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "forge.cmd");
        Report(
            File.Exists(shim) ? Status.Ok : Status.Info,
            "Lanceur global",
            File.Exists(shim) ? shim : "absent",
            File.Exists(shim) ? null : "Lancer install.ps1 pour utiliser « forge » depuis n'importe où.");

        var consumers = ConsumerRegistry.Load(root).ExternalTo(root);
        Report(Status.Info, "Projets raccordés", $"{consumers.Count}", null);

        var deprecations = DeprecationRegistry.Load(root);
        if (deprecations.Entries.Count > 0)
        {
            Report(Status.Info, "Dépréciations", $"{deprecations.Entries.Count} déclarée(s)", null);
        }

        var remote = RemoteFeed.Load(root);
        Report(
            Status.Info,
            "Dépôt d'équipe",
            remote is null ? "aucun (feed local seul)" : remote.Source,
            remote is null ? "Pour partager : forge remote --source <url>." : null);

        Console.WriteLine();
        Console.WriteLine(blocking == 0
            ? "Aucun point bloquant."
            : $"{blocking} point(s) bloquant(s) — voir les corrections indiquées ci-dessus.");

        return blocking == 0 ? 0 : 1;
    }

    private enum Status
    {
        Ok,
        Info,
        Problem,
    }

    private static int Check(string label, bool healthy, string detail, string remedy)
    {
        Report(healthy ? Status.Ok : Status.Problem, label, detail, healthy ? null : remedy);
        return healthy ? 0 : 1;
    }

    private static void Report(Status status, string label, string detail, string? remedy)
    {
        var marker = status switch
        {
            Status.Ok => "OK   ",
            Status.Problem => "ERREUR",
            _ => "note ",
        };

        Console.WriteLine($"[{marker}] {label,-22} {detail}");
        if (remedy is not null)
        {
            Console.WriteLine($"          → {remedy}");
        }
    }

    private static int CountSurfaces(ForgeRoot root, IndexDocument? index)
    {
        if (index is null)
        {
            return 0;
        }

        var store = new Api.ApiSurfaceStore(root);
        return index.Packages.Sum(p => p.AllVersions.Count(v => store.Contains(p.Id, v)));
    }

    private static void ReportNuGetSource(ForgeRoot root)
    {
        // Interrogé depuis un dossier neutre : sinon le nuget.config local de MicroForge
        // ferait croire à tort que la source est enregistrée pour toute la machine.
        var neutral = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var listed = Cli.Run("dotnet", "nuget list source", neutral, out var output) &&
                     output.Contains(root.FeedDir, StringComparison.OrdinalIgnoreCase);

        Report(
            listed ? Status.Ok : Status.Info,
            "Source NuGet machine",
            listed ? "enregistrée" : "non enregistrée",
            listed ? null : "Lancer install.ps1, ou « forge init . » dans chaque projet.");
    }

    private static string Describe(TimeSpan age) =>
        age == TimeSpan.MaxValue ? "jamais"
        : age.TotalMinutes < 60 ? $"{age.TotalMinutes:0} min"
        : age.TotalHours < 48 ? $"{age.TotalHours:0} h"
        : $"{age.TotalDays:0} j";
}
