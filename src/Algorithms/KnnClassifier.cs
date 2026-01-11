namespace MED.Algorithms;

public sealed class KnnClassifier : IClassifier
{
    private readonly int _k;
    private readonly IDistanceMetric _distance;
    private IReadOnlyList<LabeledVector>? _training;

    public KnnClassifier(int k = 3, IDistanceMetric? distance = null)
    {
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k), "k must be positive.");
        _k = k;
        _distance = distance ?? EuclideanDistance.Instance;
    }

    public void Fit(IReadOnlyList<LabeledVector> trainingData)
    {
        ModelGuards.EnsureNonEmpty(trainingData, nameof(trainingData));
        ModelGuards.EnsureConsistentFeatureLength(trainingData);
        if (_distance is ITrainableDistanceMetric trainable)
            trainable.Fit(trainingData);
        _training = trainingData;
    }

    public string Predict(double[] features)
    {
        if (_training is null) throw new InvalidOperationException("Call Fit() before Predict().");
        if (features is null) throw new ArgumentNullException(nameof(features));

        int featureCount = _training[0].Features.Length;
        if (features.Length != featureCount)
            throw new ArgumentException($"Expected feature vector length {featureCount}.", nameof(features));

        int k = Math.Min(_k, _training.Count);

        var neighbors = new (double Dist, string Label)[_training.Count];
        for (int i = 0; i < _training.Count; i++)
        {
            var item = _training[i];
            double d = _distance.Between(features, item.Features);
            neighbors[i] = (d, item.Label);
        }

        Array.Sort(neighbors, static (a, b) => a.Dist.CompareTo(b.Dist));

        var counts = new Dictionary<string, (int Count, double DistSum)>(StringComparer.Ordinal);
        for (int i = 0; i < k; i++)
        {
            var (dist, label) = neighbors[i];
            if (counts.TryGetValue(label, out var v))
                counts[label] = (v.Count + 1, v.DistSum + dist);
            else
                counts[label] = (1, dist);
        }

        string? bestLabel = null;
        int bestCount = -1;
        double bestAvgDist = double.PositiveInfinity;

        foreach (var kv in counts)
        {
            var label = kv.Key;
            var (count, distSum) = kv.Value;
            double avgDist = distSum / count;

            if (count > bestCount || (count == bestCount && avgDist < bestAvgDist))
            {
                bestLabel = label;
                bestCount = count;
                bestAvgDist = avgDist;
            }
        }

        return bestLabel ?? string.Empty;
    }
}
