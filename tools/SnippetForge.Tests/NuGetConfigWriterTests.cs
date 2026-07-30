using SnippetForge.Consumers;
using Xunit;

namespace SnippetForge.Tests;

public sealed class NuGetConfigWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "microforge-nugetconfig", Guid.NewGuid().ToString("N"));

    public NuGetConfigWriterTests() => Directory.CreateDirectory(_directory);

    private string ConfigPath => Path.Combine(_directory, "nuget.config");

    private const string FeedPath = @"C:\MicroForge\feed";

    [Fact]
    public void EnsureSource_FichierAbsent_LeCreeAvecLaSource()
    {
        var outcome = NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);

        Assert.Equal(NuGetSourceOutcome.Created, outcome);
        Assert.Equal(FeedPath, NuGetConfigWriter.ReadSource(ConfigPath));
    }

    [Fact]
    public void EnsureSource_CreeUnFichierNuGetValide()
    {
        NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);
        var content = File.ReadAllText(ConfigPath);

        Assert.Contains("<configuration>", content, StringComparison.Ordinal);
        Assert.Contains("<packageSources>", content, StringComparison.Ordinal);
        Assert.Contains("MicroForge", content, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureSource_DeuxFois_EstIdempotent()
    {
        NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);
        var first = File.ReadAllText(ConfigPath);

        var outcome = NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);

        Assert.Equal(NuGetSourceOutcome.AlreadyPresent, outcome);
        Assert.Equal(first, File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void EnsureSource_FichierExistant_PreserveLesAutresSources()
    {
        File.WriteAllText(ConfigPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                <add key="interne" value="https://nuget.interne.local/v3/index.json" />
              </packageSources>
            </configuration>
            """);

        var outcome = NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);
        var content = File.ReadAllText(ConfigPath);

        Assert.Equal(NuGetSourceOutcome.Added, outcome);
        Assert.Contains("nuget.org", content, StringComparison.Ordinal);
        Assert.Contains("nuget.interne.local", content, StringComparison.Ordinal);
        Assert.Equal(FeedPath, NuGetConfigWriter.ReadSource(ConfigPath));
    }

    [Fact]
    public void EnsureSource_CheminModifie_CorrigeLaSource()
    {
        NuGetConfigWriter.EnsureSource(ConfigPath, @"C:\ancien\feed");

        var outcome = NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);

        Assert.Equal(NuGetSourceOutcome.Updated, outcome);
        Assert.Equal(FeedPath, NuGetConfigWriter.ReadSource(ConfigPath));
    }

    [Fact]
    public void EnsureSource_NeDupliquePasLaSource()
    {
        NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);
        NuGetConfigWriter.EnsureSource(ConfigPath, @"C:\autre\feed");
        NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);

        var occurrences = File.ReadAllText(ConfigPath).Split("key=\"MicroForge\"").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void EnsureSource_SansSectionPackageSources_LaCree()
    {
        File.WriteAllText(ConfigPath, """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <config>
                <add key="globalPackagesFolder" value="packages" />
              </config>
            </configuration>
            """);

        NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath);

        Assert.Equal(FeedPath, NuGetConfigWriter.ReadSource(ConfigPath));
        Assert.Contains("globalPackagesFolder", File.ReadAllText(ConfigPath), StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureSource_NomDeSourcePersonnalise()
    {
        NuGetConfigWriter.EnsureSource(ConfigPath, FeedPath, "MaForge");

        Assert.Equal(FeedPath, NuGetConfigWriter.ReadSource(ConfigPath, "MaForge"));
        Assert.Null(NuGetConfigWriter.ReadSource(ConfigPath));
    }

    [Fact]
    public void ReadSource_FichierAbsent_RetourneNull() =>
        Assert.Null(NuGetConfigWriter.ReadSource(Path.Combine(_directory, "absent.config")));

    [Fact]
    public void EnsureSource_ArgumentsVides_LeventArgumentException()
    {
        Assert.Throws<ArgumentException>(() => NuGetConfigWriter.EnsureSource("  ", FeedPath));
        Assert.Throws<ArgumentException>(() => NuGetConfigWriter.EnsureSource(ConfigPath, "  "));
    }

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
