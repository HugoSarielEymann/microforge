using Xunit;

namespace Micro.Text.FileNameSanitize.Tests;

public sealed class FileNameSanitizeTests
{
    // ==================== Cas nominal ====================

    [Theory]
    [InlineData("Plan de vol", "Plan de vol")]
    [InlineData("Réunion 12/09", "Réunion 12-09")]
    [InlineData("Note : idée", "Note idée")]
    [InlineData("Note: idée", "Note idée")]
    [InlineData("Pourquoi ?", "Pourquoi")]
    [InlineData("<balise>", "balise")]
    [InlineData("a<>b", "a-b")]
    [InlineData("C:\\dossier\\fichier", "C-dossier-fichier")]
    [InlineData("Il a dit \"bonjour\"", "Il a dit bonjour")]
    [InlineData("  espaces   multiples  ", "espaces multiples")]
    [InlineData("Titre sur\ndeux lignes", "Titre sur deux lignes")]
    [InlineData("fin par des points...", "fin par des points")]
    [InlineData("Rendez-vous -", "Rendez-vous -")]
    public void UnTitreDevientUnNomLisible(string titre, string attendu)
    {
        Assert.Equal(attendu, FileNameSanitizer.Sanitize(titre));
    }

    [Fact]
    public void LesPointsDeTeteSontRetiresSaufReglageContraire()
    {
        Assert.Equal("cache", FileNameSanitizer.Sanitize(".cache"));
        Assert.Equal(".cache", FileNameSanitizer.Sanitize(".cache", new FileNameSanitizeOptions { AllowLeadingDot = true }));
    }

    [Fact]
    public void LesPointsInterieursSontConserves()
    {
        Assert.Equal("v1.2 notes.md", FileNameSanitizer.Sanitize("v1.2 notes.md"));
    }

    [Fact]
    public void LeRemplacementEstParametrable()
    {
        FileNameSanitizeOptions vide = new() { Replacement = string.Empty };
        FileNameSanitizeOptions souligne = new() { Replacement = "_" };

        Assert.Equal("1209", FileNameSanitizer.Sanitize("12/09", vide));
        Assert.Equal("12_09", FileNameSanitizer.Sanitize("12/09", souligne));
    }

    [Fact]
    public void DesCaracteresSupplementairesPeuventEtreRefuses()
    {
        FileNameSanitizeOptions wiki = new() { ExtraInvalidCharacters = "#^[]" };

        Assert.Equal("C-notes", FileNameSanitizer.Sanitize("C#notes", wiki));
        Assert.Equal("Lien", FileNameSanitizer.Sanitize("[[Lien]]", wiki));
        Assert.Equal("C#notes", FileNameSanitizer.Sanitize("C#notes"));
    }

    [Fact]
    public void LesBlancsPeuventEtreConserves()
    {
        FileNameSanitizeOptions options = new() { CollapseWhitespace = false };

        Assert.Equal("a   b", FileNameSanitizer.Sanitize("a   b", options));
        Assert.Equal("a b", FileNameSanitizer.Sanitize("a\tb", options));
    }

    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("nul", "nul_")]
    [InlineData("Com1", "Com1_")]
    [InlineData("LPT9.txt", "LPT9_.txt")]
    [InlineData("CONSOLE", "CONSOLE")]
    [InlineData("COM10", "COM10")]
    public void LesNomsReservesRecoiventUnSuffixe(string nom, string attendu)
    {
        Assert.Equal(attendu, FileNameSanitizer.Sanitize(nom));
    }

    [Fact]
    public void LeResultatEstIdempotent()
    {
        string[] titres = ["Réunion 12/09", "CON", "  a..  ", "x<y>z", "Été 🌞", "Note : idée ?"];

        foreach (string titre in titres)
        {
            string une = FileNameSanitizer.Sanitize(titre);
            Assert.Equal(une, FileNameSanitizer.Sanitize(une));
            Assert.True(FileNameSanitizer.IsValid(une));
        }
    }

    // ==================== Longueur ====================

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LaLongueurEstBorneeAuCaractereExact()
    {
        FileNameSanitizeOptions options = new() { MaxLength = 5, Fallback = "x" };

        Assert.Equal("abcde", FileNameSanitizer.Sanitize("abcde", options));
        Assert.Equal("abcde", FileNameSanitizer.Sanitize("abcdef", options));
        Assert.Equal("ab", FileNameSanitizer.Sanitize("ab  cdef", new FileNameSanitizeOptions { MaxLength = 3, Fallback = "x" }));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LaCoupeNeSeparePasUnePaireNiUnAccent()
    {
        FileNameSanitizeOptions trois = new() { MaxLength = 3, Fallback = "x" };

        // « a🌞 » : l'émoji occupe deux unités UTF-16 et ne peut pas être coupé en deux.
        Assert.Equal("a🌞", FileNameSanitizer.Sanitize("a🌞b", trois));
        Assert.Equal("a", FileNameSanitizer.Sanitize("a🌞b", new FileNameSanitizeOptions { MaxLength = 2, Fallback = "x" }));

        // « e » + accent combinant forment un seul caractère perçu.
        Assert.Equal("ae\u0301", FileNameSanitizer.Sanitize("ae\u0301b", trois));
        Assert.Equal("a", FileNameSanitizer.Sanitize("ae\u0301b", new FileNameSanitizeOptions { MaxLength = 2, Fallback = "x" }));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesAlphabetsNonLatinsSontConserves()
    {
        Assert.Equal("日本語のノート", FileNameSanitizer.Sanitize("日本語のノート"));
        Assert.Equal("Été 🌞 2026", FileNameSanitizer.Sanitize("Été 🌞 2026"));
    }

    // ==================== Repli ====================

    [Theory]
    [Trait("hazard", "empty-input")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("???")]
    [InlineData("...")]
    [InlineData("\n\t")]
    public void UnTitreQuiNeLaisseRienDonneLeRepli(string titre)
    {
        Assert.Equal("untitled", FileNameSanitizer.Sanitize(titre));
        Assert.Equal("Sans titre", FileNameSanitizer.Sanitize(titre, new FileNameSanitizeOptions { Fallback = "Sans titre" }));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void UnTitreNulDonneLeRepli()
    {
        Assert.Equal("untitled", FileNameSanitizer.Sanitize(null));
        Assert.False(FileNameSanitizer.IsValid(null));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void LesCaracteresDeControleSontNeutralises()
    {
        Assert.Equal("a b c", FileNameSanitizer.Sanitize("a\0b\u0007c"));
        Assert.Equal("a-b", FileNameSanitizer.Sanitize("a\u007Fb"));
    }

    // ==================== Validité ====================

    [Theory]
    [InlineData("Plan de vol", true)]
    [InlineData("Plan de vol.", false)]
    [InlineData("a/b", false)]
    [InlineData("CON", false)]
    [InlineData(" a", false)]
    [InlineData("", false)]
    public void IsValidDitSiLeNomEstDejaPropre(string nom, bool attendu)
    {
        Assert.Equal(attendu, FileNameSanitizer.IsValid(nom));
    }

    [Fact]
    public void IsValidNeLevePasSurDesReglagesIncoherents()
    {
        Assert.False(FileNameSanitizer.IsValid("a", new FileNameSanitizeOptions { MaxLength = 0 }));
        Assert.False(FileNameSanitizer.IsValid("a", new FileNameSanitizeOptions { Replacement = null! }));
    }

    // ==================== Paramétrage ====================

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LesReglagesIncoherentsSontRefuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { MaxLength = 0 }));
        Assert.Throws<ArgumentNullException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { Replacement = null! }));
        Assert.Throws<ArgumentNullException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { ExtraInvalidCharacters = null! }));
        Assert.Throws<ArgumentNullException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { Fallback = null! }));
        Assert.Throws<ArgumentNullException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { ReservedNameSuffix = null! }));
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { Replacement = "/" }));
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { ReservedNameSuffix = "" }));
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { Fallback = "CON" }));
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { Fallback = "a?" }));
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { Fallback = "" }));
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { Fallback = "long", MaxLength = 3 }));
        Assert.Throws<ArgumentException>(() => FileNameSanitizer.Sanitize("a", new FileNameSanitizeOptions { ExtraInvalidCharacters = "u", Fallback = "untitled" }));
    }

    [Fact]
    public void LesReglagesParDefautSontValides()
    {
        FileNameSanitizeOptions.Default.Validate();
        new FileNameSanitizeOptions { MaxLength = 8 }.Validate();
    }
}
