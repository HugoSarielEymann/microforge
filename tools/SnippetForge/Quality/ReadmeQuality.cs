using System.Text.RegularExpressions;

namespace SnippetForge.Quality;

/// <summary>
/// Contrôle le **contenu** du mode d'emploi, pas seulement la présence des titres.
///
/// Vérifier que les quatre sections existent ne coûte rien à contourner : il suffit de
/// publier le gabarit tel quel, « À compléter » compris. Or le README est la seule
/// chose qu'une IA lira avant de décider de réutiliser un package. Un mode d'emploi
/// vide rend le package invisible en pratique, et la bibliothèque se remplit de
/// coquilles.
/// </summary>
public static partial class ReadmeQuality
{
    /// <summary>Sections obligatoires, dans l'ordre attendu.</summary>
    public static IReadOnlyList<string> RequiredSections { get; } =
        ["## Description", "## Mode d'emploi", "## Paramétrage", "## Exemple"];

    /// <summary>Contenu utile minimal par section, en caractères.</summary>
    public const int MinimumSectionLength = 40;

    private static readonly string[] Placeholders =
    [
        "à compléter",
        "a completer",
        "todo",
        "lorem ipsum",
        "xxx",
    ];

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();

    /// <summary>
    /// Bloc de code, quel que soit le langage annoncé. Exiger « csharp » excluait de
    /// fait tout micropackage écrit ailleurs qu'en .NET.
    /// </summary>
    [GeneratedRegex(@"```[a-zA-Z0-9+#-]*\s*\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex(@"^\|.*\|\s*$", RegexOptions.Multiline)]
    private static partial Regex TableRowRegex();

    /// <summary>
    /// Analyse un mode d'emploi et retourne les défauts constatés (vide si conforme).
    /// </summary>
    /// <param name="readme">Contenu brut du README.md.</param>
    /// <param name="packageId">Identifiant du package, pour vérifier que l'exemple le cite.</param>
    /// <exception cref="ArgumentNullException">Si un argument est nul.</exception>
    public static IReadOnlyList<string> Analyze(string readme, string packageId)
    {
        ArgumentNullException.ThrowIfNull(readme);
        ArgumentNullException.ThrowIfNull(packageId);

        var problems = new List<string>();
        var sections = SplitSections(readme);

        foreach (var section in RequiredSections)
        {
            if (!sections.TryGetValue(section, out var body))
            {
                problems.Add($"README.md : section obligatoire manquante « {section} ».");
                continue;
            }

            var meaningful = StripNoise(body);
            if (meaningful.Length < MinimumSectionLength)
            {
                problems.Add(
                    $"README.md : la section « {section} » ne contient que {meaningful.Length} caractères utiles " +
                    $"(minimum {MinimumSectionLength}). Une section vide rend le package inutilisable par une IA.");
            }
        }

        foreach (var placeholder in Placeholders)
        {
            if (readme.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add(
                    $"README.md : le gabarit n'a pas été complété (« {placeholder} » subsiste). " +
                    "Rédiger le mode d'emploi avant de publier.");
            }
        }

        problems.AddRange(AnalyzeExample(sections, packageId));
        problems.AddRange(AnalyzeParameters(sections));

        return problems;
    }

    private static IEnumerable<string> AnalyzeExample(IReadOnlyDictionary<string, string> sections, string packageId)
    {
        if (!sections.TryGetValue("## Exemple", out var example))
        {
            yield break;
        }

        var fence = CodeFenceRegex().Match(example);
        if (!fence.Success)
        {
            yield return "README.md : la section « ## Exemple » doit contenir un bloc de code délimité par ```.";
            yield break;
        }

        var code = fence.Groups[1].Value;
        var codeLines = code
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        if (codeLines.Count < 2)
        {
            yield return
                "README.md : l'exemple doit comporter au moins deux instructions réelles " +
                "(un commentaire seul ne montre pas comment appeler le package).";
        }

        // Le namespace du package est son identifiant : l'exemple doit l'exercer.
        var rootNamespace = packageId.Split('.')[^1];
        if (rootNamespace.Length > 0 &&
            !code.Contains(packageId, StringComparison.OrdinalIgnoreCase) &&
            !code.Contains(rootNamespace, StringComparison.OrdinalIgnoreCase))
        {
            yield return
                $"README.md : l'exemple ne mentionne ni « {packageId} » ni « {rootNamespace} » — " +
                "il n'illustre donc pas ce package.";
        }
    }

    private static IEnumerable<string> AnalyzeParameters(IReadOnlyDictionary<string, string> sections)
    {
        if (!sections.TryGetValue("## Paramétrage", out var parameters))
        {
            yield break;
        }

        // Un tableau markdown a au moins trois lignes : en-tête, séparateur, une donnée.
        var rows = TableRowRegex().Matches(StripNoise(parameters)).Count;
        var declaresNoParameter =
            parameters.Contains("aucun paramètre", StringComparison.OrdinalIgnoreCase) ||
            parameters.Contains("sans paramètre", StringComparison.OrdinalIgnoreCase);

        if (rows < 3 && !declaresNoParameter)
        {
            yield return
                "README.md : « ## Paramétrage » doit décrire chaque option dans un tableau " +
                "(paramètre | type | défaut | rôle), ou indiquer explicitement « aucun paramètre ».";
        }
    }

    /// <summary>Découpe le document par titres de niveau 2.</summary>
    private static Dictionary<string, string> SplitSections(string readme)
    {
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? current = null;
        var body = new List<string>();

        foreach (var line in readme.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    sections[current] = string.Join('\n', body);
                }

                current = line.TrimEnd();
                body.Clear();
            }
            else if (current is not null)
            {
                body.Add(line);
            }
        }

        if (current is not null)
        {
            sections[current] = string.Join('\n', body);
        }

        return sections;
    }

    /// <summary>Retire les commentaires HTML du gabarit et l'espace superflu.</summary>
    private static string StripNoise(string text) =>
        HtmlCommentRegex().Replace(text, string.Empty).Trim();
}
