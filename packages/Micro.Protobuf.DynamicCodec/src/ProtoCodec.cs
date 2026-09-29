using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Micro.Protobuf.DynamicCodec;

/// <summary>Paramétrage de l'encodage et du décodage.</summary>
public sealed class ProtoCodecOptions
{
    /// <summary>
    /// Profondeur maximale de messages imbriqués. Défaut : 64. Un message malveillant ne peut pas
    /// faire déborder la pile en s'emboîtant indéfiniment.
    /// </summary>
    public int MaxDepth { get; init; } = 64;

    /// <summary>
    /// Écrire aussi les valeurs par défaut (0, faux, texte vide) des champs sans présence. Défaut :
    /// <see langword="false"/>, comme proto3 : un lecteur les restitue de toute façon.
    /// </summary>
    public bool EmitDefaultScalars { get; init; }

    /// <summary>Écrire les champs numériques répétés en bloc (« packed »), comme proto3. Défaut : <see langword="true"/>.</summary>
    public bool PackRepeatedScalars { get; init; } = true;

    /// <summary>Refuser à la lecture un texte qui n'est pas de l'UTF-8 valide. Défaut : <see langword="true"/>.</summary>
    public bool StrictUtf8 { get; init; } = true;

    /// <summary>Vérifie la cohérence du paramétrage.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Si <see cref="MaxDepth"/> est inférieur à 1.</exception>
    public void Validate() => ArgumentOutOfRangeException.ThrowIfLessThan(MaxDepth, 1, nameof(MaxDepth));
}

/// <summary>
/// Encode et décode des messages Protobuf (proto3) à partir de descriptions construites à
/// l'exécution : aucun code généré, aucun fichier <c>.proto</c> compilé.
/// </summary>
public static class ProtoCodec
{
    private const int WireVarint = 0;
    private const int WireFixed64 = 1;
    private const int WireLength = 2;
    private const int WireStartGroup = 3;
    private const int WireEndGroup = 4;
    private const int WireFixed32 = 5;

    private static readonly UTF8Encoding StrictEncoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding LooseEncoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    // ==================== Encodage ====================

    /// <summary>Encode un message.</summary>
    /// <param name="message">Message à encoder.</param>
    /// <param name="options">Paramétrage ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Les octets du message, sans préfixe de longueur.</returns>
    /// <exception cref="ArgumentNullException">Si le message est nul.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si le paramétrage est incohérent.</exception>
    /// <exception cref="InvalidOperationException">Si le message dépasse la profondeur maximale.</exception>
    public static byte[] Encode(ProtoMessage message, ProtoCodecOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ProtoCodecOptions effective = options ?? new ProtoCodecOptions();
        effective.Validate();
        ArrayBufferWriter<byte> writer = new();
        Write(writer, message, effective, 0);
        return writer.WrittenSpan.ToArray();
    }

    private static void Write(ArrayBufferWriter<byte> writer, ProtoMessage message, ProtoCodecOptions options, int depth)
    {
        if (depth > options.MaxDepth)
        {
            throw new InvalidOperationException($"« {message.Descriptor.Name} » dépasse la profondeur maximale de {options.MaxDepth} messages imbriqués.");
        }

        foreach (ProtoField field in message.Descriptor.Fields)
        {
            if (!message.TryGet(field.Number, out object? value) || value is null)
            {
                continue;
            }

            if (field.IsRepeated)
            {
                List<object> items = (List<object>)value;
                if (items.Count == 0)
                {
                    continue;
                }

                if (field.IsPackable && options.PackRepeatedScalars)
                {
                    ArrayBufferWriter<byte> packed = new();
                    foreach (object item in items)
                    {
                        WriteScalar(packed, field.Type, item);
                    }

                    WriteTag(writer, field.Number, WireLength);
                    WriteVarint(writer, (ulong)packed.WrittenCount);
                    writer.Write(packed.WrittenSpan);
                }
                else
                {
                    foreach (object item in items)
                    {
                        WriteField(writer, field, item, options, depth);
                    }
                }

                continue;
            }

            if (!field.HasPresence && !options.EmitDefaultScalars && IsDefault(field.Type, value))
            {
                continue;
            }

            WriteField(writer, field, value, options, depth);
        }
    }

    private static void WriteField(ArrayBufferWriter<byte> writer, ProtoField field, object value, ProtoCodecOptions options, int depth)
    {
        switch (field.Type)
        {
            case ProtoFieldType.String:
                WriteTag(writer, field.Number, WireLength);
                WriteBytes(writer, LooseEncoding.GetBytes((string)value));
                break;
            case ProtoFieldType.Bytes:
                WriteTag(writer, field.Number, WireLength);
                WriteBytes(writer, (byte[])value);
                break;
            case ProtoFieldType.Message:
                ArrayBufferWriter<byte> nested = new();
                Write(nested, (ProtoMessage)value, options, depth + 1);
                WriteTag(writer, field.Number, WireLength);
                WriteVarint(writer, (ulong)nested.WrittenCount);
                writer.Write(nested.WrittenSpan);
                break;
            default:
                WriteTag(writer, field.Number, WireTypeOf(field.Type));
                WriteScalar(writer, field.Type, value);
                break;
        }
    }

    private static void WriteScalar(ArrayBufferWriter<byte> writer, ProtoFieldType type, object value)
    {
        switch (type)
        {
            case ProtoFieldType.Int64:
                WriteVarint(writer, unchecked((ulong)(long)value));
                break;
            case ProtoFieldType.UInt64:
                WriteVarint(writer, (ulong)value);
                break;
            case ProtoFieldType.Int32:
            case ProtoFieldType.Enum:
                // Un int32 négatif s'écrit étendu à 64 bits, sur dix octets : c'est la règle Protobuf.
                WriteVarint(writer, unchecked((ulong)(long)(int)value));
                break;
            case ProtoFieldType.UInt32:
                WriteVarint(writer, (uint)value);
                break;
            case ProtoFieldType.SInt32:
                int s32 = (int)value;
                WriteVarint(writer, unchecked((uint)((s32 << 1) ^ (s32 >> 31))));
                break;
            case ProtoFieldType.SInt64:
                long s64 = (long)value;
                WriteVarint(writer, unchecked((ulong)((s64 << 1) ^ (s64 >> 63))));
                break;
            case ProtoFieldType.Bool:
                WriteVarint(writer, (bool)value ? 1UL : 0UL);
                break;
            case ProtoFieldType.Fixed32:
                BinaryPrimitives.WriteUInt32LittleEndian(writer.GetSpan(4), (uint)value);
                writer.Advance(4);
                break;
            case ProtoFieldType.SFixed32:
                BinaryPrimitives.WriteInt32LittleEndian(writer.GetSpan(4), (int)value);
                writer.Advance(4);
                break;
            case ProtoFieldType.Float:
                BinaryPrimitives.WriteSingleLittleEndian(writer.GetSpan(4), (float)value);
                writer.Advance(4);
                break;
            case ProtoFieldType.Fixed64:
                BinaryPrimitives.WriteUInt64LittleEndian(writer.GetSpan(8), (ulong)value);
                writer.Advance(8);
                break;
            case ProtoFieldType.SFixed64:
                BinaryPrimitives.WriteInt64LittleEndian(writer.GetSpan(8), (long)value);
                writer.Advance(8);
                break;
            case ProtoFieldType.Double:
                BinaryPrimitives.WriteDoubleLittleEndian(writer.GetSpan(8), (double)value);
                writer.Advance(8);
                break;
            default:
                throw new InvalidOperationException($"Le type {type} ne s'écrit pas en scalaire.");
        }
    }

    private static bool IsDefault(ProtoFieldType type, object value) => type switch
    {
        ProtoFieldType.String => ((string)value).Length == 0,
        ProtoFieldType.Bytes => ((byte[])value).Length == 0,
        ProtoFieldType.Bool => !(bool)value,
        ProtoFieldType.Double => BitConverter.DoubleToInt64Bits((double)value) == 0,
        ProtoFieldType.Float => BitConverter.SingleToInt32Bits((float)value) == 0,
        ProtoFieldType.Int64 or ProtoFieldType.SInt64 or ProtoFieldType.SFixed64 => (long)value == 0,
        ProtoFieldType.UInt64 or ProtoFieldType.Fixed64 => (ulong)value == 0,
        ProtoFieldType.Int32 or ProtoFieldType.SInt32 or ProtoFieldType.SFixed32 or ProtoFieldType.Enum => (int)value == 0,
        ProtoFieldType.UInt32 or ProtoFieldType.Fixed32 => (uint)value == 0,
        _ => false,
    };

    private static int WireTypeOf(ProtoFieldType type) => type switch
    {
        ProtoFieldType.Double or ProtoFieldType.Fixed64 or ProtoFieldType.SFixed64 => WireFixed64,
        ProtoFieldType.Float or ProtoFieldType.Fixed32 or ProtoFieldType.SFixed32 => WireFixed32,
        ProtoFieldType.String or ProtoFieldType.Bytes or ProtoFieldType.Message => WireLength,
        _ => WireVarint,
    };

    private static void WriteTag(ArrayBufferWriter<byte> writer, int number, int wireType)
        => WriteVarint(writer, ((ulong)(uint)number << 3) | (uint)wireType);

    private static void WriteBytes(ArrayBufferWriter<byte> writer, byte[] bytes)
    {
        WriteVarint(writer, (ulong)bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteVarint(ArrayBufferWriter<byte> writer, ulong value)
    {
        Span<byte> span = writer.GetSpan(10);
        int count = 0;
        while (value >= 0x80)
        {
            span[count++] = (byte)(value | 0x80);
            value >>= 7;
        }

        span[count++] = (byte)value;
        writer.Advance(count);
    }

    // ==================== Décodage ====================

    /// <summary>Décode un message.</summary>
    /// <param name="descriptor">Description du message attendu.</param>
    /// <param name="data">Octets du message, sans préfixe de longueur.</param>
    /// <param name="options">Paramétrage ; <see langword="null"/> prend les défauts.</param>
    /// <returns>Le message ; les champs inconnus de la description sont ignorés.</returns>
    /// <exception cref="ArgumentNullException">Si la description est nulle.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si le paramétrage est incohérent.</exception>
    /// <exception cref="FormatException">Si les octets ne forment pas un message Protobuf valide pour cette description.</exception>
    public static ProtoMessage Decode(ProtoMessageDescriptor descriptor, ReadOnlySpan<byte> data, ProtoCodecOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ProtoCodecOptions effective = options ?? new ProtoCodecOptions();
        effective.Validate();
        ProtoMessage message = new(descriptor);
        ReadInto(data, message, effective, 0);
        return message;
    }

    /// <summary>Décode un message sans jamais lever : faux et le motif si les octets sont invalides.</summary>
    /// <param name="descriptor">Description du message attendu.</param>
    /// <param name="data">Octets du message.</param>
    /// <param name="message">Le message décodé.</param>
    /// <param name="error">Le motif du refus.</param>
    /// <param name="options">Paramétrage ; <see langword="null"/> prend les défauts.</param>
    public static bool TryDecode(ProtoMessageDescriptor? descriptor, ReadOnlySpan<byte> data, [NotNullWhen(true)] out ProtoMessage? message, [NotNullWhen(false)] out string? error, ProtoCodecOptions? options = null)
    {
        message = null;
        if (descriptor is null)
        {
            error = "Aucune description de message.";
            return false;
        }

        ProtoCodecOptions effective = options ?? new ProtoCodecOptions();
        if (effective.MaxDepth < 1)
        {
            error = "La profondeur maximale doit valoir au moins 1.";
            return false;
        }

        try
        {
            ProtoMessage decoded = new(descriptor);
            ReadInto(data, decoded, effective, 0);
            message = decoded;
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void ReadInto(ReadOnlySpan<byte> data, ProtoMessage message, ProtoCodecOptions options, int depth)
    {
        if (depth > options.MaxDepth)
        {
            throw new FormatException($"Le message dépasse la profondeur maximale de {options.MaxDepth} messages imbriqués.");
        }

        int position = 0;
        while (position < data.Length)
        {
            (int number, int wireType) = ReadTag(data, ref position);
            ProtoField? field = message.Descriptor.FindField(number);
            if (field is null)
            {
                Skip(data, ref position, wireType, number, depth, options);
                continue;
            }

            if (field.IsRepeated && field.IsPackable && wireType == WireLength)
            {
                ReadOnlySpan<byte> packed = ReadLengthDelimited(data, ref position);
                int inner = 0;
                while (inner < packed.Length)
                {
                    message.Append(field, ReadScalar(packed, ref inner, field.Type));
                }

                continue;
            }

            int expected = WireTypeOf(field.Type);
            if (wireType != expected)
            {
                throw new FormatException($"« {message.Descriptor.Name}.{field.Name} » : type de fil {wireType} reçu, {expected} attendu.");
            }

            object value;
            switch (field.Type)
            {
                case ProtoFieldType.String:
                    ReadOnlySpan<byte> text = ReadLengthDelimited(data, ref position);
                    try
                    {
                        value = (options.StrictUtf8 ? StrictEncoding : LooseEncoding).GetString(text);
                    }
                    catch (DecoderFallbackException)
                    {
                        throw new FormatException($"« {message.Descriptor.Name}.{field.Name} » n'est pas de l'UTF-8 valide.");
                    }

                    break;
                case ProtoFieldType.Bytes:
                    value = ReadLengthDelimited(data, ref position).ToArray();
                    break;
                case ProtoFieldType.Message:
                    ReadOnlySpan<byte> nested = ReadLengthDelimited(data, ref position);
                    if (!field.IsRepeated && message.Get(field.Number) is ProtoMessage existing)
                    {
                        // Deux occurrences d'un même message se fusionnent (règle Protobuf).
                        ReadInto(nested, existing, options, depth + 1);
                        continue;
                    }

                    ProtoMessage child = new(field.MessageType!);
                    ReadInto(nested, child, options, depth + 1);
                    value = child;
                    break;
                default:
                    value = ReadScalar(data, ref position, field.Type);
                    break;
            }

            if (field.IsRepeated)
            {
                message.Append(field, value);
            }
            else
            {
                message.Store(field, value);
            }
        }
    }

    private static (int Number, int WireType) ReadTag(ReadOnlySpan<byte> data, ref int position)
    {
        ulong tag = ReadVarint(data, ref position);
        ulong number = tag >> 3;
        if (number is 0 or > ProtoField.MaxNumber)
        {
            throw new FormatException($"Numéro de champ invalide ({number}).");
        }

        return ((int)number, (int)(tag & 7));
    }

    private static object ReadScalar(ReadOnlySpan<byte> data, ref int position, ProtoFieldType type)
    {
        switch (type)
        {
            case ProtoFieldType.Int64:
                return unchecked((long)ReadVarint(data, ref position));
            case ProtoFieldType.UInt64:
                return ReadVarint(data, ref position);
            case ProtoFieldType.Int32:
            case ProtoFieldType.Enum:
                return unchecked((int)ReadVarint(data, ref position));
            case ProtoFieldType.UInt32:
                return unchecked((uint)ReadVarint(data, ref position));
            case ProtoFieldType.SInt32:
                uint z32 = unchecked((uint)ReadVarint(data, ref position));
                return unchecked((int)(z32 >> 1) ^ -(int)(z32 & 1));
            case ProtoFieldType.SInt64:
                ulong z64 = ReadVarint(data, ref position);
                return unchecked((long)(z64 >> 1) ^ -(long)(z64 & 1));
            case ProtoFieldType.Bool:
                return ReadVarint(data, ref position) != 0;
            case ProtoFieldType.Fixed32:
                return BinaryPrimitives.ReadUInt32LittleEndian(Take(data, ref position, 4));
            case ProtoFieldType.SFixed32:
                return BinaryPrimitives.ReadInt32LittleEndian(Take(data, ref position, 4));
            case ProtoFieldType.Float:
                return BinaryPrimitives.ReadSingleLittleEndian(Take(data, ref position, 4));
            case ProtoFieldType.Fixed64:
                return BinaryPrimitives.ReadUInt64LittleEndian(Take(data, ref position, 8));
            case ProtoFieldType.SFixed64:
                return BinaryPrimitives.ReadInt64LittleEndian(Take(data, ref position, 8));
            case ProtoFieldType.Double:
                return BinaryPrimitives.ReadDoubleLittleEndian(Take(data, ref position, 8));
            default:
                throw new FormatException($"Le type {type} ne se lit pas en scalaire.");
        }
    }

    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> data, ref int position, int count)
    {
        if (data.Length - position < count)
        {
            throw new FormatException("Message tronqué : une valeur de taille fixe dépasse la fin.");
        }

        ReadOnlySpan<byte> slice = data.Slice(position, count);
        position += count;
        return slice;
    }

    private static ReadOnlySpan<byte> ReadLengthDelimited(ReadOnlySpan<byte> data, ref int position)
    {
        ulong length = ReadVarint(data, ref position);
        if (length > (ulong)(data.Length - position))
        {
            throw new FormatException("Message tronqué : une longueur annoncée dépasse la fin.");
        }

        ReadOnlySpan<byte> slice = data.Slice(position, (int)length);
        position += (int)length;
        return slice;
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int position)
    {
        ulong result = 0;
        for (int shift = 0; shift < 70; shift += 7)
        {
            if (position >= data.Length)
            {
                throw new FormatException("Message tronqué au milieu d'un entier.");
            }

            byte current = data[position++];
            if (shift == 63 && current > 1)
            {
                throw new FormatException("Entier trop long : il dépasse 64 bits.");
            }

            result |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return result;
            }
        }

        throw new FormatException("Entier trop long : plus de dix octets.");
    }

    private static void Skip(ReadOnlySpan<byte> data, ref int position, int wireType, int number, int depth, ProtoCodecOptions options)
    {
        switch (wireType)
        {
            case WireVarint:
                ReadVarint(data, ref position);
                break;
            case WireFixed64:
                Take(data, ref position, 8);
                break;
            case WireLength:
                ReadLengthDelimited(data, ref position);
                break;
            case WireFixed32:
                Take(data, ref position, 4);
                break;
            case WireStartGroup:
                if (depth + 1 > options.MaxDepth)
                {
                    throw new FormatException("Groupes imbriqués trop profondément.");
                }

                while (true)
                {
                    if (position >= data.Length)
                    {
                        throw new FormatException("Groupe jamais refermé.");
                    }

                    (int inner, int innerWire) = ReadTag(data, ref position);
                    if (innerWire == WireEndGroup)
                    {
                        if (inner != number)
                        {
                            throw new FormatException("Fin de groupe inattendue.");
                        }

                        break;
                    }

                    Skip(data, ref position, innerWire, inner, depth + 1, options);
                }

                break;
            default:
                throw new FormatException($"Type de fil inconnu ({wireType}).");
        }
    }
}
