using System.Text;

namespace MED.Metrics;

public sealed class ClassificationMetrics
{
    public string[] Classes { get; }
    public int[,] Confusion { get; } // [true, pred]
    public int Total { get; }
    public int Correct { get; }
    public double Accuracy => Total == 0 ? 0.0 : (double)Correct / Total;

    public sealed record PerClass(
        string Class,
        int Support,          // true count
        int TP,
        int FP,
        int FN,
        double Precision,
        double Recall,
        double F1);

    public IReadOnlyList<PerClass> PerClasses { get; }

    // Balanced = macro average
    public double BalancedPrecision { get; }
    public double BalancedRecall { get; }
    public double BalancedF1 { get; }

    private ClassificationMetrics(
        string[] classes,
        int[,] confusion,
        int total,
        int correct,
        IReadOnlyList<PerClass> perClasses,
        double bp, double br, double bf1)
    {
        Classes = classes;
        Confusion = confusion;
        Total = total;
        Correct = correct;
        PerClasses = perClasses;
        BalancedPrecision = bp;
        BalancedRecall = br;
        BalancedF1 = bf1;
    }

    public static ClassificationMetrics FromPairs(IEnumerable<(string True, string Pred)> pairs)
    {
        var list = pairs.ToList();
        var classes = list.Select(x => x.True)
            .Concat(list.Select(x => x.Pred))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        var idx = classes.Select((c, i) => (c, i)).ToDictionary(t => t.c, t => t.i, StringComparer.Ordinal);

        var conf = new int[classes.Length, classes.Length];
        int correct = 0;

        foreach (var (t, p) in list)
        {
            int ti = idx[t];
            int pi = idx[p];
            conf[ti, pi]++;
            if (ti == pi) correct++;
        }

        var per = new List<PerClass>(classes.Length);

        double sumP = 0, sumR = 0, sumF = 0;
        int n = classes.Length;

        for (int c = 0; c < n; c++)
        {
            int tp = conf[c, c];
            int support = 0;
            int predCount = 0;

            for (int j = 0; j < n; j++)
            {
                support += conf[c, j];  // row sum
                predCount += conf[j, c]; // col sum
            }

            int fp = predCount - tp;
            int fn = support - tp;

            double precision = (tp + fp) == 0 ? 0.0 : (double)tp / (tp + fp);
            double recall = (tp + fn) == 0 ? 0.0 : (double)tp / (tp + fn);
            double f1 = (precision + recall) == 0 ? 0.0 : (2 * precision * recall) / (precision + recall);

            per.Add(new PerClass(classes[c], support, tp, fp, fn, precision, recall, f1));

            sumP += precision;
            sumR += recall;
            sumF += f1;
        }

        return new ClassificationMetrics(
            classes,
            conf,
            total: list.Count,
            correct: correct,
            perClasses: per,
            bp: sumP / n,
            br: sumR / n,
            bf1: sumF / n
        );
    }

    public string ConfusionToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine("Confusion matrix (rows=true, cols=pred):");
        sb.Append("true\\pred");
        foreach (var c in Classes) sb.Append('\t').Append(c);
        sb.AppendLine();

        for (int i = 0; i < Classes.Length; i++)
        {
            sb.Append(Classes[i]);
            for (int j = 0; j < Classes.Length; j++)
                sb.Append('\t').Append(Confusion[i, j]);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
