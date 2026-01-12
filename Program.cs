using MED.Core;
using MED.Distance;
using MED.Evaluation;
using MED.Output;
using MED.Preprocessing;
using MED.Algorithms;
using MED.Metrics;
using MED.Utils;

static string GetArg(string[] args, string name, string def)
{
    var idx = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (idx < 0 || idx + 1 >= args.Length) return def;
    return args[idx + 1];
}

static bool HasFlag(string[] args, string name)
    => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

static void PrintHelp()
{
    Console.WriteLine("""
MED (POLISH C: --all)

Wymagane:
  --data <path>

Opcje:
  --alg knn|ria|riona        (domyślnie knn; przy --all traktowane jako "bazowe" ustawienia)
  --k <int|log2n>            (domyślnie 3)
  --mode g|l                 (domyślnie g)
  --nomdist svdm|svdmprime   (domyślnie svdm)
  --missing v1|v2            (domyślnie v1)
  --no-impute
  --no-header
  --sep <char>               (domyślnie ,)

Wyjścia:
  --outdir <dir>             (zalecane; auto tworzy OUT_/STAT_/kNN_ nazwy)
  --out <path>               (ręcznie; ma pierwszeństwo nad --outdir)
  --stat <path>              (ręcznie; ma pierwszeństwo nad --outdir)
  --knn-out <path>           (ręcznie; ma pierwszeństwo nad --outdir)

Tryb zbiorczy:
  --all                      generuje:
                               kNN: k=1,3,log2n -> pliki kNN_*
                               RIONA: k=--k -> OUT_* + STAT_*
  --all-ria                  tylko razem z --all: generuje jeszcze RIA (wolne)

Przykłady:
  dotnet run -- --all --data data/nursery.data --no-header --k log2n --mode l --outdir out
  dotnet run -- --all --all-ria --data data/nursery.data --no-header --k 3 --mode g --outdir out
""");
}

var argsArr = args;

if (HasFlag(argsArr, "--help") || HasFlag(argsArr, "-h"))
{
    PrintHelp();
    return;
}

var dataPath = GetArg(argsArr, "--data", "");
if (string.IsNullOrWhiteSpace(dataPath))
{
    Console.WriteLine("Brak --data. Użyj --help.");
    return;
}

var runAll = HasFlag(argsArr, "--all");
var runAllRia = HasFlag(argsArr, "--all-ria");

var alg = GetArg(argsArr, "--alg", "knn").ToLowerInvariant();
var kArg = GetArg(argsArr, "--k", "3");

var modeStr = GetArg(argsArr, "--mode", "g").ToLowerInvariant();
var mode = modeStr == "l" ? DistanceMode.Local : DistanceMode.Global;

var nomStr = GetArg(argsArr, "--nomdist", "svdm").ToLowerInvariant();
var nom = nomStr == "svdmprime" ? NominalMetric.SvdmPrime : NominalMetric.Svdm;

var missStr = GetArg(argsArr, "--missing", "v1").ToLowerInvariant();
var missing = missStr == "v2" ? MissingDistanceMode.Variant2 : MissingDistanceMode.Variant1;

var sepStr = GetArg(argsArr, "--sep", ",");
char sep = string.IsNullOrEmpty(sepStr) ? ',' : sepStr[0];

bool hasHeader = !HasFlag(argsArr, "--no-header");

// outputs (manual override)
var outDir = GetArg(argsArr, "--outdir", "");
var outPath = GetArg(argsArr, "--out", "");
var statPath = GetArg(argsArr, "--stat", "");
var knnOut = GetArg(argsArr, "--knn-out", "");

// --- LOAD
var tLoad0 = DateTime.UtcNow;
var loader = new CsvMixedLoader();
var ds = loader.Load(dataPath, sep: sep, hasHeader: hasHeader);
var tLoad1 = DateTime.UtcNow;

// resolve k after knowing n
int k = KResolver.Resolve(kArg, ds.Count);
string kTag = (kArg.Trim().Equals("log2n", StringComparison.OrdinalIgnoreCase) || kArg.Trim().Equals("log2", StringComparison.OrdinalIgnoreCase))
    ? "log2n"
    : k.ToString();

// auto file naming for normal (non --all) flow
if (!string.IsNullOrWhiteSpace(outDir))
{
    Directory.CreateDirectory(outDir);

    var datasetTag = OutputNameBuilder.DatasetTagFromPath(dataPath);
    var tag = OutputNameBuilder.BuildTag(alg, datasetTag, kTag, mode, nom, missing);

    if (string.IsNullOrWhiteSpace(outPath) && (alg == "ria" || alg == "riona"))
        outPath = OutputNameBuilder.OutName(outDir, tag);

    if (string.IsNullOrWhiteSpace(statPath) && (alg == "ria" || alg == "riona"))
        statPath = OutputNameBuilder.StatName(outDir, tag);

    if (string.IsNullOrWhiteSpace(knnOut) && alg == "knn")
        knnOut = OutputNameBuilder.KnnName(outDir, tag);
}

// --- IMPUTE
var tImp0 = DateTime.UtcNow;
if (!HasFlag(argsArr, "--no-impute"))
{
    ds = new ClassConditionalImputer().Impute(ds);
}
var tImp1 = DateTime.UtcNow;

Console.WriteLine($"Loaded: n={ds.Count}, features={ds.FeatureCount}");
Console.WriteLine($"Mode={mode}, Nominal={nom}, Missing={missing}");
Console.WriteLine($"k={k} (kArg={kArg})");

// ======================
// POLISH C: --all mode
// ======================
if (runAll)
{
    if (string.IsNullOrWhiteSpace(outDir))
    {
        Console.WriteLine("Dla --all musisz podać --outdir <dir>.");
        return;
    }

    var datasetTag = OutputNameBuilder.DatasetTagFromPath(dataPath);

    // ---- 1) kNN for k=1,3,log2n
    int kLog2 = KResolver.Resolve("log2n", ds.Count);
    foreach (var kk in new[] { 1, 3, kLog2 })
    {
        string kkTag = (kk == kLog2) ? "log2n" : kk.ToString();
        var tag = OutputNameBuilder.BuildTag("knn", datasetTag, kkTag, mode, nom, missing);
        var knnPath = OutputNameBuilder.KnnName(outDir, tag);

        var loo = new LeaveOneOutKnn();
        var tCl0 = DateTime.UtcNow;
        var result = loo.Run(ds, kk, mode, nom, missing);
        var tCl1 = DateTime.UtcNow;

        KnnWriter.Write(knnPath, result.NeighborsPerTest.Select(x => (x.Id, x.Neighbors)));

        Console.WriteLine($"[ALL] kNN k={kkTag}: acc={result.Accuracy:F4}, time={(tCl1 - tCl0).TotalMilliseconds:F0} ms -> {knnPath}");
    }

    // ---- 2) RIONA for requested k (kArg)
    {
        var tag = OutputNameBuilder.BuildTag("riona", datasetTag, kTag, mode, nom, missing);
        var outP = OutputNameBuilder.OutName(outDir, tag);
        var statP = OutputNameBuilder.StatName(outDir, tag);

        Console.WriteLine($"[ALL] RIONA k={kTag} generating...");

        var riona = new RionaClassifier(k);

        var preds = new RionaClassifier.Prediction[ds.Count];
        var pairsC = new List<(string True, string Pred)>(ds.Count);
        var pairsNC = new List<(string True, string Pred)>(ds.Count);

        var tCl0 = DateTime.UtcNow;
        for (int i = 0; i < ds.Count; i++)
        {
            preds[i] = riona.PredictLOO(ds, i, mode, nom, missing);
            pairsC.Add((preds[i].TrueLabel, preds[i].CId));
            pairsNC.Add((preds[i].TrueLabel, preds[i].NCId));
        }
        var tCl1 = DateTime.UtcNow;

        var tMet0 = DateTime.UtcNow;
        var mC = ClassificationMetrics.FromPairs(pairsC);
        var mNC = ClassificationMetrics.FromPairs(pairsNC);
        var tMet1 = DateTime.UtcNow;

        var tW0 = DateTime.UtcNow;
        OutWriter.WriteRionaOut(outP, ds, preds);
        var tW1 = DateTime.UtcNow;

        var meta = new Dictionary<string, string>
        {
            ["ALG"] = "RIONA",
            ["DATA"] = dataPath,
            ["N"] = ds.Count.ToString(),
            ["FEATURES"] = ds.FeatureCount.ToString(),
            ["MODE"] = mode.ToString(),
            ["NOMINAL_METRIC"] = nom.ToString(),
            ["MISSING_MODE"] = missing.ToString(),
            ["K"] = kTag
        };

        StatWriter.Write(statP, "STAT_RIONA", meta, ds, mode, nom, missing,
            tLoad1 - tLoad0,
            tImp1 - tImp0,
            TimeSpan.Zero,
            tCl1 - tCl0,
            tMet1 - tMet0,
            tW1 - tW0,
            mC, mNC);

        Console.WriteLine($"[ALL] RIONA: accC={mC.Accuracy:F4}, accNC={mNC.Accuracy:F4}, time={(tCl1 - tCl0).TotalMilliseconds:F0} ms");
        Console.WriteLine($"[ALL] Saved: {outP}");
        Console.WriteLine($"[ALL] Saved: {statP}");
    }

    // ---- 3) Optional RIA
    if (runAllRia)
    {
        var tag = OutputNameBuilder.BuildTag("ria", datasetTag, kTag, mode, nom, missing);
        var outP = OutputNameBuilder.OutName(outDir, tag);
        var statP = OutputNameBuilder.StatName(outDir, tag);

        Console.WriteLine($"[ALL] RIA generating (this is slow)...");

        var ria = new RiaClassifier();

        var preds = new RiaClassifier.Prediction[ds.Count];
        var pairsC = new List<(string True, string Pred)>(ds.Count);
        var pairsNC = new List<(string True, string Pred)>(ds.Count);

        var tCl0 = DateTime.UtcNow;
        for (int i = 0; i < ds.Count; i++)
        {
            preds[i] = ria.PredictLOO(ds, i, mode, nom, missing);
            pairsC.Add((preds[i].TrueLabel, preds[i].CId));
            pairsNC.Add((preds[i].TrueLabel, preds[i].NCId));
        }
        var tCl1 = DateTime.UtcNow;

        var tMet0 = DateTime.UtcNow;
        var mC = ClassificationMetrics.FromPairs(pairsC);
        var mNC = ClassificationMetrics.FromPairs(pairsNC);
        var tMet1 = DateTime.UtcNow;

        var tW0 = DateTime.UtcNow;
        OutWriter.WriteRiaOut(outP, ds, preds);
        var tW1 = DateTime.UtcNow;

        var meta = new Dictionary<string, string>
        {
            ["ALG"] = "RIA",
            ["DATA"] = dataPath,
            ["N"] = ds.Count.ToString(),
            ["FEATURES"] = ds.FeatureCount.ToString(),
            ["MODE"] = mode.ToString(),
            ["NOMINAL_METRIC"] = nom.ToString(),
            ["MISSING_MODE"] = missing.ToString(),
            ["K"] = kTag
        };

        StatWriter.Write(statP, "STAT_RIA", meta, ds, mode, nom, missing,
            tLoad1 - tLoad0,
            tImp1 - tImp0,
            TimeSpan.Zero,
            tCl1 - tCl0,
            tMet1 - tMet0,
            tW1 - tW0,
            mC, mNC);

        Console.WriteLine($"[ALL] RIA: accC={mC.Accuracy:F4}, accNC={mNC.Accuracy:F4}, time={(tCl1 - tCl0).TotalMilliseconds:F0} ms");
        Console.WriteLine($"[ALL] Saved: {outP}");
        Console.WriteLine($"[ALL] Saved: {statP}");
    }

    return;
}

// ======================
// Normal mode: knn/ria/riona
// ======================
if (alg == "knn")
{
    var loo = new LeaveOneOutKnn();

    var tCl0 = DateTime.UtcNow;
    var result = loo.Run(ds, k, mode, nom, missing);
    var tCl1 = DateTime.UtcNow;

    Console.WriteLine($"Accuracy: {result.Accuracy:F4}");
    Console.WriteLine($"Time: {(tCl1 - tCl0).TotalMilliseconds:F0} ms");

    if (!string.IsNullOrWhiteSpace(knnOut))
    {
        var tW0 = DateTime.UtcNow;
        KnnWriter.Write(knnOut, result.NeighborsPerTest.Select(x => (x.Id, x.Neighbors)));
        var tW1 = DateTime.UtcNow;
        Console.WriteLine($"Saved kNN file: {knnOut} (write {(tW1 - tW0).TotalMilliseconds:F0} ms)");
    }
}
else if (alg == "ria")
{
    if (string.IsNullOrWhiteSpace(outPath))
    {
        Console.WriteLine("Dla --alg ria podaj --out lub --outdir.");
        return;
    }

    var ria = new RiaClassifier();

    var preds = new RiaClassifier.Prediction[ds.Count];
    var pairsC = new List<(string True, string Pred)>(ds.Count);
    var pairsNC = new List<(string True, string Pred)>(ds.Count);

    var tCl0 = DateTime.UtcNow;

    for (int i = 0; i < ds.Count; i++)
    {
        preds[i] = ria.PredictLOO(ds, i, mode, nom, missing);
        pairsC.Add((preds[i].TrueLabel, preds[i].CId));
        pairsNC.Add((preds[i].TrueLabel, preds[i].NCId));
        if ((i + 1) % 1000 == 0) Console.WriteLine($"RIA progress: {i + 1}/{ds.Count}");
    }

    var tCl1 = DateTime.UtcNow;

    var tMet0 = DateTime.UtcNow;
    var mC = ClassificationMetrics.FromPairs(pairsC);
    var mNC = ClassificationMetrics.FromPairs(pairsNC);
    var tMet1 = DateTime.UtcNow;

    Console.WriteLine($"RIA Accuracy (CId):  {mC.Accuracy:F4}");
    Console.WriteLine($"RIA Accuracy (NCId): {mNC.Accuracy:F4}");
    Console.WriteLine($"Time: {(tCl1 - tCl0).TotalMilliseconds:F0} ms");

    var tW0 = DateTime.UtcNow;
    OutWriter.WriteRiaOut(outPath, ds, preds);
    var tW1 = DateTime.UtcNow;
    Console.WriteLine($"Saved OUT file: {outPath}");

    if (!string.IsNullOrWhiteSpace(statPath))
    {
        var meta = new Dictionary<string, string>
        {
            ["ALG"] = "RIA",
            ["DATA"] = dataPath,
            ["N"] = ds.Count.ToString(),
            ["FEATURES"] = ds.FeatureCount.ToString(),
            ["MODE"] = mode.ToString(),
            ["NOMINAL_METRIC"] = nom.ToString(),
            ["MISSING_MODE"] = missing.ToString(),
            ["K"] = kTag
        };

        StatWriter.Write(statPath, "STAT_RIA", meta, ds, mode, nom, missing,
            tLoad1 - tLoad0,
            tImp1 - tImp0,
            TimeSpan.Zero,
            tCl1 - tCl0,
            tMet1 - tMet0,
            tW1 - tW0,
            mC, mNC);

        Console.WriteLine($"Saved STAT file: {statPath}");
    }
}
else if (alg == "riona")
{
    if (string.IsNullOrWhiteSpace(outPath))
    {
        Console.WriteLine("Dla --alg riona podaj --out lub --outdir.");
        return;
    }

    Console.WriteLine($"k={k} (neighborhood size)");
    var riona = new RionaClassifier(k);

    var preds = new RionaClassifier.Prediction[ds.Count];
    var pairsC = new List<(string True, string Pred)>(ds.Count);
    var pairsNC = new List<(string True, string Pred)>(ds.Count);

    var tCl0 = DateTime.UtcNow;

    for (int i = 0; i < ds.Count; i++)
    {
        preds[i] = riona.PredictLOO(ds, i, mode, nom, missing);
        pairsC.Add((preds[i].TrueLabel, preds[i].CId));
        pairsNC.Add((preds[i].TrueLabel, preds[i].NCId));
        if ((i + 1) % 1000 == 0) Console.WriteLine($"RIONA progress: {i + 1}/{ds.Count}");
    }

    var tCl1 = DateTime.UtcNow;

    var tMet0 = DateTime.UtcNow;
    var mC = ClassificationMetrics.FromPairs(pairsC);
    var mNC = ClassificationMetrics.FromPairs(pairsNC);
    var tMet1 = DateTime.UtcNow;

    Console.WriteLine($"RIONA Accuracy (CId):  {mC.Accuracy:F4}");
    Console.WriteLine($"RIONA Accuracy (NCId): {mNC.Accuracy:F4}");
    Console.WriteLine($"Time: {(tCl1 - tCl0).TotalMilliseconds:F0} ms");

    var tW0 = DateTime.UtcNow;
    OutWriter.WriteRionaOut(outPath, ds, preds);
    var tW1 = DateTime.UtcNow;
    Console.WriteLine($"Saved OUT file: {outPath}");

    if (!string.IsNullOrWhiteSpace(statPath))
    {
        var meta = new Dictionary<string, string>
        {
            ["ALG"] = "RIONA",
            ["DATA"] = dataPath,
            ["N"] = ds.Count.ToString(),
            ["FEATURES"] = ds.FeatureCount.ToString(),
            ["MODE"] = mode.ToString(),
            ["NOMINAL_METRIC"] = nom.ToString(),
            ["MISSING_MODE"] = missing.ToString(),
            ["K"] = kTag
        };

        StatWriter.Write(statPath, "STAT_RIONA", meta, ds, mode, nom, missing,
            tLoad1 - tLoad0,
            tImp1 - tImp0,
            TimeSpan.Zero,
            tCl1 - tCl0,
            tMet1 - tMet0,
            tW1 - tW0,
            mC, mNC);

        Console.WriteLine($"Saved STAT file: {statPath}");
    }
}
else
{
    Console.WriteLine("Nieznany --alg. Użyj knn / ria / riona.");
}
