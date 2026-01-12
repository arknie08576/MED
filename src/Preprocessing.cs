namespace MED;

public static class Preprocessing1
{
    public static IReadOnlyList<LabeledVector> Standardize(IReadOnlyList<LabeledVector> data)
    {
        ModelGuards.EnsureNonEmpty(data, nameof(data));
        int featureCount = ModelGuards.EnsureConsistentFeatureLength(data);

        var means = new double[featureCount];
        var variances = new double[featureCount];

        for (int i = 0; i < data.Count; i++)
        {
            var x = data[i].Features;
            for (int j = 0; j < featureCount; j++) means[j] += x[j];
        }

        for (int j = 0; j < featureCount; j++) means[j] /= data.Count;

        for (int i = 0; i < data.Count; i++)
        {
            var x = data[i].Features;
            for (int j = 0; j < featureCount; j++)
            {
                double d = x[j] - means[j];
                variances[j] += d * d;
            }
        }

        for (int j = 0; j < featureCount; j++) variances[j] = Math.Sqrt(variances[j] / data.Count);

        var output = new List<LabeledVector>(data.Count);
        for (int i = 0; i < data.Count; i++)
        {
            var src = data[i];
            var scaled = new double[featureCount];
            for (int j = 0; j < featureCount; j++)
            {
                double std = variances[j];
                scaled[j] = std == 0 ? 0 : (src.Features[j] - means[j]) / std;
            }
            output.Add(new LabeledVector(scaled, src.Label));
        }

        return output;
    }
}
