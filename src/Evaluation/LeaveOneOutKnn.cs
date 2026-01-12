using MED.Algorithms;
using MED.Core;
using MED.Distance;

namespace MED.Evaluation;

public sealed class LeaveOneOutKnn
{
    public sealed record ResultRow(int Id, string TrueLabel, string PredLabel);

    public sealed class RunResult
    {
        public List<ResultRow> Rows { get; } = new();
        public double Accuracy { get; set; }
        public List<(int Id, List<KPlusNnClassifier.Neighbor> Neighbors)> NeighborsPerTest { get; } = new();
    }

    public RunResult Run(
        Dataset ds,
        int k,
        DistanceMode mode,
        NominalMetric nominalMetric,
        MissingDistanceMode missingMode)
    {
        var clf = new KPlusNnClassifier(k);
        var res = new RunResult();

        int correct = 0;

        for (int i = 0; i < ds.Count; i++)
        {
            // Build train set without i
            var trainRecords = new List<Record>(ds.Count - 1);
            for (int t = 0; t < ds.Count; t++)
                if (t != i) trainRecords.Add(ds.Records[t]);

            var train = new Dataset(ds.Attributes, trainRecords);
            var test = ds.Records[i];

            // Build distance context
            var ctx = DistanceContext.Build(ds, mode, nominalMetric, missingMode, leaveOutIndex: i);

            // Predict using train
            var (pred, neigh) = clf.Predict(train, test, ctx);

            res.Rows.Add(new ResultRow(test.Id, test.Label, pred));
            res.NeighborsPerTest.Add((test.Id, neigh));

            if (string.Equals(test.Label, pred, StringComparison.Ordinal)) correct++;
        }

        res.Accuracy = (double)correct / ds.Count;
        return res;
    }
}
