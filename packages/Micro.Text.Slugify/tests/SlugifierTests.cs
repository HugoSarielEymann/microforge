using Xunit;

namespace Micro.Text.Slugify.Tests;

public sealed class SlugifierTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("Écrire du C# : 10 astuces !", "ecrire-du-c-10-astuces")]
    [InlineData("  espaces   multiples  ", "espaces-multiples")]
    [InlineData("déjà-slugifié", "deja-slugifie")]
    [InlineData("!!!", "")]
    [InlineData("", "")]
    public void ToSlug_CasNominaux(string input, string expected) =>
        Assert.Equal(expected, Slugifier.ToSlug(input));

    [Fact]
    public void ToSlug_EstDeterministe()
    {
        const string input = "Même entrée, même sortie ?";
        Assert.Equal(Slugifier.ToSlug(input), Slugifier.ToSlug(input));
    }

    [Fact]
    public void ToSlug_SeparateurPersonnalise()
    {
        var slug = Slugifier.ToSlug("Hello World", new SlugifyOptions { Separator = '_' });
        Assert.Equal("hello_world", slug);
    }

    [Fact]
    public void ToSlug_MaxLength_NeLaissePasDeSeparateurFinal()
    {
        var slug = Slugifier.ToSlug("Écrire du C# : 10 astuces !", new SlugifyOptions { MaxLength = 12 });
        Assert.Equal("ecrire-du-c", slug);
        Assert.True(slug.Length <= 12);
    }

    [Fact]
    public void ToSlug_SansMinuscules_ConserveLaCasse()
    {
        var slug = Slugifier.ToSlug("Hello World", new SlugifyOptions { Lowercase = false });
        Assert.Equal("Hello-World", slug);
    }

    [Theory]
    [InlineData("hello-world", true)]
    [InlineData("Hello World", false)]
    [InlineData("deja-slugifie", true)]
    [InlineData("", true)]
    public void IsSlug_ReconnaitLesChainesDejaSlugifiees(string value, bool expected) =>
        Assert.Equal(expected, Slugifier.IsSlug(value));

    [Fact]
    public void IsSlug_RespecteLesOptions() =>
        Assert.True(Slugifier.IsSlug("hello_world", new SlugifyOptions { Separator = '_' }));

    [Fact]
    public void IsSlug_EntreeNulle_LeveArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() => Slugifier.IsSlug(null!));

    [Fact]
    public void ToSlug_EntreeNulle_LeveArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() => Slugifier.ToSlug(null!));

    [Fact]
    public void ToSlug_MaxLengthInvalide_LeveArgumentOutOfRange() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Slugifier.ToSlug("abc", new SlugifyOptions { MaxLength = 0 }));
}
