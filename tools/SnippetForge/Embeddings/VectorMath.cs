namespace SnippetForge.Embeddings;

/// <summary>Opérations vectorielles pures utilisées par la recherche sémantique.</summary>
public static class VectorMath
{
    /// <summary>
    /// Similarité cosinus entre deux vecteurs, ramenée dans [0, 1]
    /// (0 pour des vecteurs orthogonaux ou opposés, 1 pour des vecteurs colinéaires).
    /// </summary>
    /// <exception cref="ArgumentException">Si les dimensions diffèrent.</exception>
    public static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Count != right.Count)
        {
            throw new ArgumentException(
                $"Dimensions incompatibles : {left.Count} contre {right.Count}.", nameof(right));
        }

        double dot = 0, leftNorm = 0, rightNorm = 0;
        for (var i = 0; i < left.Count; i++)
        {
            dot += (double)left[i] * right[i];
            leftNorm += (double)left[i] * left[i];
            rightNorm += (double)right[i] * right[i];
        }

        if (leftNorm == 0 || rightNorm == 0)
        {
            return 0;
        }

        var cosine = dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
        return Math.Clamp(cosine, 0, 1);
    }

    /// <summary>Normalise un vecteur en norme L2 (sur place, retourne le même tableau).</summary>
    public static float[] NormalizeInPlace(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        double sum = 0;
        foreach (var value in vector)
        {
            sum += (double)value * value;
        }

        if (sum == 0)
        {
            return vector;
        }

        var norm = Math.Sqrt(sum);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }
}
