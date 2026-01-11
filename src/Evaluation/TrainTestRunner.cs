namespace MED.Evaluation;

public sealed record TrainTestResult(
    IReadOnlyList<string> TrueLabels,
    IReadOnlyList<string> PredictedLabels,
    ClassificationReport Report);

public static class TrainTestRunner
{
    public static TrainTestResult Run(
        IReadOnlyList<LabeledVector> train,
        IReadOnlyList<LabeledVector> test,
        Func<IClassifier> classifierFactory)
    {
        ModelGuards.EnsureNonEmpty(train, nameof(train));
        ModelGuards.EnsureNonEmpty(test, nameof(test));

        int trainFeatureCount = ModelGuards.EnsureConsistentFeatureLength(train);
        int testFeatureCount = ModelGuards.EnsureConsistentFeatureLength(test);
        if (trainFeatureCount != testFeatureCount)
            throw new ArgumentException("Train and test feature lengths must match.");

        if (classifierFactory is null) throw new ArgumentNullException(nameof(classifierFactory));

        var model = classifierFactory();
        model.Fit(train);

        var yTrue = new string[test.Count];
        var yPred = new string[test.Count];

        for (int i = 0; i < test.Count; i++)
        {
            yTrue[i] = test[i].Label;
            yPred[i] = model.Predict(test[i]);
        }

        var report = Metrics.Compute(yTrue, yPred);
        return new TrainTestResult(yTrue, yPred, report);
    }
}
