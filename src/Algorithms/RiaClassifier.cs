using MED.Core;
using MED.Distance;
using MED.Rules;

namespace MED.Algorithms;

public sealed class RiaClassifier
{
    public sealed record Prediction(
        string TrueLabel,
        string CId,
        string NCId,
        Dictionary<string, int> SupportCounts,
        Dictionary<string, double> NormalizedSupport);

    public Prediction PredictLOO(Dataset full, int testIndex, DistanceMode mode, NominalMetric nom, MissingDistanceMode missing)
    {
        var tst = full.Records[testIndex];

        // train = all except testIndex
        var trainIdx = Enumerable.Range(0, full.Count).Where(i => i != testIndex).ToArray();

        // context (global/local)
        var ctx = DistanceContext.Build(full, mode, nom, missing, leaveOutIndex: testIndex);
        var dist = new MixedDistance(ctx);

        // class sizes (needed for NCId)
        var classSize = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var i in trainIdx)
        {
            var lab = full.Records[i].Label;
            classSize.TryGetValue(lab, out var c);
            classSize[lab] = c + 1;
        }

        // precompute distances tst->trn for bound-based consistency checking
        var distTo = new (int idx, double d)[trainIdx.Length];
        for (int k = 0; k < trainIdx.Length; k++)
        {
            int i = trainIdx[k];
            distTo[k] = (i, dist.Dist(tst, full.Records[i]));
        }

        // sort increasing so "verifySet = those with distance <= d_trn" is quick
        Array.Sort(distTo, (a, b) => a.d.CompareTo(b.d));

        var support = classSize.Keys.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);

        // For every trn in training set: build ruletst(trn) and test consistency
        foreach (var (trnIdx, dTrn) in distTo)
        {
            var trn = full.Records[trnIdx];
            var rule = new LocalRule(tst, trn, ctx);

            if (IsConsistent(rule, full, dTrn, distTo))
            {
                support[trn.Label] = support[trn.Label] + 1;
            }
        }

        // CId: argmax support[v]
        string cid = support
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .First().Key;

        // NCId: argmax support[v] / |Class(v)|
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

    // Prop 4.1: inconsistency can be caused only by examples not farther than trn from tst. :contentReference[oaicite:4]{index=4}
    private static bool IsConsistent(LocalRule rule, Dataset full, double dTrn, (int idx, double d)[] sortedByDist)
    {
        for (int i = 0; i < sortedByDist.Length; i++)
        {
            var (idx, d) = sortedByDist[i];
            if (d > dTrn) break;

            var x = full.Records[idx];

            if (!string.Equals(x.Label, rule.Decision, StringComparison.Ordinal) && rule.Satisfies(x))
                return false;
        }

        return true;
    }
}
