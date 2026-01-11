using MED;
using MED.Algorithms;
using MED.Evaluation;
using MED.Output;

static int GetIntArg(string[] args, string name, int defaultValue)
{
    int idx = Array.IndexOf(args, name);
    if (idx < 0 || idx + 1 >= args.Length) return defaultValue;
    return int.TryParse(args[idx + 1], out var v) ? v : defaultValue;
}

static string GetStringArg(string[] args, string name, string defaultValue)
{
    int idx = Array.IndexOf(args, name);
    if (idx < 0 || idx + 1 >= args.Length) return defaultValue;
    return args[idx + 1];
}

static int GetIntArgAny(string[] args, int defaultValue, params string[] names)
{
    foreach (var name in names)
    {
        int idx = Array.IndexOf(args, name);
        if (idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var v))
            return v;
    }
    return defaultValue;
}

static string GetStringArgAny(string[] args, string defaultValue, params string[] names)
{
    foreach (var name in names)
    {
        int idx = Array.IndexOf(args, name);
        if (idx >= 0 && idx + 1 < args.Length)
            return args[idx + 1];
    }
    return defaultValue;
}

static bool HasFlag(string[] args, string name) => Array.IndexOf(args, name) >= 0;

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- --input <path> [--test <path>] [--schema <path>] [--mode loo|g]");
    Console.WriteLine("               [--algo knn|ria|riona] [--k <int>] [--nomMetric mismatch|svdm] [--standardize]");
    Console.WriteLine("               [--outDir <dir>]");
    Console.WriteLine();
    Console.WriteLine("Defaults:");
    Console.WriteLine("  --algo knn");
    Console.WriteLine("  --k 3");
    Console.WriteLine("  --mode loo");
    Console.WriteLine("  --outDir out");
}

var cliArgs = args;

if (cliArgs.Length == 0 || HasFlag(cliArgs, "--help") || HasFlag(cliArgs, "-h"))
{
    PrintUsage();
    return;
}

string inputPath = GetStringArgAny(cliArgs, defaultValue: string.Empty, "--data", "--input", "--train");
if (string.IsNullOrWhiteSpace(inputPath))
{
    Console.Error.WriteLine("Missing required argument: --input <path>");
    PrintUsage();
    Environment.ExitCode = 2;
    return;
}

string algo = GetStringArg(cliArgs, "--algo", "knn").Trim().ToLowerInvariant();
int k = GetIntArgAny(cliArgs, defaultValue: 3, "--k");
string mode = GetStringArgAny(cliArgs, defaultValue: "loo", "--mode").Trim().ToLowerInvariant();
string nomMetric = GetStringArgAny(cliArgs, defaultValue: "", "--nomMetric").Trim().ToLowerInvariant();
bool standardize = HasFlag(cliArgs, "--standardize");
string outDir = GetStringArgAny(cliArgs, defaultValue: "out", "--out", "--outDir");
string schemaPath = GetStringArgAny(cliArgs, defaultValue: "", "--schema");
string testPath = GetStringArgAny(cliArgs, defaultValue: "", "--test");

if (!string.IsNullOrWhiteSpace(schemaPath) && !File.Exists(schemaPath))
    Console.WriteLine($"Warning: schema file not found: {schemaPath} (ignored)");

bool isNominal = string.Equals(Path.GetExtension(inputPath), ".data", StringComparison.OrdinalIgnoreCase)
    || !string.IsNullOrWhiteSpace(nomMetric)
    || (!string.IsNullOrWhiteSpace(schemaPath) && File.Exists(schemaPath));

IReadOnlyList<LabeledVector> train = isNominal
    ? DataLoader.LoadDelimitedNominal(inputPath, hasHeader: false, separator: ',')
    : DataLoader.LoadCsv(inputPath, hasHeader: true);

if (!isNominal && standardize)
    train = Preprocessing.Standardize(train);

Func<IDistanceMetric> distanceFactory = () =>
{
    if (!isNominal) return EuclideanDistance.Instance;

    return nomMetric switch
    {
        "svdm" => new SvdmDistance(missingPenalty: 1.0),
        "mismatch" or "" => NominalMismatchDistance.Instance,
        _ => throw new ArgumentException($"Unknown --nomMetric value '{nomMetric}'. Use: mismatch, svdm.")
    };
};

if (isNominal && algo == "ria")
    throw new ArgumentException("Algorithm 'ria' (centroid) is intended for numeric features; use knn/riona for nominal data.");

Func<IClassifier> factory = algo switch
{
    "knn" => () => new KnnClassifier(k, distance: distanceFactory()),
    "riona" => () => new RionaClassifier(k, distance: distanceFactory()),
    "ria" => () => new RiaClassifier(distance: distanceFactory()),
    _ => throw new ArgumentException($"Unknown --algo value '{algo}'. Use: knn, ria, riona.")
};

string prefixBase = algo == "ria" ? algo : $"{algo}_k{k}";
var prefix = $"{prefixBase}_{mode}";

string reportPath;
string predsPath;

if (mode == "g")
{
    if (string.IsNullOrWhiteSpace(testPath))
    {
        var dir = Path.GetDirectoryName(inputPath) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var candidate = Path.Combine(dir, stem + "_test" + ext);
        if (File.Exists(candidate)) testPath = candidate;
    }

    if (string.IsNullOrWhiteSpace(testPath))
        throw new ArgumentException("Mode 'g' requires --test <path> (or a sibling '<input>_test.*' file).");

    IReadOnlyList<LabeledVector> test = isNominal
        ? DataLoader.LoadDelimitedNominal(testPath, hasHeader: false, separator: ',')
        : DataLoader.LoadCsv(testPath, hasHeader: true);

    if (!isNominal && standardize)
    {
        // Simple standardization applied independently; for proper ML use, compute params on train and apply to test.
        train = Preprocessing.Standardize(train);
        test = Preprocessing.Standardize(test);
    }

    TrainTestResult? result = null;
    var elapsed = Timing.Measure(() => { result = TrainTestRunner.Run(train, test, factory); });

    reportPath = Naming.MakeOutputPath(outDir, prefix + "_report", "json");
    predsPath = Naming.MakeOutputPath(outDir, prefix + "_predictions", "csv");

    Writers.WriteReportJson(reportPath, result!.Report);
    Writers.WritePredictionsCsv(predsPath, result.TrueLabels, result.PredictedLabels);

    Console.WriteLine($"Done. Accuracy={result.Report.Accuracy:P2}, time={elapsed.TotalMilliseconds:F0}ms");
}
else if (mode == "loo")
{
    LeaveOneOutResult? result = null;
    var elapsed = Timing.Measure(() => { result = LeaveOneOutRunner.RunDetailed(train, factory); });

    reportPath = Naming.MakeOutputPath(outDir, prefix + "_report", "json");
    predsPath = Naming.MakeOutputPath(outDir, prefix + "_predictions", "csv");

    Writers.WriteReportJson(reportPath, result!.Report);
    Writers.WritePredictionsCsv(predsPath, result.TrueLabels, result.PredictedLabels);

    Console.WriteLine($"Done. Accuracy={result.Report.Accuracy:P2}, time={elapsed.TotalMilliseconds:F0}ms");
}
else
{
    throw new ArgumentException($"Unknown --mode value '{mode}'. Use: loo, g.");
}

Console.WriteLine($"Report: {reportPath}");
Console.WriteLine($"Preds:  {predsPath}");
