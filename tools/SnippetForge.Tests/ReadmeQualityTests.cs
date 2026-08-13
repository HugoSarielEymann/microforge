using SnippetForge.Quality;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Le validateur ne contrôlait que la présence des titres : publier le gabarit tel
/// quel passait. Ces tests verrouillent le contrôle du contenu.
/// </summary>
public sealed class ReadmeQualityTests
{
    private const string PackageId = "Micro.Text.Slugify";

    private static string Complete(
        string description = "Convertit une chaîne arbitraire en slug URL, en supprimant les diacritiques.",
        string usage = "Utiliser pour produire un identifiant lisible dans une URL à partir d'un titre saisi.",
        string parameters = """
            | Paramètre | Type | Défaut | Rôle |
            |-----------|------|--------|------|
            | `Separator` | `char` | `'-'` | Caractère de séparation. |
            """,
        string example = """
            ```csharp
            using Micro.Text.Slugify;

            var slug = Slugifier.ToSlug("Hello World");
            ```
            """) => $"""
        # {PackageId}

        ## Description

        {description}

        ## Mode d'emploi

        {usage}

        ## Paramétrage

        {parameters}

        ## Exemple

        {example}
        """;

    [Fact]
    public void ReadmeComplet_NeProduitAucunDefaut() =>
        Assert.Empty(ReadmeQuality.Analyze(Complete(), PackageId));

    [Fact]
    public void SectionManquante_EstSignalee()
    {
        var readme = Complete().Replace("## Exemple", "## Autre chose", StringComparison.Ordinal);

        Assert.Contains(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("## Exemple", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("À compléter")]
    [InlineData("à compléter")]
    [InlineData("TODO : écrire ceci plus tard")]
    public void GabaritNonComplete_EstRefuse(string placeholder)
    {
        var readme = Complete(usage: placeholder + " une description assez longue pour passer la taille minimale.");

        Assert.Contains(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("gabarit n'a pas été complété", StringComparison.Ordinal));
    }

    [Fact]
    public void SectionTropCourte_EstRefusee()
    {
        var readme = Complete(description: "Court.");

        Assert.Contains(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("## Description", StringComparison.Ordinal) &&
                 p.Contains("caractères utiles", StringComparison.Ordinal));
    }

    [Fact]
    public void CommentairesHtmlDuGabarit_NeComptentPasCommeContenu()
    {
        // Le gabarit livre des <!-- indications --> : elles ne doivent pas suffire
        // à faire passer une section pour remplie.
        var readme = Complete(usage: "<!-- Expliquer QUAND utiliser ce package et QUAND ne pas l'utiliser. -->");

        Assert.Contains(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("## Mode d'emploi", StringComparison.Ordinal));
    }

    [Fact]
    public void ExempleSansBlocDeCode_EstRefuse()
    {
        var readme = Complete(example: "Appelez la méthode ToSlug avec votre titre, elle retourne le slug attendu.");

        Assert.Contains(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("bloc de code", StringComparison.Ordinal));
    }

    /// <summary>
    /// Exiger un bloc « csharp » excluait de fait tout micropackage écrit ailleurs
    /// qu'en .NET — un réflexe à corriger pour ouvrir la forge à d'autres écosystèmes.
    /// </summary>
    [Theory]
    [InlineData("python")]
    [InlineData("typescript")]
    [InlineData("")]
    public void ExempleDansUnAutreLangage_EstAccepte(string fenceLanguage)
    {
        var readme = Complete(example: $"""
            ```{fenceLanguage}
            from slugify import slugify
            resultat = slugify("Hello World")
            ```
            """);

        Assert.DoesNotContain(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("bloc de code", StringComparison.Ordinal));
    }

    [Fact]
    public void ExempleReduitAUnCommentaire_EstRefuse()
    {
        var readme = Complete(example: """
            ```csharp
            // Exemple d'appel minimal, compilable, à compléter.
            ```
            """);

        Assert.Contains(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("deux instructions réelles", StringComparison.Ordinal));
    }

    [Fact]
    public void ExempleQuiNIllustrePasLePackage_EstSignale()
    {
        var readme = Complete(example: """
            ```csharp
            var x = 1 + 1;
            Console.WriteLine(x);
            ```
            """);

        Assert.Contains(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("n'illustre donc pas ce package", StringComparison.Ordinal));
    }

    [Fact]
    public void ParametrageSansTableau_EstRefuse()
    {
        var readme = Complete(parameters: "Ce package accepte plusieurs options que vous pouvez ajuster librement.");

        Assert.Contains(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("tableau", StringComparison.Ordinal));
    }

    [Fact]
    public void ParametrageVide_EstAccepteSiDeclareExplicitement()
    {
        var readme = Complete(parameters: "Cette fonction n'accepte aucun paramètre : son comportement est fixe.");

        Assert.DoesNotContain(
            ReadmeQuality.Analyze(readme, PackageId),
            p => p.Contains("tableau", StringComparison.Ordinal));
    }

    [Fact]
    public void GabaritScaffoldeIntegral_EstRefuse()
    {
        // Le scénario redouté : l'agent publie sans rien rédiger.
        var scaffolded = Scaffolder.ReadmeTemplateForTests(PackageId, "Une description de package suffisamment longue.");

        var problems = ReadmeQuality.Analyze(scaffolded, PackageId);

        Assert.NotEmpty(problems);
        Assert.Contains(problems, p => p.Contains("gabarit n'a pas été complété", StringComparison.Ordinal));
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ReadmeQuality.Analyze(null!, PackageId));
        Assert.Throws<ArgumentNullException>(() => ReadmeQuality.Analyze("# x", null!));
    }
}
