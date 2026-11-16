using System.Buffers.Binary;
using MyWhoosh2Garmin;

AddsCreatorAndKeepsData();
UpdatesOnlyCreator();
KeepsCompressedTimestamps();
RejectsDamagedAndNonActivityStreams();
DeletesOnlySuccessfullyConvertedSources();
Console.WriteLine("FIT fixtures passed.");

static void AddsCreatorAndKeepsData()
{
    const string originalIdDefinition = "600001000003 000100 040486 080507 01 090200";
    const string originalId = "00 04 3F010203 5465737400 AABB";
    const string outputIdDefinition = "600001000005 000100 040486 080507 010284 020284 01 090200";
    const string outputId = "00 04 3F010203 0000000000 0001 10DB AABB";
    const string creator = "410000170004 FD0486 000102 020284 040284 01 0302013F 00 0100 DB10";
    // Local type 1 is reused by an original definition after the injected creator.
    const string samples = "610000140003 FD0486 030102 070284 01 0B0300 01 0402013F A0 D200 112233";
    const string titleField = "430000CE0003 000102 010102 030607 03 00 09 5469746C6500";
    const string workout = "4400001A0001 080807 04 576F726B6F757400";
    const string session = "650000120000 01 090500 05 5269646500";
    const string summaries = titleField + workout + session;
    const string activityPrefix = "420000220002 FD0486 050486 02 0402013F";
    var tail = Fit(12, Hex("400000000001 000100 00 06"));
    var utc = new DateTimeOffset(1989, 12, 31, 0, 0, 0, TimeSpan.Zero).AddSeconds(0x3f010204);
    var localTimestamp = new byte[4];
    var offset = (int)TimeZoneInfo.Local.GetUtcOffset(utc).TotalSeconds;
    BinaryPrimitives.WriteUInt32LittleEndian(localTimestamp, (uint)(0x3f010204 + offset));
    var input = Fit(14, Hex(originalIdDefinition + originalId + samples + summaries + activityPrefix + "00000000"), tail);
    var expected = Fit(14, Hex(outputIdDefinition + outputId + creator + samples + summaries + activityPrefix)
        .Concat(localTimestamp).ToArray(), tail);

    ExpectBytes(expected, FitPatcher.Patch(input, out var report), nameof(AddsCreatorAndKeepsData));
    if (report.ActivityName != "Ride")
    {
        throw new Exception($"Expected developer title 'Ride', got '{report.ActivityName}'.");
    }
    if (report.StartDate != utc.AddSeconds(-1))
    {
        throw new Exception("Incorrect activity start date.");
    }

    ExpectBytes(expected, FitPatcher.Patch(expected), "Repatching a converted activity");
}

static void UpdatesOnlyCreator()
{
    const string idDefinition = "400000000003 000100 010284 020284";
    const string originalId = "00 04 4B01 6300";
    const string outputId = "00 04 0100 DB10";
    const string originalDeviceDefinition = "630000170003 000102 020284 1B0407 01 060100";
    const string outputDeviceDefinition = "630000170004 000102 020284 1B0407 040284 01 060100";
    const string originalDevices = "03 00 4B01 4E616D00 AB 03 01 5900 506F7700 CD";
    const string outputDevices = "03 00 0100 00000000 DB10 AB 03 01 5900 506F7700 FFFF CD";
    const string samples = "440000140002 FD0486 030102 04 0302013F 8E";
    var input = Fit(12, Hex(idDefinition + originalId + originalDeviceDefinition + originalDevices + samples));
    var expected = Fit(12, Hex(idDefinition + outputId + outputDeviceDefinition + outputDevices + samples));
    // The macOS version accepts parseable input with a bad CRC and writes a corrected copy.
    input[^1] ^= 0xff;

    ExpectBytes(expected, FitPatcher.Patch(input), nameof(UpdatesOnlyCreator));
}

static void KeepsCompressedTimestamps()
{
    const string id = "400000000003 000100 010284 020284 00 04 0100 DB10";
    const string creator = "410000170003 000102 020284 040284 01 00 0100 DB10";
    const string fullTimestamp = "420000140002 FD0486 030102 02 1F02013F 8E";
    // The low five timestamp bits roll over from 31 to 0 and then advance to 1.
    const string compressedRecord = "420000140001 030102 C0 8F";
    const string activity = "430000220001 050486 E1";
    var localTimestamp = new byte[4];
    BinaryPrimitives.WriteUInt32LittleEndian(localTimestamp, 0x3f010221 + 19800);
    var timeZone = TimeZoneInfo.CreateCustomTimeZone("Test UTC+05:30", TimeSpan.FromMinutes(330), "Test zone", "Test zone");
    var input = Fit(14, Hex(id + creator + fullTimestamp + compressedRecord + activity + "00000000"));
    var expected = Fit(14, Hex(id + creator + fullTimestamp + compressedRecord + activity)
        .Concat(localTimestamp).ToArray());

    ExpectBytes(expected, FitPatcher.Patch(input, out _, timeZone), nameof(KeepsCompressedTimestamps));
}

static void RejectsDamagedAndNonActivityStreams()
{
    ExpectRejected(Hex("504B0304"), "Non-FIT signature");
    ExpectRejected(Fit(14, Hex("400000000002 000100")), "Incomplete definition");
    ExpectRejected(Fit(12, Hex("0A 0102")), "Undefined local type");
    ExpectRejected(Fit(12, Hex("400000000001 000100 00 04"))[..^1], "Missing file CRC");
    ExpectRejected(Fit(12, Hex("400000000001 000100 00 06 410000140001 030102 01 8E")), "Course file");
}

static void DeletesOnlySuccessfullyConvertedSources()
{
    var directory = Directory.CreateTempSubdirectory("Whoosh2Garmin-tests-");
    try
    {
        const string id = "400000000003 000100 010284 020284 00 04 4B01 6300";
        const string workout = "4200001A0001 080C07 02 48696C6C3A20526964653F00";
        const string samples = "410000140002 FD0486 030102 01 1F02013F 8E";
        var input = Fit(12, Hex(id + workout + samples));
        var sources = new[] { "source-one.FIT", "source-two.fit" }
            .Select(name => Path.Combine(directory.FullName, name)).ToArray();
        foreach (var source in sources)
        {
            File.WriteAllBytes(source, input);
            File.SetLastWriteTime(source, new DateTime(2026, 1, 14, 12, 0, 0, DateTimeKind.Local));
        }

        var damaged = Path.Combine(directory.FullName, "damaged.fit");
        byte[] damagedBytes = [1, 2, 3];
        File.WriteAllBytes(damaged, damagedBytes);
        try
        {
            ActivityConverter.Convert(damaged);
            throw new Exception("A damaged activity was accepted.");
        }
        catch (InvalidDataException)
        {
            ExpectBytes(damagedBytes, File.ReadAllBytes(damaged), "Failed conversion source");
        }

        var stem = (OperatingSystem.IsWindows() ? "Hill- Ride-" : "Hill: Ride?") + " - 2026-01-14 - fixed";
        var existing = Path.Combine(directory.FullName, stem + ".fit");
        File.WriteAllText(existing, "existing copy");
        var results = new string[2];
        Parallel.For(0, results.Length, index => results[index] = ActivityConverter.Convert(sources[index]));

        var expectedPaths = new[] { stem + " (2).fit", stem + " (3).fit" }
            .Select(name => Path.Combine(directory.FullName, name));
        if (!results.Order().SequenceEqual(expectedPaths))
        {
            throw new Exception($"Unexpected output names: {string.Join(", ", results)}.");
        }

        if (sources.Any(File.Exists))
            throw new Exception("A successfully converted source was not removed.");
        if (File.ReadAllText(existing) != "existing copy" || directory.GetFiles("*.tmp").Length != 0)
        {
            throw new Exception("An existing copy changed or a temporary file remained.");
        }

        var expected = FitPatcher.Patch(input);
        foreach (var result in results)
        {
            ExpectBytes(expected, File.ReadAllBytes(result), "Published output");
        }
    }
    finally
    {
        directory.Delete(recursive: true);
    }
}

static void ExpectRejected(byte[] input, string scenario)
{
    try
    {
        FitPatcher.Patch(input);
    }
    catch (InvalidDataException)
    {
        return;
    }

    throw new Exception($"{scenario} was accepted.");
}

static void ExpectBytes(byte[] expected, byte[] actual, string scenario)
{
    if (!actual.AsSpan().SequenceEqual(expected))
    {
        throw new Exception($"{scenario} did not match.\nExpected {Convert.ToHexString(expected)}\nActual   {Convert.ToHexString(actual)}");
    }
}

static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

static byte[] Fit(int headerSize, byte[] messages, byte[]? tail = null)
{
    var file = new byte[headerSize + messages.Length + 2 + (tail?.Length ?? 0)];
    file[0] = (byte)headerSize;
    file[1] = 0x20;
    BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(2), 2100);
    BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(4), messages.Length);
    ".FIT"u8.CopyTo(file.AsSpan(8));
    if (headerSize == 14)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(12), Crc(file.AsSpan(0, 12)));
    }

    messages.CopyTo(file, headerSize);
    var end = headerSize + messages.Length;
    BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(end), Crc(file.AsSpan(0, end)));
    tail?.CopyTo(file, end + 2);
    return file;
}

// Reflected polynomial 0xA001 calculated bit by bit, independent of the patcher.
static ushort Crc(ReadOnlySpan<byte> bytes)
{
    var crc = 0;
    foreach (var value in bytes)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xa001 : crc >> 1;
        }
    }

    return (ushort)crc;
}
