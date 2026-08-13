using System.Text;
using SnippetForge.Languages;

namespace SnippetForge.Consumers;

/// <summary>Effet de l'écriture des instructions agent.</summary>
public enum InstructionsOutcome
{
    /// <summary>Le fichier n'existait pas : il a été créé.</summary>
    Created = 0,

    /// <summary>Le bloc MicroForge a été ajouté à un fichier existant.</summary>
    Appended = 1,

    /// <summary>Un bloc MicroForge existait : il a été remplacé.</summary>
    Replaced = 2,

    /// <summary>Le bloc présent était déjà à jour : aucune écriture.</summary>
    Unchanged = 3,
}

/// <summary>
/// Écrit dans le CLAUDE.md d'un projet le bloc qui oriente les IA vers MicroForge.
///
/// Le bloc est délimité par des marqueurs : il peut être réécrit lorsque MicroForge
/// évolue, sans jamais toucher au reste du fichier, qui appartient au projet.
/// </summary>
public static class AgentInstructionsWriter
{
    private const string BeginMarker = "<!-- microforge:begin -->";
    private const string EndMarker = "<!-- microforge:end -->";

    /// <summary>
    /// Fichiers d'instructions écrits dans un projet, par agent. Chaque outil charge
    /// automatiquement le sien ; le contenu est identique et pointe vers AGENT.md,
    /// qui reste la source de vérité unique du workflow.
    /// </summary>
    public static IReadOnlyList<(string RelativePath, string Agent)> ProjectTargets { get; } =
    [
        ("CLAUDE.md", "Claude Code"),
        (Path.Combine(".github", "copilot-instructions.md"), "GitHub Copilot"),
    ];

    /// <summary>
    /// Sélectionne les cibles correspondant à un filtre (liste séparée par des virgules,
    /// comparée au nom de l'agent). Un filtre vide retourne toutes les cibles.
    /// </summary>
    public static IReadOnlyList<(string RelativePath, string Agent)> SelectTargets(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return ProjectTargets;
        }

        var wanted = filter.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ProjectTargets
            .Where(target => wanted.Any(w => target.Agent.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Garantit la présence d'un bloc MicroForge à jour dans <paramref name="claudeMdPath"/>,
    /// pointant vers la racine <paramref name="forgeRootPath"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Si un argument est vide.</exception>
    public static InstructionsOutcome Ensure(string claudeMdPath, string forgeRootPath) =>
        Ensure(claudeMdPath, forgeRootPath, LanguageProfiles.CSharp);

    /// <summary>
    /// Variante précisant l'écosystème du projet : les instructions qui en dépendent
    /// (consommer par référence ou par copie, conventions de test, marqueur d'aléa)
    /// sont adaptées. Des instructions C# posées dans un projet Python enverraient
    /// l'agent sur `dotnet add package`, qui n'y existe pas.
    /// </summary>
    /// <exception cref="ArgumentException">Si un argument est vide.</exception>
    public static InstructionsOutcome Ensure(string claudeMdPath, string forgeRootPath, LanguageProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claudeMdPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(forgeRootPath);
        ArgumentNullException.ThrowIfNull(profile);

        var block = BuildBlock(forgeRootPath, profile);

        if (!File.Exists(claudeMdPath))
        {
            var directory = Path.GetDirectoryName(claudeMdPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(claudeMdPath, block + Environment.NewLine);
            return InstructionsOutcome.Created;
        }

        var content = File.ReadAllText(claudeMdPath);
        var begin = content.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = content.IndexOf(EndMarker, StringComparison.Ordinal);

        if (begin < 0 || end < begin)
        {
            var separator = content.EndsWith('\n') ? Environment.NewLine : Environment.NewLine + Environment.NewLine;
            File.WriteAllText(claudeMdPath, content + separator + block + Environment.NewLine);
            return InstructionsOutcome.Appended;
        }

        var existingBlock = content[begin..(end + EndMarker.Length)];
        if (string.Equals(existingBlock, block, StringComparison.Ordinal))
        {
            return InstructionsOutcome.Unchanged;
        }

        var builder = new StringBuilder(content.Length);
        builder.Append(content, 0, begin);
        builder.Append(block);
        builder.Append(content, end + EndMarker.Length, content.Length - end - EndMarker.Length);
        File.WriteAllText(claudeMdPath, builder.ToString());
        return InstructionsOutcome.Replaced;
    }

    /// <summary>
    /// Construit le bloc d'instructions pour une racine donnée.
    ///
    /// Le bloc est **autonome** : il contient tout ce qu'il faut pour agir, sans lire
    /// aucun autre fichier. Les agents ne suivent pas de façon fiable un chemin de
    /// fichier cité en référence — surtout hors du dossier de travail. Renvoyer vers
    /// AGENT.md pour la moitié « forger un package » revenait à ne jamais la livrer.
    /// </summary>
    public static string BuildBlock(string forgeRootPath) => BuildBlock(forgeRootPath, LanguageProfiles.CSharp);

    /// <summary>Variante adaptée à l'écosystème du projet.</summary>
    public static string BuildBlock(string forgeRootPath, LanguageProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(forgeRootPath);
        ArgumentNullException.ThrowIfNull(profile);

        var verified = profile.IsVerifiedProfile;
        var languageFlag = verified ? string.Empty : $" --language {profile.Id}";

        // Un écosystème sans registre exploitable consomme par copie ; le dire
        // explicitement évite qu'un agent invente une commande d'installation.
        var install = verified
            ? "`dotnet add package <Id> --version <version exacte>`"
            : "`forge copy <Id> --into <dossier du projet>`";

        var standardLibrary = verified
            ? "`Enumerable.Chunk`, `string.Split`, `TimeSpan.TryParse`, `HttpClient`,\n" +
              "`System.Text.Json`, `Convert`, `Path`, `Regex`…"
            : $"la bibliothèque standard de {profile.DisplayName} et les dépendances déjà\n" +
              "présentes dans le projet…";

        var sourceGuidance = verified
            ? """
              - **`src/`** — une seule responsabilité, une classe d'entrée, signature générique
                (`<T>` si pertinent). Les options vont dans un type `XxxOptions` avec des défauts
                raisonnables et une méthode `Validate()`. Documentation XML complète sur toute
                l'API publique (contrat, paramètres, exceptions).
              - **`tests/`** — de vrais tests xUnit (le test généré échoue volontairement tant
                qu'il n'est pas remplacé) : cas nominal, cas limites, erreurs de paramétrage.
              """
            : $"""
              - **`src/`** — une seule responsabilité, un point d'entrée, signature générique si
                l'écosystème le permet. Les options vont dans un objet de configuration doté de
                défauts raisonnables et d'une validation. Documenter tout le public : contrat,
                paramètres, erreurs levées.
              - **`tests/`** — de vrais tests reconnus par l'écosystème ({string.Join(", ", profile.TestMarkers.Select(m => $"`{m}`"))}) :
                cas nominal, cas limites, erreurs de paramétrage.
              """;

        var forbidden = verified
            ? """
              **Interdit dans `src/`** (refusé automatiquement) : `Console.*`, `DateTime.Now/UtcNow`,
              `Thread.Sleep`, `new Random()` sans graine, `File.*`, `Directory.*`, `Process.Start`,
              `.GetAwaiter().GetResult()`. Tout effet non déterministe est **injecté** : horloge,
              délai, aléa, et journalisation via `Microsoft.Extensions.Logging.ILogger`.
              Async de bout en bout avec `CancellationToken` si l'opération peut être longue.
              """
            : """
              **Interdit dans `src/`** : écriture sur la sortie standard, horloge système, mise en
              sommeil, aléa non ensemencé, accès disque, lancement de processus, attente bloquante
              d'un appel asynchrone. Tout effet non déterministe est **injecté** : horloge, délai,
              aléa, journalisation. Hors .NET ces interdits ne sont **pas** vérifiés
              mécaniquement — c'est à l'auteur et au relecteur de les tenir.
              """;

        var maintenance = verified
            ? """
              Les versions sont épinglées ; rien ne bouge tout seul. Pour proposer une montée :

              ```
              forge outdated .
              forge update . --safe-only --test "<commande de tests du projet>"
              ```

              Une montée « SÛRE » garantit la compilation, pas le comportement : c'est la suite
              de tests du projet qui tranche. En cas d'échec, le `.csproj` est restauré seul.
              """
            : """
              Le code copié ne se met pas à jour tout seul. Pour savoir où on en est :

              ```
              forge copied .
              ```

              Il signale les versions en retard et les fichiers **retouchés localement** —
              une recopie qui les écraserait est refusée sauf `--force`. Ne jamais modifier
              un fichier copié sans le remonter dans le package d'origine : la divergence
              silencieuse est exactement ce que la forge existe pour éviter.
              """;

        var degradation = verified
            ? string.Empty
            : $"""

              > **Écosystème {profile.DisplayName} — {profile.Guarantees}.**
              > Recherche, anti-duplication, aléas et README sont vérifiés comme ailleurs.
              > Le contrat public n'est **pas** extrait : une montée de version n'est pas
              > prouvée sans rupture, seule la suite de tests du projet le dira.

              """;

        return $$"""
        {{BeginMarker}}
        ## Bibliothèque de micropackages MicroForge

        Ce projet est raccordé à MicroForge : une bibliothèque de micropackages qui évite
        de régénérer sans cesse le même code générique. **Ces instructions sont complètes :
        les appliquer sans avoir à ouvrir d'autre fichier.**
        {{degradation}}
        ### Règle

        Avant d'écrire du code {{profile.DisplayName}} **générique** (relance, parsing, formatage,
        validation, découpage, mapping, encodage, calcul de dates…), procéder dans cet ordre.

        **1. La bibliothèque standard couvre-t-elle déjà le besoin ?**
        {{standardLibrary}} Si oui, l'utiliser directement.
        Ne jamais créer un micropackage qui double le framework.

        **2. Sinon, chercher dans la bibliothèque :**

        ```
        forge search "<besoin formulé en langage naturel>"
        forge info <PackageId>
        ```

        La recherche est filtrée sur l'écosystème du projet ({{profile.Id}}) : un package
        d'un autre langage n'apparaît pas. `--language all` lève le filtre pour vérifier
        si l'équivalent existe ailleurs et mérite d'être porté.

        - **Un package répond au besoin** → {{install}}
          puis le paramétrer. **Ne jamais le recoder.** Toujours épingler la version exacte.
        - **Un package répond presque** (une option manque) → ne pas en créer un second :
          étendre l'existant de façon rétrocompatible (paramètre optionnel, surcharge),
          `forge bump <Id> minor`, `forge publish <Id>`, puis l'utiliser.
        - **`forge search` répond « Aucun micropackage ne correspond »** → passer à
          l'étape 3. **Ne pas écrire ce code en ligne dans le projet.**

        **3. Forger le micropackage manquant.** C'est une étape obligatoire du workflow,
        pas une option :

        ```
        forge new Micro.<Domaine>.<Action>{{languageFlag}} --description "<≥ 30 caractères, rédigée pour être trouvée par recherche>" --tags "domaine;capacite;qualificatif"
        ```

        Le dossier est créé sous `{{Path.Combine(forgeRootPath, "packages")}}`. Y implémenter :

        {{sourceGuidance}}
        - **`README.md`** — les 4 sections sont obligatoires et vérifiées :
          `## Description`, `## Mode d'emploi` (quand l'utiliser / quand ne pas l'utiliser),
          `## Paramétrage` (tableau exhaustif des options), `## Exemple` (code compilable).

        {{forbidden}}

        **Déclarer les aléas de test.** Un aléa est une classe d'entrées dangereuses
        déjà éprouvée ailleurs (`null-input`, `numeric-overflow`, `secret-leak`,
        `unicode-edge`, `cancellation`…). Consulter `forge hazards list`, puis :

        ```
        forge hazards declare <Id> --hazards "null-input;numeric-overflow"
        ```

        Chaque aléa déclaré **doit** être prouvé par un test portant
        `{{profile.HazardTraitHint}}`, sinon la publication échoue. Si vous découvrez un
        mode de défaillance absent du catalogue, l'y ajouter : `forge hazards add`.
        L'acquis devient collectif au lieu d'être redécouvert au projet suivant.

        ```
        forge review   <Id>      relit ce que le validateur ne sait pas juger
        forge validate <Id>
        forge publish  <Id>
        ```

        `forge review` signale les membres publics jamais testés, les exceptions
        documentées jamais provoquées et les `TryXxx` (qui ne doivent **jamais** lever).
        Il ne tranche pas : il pose les questions. Les traiter avant de publier.

        `forge publish` peut opposer quatre refus. Les traiter, jamais les contourner :

        | Refus | Action |
        |-------|--------|
        | Violation des règles | Corriger le package (le message dit quoi) |
        | Version déjà publiée | `forge bump <Id> patch\|minor\|major` |
        | Incrément insuffisant | `forge bump` au niveau annoncé par le message |
        | Quasi-doublon | Un package couvre déjà ce besoin : l'utiliser, ne pas insister |

        **4. Consommer le package forgé** dans ce projet par {{(verified ? "`dotnet add package`" : "`forge copy`")}} —
        jamais en recopiant son code à la main.

        ### Maintenance des versions

        {{maintenance}}

        ### Référence complète (facultative)

        Workflow détaillé : `{{Path.Combine(forgeRootPath, "AGENT.md")}}`
        Règles opposables : `{{Path.Combine(forgeRootPath, "RULES.md")}}`
        {{EndMarker}}
        """;
    }
}
