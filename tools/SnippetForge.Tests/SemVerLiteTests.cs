using Xunit;

namespace SnippetForge.Tests;

public sealed class SemVerLiteTests
{
    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("0.0.0", true)]
    [InlineData("10.20.30", true)]
    [InlineData("1.0.0-beta", true)]
    [InlineData("1.0", false)]
    [InlineData("1.0.0.0", false)]
    [InlineData("v1.0.0", false)]
    [InlineData("", false)]
    public void IsValid_ReconnaitLesVersionsSemVer(string version, bool expected) =>
        Assert.Equal(expected, SemVerLite.IsValid(version));

    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.2.0", "1.10.0", -1)]
    [InlineData("2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0-beta", "1.0.0", -1)]
    public void Compare_OrdonneCorrectement(string left, string right, int expectedSign) =>
        Assert.Equal(expectedSign, Math.Sign(SemVerLite.Compare(left, right)));

    [Fact]
    public void Compare_CompareNumeriquementEtPasAlphabetiquement() =>
        Assert.True(SemVerLite.Compare("1.10.0", "1.9.0") > 0);

    [Fact]
    public void Latest_RetourneLaPlusHaute() =>
        Assert.Equal("2.0.0", SemVerLite.Latest(["1.0.0", "2.0.0", "1.10.3"]));

    [Fact]
    public void Latest_ListeVide_RetourneNull() =>
        Assert.Null(SemVerLite.Latest([]));

    [Theory]
    [InlineData("1.0.0", "2.0.0", BumpLevel.Major)]
    [InlineData("1.0.0", "1.1.0", BumpLevel.Minor)]
    [InlineData("1.0.0", "1.0.1", BumpLevel.Patch)]
    [InlineData("1.2.3", "2.0.0", BumpLevel.Major)]
    public void BumpBetween_IdentifieLeNiveau(string previous, string candidate, BumpLevel expected) =>
        Assert.Equal(expected, SemVerLite.BumpBetween(previous, candidate));

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("2.0.0", "1.9.9")]
    public void BumpBetween_VersionNonSuperieure_RetourneNull(string previous, string candidate) =>
        Assert.Null(SemVerLite.BumpBetween(previous, candidate));

    [Theory]
    [InlineData("1.2.3", BumpLevel.Major, "2.0.0")]
    [InlineData("1.2.3", BumpLevel.Minor, "1.3.0")]
    [InlineData("1.2.3", BumpLevel.Patch, "1.2.4")]
    public void Bump_RemetAZeroLesRangsInferieurs(string version, BumpLevel level, string expected) =>
        Assert.Equal(expected, SemVerLite.Bump(version, level));

    [Fact]
    public void SameMajor_DistingueLesContrats()
    {
        Assert.True(SemVerLite.SameMajor("1.0.0", "1.99.99"));
        Assert.False(SemVerLite.SameMajor("1.99.99", "2.0.0"));
    }

    [Fact]
    public void BumpLevel_EstOrdonneParGravite()
    {
        Assert.True(BumpLevel.Patch < BumpLevel.Minor);
        Assert.True(BumpLevel.Minor < BumpLevel.Major);
    }
}
