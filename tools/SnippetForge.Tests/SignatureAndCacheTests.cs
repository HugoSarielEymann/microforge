using SnippetForge.Integrity;
using Xunit;

namespace SnippetForge.Tests;

/// <summary>
/// Le registre d'empreintes seul ne résiste pas à un adversaire : qui peut réécrire un
/// artefact peut réécrire son empreinte. La signature ferme cette porte.
/// </summary>
public sealed class LedgerSignatureTests : IDisposable
{
    private readonly string _keyDirectory = Path.Combine(
        Path.GetTempPath(), "microforge-keys", Guid.NewGuid().ToString("N"));

    private string KeyPath => Path.Combine(_keyDirectory, "signing.key");

    private static ArtifactLedger LedgerWith(TempForge forge, params (string Name, string Content)[] artifacts)
    {
        var ledger = ArtifactLedger.Load(forge.Root);
        foreach (var (name, content) in artifacts)
        {
            var path = Path.Combine(forge.Path, "feed", name);
            File.WriteAllText(path, content);
            ledger.Record(path);
        }

        ledger.Save();
        return ledger;
    }

    private void UseKey() => Environment.SetEnvironmentVariable(LedgerSignature.PrivateKeyVariable, KeyPath);

    [Fact]
    public void SansCléPublique_LaSignatureNEstPasExigee()
    {
        using var forge = TempForge.Create();
        var ledger = LedgerWith(forge, ("a.1.0.0.nupkg", "a"));

        Assert.Equal(SignatureState.NotConfigured, LedgerSignature.Verify(forge.Root, ledger));
    }

    [Fact]
    public void RegistreSigne_EstVerifie()
    {
        using var forge = TempForge.Create();
        var ledger = LedgerWith(forge, ("a.1.0.0.nupkg", "a"));

        LedgerSignature.CreateKeyPair(forge.Root, KeyPath);
        UseKey();
        LedgerSignature.Sign(forge.Root, ledger);

        Assert.Equal(SignatureState.Valid, LedgerSignature.Verify(forge.Root, ledger));
    }

    [Fact]
    public void RegistreModifieApresSignature_EstDetecte()
    {
        // Le scénario que la signature existe pour couvrir : un adversaire réécrit
        // l'artefact ET son empreinte. Sans la clé privée, il ne peut pas resigner.
        using var forge = TempForge.Create();
        var ledger = LedgerWith(forge, ("a.1.0.0.nupkg", "a"));

        LedgerSignature.CreateKeyPair(forge.Root, KeyPath);
        UseKey();
        LedgerSignature.Sign(forge.Root, ledger);

        var falsifie = LedgerWith(forge, ("a.1.0.0.nupkg", "contenu falsifié"));

        Assert.Equal(SignatureState.Invalid, LedgerSignature.Verify(forge.Root, falsifie));
    }

    [Fact]
    public void CléPubliqueSansSignature_EstSignalee()
    {
        using var forge = TempForge.Create();
        var ledger = LedgerWith(forge, ("a.1.0.0.nupkg", "a"));

        LedgerSignature.CreateKeyPair(forge.Root, KeyPath);

        Assert.Equal(SignatureState.Missing, LedgerSignature.Verify(forge.Root, ledger));
    }

    [Fact]
    public void SignatureDUneAutreCle_EstRejetee()
    {
        using var forge = TempForge.Create();
        var ledger = LedgerWith(forge, ("a.1.0.0.nupkg", "a"));

        LedgerSignature.CreateKeyPair(forge.Root, KeyPath);
        UseKey();
        LedgerSignature.Sign(forge.Root, ledger);

        // Nouvelle paire : la clé publique change, la signature ne correspond plus.
        LedgerSignature.CreateKeyPair(forge.Root, KeyPath, overwrite: true);

        Assert.Equal(SignatureState.Invalid, LedgerSignature.Verify(forge.Root, ledger));
    }

    [Fact]
    public void LOrdreDesEmpreintes_NInfluePasSurLaSignature()
    {
        using var forge = TempForge.Create();
        LedgerWith(forge, ("b.1.0.0.nupkg", "b"), ("a.1.0.0.nupkg", "a"));

        LedgerSignature.CreateKeyPair(forge.Root, KeyPath);
        UseKey();
        LedgerSignature.Sign(forge.Root, ArtifactLedger.Load(forge.Root));

        // Rechargé, donc potentiellement dans un autre ordre d'énumération.
        Assert.Equal(SignatureState.Valid, LedgerSignature.Verify(forge.Root, ArtifactLedger.Load(forge.Root)));
    }

    [Fact]
    public void LaCléPriveeNEstJamaisDansLaBibliotheque()
    {
        using var forge = TempForge.Create();
        LedgerSignature.CreateKeyPair(forge.Root, KeyPath);

        var contenuBibliotheque = string.Join('\n', Directory
            .EnumerateFiles(forge.Path, "*", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.DoesNotContain("PRIVATE KEY", contenuBibliotheque, StringComparison.Ordinal);
        Assert.Contains("PUBLIC KEY", File.ReadAllText(LedgerSignature.PublicKeyPath(forge.Root)), StringComparison.Ordinal);
    }

    [Fact]
    public void RemplacerLaPaireSansForce_EstRefuse()
    {
        using var forge = TempForge.Create();
        LedgerSignature.CreateKeyPair(forge.Root, KeyPath);

        Assert.Throws<InvalidOperationException>(() => LedgerSignature.CreateKeyPair(forge.Root, KeyPath));
    }

    [Fact]
    public void SignerSansCléPrivee_LeveUneErreurExplicite()
    {
        using var forge = TempForge.Create();
        var ledger = LedgerWith(forge, ("a.1.0.0.nupkg", "a"));
        Environment.SetEnvironmentVariable(LedgerSignature.PrivateKeyVariable, null);

        var ex = Assert.Throws<InvalidOperationException>(() => LedgerSignature.Sign(forge.Root, ledger));
        Assert.Contains(LedgerSignature.PrivateKeyVariable, ex.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(LedgerSignature.PrivateKeyVariable, null);
        try
        {
            if (Directory.Exists(_keyDirectory))
            {
                Directory.Delete(_keyDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Nettoyage best-effort.
        }
    }
}

/// <summary>
/// « forge index » rouvrait chaque archive à chaque appel. Un artefact publié étant
/// immuable, il n'a aucune raison d'être relu deux fois.
/// </summary>
public sealed class FeedCacheTests
{
    private static FileInfo WriteArtifact(TempForge forge, string name, string content)
    {
        var path = Path.Combine(forge.Path, "feed", name);
        File.WriteAllText(path, content);
        return new FileInfo(path);
    }

    private static PackageMeta Meta(string id) => new(id, "1.0.0", "description", ["tag"], "auteur");

    [Fact]
    public void EntreeMemorisee_EstRelueDepuisLeCache()
    {
        using var forge = TempForge.Create();
        var file = WriteArtifact(forge, "a.1.0.0.nupkg", "contenu");

        var cache = FeedCache.Load(forge.Root);
        cache.Store(file, Meta("Micro.A.One"), "# README");
        cache.Save();

        var cached = FeedCache.Load(forge.Root).TryGet(new FileInfo(file.FullName));

        Assert.NotNull(cached);
        Assert.Equal("Micro.A.One", cached!.Meta.Id);
        Assert.Equal("# README", cached.Readme);
    }

    [Fact]
    public void FichierModifie_InvalideLEntree()
    {
        using var forge = TempForge.Create();
        var file = WriteArtifact(forge, "a.1.0.0.nupkg", "contenu");

        var cache = FeedCache.Load(forge.Root);
        cache.Store(file, Meta("Micro.A.One"), string.Empty);

        // Taille différente : l'artefact a changé, le cache ne doit pas mentir.
        File.WriteAllText(file.FullName, "contenu nettement plus long qu'avant");

        Assert.Null(cache.TryGet(new FileInfo(file.FullName)));
    }

    [Fact]
    public void FichierInconnu_NEstPasEnCache()
    {
        using var forge = TempForge.Create();
        var file = WriteArtifact(forge, "a.1.0.0.nupkg", "contenu");

        Assert.Null(FeedCache.Load(forge.Root).TryGet(file));
    }

    [Fact]
    public void PruneTo_RetireLesArtefactsDisparus()
    {
        using var forge = TempForge.Create();
        var a = WriteArtifact(forge, "a.1.0.0.nupkg", "a");
        var b = WriteArtifact(forge, "b.1.0.0.nupkg", "b");

        var cache = FeedCache.Load(forge.Root);
        cache.Store(a, Meta("Micro.A.One"), string.Empty);
        cache.Store(b, Meta("Micro.B.Two"), string.Empty);
        Assert.Equal(2, cache.Count);

        cache.PruneTo(["a.1.0.0.nupkg"]);

        Assert.Equal(1, cache.Count);
        Assert.NotNull(cache.TryGet(a));
    }

    [Fact]
    public void CacheCorrompu_RepartVide()
    {
        using var forge = TempForge.Create();
        File.WriteAllText(Path.Combine(forge.Path, "registry", "feed-cache.json"), "{ pas du JSON");

        Assert.Equal(0, FeedCache.Load(forge.Root).Count);
    }

    [Fact]
    public void ArgumentsNuls_LeventArgumentNull()
    {
        using var forge = TempForge.Create();
        var cache = FeedCache.Load(forge.Root);

        Assert.Throws<ArgumentNullException>(() => cache.TryGet(null!));
        Assert.Throws<ArgumentNullException>(() => cache.PruneTo(null!));
    }
}
