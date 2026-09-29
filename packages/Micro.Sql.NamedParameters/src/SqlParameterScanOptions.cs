namespace Micro.Sql.NamedParameters;

/// <summary>
/// Paramétrage du relevé des paramètres nommés d'une requête SQL.
/// </summary>
public sealed class SqlParameterScanOptions
{
    /// <summary>Caractères reconnus comme préfixes de paramètres autorisés.</summary>
    private const string SupportedPrefixes = "@:$";

    /// <summary>
    /// Préfixes qui ouvrent un paramètre nommé. Défaut : <c>"@"</c> (SQL Server, SQLite, Npgsql).
    /// Valeurs admises : toute combinaison de <c>@</c>, <c>:</c> (Oracle, SQLite) et <c>$</c> (SQLite).
    /// </summary>
    public string Prefixes { get; init; } = "@";

    /// <summary>
    /// Reconnaître les chaînes « dollar » de PostgreSQL (<c>$$…$$</c>, <c>$corps$…$corps$</c>) et
    /// ignorer leur contenu. Défaut : <see langword="true"/>. Incompatible avec le préfixe <c>$</c>.
    /// </summary>
    public bool DollarQuotedStrings { get; init; } = true;

    /// <summary>
    /// Imbriquer les commentaires <c>/* … */</c> comme SQL Server et PostgreSQL : un
    /// <c>/*</c> intérieur exige son propre <c>*/</c>. Défaut : <see langword="true"/>.
    /// </summary>
    public bool NestedBlockComments { get; init; } = true;

    /// <summary>
    /// Comparateur qui décide que deux noms désignent le même paramètre, pour
    /// <see cref="SqlParameterScanner.Names"/>. Défaut : <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    public StringComparer NameComparer { get; init; } = StringComparer.Ordinal;

    /// <summary>Vérifie la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentNullException">Si <see cref="Prefixes"/> ou <see cref="NameComparer"/> est nul.</exception>
    /// <exception cref="ArgumentException">
    /// Si <see cref="Prefixes"/> est vide, contient un caractère non pris en charge, ou contient
    /// <c>$</c> alors que <see cref="DollarQuotedStrings"/> est actif.
    /// </exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Prefixes);
        ArgumentNullException.ThrowIfNull(NameComparer);

        if (Prefixes.Length == 0)
        {
            throw new ArgumentException("Au moins un préfixe de paramètre est nécessaire.", nameof(Prefixes));
        }

        foreach (char prefix in Prefixes)
        {
            if (!SupportedPrefixes.Contains(prefix, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Préfixe « {prefix} » non pris en charge : seuls @, : et $ ouvrent un paramètre nommé.", nameof(Prefixes));
            }
        }

        if (DollarQuotedStrings && Prefixes.Contains('$', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Le préfixe $ et les chaînes dollar de PostgreSQL s'excluent : « $nom$ » serait ambigu. Désactiver DollarQuotedStrings pour employer $.",
                nameof(Prefixes));
        }
    }
}
