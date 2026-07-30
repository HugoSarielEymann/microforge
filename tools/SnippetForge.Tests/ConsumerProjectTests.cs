using SnippetForge.Consumers;
using Xunit;

namespace SnippetForge.Tests;

public sealed class ConsumerProjectTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "microforge-consumer", Guid.NewGuid().ToString("N"));

    public ConsumerProjectTests() => Directory.CreateDirectory(_directory);

    private string WriteProject(string content)
    {
        var path = Path.Combine(_directory, "Demo.csproj");
        File.WriteAllText(path, content);
        return path;
    }

    private const string SampleProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Micro.Flow.Retry" Version="1.0.0" />
            <PackageReference Include="Micro.Text.Slugify" Version="1.2.0" />
            <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void ReadForgeReferences_NeRetourneQueLesPackagesMicroForge()
    {
        var references = ConsumerProject.ReadForgeReferences(WriteProject(SampleProject));

        Assert.Equal(2, references.Count);
        Assert.DoesNotContain(references, r => r.PackageId == "Newtonsoft.Json");
        Assert.Equal("Micro.Flow.Retry", references[0].PackageId);
        Assert.Equal("1.0.0", references[0].Version);
    }

    [Fact]
    public void ReadForgeReferences_LitLaVersionEnElementEnfant()
    {
        var path = WriteProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Micro.A.One">
                  <Version>2.3.4</Version>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var reference = Assert.Single(ConsumerProject.ReadForgeReferences(path));
        Assert.Equal("2.3.4", reference.Version);
    }

    [Fact]
    public void ApplyVersions_MetAJourEtCompteLesChangements()
    {
        var path = WriteProject(SampleProject);

        var changed = ConsumerProject.ApplyVersions(path, new Dictionary<string, string>
        {
            ["Micro.Flow.Retry"] = "1.4.0",
        });

        Assert.Equal(1, changed);
        var references = ConsumerProject.ReadForgeReferences(path);
        Assert.Equal("1.4.0", references.First(r => r.PackageId == "Micro.Flow.Retry").Version);
        Assert.Equal("1.2.0", references.First(r => r.PackageId == "Micro.Text.Slugify").Version);
    }

    [Fact]
    public void ApplyVersions_VersionIdentique_NeCompteAucunChangement()
    {
        var path = WriteProject(SampleProject);

        var changed = ConsumerProject.ApplyVersions(path, new Dictionary<string, string>
        {
            ["Micro.Flow.Retry"] = "1.0.0",
        });

        Assert.Equal(0, changed);
    }

    [Fact]
    public void ApplyVersions_PackageAbsent_NeCasseRien()
    {
        var path = WriteProject(SampleProject);

        var changed = ConsumerProject.ApplyVersions(path, new Dictionary<string, string>
        {
            ["Micro.Inconnu.Zero"] = "9.9.9",
        });

        Assert.Equal(0, changed);
        Assert.Equal(2, ConsumerProject.ReadForgeReferences(path).Count);
    }

    [Fact]
    public void ApplyVersions_PreserveLesReferencesNonMicroForge()
    {
        var path = WriteProject(SampleProject);
        ConsumerProject.ApplyVersions(path, new Dictionary<string, string> { ["Micro.Flow.Retry"] = "1.4.0" });

        Assert.Contains("Newtonsoft.Json", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Contains("13.0.3", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveProjectFile_AcceptEUnDossierAvecUnSeulProjet()
    {
        WriteProject(SampleProject);
        Assert.EndsWith("Demo.csproj", ConsumerProject.ResolveProjectFile(_directory), StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveProjectFile_AccepteUnCheminDeProjet()
    {
        var path = WriteProject(SampleProject);
        Assert.Equal(Path.GetFullPath(path), ConsumerProject.ResolveProjectFile(path));
    }

    [Fact]
    public void ResolveProjectFile_PlusieursProjets_LeveArgumentException()
    {
        WriteProject(SampleProject);
        File.WriteAllText(Path.Combine(_directory, "Autre.csproj"), SampleProject);

        Assert.Throws<ArgumentException>(() => ConsumerProject.ResolveProjectFile(_directory));
    }

    [Fact]
    public void ResolveProjectFile_CheminInexistant_LeveArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            ConsumerProject.ResolveProjectFile(Path.Combine(_directory, "nulle-part")));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Nettoyage best-effort.
        }
    }
}
