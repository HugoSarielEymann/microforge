using SnippetForge.Embeddings;
using Xunit;

namespace SnippetForge.Tests;

public sealed class VectorMathTests
{
    [Fact]
    public void Cosine_VecteursIdentiques_VautUn() =>
        Assert.Equal(1.0, VectorMath.Cosine([1f, 2f, 3f], [1f, 2f, 3f]), precision: 6);

    [Fact]
    public void Cosine_VecteursColineaires_VautUn() =>
        Assert.Equal(1.0, VectorMath.Cosine([1f, 2f, 3f], [2f, 4f, 6f]), precision: 6);

    [Fact]
    public void Cosine_VecteursOrthogonaux_VautZero() =>
        Assert.Equal(0.0, VectorMath.Cosine([1f, 0f], [0f, 1f]), precision: 6);

    [Fact]
    public void Cosine_VecteursOpposes_EstRameneAZero() =>
        Assert.Equal(0.0, VectorMath.Cosine([1f, 0f], [-1f, 0f]), precision: 6);

    [Fact]
    public void Cosine_VecteurNul_VautZero() =>
        Assert.Equal(0.0, VectorMath.Cosine([0f, 0f], [1f, 1f]));

    [Fact]
    public void Cosine_DimensionsIncompatibles_LeveArgumentException() =>
        Assert.Throws<ArgumentException>(() => VectorMath.Cosine([1f, 2f], [1f, 2f, 3f]));

    [Fact]
    public void NormalizeInPlace_ProduitUneNormeUnitaire()
    {
        var vector = new[] { 3f, 4f };
        VectorMath.NormalizeInPlace(vector);

        Assert.Equal(0.6, vector[0], precision: 6);
        Assert.Equal(0.8, vector[1], precision: 6);
        Assert.Equal(1.0, Math.Sqrt((vector[0] * vector[0]) + (vector[1] * vector[1])), precision: 6);
    }

    [Fact]
    public void NormalizeInPlace_VecteurNul_ResteInchange()
    {
        var vector = new[] { 0f, 0f };
        VectorMath.NormalizeInPlace(vector);
        Assert.Equal([0f, 0f], vector);
    }
}
