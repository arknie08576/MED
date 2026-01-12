using MED.Core;

namespace MED.Preprocessing;

public sealed class ClassConditionalImputer
{
    public Dataset Impute(Dataset ds)
    {
        // group indices by class
        var byClass = ds.Records
            .Select((rec, idx) => (rec, idx))
            .GroupBy(t => t.rec.Label)
            .ToDictionary(g => g.Key, g => g.Select(t => t.idx).ToArray(), StringComparer.Ordinal);

        // Precompute per class:
        // - numeric means
        // - nominal modes
        var numericMean = new Dictionary<(string cls, int j), double>();
        var nominalMode = new Dictionary<(string cls, int j), string>();

        foreach (var (cls, indices) in byClass)
        {
            for (int j = 0; j < ds.FeatureCount; j++)
            {
                var attr = ds.Attributes[j];
                if (attr.Type == AttributeType.Numeric)
                {
                    double sum = 0;
                    int cnt = 0;
                    foreach (var i in indices)
                    {
                        var v = (double?)ds.Records[i].X[j];
                        if (v.HasValue) { sum += v.Value; cnt++; }
                    }

                    // If entire class missing in this column, fall back to 0.0 (rare but avoids crash)
                    numericMean[(cls, j)] = cnt > 0 ? sum / cnt : 0.0;
                }
                else
                {
                    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var i in indices)
                    {
                        var s = ds.Records[i].X[j] as string;
                        if (s is null) continue;
                        counts.TryGetValue(s, out var c);
                        counts[s] = c + 1;
                    }

                    // If entire class missing, fall back to most frequent overall (or empty)
                    var mode = counts.Count > 0
                        ? counts.OrderByDescending(kv => kv.Value).First().Key
                        : (ds.Attributes[j].NominalValues.FirstOrDefault() ?? "");

                    nominalMode[(cls, j)] = mode;
                }
            }
        }

        // Build new records with filled values (do NOT mutate original)
        var newRecords = new List<Record>(ds.Count);

        foreach (var rec in ds.Records)
        {
            var x = new object?[ds.FeatureCount];
            for (int j = 0; j < ds.FeatureCount; j++)
            {
                if (rec.X[j] is not null)
                {
                    x[j] = rec.X[j];
                    continue;
                }

                var attr = ds.Attributes[j];
                if (attr.Type == AttributeType.Numeric)
                    x[j] = (double?)numericMean[(rec.Label, j)];
                else
                    x[j] = nominalMode[(rec.Label, j)];
            }

            newRecords.Add(new Record(rec.Id, x, rec.Label));
        }

        return new Dataset(ds.Attributes, newRecords);
    }
}
