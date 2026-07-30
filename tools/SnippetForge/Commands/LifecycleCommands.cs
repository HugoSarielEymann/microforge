using SnippetForge.Lifecycle;

namespace SnippetForge.Commands;

/// <summary>Commandes de cycle de vie : dépréciation et réhabilitation.</summary>
public static class LifecycleCommands
{
    /// <summary>Déclare qu'un package (ou certaines de ses versions) ne doit plus être utilisé.</summary>
    public static int Deprecate(ForgeRoot root, string[] args)
    {
        var id = Cli.RequireArg(args, 1, "PackageId");
        var reason = Cli.RequireOption(args, "--reason", "expliquer pourquoi ce package ne doit plus être utilisé.");
        var versionSpec = Cli.Option(args, "--versions") ?? "*";
        var replacement = Cli.Option(args, "--replacement");

        var index = FeedIndexer.Load(root);
        var entry = index.Packages.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Cli.Fail($"Package inconnu dans le feed : {id}");
        }

        if (replacement is not null &&
            !index.Packages.Any(p => p.Id.Equals(replacement, StringComparison.OrdinalIgnoreCase)))
        {
            return Cli.Fail($"Remplacement inconnu dans le feed : {replacement}");
        }

        var registry = DeprecationRegistry.Load(root);
        registry.Deprecate(entry.Id, versionSpec, reason, replacement);
        registry.Save();

        var affected = entry.AllVersions.Where(v => DeprecationRegistry.Matches(versionSpec, v)).ToList();
        Console.WriteLine($"{entry.Id} : dépréciation enregistrée sur « {versionSpec} » ({affected.Count} version(s) concernée(s)).");
        Console.WriteLine($"  raison : {reason}");
        if (replacement is not null)
        {
            Console.WriteLine($"  remplacement : {replacement}");
        }

        Console.WriteLine();
        Console.WriteLine("Les artefacts publiés restent intacts et installables : aucun build existant n'est cassé.");
        Console.WriteLine("Les consommateurs verront l'alerte via « forge outdated <projet> ».");
        return 0;
    }

    /// <summary>Retire une dépréciation déclarée par erreur.</summary>
    public static int Undeprecate(ForgeRoot root, string[] args)
    {
        var id = Cli.RequireArg(args, 1, "PackageId");
        var versionSpec = Cli.Option(args, "--versions");

        var registry = DeprecationRegistry.Load(root);
        var removed = registry.Undeprecate(id, versionSpec);
        registry.Save();

        Console.WriteLine(removed == 0
            ? $"Aucune dépréciation à retirer pour {id}."
            : $"{removed} dépréciation(s) retirée(s) pour {id}.");
        return 0;
    }

    /// <summary>Liste toutes les dépréciations déclarées.</summary>
    public static int ListDeprecations(ForgeRoot root)
    {
        var registry = DeprecationRegistry.Load(root);
        if (registry.Entries.Count == 0)
        {
            Console.WriteLine("Aucune dépréciation déclarée.");
            return 0;
        }

        Console.WriteLine($"{registry.Entries.Count} dépréciation(s) :");
        foreach (var entry in registry.Entries.OrderBy(e => e.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {entry.PackageId} « {entry.VersionSpec} » (depuis {entry.DeclaredUtc:yyyy-MM-dd})");
            Console.WriteLine($"    {entry.Reason}");
            if (entry.Replacement is { } replacement)
            {
                Console.WriteLine($"    → remplacement : {replacement}");
            }
        }

        return 0;
    }
}
