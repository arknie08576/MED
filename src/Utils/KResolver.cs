namespace MED.Utils;

public static class KResolver
{
    /// <summary>
    /// Accepts:
    /// - "1", "3", "5" ...
    /// - "log2n" (rounded to nearest int, min 1)
    /// - "log2"  (alias)
    /// </summary>
    public static int Resolve(string kArg, int n)
    {
        if (string.IsNullOrWhiteSpace(kArg)) return 3;

        var s = kArg.Trim().ToLowerInvariant();

        if (s == "log2n" || s == "log2")
        {
            if (n <= 1) return 1;
            var v = Math.Log2(n);
            var k = (int)Math.Round(v);
            return Math.Max(1, k);
        }

        if (int.TryParse(s, out var kInt))
            return Math.Max(1, kInt);

        // fallback
        return 3;
    }
}
