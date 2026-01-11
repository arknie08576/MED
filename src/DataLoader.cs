using System.Globalization;

namespace MED;

public static class DataLoader
{
    /// <summary>
    /// Loads a CSV where one column is a label and the remaining columns are numeric features.
    /// By default the label is taken from the last column.
    /// </summary>
    public static IReadOnlyList<LabeledVector> LoadCsv(
        string path,
        bool hasHeader = true,
        char separator = ',',
        int labelColumn = -1,
        CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("CSV file not found.", path);

        culture ??= CultureInfo.InvariantCulture;

        var lines = File.ReadLines(path);
        using var enumerator = lines.GetEnumerator();

        if (hasHeader)
        {
            if (!enumerator.MoveNext()) return Array.Empty<LabeledVector>();
        }

        var result = new List<LabeledVector>();

        while (enumerator.MoveNext())
        {
            var line = enumerator.Current;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(separator);
            if (parts.Length < 2) continue;

            int labelIndex = labelColumn < 0 ? parts.Length - 1 : labelColumn;
            if (labelIndex < 0 || labelIndex >= parts.Length)
                throw new ArgumentOutOfRangeException(nameof(labelColumn), "Label column index is out of range.");

            string label = parts[labelIndex].Trim();
            if (label.Length == 0) continue;

            var features = new double[parts.Length - 1];
            int f = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i == labelIndex) continue;

                if (!double.TryParse(parts[i], NumberStyles.Float | NumberStyles.AllowThousands, culture, out double value))
                    throw new FormatException($"Invalid numeric value '{parts[i]}' in line: {line}");

                features[f++] = value;
            }

            result.Add(new LabeledVector(features, label));
        }

        return result;
    }

    /// <summary>
    /// Loads a delimited file with nominal (categorical) values.
    /// Values are encoded per-column into integer ids stored as doubles.
    /// By default the label is taken from the last column.
    /// </summary>
    public static IReadOnlyList<LabeledVector> LoadDelimitedNominal(
        string path,
        bool hasHeader = false,
        char separator = ',',
        int labelColumn = -1,
        string? missingToken = null)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("Input file not found.", path);

        var lines = File.ReadLines(path);
        using var enumerator = lines.GetEnumerator();

        if (hasHeader)
        {
            if (!enumerator.MoveNext()) return Array.Empty<LabeledVector>();
        }

        var rows = new List<LabeledVector>();
        List<Dictionary<string, int>>? encoders = null;

        while (enumerator.MoveNext())
        {
            var line = enumerator.Current;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(separator);
            if (parts.Length < 2) continue;

            int labelIndex = labelColumn < 0 ? parts.Length - 1 : labelColumn;
            if (labelIndex < 0 || labelIndex >= parts.Length)
                throw new ArgumentOutOfRangeException(nameof(labelColumn), "Label column index is out of range.");

            string label = parts[labelIndex].Trim();
            if (label.Length == 0) continue;

            int featureCount = parts.Length - 1;
            encoders ??= Enumerable.Range(0, featureCount)
                .Select(_ => new Dictionary<string, int>(StringComparer.Ordinal))
                .ToList();

            if (encoders.Count != featureCount)
                throw new FormatException("Inconsistent column count in input file.");

            var features = new double[featureCount];
            int f = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (i == labelIndex) continue;

                var raw = parts[i].Trim();
                if (raw.Length == 0 || (missingToken is not null && string.Equals(raw, missingToken, StringComparison.Ordinal)))
                {
                    features[f++] = -1;
                    continue;
                }

                var dict = encoders[f];
                if (!dict.TryGetValue(raw, out int code))
                {
                    code = dict.Count;
                    dict[raw] = code;
                }

                features[f++] = code;
            }

            rows.Add(new LabeledVector(features, label));
        }

        return rows;
    }
}
