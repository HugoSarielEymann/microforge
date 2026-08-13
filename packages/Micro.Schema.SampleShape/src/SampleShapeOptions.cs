namespace Micro.Schema.SampleShape;

/// <summary>Réglages de la lecture et de l'écriture d'un échantillon.</summary>
public sealed class SampleShapeOptions
{
    /// <summary>Réglages par défaut.</summary>
    public static SampleShapeOptions Default { get; } = new();

    /// <summary>Nom donné à la racine quand l'échantillon n'en porte pas. Défaut : <c>racine</c>.</summary>
    /// <remarks>
    /// Un document JSON n'a pas de nom de racine ; un document XML en a un, qui est alors
    /// repris tel quel. Ce réglage ne sert donc que côté JSON, et à l'écriture XML.
    /// </remarks>
    public string RootName { get; init; } = "racine";

    /// <summary>Profondeur d'imbrication au-delà de laquelle on cesse de descendre. Défaut : 24.</summary>
    /// <remarks>
    /// Borne la <em>forme</em> produite : les nœuds atteints à cette profondeur sont rendus
    /// sans enfants, et la partie du document située plus bas est simplement ignorée. Le
    /// document lui-même, lui, est refusé au-delà de 64 niveaux — une borne fixe, parce
    /// qu'elle protège l'analyseur avant que la moindre forme n'existe. Porter ce réglage
    /// au-delà de 64 est donc sans effet.
    /// </remarks>
    public int MaxDepth { get; init; } = 24;

    /// <summary>Nombre maximal de champs retenus par structure. Défaut : 512.</summary>
    public int MaxFieldsPerObject { get; init; } = 512;

    /// <summary>Reconnaître les instants ISO 8601 dans les chaînes. Défaut : <see langword="true"/>.</summary>
    /// <remarks>
    /// Désactiver ce réglage quand l'échantillon contient des chaînes qui ressemblent à des
    /// dates sans en être — un numéro de version, une référence — et qu'on préfère les voir
    /// toutes en texte plutôt que d'en voir certaines mal classées.
    /// </remarks>
    public bool DetectTimestamps { get; init; } = true;

    /// <summary>Lire les attributs XML comme des champs. Défaut : <see langword="true"/>.</summary>
    /// <remarks>
    /// Les ignorer perdrait des champs sans le dire. Ils sont donc lus, mais réécrits en
    /// éléments : la forme survit à l'aller-retour, la syntaxe exacte non. Un attribut qui
    /// porte le même nom qu'un élément frère est écarté — l'élément fait foi.
    /// </remarks>
    public bool ReadXmlAttributes { get; init; } = true;

    /// <summary>Indenter l'échantillon écrit. Défaut : <see langword="true"/>.</summary>
    public bool Indent { get; init; } = true;

    /// <summary>Valeur d'exemple écrite pour un champ texte. Défaut : <c>texte</c>.</summary>
    public string TextSample { get; init; } = "texte";

    /// <summary>Valeur d'exemple écrite pour un instant. Défaut : <c>2026-01-01T00:00:00Z</c>.</summary>
    public string TimestampSample { get; init; } = "2026-01-01T00:00:00Z";

    /// <summary>
    /// Nombre d'occurrences écrites pour un champ répété en XML. Défaut : 2.
    /// </summary>
    /// <remarks>
    /// XML n'a pas de tableau : la répétition s'y dit en répétant l'élément. Une occurrence
    /// unique serait relue comme un champ simple, ce qui perdrait la cardinalité à
    /// l'aller-retour. Deux suffisent à la dire.
    /// </remarks>
    public int RepeatedSampleCount { get; init; } = 2;

    /// <summary>Vérifie la cohérence des réglages.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si une borne est nulle ou négative, ou si <see cref="RepeatedSampleCount"/> est
    /// inférieur à 2 — en deçà, la répétition ne se lirait plus.
    /// </exception>
    /// <exception cref="ArgumentNullException">Si un texte d'exemple est nul.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDepth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxFieldsPerObject, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(RepeatedSampleCount, 2);
        ArgumentNullException.ThrowIfNull(RootName);
        ArgumentNullException.ThrowIfNull(TextSample);
        ArgumentNullException.ThrowIfNull(TimestampSample);
    }
}
