namespace MED.Algorithms;

/// <summary>
/// Simple nearest-centroid classifier (one centroid per label).
/// </summary>
public sealed class RiaClassifier : IClassifier
{
    private readonly IDistanceMetric _distance;
    private Dictionary<string, double[]>? _centroids;

    public RiaClassifier(IDistanceMetric? distance = null)
    {
        _distance = distance ?? EuclideanDistance.Instance;
    }

    public void Fit(IReadOnlyList<LabeledVector> trainingData)
    {
        ModelGuards.EnsureNonEmpty(trainingData, nameof(trainingData));
        int featureCount = ModelGuards.EnsureConsistentFeatureLength(trainingData);

        var sums = new Dictionary<string, (double[] Sum, int Count)>(StringComparer.Ordinal);

        foreach (var row in trainingData)
        {
            if (!sums.TryGetValue(row.Label, out var acc))
            {
                acc = (new double[featureCount], 0);
            }

            for (int j = 0; j < featureCount; j++)
                acc.Sum[j] += row.Features[j];

            acc.Count++;
            sums[row.Label] = acc;
        }

        _centroids = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (var kv in sums)
        {
            var centroid = new double[featureCount];
            for (int j = 0; j < featureCount; j++)
                centroid[j] = kv.Value.Sum[j] / kv.Value.Count;

            _centroids[kv.Key] = centroid;
        }
    }

    public string Predict(double[] features)
    {
        if (_centroids is null) throw new InvalidOperationException("Call Fit() before Predict().");
        if (features is null) throw new ArgumentNullException(nameof(features));

        string? bestLabel = null;
        double bestDist = double.PositiveInfinity;

        foreach (var kv in _centroids)
        {
            double d = _distance.Between(features, kv.Value);
            if (d < bestDist)
            {
                bestDist = d;
                bestLabel = kv.Key;
            }
        }

        return bestLabel ?? string.Empty;
    }
}
