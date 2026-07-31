using SnippetForge.Hazards;

namespace SnippetForge.Commands;

/// <summary>Consultation et enrichissement du catalogue d'aléas de test.</summary>
public static class HazardCommands
{
    /// <summary>Dispatch des sous-commandes.</summary>
    public static int Hazards(ForgeRoot root, string[] args)
    {
        return Cli.Arg(args, 1) switch
        {
            null or "list" => List(root, args),
            "add" => Add(root, args),
            "declare" => Declare(root, args),
            _ => Cli.Fail("Usage : forge hazards [list] | add <id> --description \"…\" --rationale \"…\" " +
                          "[--examples \"a;b\"] | declare <PackageId> --hazards \"a;b\""),
        };
    }

    private static int List(ForgeRoot root, string[] args)
    {
        var catalogue = HazardCatalogue.Load(root);
        var detailed = Cli.Flag(args, "--detail");

        Console.WriteLine($"{catalogue.All.Count} aléa(s) au catalogue :\n");

        foreach (var hazard in catalogue.All)
        {
            Console.WriteLine($"  {hazard.Id}");
            Console.WriteLine($"    {hazard.Description}");

            if (detailed)
            {
                Console.WriteLine($"    pourquoi : {hazard.Rationale}");
                if (hazard.Examples.Count > 0)
                {
                    Console.WriteLine($"    à éprouver : {string.Join(" · ", hazard.Examples)}");
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine("Un package déclare ses aléas dans hazards.json, et les prouve par des tests");
        Console.WriteLine($"portant [Trait(\"{HazardCatalogue.TraitKey}\", \"<id>\")].");
        Console.WriteLine("Enrichir le catalogue : forge hazards add <id> --description \"…\" --rationale \"…\"");
        return 0;
    }

    /// <summary>
    /// Consigne un aléa découvert. C'est le geste qui rend le système cumulatif :
    /// ce qu'un agent a compris une fois n'est plus à redécouvrir.
    /// </summary>
    private static int Add(ForgeRoot root, string[] args)
    {
        var id = Cli.RequireArg(args, 2, "identifiant de l'aléa (ex : numeric-overflow)");
        var description = Cli.RequireOption(args, "--description", "ce que l'aléa désigne, en une phrase.");
        var rationale = Cli.RequireOption(args, "--rationale", "le mode de défaillance que le test évite.");
        var examples = Cli.SplitList(Cli.Option(args, "--examples"));

        var catalogue = HazardCatalogue.Load(root);
        var existing = catalogue.Find(id);

        catalogue.AddOrReplace(root, new Hazard(id, description, rationale, examples));

        Console.WriteLine(existing is null
            ? $"Aléa « {id} » ajouté au catalogue."
            : $"Aléa « {id} » redéfini (il existait déjà).");
        Console.WriteLine($"Fichier : {Path.Combine(root.Path, HazardCatalogue.FileName)}");
        Console.WriteLine();
        Console.WriteLine("Ce catalogue est versionné avec le dépôt : l'acquis est collectif.");
        Console.WriteLine($"Tout package qui déclare « {id} » devra désormais le prouver par un test marqué.");
        return 0;
    }

    /// <summary>Déclare les aléas d'un package.</summary>
    private static int Declare(ForgeRoot root, string[] args)
    {
        var package = PackageSource.Resolve(root, Cli.RequireArg(args, 2, "PackageId"));
        var hazards = Cli.SplitList(Cli.RequireOption(args, "--hazards", "liste séparée par « ; »."));

        var catalogue = HazardCatalogue.Load(root);
        var unknown = hazards.Where(h => !catalogue.Contains(h)).ToList();
        if (unknown.Count > 0)
        {
            return Cli.Fail(
                $"Aléa(s) inconnu(s) : {string.Join(", ", unknown)}. " +
                "Consulter « forge hazards list », ou en ajouter un avec « forge hazards add ».");
        }

        HazardDeclaration.Write(package.Directory, hazards);

        Console.WriteLine($"{package.Id} déclare {hazards.Length} aléa(s) : {string.Join(", ", hazards)}");
        Console.WriteLine();
        Console.WriteLine("Chacun doit être prouvé par au moins un test :");
        foreach (var hazard in hazards)
        {
            Console.WriteLine($"  [Trait(\"{HazardCatalogue.TraitKey}\", \"{hazard}\")]");
        }

        Console.WriteLine();
        Console.WriteLine("Sans quoi « forge validate » refusera la publication.");
        return 0;
    }
}
