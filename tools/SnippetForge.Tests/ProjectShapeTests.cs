using SnippetForge.Consumers;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Régression : « forge init . » et « forge bench start » lancés depuis
/// C:\Users\hugoe\source\repos (133 000 fichiers, 140 projets) ont déposé des
/// instructions pour des dépôts sans rapport, et la capture n'a jamais terminé.
/// </summary>
public sealed class ProjectShapeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "microforge-shape", Guid.NewGuid().ToString("N"));

    public ProjectShapeTests() => Directory.CreateDirectory(_root);

    private void WriteFile(string relativePath, string content = "x")
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void ProjetOrdinaire_NEstPasUnConteneur()
    {
        WriteFile("Demo.csproj");
        WriteFile("Program.cs");
        WriteFile("Services/Import.cs");

        var shape = ProjectShapeAnalyzer.Analyze(_root);

        Assert.False(shape.LooksLikeContainer);
        Assert.Null(shape.Warning);
        Assert.Equal(3, shape.FileCount);
        Assert.Equal(1, shape.ProjectCount);
    }

    [Fact]
    public void SolutionAvecProjetEtTests_ResteAcceptee()
    {
        // Un projet, ses tests et un projet d'intégration : cas légitime courant.
        WriteFile("src/App.csproj");
        WriteFile("tests/App.Tests.csproj");
        WriteFile("integration/App.Integration.csproj");
        WriteFile("src/Program.cs");

        Assert.False(ProjectShapeAnalyzer.Analyze(_root).LooksLikeContainer);
    }

    [Fact]
    public void DossierDeDepots_EstReconnuCommeConteneur()
    {
        for (var i = 0; i < 5; i++)
        {
            WriteFile($"Depot{i}/Depot{i}.csproj");
            WriteFile($"Depot{i}/Program.cs");
        }

        var shape = ProjectShapeAnalyzer.Analyze(_root);

        Assert.True(shape.LooksLikeContainer);
        Assert.Contains("dossier parent", shape.Warning!, StringComparison.Ordinal);
        Assert.Equal(5, shape.ProjectCount);
    }

    [Fact]
    public void TropDeFichiers_EstReconnuCommeConteneur()
    {
        for (var i = 0; i < 60; i++)
        {
            WriteFile($"fichier{i}.txt");
        }

        var shape = ProjectShapeAnalyzer.Analyze(_root, fileCeiling: 50);

        Assert.True(shape.LooksLikeContainer);
        Assert.True(shape.Truncated);
    }

    [Fact]
    public void LAnalyseSArreteAuPlafond()
    {
        // Le contrôle ne doit pas coûter ce qu'il cherche à éviter : il s'interrompt
        // dès le plafond franchi, sans parcourir l'intégralité de l'arborescence.
        for (var i = 0; i < 200; i++)
        {
            WriteFile($"f{i}.txt");
        }

        var shape = ProjectShapeAnalyzer.Analyze(_root, fileCeiling: 10);

        Assert.Equal(10, shape.FileCount);
        Assert.True(shape.Truncated);
    }

    [Fact]
    public void BinObjEtGit_NeComptentPas()
    {
        WriteFile("Demo.csproj");
        for (var i = 0; i < 100; i++)
        {
            WriteFile($"obj/Debug/artefact{i}.cache");
            WriteFile($"bin/Release/sortie{i}.dll");
            WriteFile($".git/objects/{i}");
        }

        var shape = ProjectShapeAnalyzer.Analyze(_root, fileCeiling: 50);

        Assert.Equal(1, shape.FileCount);
        Assert.False(shape.LooksLikeContainer);
    }

    [Fact]
    public void DossierVide_EstAccepte() =>
        Assert.False(ProjectShapeAnalyzer.Analyze(_root).LooksLikeContainer);

    [Fact]
    public void ArgumentsInvalides_Levent()
    {
        Assert.Throws<ArgumentException>(() => ProjectShapeAnalyzer.Analyze("  "));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectShapeAnalyzer.Analyze(_root, fileCeiling: 0));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Nettoyage best-effort.
        }
    }
}
