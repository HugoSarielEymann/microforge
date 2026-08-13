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

        PackageSource package;
        try
        {
            package = PackageSource.Resolve(root, packageDir);
        }
        catch (ArgumentException exception)
        {
            errors.Add(exception.Message);
            return errors;
        }

        var packageId = Path.GetFileName(packageDir.TrimEnd(Path.DirectorySeparatorChar));

        // Les contrôles indépendants de l'écosystème s'appliquent partout : c'est le
        // socle commun du profil de base.
        errors.AddRange(ReadmeQuality.Analyze(File.ReadAllText(readmePath), packageId));
        ValidateCommonMetadata(root, package, packageId, errors);
        ValidateTests(package, errors);
        ValidateHazards(root, package, errors);

        // Ce qui suit exige de compiler ou de connaître la syntaxe : réservé au
        // profil vérifié. Ailleurs, l'absence de ces contrôles est signalée par
        // « forge validate » et par les commandes de consommation, jamais masquée.
        if (package.Language.SupportsContractVerification)
        {
            ValidateDotNetProject(package, errors);
            ValidateBannedApis(srcDir, errors);

            if (errors.Count == 0 && !skipTests)
            {
                RunTests(packageDir, errors);
            }
        }

        return errors;
    }

    /// <summary>
    /// Contrôles valables dans tous les écosystèmes : nommage, version, description,
    /// tags. Ce sont les données du moteur de recherche et de l'anti-duplication —
    /// elles ne dépendent d'aucun langage.
    /// </summary>
    private static void ValidateCommonMetadata(ForgeRoot root, PackageSource package, string folderName, List<string> errors)
    {
        var id = package.Id;
        var version = package.Version;
        var description = package.Description;
        var tags = package.Tags;

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

        if (tags.Count < 3)
        {
            errors.Add($"Tags insuffisants ({tags.Count}) : minimum 3, séparés par « ; ».");
        }
    }

    /// <summary>Contrôles propres au projet .NET, sans équivalent ailleurs.</summary>
    private static void ValidateDotNetProject(PackageSource package, List<string> errors)
    {
        var project = XDocument.Load(package.ProjectFile);
        var readmeFile = project.Descendants("PackageReadmeFile").FirstOrDefault()?.Value.Trim();

        if (readmeFile != "README.md")
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

    /// <summary>
    /// Chaque écosystème a sa façon de déclarer un test ; l'exigence, elle, est la
    /// même partout : un micropackage doit prouver son comportement.
    /// </summary>
    private static void ValidateTests(PackageSource package, List<string> errors)
    {
        var markers = package.Language.TestMarkers;
        var hasTests = package.TestFiles
            .Any(f => File.ReadAllText(f) is var code &&
                      markers.Any(m => code.Contains(m, StringComparison.Ordinal)));

        if (!hasTests)
        {
            errors.Add(
                $"tests/ ne contient aucun test reconnaissable en {package.Language.DisplayName} " +
                $"(attendu : {string.Join(" ou ", markers.Select(m => $"« {m} »"))}). " +
                "Chaque micropackage doit prouver son comportement.");
        }
    }

    /// <summary>
    /// Un aléa déclaré doit être prouvé par un test marqué du trait correspondant.
    /// La déclaration sans preuve serait pire que l'absence de déclaration : elle
    /// laisserait croire le cas couvert.
    /// </summary>
    private static void ValidateHazards(ForgeRoot root, PackageSource package, List<string> errors)
    {
        var declared = HazardDeclaration.Read(package.Directory);
        if (declared.Count == 0)
        {
            return;
        }

        // Les fichiers de test sont ceux de l'écosystème du package : ne lire que des
        // .cs rendait la règle inapplicable partout ailleurs.
        var testSources = string.Join('\n', package.TestFiles.Select(File.ReadAllText));

        foreach (var gap in HazardDeclaration.Verify(declared, testSources, HazardCatalogue.Load(root)))
        {
            var reason = gap.Reason.Replace(
                $"[Trait(\"{HazardCatalogue.TraitKey}\", \"{gap.HazardId}\")]",
                package.Language.HazardTraitHint.Replace("<id>", gap.HazardId, StringComparison.Ordinal),
                StringComparison.Ordinal);

            errors.Add($"Aléa « {gap.HazardId} » : {reason}");
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
