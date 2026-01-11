namespace MED.Evaluation;

public sealed record ClassificationReport(
    double Accuracy,
    double MacroPrecision,
    double MacroRecall,
    double MacroF1,
    IReadOnlyDictionary<string, PerClassMetrics> PerClass);

public sealed record PerClassMetrics(
    int Support,
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    double Precision,
    double Recall,
    double F1);

public static class Metrics
{
    public static ClassificationReport Compute(IReadOnlyList<string> yTrue, IReadOnlyList<string> yPred)
    {
        if (yTrue is null) throw new ArgumentNullException(nameof(yTrue));
        if (yPred is null) throw new ArgumentNullException(nameof(yPred));
        if (yTrue.Count != yPred.Count) throw new ArgumentException("yTrue and yPred must have the same length.");
        if (yTrue.Count == 0) return new ClassificationReport(0, 0, 0, 0, new Dictionary<string, PerClassMetrics>());

        var labels = new SortedSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < yTrue.Count; i++)
        {
            labels.Add(yTrue[i]);
            labels.Add(yPred[i]);
        }

        var tp = labels.ToDictionary(l => l, _ => 0, StringComparer.Ordinal);
        var fp = labels.ToDictionary(l => l, _ => 0, StringComparer.Ordinal);
        var fn = labels.ToDictionary(l => l, _ => 0, StringComparer.Ordinal);
        var support = labels.ToDictionary(l => l, _ => 0, StringComparer.Ordinal);

        int correct = 0;
        for (int i = 0; i < yTrue.Count; i++)
        {
            string t = yTrue[i];
            string p = yPred[i];

            if (t == p) correct++;

            support[t]++;
            if (t == p) tp[t]++;
            else
            {
                fp[p]++;
                fn[t]++;
            }
        }

        var perClass = new Dictionary<string, PerClassMetrics>(StringComparer.Ordinal);

        double sumP = 0, sumR = 0, sumF1 = 0;
        foreach (var label in labels)
        {
            int tpi = tp[label];
            int fpi = fp[label];
            int fni = fn[label];
            int sup = support[label];

            double precision = (tpi + fpi) == 0 ? 0 : (double)tpi / (tpi + fpi);
            double recall = (tpi + fni) == 0 ? 0 : (double)tpi / (tpi + fni);
            double f1 = (precision + recall) == 0 ? 0 : 2 * precision * recall / (precision + recall);

            perClass[label] = new PerClassMetrics(sup, tpi, fpi, fni, precision, recall, f1);
            sumP += precision;
            sumR += recall;
            sumF1 += f1;
        }

        double accuracy = (double)correct / yTrue.Count;
        int labelCount = labels.Count == 0 ? 1 : labels.Count;

        return new ClassificationReport(
            Accuracy: accuracy,
            MacroPrecision: sumP / labelCount,
            MacroRecall: sumR / labelCount,
            MacroF1: sumF1 / labelCount,
            PerClass: perClass);
    }
}
