using MED.Core;

namespace MED.Distance;

public sealed class DistanceContext
{
    public Dataset Dataset { get; }
    public DistanceMode Mode { get; }
    public NominalMetric NominalMetric { get; }
    public MissingDistanceMode MissingMode { get; }

    // For nominal attributes: map value -> distribution over classes, then SVDM matrix
    // We store per attribute a function Dist(a, x, y)
    private readonly Func<string?, string?, double>[] _nominalDist;

    public DistanceContext(
        Dataset dataset,
        DistanceMode mode,
        NominalMetric nominalMetric,
        MissingDistanceMode missingMode,
        Func<string?, string?, double>[] nominalDist)
    {
        Dataset = dataset;
        Mode = mode;
        NominalMetric = nominalMetric;
        MissingMode = missingMode;
        _nominalDist = nominalDist;
    }

    public double NominalDistance(int j, string? x, string? y) => _nominalDist[j](x, y);

    public static DistanceContext Build(
        Dataset ds,
        DistanceMode mode,
        NominalMetric metric,
        MissingDistanceMode missingMode,
        int? leaveOutIndex = null)
    {
        // training set indices for statistics
        IEnumerable<int> idxs = Enumerable.Range(0, ds.Count);
        if (mode == DistanceMode.Local && leaveOutIndex.HasValue)
            idxs = idxs.Where(i => i != leaveOutIndex.Value);

        var idxArr = idxs.ToArray();

        // Compute numeric min/max/range (in AttributeInfo, but we must not permanently mutate across modes)
        // We'll make shallow copy of AttributeInfo list with fresh numeric stats to avoid collisions.
        var attrs = ds.Attributes.Select(a => new AttributeInfo(a.Name, a.Type)
        {
            // keep nominal values set reference? we need values; just copy into new set
            // We'll fill nominal values below
        }).ToList();

        for (int j = 0; j < ds.FeatureCount; j++)
        {
            if (ds.Attributes[j].Type == AttributeType.Nominal)
            {
                foreach (var v in ds.Attributes[j].NominalValues)
                    attrs[j].NominalValues.Add(v);
            }
        }

        for (int j = 0; j < ds.FeatureCount; j++)
        {
            if (ds.Attributes[j].Type != AttributeType.Numeric) continue;

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;

            foreach (var i in idxArr)
            {
                var v = (double?)ds.Records[i].X[j];
                if (!v.HasValue) continue;
                if (v.Value < min) min = v.Value;
                if (v.Value > max) max = v.Value;
            }

            if (double.IsInfinity(min) || double.IsInfinity(max))
            {
                // all missing -> neutral stats
                min = 0; max = 0;
            }

            attrs[j].Min = min;
            attrs[j].Max = max;
            attrs[j].Range = Math.Max(1e-12, max - min); // avoid divide-by-zero
        }

        // Prepare nominal distance delegates
        var nominalDist = new Func<string?, string?, double>[ds.FeatureCount];

        // Precompute class list
        var classes = idxArr.Select(i => ds.Records[i].Label).Distinct(StringComparer.Ordinal).OrderBy(s => s).ToArray();
        var classIndex = classes.Select((c, k) => (c, k)).ToDictionary(t => t.c, t => t.k, StringComparer.Ordinal);

        for (int j = 0; j < ds.FeatureCount; j++)
        {
            if (ds.Attributes[j].Type != AttributeType.Nominal)
            {
                nominalDist[j] = (_, _) => 0.0;
                continue;
            }

            // For each value x: count class frequencies among rows where attr==x
            var values = attrs[j].NominalValues.OrderBy(v => v, StringComparer.Ordinal).ToArray();
            var valIndex = values.Select((v, k) => (v, k)).ToDictionary(t => t.v, t => t.k, StringComparer.Ordinal);

            // counts[x][class]
            var counts = new int[values.Length, classes.Length];
            var totals = new int[values.Length];

            foreach (var i in idxArr)
            {
                var x = ds.Records[i].X[j] as string;
                if (x is null) continue; // missing handled in distance
                if (!valIndex.TryGetValue(x, out var xi)) continue;

                var ci = classIndex[ds.Records[i].Label];
                counts[xi, ci]++;
                totals[xi]++;
            }

            double P(int xi, int ci) => totals[xi] == 0 ? 0.0 : (double)counts[xi, ci] / totals[xi];

            double svdm(string a, string b)
            {
                if (a == b) return 0.0;
                int ai = valIndex[a];
                int bi = valIndex[b];

                double sum = 0.0;
                for (int c = 0; c < classes.Length; c++)
                    sum += Math.Abs(P(ai, c) - P(bi, c));

                if (metric == NominalMetric.SvdmPrime) sum /= 2.0;
                return sum;
            }

            nominalDist[j] = (x, y) =>
            {
                // Missing handling per task:
                // - If nominal missing in p or q => 2 (or 1 for SVDM')
                if (x is null || y is null)
                {
                    if (missingMode == MissingDistanceMode.Variant1 || missingMode == MissingDistanceMode.Variant2)
                    {
                        return metric == NominalMetric.SvdmPrime ? 1.0 : 2.0;
                    }
                }

                if (x is null && y is null) return 0.0;

                // unseen value fallback (should not happen if loader gathered domain)
                if (!valIndex.ContainsKey(x!) || !valIndex.ContainsKey(y!))
                    return metric == NominalMetric.SvdmPrime ? 1.0 : 2.0;

                return svdm(x!, y!);
            };
        }

        // Important: we return context bound to original Dataset,
        // but numeric stats are stored in attrs copy; so we wrap dataset-like access:
        var wrapped = new Dataset(attrs, ds.Records);

        return new DistanceContext(wrapped, mode, metric, missingMode, nominalDist);
    }
}
