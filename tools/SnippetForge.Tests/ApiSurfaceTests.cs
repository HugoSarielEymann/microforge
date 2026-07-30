using SnippetForge.Api;
using Xunit;

namespace SnippetForge.Tests;

public sealed class ApiSurfaceTests
{
    private static ApiSurface Surface(string version, params string[] members) =>
        new("Micro.A.One", version, members);

    [Fact]
    public void Digest_EstStableEtIndependantDeLOrdre()
    {
        var first = Surface("1.0.0", "method A", "method B");
        var second = Surface("1.0.0", "method B", "method A");

        Assert.Equal(first.Digest, second.Digest);
        Assert.StartsWith("sha256:", first.Digest, StringComparison.Ordinal);
    }

    [Fact]
    public void Digest_ChangeAvecLeContrat() =>
        Assert.NotEqual(Surface("1.0.0", "method A").Digest, Surface("1.0.0", "method A", "method B").Digest);

    [Fact]
    public void Between_ContratsIdentiques_NeDemandeQuUnPatch()
    {
        var diff = ApiDiff.Between(Surface("1.0.0", "method A"), Surface("1.0.1", "method A"));

        Assert.True(diff.IsIdentical);
        Assert.False(diff.IsBreaking);
        Assert.Equal(BumpLevel.Patch, diff.RequiredBump);
    }

    [Fact]
    public void Between_AjoutSeul_ExigeUnMineur()
    {
        var diff = ApiDiff.Between(Surface("1.0.0", "method A"), Surface("1.1.0", "method A", "method B"));

        Assert.Equal(["method B"], diff.Added);
        Assert.Empty(diff.Removed);
        Assert.False(diff.IsBreaking);
        Assert.Equal(BumpLevel.Minor, diff.RequiredBump);
    }

    [Fact]
    public void Between_Retrait_ExigeUnMajeur()
    {
        var diff = ApiDiff.Between(Surface("1.0.0", "method A", "method B"), Surface("2.0.0", "method A"));

        Assert.Equal(["method B"], diff.Removed);
        Assert.True(diff.IsBreaking);
        Assert.Equal(BumpLevel.Major, diff.RequiredBump);
    }

    [Fact]
    public void Between_SignatureModifiee_EstUneRupture()
    {
        // Changer un type de paramètre = retrait de l'ancienne signature + ajout de la nouvelle.
        var diff = ApiDiff.Between(
            Surface("1.0.0", "method Foo(System.Int32) : System.Void"),
            Surface("2.0.0", "method Foo(System.Int64) : System.Void"));

        Assert.True(diff.IsBreaking);
        Assert.Equal(BumpLevel.Major, diff.RequiredBump);
        Assert.Single(diff.Added);
        Assert.Single(diff.Removed);
    }

    [Fact]
    public void Between_ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ApiDiff.Between(null!, Surface("1.0.0")));
        Assert.Throws<ArgumentNullException>(() => ApiDiff.Between(Surface("1.0.0"), null!));
    }
}
