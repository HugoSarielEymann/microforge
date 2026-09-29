namespace Micro.Net.MacAddress;

/// <summary>Conventions d'écriture d'une adresse MAC.</summary>
public enum MacAddressFormat
{
    /// <summary>Six groupes de deux chiffres séparés par des deux-points : <c>AA:BB:CC:DD:EE:FF</c>.</summary>
    Colon = 0,

    /// <summary>Six groupes de deux chiffres séparés par des tirets : <c>AA-BB-CC-DD-EE-FF</c> (convention Windows).</summary>
    Hyphen = 1,

    /// <summary>Douze chiffres accolés : <c>AABBCCDDEEFF</c>.</summary>
    Bare = 2,

    /// <summary>Trois groupes de quatre chiffres séparés par des points : <c>aabb.ccdd.eeff</c> (convention Cisco).</summary>
    Cisco = 3,
}

/// <summary>Paramétrage de la remise en forme d'une adresse MAC.</summary>
public sealed class MacAddressOptions
{
    /// <summary>Convention d'écriture du résultat. Défaut : <see cref="MacAddressFormat.Colon"/>.</summary>
    public MacAddressFormat Format { get; init; } = MacAddressFormat.Colon;

    /// <summary>Rend les chiffres hexadécimaux en majuscules. Défaut : vrai.</summary>
    public bool Uppercase { get; init; } = true;

    /// <summary>Valide la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="Format"/> ne fait pas partie des valeurs définies.</exception>
    public void Validate()
    {
        if (!Enum.IsDefined(Format))
        {
            throw new ArgumentOutOfRangeException(nameof(Format), Format, "Convention d'écriture inconnue.");
        }
    }
}
