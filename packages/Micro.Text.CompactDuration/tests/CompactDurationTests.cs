using Xunit;

namespace Micro.Text.CompactDuration.Tests;

public sealed class CompactDurationParserTests
{
    // --- Cas nominaux ---

    [Theory]
    [InlineData("30s",      0, 0, 30)]
    [InlineData("5m",       0, 5,  0)]
    [InlineData("2h",       2, 0,  0)]
    [InlineData("2h30m",    2, 30, 0)]
    [InlineData("1h15m30s", 1, 15, 30)]
    [InlineData("0s",       0, 0,  0)]
    [InlineData("1h0m0s",   1, 0,  0)]
    public void Parse_FormatValide_RetourneTimeSpanAttendu(string input, int h, int m, int s)
    {
        var result = CompactDurationParser.Parse(input);

        Assert.Equal(new TimeSpan(h, m, s), result);
    }

    [Fact]
    public void Parse_AvecEspacesEnBordure_Reussit()
    {
        var result = CompactDurationParser.Parse("  5m  ");

        Assert.Equal(TimeSpan.FromMinutes(5), result);
    }

    // --- Cas limites / erreurs ---

    [Trait("hazard", "null-input")]
    [Fact]
    public void Parse_ChaineNull_LeveArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CompactDurationParser.Parse(null!));
    }

    [Trait("hazard", "empty-input")]
    [Trait("hazard", "malformed-input")]
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("5")]
    [InlineData("1m2h")]
    [InlineData("-1s")]
    public void Parse_FormatInvalide_LeveFormatException(string input)
    {
        Assert.Throws<FormatException>(() => CompactDurationParser.Parse(input));
    }

    // --- TryParse ---

    [Fact]
    public void TryParse_FormatValide_RetourneTrue()
    {
        var ok = CompactDurationParser.TryParse("2h30m", out var result);

        Assert.True(ok);
        Assert.Equal(new TimeSpan(2, 30, 0), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xyz")]
    public void TryParse_FormatInvalide_RetourneFalse(string? input)
    {
        var ok = CompactDurationParser.TryParse(input, out var result);

        Assert.False(ok);
        Assert.Equal(TimeSpan.Zero, result);
    }

    // --- Débordement numérique (régression 1.0.1) ---
    //
    // TryParse propageait OverflowException depuis int.Parse : un appelant qui se fiait
    // à la signature plantait. Un TryXxx ne lève jamais, quelle que soit l'entrée.

    [Trait("hazard", "numeric-overflow")]
    [Theory]
    [InlineData("99999999999h")]
    [InlineData("99999999999m")]
    [InlineData("99999999999s")]
    [InlineData("2147483648h")]
    public void TryParse_ValeurHorsBornesDeInt_RetourneFalseSansLever(string input)
    {
        var ok = CompactDurationParser.TryParse(input, out var result);

        Assert.False(ok);
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void TryParse_CombinaisonDepassantTimeSpan_RetourneFalseSansLever()
    {
        // Chaque unité tient dans un int, mais leur somme excède TimeSpan.MaxValue.
        var ok = CompactDurationParser.TryParse("2000000000h2000000000m", out var result);

        Assert.False(ok);
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Theory]
    [InlineData("99999999999h")]
    [InlineData("2000000000h2000000000m")]
    public void Parse_ValeurHorsBornes_LeveFormatExceptionCommeDocumente(string input)
    {
        // Le contrat XML annonce FormatException : c'est ce que l'appelant intercepte.
        Assert.Throws<FormatException>(() => CompactDurationParser.Parse(input));
    }

    [Fact]
    public void ParseEtTryParse_SaccordentSurLesMemesEntrees()
    {
        string[] entrees = ["30s", "2h30m", "abc", "", "99999999999h", "1h15m30s"];

        foreach (var entree in entrees)
        {
            var reussite = CompactDurationParser.TryParse(entree, out var valeur);

            if (reussite)
            {
                Assert.Equal(valeur, CompactDurationParser.Parse(entree));
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => CompactDurationParser.Parse(entree));
            }
        }
    }
}