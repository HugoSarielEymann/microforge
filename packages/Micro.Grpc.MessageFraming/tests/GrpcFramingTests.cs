using Xunit;

namespace Micro.Grpc.MessageFraming.Tests;

public sealed class GrpcFramingTests
{
    [Fact]
    public void Frame_ecrit_l_indicateur_puis_la_longueur_big_endian()
    {
        byte[] frame = GrpcFraming.Frame(new byte[] { 0x08, 0x96, 0x01 });

        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x03, 0x08, 0x96, 0x01 }, frame);
        Assert.Equal(1, GrpcFraming.Frame([], compressed: true)[0]);
    }

    [Fact]
    public async Task Ecrire_puis_lire_plusieurs_trames()
    {
        using MemoryStream stream = new();
        await GrpcFraming.WriteAsync(stream, new byte[] { 1, 2, 3 });
        await GrpcFraming.WriteAsync(stream, Array.Empty<byte>());
        stream.Position = 0;

        GrpcFrame? first = await GrpcFraming.ReadAsync(stream);
        GrpcFrame? second = await GrpcFraming.ReadAsync(stream);
        GrpcFrame? end = await GrpcFraming.ReadAsync(stream);

        Assert.Equal(new byte[] { 1, 2, 3 }, first!.Value.Payload.ToArray());
        Assert.False(first.Value.IsCompressed);
        Assert.Equal(0, second!.Value.Payload.Length);
        Assert.Null(end);
    }

    [Fact]
    public async Task Une_lecture_en_morceaux_reconstitue_la_trame()
    {
        using TrickleStream stream = new(GrpcFraming.Frame(new byte[] { 9, 8, 7, 6, 5 }));

        GrpcFrame? frame = await GrpcFraming.ReadAsync(stream);

        Assert.Equal(new byte[] { 9, 8, 7, 6, 5 }, frame!.Value.Payload.ToArray());
    }

    [Fact]
    [Trait("hazard", "empty-input")]
    public async Task Un_flux_vide_n_a_aucune_trame()
    {
        using MemoryStream stream = new();

        Assert.Null(await GrpcFraming.ReadAsync(stream));
    }

    [Theory]
    [Trait("hazard", "malformed-input")]
    [InlineData(new byte[] { 0, 0, 0 })]
    [InlineData(new byte[] { 0, 0, 0, 0, 4, 1, 2 })]
    [InlineData(new byte[] { 2, 0, 0, 0, 0 })]
    public async Task Un_flux_tronque_ou_invalide_est_refuse(byte[] data)
    {
        using MemoryStream stream = new(data);

        await Assert.ThrowsAsync<InvalidDataException>(async () => await GrpcFraming.ReadAsync(stream));
    }

    [Fact]
    [Trait("hazard", "malformed-input")]
    public async Task Une_trame_compressee_n_est_admise_que_sur_demande()
    {
        byte[] data = GrpcFraming.Frame(new byte[] { 1 }, compressed: true);

        await Assert.ThrowsAsync<InvalidDataException>(async () => await GrpcFraming.ReadAsync(new MemoryStream(data)));
        GrpcFrame? frame = await GrpcFraming.ReadAsync(new MemoryStream(data), new GrpcFramingOptions { AllowCompressed = true });
        Assert.True(frame!.Value.IsCompressed);
    }

    [Fact]
    [Trait("hazard", "boundary-value")]
    public async Task La_taille_maximale_est_une_borne_incluse()
    {
        GrpcFramingOptions options = new() { MaxMessageSize = 4 };

        GrpcFrame? exact = await GrpcFraming.ReadAsync(new MemoryStream(GrpcFraming.Frame(new byte[4])), options);
        Assert.Equal(4, exact!.Value.Payload.Length);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await GrpcFraming.ReadAsync(new MemoryStream(GrpcFraming.Frame(new byte[5])), options));
    }

    [Fact]
    [Trait("hazard", "numeric-overflow")]
    public async Task Une_longueur_annoncee_demesuree_est_refusee_sans_allouer()
    {
        byte[] data = [0, 0xFF, 0xFF, 0xFF, 0xFF];

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(async () => await GrpcFraming.ReadAsync(new MemoryStream(data)));
        Assert.Contains("4294967295", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("hazard", "cancellation")]
    public async Task L_annulation_est_respectee()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await GrpcFraming.ReadAsync(new MemoryStream(GrpcFraming.Frame([1])), cancellationToken: cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await GrpcFraming.WriteAsync(new MemoryStream(), new byte[] { 1 }, cancellationToken: cancelled.Token));
    }

    [Fact]
    [Trait("hazard", "null-input")]
    public async Task Un_flux_nul_est_refuse()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await GrpcFraming.ReadAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await GrpcFraming.WriteAsync(null!, Array.Empty<byte>()));
    }

    [Fact]
    public async Task Un_parametrage_incoherent_est_refuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrpcFramingOptions { MaxMessageSize = -1 }.Validate());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await GrpcFraming.ReadAsync(new MemoryStream(), new GrpcFramingOptions { MaxMessageSize = -1 }));
        Assert.Equal(5, GrpcFraming.HeaderSize);
    }

    /// <summary>Un flux qui ne rend qu'un octet à la fois, comme un réseau lent.</summary>
    private sealed class TrickleStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
