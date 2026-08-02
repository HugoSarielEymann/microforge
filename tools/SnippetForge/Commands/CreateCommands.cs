namespace SnippetForge.Commands;

/// <summary>Création d'une bibliothèque vierge.</summary>
public static class CreateCommands
{
    /// <summary>
    /// Crée une bibliothèque MicroForge vide et opérationnelle.
    ///
    /// Sans cette commande, adopter MicroForge imposait de cloner le dépôt de
    /// référence — et donc d'hériter d'un corpus qui n'est pas le sien. Or le corpus
    /// est par nature propre à chaque organisation : le « générique » d'une équipe
    /// n'est pas celui d'une autre. Partir vide est le cas normal, pas l'exception.
    /// </summary>
    public static int Create(string[] args)
    {
        var target = Path.GetFullPath(Cli.Arg(args, 1) ?? Directory.GetCurrentDirectory());

        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any() && !Cli.Flag(args, "--force"))
        {
            return Cli.Fail($"Le dossier « {target} » n'est pas vide. Utiliser --force pour y créer quand même.");
        }

        if (ForgeRoot.IsRoot(target))
        {
            return Cli.Fail($"« {target} » est déjà une bibliothèque MicroForge.");
        }

        Directory.CreateDirectory(Path.Combine(target, "feed"));
        Directory.CreateDirectory(Path.Combine(target, "registry"));
        Directory.CreateDirectory(Path.Combine(target, "packages"));

        // RULES.md sert de marqueur de racine : sans lui, l'outil ne reconnaît pas le
        // dossier. Il porte aussi les règles opposables, d'où le renvoi vers la
        // référence plutôt qu'une copie qui divergerait en silence.
        File.WriteAllText(Path.Combine(target, "RULES.md"), RulesStub());
        File.WriteAllText(Path.Combine(target, "packages", "Directory.Build.props"), BuildProps());
        File.WriteAllText(Path.Combine(target, ".gitignore"), GitIgnore());

        var root = ForgeRoot.Remember(target);

        Console.WriteLine($"Bibliothèque créée : {root.Path}");
        Console.WriteLine("  feed/       artefacts publiés (immuables)");
        Console.WriteLine("  registry/   index, contrats, vecteurs — tous régénérables");
        Console.WriteLine("  packages/   vos micropackages, avec leurs règles de compilation");
        Console.WriteLine();
        Console.WriteLine("Racine mémorisée : « forge » l'utilisera depuis n'importe quel dossier.");
        Console.WriteLine();
        Console.WriteLine("Ensuite :");
        Console.WriteLine("  cd <votre projet> && forge init .    raccorder un projet");
        Console.WriteLine("  forge hazards list                   voir le catalogue d'aléas livré");
        Console.WriteLine("  forge doctor                         vérifier l'installation");
        Console.WriteLine();
        Console.WriteLine("La bibliothèque est vide : c'est normal. Elle se remplit au fil du travail,");
        Console.WriteLine("chaque fois qu'un besoin générique n'y trouve pas de réponse.");
        return 0;
    }

    private static string RulesStub() => """
        # RULES.md — Règles immuables de cette bibliothèque

        > **CE FICHIER EST IMMUABLE.** Il est appliqué mécaniquement par
        > `forge validate` : un package qui viole une règle ne peut pas être publié.
        > Il sert aussi de marqueur de racine — le supprimer casse l'outillage.

        Cette bibliothèque suit les règles de référence de MicroForge :
        <https://github.com/HugoSarielEymann/microforge/blob/main/RULES.md>

        En résumé, un micropackage doit :

        - **R1** porter une seule responsabilité, sans aucun terme du domaine métier ;
        - **R2** être nommé `<Préfixe>.<Domaine>.<Action>`, le dossier portant ce nom ;
        - **R3** n'avoir aucun effet ambiant : horloge, aléa, délai, E/S et
          journalisation sont **injectés** (`Console.*`, `DateTime.Now`,
          `Thread.Sleep`, `File.*`, `new Random()` sont refusés) ;
        - **R6** embarquer des tests xUnit, verts en `Release` ;
        - **R7** embarquer un mode d'emploi réellement rédigé (Description, Mode
          d'emploi, Paramétrage, Exemple) ;
        - **R8/R9** ne jamais modifier une version publiée, et laisser le contrat
          public dicter l'incrément SemVer ;
        - **R10** ne pas dupliquer une capacité existante.

        Le texte complet et à jour fait foi. `forge validate` en donne l'application
        exacte, message par message.
        """;

    private static string BuildProps() => """
        <!--
          FICHIER IMMUABLE : règles de compilation communes à TOUS les micropackages.
          Ne pas surcharger ces propriétés dans un .csproj de package.
        -->
        <Project>

          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <LangVersion>latest</LangVersion>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <GenerateDocumentationFile>true</GenerateDocumentationFile>
            <EnableNETAnalyzers>true</EnableNETAnalyzers>
            <AnalysisLevel>latest-recommended</AnalysisLevel>
            <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
            <Deterministic>true</Deterministic>
          </PropertyGroup>

          <!-- Symboles et sources embarqués : on entre au débogueur dans le code d'un
               micropackage comme dans celui du projet, sans serveur de symboles. -->
          <PropertyGroup>
            <DebugType>embedded</DebugType>
            <EmbedAllSources>true</EmbedAllSources>
            <EmbedUntrackedSources>true</EmbedUntrackedSources>
          </PropertyGroup>

          <!-- Renseignez votre attribution : elle voyagera avec chaque artefact. -->
          <PropertyGroup>
            <Authors>À renseigner</Authors>
            <Copyright>À renseigner</Copyright>
          </PropertyGroup>

        </Project>
        """;

    private static string GitIgnore() => """
        # Artefacts de build
        bin/
        obj/
        .artifacts/

        # Le feed se reconstruit depuis packages/ : les binaires n'ont pas leur place
        # dans le dépôt. Pour partager de vrais artefacts, voir « forge remote ».
        feed/*
        !feed/.gitkeep

        # Le registre est dérivé du feed…
        registry/*
        !registry/.gitkeep

        # …sauf les dépréciations, qui portent des décisions humaines qu'aucun
        # artefact ne contient et que « forge index » ne saurait reconstruire.
        !registry/deprecations.json
        """;
}
