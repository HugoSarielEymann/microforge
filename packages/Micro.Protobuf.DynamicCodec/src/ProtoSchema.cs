namespace Micro.Protobuf.DynamicCodec;

/// <summary>Type Protobuf d'un champ.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1720:Identifiers should not contain type names",
    Justification = "Les membres reprennent exactement les noms des types Protobuf (int32, int64, double, string…) : les renommer brouillerait la correspondance avec la spécification.")]
public enum ProtoFieldType
{
    /// <summary><c>double</c> — valeur <see cref="double"/>.</summary>
    Double,

    /// <summary><c>float</c> — valeur <see cref="float"/>.</summary>
    Float,

    /// <summary><c>int64</c> — valeur <see cref="long"/>.</summary>
    Int64,

    /// <summary><c>uint64</c> — valeur <see cref="ulong"/>.</summary>
    UInt64,

    /// <summary><c>int32</c> — valeur <see cref="int"/>.</summary>
    Int32,

    /// <summary><c>uint32</c> — valeur <see cref="uint"/>.</summary>
    UInt32,

    /// <summary><c>sint32</c> (zigzag) — valeur <see cref="int"/>.</summary>
    SInt32,

    /// <summary><c>sint64</c> (zigzag) — valeur <see cref="long"/>.</summary>
    SInt64,

    /// <summary><c>fixed32</c> — valeur <see cref="uint"/>.</summary>
    Fixed32,

    /// <summary><c>fixed64</c> — valeur <see cref="ulong"/>.</summary>
    Fixed64,

    /// <summary><c>sfixed32</c> — valeur <see cref="int"/>.</summary>
    SFixed32,

    /// <summary><c>sfixed64</c> — valeur <see cref="long"/>.</summary>
    SFixed64,

    /// <summary><c>bool</c> — valeur <see cref="bool"/>.</summary>
    Bool,

    /// <summary><c>string</c> — valeur <see cref="string"/> (UTF-8 sur le fil).</summary>
    String,

    /// <summary><c>bytes</c> — valeur tableau d'octets.</summary>
    Bytes,

    /// <summary>Énumération — valeur <see cref="int"/> (son numéro).</summary>
    Enum,

    /// <summary>Message imbriqué — valeur <see cref="ProtoMessage"/>.</summary>
    Message,
}

/// <summary>Un champ d'un message Protobuf.</summary>
public sealed class ProtoField
{
    /// <summary>Dernier numéro de champ autorisé par Protobuf.</summary>
    public const int MaxNumber = 536_870_911;

    /// <summary>Crée un champ.</summary>
    /// <param name="number">Numéro sur le fil (1 à 536 870 911, hors 19 000 à 19 999).</param>
    /// <param name="name">Nom, pour les messages d'erreur.</param>
    /// <param name="type">Type Protobuf.</param>
    /// <param name="isRepeated">Champ répété.</param>
    /// <param name="messageType">Description du message, obligatoire pour <see cref="ProtoFieldType.Message"/>.</param>
    /// <param name="hasPresence">
    /// Présence suivie (<c>optional</c> de proto3) : une valeur par défaut est alors écrite, et son
    /// absence reste distinguable. Les messages ont toujours la présence.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Si le numéro est hors des bornes ou réservé.</exception>
    /// <exception cref="ArgumentException">Si le nom est vide, ou si le message imbriqué manque ou est donné à tort.</exception>
    public ProtoField(int number, string name, ProtoFieldType type, bool isRepeated = false, ProtoMessageDescriptor? messageType = null, bool hasPresence = false)
    {
        if (number is < 1 or > MaxNumber || number is >= 19_000 and <= 19_999)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Numéro de champ Protobuf invalide : 1 à 536 870 911, hors 19 000 à 19 999.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if ((type == ProtoFieldType.Message) != (messageType is not null))
        {
            throw new ArgumentException("Un champ de type Message exige sa description, et seul lui en porte une.", nameof(messageType));
        }

        Number = number;
        Name = name;
        Type = type;
        IsRepeated = isRepeated;
        MessageType = messageType;
        HasPresence = !isRepeated && (hasPresence || type == ProtoFieldType.Message);
    }

    /// <summary>Numéro sur le fil.</summary>
    public int Number { get; }

    /// <summary>Nom du champ.</summary>
    public string Name { get; }

    /// <summary>Type Protobuf.</summary>
    public ProtoFieldType Type { get; }

    /// <summary>Champ répété.</summary>
    public bool IsRepeated { get; }

    /// <summary>Description du message imbriqué.</summary>
    public ProtoMessageDescriptor? MessageType { get; }

    /// <summary>Présence suivie.</summary>
    public bool HasPresence { get; }

    /// <summary>Le type s'encode en valeurs de taille fixe ou en varints, donc se « packe » quand il est répété.</summary>
    public bool IsPackable => Type is not (ProtoFieldType.String or ProtoFieldType.Bytes or ProtoFieldType.Message);
}

/// <summary>
/// La description d'un message : son nom et ses champs. Elle peut se référencer elle-même
/// (message récursif) : on crée la description, puis on lui ajoute ses champs.
/// </summary>
public sealed class ProtoMessageDescriptor
{
    private readonly List<ProtoField> _fields = [];
    private readonly Dictionary<int, ProtoField> _byNumber = [];

    /// <summary>Crée une description vide.</summary>
    /// <param name="name">Nom du message, pour les messages d'erreur.</param>
    /// <exception cref="ArgumentException">Si le nom est vide.</exception>
    public ProtoMessageDescriptor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Nom du message.</summary>
    public string Name { get; }

    /// <summary>Champs, dans l'ordre d'ajout (celui de l'écriture).</summary>
    public IReadOnlyList<ProtoField> Fields => _fields;

    /// <summary>Ajoute un champ.</summary>
    /// <returns>La description elle-même, pour enchaîner les ajouts.</returns>
    /// <exception cref="ArgumentNullException">Si le champ est nul.</exception>
    /// <exception cref="ArgumentException">Si le numéro est déjà pris.</exception>
    public ProtoMessageDescriptor Add(ProtoField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!_byNumber.TryAdd(field.Number, field))
        {
            throw new ArgumentException($"Le numéro {field.Number} est déjà pris dans « {Name} ».", nameof(field));
        }

        _fields.Add(field);
        return this;
    }

    /// <summary>Retrouve un champ par son numéro.</summary>
    public ProtoField? FindField(int number) => _byNumber.GetValueOrDefault(number);
}

/// <summary>
/// Un message en cours de construction ou décodé : les valeurs de ses champs, par numéro. Un
/// champ répété porte une liste ; un champ absent ne porte rien.
/// </summary>
public sealed class ProtoMessage
{
    private readonly Dictionary<int, object> _values = [];

    /// <summary>Crée un message vide.</summary>
    /// <exception cref="ArgumentNullException">Si la description est nulle.</exception>
    public ProtoMessage(ProtoMessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    /// <summary>Description du message.</summary>
    public ProtoMessageDescriptor Descriptor { get; }

    /// <summary>Numéros des champs renseignés.</summary>
    public IEnumerable<int> FieldNumbers => _values.Keys;

    /// <summary>Donne une valeur à un champ (une liste pour un champ répété) ; <see langword="null"/> l'efface.</summary>
    /// <exception cref="ArgumentException">Si le champ n'existe pas, ou si la valeur n'est pas du type attendu.</exception>
    public void Set(int number, object? value)
    {
        ProtoField field = Descriptor.FindField(number) ?? throw new ArgumentException($"« {Descriptor.Name} » n'a pas de champ n° {number}.", nameof(number));
        if (value is null)
        {
            _values.Remove(number);
            return;
        }

        if (field.IsRepeated)
        {
            if (value is not System.Collections.IEnumerable items || value is string)
            {
                throw new ArgumentException($"« {field.Name} » est répété : une liste est attendue.", nameof(value));
            }

            List<object> list = [];
            foreach (object? item in items)
            {
                list.Add(Check(field, item ?? throw new ArgumentException($"« {field.Name} » ne peut pas contenir d'élément nul.", nameof(value))));
            }

            _values[number] = list;
            return;
        }

        _values[number] = Check(field, value);
    }

    /// <summary>Lit un champ ; faux s'il n'est pas renseigné.</summary>
    public bool TryGet(int number, out object? value)
    {
        bool found = _values.TryGetValue(number, out object? stored);
        value = stored;
        return found;
    }

    /// <summary>Valeur d'un champ, ou <see langword="null"/> s'il n'est pas renseigné.</summary>
    public object? Get(int number) => _values.GetValueOrDefault(number);

    /// <summary>Éléments d'un champ répété ; vide s'il n'est pas renseigné.</summary>
    public IReadOnlyList<object> GetList(int number) => _values.GetValueOrDefault(number) as List<object> ?? [];

    internal void Append(ProtoField field, object value)
    {
        if (!_values.TryGetValue(field.Number, out object? existing) || existing is not List<object> list)
        {
            list = [];
            _values[field.Number] = list;
        }

        list.Add(value);
    }

    internal void Store(ProtoField field, object value) => _values[field.Number] = value;

    private static object Check(ProtoField field, object value)
    {
        bool ok = field.Type switch
        {
            ProtoFieldType.Double => value is double,
            ProtoFieldType.Float => value is float,
            ProtoFieldType.Int64 or ProtoFieldType.SInt64 or ProtoFieldType.SFixed64 => value is long,
            ProtoFieldType.UInt64 or ProtoFieldType.Fixed64 => value is ulong,
            ProtoFieldType.Int32 or ProtoFieldType.SInt32 or ProtoFieldType.SFixed32 or ProtoFieldType.Enum => value is int,
            ProtoFieldType.UInt32 or ProtoFieldType.Fixed32 => value is uint,
            ProtoFieldType.Bool => value is bool,
            ProtoFieldType.String => value is string,
            ProtoFieldType.Bytes => value is byte[],
            ProtoFieldType.Message => value is ProtoMessage message && ReferenceEquals(message.Descriptor, field.MessageType),
            _ => false,
        };

        return ok ? value : throw new ArgumentException($"« {field.Name} » attend une valeur de type {field.Type}, pas {value.GetType().Name}.", nameof(value));
    }
}
