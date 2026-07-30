namespace SnippetForge.Metrics;

/// <summary>
/// Mesure le volume de code de chaque micropackage à partir de ses sources.
/// Les lignes vides sont exclues ; les commentaires sont comptés, car ils font
/// partie du code qu'une IA aurait dû produire.
/// </summary>
public static class FootprintScanner
{
    /// <summary>Mesure tous les packages présents sous packages/.</summary>
    public static IReadOnlyList<PackageFootprint> ScanAll(ForgeRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (!Directory.Exists(root.PackagesDir))
        {
            return [];
        }

        return Directory.EnumerateDirectories(root.PackagesDir)
            .Select(Scan)
            .Where(f => f is not null)
            .Select(f => f!)
            .ToList();
    }

    /// <summary>Mesure un package, ou retourne null si le dossier n'en est pas un.</summary>
    public static PackageFootprint? Scan(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        var srcDir = Path.Combine(packageDirectory, "src");
        if (!Directory.Exists(srcDir))
        {
            return null;
        }

        var readmePath = Path.Combine(packageDirectory, "README.md");

        return new PackageFootprint(
            Path.GetFileName(packageDirectory.TrimEnd(Path.DirectorySeparatorChar)),
            CountLines(srcDir, "*.cs"),
            CountLines(Path.Combine(packageDirectory, "tests"), "*.cs"),
            File.Exists(readmePath) ? CountNonEmpty(File.ReadLines(readmePath)) : 0);
    }

    private static int CountLines(string directory, string pattern)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory
            .EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Where(IsAuthored)
            .Sum(file => CountNonEmpty(File.ReadLines(file)));
    }

    /// <summary>Exclut les artefacts de build, qui ne sont écrits par personne.</summary>
    private static bool IsAuthored(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static int CountNonEmpty(IEnumerable<string> lines) =>
        lines.Count(line => !string.IsNullOrWhiteSpace(line));
}
