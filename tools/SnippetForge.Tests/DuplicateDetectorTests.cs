using SnippetForge.Duplicates;
using SnippetForge.Embeddings;
using Xunit;

namespace SnippetForge.Tests;

public sealed class DuplicateDetectorTests
{
    private static readonly Dictionary<string, string> Versions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Micro.A.One"] = "1.0.0",
        ["Micro.B.Two"] = "2.1.0",
    };

    [Fact]
    public void Detect_VecteurIdentique_EstBloquant()
    {
        var vector = new[] { 1f, 0f, 0f };
        var published = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase) { ["Micro.A.One"] = vector };

        var results = DuplicateDetector.Detect("Micro.New.Thing", vector, published, Versions, DuplicateThresholds.Semantic);

        var hit = Assert.Single(results);
        Assert.Equal("Micro.A.One", hit.PackageId);
        Assert.Equal(DuplicateSeverity.Blocking, hit.Severity);
        Assert.Equal(1.0, hit.Similarity, precision: 5);
        Assert.Equal("1.0.0", hit.Version);
    }

    [Fact]
    public void Detect_VecteurOrthogonal_NeRemonteRien()
    {
        var published = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase) { ["Micro.A.One"] = [0f, 1f, 0f] };
        Assert.Empty(DuplicateDetector.Detect("Micro.New.Thing", [1f, 0f, 0f], published, Versions, DuplicateThresholds.Semantic));
    }

    [Fact]
    public void Detect_SimilariteIntermediaire_EstUnAvertissement()
    {
        // Vecteur à 0.85 de similarité : au-dessus du seuil d'alerte, sous le seuil bloquant.
        var angle = Math.Acos(0.85);
        var candidate = new[] { 1f, 0f };
        var published = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Micro.A.One"] = [(float)Math.Cos(angle), (float)Math.Sin(angle)],
        };

        var hit = Assert.Single(DuplicateDetector.Detect("Micro.New.Thing", candidate, published, Versions, DuplicateThresholds.Semantic));
        Assert.Equal(DuplicateSeverity.Warning, hit.Severity);
    }

    [Fact]
    public void Detect_IgnoreLePackageLuiMeme()
    {
        var vector = new[] { 1f, 0f };
        var published = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase) { ["Micro.A.One"] = vector };

        Assert.Empty(DuplicateDetector.Detect("micro.a.one", vector, published, Versions, DuplicateThresholds.Semantic));
    }

    [Fact]
    public void Detect_IgnoreLesVecteursDeDimensionDifferente()
    {
        var published = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase) { ["Micro.A.One"] = [1f, 0f, 0f] };
        Assert.Empty(DuplicateDetector.Detect("Micro.New.Thing", [1f, 0f], published, Versions, DuplicateThresholds.Semantic));
    }

    [Fact]
    public void Detect_TrieParSimilariteDecroissante()
    {
        var published = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Micro.A.One"] = [(float)Math.Cos(Math.Acos(0.85)), (float)Math.Sin(Math.Acos(0.85))],
            ["Micro.B.Two"] = [1f, 0f],
        };

        var results = DuplicateDetector.Detect("Micro.New.Thing", [1f, 0f], published, Versions, DuplicateThresholds.Semantic);

        Assert.Equal(2, results.Count);
        Assert.Equal("Micro.B.Two", results[0].PackageId);
        Assert.True(results[0].Similarity >= results[1].Similarity);
    }

    [Fact]
    public void DescribeForEmbedding_MelangeIdentifiantTagsEtDescription()
    {
        var text = DuplicateDetector.DescribeForEmbedding(
            "Micro.Text.Slugify", ["text", "slug"], "Convertit en slug.", "# README");

        Assert.Contains("Micro Text Slugify", text, StringComparison.Ordinal);
        Assert.Contains("text, slug", text, StringComparison.Ordinal);
        Assert.Contains("Convertit en slug.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeForEmbedding_TronqueLeReadme()
    {
        var text = DuplicateDetector.DescribeForEmbedding("Micro.A.B", ["t"], "d", new string('x', 5000));
        Assert.True(text.Length < 1000, $"texte trop long : {text.Length}");
    }

    [Fact]
    public void Detect_SurDeVraisEmbeddings_RepereLaReformulation()
    {
        var provider = new HashingEmbeddingProvider();
        var existing = provider.Embed(DuplicateDetector.DescribeForEmbedding(
            "Micro.Text.Slugify", ["text", "slug", "url"],
            "Convertit une chaîne arbitraire en slug URL : suppression des diacritiques, minuscules.",
            string.Empty));

        var candidate = provider.Embed(DuplicateDetector.DescribeForEmbedding(
            "Micro.Text.Sluggify", ["text", "slug", "url"],
            "Convertit une chaîne arbitraire en slug URL : suppression des diacritiques, minuscules.",
            string.Empty));

        var published = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase) { ["Micro.Text.Slugify"] = existing };
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Micro.Text.Slugify"] = "1.0.0" };

        var hit = Assert.Single(DuplicateDetector.Detect(
            "Micro.Text.Sluggify", candidate, published, versions, provider.Thresholds));
        Assert.Equal(DuplicateSeverity.Blocking, hit.Severity);
    }

    [Fact]
    public void Detect_SeuilsInvalides_LeveArgumentOutOfRange()
    {
        var published = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase) { ["Micro.A.One"] = [1f, 0f] };

        Assert.Throws<ArgumentOutOfRangeException>(() => DuplicateDetector.Detect(
            "Micro.New.Thing", [1f, 0f], published, Versions, new DuplicateThresholds(Warning: 0.9, Blocking: 0.5)));
    }

    [Fact]
    public void Detect_SeuilsNuls_LeveArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() => DuplicateDetector.Detect(
            "Micro.New.Thing", [1f, 0f], new Dictionary<string, float[]>(), Versions, null!));

    /// <summary>
    /// Vérifie sur les descriptions réelles de la bibliothèque que la calibration du
    /// repli lexical sépare bien une reformulation d'un besoin distinct.
    /// </summary>
    [Fact]
    public void SeuilsLexicaux_SeparentReformulationEtBesoinDistinct()
    {
        var provider = new HashingEmbeddingProvider();

        float[] Describe(string id, string[] tags, string description) =>
            provider.Embed(DuplicateDetector.DescribeForEmbedding(id, tags, description, string.Empty));

        var slugify = Describe(
            "Micro.Text.Slugify", ["text", "slug", "url", "normalize", "diacritics", "string", "generic"],
            "Convertit une chaîne arbitraire en slug URL : suppression des diacritiques, minuscules, séparateur configurable, longueur maximale.");

        var urlKey = Describe(
            "Micro.Text.UrlKey", ["text", "slug", "url", "normalize", "diacritics", "string", "generic"],
            "Convertit une chaine arbitraire en cle URL : suppression des diacritiques, minuscules, separateur configurable, longueur maximale.");

        var retry = Describe(
            "Micro.Flow.Retry", ["retry", "resilience", "flow", "async", "backoff", "transient", "generic"],
            "Exécute une opération asynchrone avec relances configurables : nombre de tentatives, backoff exponentiel, prédicat de relance.");

        var duplicate = VectorMath.Cosine(slugify, urlKey);
        var distinct = VectorMath.Cosine(slugify, retry);

        Assert.True(duplicate >= DuplicateThresholds.Lexical.Blocking,
            $"reformulation {duplicate:0.000} sous le seuil de blocage {DuplicateThresholds.Lexical.Blocking:0.00}");
        Assert.True(distinct < DuplicateThresholds.Lexical.Warning,
            $"besoin distinct {distinct:0.000} au-dessus du seuil d'alerte {DuplicateThresholds.Lexical.Warning:0.00}");
    }

    [Fact]
    public void Thresholds_LespaceLexicalEstPlusPermissifQueLeSemantique()
    {
        // Les cosinus d'une projection lexicale sont structurellement plus bas :
        // appliquer la calibration sémantique y masquerait tous les doublons.
        Assert.True(DuplicateThresholds.Lexical.Blocking < DuplicateThresholds.Semantic.Blocking);
        Assert.True(DuplicateThresholds.Lexical.Warning < DuplicateThresholds.Semantic.Warning);
    }
}
