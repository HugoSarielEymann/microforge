using Xunit;

namespace Micro.Text.EditSpan.Tests;

public sealed class EditSpanTests
{
    private static void AssertRoundTrip(string before, string after, EditSpanOptions? options = null)
    {
        EditSpan span = TextEditSpan.Between(before, after, options);
        Assert.Equal(after, TextEditSpan.Apply(before, span, span.InsertedText(after)));
    }

    [Fact]
    public void UneInsertionEstSitueeExactement()
    {
        EditSpan span = TextEditSpan.Between("Le chat dort", "Le petit chat dort");

        Assert.Equal(new EditSpan(3, 0, 6), span);
        Assert.Equal("petit ", span.InsertedText("Le petit chat dort"));
    }

    [Fact]
    public void UneSuppressionEstSitueeExactement()
    {
        Assert.Equal(new EditSpan(2, 3, 0), TextEditSpan.Between("abXYZcd", "abcd"));
    }

    [Fact]
    public void UnRemplacementEstSitueExactement()
    {
        EditSpan span = TextEditSpan.Between("un [[Plan]] ici", "un [[Checklist]] ici");

        Assert.Equal(5, span.Start);
        Assert.Equal("Checklist", span.InsertedText("un [[Checklist]] ici"));
        Assert.Equal(4, span.RemovedLength);
    }

    [Fact]
    public void DesModificationsEloigneesSontEnglobees()
    {
        EditSpan span = TextEditSpan.Between("A-----B", "a-----b");

        Assert.Equal(new EditSpan(0, 7, 7), span);
    }

    [Fact]
    public void DeuxVersionsIdentiquesDonnentUneZoneVide()
    {
        EditSpan span = TextEditSpan.Between("même", "même");

        Assert.True(span.IsEmpty);
        Assert.False(new EditSpan(0, 1, 0).IsEmpty);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneRepetitionAmbigueResteCoherente()
    {
        EditSpan span = TextEditSpan.Between("aaa", "aaaa");

        Assert.Equal(3, span.Start);
        Assert.Equal(1, span.InsertedLength);
        AssertRoundTrip("aaa", "aaaa");
        AssertRoundTrip("aaaa", "aa");
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnePaireDeSubstitutionNEstJamaisCoupee()
    {
        // Même demi-paire haute, demi-paire basse différente : 🌟 (D83C DF1F) devient 🌠 (D83C DF20).
        EditSpan span = TextEditSpan.Between("a🌟b", "a🌠b");

        Assert.Equal(1, span.Start);
        Assert.Equal(2, span.RemovedLength);
        Assert.Equal("🌠", span.InsertedText("a🌠b"));

        EditSpan brut = TextEditSpan.Between("a🌟b", "a🌠b", new EditSpanOptions { KeepUnitsWhole = false });
        Assert.Equal(2, brut.Start);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnSautDeLigneWindowsNEstJamaisCoupe()
    {
        EditSpan span = TextEditSpan.Between("a\r\nb", "a\nb");

        Assert.Equal(1, span.Start);
        AssertRoundTrip("a\r\nb", "a\nb");
        AssertRoundTrip("x\r\n", "x\r\ny\r\n");
    }

    [Theory]
    [Trait("hazard", "empty-input")]
    [InlineData("", "")]
    [InlineData("", "tout")]
    [InlineData("tout", "")]
    public void LesVersionsVidesSontGerees(string before, string after)
    {
        EditSpan span = TextEditSpan.Between(before, after);

        Assert.Equal(0, span.Start);
        Assert.Equal(before.Length, span.RemovedLength);
        Assert.Equal(after.Length, span.InsertedLength);
        AssertRoundTrip(before, after);
    }

    [Fact]
    public void LAllerRetourEstExactSurDesCasVaries()
    {
        string[] versions = ["", "a", "abc", "abcabc", "xbcabz", "# Titre\n\nTexte", "# Titre\n\nTexte modifié\n", "🌌🌟", "🌟🌌"];

        foreach (string before in versions)
        {
            foreach (string after in versions)
            {
                AssertRoundTrip(before, after);
            }
        }
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void LesEntreesNullesSontRefusees()
    {
        Assert.Throws<ArgumentNullException>(() => TextEditSpan.Between(null!, "a"));
        Assert.Throws<ArgumentNullException>(() => TextEditSpan.Between("a", null!));
        Assert.Throws<ArgumentNullException>(() => TextEditSpan.Apply(null!, default, ""));
        Assert.Throws<ArgumentNullException>(() => TextEditSpan.Apply("a", default, null!));
        Assert.Throws<ArgumentNullException>(() => new EditSpan(0, 0, 0).InsertedText(null!));
    }

    [Fact]
    [Trait("hazard", "negative-value")]
    public void UneZoneHorsDuTexteEstRefusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextEditSpan.Apply("abc", new EditSpan(-1, 0, 0), ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextEditSpan.Apply("abc", new EditSpan(0, -1, 0), ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextEditSpan.Apply("abc", new EditSpan(2, 5, 0), ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextEditSpan.Apply("abc", new EditSpan(0, 0, 2), "x"));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void UneZoneExtremeNeDebordePas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextEditSpan.Apply("abc", new EditSpan(int.MaxValue, int.MaxValue, 0), ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditSpan(int.MaxValue, 0, 1).InsertedText("abc"));
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        EditSpanOptions.Default.Validate();
        Assert.True(EditSpanOptions.Default.KeepUnitsWhole);
    }
}
