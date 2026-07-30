using SnippetForge.Embeddings;
using Xunit;

namespace SnippetForge.Tests;

public sealed class EmbeddingStoreTests
{
    /// <summary>Provider instrumenté : compte les calculs pour prouver l'efficacité du cache.</summary>
    private sealed class CountingProvider(string name = "test-provider") : IEmbeddingProvider
    {
        public int Calls { get; private set; }

        public string Name { get; } = name;

        public int Dimensions => 3;

        public DuplicateThresholds Thresholds => DuplicateThresholds.Lexical;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new[] { (float)text.Length, 1f, 0f });
        }
    }

    [Fact]
    public async Task GetOrComputeAsync_MemeTexte_NeCalculeQuUneFois()
    {
        using var forge = TempForge.Create();
        var store = EmbeddingStore.Load(forge.Root);
        var provider = new CountingProvider();

        var first = await store.GetOrComputeAsync("Micro.A.One@1.0.0", "texte", provider);
        var second = await store.GetOrComputeAsync("Micro.A.One@1.0.0", "texte", provider);

        Assert.Equal(1, provider.Calls);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetOrComputeAsync_TexteModifie_Recalcule()
    {
        using var forge = TempForge.Create();
        var store = EmbeddingStore.Load(forge.Root);
        var provider = new CountingProvider();

        await store.GetOrComputeAsync("Micro.A.One@1.0.0", "texte", provider);
        await store.GetOrComputeAsync("Micro.A.One@1.0.0", "texte modifié", provider);

        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task GetOrComputeAsync_ProviderDifferent_Recalcule()
    {
        using var forge = TempForge.Create();
        var store = EmbeddingStore.Load(forge.Root);

        await store.GetOrComputeAsync("Micro.A.One@1.0.0", "texte", new CountingProvider("provider-a"));
        var second = new CountingProvider("provider-b");
        await store.GetOrComputeAsync("Micro.A.One@1.0.0", "texte", second);

        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public async Task Save_PuisLoad_ConserveLesVecteurs()
    {
        using var forge = TempForge.Create();
        var provider = new CountingProvider();

        var store = EmbeddingStore.Load(forge.Root);
        var original = await store.GetOrComputeAsync("Micro.A.One@1.0.0", "texte", provider);
        store.Save();

        var reloaded = EmbeddingStore.Load(forge.Root);
        var fromCache = await reloaded.GetOrComputeAsync("Micro.A.One@1.0.0", "texte", provider);

        Assert.Equal(original, fromCache);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task PruneTo_SupprimeLesEntreesObsoletes()
    {
        using var forge = TempForge.Create();
        var store = EmbeddingStore.Load(forge.Root);
        var provider = new CountingProvider();

        await store.GetOrComputeAsync("Micro.A.One@1.0.0", "a", provider);
        await store.GetOrComputeAsync("Micro.B.Two@1.0.0", "b", provider);
        store.PruneTo(["Micro.A.One@1.0.0"]);
        store.Save();

        var reloaded = EmbeddingStore.Load(forge.Root);
        await reloaded.GetOrComputeAsync("Micro.A.One@1.0.0", "a", provider);
        Assert.Equal(2, provider.Calls);

        await reloaded.GetOrComputeAsync("Micro.B.Two@1.0.0", "b", provider);
        Assert.Equal(3, provider.Calls);
    }

    [Fact]
    public void Load_FichierCorrompu_RepartDUnCacheVide()
    {
        using var forge = TempForge.Create();
        File.WriteAllText(Path.Combine(forge.Path, "registry", "embeddings.json"), "{ ceci n'est pas du JSON");

        var store = EmbeddingStore.Load(forge.Root);
        Assert.NotNull(store);
    }

    [Fact]
    public void ContentHash_EstStableEtDiscriminant()
    {
        Assert.Equal(EmbeddingStore.ContentHash("texte"), EmbeddingStore.ContentHash("texte"));
        Assert.NotEqual(EmbeddingStore.ContentHash("texte"), EmbeddingStore.ContentHash("Texte"));
    }
}
