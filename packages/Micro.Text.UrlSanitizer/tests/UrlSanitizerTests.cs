using Xunit;

namespace Micro.Text.UrlSanitizer.Tests;

public sealed class UrlSanitizerTests
{
    // --- Cas nominaux ---

    [Fact]
    public void Sanitize_TokenDansQueryString_EstMasque()
    {
        const string url      = "https://api.example.com/data?token=secret123&page=1";
        const string expected = "https://api.example.com/data?token=***&page=1";

        Assert.Equal(expected, UrlSanitizer.Sanitize(url));
    }

    [Fact]
    public void Sanitize_ApiKeyDansQueryString_EstMasque()
    {
        const string url      = "https://api.example.com/v1?api_key=MYKEY&format=json";
        const string expected = "https://api.example.com/v1?api_key=***&format=json";

        Assert.Equal(expected, UrlSanitizer.Sanitize(url));
    }

    [Fact]
    public void Sanitize_PlusieursParamsSensibles_TousSontMasques()
    {
        const string url      = "https://host/ep?token=abc&secret=xyz&page=2";
        const string expected = "https://host/ep?token=***&secret=***&page=2";

        Assert.Equal(expected, UrlSanitizer.Sanitize(url));
    }

    [Fact]
    public void Sanitize_AucunParamSensible_UrlInchangee()
    {
        const string url = "https://example.com/path?page=1&size=10";

        Assert.Equal(url, UrlSanitizer.Sanitize(url));
    }

    [Fact]
    public void Sanitize_SansQueryString_UrlInchangee()
    {
        const string url = "https://example.com/path";

        Assert.Equal(url, UrlSanitizer.Sanitize(url));
    }

    // --- Cas limites ---

    [Theory]
    [InlineData(null,  "")]
    [InlineData("",    "")]
    public void Sanitize_UrlNullOuVide_RetourneVide(string? url, string expected)
    {
        Assert.Equal(expected, UrlSanitizer.Sanitize(url));
    }

    [Fact]
    public void Sanitize_NomParametreEnMajuscules_EstMasqueInsensibleCasse()
    {
        const string url      = "https://host/?TOKEN=abc";
        const string expected = "https://host/?TOKEN=***";

        Assert.Equal(expected, UrlSanitizer.Sanitize(url));
    }

    [Fact]
    public void Sanitize_MasquePersonnalise_EstUtilise()
    {
        var opts = new UrlSanitizerOptions { Mask = "[REDACTED]" };
        const string url      = "https://host/?token=abc";
        const string expected = "https://host/?token=[REDACTED]";

        Assert.Equal(expected, UrlSanitizer.Sanitize(url, opts));
    }

    // --- Identifiants d'URL (correctif 1.1.0) ---
    //
    // La version 1.0.0 ne traitait que la query string : « https://user:pass@hote »
    // laissait le mot de passe en clair dans le journal — le scénario même que ce
    // package existe pour empêcher.

    [Theory]
    [InlineData("https://user:motdepasse@api.local/x", "https://user:***@api.local/x")]
    [InlineData("https://user:pass@api.local/x?token=abc", "https://user:***@api.local/x?token=***")]
    [InlineData("ftp://admin:secret@serveur/fichier", "ftp://admin:***@serveur/fichier")]
    public void Sanitize_MotDePasseDansLUrl_EstMasque(string url, string expected) =>
        Assert.Equal(expected, UrlSanitizer.Sanitize(url));

    [Fact]
    public void Sanitize_ConserveLeNomDUtilisateur()
    {
        // Le nom sert au diagnostic ; le secret est le mot de passe.
        Assert.Contains("admin", UrlSanitizer.Sanitize("https://admin:secret@host/x"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://user@api.local/x")]           // pas de mot de passe
    [InlineData("https://api.local/chemin/a@b")]        // arobase dans le chemin
    [InlineData("https://api.local/x?destinataire=a@b")] // arobase dans la query
    [InlineData("chaine-sans-schema")]
    public void Sanitize_SansIdentifiants_LaisseLUrlIntacte(string url) =>
        Assert.Equal(url, UrlSanitizer.Sanitize(url));

    [Fact]
    public void Sanitize_MaskUserInfoDesactive_LaisseLesIdentifiants()
    {
        var opts = new UrlSanitizerOptions { MaskUserInfo = false };
        const string url = "https://user:pass@api.local/x";

        Assert.Equal(url, UrlSanitizer.Sanitize(url, opts));
    }

    [Fact]
    public void MaskUserInfo_EstActifParDefaut() =>
        Assert.True(new UrlSanitizerOptions().MaskUserInfo);

    [Fact]
    public void Sanitize_ParamSensiblePersonnalise_EstMasque()
    {
        var opts = new UrlSanitizerOptions { SensitiveQueryParams = ["custom_auth"] };
        const string url      = "https://host/?custom_auth=s3cr3t&other=ok";
        const string expected = "https://host/?custom_auth=***&other=ok";

        Assert.Equal(expected, UrlSanitizer.Sanitize(url, opts));
    }
}