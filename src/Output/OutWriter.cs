using System.Globalization;
using MED.Core;
using MED.Algorithms;

namespace MED.Output;

public static class OutWriter
{
    public static void WriteRiaOut(string path, Dataset ds, IReadOnlyList<RiaClassifier.Prediction> preds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var sw = new StreamWriter(path);

        // HEADER
        sw.Write("id");
        for (int j = 0; j < ds.FeatureCount; j++)
            sw.Write($",{ds.Attributes[j].Name}");
        sw.WriteLine(",RId,CId,NCId");

        for (int i = 0; i < ds.Count; i++)
        {
            var rec = ds.Records[i];
            var pr = preds[i];

            sw.Write(rec.Id.ToString(CultureInfo.InvariantCulture));

            // attributes
            for (int j = 0; j < ds.FeatureCount; j++)
            {
                sw.Write(',');
                var attr = ds.Attributes[j];

                if (attr.Type == AttributeType.Numeric)
                {
                    var v = (double?)rec.X[j];
                    sw.Write(v.HasValue ? v.Value.ToString("G17", CultureInfo.InvariantCulture) : "");
                }
                else
                {
                    sw.Write(rec.X[j]?.ToString() ?? "");
                }
            }

            // RId, CId, NCId
            sw.Write(',');
            sw.Write(pr.TrueLabel);
            sw.Write(',');
            sw.Write(pr.CId);
            sw.Write(',');
            sw.Write(pr.NCId);

            sw.WriteLine();
        }
    }

    public static void WriteRionaOut(string path, Dataset ds, IReadOnlyList<RionaClassifier.Prediction> preds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var sw = new StreamWriter(path);

        // HEADER
        sw.Write("id");
        for (int j = 0; j < ds.FeatureCount; j++)
            sw.Write($",{ds.Attributes[j].Name}");
        sw.WriteLine(",RId,CId,NCId");

        for (int i = 0; i < ds.Count; i++)
        {
            var rec = ds.Records[i];
            var pr = preds[i];

            sw.Write(rec.Id.ToString(CultureInfo.InvariantCulture));

            // attributes
            for (int j = 0; j < ds.FeatureCount; j++)
            {
                sw.Write(',');
                var attr = ds.Attributes[j];

                if (attr.Type == AttributeType.Numeric)
                {
                    var v = (double?)rec.X[j];
                    sw.Write(v.HasValue ? v.Value.ToString("G17", CultureInfo.InvariantCulture) : "");
                }
                else
                {
                    sw.Write(rec.X[j]?.ToString() ?? "");
                }
            }

            // RId, CId, NCId
            sw.Write(',');
            sw.Write(pr.TrueLabel);
            sw.Write(',');
            sw.Write(pr.CId);
            sw.Write(',');
            sw.Write(pr.NCId);

            sw.WriteLine();
        }
    }
}
