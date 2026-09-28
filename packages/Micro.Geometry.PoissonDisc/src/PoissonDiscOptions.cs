namespace Micro.Geometry.PoissonDisc;

/// <summary>
/// Réglages du tirage de <see cref="PoissonDiscSampler"/>.
/// </summary>
/// <remarks>
/// Les défauts conviennent à un placement visuel (étoiles, repères, vignettes) : une distance
/// minimale proche de ce que la surface permet, soixante essais par point avant de resserrer.
/// </remarks>
public sealed class PoissonDiscOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static PoissonDiscOptions Default { get; } = new();

    /// <summary>
    /// Essais par point avant de conclure que la distance est trop grande. Défaut : 60.
    /// </summary>
    public int AttemptsPerPoint { get; init; } = 60;

    /// <summary>
    /// Facteur de resserrement de la distance minimale quand les points ne tiennent pas, entre 0
    /// et 1 exclus. Défaut : 0,88.
    /// </summary>
    public double ShrinkFactor { get; init; } = 0.88;

    /// <summary>
    /// Surface réservée à chaque point, en multiple du carré de la distance de départ : plus il
    /// est grand, plus la distance de départ est petite. Défaut : 1,15.
    /// </summary>
    /// <remarks>
    /// Un empilement parfait de disques de Poisson ne dépasse pas une densité d'environ 0,55 ; à
    /// 1,15, la plupart des tirages aboutissent au premier tour, sans resserrement.
    /// </remarks>
    public double AreaPerPoint { get; init; } = 1.15;

    /// <summary>Nombre maximal de points d'un tirage. Défaut : 10 000.</summary>
    public int MaximumCount { get; init; } = 10_000;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <see cref="AttemptsPerPoint"/> ou <see cref="MaximumCount"/> est inférieur à 1, si
    /// <see cref="ShrinkFactor"/> n'est pas strictement entre 0 et 1, ou si
    /// <see cref="AreaPerPoint"/> n'est pas un nombre fini strictement positif.
    /// </exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(AttemptsPerPoint, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumCount, 1);
        if (!double.IsFinite(ShrinkFactor) || ShrinkFactor <= 0 || ShrinkFactor >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ShrinkFactor), ShrinkFactor, "Le resserrement doit être strictement entre 0 et 1.");
        }

        if (!double.IsFinite(AreaPerPoint) || AreaPerPoint <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AreaPerPoint), AreaPerPoint, "La surface par point doit être un nombre fini strictement positif.");
        }
    }
}
