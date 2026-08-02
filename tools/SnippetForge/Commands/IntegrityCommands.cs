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
        var signature = LedgerSignature.Verify(root, ledger);

        if (findings.Count == 0 && signature is SignatureState.Valid or SignatureState.NotConfigured)
        {
            Console.WriteLine($"Intégrité vérifiée : {total} artefact(s) conformes à leur empreinte.");
            Console.WriteLine($"  {LedgerSignature.Describe(signature)}");
            return 0;
        }

        if (signature is SignatureState.Invalid or SignatureState.Missing)
        {
            Console.WriteLine($"SIGNATURE : {LedgerSignature.Describe(signature)}");
            Console.WriteLine();
        }

        if (findings.Count == 0)
        {
            Console.WriteLine($"Empreintes conformes ({total} artefact(s)), mais la signature pose problème.");
            return 1;
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

    /// <summary>Crée une paire de clés, ou signe le registre d'empreintes.</summary>
    public static int Sign(ForgeRoot root, string[] args)
    {
        var init = Cli.Option(args, "--init");

        if (init is not null)
        {
            var written = LedgerSignature.CreateKeyPair(root, init, Cli.Flag(args, "--force"));

            Console.WriteLine($"Clé privée écrite : {written}");
            Console.WriteLine($"Clé publique     : {LedgerSignature.PublicKeyPath(root)}");
            Console.WriteLine();
            Console.WriteLine("La clé publique se versionne avec la bibliothèque ; la clé privée, JAMAIS.");
            Console.WriteLine("Protégez-la comme un secret et sauvegardez-la : la perdre oblige à");
            Console.WriteLine("resigner avec une nouvelle paire.");
            Console.WriteLine();
            Console.WriteLine("Pour signer :");
            Console.WriteLine($"  $env:{LedgerSignature.PrivateKeyVariable} = \"{written}\"");
            Console.WriteLine("  forge sign");
            return 0;
        }

        var ledger = ArtifactLedger.Load(root);
        if (ledger.Hashes.Count == 0)
        {
            return Cli.Fail("Aucune empreinte à signer. Publier au moins un package, ou « forge verify --adopt ».");
        }

        LedgerSignature.Sign(root, ledger);

        Console.WriteLine($"Registre signé : {ledger.Hashes.Count} empreinte(s).");
        Console.WriteLine($"  {LedgerSignature.SignaturePath(root)}");
        Console.WriteLine();
        Console.WriteLine("À resigner après chaque publication : « forge verify » signalera l'écart.");
        return 0;
    }
}
