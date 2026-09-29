using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace Micro.Protobuf.DynamicCodec.Tests;

public sealed class ProtoCodecTests
{
    private static ProtoMessageDescriptor Single(ProtoFieldType type, int number = 1, bool repeated = false, bool presence = false)
        => new ProtoMessageDescriptor("Essai").Add(new ProtoField(number, "valeur", type, repeated, hasPresence: presence));

    private static byte[] EncodeOne(ProtoFieldType type, object value, ProtoCodecOptions? options = null)
    {
        ProtoMessage message = new(Single(type));
        message.Set(1, value);
        return ProtoCodec.Encode(message, options);
    }

    private static object? RoundTrip(ProtoFieldType type, object value)
    {
        ProtoMessageDescriptor descriptor = Single(type);
        ProtoMessage message = new(descriptor);
        message.Set(1, value);
        return ProtoCodec.Decode(descriptor, ProtoCodec.Encode(message, new ProtoCodecOptions { EmitDefaultScalars = true })).Get(1);
    }

    // ==================== Octets de référence (documentation Protobuf) ====================

    [Fact]
    public void Encode_150_comme_la_documentation()
    {
        Assert.Equal(new byte[] { 0x08, 0x96, 0x01 }, EncodeOne(ProtoFieldType.Int32, 150));
    }

    [Fact]
    public void Encode_un_texte_comme_la_documentation()
    {
        ProtoMessage message = new(Single(ProtoFieldType.String, number: 2));
        message.Set(2, "testing");

        Assert.Equal(Convert.FromHexString("120774657374696E67"), ProtoCodec.Encode(message));
    }

    [Fact]
    public void Encode_un_message_imbrique_comme_la_documentation()
    {
        ProtoMessageDescriptor inner = Single(ProtoFieldType.Int32);
        ProtoMessageDescriptor outer = new ProtoMessageDescriptor("Externe").Add(new ProtoField(3, "c", ProtoFieldType.Message, messageType: inner));
        ProtoMessage child = new(inner);
        child.Set(1, 150);
        ProtoMessage message = new(outer);
        message.Set(3, child);

        Assert.Equal(new byte[] { 0x1A, 0x03, 0x08, 0x96, 0x01 }, ProtoCodec.Encode(message));
    }

    [Fact]
    public void Packe_les_entiers_repetes_comme_la_documentation()
    {
        // L'exemple de la documentation déclare « repeated int32 f = 6 [packed = true] ».
        ProtoMessage message = new(Single(ProtoFieldType.Int32, number: 6, repeated: true));
        message.Set(6, new object[] { 3, 270, 86942 });

        Assert.Equal(Convert.FromHexString("3206038E029EA705"), ProtoCodec.Encode(message));
    }

    [Theory]
    [InlineData(0, "0800")]
    [InlineData(-1, "0801")]
    [InlineData(1, "0802")]
    [InlineData(-2, "0803")]
    public void Encode_le_zigzag_des_sint32(int value, string hex)
    {
        Assert.Equal(Convert.FromHexString(hex), EncodeOne(ProtoFieldType.SInt32, value, new ProtoCodecOptions { EmitDefaultScalars = true }));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void Un_int32_negatif_s_ecrit_sur_dix_octets()
    {
        Assert.Equal(Convert.FromHexString("08FFFFFFFFFFFFFFFFFF01"), EncodeOne(ProtoFieldType.Int32, -1));
        Assert.Equal(-1, RoundTrip(ProtoFieldType.Int32, -1));
    }

    // ==================== Allers-retours ====================

    [Theory]
    [Trait("hazard", "boundary-value")]
    [InlineData(ProtoFieldType.Int64, long.MinValue)]
    [InlineData(ProtoFieldType.Int64, long.MaxValue)]
    [InlineData(ProtoFieldType.SInt64, long.MinValue)]
    [InlineData(ProtoFieldType.SFixed64, long.MinValue)]
    [InlineData(ProtoFieldType.UInt64, ulong.MaxValue)]
    [InlineData(ProtoFieldType.Fixed64, ulong.MaxValue)]
    [InlineData(ProtoFieldType.Int32, int.MinValue)]
    [InlineData(ProtoFieldType.SInt32, int.MaxValue)]
    [InlineData(ProtoFieldType.SFixed32, int.MinValue)]
    [InlineData(ProtoFieldType.UInt32, uint.MaxValue)]
    [InlineData(ProtoFieldType.Fixed32, uint.MaxValue)]
    [InlineData(ProtoFieldType.Enum, 7)]
    [InlineData(ProtoFieldType.Bool, true)]
    [InlineData(ProtoFieldType.Double, 3.141592653589793)]
    [InlineData(ProtoFieldType.Float, 2.5f)]
    public void Chaque_scalaire_survit_a_l_aller_retour(ProtoFieldType type, object value)
    {
        Assert.Equal(value, RoundTrip(type, value));
    }

    [Theory]
    [Trait("hazard", "non-finite-number")]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Les_doubles_non_finis_voyagent_tels_quels(double value)
    {
        Assert.Equal(value, (double)RoundTrip(ProtoFieldType.Double, value)!);
    }

    [Theory]
    [Trait("hazard", "unicode-edge")]
    [InlineData("Écrou M8 inox")]
    [InlineData("東京 · 🔩 · é")]
    [InlineData("")]
    public void Les_textes_unicode_survivent(string value)
    {
        Assert.Equal(value, RoundTrip(ProtoFieldType.String, value) ?? string.Empty);
    }

    [Fact]
    public void Les_octets_survivent()
    {
        Assert.Equal(new byte[] { 0, 1, 255 }, RoundTrip(ProtoFieldType.Bytes, new byte[] { 0, 1, 255 }));
    }

    [Fact]
    public void Un_message_recursif_s_encode_et_se_relit()
    {
        ProtoMessageDescriptor node = new("Noeud");
        node.Add(new ProtoField(1, "nom", ProtoFieldType.String)).Add(new ProtoField(2, "enfants", ProtoFieldType.Message, isRepeated: true, messageType: node));
        ProtoMessage leaf = new(node);
        leaf.Set(1, "feuille");
        ProtoMessage root = new(node);
        root.Set(1, "racine");
        root.Set(2, new object[] { leaf, leaf });

        ProtoMessage read = ProtoCodec.Decode(node, ProtoCodec.Encode(root));

        Assert.Equal("racine", read.Get(1));
        Assert.Equal(2, read.GetList(2).Count);
        Assert.Equal("feuille", ((ProtoMessage)read.GetList(2)[1]).Get(1));
    }

    [Fact]
    public void Les_repetes_se_lisent_packes_ou_non()
    {
        ProtoMessageDescriptor descriptor = Single(ProtoFieldType.SInt64, repeated: true);
        ProtoMessage message = new(descriptor);
        message.Set(1, new object[] { -5L, 0L, 5L });

        byte[] packed = ProtoCodec.Encode(message);
        byte[] unpacked = ProtoCodec.Encode(message, new ProtoCodecOptions { PackRepeatedScalars = false });

        Assert.NotEqual(packed, unpacked);
        Assert.Equal(new object[] { -5L, 0L, 5L }, ProtoCodec.Decode(descriptor, packed).GetList(1));
        Assert.Equal(new object[] { -5L, 0L, 5L }, ProtoCodec.Decode(descriptor, unpacked).GetList(1));
    }

    // ==================== Sémantique proto3 ====================

    [Fact]
    public void Les_valeurs_par_defaut_ne_s_ecrivent_pas_sauf_presence()
    {
        Assert.Empty(EncodeOne(ProtoFieldType.Int32, 0));
        Assert.Empty(EncodeOne(ProtoFieldType.String, string.Empty));
        Assert.NotEmpty(EncodeOne(ProtoFieldType.Int32, 0, new ProtoCodecOptions { EmitDefaultScalars = true }));

        ProtoMessage optional = new(Single(ProtoFieldType.Int32, presence: true));
        optional.Set(1, 0);
        Assert.Equal(new byte[] { 0x08, 0x00 }, ProtoCodec.Encode(optional));
    }

    [Fact]
    public void La_derniere_occurrence_d_un_scalaire_l_emporte_et_les_messages_fusionnent()
    {
        ProtoMessageDescriptor inner = new ProtoMessageDescriptor("Interne")
            .Add(new ProtoField(1, "a", ProtoFieldType.Int32))
            .Add(new ProtoField(2, "b", ProtoFieldType.Int32));
        ProtoMessageDescriptor outer = new ProtoMessageDescriptor("Externe")
            .Add(new ProtoField(1, "x", ProtoFieldType.Int32))
            .Add(new ProtoField(2, "m", ProtoFieldType.Message, messageType: inner));

        // x=1, x=2, m{a=5}, m{b=6}
        byte[] data = Convert.FromHexString("08010802" + "12020805" + "12021006");
        ProtoMessage read = ProtoCodec.Decode(outer, data);

        Assert.Equal(2, read.Get(1));
        ProtoMessage merged = (ProtoMessage)read.Get(2)!;
        Assert.Equal(5, merged.Get(1));
        Assert.Equal(6, merged.Get(2));
    }

    [Fact]
    public void Les_champs_inconnus_groupes_compris_sont_ignores()
    {
        // champ 9 varint, champ 10 longueur, groupe 11 { champ 1 varint }, puis le champ connu 1 = 42.
        byte[] data = Convert.FromHexString("4801" + "52026869" + "5B" + "0801" + "5C" + "082A");

        Assert.Equal(42, ProtoCodec.Decode(Single(ProtoFieldType.Int32), data).Get(1));
    }

    // ==================== Face à la bibliothèque officielle ====================

    private static readonly ProtoMessageDescriptor TimestampDescriptor = new ProtoMessageDescriptor("Timestamp")
        .Add(new ProtoField(1, "seconds", ProtoFieldType.Int64))
        .Add(new ProtoField(2, "nanos", ProtoFieldType.Int32));

    [Fact]
    public void Google_relit_un_horodatage_encode_ici()
    {
        ProtoMessage message = new(TimestampDescriptor);
        message.Set(1, -62_135_596_800L);
        message.Set(2, 999_999_999);

        Timestamp parsed = Timestamp.Parser.ParseFrom(ProtoCodec.Encode(message));

        Assert.Equal(-62_135_596_800L, parsed.Seconds);
        Assert.Equal(999_999_999, parsed.Nanos);
    }

    private static ProtoMessageDescriptor TypeDescriptor()
    {
        ProtoMessageDescriptor option = new ProtoMessageDescriptor("Option").Add(new ProtoField(1, "name", ProtoFieldType.String));
        ProtoMessageDescriptor field = new ProtoMessageDescriptor("Field")
            .Add(new ProtoField(1, "kind", ProtoFieldType.Enum))
            .Add(new ProtoField(2, "cardinality", ProtoFieldType.Enum))
            .Add(new ProtoField(3, "number", ProtoFieldType.Int32))
            .Add(new ProtoField(4, "name", ProtoFieldType.String))
            .Add(new ProtoField(8, "packed", ProtoFieldType.Bool))
            .Add(new ProtoField(9, "options", ProtoFieldType.Message, isRepeated: true, messageType: option));
        return new ProtoMessageDescriptor("Type")
            .Add(new ProtoField(1, "name", ProtoFieldType.String))
            .Add(new ProtoField(2, "fields", ProtoFieldType.Message, isRepeated: true, messageType: field))
            .Add(new ProtoField(3, "oneofs", ProtoFieldType.String, isRepeated: true));
    }

    [Fact]
    public void Un_message_ecrit_par_google_se_relit_ici()
    {
        Google.Protobuf.WellKnownTypes.Type original = new()
        {
            Name = "Commande",
            Oneofs = { "choix" },
            Fields =
            {
                new Field { Kind = Field.Types.Kind.TypeInt64, Cardinality = Field.Types.Cardinality.Repeated, Number = -3, Name = "lignes", Packed = true, Options = { new Option { Name = "o1" } } },
                new Field { Kind = Field.Types.Kind.TypeString, Number = 2, Name = "client" },
            },
        };

        ProtoMessage read = ProtoCodec.Decode(TypeDescriptor(), original.ToByteArray());

        Assert.Equal("Commande", read.Get(1));
        Assert.Equal(new object[] { "choix" }, read.GetList(3));
        ProtoMessage first = (ProtoMessage)read.GetList(2)[0];
        Assert.Equal((int)Field.Types.Kind.TypeInt64, first.Get(1));
        Assert.Equal(-3, first.Get(3));
        Assert.Equal(true, first.Get(8));
        Assert.Equal("o1", ((ProtoMessage)first.GetList(9)[0]).Get(1));
    }

    [Fact]
    public void Google_relit_a_l_identique_ce_que_le_codec_reecrit()
    {
        Google.Protobuf.WellKnownTypes.Type original = new()
        {
            Name = "Écrou",
            Fields = { new Field { Kind = Field.Types.Kind.TypeDouble, Number = 1, Name = "prix", Packed = true } },
        };

        ProtoMessage read = ProtoCodec.Decode(TypeDescriptor(), original.ToByteArray());
        Google.Protobuf.WellKnownTypes.Type reparsed = Google.Protobuf.WellKnownTypes.Type.Parser.ParseFrom(ProtoCodec.Encode(read));

        Assert.Equal(original, reparsed);
    }

    [Fact]
    public void Les_entiers_packes_ecrits_par_google_se_relisent()
    {
        SourceCodeInfo.Types.Location location = new() { Path = { 4, 0, 2, 1 }, Span = { 10, 3, 42 }, LeadingComments = "prix figé" };
        ProtoMessageDescriptor descriptor = new ProtoMessageDescriptor("Location")
            .Add(new ProtoField(1, "path", ProtoFieldType.Int32, isRepeated: true))
            .Add(new ProtoField(2, "span", ProtoFieldType.Int32, isRepeated: true))
            .Add(new ProtoField(3, "leading", ProtoFieldType.String));

        ProtoMessage read = ProtoCodec.Decode(descriptor, location.ToByteArray());

        Assert.Equal(new object[] { 4, 0, 2, 1 }, read.GetList(1));
        Assert.Equal(new object[] { 10, 3, 42 }, read.GetList(2));
        Assert.Equal("prix figé", read.Get(3));
    }

    // ==================== Entrées dangereuses ====================

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData("08")]
    [InlineData("0A05616263")]
    [InlineData("0E00")]
    [InlineData("0701")]
    [InlineData("0001")]
    [InlineData("5B0801")]
    public void Des_octets_invalides_levent_une_erreur_de_format(string hex)
    {
        Assert.Throws<FormatException>(() => ProtoCodec.Decode(Single(ProtoFieldType.String), Convert.FromHexString(hex)));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public void Un_entier_de_plus_de_dix_octets_est_refuse()
    {
        Assert.Throws<FormatException>(() => ProtoCodec.Decode(Single(ProtoFieldType.Int64), Convert.FromHexString("08FFFFFFFFFFFFFFFFFFFF01")));
        Assert.Throws<FormatException>(() => ProtoCodec.Decode(Single(ProtoFieldType.Int64), Convert.FromHexString("08FFFFFFFFFFFFFFFFFF7F")));
    }

    [Fact]
    [Trait("hazard", "unicode-edge")]
    public void Un_texte_qui_n_est_pas_de_l_utf8_est_refuse_en_mode_strict()
    {
        byte[] data = Convert.FromHexString("0A02C328");

        Assert.Throws<FormatException>(() => ProtoCodec.Decode(Single(ProtoFieldType.String), data));
        Assert.NotNull(ProtoCodec.Decode(Single(ProtoFieldType.String), data, new ProtoCodecOptions { StrictUtf8 = false }).Get(1));
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public void Un_emboitement_trop_profond_est_refuse_dans_les_deux_sens()
    {
        ProtoMessageDescriptor node = new("Noeud");
        node.Add(new ProtoField(1, "enfant", ProtoFieldType.Message, messageType: node));
        ProtoMessage current = new(node);
        for (int i = 0; i < 10; i++)
        {
            ProtoMessage parent = new(node);
            parent.Set(1, current);
            current = parent;
        }

        ProtoCodecOptions shallow = new() { MaxDepth = 5 };
        Assert.Throws<InvalidOperationException>(() => ProtoCodec.Encode(current, shallow));
        byte[] deep = ProtoCodec.Encode(current);
        Assert.Throws<FormatException>(() => ProtoCodec.Decode(node, deep, shallow));
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public void Zero_octet_est_un_message_vide()
    {
        ProtoMessage message = ProtoCodec.Decode(Single(ProtoFieldType.Int32), ReadOnlySpan<byte>.Empty);

        Assert.Empty(message.FieldNumbers);
        Assert.Empty(ProtoCodec.Encode(message));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public void Les_arguments_nuls_sont_refuses_et_TryDecode_ne_leve_jamais()
    {
        Assert.Throws<ArgumentNullException>(() => ProtoCodec.Encode(null!));
        Assert.Throws<ArgumentNullException>(() => ProtoCodec.Decode(null!, ReadOnlySpan<byte>.Empty));
        Assert.Throws<ArgumentNullException>(() => new ProtoMessage(null!));
        Assert.False(ProtoCodec.TryDecode(null, ReadOnlySpan<byte>.Empty, out _, out string? error));
        Assert.NotNull(error);
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void TryDecode_rend_le_motif_au_lieu_de_lever()
    {
        Assert.False(ProtoCodec.TryDecode(Single(ProtoFieldType.Int32), Convert.FromHexString("08"), out ProtoMessage? message, out string? error));
        Assert.Null(message);
        Assert.Contains("tronqué", error, StringComparison.Ordinal);
        Assert.False(ProtoCodec.TryDecode(Single(ProtoFieldType.Int32), [], out _, out _, new ProtoCodecOptions { MaxDepth = 0 }));
        Assert.True(ProtoCodec.TryDecode(Single(ProtoFieldType.Int32), Convert.FromHexString("0801"), out ProtoMessage? ok, out _));
        Assert.Equal(1, ok.Get(1));
    }

    // ==================== Descriptions et paramétrage ====================

    [Theory]
    [Trait("hazard", "boundary-value")]
    [InlineData(0)]
    [InlineData(19_000)]
    [InlineData(19_999)]
    [InlineData(ProtoField.MaxNumber + 1)]
    public void Un_numero_de_champ_interdit_est_refuse(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProtoField(number, "x", ProtoFieldType.Int32));
    }

    [Fact]
    public void Les_bornes_des_numeros_sont_acceptees()
    {
        Assert.Equal(ProtoField.MaxNumber, new ProtoField(ProtoField.MaxNumber, "x", ProtoFieldType.Int32).Number);
        Assert.Equal(18_999, new ProtoField(18_999, "x", ProtoFieldType.Int32).Number);
    }

    [Fact]
    public void Une_description_incoherente_est_refusee()
    {
        ProtoMessageDescriptor descriptor = Single(ProtoFieldType.Int32);
        Assert.Throws<ArgumentException>(() => descriptor.Add(new ProtoField(1, "doublon", ProtoFieldType.String)));
        Assert.Throws<ArgumentException>(() => new ProtoField(2, "m", ProtoFieldType.Message));
        Assert.Throws<ArgumentException>(() => new ProtoField(2, "i", ProtoFieldType.Int32, messageType: descriptor));
        Assert.Throws<ArgumentException>(() => new ProtoField(2, " ", ProtoFieldType.Int32));
        Assert.Throws<ArgumentException>(() => new ProtoMessageDescriptor(""));
        Assert.Throws<ArgumentNullException>(() => descriptor.Add(null!));
    }

    [Fact]
    public void Une_valeur_du_mauvais_type_est_refusee_a_la_pose()
    {
        ProtoMessage message = new(Single(ProtoFieldType.Int64));
        Assert.Throws<ArgumentException>(() => message.Set(1, 3));
        Assert.Throws<ArgumentException>(() => message.Set(2, 3L));
        ProtoMessage list = new(Single(ProtoFieldType.Int32, repeated: true));
        Assert.Throws<ArgumentException>(() => list.Set(1, 3));
        Assert.Throws<ArgumentException>(() => list.Set(1, new object?[] { 1, null }));
    }

    [Fact]
    public void Poser_nul_efface_le_champ()
    {
        ProtoMessage message = new(Single(ProtoFieldType.Int32));
        message.Set(1, 5);
        message.Set(1, null);

        Assert.False(message.TryGet(1, out _));
        Assert.Empty(message.GetList(1));
        Assert.Equal("Essai", message.Descriptor.Name);
        Assert.Single(message.Descriptor.Fields);
        Assert.NotNull(message.Descriptor.FindField(1));
    }

    [Fact]
    public void Un_parametrage_incoherent_est_refuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProtoCodecOptions { MaxDepth = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => ProtoCodec.Encode(new ProtoMessage(Single(ProtoFieldType.Int32)), new ProtoCodecOptions { MaxDepth = 0 }));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public void TryDecode_ne_leve_sur_aucun_octet_aleatoire()
    {
        ProtoMessageDescriptor node = new("Noeud");
        node.Add(new ProtoField(1, "texte", ProtoFieldType.String))
            .Add(new ProtoField(2, "nombres", ProtoFieldType.SInt64, isRepeated: true))
            .Add(new ProtoField(3, "enfant", ProtoFieldType.Message, messageType: node))
            .Add(new ProtoField(4, "prix", ProtoFieldType.Double))
            .Add(new ProtoField(5, "octets", ProtoFieldType.Bytes));
        Random random = new(20260927);
        for (int round = 0; round < 3000; round++)
        {
            byte[] data = new byte[random.Next(0, 64)];
            random.NextBytes(data);
            Exception? thrown = Record.Exception(() => ProtoCodec.TryDecode(node, data, out _, out _));
            Assert.Null(thrown);
        }
    }

    [Fact]
    public void TryGet_ne_leve_pour_aucun_numero()
    {
        ProtoMessage message = new(Single(ProtoFieldType.Int32));

        Assert.False(message.TryGet(-1, out object? negative));
        Assert.False(message.TryGet(0, out _));
        Assert.False(message.TryGet(int.MaxValue, out _));
        Assert.Null(negative);
        Assert.Null(message.Get(int.MinValue));
    }

    [Fact]
    public void La_description_d_un_champ_est_lisible()
    {
        ProtoMessageDescriptor inner = new("Interne");
        ProtoField field = new(7, "lignes", ProtoFieldType.Message, isRepeated: true, messageType: inner);

        Assert.True(field.IsRepeated);
        Assert.Same(inner, field.MessageType);
        Assert.Equal("lignes", field.Name);
        Assert.Equal(ProtoFieldType.Message, field.Type);
        Assert.False(new ProtoField(1, "x", ProtoFieldType.Int32).IsRepeated);
        Assert.Null(new ProtoField(1, "x", ProtoFieldType.Int32).MessageType);
    }

    [Fact]
    public void La_presence_des_champs_est_exposee()
    {
        ProtoField optional = new(1, "o", ProtoFieldType.Int32, hasPresence: true);
        ProtoField repeated = new(2, "r", ProtoFieldType.Int32, isRepeated: true, hasPresence: true);

        Assert.True(optional.HasPresence);
        Assert.False(repeated.HasPresence);
        Assert.True(repeated.IsPackable);
        Assert.False(new ProtoField(3, "s", ProtoFieldType.String).IsPackable);
    }
}
