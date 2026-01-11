namespace MED.Algorithms;

/// <summary>
/// Distance-weighted kNN classifier (votes weighted by 1/(d+eps)).
/// </summary>
public sealed class RionaClassifier : IClassifier
{
    private readonly int _k;
    private readonly double _epsilon;
    private readonly IDistanceMetric _distance;
    private IReadOnlyList<LabeledVector>? _training;

    public RionaClassifier(int k = 5, double epsilon = 1e-9, IDistanceMetric? distance = null)
    {
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k));
        if (epsilon <= 0) throw new ArgumentOutOfRangeException(nameof(epsilon));

        _k = k;
        _epsilon = epsilon;
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

        var neighbors = new (double Dist, string Label)[_training.Count];
        for (int i = 0; i < _training.Count; i++)
        {
            var item = _training[i];
            double d = _distance.Between(features, item.Features);
            neighbors[i] = (d, item.Label);
        }

        Array.Sort(neighbors, static (a, b) => a.Dist.CompareTo(b.Dist));

        int k = Math.Min(_k, neighbors.Length);
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);

        for (int i = 0; i < k; i++)
        {
            var (dist, label) = neighbors[i];
            double w = 1.0 / (dist + _epsilon);
            scores[label] = scores.TryGetValue(label, out var s) ? (s + w) : w;
        }

        string? bestLabel = null;
        double bestScore = double.NegativeInfinity;
        foreach (var kv in scores)
        {
            if (kv.Value > bestScore)
            {
                bestScore = kv.Value;
                bestLabel = kv.Key;
            }
        }

        return bestLabel ?? string.Empty;
    }
}
