using SnippetForge.Consumers;
using SnippetForge.Metrics;

namespace SnippetForge.Commands;

/// <summary>Mesure de la rentabilité de la bibliothèque.</summary>
public static class StatsCommands
{
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
