using Xunit;

namespace Micro.Text.UniqueName.Tests;

public sealed class UniqueNameTests
{
    // ==================== Cas nominal ====================

    [Fact]
    public void UnNomLibreEstRenduTelQuel()
    {
        Assert.Equal("Plan", UniqueNamer.MakeUnique("Plan", ["Autre"]));
    }

    [Fact]
    public void UnNomPrisEstNumeroteAPartirDeDeux()
    {
        Assert.Equal("Sans titre 2", UniqueNamer.MakeUnique("Sans titre", ["Sans titre"]));
        Assert.Equal("Sans titre 4", UniqueNamer.MakeUnique("Sans titre", ["Sans titre", "Sans titre 2", "Sans titre 3"]));
    }

    [Fact]
    public void LesTrousDeNumerotationSontComblesDansLOrdre()
    {
        Assert.Equal("a 2", UniqueNamer.MakeUnique("a", ["a", "a 3"]));
    }

    [Fact]
    public void LaCasseEstIgnoreeParDefaut()
    {
        Assert.Equal("plan 2", UniqueNamer.MakeUnique("plan", ["PLAN"]));
        Assert.Equal("plan", UniqueNamer.MakeUnique("plan", ["PLAN"], new UniqueNameOptions { Comparer = StringComparer.Ordinal }));
    }

    [Fact]
    public void LeFormatEstParametrable()
    {
        UniqueNameOptions explorateur = new() { Format = "{0} ({1})" };

        Assert.Equal("Copie (2)", UniqueNamer.MakeUnique("Copie", ["Copie"], explorateur));
        Assert.Equal("Copie (3)", UniqueNamer.MakeUnique("Copie (2)", ["Copie", "Copie (2)"], explorateur));
    }

    [Fact]
    public void LePremierNumeroEstParametrable()
    {
        Assert.Equal("a 1", UniqueNamer.MakeUnique("a", ["a"], new UniqueNameOptions { FirstNumber = 1 }));
        Assert.Equal("a 0", UniqueNamer.MakeUnique("a", ["a"], new UniqueNameOptions { FirstNumber = 0 }));
    }

    [Fact]
    public void LaNumerotationExistanteEstPoursuivie()
    {
        Assert.Equal("Sans titre 3", UniqueNamer.MakeUnique("Sans titre 2", ["Sans titre 2"]));
        Assert.Equal("Sans titre 2 2", UniqueNamer.MakeUnique("Sans titre 2", ["Sans titre 2"], new UniqueNameOptions { ContinueNumbering = false }));
    }

    [Fact]
    public void UnNumeroInferieurAuPremierRepartDuPremier()
    {
        Assert.Equal("Version 2", UniqueNamer.MakeUnique("Version 0", ["Version 0"]));
    }

    [Fact]
    public void UnNomQuiNEstQuUnNombreNEstPasRelu()
    {
        Assert.Equal("2026 2", UniqueNamer.MakeUnique("2026", ["2026"]));
    }

    [Fact]
    public void LaFonctionDeDisponibiliteEstInterrogeeDansLOrdre()
    {
        List<string> vus = [];

        string nom = UniqueNamer.MakeUnique("n", c =>
        {
            vus.Add(c);
            return vus.Count < 3;
        });

        Assert.Equal("n 3", nom);
        Assert.Equal(["n", "n 2", "n 3"], vus);
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void LesNomsUnicodeSontNumerotesSansAlteration()
    {
        Assert.Equal("Été 🌞 2", UniqueNamer.MakeUnique("Été 🌞", ["été 🌞"]));
    }

    // ==================== Bornes ====================

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void LeNombreDEssaisEstBorne()
    {
        UniqueNameOptions un = new() { MaxAttempts = 1 };
        UniqueNameOptions deux = new() { MaxAttempts = 2 };

        Assert.Throws<InvalidOperationException>(() => UniqueNamer.MakeUnique("a", ["a"], un));
        Assert.Equal("a 2", UniqueNamer.MakeUnique("a", ["a"], deux));
        Assert.Throws<InvalidOperationException>(() => UniqueNamer.MakeUnique("a", ["a", "a 2"], deux));
        Assert.Throws<InvalidOperationException>(() => UniqueNamer.MakeUnique("a", _ => true));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void UnTresGrandNumeroEstPoursuiviSansDeborder()
    {
        string grand = "a " + 999_999_999_999_999_998L.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("a 999999999999999999", UniqueNamer.MakeUnique(grand, [grand]));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void UnNombreTropLongNEstPasReluCommeNumero()
    {
        string enorme = "a " + new string('9', 30);

        Assert.Equal(enorme + " 2", UniqueNamer.MakeUnique(enorme, [enorme]));
    }

    // ==================== Entrées limites et paramétrage ====================

    [Fact]
    [Trait("hazard", "null-input")]
    public void LesEntreesNullesSontRefusees()
    {
        Assert.Throws<ArgumentNullException>(() => UniqueNamer.MakeUnique(null!, ["a"]));
        Assert.Throws<ArgumentNullException>(() => UniqueNamer.MakeUnique("a", (IEnumerable<string?>)null!));
        Assert.Throws<ArgumentNullException>(() => UniqueNamer.MakeUnique("a", (Func<string, bool>)null!));
        Assert.Equal("a 2", UniqueNamer.MakeUnique("a", [null, "a"]));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void UnNomVideEstNumeroteCommeLesAutres()
    {
        Assert.Equal(string.Empty, UniqueNamer.MakeUnique(string.Empty, []));
        Assert.Equal(" 2", UniqueNamer.MakeUnique(string.Empty, [string.Empty]));
    }

    [Fact]
    [Trait("hazard", "empty-collection")]
    public void UneListeVideLaisseLeNomLibre()
    {
        Assert.Equal("Plan", UniqueNamer.MakeUnique("Plan", Array.Empty<string>()));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void DesFormatsInvalidesSontRefuses()
    {
        Assert.Throws<ArgumentException>(() => UniqueNamer.MakeUnique("a", ["a"], new UniqueNameOptions { Format = "{0}" }));
        Assert.Throws<ArgumentException>(() => UniqueNamer.MakeUnique("a", ["a"], new UniqueNameOptions { Format = "{1}" }));
        Assert.Throws<ArgumentException>(() => UniqueNamer.MakeUnique("a", ["a"], new UniqueNameOptions { Format = "{0} {1} {2}" }));
        Assert.Throws<ArgumentNullException>(() => UniqueNamer.MakeUnique("a", ["a"], new UniqueNameOptions { Format = null! }));
    }

    [Fact]
    [Trait("hazard", "negative-value")]
    public void DesBornesIncoherentesSontRefusees()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UniqueNameOptions { FirstNumber = -1 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new UniqueNameOptions { MaxAttempts = 0 }.Validate());
        Assert.Throws<ArgumentNullException>(() => new UniqueNameOptions { Comparer = null! }.Validate());
        UniqueNameOptions.Default.Validate();
    }
}
