using SnippetForge.Remote;

namespace SnippetForge.Commands;

/// <summary>Commandes de partage vers un dépôt NuGet d'équipe.</summary>
public static class RemoteCommands
{
    /// <summary>Configure ou affiche le dépôt distant.</summary>
    public static int Remote(ForgeRoot root, string[] args)
    {
        var source = Cli.Option(args, "--source");

        if (source is not null)
        {
            var config = new RemoteFeedConfig(source, Cli.Option(args, "--api-key-var"));
            RemoteFeed.Save(root, config);
            Console.WriteLine($"Dépôt distant enregistré : {config.Source}");
            Console.WriteLine($"Clé d'API lue dans la variable d'environnement : {config.ApiKeyVariable}");
            Console.WriteLine();
            Console.WriteLine("La clé elle-même n'est pas stockée. Définissez-la dans votre session :");
            Console.WriteLine($"  $env:{config.ApiKeyVariable} = \"<votre clé>\"");
            return 0;
        }

        var existing = RemoteFeed.Load(root);
        if (existing is null)
        {
            Console.WriteLine("Aucun dépôt distant configuré. Le feed local reste seul en service.");
            Console.WriteLine();
            Console.WriteLine("Pour partager la bibliothèque avec une équipe :");
            Console.WriteLine("  forge remote --source https://nuget.interne/v3/index.json");
            return 0;
        }

        var key = Environment.GetEnvironmentVariable(existing.ApiKeyVariable);
        Console.WriteLine($"Dépôt distant : {existing.Source}");
        Console.WriteLine($"Variable de clé : {existing.ApiKeyVariable} → {RemoteFeed.MaskApiKey(key)}");
        return 0;
    }

    /// <summary>
    /// Pousse vers le dépôt d'équipe une version déjà publiée localement.
    /// </summary>
    public static int Push(ForgeRoot root, string[] args)
    {
        var config = RemoteFeed.Load(root);
        if (config is null)
        {
            return Cli.Fail("Aucun dépôt distant configuré. Lancez « forge remote --source <url> ».");
        }

        var id = Cli.RequireArg(args, 1, "PackageId");
        var index = FeedIndexer.Load(root);
        var entry = index.Packages.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Cli.Fail($"Package inconnu dans le feed local : {id}");
        }

        var version = Cli.Option(args, "--version") ?? entry.LatestVersion;
        if (!entry.AllVersions.Contains(version, StringComparer.OrdinalIgnoreCase))
        {
            return Cli.Fail($"Version {version} absente du feed local. Disponibles : {string.Join(", ", entry.AllVersions)}.");
        }

        var nupkg = FeedIndexer.PackagePath(root, entry.Id, version);
        if (!File.Exists(nupkg))
        {
            return Cli.Fail($"Artefact introuvable : {nupkg}");
        }

        var apiKey = Environment.GetEnvironmentVariable(config.ApiKeyVariable);
        Console.WriteLine($"Envoi de {entry.Id} {version} vers {config.Source}");
        Console.WriteLine($"  clé d'API ({config.ApiKeyVariable}) : {RemoteFeed.MaskApiKey(apiKey)}");

        var arguments = RemoteFeed.BuildPushArguments(nupkg, config, apiKey);
        if (!Cli.Run("dotnet", arguments, root.Path, out var output))
        {
            // La sortie peut contenir la clé si le serveur la renvoie : on la masque.
            var safe = apiKey is null ? output : output.Replace(apiKey, RemoteFeed.MaskApiKey(apiKey), StringComparison.Ordinal);
            return Cli.Fail($"Échec de dotnet nuget push :\n{Cli.Tail(safe, 20)}");
        }

        Console.WriteLine("  Envoyé. L'artefact local reste la référence : le dépôt distant en est une copie.");
        return 0;
    }
}
