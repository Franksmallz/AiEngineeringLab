using System.Diagnostics;

namespace RiskInference.Benchmark;

public sealed record LevelResult(
    string Name,
    int Concurrency,
    double Seconds,
    long Calls,
    long Errors,
    double CallsPerSecond,
    double MeanUs,
    double P50Us,
    double P95Us,
    double P99Us,
    double MaxUs,
    double AllocBytesPerCall,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections);

// Shared measuring code. Latency is the time of one call, measured by the
// worker that made it. Throughput is completed calls divided by wall time.
public static class Measurement
{
    private static double ToUs(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;

    // Runs `operation` on `concurrency` dedicated threads for `seconds`.
    // The operation takes a record index. It returns false for an error.
    public static LevelResult RunSync(
        string name, int concurrency, double seconds, int recordCount, Func<int, bool> operation) =>
        Run(name, concurrency, seconds, recordCount, asyncOperation: null, sync: operation);

    // Same, for async operations (HTTP). The worker thread waits for each call,
    // so every worker has exactly one request in flight.
    public static LevelResult RunAsync(
        string name, int concurrency, double seconds, int recordCount, Func<int, Task<bool>> operation) =>
        Run(name, concurrency, seconds, recordCount, operation, sync: null);

    private static LevelResult Run(
        string name, int concurrency, double seconds, int recordCount,
        Func<int, Task<bool>>? asyncOperation, Func<int, bool>? sync)
    {
        // Each worker records the latency of its calls in a fixed array that is
        // allocated before the run, so recording itself allocates nothing.
        // After MaxSamplesPerWorker calls, a worker keeps counting calls but
        // stops recording latency. At these rates that cap is rarely reached.
        // The total over all workers is about 4 million samples (32 MB).
        var maxSamplesPerWorker = Math.Max(10_000, 4_000_000 / concurrency);
        var buffers = Enumerable.Range(0, concurrency).Select(_ => new long[maxSamplesPerWorker]).ToArray();
        var recorded = new int[concurrency];
        var callCounts = new long[concurrency];
        var errors = new long[concurrency];
        using var go = new ManualResetEventSlim(false);
        long endTimestamp = 0;

        var threads = Enumerable.Range(0, concurrency).Select(worker => new Thread(() =>
        {
            go.Wait();
            var end = Volatile.Read(ref endTimestamp);
            var buffer = buffers[worker];
            var count = 0;
            long calls = 0;
            var i = worker * 997;
            long failed = 0;
            while (true)
            {
                var t0 = Stopwatch.GetTimestamp();
                if (t0 >= end) break;
                var ok = sync is not null
                    ? sync(i++ % recordCount)
                    : asyncOperation!(i++ % recordCount).GetAwaiter().GetResult();
                var elapsedTicks = Stopwatch.GetTimestamp() - t0;
                calls++;
                if (count < buffer.Length) buffer[count++] = elapsedTicks;
                if (!ok) failed++;
            }
            errors[worker] = failed;
            recorded[worker] = count;
            callCounts[worker] = calls;
        }) { IsBackground = true }).ToList();

        threads.ForEach(t => t.Start());

        // Start from a clean GC state so the counts belong to this level only.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var allocBefore = GC.GetTotalAllocatedBytes(precise: true);

        var start = Stopwatch.GetTimestamp();
        Volatile.Write(ref endTimestamp, start + (long)(seconds * Stopwatch.Frequency));
        go.Set();
        threads.ForEach(t => t.Join());
        var elapsed = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;

        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;
        var all = buffers.SelectMany((b, w) => b.Take(recorded[w])).ToArray();
        Array.Sort(all);
        var calls = callCounts.Sum();

        double Percentile(double p) => all.Length == 0 ? 0 : ToUs(all[Math.Min(all.Length - 1, (int)Math.Ceiling(p * all.Length) - 1)]);

        return new LevelResult(
            name, concurrency, Math.Round(elapsed, 3), calls, errors.Sum(),
            Math.Round(calls / elapsed, 1),
            Math.Round(all.Length == 0 ? 0 : ToUs((long)all.Average()), 1),
            Math.Round(Percentile(0.50), 1),
            Math.Round(Percentile(0.95), 1),
            Math.Round(Percentile(0.99), 1),
            Math.Round(all.Length == 0 ? 0 : ToUs(all[^1]), 1),
            Math.Round(calls == 0 ? 0 : (double)allocated / calls, 0),
            GC.CollectionCount(0) - gen0,
            GC.CollectionCount(1) - gen1,
            GC.CollectionCount(2) - gen2);
    }
}
