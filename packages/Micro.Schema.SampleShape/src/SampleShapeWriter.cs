using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Xml;
using System.Xml.Linq;

namespace Micro.Schema.SampleShape;

/// <summary>
/// Réémet un document d'exemple à partir d'une forme.
/// </summary>
/// <remarks>
/// L'échantillon écrit sert à montrer une forme à quelqu'un, et à être relu par
/// <see cref="SampleShapeReader"/> : les deux opérations sont réciproques, aux natures
/// indécidables près, qui reviennent en texte faute de mieux.
/// </remarks>
public static class SampleShapeWriter
{
    /// <summary>Écrit un échantillon JSON représentant la forme.</summary>
    /// <param name="root">Forme à représenter.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Le document JSON.</returns>
    /// <exception cref="ArgumentNullException">Si la forme est nulle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public static string ToJson(ShapeNode root, SampleShapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        SampleShapeOptions settings = options ?? SampleShapeOptions.Default;
        settings.Validate();

        using MemoryStream flux = new();
        using (Utf8JsonWriter ecrivain = new(flux, new JsonWriterOptions
        {
            Indented = settings.Indent,
            // Sans cet encodeur, « é » ressortirait en « é » : illisible pour quelqu'un
            // qui relit l'échantillon, et ce document est fait pour être relu.
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        }))
        {
            WriteJsonValue(ecrivain, root, settings, depth: 0);
        }

        return Encoding.UTF8.GetString(flux.ToArray());
    }

    /// <summary>Écrit un échantillon XML représentant la forme.</summary>
    /// <param name="root">Forme à représenter.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Le document XML.</returns>
    /// <exception cref="ArgumentNullException">Si la forme est nulle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    /// <remarks>
    /// Un champ répété est écrit plusieurs fois : XML n'a pas de tableau, et c'est la
    /// répétition de l'élément qui porte la cardinalité.
    /// </remarks>
    public static string ToXml(ShapeNode root, SampleShapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        SampleShapeOptions settings = options ?? SampleShapeOptions.Default;
        settings.Validate();

        XElement racine = WriteXmlElement(root, settings, depth: 0, name: SafeName(root.Name, settings.RootName));

        return racine.ToString(settings.Indent ? SaveOptions.None : SaveOptions.DisableFormatting);
    }

    // ==================== JSON ====================

    private static void WriteJsonValue(Utf8JsonWriter writer, ShapeNode node, SampleShapeOptions options, int depth)
    {
        if (node.IsRepeated)
        {
            writer.WriteStartArray();
            WriteJsonScalarOrObject(writer, node, options, depth);
            writer.WriteEndArray();
            return;
        }

        WriteJsonScalarOrObject(writer, node, options, depth);
    }

    private static void WriteJsonScalarOrObject(
        Utf8JsonWriter writer,
        ShapeNode node,
        SampleShapeOptions options,
        int depth)
    {
        if (node.Kind != ShapeKind.Structure)
        {
            WriteJsonScalar(writer, node.Kind, options);
            return;
        }

        writer.WriteStartObject();

        if (depth < options.MaxDepth)
        {
            foreach (ShapeNode enfant in node.Children)
            {
                writer.WritePropertyName(enfant.Name);
                WriteJsonValue(writer, enfant, options, depth + 1);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteJsonScalar(Utf8JsonWriter writer, ShapeKind kind, SampleShapeOptions options)
    {
        switch (kind)
        {
            case ShapeKind.WholeNumber:
                writer.WriteNumberValue(0);
                break;

            case ShapeKind.FractionalNumber:
                // Le « .0 » est ce qui distingue un nombre à virgule d'un entier à la relecture ;
                // WriteNumberValue(0d) écrirait « 0 » et perdrait la nature.
                writer.WriteRawValue("0.0");
                break;

            case ShapeKind.Boolean:
                writer.WriteBooleanValue(false);
                break;

            case ShapeKind.Timestamp:
                writer.WriteStringValue(options.TimestampSample);
                break;

            case ShapeKind.Unknown:
                writer.WriteNullValue();
                break;

            default:
                writer.WriteStringValue(options.TextSample);
                break;
        }
    }

    // ==================== XML ====================

    private static XElement WriteXmlElement(ShapeNode node, SampleShapeOptions options, int depth, string name)
    {
        XElement element = new(name);

        if (node.Kind != ShapeKind.Structure)
        {
            element.Value = XmlScalar(node.Kind, options);
            return element;
        }

        if (depth >= options.MaxDepth)
        {
            return element;
        }

        foreach (ShapeNode enfant in node.Children)
        {
            string nomEnfant = SafeName(enfant.Name, $"champ{depth}");
            int occurrences = enfant.IsRepeated ? options.RepeatedSampleCount : 1;

            for (int i = 0; i < occurrences; i++)
            {
                element.Add(WriteXmlElement(enfant, options, depth + 1, nomEnfant));
            }
        }

        return element;
    }

    private static string XmlScalar(ShapeKind kind, SampleShapeOptions options) => kind switch
    {
        ShapeKind.WholeNumber => "0",
        ShapeKind.FractionalNumber => "0.0",
        ShapeKind.Boolean => "false",
        ShapeKind.Timestamp => options.TimestampSample,
        ShapeKind.Unknown => string.Empty,
        _ => options.TextSample,
    };

    /// <summary>
    /// Ramène un nom à ce que XML accepte comme nom d'élément, ou rend le repli donné.
    /// </summary>
    /// <param name="name">Nom souhaité.</param>
    /// <param name="fallback">Nom de repli si le souhaité est inutilisable.</param>
    /// <returns>Un nom d'élément valide.</returns>
    /// <remarks>
    /// Une forme peut venir de JSON, où un nom de champ est une chaîne quelconque : « prix TTC »
    /// ou « 2026 » sont des clés JSON légitimes et des noms d'élément XML impossibles. Plutôt
    /// que d'échouer sur un document que l'utilisateur voit à l'écran, on nettoie.
    /// </remarks>
    private static string SafeName(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        string nettoye = XmlConvert.EncodeLocalName(name.Trim()) ?? fallback;

        return string.IsNullOrEmpty(nettoye) ? fallback : nettoye;
    }
}
