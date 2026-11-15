using System.Diagnostics.Metrics;

namespace RiskInference.Api.Operations;

// How a request to POST /risk/predict ended.
public enum RequestOutcome
{
    Predicted,          // 200: a prediction was made
    ValidationFailed,   // 400: the fields were missing or out of range
    MalformedRequest,   // 400: the body was not readable JSON
    NotReady,           // 503: the model failed an integrity check
    Error               // 500: an unexpected error
}

public sealed record LatencySummary(double Count, double MeanMs, double P50Ms, double P95Ms, double P99Ms, double MaxMs);

public sealed record MetricsSnapshot(
    DateTimeOffset StartedAt,
    string ModelVersion,
    string DecisionPolicyVersion,
    long Requests,
    IReadOnlyDictionary<string, long> RequestsByOutcome,
    // Share of requests that ended in a server error (HTTP 5xx, outcome Error or NotReady).
    double ServerErrorRate,
    // Share of requests that the client got wrong (HTTP 400).
    double ClientErrorRate,
    LatencySummary Latency,
    long Predictions,
    IReadOnlyDictionary<string, long> PredictionsByRiskLevel,
    long FlaggedHighRisk,
    // FlaggedHighRisk divided by Predictions.
    double HighRiskFlagRate);

// Aggregate numbers about the service. They hold no transaction data and no
// customer data: only counts and times.
//
// Two views of the same measurements:
//  - GET /metrics returns a snapshot with the percentiles.
//  - A System.Diagnostics.Metrics Meter ("RiskInference.Api") feeds standard collectors
//    (OpenTelemetry, dotnet-counters).
//
// Every method is safe to call from many threads at the same time.
public sealed class RiskMetrics : IDisposable
{
    public const string MeterName = "RiskInference.Api";

    // Latency buckets grow by 10% each step, from 0.01 ms. A percentile is the
    // upper edge of its bucket, so it is at most 10% above the real value.
    private const double FirstBucketMs = 0.01;
    private const double BucketGrowth = 1.10;
    private const int BucketCount = 180;   // up to about 0.01 * 1.1^180 = 270 s

    private static readonly string[] RiskLevels = ["Low", "Medium", "High"];
    private static readonly string[] OutcomeNames = Enum.GetNames<RequestOutcome>();

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _durationHistogram;
    private readonly Counter<long> _predictionCounter;
    private readonly Counter<long> _flaggedCounter;

    private readonly long[] _outcomeCounts = new long[OutcomeNames.Length];
    private readonly long[] _latencyBuckets = new long[BucketCount + 1];
    private readonly long[] _riskCounts = new long[RiskLevels.Length + 1];   // last slot: any other label
    private long _latencySumMicros;
    private long _latencyMaxMicros;
    private long _flagged;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public RiskMetrics()
    {
        _requestCounter = _meter.CreateCounter<long>("risk.requests", description: "Prediction requests by outcome");
        _durationHistogram = _meter.CreateHistogram<double>("risk.request.duration", unit: "ms",
            description: "Duration of prediction requests");
        _predictionCounter = _meter.CreateCounter<long>("risk.predictions", description: "Predictions by predicted risk level");
        _flaggedCounter = _meter.CreateCounter<long>("risk.predictions.flagged", description: "Predictions flagged as High risk");
    }

    // predictedRisk and flagged are only set when the request gave a prediction.
    public void Record(RequestOutcome outcome, double durationMs, string? predictedRisk = null, bool flagged = false)
    {
        Interlocked.Increment(ref _outcomeCounts[(int)outcome]);
        Interlocked.Increment(ref _latencyBuckets[BucketIndex(durationMs)]);
        var micros = (long)(durationMs * 1000);
        Interlocked.Add(ref _latencySumMicros, micros);
        long seen;
        while (micros > (seen = Interlocked.Read(ref _latencyMaxMicros)) &&
               Interlocked.CompareExchange(ref _latencyMaxMicros, micros, seen) != seen) { }

        _requestCounter.Add(1, new KeyValuePair<string, object?>("outcome", OutcomeNames[(int)outcome]));
        _durationHistogram.Record(durationMs, new KeyValuePair<string, object?>("outcome", OutcomeNames[(int)outcome]));

        if (outcome == RequestOutcome.Predicted && predictedRisk is not null)
        {
            var slot = Array.IndexOf(RiskLevels, predictedRisk);
            Interlocked.Increment(ref _riskCounts[slot < 0 ? RiskLevels.Length : slot]);
            _predictionCounter.Add(1, new KeyValuePair<string, object?>("predicted_risk", predictedRisk));
            if (flagged)
            {
                Interlocked.Increment(ref _flagged);
                _flaggedCounter.Add(1);
            }
        }
    }

    public MetricsSnapshot Snapshot(string modelVersion, string policyVersion)
    {
        var outcomes = OutcomeNames.Select((name, i) => (name, count: Interlocked.Read(ref _outcomeCounts[i])))
            .ToDictionary(x => x.name, x => x.count);
        var requests = outcomes.Values.Sum();
        var serverErrors = outcomes[nameof(RequestOutcome.Error)] + outcomes[nameof(RequestOutcome.NotReady)];
        var clientErrors = outcomes[nameof(RequestOutcome.ValidationFailed)] + outcomes[nameof(RequestOutcome.MalformedRequest)];

        var risks = RiskLevels.Select((name, i) => (name, count: Interlocked.Read(ref _riskCounts[i])))
            .ToDictionary(x => x.name, x => x.count);
        var other = Interlocked.Read(ref _riskCounts[RiskLevels.Length]);
        if (other > 0) risks["Other"] = other;
        var predictions = risks.Values.Sum();
        var flagged = Interlocked.Read(ref _flagged);

        return new MetricsSnapshot(
            _startedAt, modelVersion, policyVersion, requests, outcomes,
            ServerErrorRate: Rate(serverErrors, requests),
            ClientErrorRate: Rate(clientErrors, requests),
            Latency: LatencySnapshot(requests),
            Predictions: predictions,
            PredictionsByRiskLevel: risks,
            FlaggedHighRisk: flagged,
            HighRiskFlagRate: Rate(flagged, predictions));
    }

    private LatencySummary LatencySnapshot(long requests)
    {
        var counts = _latencyBuckets.Select(c => Interlocked.Read(ref c)).ToArray();
        var total = counts.Sum();
        if (total == 0) return new LatencySummary(0, 0, 0, 0, 0, 0);

        double Percentile(double p)
        {
            var rank = (long)Math.Ceiling(p * total);
            long cumulative = 0;
            for (var i = 0; i < counts.Length; i++)
            {
                cumulative += counts[i];
                if (cumulative >= rank) return Math.Round(BucketUpperEdge(i), 3);
            }
            return Math.Round(BucketUpperEdge(counts.Length - 1), 3);
        }

        var max = Interlocked.Read(ref _latencyMaxMicros) / 1000.0;
        return new LatencySummary(
            total,
            Math.Round(Interlocked.Read(ref _latencySumMicros) / 1000.0 / total, 3),
            // A percentile never exceeds the largest value that was seen.
            Math.Min(Percentile(0.50), Math.Round(max, 3)),
            Math.Min(Percentile(0.95), Math.Round(max, 3)),
            Math.Min(Percentile(0.99), Math.Round(max, 3)),
            Math.Round(max, 3));
    }

    private static int BucketIndex(double ms)
    {
        if (!(ms > FirstBucketMs)) return 0;
        var index = (int)Math.Ceiling(Math.Log(ms / FirstBucketMs) / Math.Log(BucketGrowth));
        return Math.Min(index, BucketCount);
    }

    private static double BucketUpperEdge(int index) => FirstBucketMs * Math.Pow(BucketGrowth, index);

    private static double Rate(long part, long whole) => whole == 0 ? 0 : Math.Round((double)part / whole, 6);

    public void Dispose() => _meter.Dispose();
}
