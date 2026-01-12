using System.Globalization;
using MED.Algorithms;

namespace MED.Output;

public static class KnnWriter
{
    /// <summary>
    /// Writes one line per test object:
    /// testId; neighId1:dist1; neighId2:dist2; ...; neighIdK:distK
    /// Distances written with invariant culture.
    /// </summary>
    public static void Write(string path, IEnumerable<(int TestId, List<KPlusNnClassifier.Neighbor> Neighbors)> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        using var sw = new StreamWriter(path);

        foreach (var (testId, neigh) in data)
        {
            sw.Write(testId.ToString(CultureInfo.InvariantCulture));

            foreach (var n in neigh)
            {
                sw.Write(';');
                sw.Write(n.Id.ToString(CultureInfo.InvariantCulture));
                sw.Write(':');
                sw.Write(n.Distance.ToString("G17", CultureInfo.InvariantCulture));
            }

            sw.WriteLine();
        }
    }
}
