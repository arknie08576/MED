using MED.Distance;

namespace MED.Utils;

public static class OutputNameBuilder
{
    public static string DatasetTagFromPath(string dataPath)
    {
        var name = Path.GetFileName(dataPath);
        // nursery.data -> nursery
        if (name.EndsWith(".data", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];
        else
            name = Path.GetFileNameWithoutExtension(name);

        return Sanitize(name);
    }

    public static string BuildTag(
        string alg,
        string datasetTag,
        string kTag,
        DistanceMode mode,
        NominalMetric nom,
        MissingDistanceMode missing)
    {
        var modeTag = mode == DistanceMode.Global ? "g" : "l";
        var nomTag = nom == NominalMetric.SvdmPrime ? "svdmprime" : "svdm";
        var missTag = missing == MissingDistanceMode.Variant2 ? "v2" : "v1";

        return $"{alg}_{datasetTag}_k{kTag}_{modeTag}_{nomTag}_{missTag}";
    }

    public static string OutName(string outDir, string tag) => Path.Combine(outDir, $"OUT_{tag}.csv");
    public static string StatName(string outDir, string tag) => Path.Combine(outDir, $"STAT_{tag}.txt");
    public static string KnnName(string outDir, string tag) => Path.Combine(outDir, $"kNN_{tag}.txt");

    private static string Sanitize(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        return new string(s.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray());
    }
}
