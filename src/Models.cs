namespace MED;

public sealed record LabeledVector(double[] Features, string Label);

public interface IClassifier
{
    void Fit(IReadOnlyList<LabeledVector> trainingData);
    string Predict(double[] features);

    string Predict(LabeledVector item) => Predict(item.Features);
}

public static class ModelGuards
{
    public static void EnsureNonEmpty(IReadOnlyList<LabeledVector> data, string paramName)
    {
        if (data is null) throw new ArgumentNullException(paramName);
        if (data.Count == 0) throw new ArgumentException("Dataset cannot be empty.", paramName);
    }

    public static int EnsureConsistentFeatureLength(IReadOnlyList<LabeledVector> data)
    {
        EnsureNonEmpty(data, nameof(data));

        int length = data[0].Features.Length;
        if (length == 0) throw new ArgumentException("Feature vectors cannot be empty.", nameof(data));

        for (int i = 1; i < data.Count; i++)
        {
            if (data[i].Features.Length != length)
                throw new ArgumentException("All feature vectors must have the same length.", nameof(data));
        }

        return length;
    }
}
