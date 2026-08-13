namespace Micro.Text.IdentifierCase;

/// <summary>Convention de nommage appliquée aux mots extraits du libellé.</summary>
public enum IdentifierStyle
{
    /// <summary>Chaque mot capitalisé, sans séparateur : <c>NomDuClient</c>.</summary>
    Pascal = 0,

    /// <summary>Comme <see cref="Pascal"/>, premier mot en minuscules : <c>nomDuClient</c>.</summary>
    Camel = 1,

    /// <summary>Minuscules séparées par un tiret bas : <c>nom_du_client</c>.</summary>
    Snake = 2,

    /// <summary>Minuscules séparées par un tiret : <c>nom-du-client</c>.</summary>
    Kebab = 3,

    /// <summary>Majuscules séparées par un tiret bas : <c>NOM_DU_CLIENT</c>.</summary>
    ScreamingSnake = 4,
}
