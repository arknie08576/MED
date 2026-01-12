using System.Globalization;
using System.Text;
using MED.Core;
using MED.Distance;
using MED.Metrics;

namespace MED.Output;

public static class StatWriter
{
    // UWAGA: tu jest missingMode jako parametr wymagany
    public static void Write(
        string path,
        string title,
        Dictionary<string, string> meta,
        Dataset dsAfterImpute,
        DistanceMode mode,
        NominalMetric nomMetric,
        MissingDistanceMode missingMode,
        TimeSpan tLoad,
        TimeSpan tImpute,
        TimeSpan tContext,
        TimeSpan tClassify,
        TimeSpan tMetrics,
        TimeSpan tWrite,
        ClassificationMetrics cid,
        ClassificationMetrics ncid)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        var sb = new StringBuilder();

        sb.AppendLine(title);
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("META:");
        foreach (var kv in meta)
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        sb.AppendLine();

        sb.AppendLine("TIMES_MS:");
        sb.AppendLine($"LOAD:     {tLoad.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"IMPUTE:   {tImpute.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"CONTEXT:  {tContext.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"CLASSIFY: {tClassify.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"METRICS:  {tMetrics.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"WRITE:    {tWrite.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"TOTAL:    {(tLoad + tImpute + tContext + tClassify + tMetrics + tWrite).TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}");
        sb.AppendLine();

        sb.AppendLine("ATTRIBUTE_STATS:");
        sb.AppendLine($"DistanceMode: {mode}");
        sb.AppendLine($"NominalMetric: {nomMetric}");
        sb.AppendLine($"MissingMode: {missingMode}");
        sb.AppendLine();

        sb.AppendLine("Numeric attributes (min, max, range):");
        bool anyNum = false;
        for (int j = 0; j < dsAfterImpute.FeatureCount; j++)
        {
            var a = dsAfterImpute.Attributes[j];
            if (a.Type != AttributeType.Numeric) continue;
            anyNum = true;

            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            foreach (var r in dsAfterImpute.Records)
            {
                var v = (double?)r.X[j];
                if (!v.HasValue) continue;
                min = Math.Min(min, v.Value);
                max = Math.Max(max, v.Value);
            }
            if (double.IsInfinity(min) || double.IsInfinity(max)) { min = 0; max = 0; }
            double range = Math.Max(1e-12, max - min);

            sb.AppendLine($"{a.Name}\tmin={min.ToString("G17", CultureInfo.InvariantCulture)}\tmax={max.ToString("G17", CultureInfo.InvariantCulture)}\trange={range.ToString("G17", CultureInfo.InvariantCulture)}");
        }
        if (!anyNum) sb.AppendLine("(none)");
        sb.AppendLine();

        sb.AppendLine("Nominal attributes (domain size):");
        bool anyNom = false;
        for (int j = 0; j < dsAfterImpute.FeatureCount; j++)
        {
            var a = dsAfterImpute.Attributes[j];
            if (a.Type != AttributeType.Nominal) continue;
            anyNom = true;
            sb.AppendLine($"{a.Name}\tdomain={a.NominalValues.Count}");
        }
        if (!anyNom) sb.AppendLine("(none)");
        sb.AppendLine();

        void Dump(string name, ClassificationMetrics m)
        {
            sb.AppendLine($"== {name} ==");
            sb.AppendLine($"Accuracy: {m.Accuracy.ToString("F6", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"BalancedPrecision: {m.BalancedPrecision.ToString("F6", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"BalancedRecall:    {m.BalancedRecall.ToString("F6", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"BalancedF1:        {m.BalancedF1.ToString("F6", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine(m.ConfusionToString());
            sb.AppendLine();

            sb.AppendLine("Per-class metrics:");
            sb.AppendLine("Class\tSupport\tTP\tFP\tFN\tPrecision\tRecall\tF1");

            foreach (var pc in m.PerClasses)
            {
                sb.Append(pc.Class).Append('\t')
                  .Append(pc.Support).Append('\t')
                  .Append(pc.TP).Append('\t')
                  .Append(pc.FP).Append('\t')
                  .Append(pc.FN).Append('\t')
                  .Append(pc.Precision.ToString("F6", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(pc.Recall.ToString("F6", CultureInfo.InvariantCulture)).Append('\t')
                  .AppendLine(pc.F1.ToString("F6", CultureInfo.InvariantCulture));
            }

            sb.AppendLine();
        }

        Dump("CId", cid);
        Dump("NCId", ncid);

        File.WriteAllText(path, sb.ToString());
    }
}
