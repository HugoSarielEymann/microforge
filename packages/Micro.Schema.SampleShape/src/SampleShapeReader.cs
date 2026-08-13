using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Micro.Schema.SampleShape;

/// <summary>
/// Déduit la forme d'un document d'exemple : ce qu'il contient, pas ce qu'il vaut.
/// </summary>
/// <remarks>
/// Sert à proposer un schéma à partir d'une charge utile collée par quelqu'un qui la connaît
/// mieux que la documentation. Ce que l'échantillon montre est pris pour argent comptant : un
/// champ absent de l'exemple est absent de la forme, un tableau vide ne dit rien de ses
/// éléments. C'est une proposition à relire, pas une vérité.
/// </remarks>
public static class SampleShapeReader
{
    /// <summary>
    /// Profondeur d'imbrication au-delà de laquelle un document est déclaré illisible.
    /// </summary>
    /// <remarks>
    /// Distincte de <see cref="SampleShapeOptions.MaxDepth"/>, qui borne la <em>forme</em>
    /// produite : ici on borne le <em>document</em>. Un échantillon de mille crochets ouverts
    /// ferait déborder la pile de l'analyseur avant que la moindre forme n'existe, et c'est
    /// donc à la lecture qu'il faut le refuser, pas à la restitution.
    /// </remarks>
    private const int DocumentDepthLimit = 64;

    /// <summary>Déduit la forme d'un échantillon JSON.</summary>
    /// <param name="sample">Document JSON.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>La forme du document.</returns>
    /// <exception cref="ArgumentNullException">Si l'échantillon est nul.</exception>
    /// <exception cref="FormatException">Si l'échantillon n'est pas du JSON lisible.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public static ShapeNode FromJson(string sample, SampleShapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sample);

        SampleShapeOptions settings = options ?? SampleShapeOptions.Default;
        settings.Validate();

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                sample,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = DocumentDepthLimit,
                });

            return ReadJson(settings.RootName, document.RootElement, settings, depth: 0);
        }
        catch (JsonException ex)
        {
            throw new FormatException(DescribeJsonFailure(ex), ex);
        }
    }

    /// <summary>Déduit la forme d'un échantillon JSON sans lever.</summary>
    /// <param name="sample">Document JSON ; nul ou vide échoue proprement.</param>
    /// <param name="shape">La forme lue, ou <see langword="null"/> en cas d'échec.</param>
    /// <param name="error">Le motif de l'échec, rédigé pour être affiché, ou <see langword="null"/>.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si la forme a pu être lue.</returns>
    /// <remarks>
    /// À préférer partout où l'échantillon vient d'un champ de saisie : quelqu'un qui tape du
    /// JSON passe l'essentiel de son temps dans un état invalide, et chaque frappe ne doit pas
    /// coûter une exception.
    /// </remarks>
    public static bool TryFromJson(
        string? sample,
        out ShapeNode? shape,
        out string? error,
        SampleShapeOptions? options = null)
    {
        shape = null;
        error = null;

        if (string.IsNullOrWhiteSpace(sample))
        {
            error = "Le document est vide.";
            return false;
        }

        try
        {
            shape = FromJson(sample, options);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or ArgumentNullException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Déduit la forme d'un échantillon XML.</summary>
    /// <param name="sample">Document XML.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns>La forme du document, nommée d'après l'élément racine.</returns>
    /// <exception cref="ArgumentNullException">Si l'échantillon est nul.</exception>
    /// <exception cref="FormatException">Si l'échantillon n'est pas du XML lisible.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si les options sont invalides.</exception>
    public static ShapeNode FromXml(string sample, SampleShapeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sample);

        SampleShapeOptions settings = options ?? SampleShapeOptions.Default;
        settings.Validate();

        try
        {
            // La résolution d'entités est coupée : un échantillon collé peut venir de
            // n'importe où, et une entité externe irait chercher un fichier ou une URL.
            using StringReader texte = new(sample);
            using XmlReader lecteur = XmlReader.Create(texte, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
            });

            XElement racine = XDocument.Load(lecteur).Root
                ?? throw new FormatException("Le document XML n'a pas d'élément racine.");

            return ReadXml(racine, settings, depth: 0, isRepeated: false);
        }
        catch (XmlException ex)
        {
            throw new FormatException(
                $"Le document XML est illisible à la ligne {ex.LineNumber}, position {ex.LinePosition} : {ex.Message}",
                ex);
        }
    }

    /// <summary>Déduit la forme d'un échantillon XML sans lever.</summary>
    /// <param name="sample">Document XML ; nul ou vide échoue proprement.</param>
    /// <param name="shape">La forme lue, ou <see langword="null"/> en cas d'échec.</param>
    /// <param name="error">Le motif de l'échec, rédigé pour être affiché, ou <see langword="null"/>.</param>
    /// <param name="options">Réglages ; <see langword="null"/> prend les défauts.</param>
    /// <returns><see langword="true"/> si la forme a pu être lue.</returns>
    public static bool TryFromXml(
        string? sample,
        out ShapeNode? shape,
        out string? error,
        SampleShapeOptions? options = null)
    {
        shape = null;
        error = null;

        if (string.IsNullOrWhiteSpace(sample))
        {
            error = "Le document est vide.";
            return false;
        }

        try
        {
            shape = FromXml(sample, options);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or ArgumentNullException)
        {
            error = ex.Message;
            return false;
        }
    }

    // ==================== JSON ====================

    private static ShapeNode ReadJson(string name, JsonElement element, SampleShapeOptions options, int depth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return ShapeNode.Structure(name, ReadJsonFields(element, options, depth));

            case JsonValueKind.Array:
                return ReadJsonArray(name, element, options, depth);

            case JsonValueKind.String:
                return ShapeNode.Leaf(name, ClassifyText(element.GetString(), options));

            case JsonValueKind.Number:
                return ShapeNode.Leaf(name, ClassifyNumber(element.GetRawText()));

            case JsonValueKind.True:
            case JsonValueKind.False:
                return ShapeNode.Leaf(name, ShapeKind.Boolean);

            default:
                // Null ou indéfini : la valeur ne dit rien de la nature du champ, et prétendre
                // le contraire imposerait un type que le document ne montre pas.
                return ShapeNode.Leaf(name, ShapeKind.Unknown);
        }
    }

    private static List<ShapeNode> ReadJsonFields(
        JsonElement element,
        SampleShapeOptions options,
        int depth)
    {
        if (depth >= options.MaxDepth)
        {
            return [];
        }

        List<ShapeNode> champs = [];

        foreach (JsonProperty propriete in element.EnumerateObject())
        {
            if (champs.Count >= options.MaxFieldsPerObject)
            {
                break;
            }

            // Un objet JSON peut porter deux fois la même clé ; le premier vu l'emporte,
            // comme le ferait n'importe quel désérialiseur.
            if (champs.Any(c => string.Equals(c.Name, propriete.Name, StringComparison.Ordinal)))
            {
                continue;
            }

            champs.Add(ReadJson(propriete.Name, propriete.Value, options, depth + 1));
        }

        return champs;
    }

    private static ShapeNode ReadJsonArray(
        string name,
        JsonElement element,
        SampleShapeOptions options,
        int depth)
    {
        // Les éléments sont fusionnés plutôt que lus sur le seul premier : un tableau dont la
        // première entrée est incomplète décrirait mal les suivantes.
        ShapeNode? fusion = null;

        foreach (JsonElement item in element.EnumerateArray())
        {
            ShapeNode lu = ReadJson(name, item, options, depth);
            fusion = fusion is null ? lu : Merge(fusion, lu);
        }

        return fusion is null
            ? ShapeNode.Leaf(name, ShapeKind.Unknown, isRepeated: true)
            : fusion with { IsRepeated = true };
    }

    private static string DescribeJsonFailure(JsonException ex)
        => ex.LineNumber is { } ligne
            ? $"Le document JSON est illisible à la ligne {ligne + 1}, position {ex.BytePositionInLine + 1} : {ex.Message}"
            : $"Le document JSON est illisible : {ex.Message}";

    // ==================== XML ====================

    private static ShapeNode ReadXml(XElement element, SampleShapeOptions options, int depth, bool isRepeated)
    {
        string nom = element.Name.LocalName;

        List<ShapeNode> champs = [];

        if (depth < options.MaxDepth)
        {
            if (options.ReadXmlAttributes)
            {
                foreach (XAttribute attribut in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
                {
                    if (champs.Count >= options.MaxFieldsPerObject)
                    {
                        break;
                    }

                    champs.Add(ShapeNode.Leaf(
                        attribut.Name.LocalName,
                        ClassifyText(attribut.Value, options)));
                }
            }

            foreach (IGrouping<string, XElement> groupe in element.Elements()
                .GroupBy(e => e.Name.LocalName, StringComparer.Ordinal))
            {
                // Un élément fait toujours foi contre un attribut de même nom : c'est lui qui
                // peut porter une structure, là où l'attribut ne porte qu'un texte.
                champs.RemoveAll(c => string.Equals(c.Name, groupe.Key, StringComparison.Ordinal));

                if (champs.Count >= options.MaxFieldsPerObject)
                {
                    break;
                }

                bool repete = groupe.Count() > 1;
                ShapeNode? fusion = null;

                foreach (XElement enfant in groupe)
                {
                    ShapeNode lu = ReadXml(enfant, options, depth + 1, repete);
                    fusion = fusion is null ? lu : Merge(fusion, lu);
                }

                champs.Add(fusion! with { IsRepeated = repete });
            }
        }

        if (champs.Count > 0)
        {
            return new ShapeNode(nom, ShapeKind.Structure, isRepeated, champs);
        }

        // Une feuille : sa nature se lit dans son texte. Un élément vide ne dit rien.
        return ShapeNode.Leaf(nom, ClassifyText(element.Value, options), isRepeated);
    }

    // ==================== Classement ====================

    private static ShapeKind ClassifyText(string? value, SampleShapeOptions options)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ShapeKind.Unknown;
        }

        if (options.DetectTimestamps && LooksLikeTimestamp(value))
        {
            return ShapeKind.Timestamp;
        }

        // Côté XML tout est texte : « 12 » y est un entier, là où en JSON la chaîne « 12 »
        // dit explicitement qu'on veut du texte. Ce classement ne s'applique donc qu'aux
        // valeurs qui arrivent sans type, et le JSON, lui, passe par ClassifyNumber.
        return ShapeKind.Text;
    }

    private static bool LooksLikeTimestamp(string value)
    {
        // Un instant se reconnaît à sa forme complète : « 2026-08-05T14:30:00Z ». Accepter
        // les formats laxistes ferait passer « 5 » pour une date dans certaines cultures.
        string[] formats =
        [
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd",
        ];

        return DateTimeOffset.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out _);
    }

    private static ShapeKind ClassifyNumber(string raw)
        => raw.Contains('.', StringComparison.Ordinal)
            || raw.Contains('e', StringComparison.OrdinalIgnoreCase)
                ? ShapeKind.FractionalNumber
                : ShapeKind.WholeNumber;

    // ==================== Fusion ====================

    /// <summary>
    /// Réunit deux lectures d'un même champ en une forme qui les décrit toutes les deux.
    /// </summary>
    /// <param name="left">Première lecture.</param>
    /// <param name="right">Seconde lecture.</param>
    /// <returns>La forme fusionnée.</returns>
    /// <exception cref="ArgumentNullException">Si l'une des deux formes est nulle.</exception>
    /// <remarks>
    /// Deux natures différentes se réconcilient par la plus large qui les contient : un entier
    /// et un nombre à virgule donnent un nombre à virgule ; tout le reste, du texte. Une nature
    /// indécidable s'efface devant une nature connue — c'est ce qui permet à un tableau dont la
    /// première entrée porte un <c>null</c> d'être décrit par les suivantes.
    /// </remarks>
    public static ShapeNode Merge(ShapeNode left, ShapeNode right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        ShapeKind nature = MergeKinds(left.Kind, right.Kind);

        if (nature != ShapeKind.Structure)
        {
            return left with { Kind = nature, IsRepeated = left.IsRepeated || right.IsRepeated, Children = [] };
        }

        List<ShapeNode> champs = [.. left.Children];

        foreach (ShapeNode droit in right.Children)
        {
            int index = champs.FindIndex(c => string.Equals(c.Name, droit.Name, StringComparison.Ordinal));

            if (index < 0)
            {
                champs.Add(droit);
            }
            else
            {
                champs[index] = Merge(champs[index], droit);
            }
        }

        return left with
        {
            Kind = ShapeKind.Structure,
            IsRepeated = left.IsRepeated || right.IsRepeated,
            Children = champs,
        };
    }

    private static ShapeKind MergeKinds(ShapeKind left, ShapeKind right)
    {
        if (left == right)
        {
            return left;
        }

        if (left == ShapeKind.Unknown)
        {
            return right;
        }

        if (right == ShapeKind.Unknown)
        {
            return left;
        }

        if ((left is ShapeKind.WholeNumber && right is ShapeKind.FractionalNumber)
            || (left is ShapeKind.FractionalNumber && right is ShapeKind.WholeNumber))
        {
            return ShapeKind.FractionalNumber;
        }

        // Une structure face à une valeur simple, ou deux natures étrangères : le texte est
        // le seul terrain d'entente, puisque tout s'y écrit.
        return ShapeKind.Text;
    }
}
