namespace Micro.Graph.ForceLayout;

/// <summary>Un lien entre deux nœuds, désignés par leur rang.</summary>
/// <param name="Source">Rang du premier nœud.</param>
/// <param name="Target">Rang du second nœud.</param>
/// <remarks>Le sens n'a pas d'effet sur la disposition : un lien tire ses deux bouts l'un vers l'autre.</remarks>
public readonly record struct ForceLink(int Source, int Target);

/// <summary>Réglages de la simulation.</summary>
/// <remarks>
/// Les défauts reprennent les proportions de d3-force, éprouvées sur des graphes de quelques
/// dizaines à quelques milliers de nœuds : ressorts de 60 unités, répulsion de −120, une
/// légère gravité qui empêche les composantes isolées de dériver à l'infini, et un
/// refroidissement qui fige la disposition en trois cents pas environ.
/// </remarks>
public sealed class ForceLayoutOptions
{
    /// <summary>Réglages par défaut, partagés car l'instance est immuable en pratique.</summary>
    public static ForceLayoutOptions Default { get; } = new();

    /// <summary>Longueur au repos d'un lien. Défaut : 60.</summary>
    public double LinkDistance { get; init; } = 60;

    /// <summary>
    /// Raideur des liens, entre 0 et 1. Défaut : <see cref="double.NaN"/>, qui applique la règle
    /// de d3 : l'inverse du plus petit degré des deux bouts.
    /// </summary>
    /// <remarks>
    /// La règle automatique assouplit les liens d'un nœud très connecté : sans elle, un moyeu
    /// tirerait tous ses voisins en boule contre lui.
    /// </remarks>
    public double LinkStrength { get; init; } = double.NaN;

    /// <summary>Force entre chaque paire de nœuds : négative, elle repousse. Défaut : −120.</summary>
    public double ChargeStrength { get; init; } = -120;

    /// <summary>Distance au-delà de laquelle deux nœuds s'ignorent. Défaut : l'infini.</summary>
    public double ChargeDistanceMax { get; init; } = double.PositiveInfinity;

    /// <summary>
    /// Précision de l'approximation de Barnes-Hut. Défaut : 0,9.
    /// </summary>
    /// <remarks>
    /// Un groupe de nœuds lointain est traité comme un seul quand sa taille rapportée à sa
    /// distance passe sous ce seuil. 0 calcule chaque paire exactement, en temps quadratique.
    /// </remarks>
    public double Theta { get; init; } = 0.9;

    /// <summary>Attraction de chaque nœud vers l'origine, entre 0 et 1. Défaut : 0,05.</summary>
    public double CenterStrength { get; init; } = 0.05;

    /// <summary>Température en deçà de laquelle la simulation est figée. Défaut : 0,001.</summary>
    public double AlphaMin { get; init; } = 0.001;

    /// <summary>Refroidissement à chaque pas, entre 0 et 1. Défaut : ≈ 0,0228 (figé en 300 pas).</summary>
    public double AlphaDecay { get; init; } = 1 - Math.Pow(0.001, 1.0 / 300);

    /// <summary>Température vers laquelle la simulation tend. Défaut : 0.</summary>
    /// <remarks>Une cible au-dessus de <see cref="AlphaMin"/> garde la simulation vivante, par exemple pendant un glisser.</remarks>
    public double AlphaTarget { get; init; }

    /// <summary>Frottement : part de la vitesse perdue à chaque pas, entre 0 et 1. Défaut : 0,4.</summary>
    public double VelocityDecay { get; init; } = 0.4;

    /// <summary>Espacement de la spirale de départ. Défaut : 10.</summary>
    public double InitialRadius { get; init; } = 10;

    /// <summary>Graine du minuscule aléa qui sépare deux nœuds confondus. Défaut : 1.</summary>
    public int Seed { get; init; } = 1;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si une valeur n'est pas finie (hors <see cref="LinkStrength"/> automatique et
    /// <see cref="ChargeDistanceMax"/> infinie), ou sort de son intervalle.
    /// </exception>
    public void Validate()
    {
        RequireFinite(LinkDistance, nameof(LinkDistance));
        ArgumentOutOfRangeException.ThrowIfNegative(LinkDistance);

        if (!double.IsNaN(LinkStrength))
        {
            RequireFinite(LinkStrength, nameof(LinkStrength));
            RequireUnit(LinkStrength, nameof(LinkStrength));
        }

        RequireFinite(ChargeStrength, nameof(ChargeStrength));

        if (double.IsNaN(ChargeDistanceMax) || ChargeDistanceMax <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ChargeDistanceMax), ChargeDistanceMax, "La portée de la répulsion doit être strictement positive.");
        }

        RequireFinite(Theta, nameof(Theta));
        ArgumentOutOfRangeException.ThrowIfNegative(Theta);

        RequireFinite(CenterStrength, nameof(CenterStrength));
        RequireUnit(CenterStrength, nameof(CenterStrength));

        RequireFinite(AlphaMin, nameof(AlphaMin));
        RequireUnit(AlphaMin, nameof(AlphaMin));
        RequireFinite(AlphaDecay, nameof(AlphaDecay));
        RequireUnit(AlphaDecay, nameof(AlphaDecay));
        RequireFinite(AlphaTarget, nameof(AlphaTarget));
        RequireUnit(AlphaTarget, nameof(AlphaTarget));
        RequireFinite(VelocityDecay, nameof(VelocityDecay));
        RequireUnit(VelocityDecay, nameof(VelocityDecay));

        RequireFinite(InitialRadius, nameof(InitialRadius));
        ArgumentOutOfRangeException.ThrowIfNegative(InitialRadius);
    }

    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "La valeur doit être un nombre fini.");
        }
    }

    private static void RequireUnit(double value, string name)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(name, value, "La valeur doit être comprise entre 0 et 1.");
        }
    }
}
