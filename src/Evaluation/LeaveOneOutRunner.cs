using MED.Algorithms;

namespace MED.Evaluation;

public sealed record LeaveOneOutResult(
    IReadOnlyList<string> TrueLabels,
    IReadOnlyList<string> PredictedLabels,
    ClassificationReport Report);

public static class LeaveOneOutRunner
{
    public static ClassificationReport Run(
        IReadOnlyList<LabeledVector> data,
        Func<IClassifier> classifierFactory)
    {
        return RunDetailed(data, classifierFactory).Report;
    }

    public static LeaveOneOutResult RunDetailed(
        IReadOnlyList<LabeledVector> data,
        Func<IClassifier> classifierFactory)
    {
        ModelGuards.EnsureNonEmpty(data, nameof(data));
        _ = ModelGuards.EnsureConsistentFeatureLength(data);
        if (classifierFactory is null) throw new ArgumentNullException(nameof(classifierFactory));

        var yTrue = new string[data.Count];
        var yPred = new string[data.Count];

        for (int i = 0; i < data.Count; i++)
        {
            var train = new List<LabeledVector>(data.Count - 1);
            for (int j = 0; j < data.Count; j++)
            {
                if (j != i) train.Add(data[j]);
            }

            var model = classifierFactory();
            model.Fit(train);

            yTrue[i] = data[i].Label;
            yPred[i] = model.Predict(data[i]);
        }

        var report = Metrics.Compute(yTrue, yPred);
        return new LeaveOneOutResult(yTrue, yPred, report);
    }
}
