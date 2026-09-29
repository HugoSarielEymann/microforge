namespace Micro.Net.IpRange;

/// <summary>Paramétrage de l'expansion d'une plage d'adresses IPv4.</summary>
public sealed class IpRangeOptions
{
    /// <summary>
    /// Écarte l'adresse de réseau et l'adresse de diffusion d'un bloc CIDR.
    /// Sans effet sur les blocs /31 et /32, qui n'en comportent pas au sens usuel.
    /// Défaut : vrai.
    /// </summary>
    public bool ExcludeNetworkAndBroadcast { get; init; } = true;

    /// <summary>
    /// Nombre maximal d'adresses que l'expansion accepte de produire. Garde-fou contre
    /// une expression trop large (un /8 en énumère seize millions). Défaut : 65 536.
    /// </summary>
    public int MaxAddresses { get; init; } = 65_536;

    /// <summary>Valide la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxAddresses"/> est inférieur à 1.</exception>
    public void Validate() => ArgumentOutOfRangeException.ThrowIfLessThan(MaxAddresses, 1);
}
