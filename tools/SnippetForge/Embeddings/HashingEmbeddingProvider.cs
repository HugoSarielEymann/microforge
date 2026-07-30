using System.Globalization;
using System.Text;

namespace SnippetForge.Embeddings;

/// <summary>
/// Repli local sans dépendance : projection par « hashing trick » de n-grammes de
/// caractères et de mots, pondérés en fréquence sous-linéaire puis normalisés L2.
///
/// Ce provider est déterministe et hors ligne. Il capture la proximité lexicale et
/// morphologique (« slugify » ≈ « slugifier »), mais pas la synonymie profonde
/// (« découper » ≠ « chunk ») : pour cela, installer un modèle via Ollama.
/// </summary>
public sealed class HashingEmbeddingProvider : IEmbeddingProvider
{
    private const int MinCharGram = 3;
    private const int MaxCharGram = 5;

    /// <summary>Crée le provider avec la dimension de projection voulue.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="dimensions"/> est inférieur à 16.</exception>
    public HashingEmbeddingProvider(int dimensions = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 16);
        Dimensions = dimensions;
    }

    /// <inheritdoc />
    public string Name => $"hashing-v1-{Dimensions}";

    /// <inheritdoc />
    public int Dimensions { get; }

    /// <inheritdoc />
    public DuplicateThresholds Thresholds => DuplicateThresholds.Lexical;

    /// <inheritdoc />
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Embed(text));
    }

    /// <summary>Variante synchrone : ce provider ne fait aucune E/S.</summary>
    public float[] Embed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var counts = new double[Dimensions];
        var normalized = Normalize(text);

        foreach (var word in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Accumulate(counts, $"w:{word}", weight: 2.0);

            var padded = $" {word} ";
            for (var size = MinCharGram; size <= MaxCharGram; size++)
            {
                for (var start = 0; start + size <= padded.Length; start++)
                {
                    Accumulate(counts, $"c:{padded.AsSpan(start, size)}", weight: 1.0);
                }
            }
        }

        var vector = new float[Dimensions];
        for (var i = 0; i < Dimensions; i++)
        {
            // Pondération sous-linéaire : un terme répété ne doit pas écraser le vecteur.
            vector[i] = (float)(counts[i] > 0 ? 1 + Math.Log(counts[i]) : counts[i] < 0 ? -(1 + Math.Log(-counts[i])) : 0);
        }

        return VectorMath.NormalizeInPlace(vector);
    }

    private void Accumulate(double[] counts, string feature, double weight)
    {
        var hash = StableHash(feature);
        var index = (int)(hash % (uint)Dimensions);

        // Signe dérivé d'un bit du hash : réduit le biais des collisions (Weinberger et al.).
        var sign = (hash & 0x8000_0000u) == 0 ? 1.0 : -1.0;
        counts[index] += sign * weight;
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return builder.ToString();
    }

    /// <summary>FNV-1a 32 bits : stable entre exécutions et entre machines, contrairement à string.GetHashCode.</summary>
    private static uint StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }
}
