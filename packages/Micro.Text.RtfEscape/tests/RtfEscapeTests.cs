using System.Text;
using Xunit;

namespace Micro.Text.RtfEscape.Tests;

public sealed class RtfEscapeTests
{
    [Fact]
    public void LeTexteAsciiOrdinairePasseTelQuel()
    {
        Assert.Equal("Bonjour, monde !", RtfEscaper.Escape("Bonjour, monde !"));
    }

    [Fact]
    public void LaSyntaxeRtfEstProtegee()
    {
        Assert.Equal(@"a\\b \{c\}", RtfEscaper.Escape(@"a\b {c}"));
    }

    [Fact]
    public void TabulationsEtFinsDeLigneDeviennentDesMotsDeControle()
    {
        Assert.Equal(@"a\tab b\par c\par d\par e", RtfEscaper.Escape("a\tb\nc\r\nd\re"));
    }

    [Fact]
    public void LaFinDeLigneEstParametrable()
    {
        Assert.Equal(@"a\line b", RtfEscaper.Escape("a\nb", new RtfEscapeOptions { NewLine = "\\line " }));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesCaracteresNonAsciiSontEcritsEnUnicodeSigne()
    {
        Assert.Equal(@"\u233?t\u233?", RtfEscaper.Escape("été"));
        Assert.Equal(@"\u8364?", RtfEscaper.Escape("€"));
        // U+FFFD dépasse 32 767 : l'entier signé de la norme RTF est négatif.
        Assert.Equal(@"\u-3?", RtfEscaper.Escape("\uFFFD"));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void UnePaireDeSubstitutionDonneDeuxUnites()
    {
        Assert.Equal(@"\u-10180?\u-8417?", RtfEscaper.Escape("🌟"));
    }

    [Fact]
    public void LeCaractereDeRepliEstParametrable()
    {
        Assert.Equal(@"\u233*", RtfEscaper.Escape("é", new RtfEscapeOptions { Fallback = '*' }));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void LesAutresCaracteresDeControleSontEcartes()
    {
        Assert.Equal("ab", RtfEscaper.Escape("a\0\u0007\u001B\u007Fb"));
    }

    [Fact]
    public void AppendAjouteAuDocumentEnConstruction()
    {
        StringBuilder document = new(@"{\rtf1 ");

        RtfEscaper.Append(document, "{x}");
        RtfEscaper.Append(document, "é!".AsSpan(1, 1));

        Assert.Equal(@"{\rtf1 \{x\}!", document.ToString());
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnTexteVideDonneUneChaineVide()
    {
        Assert.Equal(string.Empty, RtfEscaper.Escape(string.Empty));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneFinDeLigneFinaleEstConvertie()
    {
        Assert.Equal(@"a\par ", RtfEscaper.Escape("a\r"));
        Assert.Equal(@"\par \par ", RtfEscaper.Escape("\n\n"));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void LesEntreesNullesSontRefusees()
    {
        Assert.Throws<ArgumentNullException>(() => RtfEscaper.Escape(null!));
        Assert.Throws<ArgumentNullException>(() => RtfEscaper.Append(null!, "a"));
        Assert.Throws<ArgumentNullException>(() => RtfEscaper.Append(new StringBuilder(), (string)null!));
        Assert.Throws<ArgumentNullException>(() => RtfEscaper.Append(null!, "a".AsSpan()));
        Assert.Throws<ArgumentNullException>(() => new RtfEscapeOptions { NewLine = null! }.Validate());
    }

    [Theory]
    [InlineData('\\')]
    [InlineData('{')]
    [InlineData('}')]
    [InlineData('\n')]
    [InlineData('é')]
    public void UnRepliQuiCasseraitLaSyntaxeEstRefuse(char fallback)
    {
        Assert.Throws<ArgumentException>(() => RtfEscaper.Escape("é", new RtfEscapeOptions { Fallback = fallback }));
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        RtfEscapeOptions.Default.Validate();
        Assert.Equal("\\par ", RtfEscapeOptions.Default.NewLine);
        Assert.Equal('?', RtfEscapeOptions.Default.Fallback);
    }
}
