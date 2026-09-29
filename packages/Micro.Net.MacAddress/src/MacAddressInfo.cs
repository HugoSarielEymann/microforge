namespace Micro.Net.MacAddress;

/// <summary>
/// Une adresse MAC analysée : sa forme normalisée, ses six octets, son préfixe fabricant
/// et les deux bits de portée que porte son premier octet.
/// </summary>
public sealed class MacAddressInfo
{
    internal MacAddressInfo(byte[] bytes, string formatted)
    {
        // Enveloppe en lecture seule : le tableau interne ne fuit pas vers l'appelant.
        Bytes = Array.AsReadOnly(bytes);
        Formatted = formatted;
    }

    /// <summary>L'adresse remise en forme selon le paramétrage fourni.</summary>
    public string Formatted { get; }

    /// <summary>Les six octets de l'adresse, du premier transmis au dernier.</summary>
    public IReadOnlyList<byte> Bytes { get; }

    /// <summary>
    /// Le préfixe fabricant (OUI), soit les trois premiers octets en majuscules et sans
    /// séparateur : <c>"AABBCC"</c>. C'est la clé de recherche dans les catalogues publics
    /// d'attributions IEEE.
    /// </summary>
    public string Oui => $"{Bytes[0]:X2}{Bytes[1]:X2}{Bytes[2]:X2}";

    /// <summary>
    /// Vrai si le bit « administré localement » (bit 1 du premier octet) est posé : l'adresse
    /// n'a pas été attribuée par l'IEEE. C'est la signature des adresses aléatoires que les
    /// systèmes récents tirent pour préserver la vie privée, et des adresses forgées à la main.
    /// Chercher un fabricant à partir de son <see cref="Oui"/> n'a alors pas de sens.
    /// </summary>
    public bool IsLocallyAdministered => (Bytes[0] & 0b0000_0010) != 0;

    /// <summary>
    /// Vrai si le bit de groupe (bit 0 du premier octet) est posé : la trame vise un groupe
    /// de destinataires et non une carte unique. Une adresse <em>source</em> ne devrait
    /// jamais l'avoir.
    /// </summary>
    public bool IsMulticast => (Bytes[0] & 0b0000_0001) != 0;

    /// <summary>Vrai pour <c>FF:FF:FF:FF:FF:FF</c>, l'adresse de diffusion générale.</summary>
    public bool IsBroadcast => Bytes[0] == 0xFF && Bytes[1] == 0xFF && Bytes[2] == 0xFF
        && Bytes[3] == 0xFF && Bytes[4] == 0xFF && Bytes[5] == 0xFF;

    /// <summary>
    /// Vrai pour <c>00:00:00:00:00:00</c>. Une pile réseau la rend souvent à la place d'une
    /// absence de réponse : la traiter comme une adresse valide mène à des rapprochements faux.
    /// </summary>
    public bool IsUnspecified => Bytes[0] == 0 && Bytes[1] == 0 && Bytes[2] == 0
        && Bytes[3] == 0 && Bytes[4] == 0 && Bytes[5] == 0;

    /// <summary>Rend la forme normalisée.</summary>
    /// <returns>La valeur de <see cref="Formatted"/>.</returns>
    public override string ToString() => Formatted;
}
