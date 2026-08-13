namespace SnippetForge.Languages;

/// <summary>
/// Ce qu'un écosystème permet de vérifier, et comment y reconnaître du code et des tests.
///
/// Deux profils coexistent, et la différence doit rester **explicite** : un écosystème
/// incapable de prouver l'absence de rupture ne doit jamais laisser croire qu'il l'a
/// prouvée. C'est la seule façon d'ouvrir MicroForge à d'autres langages sans
/// affaiblir les garanties là où elles existent.
/// </summary>
/// <param name="Id">Identifiant en minuscules, déclaré dans le manifeste du package.</param>
/// <param name="DisplayName">Nom lisible.</param>
/// <param name="SourceExtensions">Extensions considérées comme du code source.</param>
/// <param name="TestMarkers">Marqueurs prouvant qu'un fichier contient de vrais tests.</param>
/// <param name="HazardTraitHint">Comment un test déclare couvrir un aléa.</param>
/// <param name="SupportsPackageDistribution">L'écosystème a un registre exploitable par la forge.</param>
/// <param name="SupportsContractVerification">Le contrat public est extractible, donc le SemVer opposable.</param>
public sealed record LanguageProfile(
    string Id,
    string DisplayName,
    IReadOnlyList<string> SourceExtensions,
    IReadOnlyList<string> TestMarkers,
    string HazardTraitHint,
    bool SupportsPackageDistribution,
    bool SupportsContractVerification)
{
    /// <summary>Vrai si l'écosystème bénéficie de toutes les garanties (profil vérifié).</summary>
    public bool IsVerifiedProfile => SupportsPackageDistribution && SupportsContractVerification;

    /// <summary>Résumé des garanties, à afficher pour que la dégradation soit visible.</summary>
    public string Guarantees => IsVerifiedProfile
        ? "profil vérifié : contrat public extrait, SemVer opposable, distribution par package"
        : "profil de base : structure et tests validés, distribution par copie, SemVer déclaratif";
}

/// <summary>Écosystèmes reconnus.</summary>
public static class LanguageProfiles
{
    /// <summary>Écosystème par défaut : le seul à offrir toutes les garanties.</summary>
    public const string DefaultId = "csharp";

    /// <summary>C# / .NET — implémentation de référence.</summary>
    public static LanguageProfile CSharp { get; } = new(
        "csharp", "C# / .NET",
        [".cs"],
        ["[Fact", "[Theory"],
        "[Trait(\"hazard\", \"<id>\")]",
        SupportsPackageDistribution: true,
        SupportsContractVerification: true);

    /// <summary>Python — typage optionnel, contrat non extractible de façon fiable.</summary>
    public static LanguageProfile Python { get; } = new(
        "python", "Python",
        [".py"],
        ["def test_", "assert "],
        "# hazard: <id>",
        SupportsPackageDistribution: false,
        SupportsContractVerification: false);

    /// <summary>TypeScript.</summary>
    public static LanguageProfile TypeScript { get; } = new(
        "typescript", "TypeScript",
        [".ts", ".tsx"],
        ["test(", "it(", "describe("],
        "// hazard: <id>",
        SupportsPackageDistribution: false,
        SupportsContractVerification: false);

    /// <summary>JavaScript.</summary>
    public static LanguageProfile JavaScript { get; } = new(
        "javascript", "JavaScript",
        [".js", ".mjs"],
        ["test(", "it(", "describe("],
        "// hazard: <id>",
        SupportsPackageDistribution: false,
        SupportsContractVerification: false);

    /// <summary>Go.</summary>
    public static LanguageProfile Go { get; } = new(
        "go", "Go",
        [".go"],
        ["func Test"],
        "// hazard: <id>",
        SupportsPackageDistribution: false,
        SupportsContractVerification: false);

    /// <summary>Rust.</summary>
    public static LanguageProfile Rust { get; } = new(
        "rust", "Rust",
        [".rs"],
        ["#[test]"],
        "// hazard: <id>",
        SupportsPackageDistribution: false,
        SupportsContractVerification: false);

    /// <summary>Tous les profils connus.</summary>
    public static IReadOnlyList<LanguageProfile> All { get; } =
        [CSharp, Python, TypeScript, JavaScript, Go, Rust];

    /// <summary>Résout un profil par identifiant, ou null s'il est inconnu.</summary>
    public static LanguageProfile? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : All.FirstOrDefault(p => p.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Résout un profil, en retombant sur C# faute de déclaration.</summary>
    public static LanguageProfile Resolve(string? id) => Find(id) ?? CSharp;

    /// <summary>Identifiants connus, pour les messages d'erreur.</summary>
    public static string KnownIds => string.Join(", ", All.Select(p => p.Id));
}
