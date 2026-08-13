using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Les règles de RULES.md doivent être opposables : ces tests vérifient qu'une
/// violation est effectivement détectée, et qu'un package conforme passe.
/// Les tests unitaires des packages ne sont pas exécutés ici (--skip-tests).
/// </summary>
public sealed class ValidatorTests
{
    private static IReadOnlyList<string> Validate(TempForge forge, string directory) =>
        Validator.Validate(forge.Root, directory, skipTests: true);

    [Fact]
    public void PackageConforme_NeProduitAucuneErreur()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify");

        Assert.Empty(Validate(forge, directory));
    }

    [Theory]
    [InlineData("microtextslugify")]
    [InlineData("Micro.Text")]
    [InlineData("Text.Slugify")]
    [InlineData("Micro.text.slugify")]
    public void IdentifiantHorsConvention_EstRefuse(string id)
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage(id);

        Assert.Contains(Validate(forge, directory), e => e.Contains("PackageId", StringComparison.Ordinal));
    }

    [Fact]
    public void DossierNeCorrespondantPasAuPackageId_EstRefuse()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify");
        var renamed = Path.Combine(forge.Path, "packages", "AutreNom");
        Directory.Move(directory, renamed);

        Assert.Contains(Validate(forge, renamed), e => e.Contains("doit porter exactement", StringComparison.Ordinal));
    }

    [Fact]
    public void VersionNonSemVer_EstRefusee()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify", version: "1.0");

        Assert.Contains(Validate(forge, directory), e => e.Contains("SemVer", StringComparison.Ordinal));
    }

    [Fact]
    public void DescriptionTropCourte_EstRefusee()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify", description: "Trop court.");

        Assert.Contains(Validate(forge, directory), e => e.Contains("Description trop courte", StringComparison.Ordinal));
    }

    [Fact]
    public void MoinsDeTroisTags_EstRefuse()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify", tags: "text;slug");

        Assert.Contains(Validate(forge, directory), e => e.Contains("Tags insuffisants", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadmeSansSectionObligatoire_EstRefuse()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify", readme: "# Titre\n\n## Description\nTexte.");

        var errors = Validate(forge, directory);
        Assert.Contains(errors, e => e.Contains("Mode d'emploi", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Paramétrage", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Exemple", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Console.WriteLine(\"x\");", "Console")]
    [InlineData("Thread.Sleep(100);", "Thread.Sleep")]
    [InlineData("var now = DateTime.UtcNow;", "DateTime")]
    [InlineData("var r = new Random();", "Random")]
    [InlineData("File.ReadAllText(p);", "File")]
    [InlineData("Directory.CreateDirectory(p);", "Directory")]
    [InlineData("Process.Start(p);", "Process.Start")]
    [InlineData("task.GetAwaiter().GetResult();", "GetResult")]
    public void ApiBannie_EstDetectee(string statement, string expectedMention)
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage(
            "Micro.Text.Slugify",
            sourceCode: $"namespace Sample; public static class Entry {{ public static void Run() {{ {statement} }} }}");

        Assert.Contains(
            Validate(forge, directory),
            e => e.Contains("API bannie", StringComparison.Ordinal) &&
                 e.Contains(expectedMention, StringComparison.Ordinal));
    }

    [Fact]
    public void ApiBannie_SignaleLaLigne()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage(
            "Micro.Text.Slugify",
            sourceCode: "namespace Sample;\npublic static class Entry\n{\n    public static void Run() => Console.WriteLine(\"x\");\n}");

        Assert.Contains(Validate(forge, directory), e => e.Contains(":4 :", StringComparison.Ordinal));
    }

    [Fact]
    public void TestsSansFactNiTheory_EstRefuse()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify", testCode: "public class EntryTests { public void Rien() { } }");

        Assert.Contains(Validate(forge, directory), e => e.Contains("aucun test reconnaissable", StringComparison.Ordinal));
    }

    [Fact]
    public void TheoryEstAccepteCommePreuve()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage(
            "Micro.Text.Slugify",
            testCode: "public class EntryTests { [Theory] [InlineData(1)] public void Ok(int x) { } }");

        Assert.DoesNotContain(Validate(forge, directory), e => e.Contains("[Fact]", StringComparison.Ordinal));
    }

    [Fact]
    public void DossierTestsManquant_EstRefuse()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify");
        Directory.Delete(Path.Combine(directory, "tests"), recursive: true);

        Assert.Contains(Validate(forge, directory), e => e.Contains("tests/", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadmeManquant_EstRefuse()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify");
        File.Delete(Path.Combine(directory, "README.md"));

        Assert.Contains(Validate(forge, directory), e => e.Contains("README.md manquant", StringComparison.Ordinal));
    }

    [Fact]
    public void VersionDejaPubliee_EstRefusee_ImmutabiliteDuFeed()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify", version: "1.0.0");
        File.WriteAllText(Path.Combine(forge.Path, "feed", "Micro.Text.Slugify.1.0.0.nupkg"), "artefact");

        Assert.Contains(Validate(forge, directory), e => e.Contains("immuables", StringComparison.Ordinal));
    }

    [Fact]
    public void VersionSuperieure_EstAcceptee_ApresUnePremierePublication()
    {
        using var forge = TempForge.Create();
        var directory = forge.WritePackage("Micro.Text.Slugify", version: "1.0.1");
        File.WriteAllText(Path.Combine(forge.Path, "feed", "Micro.Text.Slugify.1.0.0.nupkg"), "artefact");

        Assert.Empty(Validate(forge, directory));
    }

    [Fact]
    public void DossierInexistant_EstSignale()
    {
        using var forge = TempForge.Create();
        var errors = Validate(forge, Path.Combine(forge.Path, "packages", "Inexistant"));

        Assert.Contains(errors, e => e.Contains("introuvable", StringComparison.Ordinal));
    }
}
