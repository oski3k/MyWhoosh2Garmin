using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MyWhoosh2Garmin;

internal sealed record FitReport(string? ActivityName, DateTimeOffset? StartDate);

internal static class FitPatcher
{
    private const ushort GarminProduct = 4315;
    private const ushort GarminManufacturer = 1;
    private const ushort FileId = 0;
    private const ushort Session = 18;
    private const ushort Record = 20;
    private const ushort DeviceInfo = 23;
    private const ushort Activity = 34;
    private const ushort FieldDescription = 206;
    private const byte Timestamp = 253;
    private const string Damaged = "Plik FIT jest niekompletny lub uszkodzony.";
    private const string VerificationFailed = "Weryfikacja przekonwertowanego pliku FIT nie powiodła się.";
    private static readonly DateTimeOffset FitEpoch = new(1989, 12, 31, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Patch(byte[] data) => Patch(data, out _);

    public static byte[] Patch(byte[] data, out FitReport report, TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        FitFile input = Read(data);
        RecordEntry[] messages = input.Entries.Where(entry => !entry.IsDefinition).ToArray();
        if (!messages.Any(entry => entry.Definition.Global == Record) ||
            messages.Any(entry => entry.Definition.Global == FileId && (entry.ReadUInt(0) ?? 4) != 4))
        {
            throw new InvalidDataException("Ten plik FIT nie zawiera aktywności.");
        }

        bool hasCreator = messages.Any(IsCreator);
        bool hasCompressedTime = messages.Any(entry => entry.IsCompressed);
        bool addedCreator = false;
        DateTimeOffset? startDate = null;
        timeZone ??= TimeZoneInfo.Local;

        using var output = new MemoryStream();
        output.Write(data.AsSpan(0, input.HeaderSize));
        var active = new Definition?[16];

        foreach (RecordEntry entry in input.Entries)
        {
            Definition original = entry.Definition;
            if (entry.IsDefinition)
            {
                Definition widened = original.Global switch
                {
                    FileId => Widen(original, 1, 2),
                    DeviceInfo => Widen(original, 2, 4),
                    _ => original
                };
                active[entry.LocalType] = widened;
                output.WriteByte(entry.Header);
                if (ReferenceEquals(original, widened))
                    output.Write(entry.Body.Span);
                else
                    WriteDefinition(output, widened);
                continue;
            }

            Definition definition = active[entry.LocalType]!;
            output.WriteByte(entry.Header);
            if (original.Global is FileId or DeviceInfo or Activity)
            {
                byte[] body = WidenBody(entry.Body.Span, original, definition);
                switch (original.Global)
                {
                    case FileId:
                        ulong? created = entry.ReadUInt(4);
                        if (created is < uint.MaxValue)
                            startDate ??= FitEpoch.AddSeconds((uint)created.Value);
                        SetDevice(body, definition, 1, 2, 8);
                        break;
                    case DeviceInfo when IsCreator(entry):
                        SetDevice(body, definition, 2, 4, 27);
                        break;
                    case Activity when entry.UtcTimestamp is uint utc && definition.Find(5) is { Size: 4 } field:
                        long localTime = utc + (long)timeZone.GetUtcOffset(FitEpoch.AddSeconds(utc)).TotalSeconds;
                        if (localTime is >= 0 and < uint.MaxValue && entry.ReadUInt(5) != (ulong)localTime)
                            WriteUInt(body, field, definition.BigEndian, (ulong)localTime);
                        break;
                }
                output.Write(body);
            }
            else
            {
                output.Write(entry.Body.Span);
            }

            if (original.Global == FileId && !hasCreator && !addedCreator)
            {
                int slot = Array.FindIndex(active, definition => definition is null);
                if (slot < 0)
                    throw new InvalidDataException(VerificationFailed);

                // An injected full timestamp could change the reference for a later compressed record.
                WriteCreator(output, (byte)slot, hasCompressedTime ? null : entry.ReadUInt(4));
                addedCreator = true;
            }
        }

        Span<byte> contents = output.GetBuffer().AsSpan(0, checked((int)output.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(contents[4..], (uint)(contents.Length - input.HeaderSize));
        if (input.HeaderSize == 14)
            BinaryPrimitives.WriteUInt16LittleEndian(contents[12..], ComputeCrc(contents[..12]));
        ushort crc = ComputeCrc(contents);
        output.WriteByte((byte)crc);
        output.WriteByte((byte)(crc >> 8));
        output.Write(data.AsSpan(input.End + 2));
        byte[] result = output.ToArray();
        Verify(input, result, addedCreator ? 2 : 0);
        report = new FitReport(FindActivityName(messages), startDate);
        return result;
    }

    private static FitFile Read(byte[] bytes)
    {
        if (bytes.Length < 12 || bytes[0] is not (12 or 14) || !bytes.AsSpan(8, 4).SequenceEqual(".FIT"u8))
            throw new InvalidDataException("Wybrany plik nie jest w formacie FIT.");

        int headerSize = bytes[0];
        uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        if ((long)headerSize + dataSize + 2 > bytes.Length)
            throw new InvalidDataException(Damaged);

        int end = headerSize + (int)dataSize;
        var definitions = new Definition?[16];
        var entries = new List<RecordEntry>();
        uint? timestamp = null;
        int position = headerSize;

        while (position < end)
        {
            byte header = bytes[position++];
            int start = position;
            bool compressed = (header & 0x80) != 0;
            int localType = compressed ? (header >> 5) & 3 : header & 15;
            if (!compressed && (header & 0x40) != 0)
            {
                ReadOnlySpan<byte> attributes = Take(4).Span;
                if (attributes[1] > 1)
                    throw new InvalidDataException(Damaged);
                bool bigEndian = attributes[1] == 1;
                ushort global = bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(attributes[2..])
                    : BinaryPrimitives.ReadUInt16LittleEndian(attributes[2..]);
                Field[] fields = TakeFields();
                Field[] developerFields = (header & 0x20) != 0 ? TakeFields() : [];
                var definition = new Definition(global, attributes[0], bigEndian, (header & 0x20) != 0, fields, developerFields);
                definitions[localType] = definition;
                entries.Add(new RecordEntry(header, definition, bytes.AsMemory(start, position - start)));
                continue;
            }

            Definition active = definitions[localType] ?? throw new InvalidDataException(Damaged);
            var entry = new RecordEntry(header, active, Take(active.BodySize));
            if (compressed && timestamp is uint previous)
            {
                // The referenced definition gives the wire body size. The header adds a timestamp.
                uint delta = (uint)((header - (previous & 31)) & 31);
                uint expanded = unchecked(previous + delta);
                timestamp = expanded;
                entry = entry with { UtcTimestamp = expanded };
            }
            else if (!compressed && entry.ReadUInt(Timestamp) is ulong utc && utc < uint.MaxValue)
            {
                timestamp = (uint)utc;
                entry = entry with { UtcTimestamp = timestamp };
            }
            entries.Add(entry);
        }

        return new FitFile(headerSize, end, entries);

        ReadOnlyMemory<byte> Take(int count)
        {
            if (count > end - position)
                throw new InvalidDataException(Damaged);
            ReadOnlyMemory<byte> segment = bytes.AsMemory(position, count);
            position += count;
            return segment;
        }

        Field[] TakeFields()
        {
            int count = Take(1).Span[0];
            ReadOnlySpan<byte> raw = Take(3 * count).Span;
            var fields = new Field[count];
            int offset = 0;
            for (int index = 0; index < count; index++)
            {
                fields[index] = new Field(raw[index * 3], raw[index * 3 + 1], raw[index * 3 + 2], offset);
                offset += fields[index].Size;
            }
            return fields;
        }
    }

    private static bool IsCreator(RecordEntry entry) => entry.Definition.Global == DeviceInfo && entry.ReadUInt(0) == 0;

    private static string? FindActivityName(RecordEntry[] messages)
    {
        foreach (RecordEntry description in messages.Where(entry => entry.Definition.Global == FieldDescription))
        {
            ulong? developerIndex = description.ReadUInt(0);
            ulong? field = description.ReadUInt(1);
            if (description.ReadString(3) != "Title" || developerIndex is not (<= byte.MaxValue) ||
                field is not (<= byte.MaxValue))
                continue;

            foreach (RecordEntry session in messages.Where(entry => entry.Definition.Global == Session))
            {
                string? text = session.ReadDeveloperString((byte)field.Value, (byte)developerIndex.Value);
                if (text is not null)
                    return text;
            }
        }

        (ushort Global, byte Field)[] sources = [(26, 8), (12, 3), (Session, 110)];
        foreach ((ushort global, byte field) in sources)
        {
            string? name = messages.Where(entry => entry.Definition.Global == global)
                .Select(entry => entry.ReadString(field)).FirstOrDefault(value => value is not null);
            if (name is not null)
                return name;
        }
        return null;
    }

    private static Definition Widen(Definition definition, byte manufacturer, byte product)
    {
        var fields = new List<Field>(definition.Fields);
        int offset = definition.NativeSize;
        foreach (byte number in new[] { manufacturer, product })
        {
            if (definition.Find(number) is null)
            {
                fields.Add(new Field(number, 2, 0x84, offset));
                offset += 2;
            }
        }
        if (fields.Count > byte.MaxValue)
            throw new InvalidDataException(Damaged);
        return fields.Count == definition.Fields.Length ? definition
            : new Definition(definition.Global, definition.Reserved, definition.BigEndian,
                definition.HasDeveloperFields, fields.ToArray(), definition.DeveloperFields);
    }

    private static byte[] WidenBody(ReadOnlySpan<byte> body, Definition original, Definition widened)
    {
        var result = new byte[widened.BodySize];
        body[..original.NativeSize].CopyTo(result);
        result.AsSpan(original.NativeSize, widened.NativeSize - original.NativeSize).Fill(0xFF);
        body[original.NativeSize..].CopyTo(result.AsSpan(widened.NativeSize));
        return result;
    }

    private static void SetDevice(byte[] body, Definition definition, byte manufacturer, byte product, byte name)
    {
        WriteUInt(body, definition.Find(manufacturer)!.Value, definition.BigEndian, GarminManufacturer);
        WriteUInt(body, definition.Find(product)!.Value, definition.BigEndian, GarminProduct);
        if (definition.Find(name) is Field field)
            body.AsSpan(field.Offset, field.Size).Clear();
    }

    private static void WriteCreator(Stream output, byte localType, ulong? created)
    {
        var fields = new List<Field>(4);
        int offset = 0;
        if (created is < uint.MaxValue)
        {
            fields.Add(new Field(Timestamp, 4, 0x86, offset));
            offset += 4;
        }
        fields.Add(new Field(0, 1, 0x02, offset));
        fields.Add(new Field(2, 2, 0x84, offset + 1));
        fields.Add(new Field(4, 2, 0x84, offset + 3));
        var definition = new Definition(DeviceInfo, 0, false, false, fields.ToArray(), []);
        var body = new byte[definition.BodySize];
        if (definition.Find(Timestamp) is Field timestamp)
            WriteUInt(body, timestamp, false, created!.Value);
        SetDevice(body, definition, 2, 4, 27);
        output.WriteByte((byte)(0x40 | localType));
        WriteDefinition(output, definition);
        output.WriteByte(localType);
        output.Write(body);
    }

    private static void WriteDefinition(Stream output, Definition definition)
    {
        output.WriteByte(definition.Reserved);
        output.WriteByte(definition.BigEndian ? (byte)1 : (byte)0);
        Span<byte> global = stackalloc byte[2];
        if (definition.BigEndian)
            BinaryPrimitives.WriteUInt16BigEndian(global, definition.Global);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(global, definition.Global);
        output.Write(global);

        WriteFields(definition.Fields);
        if (definition.HasDeveloperFields)
            WriteFields(definition.DeveloperFields);

        void WriteFields(Field[] fields)
        {
            output.WriteByte((byte)fields.Length);
            foreach (Field field in fields)
            {
                output.WriteByte(field.Number);
                output.WriteByte(field.Size);
                output.WriteByte(field.BaseType);
            }
        }
    }

    private static void WriteUInt(Span<byte> body, Field field, bool bigEndian, ulong value)
    {
        if (field.Size is 0 or > 8 || (field.Size < 8 && value >= (1UL << (8 * field.Size))))
            throw new InvalidDataException(Damaged);

        for (int index = 0; index < field.Size; index++)
        {
            int destination = field.Offset + (bigEndian ? field.Size - index - 1 : index);
            body[destination] = (byte)(value >> (8 * index));
        }
    }

    private static void Verify(FitFile input, byte[] output, int extraEntries)
    {
        FitFile parsed;
        try
        {
            parsed = Read(output);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException(VerificationFailed, exception);
        }

        if (parsed.Entries.Count != input.Entries.Count + extraEntries ||
            ComputeCrc(output.AsSpan(0, parsed.End)) != BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(parsed.End)) ||
            (parsed.HeaderSize == 14 && ComputeCrc(output.AsSpan(0, 12)) != BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(12))))
        {
            throw new InvalidDataException(VerificationFailed);
        }

        var messages = parsed.Entries.Where(entry => !entry.IsDefinition);
        if (!messages.Any(entry => IsCreator(entry) && entry.ReadUInt(2) == GarminManufacturer && entry.ReadUInt(4) == GarminProduct) ||
            messages.Any(entry => entry.Definition.Global == FileId &&
                (entry.ReadUInt(1) != GarminManufacturer || entry.ReadUInt(2) != GarminProduct)))
        {
            throw new InvalidDataException(VerificationFailed);
        }

        using var original = input.Entries.Where(Untouched).GetEnumerator();
        using var patched = parsed.Entries.Where(Untouched).GetEnumerator();
        while (original.MoveNext())
        {
            if (!patched.MoveNext() || original.Current.Header != patched.Current.Header ||
                original.Current.UtcTimestamp != patched.Current.UtcTimestamp ||
                original.Current.Definition.Global != patched.Current.Definition.Global ||
                !original.Current.Body.Span.SequenceEqual(patched.Current.Body.Span))
            {
                throw new InvalidDataException(VerificationFailed);
            }
        }
        if (patched.MoveNext())
            throw new InvalidDataException(VerificationFailed);

        static bool Untouched(RecordEntry entry) => entry.Definition.Global is not (FileId or DeviceInfo or Activity);
    }

    private static ushort ComputeCrc(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<ushort> table =
        [
            0x0000, 0xCC01, 0xD801, 0x1400, 0xF001, 0x3C00, 0x2800, 0xE401,
            0xA001, 0x6C00, 0x7800, 0xB401, 0x5000, 0x9C01, 0x8801, 0x4400
        ];
        int crc = 0;
        foreach (byte value in bytes)
        {
            crc = (crc >> 4) ^ table[crc & 15] ^ table[value & 15];
            crc = (crc >> 4) ^ table[crc & 15] ^ table[value >> 4];
        }
        return (ushort)crc;
    }

    private readonly record struct Field(byte Number, byte Size, byte BaseType, int Offset);

    private sealed record Definition(ushort Global, byte Reserved, bool BigEndian, bool HasDeveloperFields,
        Field[] Fields, Field[] DeveloperFields)
    {
        public int NativeSize { get; } = Fields.Sum(field => field.Size);
        public int BodySize { get; } = Fields.Sum(field => field.Size) + DeveloperFields.Sum(field => field.Size);

        public Field? Find(byte number)
        {
            foreach (Field field in Fields)
            {
                if (field.Number == number)
                    return field;
            }
            return null;
        }
    }

    private readonly record struct RecordEntry(byte Header, Definition Definition, ReadOnlyMemory<byte> Body,
        uint? UtcTimestamp = null)
    {
        public bool IsCompressed => (Header & 0x80) != 0;
        public bool IsDefinition => !IsCompressed && (Header & 0x40) != 0;
        public int LocalType => IsCompressed ? (Header >> 5) & 3 : Header & 15;

        public ulong? ReadUInt(byte number)
        {
            if (Definition.Find(number) is not Field field || field.Size is 0 or > 8)
                return null;

            ReadOnlySpan<byte> data = Body.Span.Slice(field.Offset, field.Size);
            ulong value = 0;
            for (int index = 0; index < data.Length; index++)
            {
                int source = Definition.BigEndian ? index : data.Length - 1 - index;
                value = (value << 8) | data[source];
            }
            ulong invalid = ulong.MaxValue >> (64 - 8 * data.Length);
            return value == invalid ? null : value;
        }

        public string? ReadString(byte number) => Definition.Find(number) is Field field
            ? DecodeString(Body.Span.Slice(field.Offset, field.Size)) : null;

        public string? ReadDeveloperString(byte number, byte index)
        {
            foreach (Field field in Definition.DeveloperFields)
            {
                if (field.Number == number && field.BaseType == index)
                    return DecodeString(Body.Span.Slice(Definition.NativeSize + field.Offset, field.Size));
            }
            return null;
        }

        private static string? DecodeString(ReadOnlySpan<byte> bytes)
        {
            int end = bytes.IndexOf((byte)0);
            string text = Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]).Trim();
            return text.Length > 0 ? text : null;
        }
    }

    private sealed record FitFile(int HeaderSize, int End, List<RecordEntry> Entries);
}
