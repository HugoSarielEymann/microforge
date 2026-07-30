using SnippetForge.Integrity;
using Xunit;

namespace SnippetForge.Tests;

public sealed class ArtifactLedgerTests
{
    private static string WriteArtifact(TempForge forge, string name, string content)
    {
        var path = Path.Combine(forge.Path, "feed", name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void FeedConforme_NeProduitAucunEcart()
    {
        using var forge = TempForge.Create();
        var artifact = WriteArtifact(forge, "Micro.A.One.1.0.0.nupkg", "contenu");

        var ledger = ArtifactLedger.Load(forge.Root);
        ledger.Record(artifact);

        Assert.Empty(ledger.Verify(forge.Root));
    }

    [Fact]
    public void ArtefactDeposeALaMain_EstSignaleCommeNonEnregistre()
    {
        using var forge = TempForge.Create();
        WriteArtifact(forge, "Micro.Intrus.Zero.1.0.0.nupkg", "déposé sans validation");

        var finding = Assert.Single(ArtifactLedger.Load(forge.Root).Verify(forge.Root));

        Assert.Equal(IntegrityIssue.Unregistered, finding.Issue);
        Assert.Equal("Micro.Intrus.Zero.1.0.0.nupkg", finding.FileName);
    }

    [Fact]
    public void ArtefactModifieApresPublication_EstDetecte()
    {
        using var forge = TempForge.Create();
        var artifact = WriteArtifact(forge, "Micro.A.One.1.0.0.nupkg", "original");

        var ledger = ArtifactLedger.Load(forge.Root);
        ledger.Record(artifact);
        File.WriteAllText(artifact, "falsifié");

        var finding = Assert.Single(ledger.Verify(forge.Root));
        Assert.Equal(IntegrityIssue.Tampered, finding.Issue);
    }

    [Fact]
    public void VersionSupprimee_EstDetectee()
    {
        using var forge = TempForge.Create();
        var artifact = WriteArtifact(forge, "Micro.A.One.1.0.0.nupkg", "contenu");

        var ledger = ArtifactLedger.Load(forge.Root);
        ledger.Record(artifact);
        File.Delete(artifact);

        var finding = Assert.Single(ledger.Verify(forge.Root));
        Assert.Equal(IntegrityIssue.Missing, finding.Issue);
    }

    [Fact]
    public void Empreintes_SontPersistees()
    {
        using var forge = TempForge.Create();
        var artifact = WriteArtifact(forge, "Micro.A.One.1.0.0.nupkg", "contenu");

        var ledger = ArtifactLedger.Load(forge.Root);
        ledger.Record(artifact);
        ledger.Save();

        Assert.Empty(ArtifactLedger.Load(forge.Root).Verify(forge.Root));
    }

    [Fact]
    public void AdoptExisting_EnroleLesArtefactsAnterieurs()
    {
        using var forge = TempForge.Create();
        WriteArtifact(forge, "Micro.A.One.1.0.0.nupkg", "a");
        WriteArtifact(forge, "Micro.B.Two.1.0.0.nupkg", "b");

        var ledger = ArtifactLedger.Load(forge.Root);
        Assert.Equal(2, ledger.AdoptExisting(forge.Root));
        Assert.Empty(ledger.Verify(forge.Root));
    }

    [Fact]
    public void AdoptExisting_EstIdempotent()
    {
        using var forge = TempForge.Create();
        WriteArtifact(forge, "Micro.A.One.1.0.0.nupkg", "a");

        var ledger = ArtifactLedger.Load(forge.Root);
        Assert.Equal(1, ledger.AdoptExisting(forge.Root));
        Assert.Equal(0, ledger.AdoptExisting(forge.Root));
    }

    [Fact]
    public void AdoptExisting_NeMasquePasUneFalsification()
    {
        // L'adoption enrôle l'inconnu, elle ne réécrit pas une empreinte existante.
        using var forge = TempForge.Create();
        var artifact = WriteArtifact(forge, "Micro.A.One.1.0.0.nupkg", "original");

        var ledger = ArtifactLedger.Load(forge.Root);
        ledger.Record(artifact);
        File.WriteAllText(artifact, "falsifié");

        Assert.Equal(0, ledger.AdoptExisting(forge.Root));
        Assert.Equal(IntegrityIssue.Tampered, Assert.Single(ledger.Verify(forge.Root)).Issue);
    }

    [Fact]
    public void ComputeHash_EstStableEtDiscriminant()
    {
        using var forge = TempForge.Create();
        var first = WriteArtifact(forge, "a.nupkg", "contenu");
        var second = WriteArtifact(forge, "b.nupkg", "contenu");
        var third = WriteArtifact(forge, "c.nupkg", "autre");

        Assert.Equal(ArtifactLedger.ComputeHash(first), ArtifactLedger.ComputeHash(second));
        Assert.NotEqual(ArtifactLedger.ComputeHash(first), ArtifactLedger.ComputeHash(third));
        Assert.Equal(64, ArtifactLedger.ComputeHash(first).Length);
    }

    [Fact]
    public void Load_FichierCorrompu_RepartDUnRegistreVide()
    {
        using var forge = TempForge.Create();
        File.WriteAllText(Path.Combine(forge.Path, "registry", "artifacts.json"), "{ pas du JSON");

        Assert.Empty(ArtifactLedger.Load(forge.Root).Hashes);
    }

    [Theory]
    [InlineData(IntegrityIssue.Unregistered, "jamais enregistré")]
    [InlineData(IntegrityIssue.Tampered, "modifié après publication")]
    [InlineData(IntegrityIssue.Missing, "absent du feed")]
    public void Describe_ExpliqueLEcart(IntegrityIssue issue, string expected) =>
        Assert.Contains(expected, ArtifactLedger.Describe(new IntegrityFinding("x.nupkg", issue)), StringComparison.Ordinal);
}

public sealed class ForgeVersionTests
{
    [Fact]
    public void Current_EstUneVersionSemVer() =>
        Assert.True(SemVerLite.IsValid(ForgeVersion.Current), $"version illisible : {ForgeVersion.Current}");

    [Fact]
    public void Banner_MentionneLaVersionEtLeFormat()
    {
        Assert.Contains(ForgeVersion.Current, ForgeVersion.Banner, StringComparison.Ordinal);
        Assert.Contains(ForgeVersion.RegistryFormat.ToString(), ForgeVersion.Banner, StringComparison.Ordinal);
    }
}
