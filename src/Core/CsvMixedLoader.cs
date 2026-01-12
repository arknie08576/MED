using System.Globalization;

namespace MED.Core;

public sealed class CsvMixedLoader
{
    private static readonly string[] DefaultMissingTokens = ["", "?", "NA", "N/A", "null", "NULL", "-"];

    public Dataset Load(string path, char sep = ',', bool hasHeader = true, string[]? missingTokens = null)
    {
        missingTokens ??= DefaultMissingTokens;

        // 1) Read lines and ignore empty/whitespace-only lines (nursery.data has an empty last line)
        var rawLines = File.ReadAllLines(path);
        var lines = rawLines
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length < 2) throw new InvalidOperationException("CSV has no data rows.");

        int start = hasHeader ? 1 : 0;

        var firstData = Split(lines[start], sep);
        int cols = firstData.Length;
        if (cols < 2) throw new InvalidOperationException("CSV must have at least 1 feature and 1 label column.");

        int featureCount = cols - 1;

        // 2) Infer types: if any non-missing token cannot be parsed as double -> nominal
        var colIsNumeric = Enumerable.Repeat(true, featureCount).ToArray();

        for (int r = start; r < lines.Length; r++)
        {
            var parts = Split(lines[r], sep);

            // If a line is malformed, throw with clear message
            if (parts.Length != cols)
                throw new InvalidOperationException($"Row {r + 1} has wrong column count (got {parts.Length}, expected {cols}). Line='{lines[r]}'");

            for (int c = 0; c < featureCount; c++)
            {
                var tok = parts[c].Trim();
                if (IsMissing(tok, missingTokens)) continue;

                if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    colIsNumeric[c] = false;
            }
        }

        // 3) Attribute names
        string[] names;
        if (hasHeader)
        {
            var header = Split(lines[0], sep);
            if (header.Length != cols) throw new InvalidOperationException("Header column count mismatch.");
            names = header.Take(featureCount).Select(s => s.Trim()).ToArray();
        }
        else
        {
            names = Enumerable.Range(1, featureCount).Select(i => $"f{i}").ToArray();
        }

        var attrs = new List<AttributeInfo>(featureCount);
        for (int c = 0; c < featureCount; c++)
            attrs.Add(new AttributeInfo(names[c], colIsNumeric[c] ? AttributeType.Numeric : AttributeType.Nominal));

        // 4) Parse rows
        var records = new List<Record>(lines.Length - start);
        int id = 0;

        for (int r = start; r < lines.Length; r++)
        {
            var parts = Split(lines[r], sep);
            if (parts.Length != cols)
                throw new InvalidOperationException($"Row {r + 1} has wrong column count (got {parts.Length}, expected {cols}). Line='{lines[r]}'");

            var x = new object?[featureCount];

            for (int c = 0; c < featureCount; c++)
            {
                var tok = parts[c].Trim();
                if (IsMissing(tok, missingTokens))
                {
                    x[c] = null;
                    continue;
                }

                if (attrs[c].Type == AttributeType.Numeric)
                {
                    if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        throw new InvalidOperationException($"Cannot parse numeric value '{tok}' at row {r + 1}, col {c + 1}.");
                    x[c] = (double?)v;
                }
                else
                {
                    x[c] = tok;
                    attrs[c].NominalValues.Add(tok);
                }
            }

            var label = parts[featureCount].Trim();
            if (string.IsNullOrWhiteSpace(label))
                throw new InvalidOperationException($"Empty label at row {r + 1}.");

            records.Add(new Record(id++, x, label));
        }

        return new Dataset(attrs, records);
    }

    private static string[] Split(string line, char sep) => line.Split(sep);

    private static bool IsMissing(string token, string[] missingTokens)
        => missingTokens.Any(mt => string.Equals(token, mt, StringComparison.OrdinalIgnoreCase));
}
