using System.Text;
using System.Text.Json;
using MED.Evaluation;

namespace MED.Output;

public static class Writers
{
    public static void WritePredictionsCsv(string path, IReadOnlyList<string> yTrue, IReadOnlyList<string> yPred)
    {
        if (yTrue is null) throw new ArgumentNullException(nameof(yTrue));
        if (yPred is null) throw new ArgumentNullException(nameof(yPred));
        if (yTrue.Count != yPred.Count) throw new ArgumentException("yTrue and yPred must have the same length.");

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        var sb = new StringBuilder();
        sb.AppendLine("index,y_true,y_pred");
        for (int i = 0; i < yTrue.Count; i++)
        {
            sb.Append(i);
            sb.Append(',');
            sb.Append(Escape(yTrue[i]));
            sb.Append(',');
            sb.AppendLine(Escape(yPred[i]));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    public static void WriteReportJson(string path, ClassificationReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(path, JsonSerializer.Serialize(report, options), Encoding.UTF8);
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;
        bool needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes) return value;

        return '"' + value.Replace("\"", "\"\"") + '"';
    }
}
