namespace Micro.Text.Slugify;

/// <summary>Paramétrage de la génération de slug.</summary>
public sealed class SlugifyOptions
{
    /// <summary>Caractère inséré entre les segments. Défaut : tiret.</summary>
    public char Separator { get; init; } = '-';

    /// <summary>Longueur maximale du slug ; null pour illimitée. La coupe ne laisse pas de séparateur final.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Convertit le résultat en minuscules invariantes. Défaut : vrai.</summary>
    public bool Lowercase { get; init; } = true;

    /// <summary>Valide la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxLength"/> est inférieur à 1.</exception>
    public void Validate()
    {
        if (MaxLength is { } max)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
        }
    }
}
