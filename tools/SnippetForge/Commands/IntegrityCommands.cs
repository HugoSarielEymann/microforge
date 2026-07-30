using SnippetForge.Integrity;

namespace SnippetForge.Commands;

/// <summary>Contrôle d'intégrité du feed.</summary>
public static class IntegrityCommands
{
    /// <summary>
    /// Vérifie que chaque artefact du feed correspond à son empreinte enregistrée.
    /// Retourne 1 si un écart est constaté.
    /// </summary>
    public static int Verify(ForgeRoot root, string[] args)
    {
        var ledger = ArtifactLedger.Load(root);

        if (Cli.Flag(args, "--adopt"))
        {
            var adopted = ledger.AdoptExisting(root);
            ledger.Save();
            Console.WriteLine(adopted == 0
                ? "Aucun artefact à adopter : tous sont déjà enregistrés."
                : $"{adopted} artefact(s) adopté(s). Leur empreinte est désormais la référence.");
            Console.WriteLine("Note : l'adoption fait confiance à l'état actuel du feed. À n'utiliser");
            Console.WriteLine("qu'une fois, sur un feed dont vous répondez.");
            return 0;
        }

        var findings = ledger.Verify(root);
        var total = ledger.Hashes.Count;

        if (findings.Count == 0)
        {
            Console.WriteLine($"Intégrité vérifiée : {total} artefact(s) conformes à leur empreinte.");
            return 0;
        }

        Console.WriteLine($"{findings.Count} écart(s) sur {total} artefact(s) enregistré(s) :\n");
        foreach (var finding in findings)
        {
            Console.WriteLine($"  {ArtifactLedger.Describe(finding)}");
        }

        Console.WriteLine();
        if (findings.Any(f => f.Issue == IntegrityIssue.Unregistered))
        {
            Console.WriteLine("Artefacts non enregistrés : republier proprement via « forge publish »,");
            Console.WriteLine("ou, si vous répondez de leur origine, « forge verify --adopt ».");
        }

        if (findings.Any(f => f.Issue is IntegrityIssue.Tampered or IntegrityIssue.Missing))
        {
            Console.WriteLine("Modification ou suppression d'une version publiée : restaurer le feed");
            Console.WriteLine("depuis votre sauvegarde. Les versions publiées sont immuables (R8).");
        }

        return 1;
    }
}
