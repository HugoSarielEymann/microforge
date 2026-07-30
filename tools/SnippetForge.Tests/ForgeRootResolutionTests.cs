using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// En outil global, « forge » s'exécute depuis le dossier d'un projet quelconque :
/// la remontée de répertoires ne trouve alors aucune racine. Ces tests couvrent le
/// mécanisme de mémorisation qui rend l'outil utilisable de partout.
/// </summary>
public sealed class ForgeRootResolutionTests
{
    [Fact]
    public void IsRoot_ExigeRulesMdEtFeed()
    {
        using var forge = TempForge.Create();
        Assert.True(ForgeRoot.IsRoot(forge.Path));

        File.Delete(Path.Combine(forge.Path, "RULES.md"));
        Assert.False(ForgeRoot.IsRoot(forge.Path));
    }

    [Fact]
    public void IsRoot_CheminVideOuInexistant_EstFaux()
    {
        Assert.False(ForgeRoot.IsRoot("  "));
        Assert.False(ForgeRoot.IsRoot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void Remember_RacineInvalide_LeveArgumentException()
    {
        var directory = Path.Combine(Path.GetTempPath(), "microforge-invalide", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Throws<ArgumentException>(() => ForgeRoot.Remember(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void UserConfigPath_EstSousLeProfilUtilisateur()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.StartsWith(profile, ForgeRoot.UserConfigPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".microforge", ForgeRoot.UserConfigPath, StringComparison.Ordinal);
    }

    [Fact]
    public void At_EtIsRoot_SontCoherents()
    {
        using var forge = TempForge.Create();

        Assert.True(ForgeRoot.IsRoot(forge.Path));
        Assert.Equal(Path.GetFullPath(forge.Path), ForgeRoot.At(forge.Path).Path);
    }
}
