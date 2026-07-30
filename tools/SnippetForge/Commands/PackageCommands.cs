using SnippetForge.Api;
using SnippetForge.Duplicates;
using SnippetForge.Embeddings;
using SnippetForge.Integrity;

namespace SnippetForge.Commands;

/// <summary>Commandes du cycle de vie d'un micropackage : création, validation, publication, versionnage.</summary>
public static class PackageCommands
{
    /// <summary>Scaffolde un micropackage conforme.</summary>
    public static int New(ForgeRoot root, string[] args)
    {
        var id = Cli.RequireArg(args, 1, "PackageId (ex : Micro.Text.Slugify)");
        var description = Cli.RequireOption(args, "--description", "elle alimente la recherche (≥ 30 caractères).");
        var tags = Cli.SplitList(Cli.RequireOption(args, "--tags", "minimum 3 tags séparés par « ; »."));

        var directory = Scaffolder.Scaffold(root, id, description, tags);
        Console.WriteLine($"Micropackage scaffoldé : {directory}");
        Console.WriteLine("Étapes suivantes : implémenter src/, écrire les tests, compléter README.md,");
        Console.WriteLine($"puis « forge publish {id} ».");
        return 0;
    }

    /// <summary>Vérifie les règles immuables.</summary>
    public static int Validate(ForgeRoot root, string[] args)
    {
        var package = PackageSource.Resolve(root, Cli.RequireArg(args, 1, "chemin ou PackageId"));
        Console.WriteLine($"Validation de {package.Directory} selon RULES.md :");

        var errors = Validator.Validate(root, package.Directory, Cli.Flag(args, "--skip-tests"));
        if (errors.Count == 0)
        {
            Console.WriteLine("  CONFORME : structure, métadonnées, API, README et tests valides.");
            return 0;
        }

        foreach (var error in errors)
        {
            Console.WriteLine($"  ERREUR : {error}");
        }

        return 1;
    }

    /// <summary>Incrémente la version déclarée d'un package.</summary>
    public static int Bump(ForgeRoot root, string[] args)
    {
        var package = PackageSource.Resolve(root, Cli.RequireArg(args, 1, "chemin ou PackageId"));
        var levelText = Cli.RequireArg(args, 2, "niveau (major | minor | patch)");

        var level = levelText.ToLowerInvariant() switch
        {
            "major" => BumpLevel.Major,
            "minor" => BumpLevel.Minor,
            "patch" => BumpLevel.Patch,
            _ => throw new ArgumentException($"Niveau inconnu : « {levelText} » (major | minor | patch)."),
        };

        var current = package.Version;
        var next = SemVerLite.Bump(current, level);
        package.SetVersion(next);

        Console.WriteLine($"{package.Id} : {current} → {next}");
        Console.WriteLine("Le contrat public sera vérifié à la publication (forge publish).");
        return 0;
    }

    /// <summary>
    /// Valide, teste, empaquette, vérifie la compatibilité de contrat et l'absence de
    /// quasi-duplication, puis publie dans le feed.
    /// </summary>
    public static async Task<int> PublishAsync(ForgeRoot root, string[] args)
    {
        var package = PackageSource.Resolve(root, Cli.RequireArg(args, 1, "chemin ou PackageId"));
        Console.WriteLine($"Publication de {package.Id} {package.Version} :");

        var errors = Validator.Validate(root, package.Directory, skipTests: false);
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.WriteLine($"  ERREUR : {error}");
            }

            return Cli.Fail("Publication refusée : le package viole RULES.md.");
        }

        Console.WriteLine("  Validation conforme. Packaging (dotnet pack)...");
        var outDir = Path.Combine(root.ArtifactsDir, package.Id);
        if (Directory.Exists(outDir))
        {
            Directory.Delete(outDir, recursive: true);
        }

        if (!Cli.Run("dotnet", BuildCommands.PackArguments(outDir), package.Directory, out var packOutput))
        {
            return Cli.Fail($"Échec de dotnet pack :\n{Cli.Tail(packOutput, 20)}");
        }

        var nupkg = Directory.EnumerateFiles(outDir, "*.nupkg").Single();
        var index = FeedIndexer.Load(root);

        var compatibility = CheckContract(root, index, package, nupkg, out var candidateSurface);
        if (compatibility != 0)
        {
            return compatibility;
        }

        var duplication = await CheckDuplicationAsync(root, index, package, candidateSurface, args).ConfigureAwait(false);
        if (duplication != 0)
        {
            return duplication;
        }

        var published = Path.Combine(root.FeedDir, Path.GetFileName(nupkg));
        File.Copy(nupkg, published, overwrite: false);
        new ApiSurfaceStore(root).Save(candidateSurface);

        // L'empreinte est enregistrée sans intervention : « forge verify » saura plus
        // tard si cet artefact est bien celui qui a franchi la validation.
        var ledger = ArtifactLedger.Load(root);
        var hash = ledger.Record(published);
        ledger.Save();

        var refreshed = FeedIndexer.Rebuild(root);
        var semantic = await SemanticIndex.BuildAsync(root, refreshed, Cli.Flag(args, "--offline")).ConfigureAwait(false);
        semantic.Dispose();

        Console.WriteLine($"  Publié : {Path.GetFileName(nupkg)} (contrat {candidateSurface.Digest})");
        Console.WriteLine($"  Empreinte : sha256:{hash[..16]}…");
        Console.WriteLine("  Index, surfaces d'API et vecteurs régénérés.");
        return 0;
    }

    /// <summary>
    /// Compare le contrat public du candidat à celui de son prédécesseur immédiat et
    /// exige l'incrément SemVer correspondant.
    /// </summary>
    private static int CheckContract(ForgeRoot root, IndexDocument index, PackageSource package, string nupkg, out ApiSurface candidateSurface)
    {
        candidateSurface = ApiSurfaceExtractor.FromPackage(nupkg, package.Id, package.Version);

        var entry = index.Packages.FirstOrDefault(p => p.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase));
        var predecessor = entry?.AllVersions
            .Where(v => SemVerLite.Compare(v, package.Version) < 0)
            .OrderByDescending(v => v, Comparer<string>.Create(SemVerLite.Compare))
            .FirstOrDefault();

        if (predecessor is null)
        {
            Console.WriteLine($"  Première version : contrat {candidateSurface.Digest} ({candidateSurface.Members.Count} membres publics).");
            return 0;
        }

        var previousSurface = new ApiSurfaceStore(root).Load(package.Id, predecessor);
        if (previousSurface is null)
        {
            Console.WriteLine($"  Contrat de {predecessor} non extrait : vérification ignorée (lancer « forge index »).");
            return 0;
        }

        var diff = ApiDiff.Between(previousSurface, candidateSurface);
        var actual = SemVerLite.BumpBetween(predecessor, package.Version);

        if (actual is null)
        {
            return Cli.Fail($"La version {package.Version} n'est pas supérieure à {predecessor}.");
        }

        Console.WriteLine($"  Contrat vs {predecessor} : +{diff.Added.Count} / -{diff.Removed.Count} membre(s) → {diff.RequiredBump} exigé, {actual} appliqué.");

        if (actual < diff.RequiredBump)
        {
            foreach (var removed in diff.Removed.Take(10))
            {
                Console.WriteLine($"    - {removed}");
            }

            foreach (var added in diff.Added.Take(10))
            {
                Console.WriteLine($"    + {added}");
            }

            return Cli.Fail(
                $"Incrément insuffisant : le contrat exige un incrément {diff.RequiredBump}. " +
                $"Corriger avec « forge bump {package.Id} {diff.RequiredBump.ToString().ToLowerInvariant()} », " +
                "ou rétablir les membres retirés pour rester rétrocompatible.");
        }

        if (diff.IsIdentical)
        {
            Console.WriteLine("    Contrat identique : les consommateurs peuvent monter sans recompiler leur code.");
        }

        return 0;
    }

    /// <summary>
    /// Refuse la publication d'un package quasi identique à un package existant.
    /// Deux signaux indépendants : la description (vectorielle) et la forme du contrat
    /// public. Le second rattrape une description volontairement reformulée.
    /// </summary>
    private static async Task<int> CheckDuplicationAsync(
        ForgeRoot root,
        IndexDocument index,
        PackageSource package,
        ApiSurface candidateSurface,
        string[] args)
    {
        if (index.Packages.Count == 0)
        {
            return 0;
        }

        var semantic = await SemanticIndex.BuildAsync(root, index, Cli.Flag(args, "--offline")).ConfigureAwait(false);
        try
        {
            var text = DuplicateDetector.DescribeForEmbedding(package.Id, package.Tags, package.Description, package.Readme);
            var vector = await semantic.EmbedAsync(text).ConfigureAwait(false);
            var candidates = DuplicateDetector.Detect(
                package.Id, vector, semantic.Vectors, semantic.Versions, semantic.Thresholds);

            if (candidates.Count == 0)
            {
                Console.WriteLine($"  Duplication : aucune proximité suspecte (seuil d'alerte {semantic.Thresholds.Warning:0.00}).");
                return 0;
            }

            var surfaces = new ApiSurfaceStore(root);
            candidates = DuplicateDetector.Escalate(candidates, otherId =>
            {
                var version = semantic.Versions.GetValueOrDefault(otherId);
                var other = version is null ? null : surfaces.Load(otherId, version);
                return other is null ? null : ApiSimilarity.Compare(candidateSurface, other);
            });

            foreach (var candidate in candidates)
            {
                var label = candidate.Severity == DuplicateSeverity.Blocking ? "QUASI-DOUBLON" : "proximité";
                var api = candidate.ApiSimilarity is { } value ? $", contrat {value:0.000}" : string.Empty;
                Console.WriteLine($"  [{label}] {candidate.PackageId} {candidate.Version} : description {candidate.Similarity:0.000}{api}");
            }

            var blocking = candidates.Where(c => c.Severity == DuplicateSeverity.Blocking).ToList();
            if (blocking.Count > 0 && !Cli.Flag(args, "--allow-similar"))
            {
                var first = blocking[0];
                var reason = first.ApiSimilarity >= DuplicateDetector.ApiEscalationThreshold
                    ? $"description {first.Similarity:0.000} et contrat public {first.ApiSimilarity:0.000}"
                    : $"similarité {first.Similarity:0.000}";

                return Cli.Fail(
                    $"Publication refusée : {first.PackageId} couvre déjà ce besoin ({reason}). " +
                    "Réutiliser ou étendre ce package. Si la distinction est réelle, la rendre " +
                    "explicite dans la description et le contrat, ou publier avec --allow-similar " +
                    "en dernier recours.");
            }

            return 0;
        }
        finally
        {
            semantic.Dispose();
        }
    }
}
