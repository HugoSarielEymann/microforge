using Xunit;

namespace Micro.Text.LineEndings.Tests;

public sealed class LineEndingsTests
{
    private const char Lf = '\n';
    private const char Cr = '\r';

    [Fact]
    public void ChaqueConventionEstComptee()
    {
        string text = "a" + Lf + "b" + Cr + Lf + "c" + Cr + "d" + Cr + Lf + "e";

        LineEndingProfile profil = LineEndingDetector.Analyze(text);

        Assert.Equal(1, profil.Lf);
        Assert.Equal(2, profil.CrLf);
        Assert.Equal(1, profil.Cr);
        Assert.Equal(4, profil.Total);
        Assert.True(profil.IsMixed);
        Assert.Equal(LineEnding.CrLf, profil.Dominant);
    }

    [Theory]
    [InlineData("a\nb\nc", LineEnding.Lf)]
    [InlineData("a\r\nb\r\nc", LineEnding.CrLf)]
    [InlineData("a\rb\rc", LineEnding.Cr)]
    [InlineData("a\nb\nc\r\nd", LineEnding.Lf)]
    public void LaConventionDominanteEstDetectee(string text, LineEnding attendu)
    {
        Assert.Equal(attendu, LineEndingDetector.Detect(text));
    }

    [Fact]
    public void AEgaliteCrLfPuisLfLEmportent()
    {
        Assert.Equal(LineEnding.CrLf, LineEndingDetector.Analyze("a\nb\r\nc").Dominant);
        Assert.Equal(LineEnding.Lf, LineEndingDetector.Analyze("a\nb\rc").Dominant);
    }

    [Fact]
    public void UnTexteHomogeneNEstPasMelange()
    {
        Assert.False(LineEndingDetector.Analyze("a\r\nb\r\n").IsMixed);
        Assert.False(LineEndingDetector.Analyze("sans fin de ligne").IsMixed);
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void SansFinDeLigneLeRepliEstRendu()
    {
        Assert.Equal(LineEnding.None, LineEndingDetector.Analyze(string.Empty).Dominant);
        Assert.Equal(LineEnding.Lf, LineEndingDetector.Detect(string.Empty));
        Assert.Equal(LineEnding.CrLf, LineEndingDetector.Detect("une ligne", new LineEndingOptions { Fallback = LineEnding.CrLf }));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UnRetourChariotFinalEstUnCrIsole()
    {
        LineEndingProfile profil = LineEndingDetector.Analyze("a\r");

        Assert.Equal(1, profil.Cr);
        Assert.Equal(0, profil.CrLf);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void UneSuiteDeRetoursEtDeSautsEstDecoupeeSansChevauchement()
    {
        LineEndingProfile profil = LineEndingDetector.Analyze("\r\r\n\n\r\n\r");

        Assert.Equal(1, profil.Lf);
        Assert.Equal(2, profil.CrLf);
        Assert.Equal(2, profil.Cr);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesSeparateursUnicodeNeSontPasDesFinsDeLigneDeFichier()
    {
        const char SeparateurDeLigne = (char)0x2028;
        const char LigneSuivante = (char)0x85;
        string text = "a" + SeparateurDeLigne + "b" + LigneSuivante + "c" + char.ConvertFromUtf32(0x1F31F) + Lf + "d";

        LineEndingProfile profil = LineEndingDetector.Analyze(text);

        Assert.Equal(1, profil.Total);
        Assert.Equal(LineEnding.Lf, profil.Dominant);
    }

    [Theory]
    [InlineData(LineEnding.Lf, "\n")]
    [InlineData(LineEnding.CrLf, "\r\n")]
    [InlineData(LineEnding.Cr, "\r")]
    public void ChaqueConventionASaSequence(LineEnding ending, string sequence)
    {
        Assert.Equal(sequence, LineEndingDetector.ToSequence(ending));
    }

    [Fact]
    public void LaSequencePermetDeReecrireAvecLaConventionDOrigine()
    {
        string original = "a\r\nb\r\n";
        string interne = original.ReplaceLineEndings("\n") + "c\n";

        string reecrit = interne.ReplaceLineEndings(LineEndingDetector.ToSequence(LineEndingDetector.Detect(original)));

        Assert.Equal("a\r\nb\r\nc\r\n", reecrit);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void UneConventionInexistanteNAPasDeSequence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LineEndingDetector.ToSequence(LineEnding.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => LineEndingDetector.ToSequence((LineEnding)42));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnTexteNulEstRefuse()
    {
        Assert.Throws<ArgumentNullException>(() => LineEndingDetector.Analyze(null!));
        Assert.Throws<ArgumentNullException>(() => LineEndingDetector.Detect(null!));
    }

    [Fact]
    public void UnRepliSansConventionEstRefuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineEndingOptions { Fallback = LineEnding.None }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => LineEndingDetector.Detect("a", new LineEndingOptions { Fallback = (LineEnding)9 }));
        LineEndingOptions.Default.Validate();
    }
}
