using SnippetForge.Api;
using SnippetForge.Duplicates;
using SnippetForge.Embeddings;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// La détection par description se contourne en reformulant. Ces tests vérifient que
/// la forme du contrat public, elle, ne se reformule pas.
/// </summary>
public sealed class ApiSimilarityTests
{
    private static ApiSurface Slugify() => new("Micro.Text.Slugify", "1.0.0",
    [
        "type Micro.Text.Slugify.Slugifier",
        "method Micro.Text.Slugify.Slugifier.ToSlug(System.String, Micro.Text.Slugify.SlugifyOptions) : System.String",
        "type Micro.Text.Slugify.SlugifyOptions",
        "property Micro.Text.Slugify.SlugifyOptions.Separator : System.Char { get;set; }",
    ]);

    /// <summary>Même capacité, tous les identifiants renommés.</summary>
    private static ApiSurface UrlKey() => new("Micro.Text.UrlKey", "1.0.0",
    [
        "type Micro.Text.UrlKey.UrlKeyBuilder",
        "method Micro.Text.UrlKey.UrlKeyBuilder.Create(System.String, Micro.Text.UrlKey.UrlKeyOptions) : System.String",
        "type Micro.Text.UrlKey.UrlKeyOptions",
        "property Micro.Text.UrlKey.UrlKeyOptions.Delimiter : System.Char { get;set; }",
    ]);

    private static ApiSurface Retry() => new("Micro.Flow.Retry", "1.0.0",
    [
        "type Micro.Flow.Retry.RetryExecutor",
        "method Micro.Flow.Retry.RetryExecutor.ExecuteAsync`1(System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<TResult>>, Micro.Flow.Retry.RetryOptions, Microsoft.Extensions.Logging.ILogger, System.Threading.CancellationToken) : System.Threading.Tasks.Task<TResult>",
        "type Micro.Flow.Retry.RetryOptions",
        "property Micro.Flow.Retry.RetryOptions.MaxAttempts : System.Int32 { get;set; }",
    ]);

    [Fact]
    public void ContratIdentiqueASoiMeme_VautUn() =>
        Assert.Equal(1.0, ApiSimilarity.Compare(Slugify(), Slugify()), precision: 6);

    [Fact]
    public void MemeCapaciteRenommee_ResteTresProche()
    {
        // C'est le cas que la détection par description rate.
        var similarity = ApiSimilarity.Compare(Slugify(), UrlKey());

        Assert.True(similarity >= DuplicateDetector.ApiEscalationThreshold,
            $"forme trop éloignée : {similarity:0.000}");
    }

    [Fact]
    public void CapacitesDifferentes_RestentEloignees()
    {
        var similarity = ApiSimilarity.Compare(Slugify(), Retry());

        Assert.True(similarity < DuplicateDetector.ApiEscalationThreshold,
            $"faux positif : {similarity:0.000}");
    }

    [Fact]
    public void ContratVide_VautZero() =>
        Assert.Equal(0, ApiSimilarity.Compare(new ApiSurface("Micro.A.One", "1.0.0", []), Slugify()));

    [Fact]
    public void Shapes_IgnoreLesDeclarationsDeType()
    {
        // « type X » ne dit rien de la capacité : seules les signatures comptent.
        var shapes = ApiSimilarity.Shapes(Slugify());
        Assert.DoesNotContain(shapes, s => s.StartsWith("type", StringComparison.Ordinal));
    }

    [Fact]
    public void Shapes_EffaceLesNomsPropresAuPackage()
    {
        var left = ApiSimilarity.Shapes(Slugify());
        var right = ApiSimilarity.Shapes(UrlKey());

        Assert.NotEmpty(left.Intersect(right, StringComparer.Ordinal));
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ApiSimilarity.Compare(null!, Slugify()));
        Assert.Throws<ArgumentNullException>(() => ApiSimilarity.Compare(Slugify(), null!));
        Assert.Throws<ArgumentNullException>(() => ApiSimilarity.Shapes(null!));
    }
}

public sealed class DuplicateEscalationTests
{
    private static DuplicateCandidate Warning(string id = "Micro.A.One") =>
        new(id, "1.0.0", Similarity: 0.60, DuplicateSeverity.Warning);

    private static DuplicateCandidate Blocking(string id = "Micro.B.Two") =>
        new(id, "1.0.0", Similarity: 0.95, DuplicateSeverity.Blocking);

    [Fact]
    public void ProximiteAvecContratIdentique_DevientBloquante()
    {
        var escalated = DuplicateDetector.Escalate([Warning()], _ => 0.9);

        var candidate = Assert.Single(escalated);
        Assert.Equal(DuplicateSeverity.Blocking, candidate.Severity);
        Assert.Equal(0.9, candidate.ApiSimilarity);
    }

    [Fact]
    public void ProximiteAvecContratDifferent_ResteUnAvertissement()
    {
        var candidate = Assert.Single(DuplicateDetector.Escalate([Warning()], _ => 0.1));

        Assert.Equal(DuplicateSeverity.Warning, candidate.Severity);
        Assert.Equal(0.1, candidate.ApiSimilarity);
    }

    [Fact]
    public void ContratInconnu_NeChangeRien()
    {
        var candidate = Assert.Single(DuplicateDetector.Escalate([Warning()], _ => null));

        Assert.Equal(DuplicateSeverity.Warning, candidate.Severity);
        Assert.Null(candidate.ApiSimilarity);
    }

    [Fact]
    public void UnBlocageNeSeDegradeJamais()
    {
        var candidate = Assert.Single(DuplicateDetector.Escalate([Blocking()], _ => 0.0));
        Assert.Equal(DuplicateSeverity.Blocking, candidate.Severity);
    }

    [Fact]
    public void LesPlusGravesRemontentEnTete()
    {
        var escalated = DuplicateDetector.Escalate(
            [Warning("Micro.A.One"), Warning("Micro.C.Three")],
            id => id == "Micro.C.Three" ? 0.95 : 0.05);

        Assert.Equal("Micro.C.Three", escalated[0].PackageId);
        Assert.Equal(DuplicateSeverity.Blocking, escalated[0].Severity);
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => DuplicateDetector.Escalate(null!, _ => 0.5));
        Assert.Throws<ArgumentNullException>(() => DuplicateDetector.Escalate([], null!));
    }
}
