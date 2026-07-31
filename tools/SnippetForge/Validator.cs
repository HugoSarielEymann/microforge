using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SnippetForge.Hazards;
using SnippetForge.Quality;

namespace SnippetForge;

/// <summary>
/// Applique les règles immuables de RULES.md à un micropackage :
/// structure, métadonnées, API bannies, documentation, tests.
/// </summary>
public static partial class Validator
{
    private static readonly (Regex Pattern, string Reason)[] BannedApis =
    [
        (BannedRegex(@"Console\s*\."), "Sortie console interdite : injecter Microsoft.Extensions.Logging.ILogger."),
        (BannedRegex(@"Thread\s*\.\s*Sleep"), "Thread.Sleep interdit : injecter une stratégie de délai asynchrone."),
        (BannedRegex(@"DateTime\s*\.\s*(Now|UtcNow|Today)"), "Horloge ambiante interdite : injecter TimeProvider ou un Func<DateTime>."),
        (BannedRegex(@"new\s+Random\s*\(\s*\)"), "Random sans graine interdit : injecter la source d'aléa ou accepter une graine."),
        (BannedRegex(@"Environment\s*\.\s*(Exit|GetEnvironmentVariable)"), "Accès à l'environnement interdit : passer les valeurs en paramètres."),
        (BannedRegex(@"\bFile\s*\."), "E/S fichier interdite dans un micropackage : recevoir des Stream/string en paramètres."),
        (BannedRegex(@"\bDirectory\s*\."), "E/S disque interdite dans un micropackage : recevoir les données en paramètres."),
        (BannedRegex(@"Process\s*\.\s*Start"), "Lancement de processus interdit dans un micropackage."),
        (BannedRegex(@"\.GetAwaiter\s*\(\s*\)\s*\.GetResult\s*\(\s*\)"), "Blocage synchrone sur async interdit : rester async de bout en bout."),
    ];

    [GeneratedRegex(@"^Micro\.[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PackageIdRegex();

    /// <summary>
    /// Valide le micropackage situé dans <paramref name="packageDir"/>.
    /// Retourne la liste des erreurs (vide si conforme).
    /// </summary>
    public static IReadOnlyList<string> Validate(ForgeRoot root, string packageDir, bool skipTests)
    {
        var errors = new List<string>();
        packageDir = Path.GetFullPath(packageDir);

        if (!Directory.Exists(packageDir))
        {
            return [$"Dossier introuvable : {packageDir}"];
        }

        var readmePath = Path.Combine(packageDir, "README.md");
        var srcDir = Path.Combine(packageDir, "src");
        var testsDir = Path.Combine(packageDir, "tests");

        if (!File.Exists(readmePath)) errors.Add("README.md manquant à la racine du package.");
        if (!Directory.Exists(srcDir)) errors.Add("Dossier src/ manquant.");
        if (!Directory.Exists(testsDir)) errors.Add("Dossier tests/ manquant (les tests unitaires sont obligatoires).");
        if (errors.Count > 0) return errors;

        var csproj = Directory.EnumerateFiles(srcDir, "*.csproj").ToList();
        if (csproj.Count != 1)
        {
            errors.Add($"src/ doit contenir exactement un .csproj (trouvé : {csproj.Count}).");
            return errors;
        }

        var packageId = Path.GetFileName(packageDir.TrimEnd(Path.DirectorySeparatorChar));
        ValidateMetadata(root, csproj[0], packageId, errors);
        errors.AddRange(ReadmeQuality.Analyze(File.ReadAllText(readmePath), packageId));
        ValidateBannedApis(srcDir, errors);
        ValidateTests(testsDir, errors);
        ValidateHazards(root, packageDir, testsDir, errors);

        if (errors.Count == 0 && !skipTests)
        {
            RunTests(packageDir, errors);
        }

        return errors;
    }

    private static void ValidateMetadata(ForgeRoot root, string csprojPath, string folderName, List<string> errors)
    {
        var project = XDocument.Load(csprojPath);
        string Property(string name) =>
            project.Descendants(name).FirstOrDefault()?.Value.Trim() ?? string.Empty;

        var id = Property("PackageId");
        var version = Property("Version");
        var description = Property("Description");
        var tags = Property("PackageTags")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!PackageIdRegex().IsMatch(id))
        {
            errors.Add($"PackageId « {id} » invalide : format requis Micro.<Domaine>.<Action> en PascalCase.");
        }

        if (id != folderName)
        {
            errors.Add($"Le dossier « {folderName} » doit porter exactement le nom du PackageId « {id} ».");
        }

        if (string.IsNullOrEmpty(version) || !SemVerLite.IsValid(version))
        {
            errors.Add($"Version « {version} » invalide : SemVer requis (majeur.mineur.patch).");
        }
        else if (FeedIndexer.VersionExists(root, id, version))
        {
            errors.Add($"La version {version} de {id} est déjà publiée : les versions sont immuables, incrémentez la version.");
        }

        if (description.Length < 30)
        {
            errors.Add("Description trop courte (minimum 30 caractères) : elle alimente le moteur de recherche.");
        }

        if (tags.Length < 3)
        {
            errors.Add($"PackageTags insuffisants ({tags.Length}) : minimum 3 tags, séparés par « ; ».");
        }

        if (Property("PackageReadmeFile") != "README.md")
        {
            errors.Add("PackageReadmeFile doit valoir README.md pour embarquer le mode d'emploi dans le package.");
        }
    }

    private static void ValidateBannedApis(string srcDir, List<string> errors)
    {
        foreach (var file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var code = File.ReadAllText(file);
            foreach (var (pattern, reason) in BannedApis)
            {
                var match = pattern.Match(code);
                if (match.Success)
                {
                    var line = code[..match.Index].Count(c => c == '\n') + 1;
                    errors.Add($"{Path.GetFileName(file)}:{line} : API bannie « {match.Value.Trim()} ». {reason}");
                }
            }
        }
    }

    private static void ValidateTests(string testsDir, List<string> errors)
    {
        var hasTestAttribute = Directory
            .EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories)
            .Any(f => File.ReadAllText(f) is var code &&
                      (code.Contains("[Fact", StringComparison.Ordinal) ||
                       code.Contains("[Theory", StringComparison.Ordinal)));

        if (!hasTestAttribute)
        {
            errors.Add("tests/ ne contient aucun [Fact] ou [Theory] : chaque micropackage doit prouver son comportement.");
        }
    }

    /// <summary>
    /// Un aléa déclaré doit être prouvé par un test marqué du trait correspondant.
    /// La déclaration sans preuve serait pire que l'absence de déclaration : elle
    /// laisserait croire le cas couvert.
    /// </summary>
    private static void ValidateHazards(ForgeRoot root, string packageDir, string testsDir, List<string> errors)
    {
        var declared = HazardDeclaration.Read(packageDir);
        if (declared.Count == 0)
        {
            return;
        }

        var testSources = string.Join('\n', Directory
            .EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText));

        foreach (var gap in HazardDeclaration.Verify(declared, testSources, HazardCatalogue.Load(root)))
        {
            errors.Add($"Aléa « {gap.HazardId} » : {gap.Reason}");
        }
    }

    private static void RunTests(string packageDir, List<string> errors)
    {
        // Tests en Release : c'est le binaire qui sera empaqueté et livré.
        Console.WriteLine($"  Exécution des tests unitaires en {BuildCommands.Configuration}...");
        var info = new ProcessStartInfo("dotnet", BuildCommands.TestArguments())
        {
            WorkingDirectory = packageDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var tail = string.Join('\n', output.Split('\n').TakeLast(25));
            errors.Add($"Échec de dotnet test (code {process.ExitCode}) :\n{tail}");
        }
    }

    private static Regex BannedRegex(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
