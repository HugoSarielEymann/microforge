using Xunit;

namespace Micro.Sql.NamedParameters.Tests;

public sealed class SqlParameterScannerTests
{
    [Fact]
    public void Releve_les_parametres_dans_l_ordre()
    {
        IReadOnlyList<string> names = SqlParameterScanner.Names("SELECT * FROM t WHERE a = @a AND b = @beta_2");

        Assert.Equal(["a", "beta_2"], names);
    }

    [Fact]
    public void Un_parametre_repete_n_apparait_qu_une_fois_dans_les_noms_mais_a_chaque_occurrence_au_releve()
    {
        const string sql = "UPDATE t SET x = @v WHERE y = @v";

        Assert.Equal(["v"], SqlParameterScanner.Names(sql));
        Assert.Equal(2, SqlParameterScanner.Scan(sql).Count);
    }

    [Fact]
    public void Donne_la_position_et_la_longueur_de_chaque_occurrence()
    {
        const string sql = "WHERE id = @id";

        SqlParameterToken token = Assert.Single(SqlParameterScanner.Scan(sql));

        Assert.Equal("id", token.Name);
        Assert.Equal('@', token.Prefix);
        Assert.Equal(11, token.Start);
        Assert.Equal(3, token.Length);
        Assert.Equal("@id", sql.Substring(token.Start, token.Length));
    }

    [Theory]
    [InlineData("WHERE x = '@non' AND y = @oui")]
    [InlineData("WHERE x = 'l''@non' AND y = @oui")]
    [InlineData("WHERE \"@non\" = @oui")]
    [InlineData("WHERE [@non]]x] = @oui")]
    [InlineData("WHERE `@non` = @oui")]
    [InlineData("-- @non\nSELECT @oui")]
    [InlineData("/* @non */ SELECT @oui")]
    [InlineData("SELECT E'a\\'@non' , @oui")]
    [InlineData("DO $$ @non $$; SELECT @oui")]
    [InlineData("DO $corps$ @non $x$ @non $corps$; SELECT @oui")]
    public void Ignore_chaines_identifiants_et_commentaires(string sql)
    {
        Assert.Equal(["oui"], SqlParameterScanner.Names(sql));
    }

    [Fact]
    public void Imbrique_les_commentaires_par_defaut_et_pas_sur_demande()
    {
        const string sql = "/* a /* @non */ encore @non */ SELECT @oui";

        Assert.Equal(["oui"], SqlParameterScanner.Names(sql));
        Assert.Equal(["non", "oui"], SqlParameterScanner.Names(sql, new SqlParameterScanOptions { NestedBlockComments = false }));
    }

    [Fact]
    public void Ecarte_les_variables_systeme_et_les_adresses()
    {
        Assert.Equal(["x"], SqlParameterScanner.Names("SELECT @@IDENTITY, @@ROWCOUNT, @x, contact@domaine.fr"));
    }

    [Fact]
    public void Ecarte_les_conversions_postgresql_avec_le_prefixe_deux_points()
    {
        SqlParameterScanOptions options = new() { Prefixes = "@:" };

        Assert.Equal(["b", "c"], SqlParameterScanner.Names("SELECT a::int, :b, @c, x := 1", options));
    }

    [Fact]
    public void Ne_reconnait_que_les_prefixes_demandes()
    {
        SqlParameterScanOptions options = new() { Prefixes = ":" };

        Assert.Equal(["deux"], SqlParameterScanner.Names("SELECT @un, :deux", options));
    }

    [Fact]
    public void Le_prefixe_dollar_fonctionne_sans_chaines_dollar()
    {
        SqlParameterScanOptions options = new() { Prefixes = "$", DollarQuotedStrings = false };

        Assert.Equal(["nom"], SqlParameterScanner.Names("SELECT $nom, $1", options));
    }

    [Fact]
    public void Un_parametre_positionnel_postgresql_n_ouvre_pas_de_chaine_dollar()
    {
        Assert.Equal(["x"], SqlParameterScanner.Names("SELECT $1, @x"));
    }

    [Fact]
    public void Le_comparateur_decide_des_doublons()
    {
        const string sql = "SELECT @Id, @id";

        Assert.Equal(["Id", "id"], SqlParameterScanner.Names(sql));
        Assert.Equal(["Id"], SqlParameterScanner.Names(sql, new SqlParameterScanOptions { NameComparer = StringComparer.OrdinalIgnoreCase }));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void Une_requete_nulle_ne_contient_aucun_parametre()
    {
        Assert.Empty(SqlParameterScanner.Scan(null));
        Assert.Empty(SqlParameterScanner.Names(null));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void Une_requete_vide_ne_contient_aucun_parametre()
    {
        Assert.Empty(SqlParameterScanner.Names(string.Empty));
        Assert.Empty(SqlParameterScanner.Names("   \n  "));
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData("SELECT @a, 'jamais fermé @b")]
    [InlineData("SELECT @a /* jamais fermé @b")]
    [InlineData("SELECT @a, [jamais fermé @b")]
    [InlineData("SELECT @a, \"jamais fermé @b")]
    [InlineData("SELECT @a; DO $$ jamais fermé @b")]
    public void Une_requete_mal_fermee_ne_leve_pas_et_ignore_la_suite(string sql)
    {
        Assert.Equal(["a"], SqlParameterScanner.Names(sql));
    }

    [Theory]
    [Trait("hazard", "boundary-value")]
    [InlineData("SELECT @")]
    [InlineData("SELECT @1")]
    [InlineData("@")]
    [InlineData("SELECT @ nom")]
    public void Un_prefixe_sans_nom_n_est_pas_un_parametre(string sql)
    {
        Assert.Empty(SqlParameterScanner.Names(sql));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void Un_parametre_en_toute_fin_de_texte_est_releve()
    {
        Assert.Equal(["z"], SqlParameterScanner.Names("@z"));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void Les_noms_accentues_composes_ou_non_sont_entiers()
    {
        const string decompose = "SELECT @prénom";

        Assert.Equal(["prénom", "ville_été"], SqlParameterScanner.Names("SELECT @prénom, @ville_été"));
        Assert.Equal(["prénom"], SqlParameterScanner.Names(decompose));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void Un_emoji_termine_le_nom()
    {
        Assert.Equal(["nom"], SqlParameterScanner.Names("SELECT @nom\U0001F600"));
    }

    [Fact]
    public void Les_options_nulles_prennent_les_defauts()
    {
        Assert.Equal(["a"], SqlParameterScanner.Names("SELECT @a", null));
    }

    [Fact]
    public void Refuse_des_prefixes_vides_ou_inconnus()
    {
        Assert.Throws<ArgumentException>(() => SqlParameterScanner.Names("x", new SqlParameterScanOptions { Prefixes = string.Empty }));
        Assert.Throws<ArgumentException>(() => SqlParameterScanner.Names("x", new SqlParameterScanOptions { Prefixes = "#" }));
    }

    [Fact]
    public void Refuse_le_prefixe_dollar_avec_les_chaines_dollar()
    {
        Assert.Throws<ArgumentException>(() => new SqlParameterScanOptions { Prefixes = "@$" }.Validate());
    }

    [Fact]
    public void Refuse_un_parametrage_nul()
    {
        Assert.Throws<ArgumentNullException>(() => new SqlParameterScanOptions { Prefixes = null! }.Validate());
        Assert.Throws<ArgumentNullException>(() => new SqlParameterScanOptions { NameComparer = null! }.Validate());
    }
}
