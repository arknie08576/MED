namespace MED;

public interface IDistanceMetric
{
    double Between(ReadOnlySpan<double> a, ReadOnlySpan<double> b);
}

public interface ITrainableDistanceMetric : IDistanceMetric
{
    void Fit(IReadOnlyList<LabeledVector> trainingData);
}

/// <summary>
/// Nominal mismatch distance: counts how many attributes differ.
/// Assumes nominal values are encoded as integer ids stored in doubles.
/// </summary>
public sealed class NominalMismatchDistance : IDistanceMetric
{
    public static readonly NominalMismatchDistance Instance = new();

    private NominalMismatchDistance() { }

    public double Between(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vector lengths must match.");

        double mismatches = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) mismatches += 1;
        }
        return mismatches;
    }
}

/// <summary>
/// SVDM (Symbolic Value Difference Metric) for nominal attributes.
/// For each attribute and value, estimates P(class|value) from training data.
/// Distance is sum over attributes of sum_c |P(c|v1) - P(c|v2)|.
/// </summary>
public sealed class SvdmDistance : ITrainableDistanceMetric
{
    private readonly double _missingPenalty;
    private int _featureCount;
    private int _classCount;
    private Dictionary<string, int>? _classIndex;
    private List<Dictionary<int, double[]>>? _probsByAttr;

    public SvdmDistance(double missingPenalty = 1.0)
    {
        if (missingPenalty <= 0) throw new ArgumentOutOfRangeException(nameof(missingPenalty));
        _missingPenalty = missingPenalty;
    }

    public void Fit(IReadOnlyList<LabeledVector> trainingData)
    {
        ModelGuards.EnsureNonEmpty(trainingData, nameof(trainingData));
        _featureCount = ModelGuards.EnsureConsistentFeatureLength(trainingData);

        _classIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in trainingData)
        {
            if (!_classIndex.ContainsKey(row.Label))
                _classIndex[row.Label] = _classIndex.Count;
        }

        _classCount = _classIndex.Count;
        if (_classCount == 0) throw new ArgumentException("Training data must contain at least one class.", nameof(trainingData));

        var countsByAttr = new List<Dictionary<int, int[]>>(_featureCount);
        for (int j = 0; j < _featureCount; j++)
            countsByAttr.Add(new Dictionary<int, int[]>());

        foreach (var row in trainingData)
        {
            int classIdx = _classIndex[row.Label];
            for (int j = 0; j < _featureCount; j++)
            {
                int v = (int)row.Features[j];
                if (v < 0) continue; // missing

                var dict = countsByAttr[j];
                if (!dict.TryGetValue(v, out var counts))
                {
                    counts = new int[_classCount + 1]; // last slot = total
                    dict[v] = counts;
                }

                counts[classIdx]++;
                counts[_classCount]++;
            }
        }

        _probsByAttr = new List<Dictionary<int, double[]>>(_featureCount);
        for (int j = 0; j < _featureCount; j++)
        {
            var probs = new Dictionary<int, double[]>();
            foreach (var kv in countsByAttr[j])
            {
                int total = kv.Value[_classCount];
                if (total <= 0) continue;

                var p = new double[_classCount];
                for (int c = 0; c < _classCount; c++)
                    p[c] = (double)kv.Value[c] / total;

                probs[kv.Key] = p;
            }

            _probsByAttr.Add(probs);
        }
    }

    public double Between(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (_probsByAttr is null || _classIndex is null)
            throw new InvalidOperationException("Call Fit() before using SvdmDistance.");

        if (a.Length != b.Length) throw new ArgumentException("Vector lengths must match.");
        if (a.Length != _featureCount) throw new ArgumentException("Vector length does not match fitted feature count.");

        double dist = 0;
        for (int j = 0; j < _featureCount; j++)
        {
            int v1 = (int)a[j];
            int v2 = (int)b[j];
            if (v1 == v2) continue;

            if (v1 < 0 || v2 < 0)
            {
                dist += _missingPenalty;
                continue;
            }

            var probs = _probsByAttr[j];
            if (!probs.TryGetValue(v1, out var p1) || !probs.TryGetValue(v2, out var p2))
            {
                dist += _missingPenalty;
                continue;
            }

            double d = 0;
            for (int c = 0; c < _classCount; c++)
                d += Math.Abs(p1[c] - p2[c]);

            dist += d;
        }

        return dist;
    }
}

public sealed class EuclideanDistance : IDistanceMetric
{
    public static readonly EuclideanDistance Instance = new();

    private EuclideanDistance() { }

    public double Between(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Vector lengths must match.");

        double sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            double d = a[i] - b[i];
            sum += d * d;
        }
        return Math.Sqrt(sum);
    }
}
