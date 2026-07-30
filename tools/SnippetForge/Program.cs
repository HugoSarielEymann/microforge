using System.Text;
using SnippetForge.Commands;

namespace SnippetForge;

/// <summary>Point d'entrée du CLI SnippetForge : forge de micropackages MicroForge.</summary>
public static class Program
{
    /// <summary>Dispatch des commandes.</summary>
    public static async Task<int> Main(string[] args)
    {
        EnableUnicodeOutput();

        try
        {
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            {
                PrintHelp();
                return 0;
            }

            if (args[0] is "--version" or "version")
            {
                Console.WriteLine(ForgeVersion.Banner);
                return 0;
            }

            // « use » doit fonctionner avant toute résolution : c'est la commande qui
            // enseigne à l'outil où se trouve la racine.
            if (args[0] == "use")
            {
                return Use(args);
            }

            var root = ForgeRoot.Locate();
            return args[0] switch
            {
                "search" => await QueryCommands.SearchAsync(root, args).ConfigureAwait(false),
                "info" => QueryCommands.Info(root, args),
                "list" => QueryCommands.List(root),
                "index" => await QueryCommands.IndexAsync(root, args).ConfigureAwait(false),
                "diff" => QueryCommands.Diff(root, args),
                "duplicates" or "dup" => await QueryCommands.DuplicatesAsync(root, args).ConfigureAwait(false),

                "new" => PackageCommands.New(root, args),
                "validate" => PackageCommands.Validate(root, args),
                "bump" => PackageCommands.Bump(root, args),
                "publish" => await PackageCommands.PublishAsync(root, args).ConfigureAwait(false),

                "deprecate" => LifecycleCommands.Deprecate(root, args),
                "undeprecate" => LifecycleCommands.Undeprecate(root, args),
                "deprecations" => LifecycleCommands.ListDeprecations(root),

                "stats" => StatsCommands.Stats(root, args),
                "doctor" => await DoctorCommands.Doctor(root, args).ConfigureAwait(false),
                "verify" => IntegrityCommands.Verify(root, args),
                "remote" => RemoteCommands.Remote(root, args),
                "push" => RemoteCommands.Push(root, args),
                "init" => ConsumerCommands.Init(root, args),
                "outdated" => ConsumerCommands.Outdated(root, args),
                "update" => ConsumerCommands.Update(root, args),

                _ => Cli.Fail($"Commande inconnue : {args[0]}. Lancez « forge help »."),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Cli.Fail(ex.Message);
        }
    }

    /// <summary>Mémorise la racine MicroForge pour toutes les exécutions futures.</summary>
    private static int Use(string[] args)
    {
        var path = Cli.Arg(args, 1);
        if (path is null)
        {
            var remembered = ForgeRoot.ReadRemembered();
            Console.WriteLine(remembered is null
                ? "Aucune racine mémorisée. Usage : forge use <chemin de MicroForge>"
                : $"Racine mémorisée : {remembered}");
            Console.WriteLine($"Fichier : {ForgeRoot.UserConfigPath}");
            return 0;
        }

        var root = ForgeRoot.Remember(path);
        Console.WriteLine($"Racine mémorisée : {root.Path}");
        Console.WriteLine($"Écrite dans {ForgeRoot.UserConfigPath}.");
        Console.WriteLine("« forge » fonctionne désormais depuis n'importe quel dossier.");
        return 0;
    }

    /// <summary>
    /// Bascule la console en UTF-8. Sans cela, la page de codes Windows par défaut
    /// mutile les caractères hors Latin-1 (« → » devient « ␦ », « … » devient « . »).
    /// L'échec n'est pas bloquant : sortie redirigée ou terminal restreint.
    /// </summary>
    private static void EnableUnicodeOutput()
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (IOException)
        {
            // Sortie redirigée : l'encodage est celui du consommateur.
        }
        catch (System.Security.SecurityException)
        {
            // Terminal restreint : on garde l'encodage en place.
        }
    }

    private static void PrintHelp() => Console.WriteLine("""
        SnippetForge — forge de micropackages MicroForge

        Usage : forge <commande> [arguments]

        TROUVER ET RÉUTILISER
          search <mots clés> [--tags a;b] [--lexical] [--offline]
                                       Recherche hybride (lexicale + sémantique).
          info <PackageId>             Mode d'emploi, versions, contrats, dépréciations.
          list                         Inventaire du feed.
          diff <Id> <v1> <v2>          Différence de contrat public entre deux versions.

        FORGER ET PUBLIER
          new <PackageId> --description "…" --tags "a;b;c"
                                       Scaffolde un micropackage conforme.
          validate <Id|chemin> [--skip-tests]
                                       Vérifie les règles immuables de RULES.md.
          bump <Id|chemin> <major|minor|patch>
                                       Incrémente la version déclarée.
          publish <Id|chemin> [--allow-similar] [--offline]
                                       Valide, teste, empaquette, vérifie le contrat et
                                       l'absence de quasi-doublon, puis publie.
          duplicates                   Recherche les quasi-doublons dans la bibliothèque.
          index [--offline]            Régénère index, contrats et vecteurs depuis le feed.

        CYCLE DE VIE
          deprecate <Id> --reason "…" [--versions "<2.0.0"] [--replacement <Id>]
                                       Déprécie sans altérer les artefacts publiés.
          undeprecate <Id> [--versions …]
                                       Retire une dépréciation.
          deprecations                 Liste les dépréciations déclarées.

        CONSOMMER (projets utilisateurs)
          init [<dossier>] [--no-agent-instructions] [--no-nuget-config]
                          [--agents "Claude,Copilot"]
                                       Raccorde un projet : source NuGet + instructions
                                       agent. Idempotent. Défaut : dossier courant.
          outdated <projet|.csproj>    Diagnostique les références : sûres vs à relire.
          update <projet|.csproj> [--safe-only] [--test "dotnet test"]
                                       Applique les montées ; restaure le projet si vos
                                       tests échouent.

        MESURER ET DIAGNOSTIQUER
          stats                        Investissement, réutilisation, économie estimée.
          doctor [--offline]           Vérifie l'installation et indique quoi corriger.
          verify [--adopt]             Confronte le feed aux empreintes enregistrées.
          use [<chemin>]               Mémorise la racine MicroForge (outil global).
          --version                    Version de l'outil et du format de registre.

        PARTAGER EN ÉQUIPE
          remote [--source <url>] [--api-key-var <VAR>]
                                       Configure ou affiche le dépôt NuGet d'équipe.
          push <Id> [--version <v>]    Pousse une version déjà publiée localement.

        Workflow IA : AGENT.md — Règles immuables : RULES.md
        """);
}
