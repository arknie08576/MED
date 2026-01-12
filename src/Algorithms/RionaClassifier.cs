using MED.Core;
using MED.Distance;
using MED.Rules;

namespace MED.Algorithms;

public sealed class RionaClassifier
{
    public sealed record Prediction(
        string TrueLabel,
        string CId,
        string NCId,
        Dictionary<string, int> SupportCounts,
        Dictionary<string, double> NormalizedSupport);

    private readonly int _k;

    public RionaClassifier(int k)
    {
        _k = Math.Max(1, k);
    }

    public Prediction PredictLOO(Dataset full, int testIndex, DistanceMode mode, NominalMetric nom, MissingDistanceMode missing)
    {
        var tst = full.Records[testIndex];

        // Distance context for this LOO case
        var ctx = DistanceContext.Build(full, mode, nom, missing, leaveOutIndex: testIndex);
        var dist = new MixedDistance(ctx);

        // Build neighbor set S(tst,k) from training records (all except testIndex)
        var neigh = new List<(int idx, double d)>(full.Count - 1);
        for (int i = 0; i < full.Count; i++)
        {
            if (i == testIndex) continue;
            neigh.Add((i, dist.Dist(tst, full.Records[i])));
        }

        neigh.Sort((a, b) => a.d.CompareTo(b.d));
        var neighK = neigh.Take(_k).ToArray();

        // class sizes IN neighborhood (RIONA uses neighborhood both for support and consistency)
        var classSize = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (idx, _) in neighK)
        {
            var lab = full.Records[idx].Label;
            classSize.TryGetValue(lab, out var c);
            classSize[lab] = c + 1;
        }

        // support counts init for classes present in neighborhood
        var support = classSize.Keys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);

        // For each neighbor as trn: build ruletst(trn) and test consistency within neighborhood only
        foreach (var (trnIdx, dTrn) in neighK)
        {
            var trn = full.Records[trnIdx];
            var rule = new LocalRule(tst, trn, ctx);

            if (IsConsistentInNeighborhood(rule, full, dTrn, neighK))
                support[trn.Label] = support[trn.Label] + 1;
        }

        // CId: argmax support[v]
        string cid = support
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .First().Key;

        // NCId: argmax support[v] / |Class(v)|  (class size computed in neighborhood)
        var norm = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var v in support.Keys)
        {
            int sz = classSize[v];
            norm[v] = sz > 0 ? (double)support[v] / sz : 0.0;
        }

        string ncid = norm
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .First().Key;

        return new Prediction(tst.Label, cid, ncid, support, norm);
    }

    private static bool IsConsistentInNeighborhood(LocalRule rule, Dataset full, double dTrn, (int idx, double d)[] neighK)
    {
        // In RIONA, verify set is restricted to neighborhood. We can also apply the <= dTrn bound.
        foreach (var (idx, d) in neighK)
        {
            if (d > dTrn) continue;

            var x = full.Records[idx];

            if (!string.Equals(x.Label, rule.Decision, StringComparison.Ordinal) && rule.Satisfies(x))
                return false;
        }

        return true;
    }
}
