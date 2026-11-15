using System.Diagnostics;
using System.Net;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Implementations.ML;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Model;
using RiskInference.Benchmark;

// Chapter 7, Step 4: measure the inference service. This tool only measures.
//
//   inprocess                       service.Predict, both inference modes, no HTTP
//   http <url> --label <name> [--server-pid <pid>]
//                                   POST /risk/predict against a running API
//   compare <fileA> <fileB>         print the comparison table from two result files
//
// Options: --levels 1,10,50,100  --seconds 60  --warmup 5  --out <file>
var mode = args.Length > 0 ? args[0] : "inprocess";
var seconds = double.Parse(ArgValue("--seconds") ?? "60");
var warmup = double.Parse(ArgValue("--warmup") ?? "5");
var levels = (ArgValue("--levels") ?? "1,10,50,100").Split(',').Select(int.Parse).ToArray();
var label = ArgValue("--label") ?? "run";
var outFile = ArgValue("--out");
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

var resolver = new SolutionPathResolver();

if (mode == "compare") return Compare(args[1], args[2]);
if (mode == "verify") return await Verify.Run(args, resolver, ArgValue);

var records = new DatasetReader(resolver).Read("chapter-06/train-5000.csv")
    .Where((_, i) => i % 5 == 0).ToArray();   // 1,000 records
Console.WriteLine($"Mode: {mode}. {records.Length} records. {seconds}s per level, {warmup}s warm-up before each. " +
                  $"Levels: {string.Join(",", levels)}. Processors: {Environment.ProcessorCount}. " +
                  $"Server GC (this process): {GCSettings.IsServerGC}. Debug build: {IsDebugBuild()}.");
if (IsDebugBuild()) Console.WriteLine("WARNING: this is a Debug build. Run with -c Release for real numbers.");

var results = new Dictionary<string, object?>
{
    ["environment"] = new
    {
        mode,
        label,
        processors = Environment.ProcessorCount,
        os = RuntimeInformation.OSDescription,
        runtime = RuntimeInformation.FrameworkDescription,
        debugBuild = IsDebugBuild(),
        recordsUsed = records.Length,
        secondsPerLevel = seconds,
        warmupSeconds = warmup,
        measuredAtUtc = DateTime.UtcNow
    }
};

if (mode == "inprocess") RunInProcess();
else if (mode == "http") await RunHttp(args[1]);
else { Console.WriteLine("Unknown mode."); return 1; }

outFile ??= mode == "http" ? $"evaluations/risk-inference/step-4-http-{label}.json"
                           : "evaluations/risk-inference/step-4-inprocess.json";
var outPath = resolver.ResolveConfiguredPath(outFile);
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, JsonSerializer.Serialize(results, jsonOptions));
Console.WriteLine($"Wrote {outPath}");
return 0;

// ---------------------------------------------------------------------------

void RunInProcess()
{
    var baseOptions = LoadOptions();
    RiskModelService Create(InferenceMode m)
    {
        baseOptions.InferenceMode = m;
        return new RiskModelService(Options.Create(baseOptions), resolver);
    }

    var loadWatch = Stopwatch.StartNew();
    var perRequest = Create(InferenceMode.EnginePerRequest);
    loadWatch.Stop();
    results["modelLoadMs"] = Math.Round(loadWatch.Elapsed.TotalMilliseconds, 0);
    Console.WriteLine($"\nModel load (cold, includes JIT and hash check): {loadWatch.Elapsed.TotalMilliseconds:F0} ms");
    using var pooled = Create(InferenceMode.Pooled);

    // Both modes must give identical predictions.
    var identical = records.All(r => perRequest.Predict(r) == pooled.Predict(r));
    results["identicalPredictions"] = identical;
    Console.WriteLine($"Predictions identical in both modes for all {records.Length} records: {identical}");
    if (!identical) throw new InvalidOperationException("The two modes gave different predictions.");

    // Where does one call spend its time? One thread, per-request engine.
    var ml = new MLContext();
    var manifestPath = resolver.ResolveConfiguredPath(baseOptions.ManifestPath);
    var manifest = ModelManifest.Load(manifestPath);
    var model = new FinalModelStore(resolver).LoadModel(
        ml, Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.ModelFile), manifest.ModelSha256);
    using var sharedEngine = ml.Model.CreatePredictionEngine<TransactionRiskData, TransactionRiskPrediction>(model);
    Warm(() => { for (var i = 0; i < 3000; i++) sharedEngine.Predict(records[i % records.Length]); });
    Console.WriteLine("\nCost breakdown, one thread");
    PrintHeader();
    var breakdown = new[]
    {
        Measurement.RunSync("create+dispose engine only", 1, Math.Min(seconds, 10), records.Length, _ =>
        {
            using var e = ml.Model.CreatePredictionEngine<TransactionRiskData, TransactionRiskPrediction>(model);
            return true;
        }),
        Measurement.RunSync("predict on a reused engine (one thread only)", 1, Math.Min(seconds, 10), records.Length, i =>
            sharedEngine.Predict(records[i]).Score.Length == 3)
    };
    foreach (var r in breakdown) Print(r);
    results["costBreakdown"] = breakdown;

    foreach (var (name, service) in new[] { ("EnginePerRequest", perRequest), ("Pooled", (RiskModelService)pooled) })
    {
        Console.WriteLine($"\nservice.Predict, {name}");
        PrintHeader();
        var sweep = new List<LevelResult>();
        foreach (var c in levels)
        {
            Measurement.RunSync("warm-up", c, warmup, records.Length, i => service.Predict(records[i]).ModelVersion.Length > 0);
            var r = Measurement.RunSync($"{name} x{c}", c, seconds, records.Length,
                i => service.Predict(records[i]).ModelVersion.Length > 0);
            sweep.Add(r);
            Print(r);
        }
        results[name] = sweep;
    }
}

async Task RunHttp(string baseUrl)
{
    var bodies = records.Select(r => JsonSerializer.Serialize(new
    {
        amount = r.Amount,
        transactionHour = r.TransactionHour,
        customerTransactionCount24h = r.CustomerTransactionCount24h,
        recentFailureCount = r.RecentFailureCount,
        beneficiaryAgeDays = r.BeneficiaryAgeDays,
        isHighRiskCountry = r.IsHighRiskCountry
    })).ToArray();

    using var client = new HttpClient(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = 512,
        PooledConnectionLifetime = TimeSpan.FromMinutes(30)
    })
    { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };

    var health = await client.GetAsync("/health/ready");
    if (health.StatusCode != HttpStatusCode.OK)
        throw new InvalidOperationException($"{baseUrl}/health/ready returned {(int)health.StatusCode}. Start the API first.");

    async Task<string> Post(int i)
    {
        using var content = new StringContent(bodies[i], Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/risk/predict", content);
        return response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadAsStringAsync() : "";
    }

    async Task<bool> Call(int i) => (await Post(i)).Length > 0;

    // A fingerprint of every answer, so two runs can be compared exactly.
    var all = new StringBuilder();
    for (var i = 0; i < bodies.Length; i++) all.Append(await Post(i)).Append('\n');
    var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(all.ToString()))).ToLowerInvariant();
    results["responseDigestSha256"] = digest;
    Console.WriteLine($"Response digest over {bodies.Length} records: {digest[..16]}...");

    ProcessSampler? sampler = null;
    var pidText = ArgValue("--server-pid");

    Console.WriteLine($"\nPOST /risk/predict [{label}] (each client keeps one request in flight; client and server share this machine)");
    PrintHeader(withResources: pidText is not null);
    var sweep = new List<object>();
    foreach (var c in levels)
    {
        await Task.Run(() => Measurement.RunAsync("warm-up", c, warmup, bodies.Length, Call));

        if (pidText is not null) sampler = new ProcessSampler(int.Parse(pidText));
        sampler?.Start();
        var windowStart = DateTime.Now;
        var r = await Task.Run(() => Measurement.RunAsync($"POST /risk/predict x{c}", c, seconds, bodies.Length, Call));
        var windowEnd = DateTime.Now;
        var usage = sampler?.Stop();
        sampler?.Dispose();

        // The local clock times let a tool such as dotnet-counters be matched to this window.
        sweep.Add(new { result = r, serverUsage = usage, windowStartLocal = windowStart, windowEndLocal = windowEnd });
        Print(r, usage);
    }
    results["concurrencySweep"] = sweep;
}

int Compare(string fileA, string fileB)
{
    using var a = JsonDocument.Parse(File.ReadAllText(resolver.ResolveConfiguredPath(fileA)));
    using var b = JsonDocument.Parse(File.ReadAllText(resolver.ResolveConfiguredPath(fileB)));
    var labelA = a.RootElement.GetProperty("environment").GetProperty("label").GetString();
    var labelB = b.RootElement.GetProperty("environment").GetProperty("label").GetString();
    var digestA = a.RootElement.GetProperty("responseDigestSha256").GetString();
    var digestB = b.RootElement.GetProperty("responseDigestSha256").GetString();
    Console.WriteLine($"Response digests equal: {digestA == digestB}\n");

    var levelsA = a.RootElement.GetProperty("concurrencySweep").EnumerateArray().ToList();
    var levelsB = b.RootElement.GetProperty("concurrencySweep").EnumerateArray().ToList();
    for (var i = 0; i < levelsA.Count; i++)
    {
        var ra = levelsA[i].GetProperty("result");
        var rb = levelsB[i].GetProperty("result");
        var ua = levelsA[i].GetProperty("serverUsage");
        var ub = levelsB[i].GetProperty("serverUsage");
        Console.WriteLine($"### {ra.GetProperty("Concurrency").GetInt32()} concurrent clients\n");
        Console.WriteLine($"| Metric | {labelA} | {labelB} |\n|---|---|---|");
        void Row(string metric, string va, string vb) => Console.WriteLine($"| {metric} | {va} | {vb} |");
        string D(JsonElement e, string k, string f = "F2") => e.GetProperty(k).GetDouble().ToString(f);
        Row("p50 latency (ms)", (ra.GetProperty("P50Us").GetDouble() / 1000).ToString("F2"), (rb.GetProperty("P50Us").GetDouble() / 1000).ToString("F2"));
        Row("p95 latency (ms)", (ra.GetProperty("P95Us").GetDouble() / 1000).ToString("F2"), (rb.GetProperty("P95Us").GetDouble() / 1000).ToString("F2"));
        Row("p99 latency (ms)", (ra.GetProperty("P99Us").GetDouble() / 1000).ToString("F2"), (rb.GetProperty("P99Us").GetDouble() / 1000).ToString("F2"));
        Row("Throughput (RPS)", D(ra, "CallsPerSecond", "F0"), D(rb, "CallsPerSecond", "F0"));
        Row("Error rate", (100.0 * ra.GetProperty("Errors").GetInt64() / Math.Max(1, ra.GetProperty("Calls").GetInt64())).ToString("F3") + " %",
            (100.0 * rb.GetProperty("Errors").GetInt64() / Math.Max(1, rb.GetProperty("Calls").GetInt64())).ToString("F3") + " %");
        if (ua.ValueKind == JsonValueKind.Object && ub.ValueKind == JsonValueKind.Object)
        {
            Row("Server CPU (% of machine)", D(ua, "AverageCpuPercentOfMachine", "F1"), D(ub, "AverageCpuPercentOfMachine", "F1"));
            Row("Server peak working set (MB)", D(ua, "PeakWorkingSetMb", "F0"), D(ub, "PeakWorkingSetMb", "F0"));
        }
        Console.WriteLine();
    }
    return 0;
}

void Warm(Action action) => action();

void PrintHeader(bool withResources = false) =>
    Console.WriteLine($"{"case",-52} {"calls/s",9} {"mean",9} {"p50",9} {"p95",9} {"p99",9} {"max",10} {"B/call",8} {"gc0/1/2",11} {"err",5}" +
                      (withResources ? $" {"cpu%mach",9} {"wsMB",7}" : "") + "  (latency in us)");

void Print(LevelResult r, ResourceUsage? usage = null) =>
    Console.WriteLine($"{r.Name,-52} {r.CallsPerSecond,9:F0} {r.MeanUs,9:F1} {r.P50Us,9:F1} {r.P95Us,9:F1} {r.P99Us,9:F1} {r.MaxUs,10:F0} {r.AllocBytesPerCall,8:F0} {$"{r.Gen0Collections}/{r.Gen1Collections}/{r.Gen2Collections}",11} {r.Errors,5}" +
                      (usage is null ? "" : $" {usage.AverageCpuPercentOfMachine,9:F1} {usage.PeakWorkingSetMb,7:F0}"));

RiskModelOptions LoadOptions()
{
    var path = resolver.ResolveConfiguredPath("src/RiskInference.Api/appsettings.json");
    return new ConfigurationBuilder().AddJsonFile(path).Build()
        .GetSection(RiskModelOptions.SectionName).Get<RiskModelOptions>()!;
}

string? ArgValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static bool IsDebugBuild() =>
#if DEBUG
    true;
#else
    false;
#endif

// Finds the folder with the .slnx file, like the PathResolver of the lab, but
// without a web host.
sealed class SolutionPathResolver : IPathResolver
{
    public string ResolveConfiguredPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath)) return configuredPath;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
                return Path.Combine(dir.FullName, configuredPath);
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, configuredPath);
    }
}
