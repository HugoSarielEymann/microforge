using SnippetForge.Bench;
using SnippetForge.Telemetry;
using Xunit;

namespace SnippetForge.Tests;

public sealed class BenchSessionTests
{
    private static string CreateProject(TempForge forge, string name = "projet")
    {
        var dir = Path.Combine(forge.Path, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Program.cs"), "var x = 1;\nvar y = 2;");
        File.WriteAllText(Path.Combine(dir, "Demo.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Micro.Flow.Retry" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);
        return dir;
    }

    [Fact]
    public void Capture_PuisLoad_RestitueLaManche()
    {
        using var forge = TempForge.Create();
        var project = CreateProject(forge);

        BenchSession.Capture(forge.Root, "manche1", project, "prompt de test");
        var loaded = BenchSession.Load(forge.Root, "manche1")!;

        Assert.Equal("manche1", loaded.Name);
        Assert.Equal("prompt de test", loaded.Prompt);
        Assert.Equal(2, loaded.FileHashes.Count);
        Assert.Equal(2, loaded.CsLineCounts.Values.Sum());
        Assert.Contains("Micro.Flow.Retry 1.0.0", loaded.ForgeReferences);
    }

    [Fact]
    public void Report_SansChangement_EstNeutre()
    {
        using var forge = TempForge.Create();
        var project = CreateProject(forge);
        var snapshot = BenchSession.Capture(forge.Root, "m", project, null);

        var report = BenchSession.Report(forge.Root, snapshot);

        Assert.Empty(report.AddedFiles);
        Assert.Empty(report.ModifiedFiles);
        Assert.Equal(0, report.NetCsLines);
        Assert.Empty(report.NewForgeReferences);
        Assert.Empty(report.ForgedArtifacts);
        Assert.False(report.SearchedBeforeCoding);
    }

    [Fact]
    public void Report_MesureFichiersEtLignesProduits()
    {
        using var forge = TempForge.Create();
        var project = CreateProject(forge);
        var snapshot = BenchSession.Capture(forge.Root, "m", project, null);

        File.WriteAllText(Path.Combine(project, "Service.cs"), "a();\nb();\nc();");
        File.AppendAllText(Path.Combine(project, "Program.cs"), "\nvar z = 3;");

        var report = BenchSession.Report(forge.Root, snapshot);

        Assert.Equal(["Service.cs"], report.AddedFiles);
        Assert.Equal(["Program.cs"], report.ModifiedFiles);
        Assert.Equal(4, report.NetCsLines);
    }

    [Fact]
    public void Report_DetecteReferencesEtPackagesForges()
    {
        using var forge = TempForge.Create();
        var project = CreateProject(forge);
        var snapshot = BenchSession.Capture(forge.Root, "m", project, null);

        var csproj = Path.Combine(project, "Demo.csproj");
        File.WriteAllText(csproj, File.ReadAllText(csproj).Replace(
            "</ItemGroup>",
            "  <PackageReference Include=\"Micro.Text.Slugify\" Version=\"1.1.0\" />\n  </ItemGroup>",
            StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(forge.Path, "feed", "Micro.Time.Parse.1.0.0.nupkg"), "forgé pendant la manche");

        var report = BenchSession.Report(forge.Root, snapshot);

        Assert.Equal(["Micro.Text.Slugify 1.1.0"], report.NewForgeReferences);
        Assert.Equal(["Micro.Time.Parse.1.0.0.nupkg"], report.ForgedArtifacts);
    }

    [Fact]
    public void Report_AttribueLesInvocationsALaManche()
    {
        using var forge = TempForge.Create();
        var project = CreateProject(forge);

        UsageLog.Append(forge.Root, "list", [], 0); // avant la manche : hors périmètre
        var snapshot = BenchSession.Capture(forge.Root, "m", project, null);
        UsageLog.Append(forge.Root, "bench", ["start", "m"], 0); // instrumentation : exclue
        UsageLog.Append(forge.Root, "search", ["parse", "duration"], 0);
        UsageLog.Append(forge.Root, "publish", ["Micro.Time.Parse"], 0);

        var report = BenchSession.Report(forge.Root, snapshot);

        Assert.Equal(2, report.ForgeInvocations.Count);
        Assert.Equal("search", report.ForgeInvocations[0].Command);
        Assert.True(report.SearchedBeforeCoding);
    }

    [Fact]
    public void Report_IgnoreBinObjEtGit()
    {
        using var forge = TempForge.Create();
        var project = CreateProject(forge);
        var snapshot = BenchSession.Capture(forge.Root, "m", project, null);

        var objDir = Path.Combine(project, "obj", "Debug");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "Generated.cs"), string.Join('\n', Enumerable.Repeat("bruit();", 500)));

        var report = BenchSession.Report(forge.Root, snapshot);

        Assert.Empty(report.AddedFiles);
        Assert.Equal(0, report.NetCsLines);
    }

    [Fact]
    public void List_EnumereLesManchesCapturees()
    {
        using var forge = TempForge.Create();
        var project = CreateProject(forge);

        Assert.Empty(BenchSession.List(forge.Root));
        BenchSession.Capture(forge.Root, "manche1", project, null);
        BenchSession.Capture(forge.Root, "manche2", project, null);

        Assert.Equal(["manche1", "manche2"], BenchSession.List(forge.Root));
    }

    [Fact]
    public void Capture_ProjetInexistant_LeveArgumentException()
    {
        using var forge = TempForge.Create();
        Assert.Throws<ArgumentException>(() => BenchSession.Capture(
            forge.Root, "m", Path.Combine(forge.Path, "nulle-part"), null));
    }

    [Fact]
    public void Load_MancheInconnue_RetourneNull()
    {
        using var forge = TempForge.Create();
        Assert.Null(BenchSession.Load(forge.Root, "fantome"));
    }
}
