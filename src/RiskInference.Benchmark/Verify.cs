using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations;
using RiskClassificationLab.Services.Implementations.ML;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Model;

namespace RiskInference.Benchmark;

// Checks a running API against the Chapter 6 pipeline.
//
//   verify <url> [--parallel 32] [--rounds 3] [--save <file>]
//
// Reference: the frozen Chapter 6 model, loaded with the Chapter 6 FinalModelStore,
// scored with one batch Transform. The High score is read by slot name.
// For every sample the API must give:
//   - the same predicted label (exact),
//   - the same High score (within a small tolerance),
//   - the same threshold decision (exact),
//   - the model version, policy version and threshold of the manifest.
// The samples are the Chapter 6 train sample (every 5th record of train-5000.csv)
// and all records of test-1000.csv. This only scores them. It does not tune anything.
public static class Verify
{
    private const float ScoreTolerance = 1e-5f;

    private sealed record Reference(string Label, float HighScore, bool Flagged);
    private sealed record Answer(string PredictedRisk, float HighRiskScore, bool FlaggedHighRisk,
        string ModelVersion, string DecisionPolicyVersion, double Threshold);

    public static async Task<int> Run(string[] args, IPathResolver resolver, Func<string, string?> argValue)
    {
        var url = args[1];
        var parallel = int.Parse(argValue("--parallel") ?? "32");
        var rounds = int.Parse(argValue("--rounds") ?? "3");
        var savePath = argValue("--save");

        var reader = new DatasetReader(resolver);
        var samples = reader.Read("chapter-06/train-5000.csv").Where((_, i) => i % 5 == 0)
            .Concat(reader.Read("chapter-06/test-1000.csv"))
            .ToArray();

        var manifestPath = resolver.ResolveConfiguredPath("Models/chapter-06-risk-classifier.manifest.json");
        var manifest = ModelManifest.Load(manifestPath);
        var reference = ChapterSixReference(resolver, manifestPath, manifest, samples);
        Console.WriteLine($"Verify {url}: {samples.Length} Chapter 6 samples. Reference: Chapter 6 batch pipeline, " +
                          $"threshold {manifest.Threshold}.");

        using var client = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 256 })
        { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(30) };
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        async Task<Answer?> Ask(int i)
        {
            var s = samples[i];
            var body = JsonSerializer.Serialize(new
            {
                amount = s.Amount, transactionHour = s.TransactionHour,
                customerTransactionCount24h = s.CustomerTransactionCount24h,
                recentFailureCount = s.RecentFailureCount, beneficiaryAgeDays = s.BeneficiaryAgeDays,
                isHighRiskCountry = s.IsHighRiskCountry
            });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/risk/predict", content);
            return response.StatusCode == HttpStatusCode.OK
                ? JsonSerializer.Deserialize<Answer>(await response.Content.ReadAsStringAsync(), web)
                : null;
        }

        var problems = new List<string>();
        var maxScoreDiff = 0f;
        var compared = 0L;
        var flagged = 0;
        var flaggedNotHigh = 0;

        void Check(int i, Answer? a, string phase)
        {
            Interlocked.Increment(ref compared);
            var r = reference[i];
            string? problem = null;
            if (a is null) problem = "no answer (HTTP error)";
            else if (a.PredictedRisk != r.Label) problem = $"label {a.PredictedRisk} != {r.Label}";
            else if (a.FlaggedHighRisk != r.Flagged) problem = $"flag {a.FlaggedHighRisk} != {r.Flagged}";
            else if (MathF.Abs(a.HighRiskScore - r.HighScore) > ScoreTolerance)
                problem = $"score {a.HighRiskScore} vs {r.HighScore}";
            else if (a.ModelVersion != manifest.ModelVersion || a.DecisionPolicyVersion != manifest.DecisionPolicyVersion
                     || a.Threshold != manifest.Threshold)
                problem = $"versions/threshold {a.ModelVersion}/{a.DecisionPolicyVersion}/{a.Threshold}";

            if (a is not null)
                lock (problems) maxScoreDiff = MathF.Max(maxScoreDiff, MathF.Abs(a.HighRiskScore - r.HighScore));
            if (problem is not null)
                lock (problems) { if (problems.Count < 10) problems.Add($"[{phase}] sample {i}: {problem}"); else problems.Add(null!); }
        }

        // Phase 1: one request at a time.
        var answers = new Answer?[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            answers[i] = await Ask(i);
            Check(i, answers[i], "sequential");
            if (reference[i].Flagged) flagged++;
            if (reference[i].Flagged && reference[i].Label != "High") flaggedNotHigh++;
        }
        Console.WriteLine($"Sequential: {samples.Length} samples compared. Reference flagged {flagged}, " +
                          $"of which {flaggedNotHigh} were predicted Low or Medium.");

        // Phase 2: many requests at the same time, each sample several times, in different orders.
        var sequentialProblems = problems.Count;
        var tasks = Enumerable.Range(0, parallel).Select(worker => Task.Run(async () =>
        {
            for (var round = 0; round < rounds; round++)
                for (var k = 0; k < samples.Length; k++)
                {
                    var i = (k * 7 + worker * 131 + round * 17) % samples.Length;
                    Check(i, await Ask(i), "concurrent");
                }
        })).ToArray();
        await Task.WhenAll(tasks);
        Console.WriteLine($"Concurrent: {parallel} workers x {rounds} rounds x {samples.Length} samples.");

        if (savePath is not null)
        {
            var path = resolver.ResolveConfiguredPath(savePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(answers.Select(a => a is null ? null : new
            {
                a.PredictedRisk, a.HighRiskScore, a.FlaggedHighRisk
            })));
        }

        var failures = problems.Count;
        Console.WriteLine($"Compared {compared} responses. Mismatches: {failures}. Max |score difference|: {maxScoreDiff:E2} " +
                          $"(tolerance {ScoreTolerance:E0}).");
        foreach (var p in problems.Where(p => p is not null)) Console.WriteLine("  " + p);
        Console.WriteLine(failures == 0 ? "VERIFY PASSED" : "VERIFY FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static Reference[] ChapterSixReference(
        IPathResolver resolver, string manifestPath, ModelManifest manifest, TransactionRiskData[] samples)
    {
        var ml = new MLContext();
        var modelPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.ModelFile);
        var model = new FinalModelStore(resolver).LoadModel(ml, modelPath, manifest.ModelSha256);
        var scored = model.Transform(ml.Data.LoadFromEnumerable(samples));

        VBuffer<ReadOnlyMemory<char>> slotNames = default;
        scored.Schema["Score"].GetSlotNames(ref slotNames);
        var highSlot = slotNames.DenseValues().Select(n => n.ToString()).ToList().IndexOf("High");
        if (highSlot < 0) throw new InvalidOperationException("The model has no High score slot.");

        return ml.Data.CreateEnumerable<TransactionRiskPrediction>(scored, reuseRowObject: false)
            .Select(p => new Reference(p.RiskLevel, p.Score[highSlot], (double)p.Score[highSlot] >= manifest.Threshold))
            .ToArray();
    }
}
