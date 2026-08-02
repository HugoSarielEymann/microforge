using SnippetForge.Consumers;
using SnippetForge.Metrics;
using SnippetForge.Telemetry;

namespace SnippetForge.Commands;

/// <summary>Mesure de la rentabilité de la bibliothèque.</summary>
public static class StatsCommands
{
    /// <summary>
    /// Ce que la bibliothèque a apporté à **un projet**, du point de vue de ce projet.
    /// </summary>
    public static int Report(ForgeRoot root, string[] args)
    {
        var projectPath = Path.GetFullPath(Cli.Arg(args, 1) ?? Directory.GetCurrentDirectory());
        if (!Directory.Exists(projectPath))
        {
            return Cli.Fail($"Projet introuvable : {projectPath}");
        }

        var references = Directory
            .EnumerateFiles(projectPath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(ConsumerProject.ReadForgeReferences)
            .DistinctBy(r => r.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var footprints = FootprintScanner.ScanAll(root).ToDictionary(f => f.PackageId, StringComparer.OrdinalIgnoreCase);
        var invocations = ProjectReportBuilder.InvocationsFrom(UsageLog.ReadSince(root, 0), projectPath);
        var report = ProjectReportBuilder.Build(projectPath, references, footprints, invocations);

        Console.WriteLine($"=== {report.ProjectPath} ===");
        Console.WriteLine();

        if (report.Reused.Count == 0)
        {
            Console.WriteLine("Aucun micropackage référencé. Ce projet n'a rien réutilisé.");
            Console.WriteLine(report.QueriedLibrary
                ? "  La bibliothèque a pourtant été interrogée : elle ne couvrait pas le besoin."
                : "  La bibliothèque n'a jamais été interrogée depuis ce projet — vérifier que");
            if (!report.QueriedLibrary)
            {
                Console.WriteLine("  « forge init . » a bien été lancé et que l'agent lit ses instructions.");
            }

            return 0;
        }

        Console.WriteLine("Réutilisé — code que ce projet n'a pas eu à produire :");
        foreach (var package in report.Reused)
        {
            var lines = package.SourceLines > 0 ? $"{package.SourceLines,5} lignes" : "    ? lignes";
            Console.WriteLine($"  {package.PackageId,-30} {package.Version,-8} {lines}");
        }

        Console.WriteLine();

        if (report.ForgedHere.Count > 0)
        {
            Console.WriteLine("Forgé depuis ce projet — écrit une fois, disponible partout :");
            foreach (var id in report.ForgedHere)
            {
                Console.WriteLine($"  {id}");
            }

            Console.WriteLine();
        }

        Console.WriteLine($"Activité : {report.Searches} recherche(s), {report.Consultations} consultation(s), " +
                          $"{report.Publications} publication(s).");
        if (invocations.Count == 0)
        {
            Console.WriteLine("  (Aucune invocation attribuée : journal antérieur au suivi du dossier courant.)");
        }

        Console.WriteLine();
        Console.WriteLine("=== Bilan, vu de ce projet ===");
        Console.WriteLine($"  Non réécrit : {report.LinesNotWritten,5} lignes  (~{report.TokensNotWritten} tokens)");
        if (report.ForgedLines > 0)
        {
            Console.WriteLine($"  Investi ici : {report.ForgedLines,5} lignes  (~{report.TokensForged} tokens)");
            Console.WriteLine("                amorti dès le prochain projet qui réutilisera ces packages.");
        }

        Console.WriteLine();
        Console.WriteLine("Ce bilan est celui du projet : tout package réutilisé y compte, même si la");
        Console.WriteLine("bibliothèque a dû l'écrire une première fois. Pour la rentabilité globale,");
        Console.WriteLine("qui n'attribue l'économie qu'à partir du deuxième usage : forge stats.");
        return 0;
    }

    /// <summary>Affiche l'investissement, la réutilisation effective et l'économie estimée.</summary>
    public static int Stats(ForgeRoot root, string[] args)
    {
        var footprints = FootprintScanner.ScanAll(root);
        if (footprints.Count == 0)
        {
            Console.WriteLine("Aucun micropackage : rien à mesurer.");
            return 0;
        }

        var registry = ConsumerRegistry.Load(root);
        var pruned = registry.PruneMissing();
        if (pruned > 0)
        {
            registry.Save();
        }

        var external = registry.ExternalTo(root);
        var internalCount = registry.Entries.Count - external.Count;

        var reuse = BuildReuseRecords(external, out var referencing, out var unreadable);
        var report = SavingsCalculator.Estimate(footprints, reuse);

        Console.WriteLine("=== Bibliothèque ===");
        Console.WriteLine($"  {footprints.Count} micropackage(s), {report.InvestmentLines} lignes écrites " +
                          $"(code + tests + mode d'emploi).");
        Console.WriteLine($"  {external.Count} projet(s) raccordé(s), dont {referencing} référence(nt) " +
                          "effectivement au moins un package.");

        if (internalCount > 0)
        {
            Console.WriteLine($"  ({internalCount} projet(s) interne(s) à MicroForge exclu(s) du bilan.)");
        }

        if (pruned > 0)
        {
            Console.WriteLine($"  ({pruned} projet(s) disparu(s), retiré(s) du registre.)");
        }

        foreach (var problem in unreadable)
        {
            Console.WriteLine($"  AVERTISSEMENT : {problem}");
        }

        Console.WriteLine();
        Console.WriteLine("=== Réutilisation ===");
        Console.WriteLine($"  {"Package",-28} {"conso.",6} {"régén. évitées",15} {"lignes évitées",15}");
        foreach (var package in report.Packages)
        {
            Console.WriteLine($"  {package.PackageId,-28} {package.Consumers,6} " +
                              $"{package.RegenerationsAvoided,15} {package.LinesAvoided,15}");
        }

        Console.WriteLine();
        Console.WriteLine("=== Bilan ===");
        Console.WriteLine($"  Investi   : {report.InvestmentLines,6} lignes  (~{report.TokensInvested} tokens)");
        Console.WriteLine($"  Économisé : {report.LinesAvoided,6} lignes  (~{report.TokensAvoided} tokens)");
        Console.WriteLine();

        if (report.IsProfitable)
        {
            var ratio = report.LinesAvoided / (double)Math.Max(1, report.InvestmentLines);
            Console.WriteLine($"  Rentable : {ratio:0.0}× l'investissement récupéré.");
        }
        else
        {
            Console.WriteLine($"  Pas encore rentable : {report.LinesToBreakEven} lignes de réutilisation manquantes.");
            Console.WriteLine("  C'est attendu au début — un package ne rapporte qu'à partir du deuxième");
            Console.WriteLine("  projet qui l'utilise. Le gain croît ensuite sans que la bibliothèque ne coûte plus.");
        }

        Console.WriteLine();
        Console.WriteLine("Méthode : l'économie porte sur N-1 régénérations pour N consommateurs (le 1er");
        Console.WriteLine($"usage ne rapporte rien), et sur les seules lignes de src/. ~{SavingsCalculator.TokensPerLine}");
        Console.WriteLine("tokens/ligne est un ordre de grandeur assumé, pas une mesure de facturation.");
        return 0;
    }

    private static List<ReuseRecord> BuildReuseRecords(
        IReadOnlyList<RegisteredConsumer> consumers,
        out int referencing,
        out List<string> unreadable)
    {
        var byPackage = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        unreadable = [];
        referencing = 0;

        foreach (var consumer in consumers)
        {
            var referencesSomething = false;

            foreach (var projectFile in Directory.EnumerateFiles(consumer.Path, "*.csproj", SearchOption.AllDirectories))
            {
                try
                {
                    foreach (var reference in ConsumerProject.ReadForgeReferences(projectFile))
                    {
                        if (!byPackage.TryGetValue(reference.PackageId, out var list))
                        {
                            list = [];
                            byPackage[reference.PackageId] = list;
                        }

                        list.Add(projectFile);
                        referencesSomething = true;
                    }
                }
                catch (Exception exception) when (exception is IOException or System.Xml.XmlException)
                {
                    unreadable.Add($"{projectFile} illisible : {exception.Message}");
                }
            }

            if (referencesSomething)
            {
                referencing++;
            }
        }

        return byPackage.Select(kv => new ReuseRecord(kv.Key, kv.Value)).ToList();
    }
}
