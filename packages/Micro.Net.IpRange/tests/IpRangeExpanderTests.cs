using System.Net;
using Xunit;

namespace Micro.Net.IpRange.Tests;

public sealed class IpRangeExpanderTests
{
    private static readonly IpRangeOptions Inclusive = new() { ExcludeNetworkAndBroadcast = false };

    // ---------- Cas nominaux ----------

    [Fact]
    public void Cidr_Enumere_Les_Adresses_Hotes()
    {
        IReadOnlyList<IPAddress> addresses = IpRangeExpander.Expand("192.168.1.0/24");

        Assert.Equal(254, addresses.Count);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), addresses[0]);
        Assert.Equal(IPAddress.Parse("192.168.1.254"), addresses[^1]);
    }

    [Fact]
    public void Cidr_Sans_Exclusion_Conserve_Reseau_Et_Diffusion()
    {
        IReadOnlyList<IPAddress> addresses = IpRangeExpander.Expand("192.168.1.0/24", Inclusive);

        Assert.Equal(256, addresses.Count);
        Assert.Equal(IPAddress.Parse("192.168.1.0"), addresses[0]);
        Assert.Equal(IPAddress.Parse("192.168.1.255"), addresses[^1]);
    }

    [Fact]
    public void Cidr_Ignore_Les_Bits_Hote_De_L_Adresse_Fournie()
    {
        IReadOnlyList<IPAddress> depuisHote = IpRangeExpander.Expand("192.168.1.42/24");
        IReadOnlyList<IPAddress> depuisReseau = IpRangeExpander.Expand("192.168.1.0/24");

        Assert.Equal(depuisReseau, depuisHote);
    }

    [Fact]
    public void Intervalle_Borne_Inclut_Les_Deux_Bornes()
    {
        IReadOnlyList<IPAddress> addresses = IpRangeExpander.Expand("10.0.0.10-10.0.0.13");

        Assert.Equal(
            new[] { IPAddress.Parse("10.0.0.10"), IPAddress.Parse("10.0.0.11"), IPAddress.Parse("10.0.0.12"), IPAddress.Parse("10.0.0.13") },
            addresses);
    }

    [Fact]
    public void Intervalle_Abrege_Complete_Le_Dernier_Octet()
    {
        IReadOnlyList<IPAddress> abrege = IpRangeExpander.Expand("10.0.0.10-13");
        IReadOnlyList<IPAddress> complet = IpRangeExpander.Expand("10.0.0.10-10.0.0.13");

        Assert.Equal(complet, abrege);
    }

    [Fact]
    public void Adresse_Unique_Produit_Une_Seule_Adresse()
    {
        IReadOnlyList<IPAddress> addresses = IpRangeExpander.Expand("192.168.0.1");

        Assert.Equal(IPAddress.Parse("192.168.0.1"), Assert.Single(addresses));
    }

    [Fact]
    public void Union_Trie_Les_Adresses_Et_Supprime_Les_Doublons()
    {
        IReadOnlyList<IPAddress> addresses = IpRangeExpander.Expand("192.168.1.5 ; 192.168.1.1, 192.168.1.5");

        Assert.Equal(new[] { IPAddress.Parse("192.168.1.1"), IPAddress.Parse("192.168.1.5") }, addresses);
    }

    [Fact]
    public void Union_De_Segments_Recouvrants_Ne_Duplique_Pas()
    {
        IReadOnlyList<IPAddress> addresses = IpRangeExpander.Expand("192.168.1.0/30, 192.168.1.2");

        Assert.Equal(new[] { IPAddress.Parse("192.168.1.1"), IPAddress.Parse("192.168.1.2") }, addresses);
    }

    // ---------- Comptage ----------

    [Fact]
    public void CountAddresses_Mesure_Sans_Materialiser_Ni_Buter_Sur_La_Borne()
    {
        Assert.Equal(16_777_214L, IpRangeExpander.CountAddresses("10.0.0.0/8"));
        Assert.Equal(16_777_216L, IpRangeExpander.CountAddresses("10.0.0.0/8", Inclusive));
    }

    [Fact]
    public void CountAddresses_Somme_Les_Segments_Avant_Deduplication()
    {
        // Contrat documenté : le compte précède la déduplication, il majore donc le résultat réel.
        Assert.Equal(3L, IpRangeExpander.CountAddresses("192.168.1.1, 192.168.1.1, 192.168.1.2"));
        Assert.Equal(2, IpRangeExpander.Expand("192.168.1.1, 192.168.1.1, 192.168.1.2").Count);
    }

    // ---------- Variante non levante ----------

    [Fact]
    public void TryExpand_Reussit_Sur_Une_Expression_Valide()
    {
        Assert.True(IpRangeExpander.TryExpand("172.16.0.0/30", null, out IReadOnlyList<IPAddress> addresses));
        Assert.Equal(2, addresses.Count);
    }

    [Fact]
    public void TryExpand_Echoue_Sans_Lever_Sur_Une_Plage_Trop_Large()
    {
        Assert.False(IpRangeExpander.TryExpand("10.0.0.0/8", null, out IReadOnlyList<IPAddress> addresses));
        Assert.Empty(addresses);
    }

    [Fact]
    public void TryExpand_Echoue_Sans_Lever_Sur_Un_Parametrage_Invalide()
    {
        Assert.False(IpRangeExpander.TryExpand("192.168.1.1", new IpRangeOptions { MaxAddresses = 0 }, out IReadOnlyList<IPAddress> addresses));
        Assert.Empty(addresses);
    }

    [Fact]
    public void TryExpand_Ne_Leve_Jamais_Quelle_Que_Soit_L_Entree()
    {
        // Contrat du motif TryXxx : aucune entrée, si aberrante soit-elle, ne doit produire
        // d'exception — c'est ce qui permet de l'appeler directement sur une saisie humaine.
        string?[] entrees =
        [
            null, "", "   ", ",,,", "bonjour", "999.999.999.999", "192.168.1.0/99", "192.168.1.0/",
            "/24", "-", "192.168.1.5-", "-192.168.1.5", "2001:db8::1", "0.0.0.0/0", "192.168.1.1/24/8",
            "\0", "192.168.1.1\n192.168.1.2", new string('9', 4096),
        ];

        foreach (string? entree in entrees)
        {
            bool ok = IpRangeExpander.TryExpand(entree, new IpRangeOptions { MaxAddresses = -5 }, out IReadOnlyList<IPAddress> avecOptionsInvalides);
            Assert.False(ok);
            Assert.Empty(avecOptionsInvalides);

            IpRangeExpander.TryExpand(entree, null, out IReadOnlyList<IPAddress> addresses);
            Assert.NotNull(addresses);
        }
    }

    // ---------- Erreurs de paramétrage ----------

    [Fact]
    public void Options_Validate_Refuse_Une_Borne_Nulle_Ou_Negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpRangeOptions { MaxAddresses = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpRangeOptions { MaxAddresses = -1 }.Validate());
        new IpRangeOptions { MaxAddresses = 1, ExcludeNetworkAndBroadcast = true }.Validate();
    }

    [Fact]
    public void Expand_Refuse_Une_Plage_Plus_Large_Que_La_Borne()
    {
        // Un /16 tient tout juste sous la borne par défaut (65 534 hôtes) ; un /15 la dépasse.
        Assert.Equal(65_534, IpRangeExpander.Expand("192.168.0.0/16").Count);

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
            () => IpRangeExpander.Expand("192.168.0.0/15"));

        Assert.Equal("expression", error.ParamName);
    }

    // ---------- Aléas déclarés ----------

    [Fact]
    [Trait("hazard", "null-input")]
    public void Entree_Nulle_Est_Signalee_Et_Jamais_Deferencee()
    {
        Assert.Throws<ArgumentNullException>(() => IpRangeExpander.Expand(null!));
        Assert.Throws<ArgumentNullException>(() => IpRangeExpander.CountAddresses(null!));
        Assert.False(IpRangeExpander.TryExpand(null, null, out IReadOnlyList<IPAddress> addresses));
        Assert.Empty(addresses);
    }

    [Theory]
    [Trait("hazard", "empty-input")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",;,")]
    public void Entree_Vide_Est_Refusee_Comme_Mal_Formee(string expression)
    {
        Assert.Throws<FormatException>(() => IpRangeExpander.Expand(expression));
        Assert.False(IpRangeExpander.TryExpand(expression, null, out _));
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData("bonjour")]
    [InlineData("192.168.1.0/33")]
    [InlineData("192.168.1.0/-1")]
    [InlineData("192.168.1.0/vingt")]
    [InlineData("192.168.1.300")]
    [InlineData("192.168.1.10-192.168.1.1")]
    [InlineData("192.168.1.10-999")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:db8::/64")]
    public void Entree_Mal_Formee_Est_Refusee_Sans_Resultat_Partiel(string expression)
    {
        Assert.Throws<FormatException>(() => IpRangeExpander.Expand(expression));
        Assert.False(IpRangeExpander.TryExpand(expression, null, out IReadOnlyList<IPAddress> addresses));
        Assert.Empty(addresses);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void Prefixes_Extremes_Restent_Coherents()
    {
        // /32 : un hôte. /31 : liaison point à point, les deux adresses sont utilisables.
        // /30 : deux hôtes une fois réseau et diffusion écartés.
        Assert.Single(IpRangeExpander.Expand("192.168.1.7/32"));
        Assert.Equal(2, IpRangeExpander.Expand("192.168.1.6/31").Count);
        Assert.Equal(2, IpRangeExpander.Expand("192.168.1.4/30").Count);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void La_Borne_Est_Atteignable_Mais_Pas_Depassable()
    {
        Assert.Equal(254, IpRangeExpander.Expand("192.168.1.0/24", new IpRangeOptions { MaxAddresses = 254 }).Count);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IpRangeExpander.Expand("192.168.1.0/24", new IpRangeOptions { MaxAddresses = 253 }));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void L_Espace_IPv4_Entier_Se_Compte_Sans_Deborder()
    {
        // 2^32 adresses ne tiennent pas dans un Int32 : le compte est porté par un Int64,
        // et l'expansion refuse proprement au lieu de boucler ou de déborder.
        Assert.Equal(4_294_967_296L, IpRangeExpander.CountAddresses("0.0.0.0/0", Inclusive));
        Assert.Equal(4_294_967_294L, IpRangeExpander.CountAddresses("0.0.0.0/0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => IpRangeExpander.Expand("0.0.0.0/0", Inclusive));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IpRangeExpander.Expand("0.0.0.0/0", new IpRangeOptions { MaxAddresses = int.MaxValue }));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void Les_Extremites_De_L_Espace_IPv4_Sont_Representables()
    {
        Assert.Equal(IPAddress.Parse("0.0.0.0"), Assert.Single(IpRangeExpander.Expand("0.0.0.0")));
        Assert.Equal(IPAddress.Parse("255.255.255.255"), Assert.Single(IpRangeExpander.Expand("255.255.255.255")));
        Assert.Equal(2, IpRangeExpander.Expand("255.255.255.254-255.255.255.255").Count);
    }
}
