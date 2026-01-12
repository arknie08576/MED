using MED.Core;

namespace MED.Distance;

public sealed class MixedDistance
{
    private readonly DistanceContext _ctx;

    public MixedDistance(DistanceContext ctx) => _ctx = ctx;

    public double Dist(Record a, Record b)
    {
        double sum = 0.0;

        for (int j = 0; j < _ctx.Dataset.FeatureCount; j++)
        {
            var attr = _ctx.Dataset.Attributes[j];
            if (attr.Type == AttributeType.Numeric)
            {
                var x = (double?)a.X[j];
                var y = (double?)b.X[j];

                if (!x.HasValue || !y.HasValue)
                {
                    sum += 1.0; // per requirements
                }
                else
                {
                    sum += Math.Abs(x.Value - y.Value) / attr.Range;
                }
            }
            else
            {
                var x = a.X[j] as string;
                var y = b.X[j] as string;
                sum += _ctx.NominalDistance(j, x, y);
            }
        }

        return sum;
    }
}
