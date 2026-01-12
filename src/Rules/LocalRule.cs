using MED.Core;
using MED.Distance;

namespace MED.Rules;

public sealed class LocalRule
{
    private readonly Record _tst;
    private readonly DistanceContext _ctx;

    // numeric: interval [min, max]
    private readonly (double lo, double hi)?[] _numInterval;

    // nominal: radius r = dist(tstVal, trnVal)
    private readonly double?[] _nomRadius;

    public string Decision { get; }

    public LocalRule(Record tst, Record trn, DistanceContext ctx)
    {
        _tst = tst;
        _ctx = ctx;
        Decision = trn.Label;

        int m = ctx.Dataset.FeatureCount;
        _numInterval = new (double lo, double hi)?[m];
        _nomRadius = new double?[m];

        for (int j = 0; j < m; j++)
        {
            var attr = ctx.Dataset.Attributes[j];
            if (attr.Type == AttributeType.Numeric)
            {
                var a = (double?)tst.X[j];
                var b = (double?)trn.X[j];

                // po imputacji raczej zawsze są, ale zostawiamy bezpiecznie
                if (a.HasValue && b.HasValue)
                {
                    var lo = Math.Min(a.Value, b.Value);
                    var hi = Math.Max(a.Value, b.Value);
                    _numInterval[j] = (lo, hi);
                }
                else
                {
                    // jeśli brak - traktuj jako "star" (zawsze spełnione)
                    _numInterval[j] = null;
                }
            }
            else
            {
                var a = tst.X[j] as string;
                var b = trn.X[j] as string;

                if (a is null || b is null)
                {
                    _nomRadius[j] = double.PositiveInfinity; // "star"
                }
                else
                {
                    _nomRadius[j] = ctx.NominalDistance(j, a, b);
                }
            }
        }
    }

    public bool Satisfies(Record x)
    {
        int m = _ctx.Dataset.FeatureCount;

        for (int j = 0; j < m; j++)
        {
            var attr = _ctx.Dataset.Attributes[j];

            if (attr.Type == AttributeType.Numeric)
            {
                var interval = _numInterval[j];
                if (!interval.HasValue) continue; // star

                var v = (double?)x.X[j];
                if (!v.HasValue) return false;

                if (v.Value < interval.Value.lo || v.Value > interval.Value.hi)
                    return false;
            }
            else
            {
                var r = _nomRadius[j];
                if (!r.HasValue) continue;

                if (double.IsPositiveInfinity(r.Value)) continue; // star

                var a = _tst.X[j] as string;     // center
                var b = x.X[j] as string;

                if (a is null || b is null) return false;

                if (_ctx.NominalDistance(j, a, b) > r.Value)
                    return false;
            }
        }

        return true;
    }
}
