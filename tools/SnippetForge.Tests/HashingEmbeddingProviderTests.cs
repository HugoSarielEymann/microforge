using SnippetForge.Embeddings;
using Xunit;

namespace SnippetForge.Tests;

public sealed class HashingEmbeddingProviderTests
{
    private readonly HashingEmbeddingProvider _provider = new();

    [Fact]
    public void Embed_EstDeterministe()
    {
        const string text = "Exécute une opération avec relances et backoff exponentiel.";
        Assert.Equal(_provider.Embed(text), _provider.Embed(text));
    }

    [Fact]
    public void Embed_ProduitUnVecteurNormalise()
    {
        var vector = _provider.Embed("slugification d'une chaîne de caractères");
        var norm = Math.Sqrt(vector.Sum(v => (double)v * v));
        Assert.Equal(1.0, norm, precision: 5);
    }

    [Fact]
    public void Embed_RespecteLaDimensionDemandee()
    {
        var provider = new HashingEmbeddingProvider(dimensions: 128);
        Assert.Equal(128, provider.Embed("texte").Length);
        Assert.Equal(128, provider.Dimensions);
    }

    [Fact]
    public void Embed_TexteVide_ProduitUnVecteurNul()
    {
        var vector = _provider.Embed(string.Empty);
        Assert.All(vector, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void Embed_IgnoreLaCasseEtLesAccents()
    {
        var accented = _provider.Embed("Opération Générique");
        var plain = _provider.Embed("operation generique");
        Assert.Equal(1.0, VectorMath.Cosine(accented, plain), precision: 5);
    }

    [Fact]
    public void Embed_TextesProches_PlusSimilairesQueTextesEtrangers()
    {
        var slugify = _provider.Embed("Convertit une chaîne en slug URL avec suppression des diacritiques.");
        var slugifyVariant = _provider.Embed("Transforme une chaîne en slug d'URL, diacritiques supprimés.");
        var retry = _provider.Embed("Exécute une opération asynchrone avec relances et backoff exponentiel.");

        var near = VectorMath.Cosine(slugify, slugifyVariant);
        var far = VectorMath.Cosine(slugify, retry);

        Assert.True(near > far, $"proximité attendue supérieure : {near:0.000} contre {far:0.000}");
    }

    [Fact]
    public void Embed_CaptureLaMorphologie()
    {
        // Les n-grammes de caractères rapprochent une racine commune, ce qu'une
        // comparaison de mots entiers manquerait totalement (similarité nulle).
        var sameRoot = VectorMath.Cosine(_provider.Embed("slugify"), _provider.Embed("slugifier"));
        var unrelated = VectorMath.Cosine(_provider.Embed("slugify"), _provider.Embed("retry"));

        Assert.True(sameRoot > unrelated * 3, $"racine commune {sameRoot:0.000} vs sans rapport {unrelated:0.000}");
    }

    [Fact]
    public void Embed_MotsIsoles_RestentLoinDuSeuilDeDuplication()
    {
        // Deux mots partageant une racine ne suffisent pas à déclencher une alerte de
        // doublon : la détection s'appuie sur des descriptions entières, pas sur un mot.
        var similarity = VectorMath.Cosine(_provider.Embed("slugify"), _provider.Embed("slugifier"));
        Assert.True(similarity < _provider.Thresholds.Warning);
    }

    [Fact]
    public void Thresholds_SontCeuxDeLEspaceLexical() =>
        Assert.Equal(DuplicateThresholds.Lexical, _provider.Thresholds);

    [Fact]
    public async Task EmbedAsync_EquivautALaVersionSynchrone()
    {
        const string text = "découpage en lots";
        Assert.Equal(_provider.Embed(text), await _provider.EmbedAsync(text));
    }

    [Fact]
    public async Task EmbedAsync_TexteNul_LeveArgumentNull() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => _provider.EmbedAsync(null!));

    [Fact]
    public void Constructeur_DimensionTropPetite_LeveArgumentOutOfRange() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new HashingEmbeddingProvider(dimensions: 8));

    [Fact]
    public void Name_TraceLaDimension_PourInvaliderLeCache() =>
        Assert.Equal("hashing-v1-256", new HashingEmbeddingProvider(256).Name);
}
