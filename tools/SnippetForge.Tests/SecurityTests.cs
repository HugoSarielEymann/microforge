using SnippetForge.Remote;
using SnippetForge.Telemetry;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Le chemin d'un artefact rapatrié est construit à partir de données que l'on ne
/// contrôle pas : la version provient de l'index de versions du **dépôt distant**,
/// l'identifiant de la ligne de commande. Un dépôt compromis ou mal écrit ne doit pas
/// pouvoir faire écrire hors du feed.
/// </summary>
public sealed class ArtifactPathTests
{
    [Fact]
    public void NomOrdinaire_ResoutDansLeFeed()
    {
        using var forge = TempForge.Create();

        var path = FeedMirror.ResolveArtifactPath(forge.Root, "Micro.Flow.Retry", "1.0.1");

        Assert.Equal(Path.Combine(forge.Root.FeedDir, "Micro.Flow.Retry.1.0.1.nupkg"), path);
    }

    [Theory]
    [InlineData("../../evil", "1.0.0")]
    [InlineData("Micro.A.One", "../../../1.0.0")]
    [InlineData("Micro.A.One", "..")]
    [InlineData("a/b", "1.0.0")]
    [InlineData("Micro.A.One", "1.0.0/../../x")]
    public void RemonteeDeDossier_EstRefusee(string packageId, string version)
    {
        using var forge = TempForge.Create();

        var ex = Assert.Throws<InvalidOperationException>(
            () => FeedMirror.ResolveArtifactPath(forge.Root, packageId, version));

        Assert.Contains("refusé", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SeparateurDeChemin_EstRefuse()
    {
        using var forge = TempForge.Create();

        Assert.Throws<InvalidOperationException>(
            () => FeedMirror.ResolveArtifactPath(forge.Root, "Micro" + Path.DirectorySeparatorChar + "A", "1.0.0"));
    }

    [Fact]
    public void ArgumentsVides_LeventArgumentException()
    {
        using var forge = TempForge.Create();

        Assert.Throws<ArgumentException>(() => FeedMirror.ResolveArtifactPath(forge.Root, "  ", "1.0.0"));
        Assert.Throws<ArgumentException>(() => FeedMirror.ResolveArtifactPath(forge.Root, "Micro.A.One", "  "));
    }
}

/// <summary>
/// Le journal d'usage est un fichier ordinaire du registre, susceptible d'être
/// sauvegardé ou partagé : aucun secret ne doit s'y retrouver en clair.
/// </summary>
public sealed class UsageLogSanitizationTests
{
    [Fact]
    public void MotDePasseDansUneUrl_EstMasque()
    {
        var ligne = UsageLog.Sanitize(["--source", "https://user:motdepasse@depot.local/v3/index.json"]);

        Assert.DoesNotContain("motdepasse", ligne, StringComparison.Ordinal);
        Assert.Contains("https://user:***@depot.local", ligne, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--api-key")]
    [InlineData("--password")]
    [InlineData("--token")]
    public void ValeurSuivantUneOptionSensible_EstMasquee(string option)
    {
        var ligne = UsageLog.Sanitize([option, "oups-un-secret"]);

        Assert.DoesNotContain("oups-un-secret", ligne, StringComparison.Ordinal);
        Assert.Contains("***", ligne, StringComparison.Ordinal);
    }

    [Fact]
    public void ArgumentsOrdinaires_SontConservesTelsQuels() =>
        Assert.Equal("search retry http", UsageLog.Sanitize(["search", "retry", "http"]));

    [Fact]
    public void UrlSansIdentifiants_ResteIntacte()
    {
        const string url = "https://depot.local/v3/index.json";
        Assert.Equal($"--source {url}", UsageLog.Sanitize(["--source", url]));
    }

    [Fact]
    public void ArobaseHorsAutorite_NestPasConfondueAvecDesIdentifiants()
    {
        const string url = "https://depot.local/chemin?to=a:b@c";
        Assert.Equal($"--source {url}", UsageLog.Sanitize(["--source", url]));
    }

    [Fact]
    public void LeJournalEcritSurDisqueNeContientPasLeSecret()
    {
        using var forge = TempForge.Create();

        UsageLog.Append(forge.Root, "remote", ["--source", "https://u:tres-secret@depot/v3/index.json"], 0);

        var contenu = File.ReadAllText(UsageLog.PathFor(forge.Root));
        Assert.DoesNotContain("tres-secret", contenu, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_ArgumentNul_LeveArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() => UsageLog.Sanitize(null!));
}
