using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SnippetForge.Embeddings;

/// <summary>
/// Provider adossé à un modèle d'embedding local servi par Ollama
/// (par défaut <c>nomic-embed-text</c>, 768 dimensions, ~270 Mo).
///
/// Prérequis : <c>ollama serve</c> en cours d'exécution et <c>ollama pull nomic-embed-text</c>.
/// Aucune donnée ne quitte la machine.
/// </summary>
public sealed class OllamaEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    /// <summary>Endpoint par défaut du démon Ollama local.</summary>
    public const string DefaultEndpoint = "http://localhost:11434";

    /// <summary>Modèle d'embedding par défaut.</summary>
    public const string DefaultModel = "nomic-embed-text";

    private readonly HttpClient _http;
    private readonly string _model;

    /// <summary>Crée le provider pour un modèle et un endpoint donnés.</summary>
    public OllamaEmbeddingProvider(string model = DefaultModel, string endpoint = DefaultEndpoint, int dimensions = 768)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        _model = model;
        _http = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = TimeSpan.FromSeconds(60) };
        Dimensions = dimensions;
    }

    /// <inheritdoc />
    public string Name => $"ollama-{_model}";

    /// <inheritdoc />
    public int Dimensions { get; private set; }

    /// <inheritdoc />
    public DuplicateThresholds Thresholds => DuplicateThresholds.Semantic;

    /// <inheritdoc />
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        using var response = await _http
            .PostAsJsonAsync("/api/embeddings", new EmbeddingRequest(_model, text), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<EmbeddingResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (payload?.Embedding is not { Length: > 0 } embedding)
        {
            throw new InvalidOperationException($"Ollama a renvoyé un embedding vide pour le modèle {_model}.");
        }

        Dimensions = embedding.Length;
        return VectorMath.NormalizeInPlace(embedding);
    }

    /// <summary>
    /// Teste la disponibilité du démon et la présence du modèle, sans lever d'exception.
    /// </summary>
    public static async Task<bool> IsAvailableAsync(
        string model = DefaultModel,
        string endpoint = DefaultEndpoint,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = TimeSpan.FromSeconds(3) };
            var tags = await http.GetFromJsonAsync<TagsResponse>("/api/tags", cancellationToken).ConfigureAwait(false);
            return tags?.Models?.Any(m =>
                m.Name.StartsWith(model, StringComparison.OrdinalIgnoreCase)) ?? false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[]? Embedding);

    private sealed record TagsResponse(
        [property: JsonPropertyName("models")] IReadOnlyList<TagEntry>? Models);

    private sealed record TagEntry(
        [property: JsonPropertyName("name")] string Name);
}
