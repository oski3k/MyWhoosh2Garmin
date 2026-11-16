using System.Globalization;

namespace MyWhoosh2Garmin;

internal static class ActivityConverter
{
    public static string Convert(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".fit", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Wybierz plik z rozszerzeniem .fit.");

        var source = Path.GetFullPath(path);
        string output;
        using (var input = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (input.Length > Array.MaxLength)
                throw new InvalidDataException("Plik FIT jest za duży.");

            var original = new byte[(int)input.Length];
            input.ReadExactly(original);
            var bytes = FitPatcher.Patch(original, out var report);
            var name = SafeTitle(report.ActivityName) ?? SafeTitle(Path.GetFileNameWithoutExtension(source));
            var date = (report.StartDate ?? File.GetLastWriteTimeUtc(input.SafeFileHandle)).ToLocalTime();
            var stamp = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var stem = name is null ? $"{stamp} - fixed" : $"{name} - {stamp} - fixed";
            output = SaveVerifiedCopy(Path.GetDirectoryName(source)!, stem, bytes);
        }

        File.Delete(source);
        return output;
    }

    private static string SaveVerifiedCopy(string directory, string stem, byte[] bytes)
    {
        // Commit a complete file in the same directory without replacing an existing file.
        var temp = Path.Combine(directory, $".whoosh2garmin-{Guid.NewGuid():N}.tmp");
        var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write);
        try
        {
            using (stream)
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            for (var number = 1; ; number++)
            {
                var suffix = number == 1 ? "" : $" ({number})";
                var output = Path.Combine(directory, $"{stem}{suffix}.fit");
                if (File.Exists(output) || Directory.Exists(output))
                    continue;

                try
                {
                    File.Move(temp, output);
                    return output;
                }
                catch (IOException) when (File.Exists(output) || Directory.Exists(output))
                {
                    // Another process claimed this name while we were writing.
                }
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string? SafeTitle(string? name)
    {
        if (name is null)
            return null;

        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(name.Select(character => invalid.Contains(character) ? '-' : character))
            .Trim().TrimEnd('.');
        const int maxTitleLength = 120;
        if (safe.Length > maxTitleLength)
        {
            var end = char.IsHighSurrogate(safe[maxTitleLength - 1]) ? maxTitleLength - 1 : maxTitleLength;
            safe = safe[..end].TrimEnd(' ', '.');
        }

        return safe.Length == 0 ? null : safe;
    }
}
