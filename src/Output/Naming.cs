namespace MED.Output;

public static class Naming
{
    public static string Timestamp() => DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

    public static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "_";

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new char[value.Length];
        int n = 0;

        foreach (var ch in value)
        {
            cleaned[n++] = invalid.Contains(ch) ? '_' : ch;
        }

        return new string(cleaned, 0, n);
    }

    public static string MakeOutputPath(string directory, string prefix, string extension)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory cannot be empty.", nameof(directory));
        if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("Extension cannot be empty.", nameof(extension));

        Directory.CreateDirectory(directory);

        prefix = SanitizeFileNamePart(prefix);
        extension = extension.StartsWith('.') ? extension : "." + extension;

        return Path.Combine(directory, $"{prefix}_{Timestamp()}{extension}");
    }
}
