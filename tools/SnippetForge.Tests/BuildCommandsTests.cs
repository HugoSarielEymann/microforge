using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Garde-fou sur la promesse centrale du système : le binaire livré est celui qui a
/// été testé. Si tests et empaquetage divergeaient de configuration, on validerait
/// un artefact et on en publierait un autre.
/// </summary>
public sealed class BuildCommandsTests
{
    [Fact]
    public void TestsEtPack_UtilisentLaMemeConfiguration()
    {
        var test = BuildCommands.TestArguments();
        var pack = BuildCommands.PackArguments(@"C:\out");

        Assert.Contains($"-c {BuildCommands.Configuration}", test, StringComparison.Ordinal);
        Assert.Contains($"-c {BuildCommands.Configuration}", pack, StringComparison.Ordinal);
    }

    [Fact]
    public void Configuration_EstRelease_PasDebug() =>
        Assert.Equal("Release", BuildCommands.Configuration);

    [Fact]
    public void TestArguments_CibleLeDossierDeTests() =>
        Assert.StartsWith("test tests ", BuildCommands.TestArguments(), StringComparison.Ordinal);

    [Fact]
    public void PackArguments_CibleSrcEtEncadreLaSortie()
    {
        var pack = BuildCommands.PackArguments(@"C:\dossier avec espaces\out");

        Assert.StartsWith("pack src ", pack, StringComparison.Ordinal);
        Assert.Contains(@"-o ""C:\dossier avec espaces\out""", pack, StringComparison.Ordinal);
    }

    [Fact]
    public void Arguments_AcceptentDesDossiersPersonnalises()
    {
        Assert.Contains("test integration", BuildCommands.TestArguments("integration"), StringComparison.Ordinal);
        Assert.Contains("pack lib", BuildCommands.PackArguments(@"C:\out", "lib"), StringComparison.Ordinal);
    }
}
