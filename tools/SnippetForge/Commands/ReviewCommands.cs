using SnippetForge.Api;
using SnippetForge.Hazards;
using SnippetForge.Quality;

namespace SnippetForge.Commands;

/// <summary>Préparation de la relecture d'un micropackage.</summary>
public static class ReviewCommands
{
    /// <summary>
    /// Compile le package, extrait son contrat public, le confronte aux tests et
    /// produit la liste des points à faire relire.
    /// </summary>
    public static int Review(ForgeRoot root, string[] args)
    {
        var package = PackageSource.Resolve(root, Cli.RequireArg(args, 1, "chemin ou PackageId"));

        Console.WriteLine($"Revue de {package.Id} {package.Version}");
        Console.WriteLine();

        var surface = ExtractSurface(package, out var error);
        if (surface is null)
        {
            return Cli.Fail(error!);
        }

        var sourceText = ReadAll(Path.Combine(package.Directory, "src"));
        var testText = ReadAll(Path.Combine(package.Directory, "tests"));
        var findings = ReviewAnalyzer.Analyze(surface, sourceText, testText);

        Console.WriteLine($"Contrat public : {surface.Members.Count} membre(s), empreinte {surface.Digest}");
        Console.WriteLine($"Tests          : {testText.Split('\n').Count(l => l.Contains("[Fact", StringComparison.Ordinal) || l.Contains("[Theory", StringComparison.Ordinal))} cas déclaré(s)");
        Console.WriteLine();

        if (findings.Count == 0)
        {
            Console.WriteLine("Aucun point saillant : chaque membre public est cité dans les tests,");
            Console.WriteLine("les exceptions documentées sont provoquées, les bornes sont abordées.");
        }
        else
        {
            var gaps = findings.Count(f => f.Severity == ReviewSeverity.Gap);
            Console.WriteLine($"{findings.Count} point(s) à relire, dont {gaps} absence(s) constatée(s) :");
            Console.WriteLine();

            foreach (var finding in findings)
            {
                var marker = finding.Severity == ReviewSeverity.Gap ? "ABSENCE " : "question";
                Console.WriteLine($"  [{marker}] {finding.Category} — {finding.Subject}");
                Console.WriteLine($"             {finding.Question}");
                Console.WriteLine();
            }
        }

        ReportHazards(root, package, testText);

        Console.WriteLine("---");
        Console.WriteLine("Ces remarques sont heuristiques : l'outil dirige l'attention, il ne tranche pas.");
        Console.WriteLine("Le validateur garantit que des tests existent et passent, jamais qu'ils couvrent");
        Console.WriteLine("les bons cas — seule une relecture le peut.");
        Console.WriteLine();
        Console.WriteLine("Faire relire par un agent DISTINCT de celui qui a forgé le package : celui qui");
        Console.WriteLine("a écrit le code a déjà, par construction, testé ce à quoi il avait pensé.");

        return 0;
    }

    /// <summary>
    /// Confronte les aléas déclarés au catalogue et aux tests, et signale ceux qui sont
    /// éprouvés sans être déclarés — une déclaration à compléter, pas une faute.
    /// </summary>
    private static void ReportHazards(ForgeRoot root, PackageSource package, string testText)
    {
        var catalogue = HazardCatalogue.Load(root);
        var declared = HazardDeclaration.Read(package.Directory);
        var covered = HazardDeclaration.CoveredBy(testText);

        Console.WriteLine("Aléas de test :");

        if (declared.Count == 0 && covered.Count == 0)
        {
            Console.WriteLine("  Aucun aléa déclaré ni marqué. Les aléas pertinents pour cette capacité :");
            Console.WriteLine($"    forge hazards list");
            Console.WriteLine($"    forge hazards declare {package.Id} --hazards \"null-input;…\"");
            Console.WriteLine();
            return;
        }

        foreach (var gap in HazardDeclaration.Verify(declared, testText, catalogue))
        {
            Console.WriteLine($"  [ABSENCE ] {gap.HazardId} — {gap.Reason}");
        }

        foreach (var id in declared.Where(d => covered.Contains(d, StringComparer.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"  [prouvé  ] {id}");
        }

        foreach (var id in HazardDeclaration.UndeclaredButTested(declared, testText))
        {
            var known = catalogue.Find(id) is not null ? string.Empty : " (inconnu du catalogue)";
            Console.WriteLine($"  [à déclarer] {id} — éprouvé par un test mais absent de hazards.json{known}");
        }

        Console.WriteLine();
    }

    private static ApiSurface? ExtractSurface(PackageSource package, out string? error)
    {
        error = null;
        var srcDir = Path.Combine(package.Directory, "src");

        Console.WriteLine($"  Compilation en {BuildCommands.Configuration}...");
        if (!Cli.Run("dotnet", $"build src -c {BuildCommands.Configuration} --nologo -v quiet", package.Directory, out var output))
        {
            error = $"Le package ne compile pas :\n{Cli.Tail(output, 15)}";
            return null;
        }

        var dll = Directory
            .EnumerateFiles(srcDir, $"{package.Id}.dll", SearchOption.AllDirectories)
            .FirstOrDefault(p => p.Contains(BuildCommands.Configuration, StringComparison.OrdinalIgnoreCase));

        if (dll is null)
        {
            error = $"Assembly {package.Id}.dll introuvable après compilation.";
            return null;
        }

        try
        {
            return ApiSurfaceExtractor.FromAssemblyFile(dll, package.Id, package.Version);
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
            return null;
        }
    }

    private static string ReadAll(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return string.Empty;
        }

        var files = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        return string.Join('\n', files.Select(File.ReadAllText));
    }
}
