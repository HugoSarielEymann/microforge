using SnippetForge.Lifecycle;
using Xunit;

namespace SnippetForge.Tests;

public sealed class DeprecationRegistryTests
{
    [Theory]
    [InlineData("*", "1.0.0", true)]
    [InlineData("*", "9.9.9", true)]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("<2.0.0", "1.9.9", true)]
    [InlineData("<2.0.0", "2.0.0", false)]
    [InlineData("<=2.0.0", "2.0.0", true)]
    [InlineData("<=2.0.0", "2.0.1", false)]
    public void Matches_EvalueLesSpecifications(string spec, string version, bool expected) =>
        Assert.Equal(expected, DeprecationRegistry.Matches(spec, version));

    [Theory]
    [InlineData("*", true)]
    [InlineData("1.2.3", true)]
    [InlineData("<1.2.3", true)]
    [InlineData("<=1.2.3", true)]
    [InlineData("~1.2.3", false)]
    [InlineData(">1.2.3", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidSpec_RejetteLesFormesNonSupportees(string? spec, bool expected) =>
        Assert.Equal(expected, DeprecationRegistry.IsValidSpec(spec));

    [Fact]
    public void Deprecate_PuisFor_RetourneLaDeclaration()
    {
        using var forge = TempForge.Create();
        var registry = DeprecationRegistry.Load(forge.Root);

        registry.Deprecate("Micro.A.One", "<2.0.0", "Fuite mémoire", "Micro.A.Two");

        var applicable = Assert.Single(registry.For("Micro.A.One", "1.5.0"));
        Assert.Equal("Fuite mémoire", applicable.Reason);
        Assert.Equal("Micro.A.Two", applicable.Replacement);
        Assert.Empty(registry.For("Micro.A.One", "2.0.0"));
    }

    [Fact]
    public void Deprecate_EstPersisteEtRelu()
    {
        using var forge = TempForge.Create();

        var registry = DeprecationRegistry.Load(forge.Root);
        registry.Deprecate("Micro.A.One", "*", "Obsolète", null);
        registry.Save();

        var reloaded = DeprecationRegistry.Load(forge.Root);
        Assert.Single(reloaded.For("Micro.A.One", "3.2.1"));
    }

    [Fact]
    public void Deprecate_DeuxFoisLaMemeSpec_NeDupliquePas()
    {
        using var forge = TempForge.Create();
        var registry = DeprecationRegistry.Load(forge.Root);

        registry.Deprecate("Micro.A.One", "*", "Première raison", null);
        registry.Deprecate("Micro.A.One", "*", "Raison corrigée", null);

        var applicable = Assert.Single(registry.For("Micro.A.One", "1.0.0"));
        Assert.Equal("Raison corrigée", applicable.Reason);
    }

    [Fact]
    public void Deprecate_SpecInvalide_LeveArgumentException()
    {
        using var forge = TempForge.Create();
        var registry = DeprecationRegistry.Load(forge.Root);

        Assert.Throws<ArgumentException>(() => registry.Deprecate("Micro.A.One", ">1.0.0", "raison", null));
    }

    [Fact]
    public void Deprecate_RaisonVide_LeveArgumentException()
    {
        using var forge = TempForge.Create();
        var registry = DeprecationRegistry.Load(forge.Root);

        Assert.Throws<ArgumentException>(() => registry.Deprecate("Micro.A.One", "*", "  ", null));
    }

    [Fact]
    public void Undeprecate_RetireLesDeclarations()
    {
        using var forge = TempForge.Create();
        var registry = DeprecationRegistry.Load(forge.Root);
        registry.Deprecate("Micro.A.One", "*", "raison", null);
        registry.Deprecate("Micro.B.Two", "*", "raison", null);

        Assert.Equal(1, registry.Undeprecate("Micro.A.One"));
        Assert.Empty(registry.For("Micro.A.One", "1.0.0"));
        Assert.Single(registry.For("Micro.B.Two", "1.0.0"));
    }

    [Fact]
    public void Undeprecate_CiblageParSpec()
    {
        using var forge = TempForge.Create();
        var registry = DeprecationRegistry.Load(forge.Root);
        registry.Deprecate("Micro.A.One", "<2.0.0", "raison", null);
        registry.Deprecate("Micro.A.One", "3.0.0", "autre raison", null);

        Assert.Equal(1, registry.Undeprecate("Micro.A.One", "3.0.0"));
        Assert.Single(registry.For("Micro.A.One", "1.0.0"));
        Assert.Empty(registry.For("Micro.A.One", "3.0.0"));
    }

    [Fact]
    public void IsPackageAffected_SignaleUnPackageTouche()
    {
        using var forge = TempForge.Create();
        var registry = DeprecationRegistry.Load(forge.Root);
        registry.Deprecate("Micro.A.One", "1.0.0", "raison", null);

        Assert.True(registry.IsPackageAffected("micro.a.one"));
        Assert.False(registry.IsPackageAffected("Micro.B.Two"));
    }

    [Fact]
    public void Load_RegistreAbsent_RetourneVide()
    {
        using var forge = TempForge.Create();
        Assert.Empty(DeprecationRegistry.Load(forge.Root).Entries);
    }
}
