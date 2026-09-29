using Xunit;

namespace Micro.Net.MacAddress.Tests;

public sealed class MacAddressParserTests
{
    // ---------- Formats acceptés en entrée ----------

    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF")]
    [InlineData("aa:bb:cc:dd:ee:ff")]
    [InlineData("AA-BB-CC-DD-EE-FF")]
    [InlineData("aa bb cc dd ee ff")]
    [InlineData("AABBCCDDEEFF")]
    [InlineData("aabbccddeeff")]
    [InlineData("aabb.ccdd.eeff")]
    [InlineData("  AA:BB:CC:DD:EE:FF  ")]
    public void Tous_Les_Formats_Courants_Convergent_Vers_La_Meme_Forme(string entree) =>
        Assert.Equal("AA:BB:CC:DD:EE:FF", MacAddressParser.Normalize(entree));

    // ---------- Formats produits en sortie ----------

    [Theory]
    [InlineData(MacAddressFormat.Colon, true, "AA:BB:CC:DD:EE:FF")]
    [InlineData(MacAddressFormat.Colon, false, "aa:bb:cc:dd:ee:ff")]
    [InlineData(MacAddressFormat.Hyphen, true, "AA-BB-CC-DD-EE-FF")]
    [InlineData(MacAddressFormat.Bare, true, "AABBCCDDEEFF")]
    [InlineData(MacAddressFormat.Bare, false, "aabbccddeeff")]
    [InlineData(MacAddressFormat.Cisco, false, "aabb.ccdd.eeff")]
    [InlineData(MacAddressFormat.Cisco, true, "AABB.CCDD.EEFF")]
    public void La_Convention_D_Ecriture_Est_Respectee(MacAddressFormat format, bool majuscules, string attendu) =>
        Assert.Equal(attendu, MacAddressParser.Normalize("AA:BB:CC:DD:EE:FF", new MacAddressOptions
        {
            Format = format,
            Uppercase = majuscules,
        }));

    // ---------- Attributs de l'adresse ----------

    [Fact]
    public void L_Adresse_Expose_Ses_Octets_Et_Son_Prefixe_Fabricant()
    {
        MacAddressInfo info = MacAddressParser.Parse("3C:22:FB:01:02:03");

        Assert.Equal("3C22FB", info.Oui);
        Assert.Equal(new byte[] { 0x3C, 0x22, 0xFB, 0x01, 0x02, 0x03 }, info.Bytes);
        Assert.Equal("3C:22:FB:01:02:03", info.Formatted);
        Assert.Equal(info.Formatted, info.ToString());
    }

    [Theory]
    [InlineData("3C:22:FB:01:02:03", false, false)] // Attribuée par l'IEEE, monodiffusion.
    [InlineData("02:00:00:00:00:01", true, false)]  // Bit « administré localement » posé.
    [InlineData("01:00:5E:00:00:FB", false, true)]  // Multidiffusion IPv4 (mDNS).
    [InlineData("33:33:00:00:00:FB", true, true)]   // Multidiffusion IPv6.
    public void Les_Bits_De_Portee_Du_Premier_Octet_Sont_Lisibles(string adresse, bool local, bool multi)
    {
        MacAddressInfo info = MacAddressParser.Parse(adresse);

        Assert.Equal(local, info.IsLocallyAdministered);
        Assert.Equal(multi, info.IsMulticast);
    }

    [Fact]
    public void Une_Adresse_Aleatoire_Se_Reconnait_A_Son_Bit_Local()
    {
        // Les systèmes récents tirent une adresse aléatoire par réseau pour éviter le pistage :
        // elle porte toujours le bit « administré localement », et son OUI ne désigne personne.
        Assert.True(MacAddressParser.Parse("DA:A1:19:9E:2F:88").IsLocallyAdministered);
        Assert.False(MacAddressParser.Parse("D8:A1:19:9E:2F:88").IsLocallyAdministered);
    }

    // ---------- Variante non levante ----------

    [Fact]
    public void TryParse_Reussit_Et_Renseigne_La_Sortie()
    {
        Assert.True(MacAddressParser.TryParse("aabb.ccdd.eeff", null, out MacAddressInfo? info));
        Assert.NotNull(info);
        Assert.Equal("AA:BB:CC:DD:EE:FF", info?.Formatted);
    }

    [Fact]
    public void TryParse_Ne_Leve_Jamais_Quelle_Que_Soit_L_Entree()
    {
        // Contrat du motif TryXxx : aucune entrée, si aberrante soit-elle, ne produit d'exception.
        string?[] entrees =
        [
            null, "", "   ", ":::::", "::::::", "-", ".", "bonjour", "AA:BB:CC:DD:EE",
            "AA:BB:CC:DD:EE:FF:00", "AAA:BB:CC:DD:EE:FF", "GG:BB:CC:DD:EE:FF", "AABBCCDDEEF",
            "AABBCCDDEEFFF", "aabb.ccdd.eef", "\0", "AA:BB:CC:DD:EE:FF\n", new string('a', 4096),
        ];

        foreach (string? entree in entrees)
        {
            bool ok = MacAddressParser.TryParse(entree, new MacAddressOptions { Format = (MacAddressFormat)99 }, out MacAddressInfo? avecFormatInvalide);
            Assert.False(ok);
            Assert.Null(avecFormatInvalide);

            MacAddressParser.TryParse(entree, null, out MacAddressInfo? info);
            Assert.True(info is null || info.Bytes.Count == 6);
        }
    }

    // ---------- Erreurs de paramétrage ----------

    [Fact]
    public void Options_Validate_Refuse_Une_Convention_Inconnue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MacAddressOptions { Format = (MacAddressFormat)42 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => MacAddressParser.Normalize("AABBCCDDEEFF", new MacAddressOptions { Format = (MacAddressFormat)42 }));

        new MacAddressOptions { Format = MacAddressFormat.Cisco, Uppercase = false }.Validate();
    }

    // ---------- Aléas déclarés ----------

    [Fact]
    [Trait("hazard", "null-input")]
    public void Entree_Nulle_Est_Signalee_Et_Jamais_Deferencee()
    {
        Assert.Throws<ArgumentNullException>(() => MacAddressParser.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => MacAddressParser.Normalize(null!));
        Assert.False(MacAddressParser.TryParse(null, null, out MacAddressInfo? info));
        Assert.Null(info);
    }

    [Theory]
    [Trait("hazard", "empty-input")]
    [InlineData("")]
    [InlineData("   ")]
    public void Entree_Vide_Est_Refusee(string entree)
    {
        Assert.Throws<FormatException>(() => MacAddressParser.Parse(entree));
        Assert.False(MacAddressParser.TryParse(entree, null, out _));
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData("bonjour")]
    [InlineData("AA:BB:CC:DD:EE")]                 // Cinq groupes.
    [InlineData("AA:BB:CC:DD:EE:FF:11")]           // Sept groupes.
    [InlineData("AAA:BB:CC:DD:EE:FF")]             // Groupe de trois chiffres.
    [InlineData("A:B:C:D:E:F")]                    // Groupes d'un chiffre.
    [InlineData("GG:BB:CC:DD:EE:FF")]              // « G » n'est pas hexadécimal.
    [InlineData("AABBCCDDEEF")]                    // Onze chiffres.
    [InlineData("AABBCCDDEEFFF")]                  // Treize chiffres.
    [InlineData("aabb.ccdd.eef")]                  // Groupe Cisco tronqué.
    [InlineData("192.168.1.1")]                    // Une adresse IP n'est pas une adresse MAC.
    [InlineData("AA:BB:CC:DD:EE:FF:00:11")]        // EUI-64 : hors périmètre, refusé explicitement.
    public void Entree_Mal_Formee_Est_Refusee_Sans_Resultat_Partiel(string entree)
    {
        Assert.Throws<FormatException>(() => MacAddressParser.Parse(entree));
        Assert.False(MacAddressParser.TryParse(entree, null, out MacAddressInfo? info));
        Assert.Null(info);
    }

    [Theory]
    [Trait("hazard", "unicode-edge")]
    [InlineData("ＡＡ:ＢＢ:ＣＣ:ＤＤ:ＥＥ:ＦＦ")]   // Chiffres pleine chasse.
    [InlineData("١٢:٣٤:٥٦:٧٨:٩٠:١٢")]              // Chiffres arabes.
    [InlineData("АА:ВВ:СС:DD:EE:FF")]              // « А » et « В » cyrilliques, homoglyphes des latines.
    [InlineData("AA:BB:CC:DD:EE:F́F")]        // Accent combinant inséré dans un groupe.
    public void Les_Homoglyphes_Et_Chiffres_Non_ASCII_Sont_Refuses(string entree)
    {
        // char.IsAsciiHexDigit tranche : un chiffre qui ressemble à un chiffre n'en est pas un.
        // Les accepter reviendrait à rapprocher deux adresses visuellement identiques mais
        // distinctes en mémoire — ou à en fabriquer une qui n'existe pas.
        Assert.Throws<FormatException>(() => MacAddressParser.Parse(entree));
        Assert.False(MacAddressParser.TryParse(entree, null, out _));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void Les_Adresses_Extremes_Sont_Reconnues_Pour_Ce_Qu_Elles_Sont()
    {
        MacAddressInfo diffusion = MacAddressParser.Parse("FF:FF:FF:FF:FF:FF");
        Assert.True(diffusion.IsBroadcast);
        Assert.True(diffusion.IsMulticast);            // La diffusion est un cas de multidiffusion.
        Assert.True(diffusion.IsLocallyAdministered);  // Les deux bits de portée sont posés.
        Assert.False(diffusion.IsUnspecified);

        MacAddressInfo vide = MacAddressParser.Parse("00:00:00:00:00:00");
        Assert.True(vide.IsUnspecified);
        Assert.False(vide.IsBroadcast);
        Assert.False(vide.IsMulticast);
        Assert.False(vide.IsLocallyAdministered);
        Assert.Equal("000000", vide.Oui);

        MacAddressInfo ordinaire = MacAddressParser.Parse("3C:22:FB:01:02:03");
        Assert.False(ordinaire.IsBroadcast);
        Assert.False(ordinaire.IsUnspecified);
    }
}
