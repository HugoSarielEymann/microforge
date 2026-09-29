using System.Xml.Linq;
using Xunit;

namespace Micro.Soap.Envelope.Tests;

public sealed class SoapEnvelopeTests
{
    private static readonly XNamespace Ns = "urn:mini-erp";

    [Theory]
    [InlineData(SoapVersion.Soap11)]
    [InlineData(SoapVersion.Soap12)]
    public void Une_enveloppe_construite_se_relit(SoapVersion version)
    {
        XElement body = new(Ns + "ConsulterUnClient", new XElement(Ns + "identifiant", "42"));
        XElement header = new(Ns + "Jeton", "abc");

        string xml = SoapEnvelope.Create(body, version, [header]).ToString();
        SoapMessage message = SoapEnvelope.Parse(xml);

        Assert.Equal(version, message.Version);
        Assert.False(message.IsFault);
        Assert.Equal(Ns + "ConsulterUnClient", message.Body!.Name);
        Assert.Equal("42", message.Body.Element(Ns + "identifiant")!.Value);
        Assert.Equal("abc", Assert.Single(message.Headers).Value);
    }

    [Fact]
    public void Lit_une_enveloppe_ecrite_par_un_autre_outil()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/" xmlns:e="urn:mini-erp">
              <!-- commentaire ignoré -->
              <S:Body><e:ConsulterLeStock><e:reference>ECR-M8</e:reference></e:ConsulterLeStock></S:Body>
            </S:Envelope>
            """;

        SoapMessage message = SoapEnvelope.Parse(xml);

        Assert.Equal(SoapVersion.Soap11, message.Version);
        Assert.Equal("ECR-M8", message.Body!.Value);
        Assert.Empty(message.Headers);
    }

    [Theory]
    [InlineData(SoapVersion.Soap11)]
    [InlineData(SoapVersion.Soap12)]
    public void Une_faute_complete_fait_l_aller_retour(SoapVersion version)
    {
        SoapFault fault = new(SoapFaultCode.Client, "Aucun client ne porte ce numéro.")
        {
            SubCode = "INTROUVABLE",
            Actor = "urn:mini-erp",
            Detail = new XElement(Ns + "erreur", new XElement(Ns + "code", "404")),
        };

        SoapMessage message = SoapEnvelope.Parse(SoapEnvelope.CreateFault(fault, version).ToString());

        Assert.True(message.IsFault);
        Assert.Equal(SoapFaultCode.Client, message.Fault.Code);
        Assert.Equal("Aucun client ne porte ce numéro.", message.Fault.Reason);
        Assert.Equal("INTROUVABLE", message.Fault.SubCode);
        Assert.Equal("urn:mini-erp", message.Fault.Actor);
        Assert.Equal("404", message.Fault.Detail!.Element(Ns + "code")!.Value);
    }

    [Theory]
    [InlineData(SoapVersion.Soap11, "soap:Server")]
    [InlineData(SoapVersion.Soap12, "soap:Receiver")]
    public void Les_codes_suivent_la_version(SoapVersion version, string expected)
    {
        string xml = SoapEnvelope.CreateFault(new SoapFault(SoapFaultCode.Server, "panne"), version).ToString();

        Assert.Contains(expected, xml, StringComparison.Ordinal);
        Assert.Equal(SoapFaultCode.Server, SoapEnvelope.Parse(xml).Fault!.Code);
    }

    [Fact]
    public void Les_codes_rares_sont_reconnus()
    {
        Assert.Equal(SoapFaultCode.VersionMismatch, SoapEnvelope.Parse(SoapEnvelope.CreateFault(new SoapFault(SoapFaultCode.VersionMismatch, "v"), SoapVersion.Soap12).ToString()).Fault!.Code);
        Assert.Equal(SoapFaultCode.MustUnderstand, SoapEnvelope.Parse(SoapEnvelope.CreateFault(new SoapFault(SoapFaultCode.MustUnderstand, "m")).ToString()).Fault!.Code);
    }

    [Fact]
    public void Un_corps_vide_n_a_pas_d_element()
    {
        SoapMessage message = SoapEnvelope.Parse(SoapEnvelope.Create(null).ToString());

        Assert.Null(message.Body);
        Assert.False(message.IsFault);
    }

    [Fact]
    public void Le_content_type_et_l_action_suivent_la_version()
    {
        Assert.Equal("text/xml; charset=utf-8", SoapEnvelope.ContentType(SoapVersion.Soap11, "ignoree"));
        Assert.Equal("application/soap+xml; charset=utf-8; action=\"urn:a/B\"", SoapEnvelope.ContentType(SoapVersion.Soap12, "urn:a/B"));
        Assert.Equal(SoapVersion.Soap12, SoapEnvelope.DetectVersion("application/soap+xml; charset=utf-8; action=\"urn:a/B\""));
        Assert.Equal(SoapVersion.Soap11, SoapEnvelope.DetectVersion("TEXT/XML"));
        Assert.Null(SoapEnvelope.DetectVersion("application/json"));
        Assert.Null(SoapEnvelope.DetectVersion(null));
        Assert.Equal("urn:a/B", SoapEnvelope.ActionFromContentType("application/soap+xml; action=\"urn:a/B\""));
        Assert.Null(SoapEnvelope.ActionFromContentType("text/xml; charset=utf-8"));
        Assert.Null(SoapEnvelope.ActionFromContentType(" "));
    }

    [Fact]
    public void Lit_depuis_un_flux_sans_le_fermer()
    {
        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(SoapEnvelope.Create(new XElement(Ns + "A")).ToString()));

        SoapMessage message = SoapEnvelope.Parse(stream);

        Assert.Equal(Ns + "A", message.Body!.Name);
        Assert.True(stream.CanRead);
    }

    [Fact]
    [Trait("hazard", "xml-external-entity")]
    public void Une_dtd_et_ses_entites_externes_sont_refusees()
    {
        const string xxe = """
            <?xml version="1.0"?>
            <!DOCTYPE foo [ <!ENTITY xxe SYSTEM "file:///c:/windows/win.ini"> ]>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><a>&xxe;</a></soap:Body></soap:Envelope>
            """;
        const string bomb = """
            <?xml version="1.0"?>
            <!DOCTYPE lolz [ <!ENTITY lol "lol"> <!ENTITY lol2 "&lol;&lol;&lol;&lol;"> ]>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><a>&lol2;</a></soap:Body></soap:Envelope>
            """;

        Assert.Throws<FormatException>(() => SoapEnvelope.Parse(xxe));
        Assert.Throws<FormatException>(() => SoapEnvelope.Parse(bomb));
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData("<pas du xml")]
    [InlineData("<Envelope/>")]
    [InlineData("<soap:Body xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"/>")]
    [InlineData("<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"/>")]
    public void Un_document_qui_n_est_pas_une_enveloppe_est_refuse(string xml)
    {
        Assert.Throws<FormatException>(() => SoapEnvelope.Parse(xml));
        Assert.False(SoapEnvelope.TryParse(xml, out _, out string? error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void Un_texte_vide_est_refuse()
    {
        Assert.Throws<FormatException>(() => SoapEnvelope.Parse(string.Empty));
        Assert.False(SoapEnvelope.TryParse("   ", out _, out _));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void Les_entrees_nulles_sont_refusees_et_TryParse_ne_leve_jamais()
    {
        Assert.Throws<ArgumentNullException>(() => SoapEnvelope.Parse((string)null!));
        Assert.Throws<ArgumentNullException>(() => SoapEnvelope.Parse((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => SoapEnvelope.CreateFault(null!));
        Assert.False(SoapEnvelope.TryParse(null, out SoapMessage? message, out string? error));
        Assert.Null(message);
        Assert.NotNull(error);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void La_taille_maximale_borne_la_lecture()
    {
        string xml = SoapEnvelope.Create(new XElement(Ns + "A", new string('x', 500))).ToString();

        Assert.Throws<FormatException>(() => SoapEnvelope.Parse(xml, new SoapEnvelopeOptions { MaxCharacters = 100 }));
        Assert.NotNull(SoapEnvelope.Parse(xml, new SoapEnvelopeOptions { MaxCharacters = 10_000 }).Body);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoapEnvelopeOptions { MaxCharacters = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => SoapEnvelope.Parse(xml, new SoapEnvelopeOptions { MaxCharacters = 0 }));
        Assert.False(SoapEnvelope.TryParse(xml, out _, out _, new SoapEnvelopeOptions { MaxCharacters = 0 }));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void Les_textes_unicode_voyagent()
    {
        const string reason = "Stock insuffisant pour « Écrou » — 東京 🔩";
        SoapMessage message = SoapEnvelope.Parse(SoapEnvelope.CreateFault(new SoapFault(SoapFaultCode.Client, reason), SoapVersion.Soap12).ToString());

        Assert.Equal(reason, message.Fault!.Reason);
    }

    [Fact]
    public void Les_espaces_de_noms_sont_exposes()
    {
        Assert.Equal(SoapEnvelope.Soap11Namespace, SoapEnvelope.EnvelopeNamespace(SoapVersion.Soap11));
        Assert.Equal(SoapEnvelope.Soap12Namespace, SoapEnvelope.EnvelopeNamespace(SoapVersion.Soap12));
    }
}
