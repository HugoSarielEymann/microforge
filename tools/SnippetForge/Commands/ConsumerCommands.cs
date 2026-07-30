using SnippetForge.Api;
using SnippetForge.Consumers;
using SnippetForge.Lifecycle;
using SnippetForge.Metrics;

namespace SnippetForge.Commands;

/// <summary>
/// Commandes destinées aux projets qui consomment les micropackages : diagnostic des
/// versions et mise à jour contrôlée par la suite de tests du consommateur.
/// </summary>
public static class ConsumerCommands
{
    /// <summary>
    /// Raccorde un projet existant à la bibliothèque : source NuGet et instructions agent.
    /// Idempotent — relancer la commande ne duplique rien.
    /// </summary>
    public static int Init(ForgeRoot root, string[] args)
    {
        var target = Path.GetFullPath(Cli.Arg(args, 1) ?? Directory.GetCurrentDirectory());
        if (!Directory.Exists(target))
        {
            return Cli.Fail($"Dossier de projet introuvable : {target}");
        }

        var withNuGetConfig = !Cli.Flag(args, "--no-nuget-config");
        var withInstructions = !Cli.Flag(args, "--no-agent-instructions");

        // Raccorder un dossier parent déposerait des instructions valables pour des
        // dizaines de dépôts sans rapport, et fausserait le décompte des consommateurs.
        if (withNuGetConfig && !Cli.Flag(args, "--force"))
        {
            var shape = ProjectShapeAnalyzer.Analyze(target);
            if (shape.Warning is { } warning)
            {
                return Cli.Fail(
                    $"Cible suspecte : {warning}\n" +
                    "Raccordez chaque projet individuellement (cd <projet> puis forge init .), " +
                    "ou passez --force si c'est bien voulu.");
            }
        }

        if (!withNuGetConfig && !withInstructions)
        {
            return Cli.Fail("Les deux volets sont désactivés : il n'y a rien à faire.");
        }

        Console.WriteLine($"Raccordement de {target} à MicroForge ({root.Path}) :\n");

        if (withNuGetConfig)
        {
            var configPath = Path.Combine(target, "nuget.config");
            var sourceOutcome = NuGetConfigWriter.EnsureSource(configPath, root.FeedDir);
            Console.WriteLine($"  nuget.config  [{Describe(sourceOutcome)}]  {configPath}");
            Console.WriteLine($"                source « {NuGetConfigWriter.SourceName} » → {root.FeedDir}");
        }

        if (withInstructions)
        {
            // --agents restreint aux outils voulus : écrire un fichier Copilot dans un
            // dossier que seul Claude Code lit n'apporte rien et encombre.
            var filter = Cli.Option(args, "--agents");
            var targets = AgentInstructionsWriter.SelectTargets(filter);

            if (targets.Count == 0)
            {
                return Cli.Fail($"Aucun agent ne correspond à « {filter} ». " +
                                $"Connus : {string.Join(", ", AgentInstructionsWriter.ProjectTargets.Select(t => t.Agent))}.");
            }

            foreach (var (relativePath, agent) in targets)
            {
                var path = Path.Combine(target, relativePath);
                var outcome = AgentInstructionsWriter.Ensure(path, root.Path);
                Console.WriteLine($"  {relativePath,-32} [{Describe(outcome)}]  ({agent})");
            }

            Console.WriteLine("                instructions complètes, autonomes, délimitées par des marqueurs");
        }

        if (withNuGetConfig)
        {
            var index = FeedIndexer.Load(root);
            Console.WriteLine();
            Console.WriteLine($"{index.Packages.Count} micropackage(s) désormais installable(s) depuis ce projet :");
            foreach (var entry in index.Packages)
            {
                Console.WriteLine($"  dotnet add package {entry.Id} --version {entry.LatestVersion}");
            }
        }

        // Le projet est mémorisé pour que « forge stats » sache où mesurer la réutilisation.
        // Sans source NuGet, le dossier ne consommera jamais de package : l'enregistrer
        // gonflerait le dénominateur du bilan (cas de ~/.claude, qui n'est pas un projet).
        if (withNuGetConfig)
        {
            var registry = ConsumerRegistry.Load(root);
            if (registry.Register(target))
            {
                registry.Save();
                Console.WriteLine();
                Console.WriteLine("Projet enregistré pour les métriques (forge stats).");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Chercher avant de coder : forge search \"<besoin>\"");
        return 0;
    }

    private static string Describe(NuGetSourceOutcome outcome) => outcome switch
    {
        NuGetSourceOutcome.Created => "créé       ",
        NuGetSourceOutcome.Added => "complété   ",
        NuGetSourceOutcome.Updated => "corrigé    ",
        _ => "déjà à jour",
    };

    private static string Describe(InstructionsOutcome outcome) => outcome switch
    {
        InstructionsOutcome.Created => "créé       ",
        InstructionsOutcome.Appended => "complété   ",
        InstructionsOutcome.Replaced => "actualisé  ",
        _ => "déjà à jour",
    };

    /// <summary>Diagnostique les références MicroForge d'un projet face au feed.</summary>
    public static int Outdated(ForgeRoot root, string[] args)
    {
        var projectFile = ConsumerProject.ResolveProjectFile(Cli.RequireArg(args, 1, "chemin du projet ou du .csproj"));
        var plan = BuildPlan(root, projectFile, out var references);

        Console.WriteLine($"Projet : {projectFile}");
        if (references.Count == 0)
        {
            Console.WriteLine("Aucune référence Micro.* : rien à diagnostiquer.");
            return 0;
        }

        Console.WriteLine($"{references.Count} référence(s) MicroForge :\n");
        foreach (var item in plan)
        {
            var marker = item.Classification switch
            {
                UpdateClassification.Safe => "SÛRE",
                UpdateClassification.Review => "RELECTURE",
                UpdateClassification.Unavailable => "INTROUVABLE",
                _ => "À JOUR",
            };

            var target = item.SafeTarget ?? item.LatestTarget;
            var arrow = target is null ? string.Empty : $" → {target}";
            Console.WriteLine($"  [{marker,-11}] {item.PackageId} {item.CurrentVersion}{arrow}");
            Console.WriteLine($"                {item.Rationale}");
            if (item.CurrentIsDeprecated)
            {
                Console.WriteLine("                La version installée est dépréciée.");
            }
        }

        var safe = plan.Count(i => i.Classification == UpdateClassification.Safe);
        var review = plan.Count(i => i.Classification == UpdateClassification.Review);

        Console.WriteLine();
        if (safe > 0)
        {
            Console.WriteLine($"{safe} mise(s) à jour sans rupture de contrat. Appliquer et valider par vos tests :");
            Console.WriteLine($"  forge update \"{projectFile}\" --safe-only --test \"dotnet test\"");
        }

        if (review > 0)
        {
            Console.WriteLine($"{review} référence(s) exigent une relecture humaine (rupture de contrat ou dépréciation).");
            Console.WriteLine("  Inspecter le détail : forge diff <PackageId> <version actuelle> <version cible>");
        }

        if (safe == 0 && review == 0)
        {
            Console.WriteLine("Tout est à jour.");
        }

        return 0;
    }

    /// <summary>
    /// Applique les montées de version puis délègue la validation à la suite de tests
    /// du consommateur. En cas d'échec, le projet est restauré à l'identique.
    /// </summary>
    public static int Update(ForgeRoot root, string[] args)
    {
        var projectFile = ConsumerProject.ResolveProjectFile(Cli.RequireArg(args, 1, "chemin du projet ou du .csproj"));
        var safeOnly = Cli.Flag(args, "--safe-only");
        var testCommand = Cli.Option(args, "--test");

        var plan = BuildPlan(root, projectFile, out _);

        var updates = plan
            .Where(i => i.Classification == UpdateClassification.Safe ||
                        (!safeOnly && i.Classification == UpdateClassification.Review && i.LatestTarget is not null))
            .ToDictionary(
                i => i.PackageId,
                i => (safeOnly ? i.SafeTarget : i.SafeTarget ?? i.LatestTarget)!,
                StringComparer.Ordinal);

        if (updates.Count == 0)
        {
            Console.WriteLine("Aucune mise à jour applicable.");
            return 0;
        }

        foreach (var (id, version) in updates)
        {
            var current = plan.First(i => i.PackageId == id).CurrentVersion;
            Console.WriteLine($"  {id} {current} → {version}");
        }

        var backup = File.ReadAllText(projectFile);
        var changed = ConsumerProject.ApplyVersions(projectFile, updates);
        Console.WriteLine($"{changed} référence(s) mise(s) à jour dans le projet.");

        if (testCommand is null)
        {
            Console.WriteLine();
            Console.WriteLine("Aucune commande de test fournie (--test) : lancez votre suite pour valider.");
            Console.WriteLine("En cas de régression, restaurez le .csproj depuis votre gestionnaire de versions.");
            return 0;
        }

        var workingDir = Path.GetDirectoryName(projectFile)!;
        Console.WriteLine($"\nValidation par votre suite de tests : « {testCommand} »...");

        if (Cli.RunShell(testCommand, workingDir, out var output))
        {
            Console.WriteLine("Tests réussis : les montées de version sont conservées.");
            return 0;
        }

        File.WriteAllText(projectFile, backup);
        Console.WriteLine();
        Console.WriteLine(Cli.Tail(output, 30));
        Console.WriteLine();
        return Cli.Fail(
            "Tests en échec : le projet a été restauré dans son état initial. " +
            "Les versions publiées étant immuables, votre build précédent reste reproductible.");
    }

    private static List<UpdatePlanItem> BuildPlan(ForgeRoot root, string projectFile, out IReadOnlyList<PinnedReference> references)
    {
        references = ConsumerProject.ReadForgeReferences(projectFile);

        var index = FeedIndexer.Load(root);
        var deprecations = DeprecationRegistry.Load(root);
        var surfaces = new ApiSurfaceStore(root);

        return references.Select(reference =>
        {
            var entry = index.Packages.FirstOrDefault(p =>
                p.Id.Equals(reference.PackageId, StringComparison.OrdinalIgnoreCase));
            var versions = entry?.AllVersions ?? [];

            return UpdatePlanner.Plan(
                reference,
                versions,
                version => deprecations.For(reference.PackageId, version).Count > 0,
                (from, to) =>
                {
                    var fromSurface = surfaces.Load(reference.PackageId, from);
                    var toSurface = surfaces.Load(reference.PackageId, to);

                    // Contrat inconnu : on ne peut pas prouver l'innocuité, on suppose la rupture.
                    return fromSurface is null || toSurface is null || ApiDiff.Between(fromSurface, toSurface).IsBreaking;
                });
        }).ToList();
    }
}
