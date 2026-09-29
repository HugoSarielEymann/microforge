using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;

namespace Micro.Soap.Envelope;

/// <summary>Version de SOAP.</summary>
public enum SoapVersion
{
    /// <summary>SOAP 1.1 : <c>text/xml</c>, en-tête HTTP <c>SOAPAction</c>.</summary>
    Soap11,

    /// <summary>SOAP 1.2 : <c>application/soap+xml</c>, action dans le Content-Type.</summary>
    Soap12,
}

/// <summary>À qui incombe une faute SOAP.</summary>
public enum SoapFaultCode
{
    /// <summary>L'appelant s'est trompé (SOAP 1.1 <c>Client</c>, SOAP 1.2 <c>Sender</c>).</summary>
    Client,

    /// <summary>Le service a failli (SOAP 1.1 <c>Server</c>, SOAP 1.2 <c>Receiver</c>).</summary>
    Server,

    /// <summary>Version d'enveloppe non prise en charge.</summary>
    VersionMismatch,

    /// <summary>Un en-tête obligatoire n'a pas été compris.</summary>
    MustUnderstand,
}

/// <summary>Une faute SOAP.</summary>
/// <param name="Code">À qui incombe la faute.</param>
/// <param name="Reason">Explication lisible.</param>
public sealed record SoapFault(SoapFaultCode Code, string Reason)
{
    /// <summary>Code applicatif précis (SOAP 1.1 : <c>Client.Code</c> ; SOAP 1.2 : <c>Subcode</c>).</summary>
    public string? SubCode { get; init; }

    /// <summary>Détail applicatif, sous forme d'élément XML.</summary>
    public XElement? Detail { get; init; }

    /// <summary>Acteur (SOAP 1.1 <c>faultactor</c>) ou rôle (SOAP 1.2 <c>Role</c>).</summary>
    public string? Actor { get; init; }
}

/// <summary>Une enveloppe lue.</summary>
public sealed class SoapMessage
{
    internal SoapMessage(SoapVersion version, IReadOnlyList<XElement> headers, XElement? body, SoapFault? fault)
    {
        Version = version;
        Headers = headers;
        Body = body;
        Fault = fault;
    }

    /// <summary>Version reconnue à l'espace de noms de l'enveloppe.</summary>
    public SoapVersion Version { get; }

    /// <summary>Éléments de l'en-tête SOAP.</summary>
    public IReadOnlyList<XElement> Headers { get; }

    /// <summary>Premier élément du corps ; <see langword="null"/> pour un corps vide.</summary>
    public XElement? Body { get; }

    /// <summary>La faute, si le corps en porte une.</summary>
    public SoapFault? Fault { get; }

    /// <summary>Le corps porte une faute.</summary>
    [MemberNotNullWhen(true, nameof(Fault))]
    public bool IsFault => Fault is not null;
}

/// <summary>Paramétrage de la lecture.</summary>
public sealed class SoapEnvelopeOptions
{
    /// <summary>Taille maximale d'un document lu, en caractères. Défaut : 10 millions.</summary>
    public long MaxCharacters { get; init; } = 10_000_000;

    /// <summary>Vérifie la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxCharacters"/> est inférieur à 1.</exception>
    public void Validate() => ArgumentOutOfRangeException.ThrowIfLessThan(MaxCharacters, 1L, nameof(MaxCharacters));
}

/// <summary>
/// Construit et lit des enveloppes SOAP 1.1 et 1.2. La lecture refuse les DTD et ne résout
/// aucune entité externe : une enveloppe reçue du réseau ne peut ni lire un fichier local ni
/// faire exploser la mémoire.
/// </summary>
public static class SoapEnvelope
{
    /// <summary>Espace de noms de l'enveloppe SOAP 1.1.</summary>
    public static readonly XNamespace Soap11Namespace = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>Espace de noms de l'enveloppe SOAP 1.2.</summary>
    public static readonly XNamespace Soap12Namespace = "http://www.w3.org/2003/05/soap-envelope";

    private static readonly XNamespace SubCodeNamespace = "urn:micro-soap:subcode";

    /// <summary>Espace de noms de l'enveloppe d'une version.</summary>
    public static XNamespace EnvelopeNamespace(SoapVersion version) => version == SoapVersion.Soap12 ? Soap12Namespace : Soap11Namespace;

    /// <summary>Content-Type HTTP d'une enveloppe (l'action y figure en SOAP 1.2).</summary>
    public static string ContentType(SoapVersion version, string? action = null)
        => version == SoapVersion.Soap12
            ? "application/soap+xml; charset=utf-8" + (string.IsNullOrEmpty(action) ? string.Empty : $"; action=\"{action}\"")
            : "text/xml; charset=utf-8";

    /// <summary>Version d'après un Content-Type HTTP ; <see langword="null"/> s'il n'est pas SOAP.</summary>
    public static SoapVersion? DetectVersion(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        string media = contentType.Split(';')[0].Trim();
        return media.Equals("application/soap+xml", StringComparison.OrdinalIgnoreCase) ? SoapVersion.Soap12
            : media.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ? SoapVersion.Soap11
            : null;
    }

    /// <summary>Action SOAP 1.2 portée par le Content-Type (<c>action="…"</c>) ; <see langword="null"/> sinon.</summary>
    public static string? ActionFromContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        foreach (string part in contentType.Split(';').Skip(1))
        {
            string[] pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim().Equals("action", StringComparison.OrdinalIgnoreCase))
            {
                return pair[1].Trim().Trim('"');
            }
        }

        return null;
    }

    /// <summary>Construit une enveloppe.</summary>
    /// <param name="body">Élément du corps ; <see langword="null"/> pour un corps vide.</param>
    /// <param name="version">Version.</param>
    /// <param name="headers">Éléments d'en-tête.</param>
    public static XDocument Create(XElement? body, SoapVersion version = SoapVersion.Soap11, IEnumerable<XElement>? headers = null)
    {
        XNamespace env = EnvelopeNamespace(version);
        XElement envelope = new(env + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", env.NamespaceName));
        List<XElement> headerList = headers?.ToList() ?? [];
        if (headerList.Count > 0)
        {
            envelope.Add(new XElement(env + "Header", headerList));
        }

        envelope.Add(new XElement(env + "Body", body));
        return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope);
    }

    /// <summary>Construit une enveloppe portant une faute.</summary>
    /// <exception cref="ArgumentNullException">Si la faute est nulle.</exception>
    public static XDocument CreateFault(SoapFault fault, SoapVersion version = SoapVersion.Soap11)
    {
        ArgumentNullException.ThrowIfNull(fault);
        XNamespace env = EnvelopeNamespace(version);
        XElement element;
        if (version == SoapVersion.Soap11)
        {
            string code = "soap:" + fault.Code switch
            {
                SoapFaultCode.Client => "Client",
                SoapFaultCode.VersionMismatch => "VersionMismatch",
                SoapFaultCode.MustUnderstand => "MustUnderstand",
                _ => "Server",
            } + (string.IsNullOrEmpty(fault.SubCode) ? string.Empty : "." + fault.SubCode);

            element = new XElement(
                env + "Fault",
                new XElement("faultcode", code),
                new XElement("faultstring", fault.Reason),
                fault.Actor is null ? null : new XElement("faultactor", fault.Actor),
                fault.Detail is null ? null : new XElement("detail", fault.Detail));
        }
        else
        {
            string value = "soap:" + fault.Code switch
            {
                SoapFaultCode.Client => "Sender",
                SoapFaultCode.VersionMismatch => "VersionMismatch",
                SoapFaultCode.MustUnderstand => "MustUnderstand",
                _ => "Receiver",
            };

            XElement code = new(env + "Code", new XElement(env + "Value", value));
            if (!string.IsNullOrEmpty(fault.SubCode))
            {
                code.Add(new XElement(
                    env + "Subcode",
                    new XElement(env + "Value", new XAttribute(XNamespace.Xmlns + "c", SubCodeNamespace.NamespaceName), "c:" + fault.SubCode)));
            }

            element = new XElement(
                env + "Fault",
                code,
                new XElement(env + "Reason", new XElement(env + "Text", new XAttribute(XNamespace.Xml + "lang", "fr"), fault.Reason)),
                fault.Actor is null ? null : new XElement(env + "Role", fault.Actor),
                fault.Detail is null ? null : new XElement(env + "Detail", fault.Detail));
        }

        return Create(element, version);
    }

    /// <summary>Lit une enveloppe.</summary>
    /// <exception cref="ArgumentNullException">Si le texte est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si le paramétrage est incohérent.</exception>
    /// <exception cref="FormatException">Si le texte n'est pas du XML sûr, ou pas une enveloppe SOAP.</exception>
    public static SoapMessage Parse(string xml, SoapEnvelopeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using StringReader reader = new(xml);
        return Parse(reader, options);
    }

    /// <summary>Lit une enveloppe depuis un flux.</summary>
    /// <exception cref="ArgumentNullException">Si le flux est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si le paramétrage est incohérent.</exception>
    /// <exception cref="FormatException">Si le contenu n'est pas du XML sûr, ou pas une enveloppe SOAP.</exception>
    public static SoapMessage Parse(Stream stream, SoapEnvelopeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using StreamReader reader = new(stream, leaveOpen: true);
        return Parse(reader, options);
    }

    /// <summary>Lit une enveloppe sans jamais lever : faux et le motif si le texte n'en est pas une.</summary>
    public static bool TryParse(string? xml, [NotNullWhen(true)] out SoapMessage? message, [NotNullWhen(false)] out string? error, SoapEnvelopeOptions? options = null)
    {
        message = null;
        if (xml is null)
        {
            error = "Aucun document.";
            return false;
        }

        if (options is not null && options.MaxCharacters < 1)
        {
            error = "La taille maximale doit valoir au moins 1.";
            return false;
        }

        try
        {
            message = Parse(xml, options);
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static SoapMessage Parse(TextReader text, SoapEnvelopeOptions? options)
    {
        SoapEnvelopeOptions effective = options ?? new SoapEnvelopeOptions();
        effective.Validate();
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = effective.MaxCharacters,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        XDocument document;
        try
        {
            using XmlReader reader = XmlReader.Create(text, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"Enveloppe illisible : {ex.Message}", ex);
        }

        XElement root = document.Root ?? throw new FormatException("Le document est vide.");
        SoapVersion version = root.Name.Namespace == Soap12Namespace ? SoapVersion.Soap12
            : root.Name.Namespace == Soap11Namespace ? SoapVersion.Soap11
            : throw new FormatException($"« {root.Name.LocalName} » n'est pas une enveloppe SOAP 1.1 ou 1.2.");
        if (root.Name.LocalName != "Envelope")
        {
            throw new FormatException($"« {root.Name.LocalName} » n'est pas une enveloppe SOAP.");
        }

        XNamespace env = root.Name.Namespace;
        XElement body = root.Element(env + "Body") ?? throw new FormatException("L'enveloppe n'a pas de corps (Body).");
        List<XElement> headers = root.Element(env + "Header")?.Elements().ToList() ?? [];
        XElement? content = body.Elements().FirstOrDefault();
        SoapFault? fault = content is not null && content.Name == env + "Fault" ? ReadFault(content, version) : null;
        return new SoapMessage(version, headers, content, fault);
    }

    private static SoapFault ReadFault(XElement fault, SoapVersion version)
    {
        if (version == SoapVersion.Soap11)
        {
            string code = fault.Element("faultcode")?.Value.Trim() ?? string.Empty;
            string local = code.Contains(':', StringComparison.Ordinal) ? code[(code.IndexOf(':', StringComparison.Ordinal) + 1)..] : code;
            string[] parts = local.Split('.', 2);
            return new SoapFault(MapCode(parts[0]), fault.Element("faultstring")?.Value ?? string.Empty)
            {
                SubCode = parts.Length > 1 ? parts[1] : null,
                Actor = fault.Element("faultactor")?.Value,
                Detail = fault.Element("detail")?.Elements().FirstOrDefault(),
            };
        }

        XNamespace env = Soap12Namespace;
        XElement? code12 = fault.Element(env + "Code");
        string value = LocalPart(code12?.Element(env + "Value")?.Value);
        string? sub = code12?.Element(env + "Subcode")?.Element(env + "Value")?.Value;
        return new SoapFault(MapCode(value), fault.Element(env + "Reason")?.Elements(env + "Text").FirstOrDefault()?.Value ?? string.Empty)
        {
            SubCode = sub is null ? null : LocalPart(sub),
            Actor = fault.Element(env + "Role")?.Value,
            Detail = fault.Element(env + "Detail")?.Elements().FirstOrDefault(),
        };
    }

    private static string LocalPart(string? qualified)
    {
        string text = qualified?.Trim() ?? string.Empty;
        int colon = text.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 ? text[(colon + 1)..] : text;
    }

    private static SoapFaultCode MapCode(string code) => code switch
    {
        "Client" or "Sender" => SoapFaultCode.Client,
        "VersionMismatch" => SoapFaultCode.VersionMismatch,
        "MustUnderstand" => SoapFaultCode.MustUnderstand,
        _ => SoapFaultCode.Server,
    };
}
