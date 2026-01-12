using MED.Core;
using MED.Distance;

namespace MED.Algorithms;

public sealed class KPlusNnClassifier
{
    public sealed record Neighbor(int TrainIndex, int Id, string Label, double Distance);

    private readonly int _k;

    public KPlusNnClassifier(int k)
    {
        _k = Math.Max(1, k);
    }

    public (string predicted, List<Neighbor> neighbors) Predict(
        Dataset train,
        Record test,
        DistanceContext ctx)
    {
        var dist = new MixedDistance(ctx);

        var list = new List<Neighbor>(train.Count);

        for (int i = 0; i < train.Count; i++)
        {
            var tr = train.Records[i];
            var d = dist.Dist(test, tr);
            list.Add(new Neighbor(i, tr.Id, tr.Label, d));
        }

        // Deterministic sorting: distance, then Id, then TrainIndex
        list.Sort((a, b) =>
        {
            int c = a.Distance.CompareTo(b.Distance);
            if (c != 0) return c;

            c = a.Id.CompareTo(b.Id);
            if (c != 0) return c;

            return a.TrainIndex.CompareTo(b.TrainIndex);
        });

        // Take k nearest
        var knn = list.Take(_k).ToList();

        // majority vote (deterministic tie-break: class name)
        var pred = knn
            .GroupBy(n => n.Label, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .First().Key;

        return (pred, knn);
    }
}
