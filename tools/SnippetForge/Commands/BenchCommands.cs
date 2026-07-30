using SnippetForge.Bench;

namespace SnippetForge.Commands;

/// <summary>Encadrement mesuré des manches de test avec un agent IA.</summary>
public static class BenchCommands
{
    /// <summary>Dispatch des sous-commandes bench.</summary>
    public static int Bench(ForgeRoot root, string[] args)
    {
        return Cli.Arg(args, 1) switch
        {
            "start" => Start(root, args),
            "report" => Report(root, args),
            "list" => List(root),
            _ => Cli.Fail("Usage : forge bench start <nom> [--project <dossier>] [--prompt \"…\"] | report <nom> | list"),
        };
    }

    private static int Start(ForgeRoot root, string[] args)
    {
        var name = Cli.RequireArg(args, 2, "nom de la manche (ex : manche4)");
        var project = Cli.Option(args, "--project") ?? Directory.GetCurrentDirectory();
        var prompt = Cli.Option(args, "--prompt");

        if (BenchSession.Load(root, name) is not null && !Cli.Flag(args, "--force"))
        {
            return Cli.Fail($"La manche « {name} » existe déjà. Choisir un autre nom, ou --force pour recapturer.");
        }

        var snapshot = BenchSession.Capture(root, name, project, prompt);

        Console.WriteLine($"Manche « {name} » capturée.");
        Console.WriteLine($"  Projet    : {snapshot.ProjectPath}");
        Console.WriteLine($"  Fichiers  : {snapshot.FileHashes.Count} (dont {snapshot.CsLineCounts.Count} .cs, {snapshot.CsLineCounts.Values.Sum()} lignes)");
        Console.WriteLine($"  Feed      : {snapshot.FeedArtifacts.Count} artefact(s)");
        Console.WriteLine();

        if (prompt is not null)
        {
            Console.WriteLine("Prompt à donner à l'agent, tel quel :");
            Console.WriteLine();
            Console.WriteLine($"  {prompt}");
            Console.WriteLine();
        }

        Console.WriteLine("Lancez maintenant l'agent sur ce projet. Quand il a terminé :");
        Console.WriteLine($"  forge bench report {name}");
        return 0;
    }

    private static int Report(ForgeRoot root, string[] args)
    {
        var name = Cli.RequireArg(args, 2, "nom de la manche");
        var snapshot = BenchSession.Load(root, name);
        if (snapshot is null)
        {
            var known = BenchSession.List(root);
            return Cli.Fail($"Manche inconnue : « {name} ». " +
                            (known.Count > 0 ? $"Capturées : {string.Join(", ", known)}." : "Aucune manche capturée."));
        }

        var report = BenchSession.Report(root, snapshot);

        Console.WriteLine($"=== Manche « {report.Name} » — {report.Elapsed.TotalMinutes:0} min ===");
        Console.WriteLine();
        Console.WriteLine("Production :");
        Console.WriteLine($"  {report.AddedFiles.Count} fichier(s) créé(s), {report.ModifiedFiles.Count} modifié(s), " +
                          $"{report.NetCsLines:+0;-0;0} ligne(s) C# nette(s).");
        foreach (var file in report.AddedFiles.Take(15))
        {
            Console.WriteLine($"    + {file}");
        }

        Console.WriteLine();
        Console.WriteLine("Réutilisation :");
        if (report.NewForgeReferences.Count == 0)
        {
            Console.WriteLine("  Aucun micropackage installé pendant la manche.");
        }

        foreach (var reference in report.NewForgeReferences)
        {
            Console.WriteLine($"  + {reference}");
        }

        Console.WriteLine();
        Console.WriteLine("Forge :");
        Console.WriteLine(report.ForgedArtifacts.Count == 0
            ? "  Aucun package forgé pendant la manche."
            : $"  {report.ForgedArtifacts.Count} package(s) publié(s) :");
        foreach (var artifact in report.ForgedArtifacts)
        {
            Console.WriteLine($"  + {artifact}");
        }

        Console.WriteLine();
        Console.WriteLine($"Trace forge ({report.ForgeInvocations.Count} invocation(s) pendant la manche) :");
        foreach (var entry in report.ForgeInvocations)
        {
            var status = entry.ExitCode == 0 ? " " : "!";
            Console.WriteLine($"  {status} forge {entry.Command} {Cli.Truncate(entry.Arguments, 80)}");
        }

        Console.WriteLine();
        Console.WriteLine(report.SearchedBeforeCoding
            ? "Workflow : l'agent a interrogé la bibliothèque pendant la manche."
            : "Workflow : AUCUNE recherche forge détectée — les instructions n'ont probablement pas été lues.");

        return 0;
    }

    private static int List(ForgeRoot root)
    {
        var sessions = BenchSession.List(root);
        if (sessions.Count == 0)
        {
            Console.WriteLine("Aucune manche capturée. Démarrer : forge bench start <nom> --project <dossier>");
            return 0;
        }

        Console.WriteLine($"{sessions.Count} manche(s) :");
        foreach (var name in sessions)
        {
            var snapshot = BenchSession.Load(root, name);
            Console.WriteLine($"  {name} — {snapshot?.ProjectPath} (capturée {snapshot?.StartedUtc:yyyy-MM-dd HH:mm}Z)");
        }

        return 0;
    }
}
