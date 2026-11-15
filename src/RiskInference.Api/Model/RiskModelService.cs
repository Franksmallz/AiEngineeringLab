using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using RiskClassificationLab.Models;
using RiskClassificationLab.Services.Implementations.ML;
using RiskClassificationLab.Services.Interfaces;
using RiskInference.Api.Contracts;

namespace RiskInference.Api.Model;

// Loads the Chapter 6 model once, in the constructor, and scores with it.
// The constructor reads the manifest and then the model file next to it. It
// throws if the manifest is missing or not valid, if its versions are not
// supported, or if the model is missing, changed (hash), corrupted or
// incompatible. Register this class as a singleton and create it at startup,
// so a bad model stops the application before it accepts any request.
//
// Thread safety: after construction, the MLContext and the ITransformer are
// only read. A PredictionEngine is not thread-safe, so a request never shares
// one with another request at the same time:
//  - EnginePerRequest: each call makes its own engine and disposes it.
//  - Pooled: each call rents an engine that no other call holds, and gives it
//    back after the prediction. A pool never hands one engine to two callers.
public sealed class RiskModelService : IRiskModelService, IDisposable
{
    // The positive class of the Chapter 6 decision rule.
    public const string PositiveClass = "High";

    private readonly MLContext _mlContext;
    private readonly ITransformer _model;
    private readonly int _highScoreSlot;
    private readonly double _threshold;
    private readonly ObjectPool<PredictionEngine<TransactionRiskData, TransactionRiskPrediction>>? _pool;
    private readonly IPathResolver _pathResolver;
    private readonly string _manifestPath;
    private readonly (string Risk, float HighScore) _expectedTestAnswer;

    // A fixed test input. Its answer at start-up is the reference for VerifyIntegrity.
    // It is not a real transaction.
    private static readonly TransactionRiskData TestInput = new()
    {
        Amount = 100,
        TransactionHour = 12,
        CustomerTransactionCount24h = 1,
        RecentFailureCount = 0,
        BeneficiaryAgeDays = 365,
        IsHighRiskCountry = false
    };

    public RiskModelService(IOptions<RiskModelOptions> options, IPathResolver pathResolver)
    {
        var settings = options.Value;
        var manifestPath = pathResolver.ResolveConfiguredPath(settings.ManifestPath);
        _pathResolver = pathResolver;
        _manifestPath = manifestPath;

        try
        {
            var manifest = ModelManifest.Load(manifestPath);
            _threshold = manifest.Threshold;
            Active = new ActiveModelInfo(
                manifest.ModelVersion,
                manifest.DecisionPolicyVersion,
                manifest.FeatureSchemaVersion,
                manifest.ModelSha256.ToLowerInvariant(),
                manifest.Threshold);

            // The model file must be next to the manifest.
            var modelPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.ModelFile);

            _mlContext = new MLContext();

            // FinalModelStore checks the content hash, registers the custom
            // feature mapping, and then calls MLContext.Model.Load.
            _model = new FinalModelStore(pathResolver)
                .LoadModel(_mlContext, modelPath, Active.ModelSha256);

            if (settings.InferenceMode == InferenceMode.Pooled)
            {
                // The pool keeps at most this many idle engines. A burst above
                // this number makes extra engines that are dropped after use.
                var provider = new DefaultObjectPoolProvider { MaximumRetained = Environment.ProcessorCount * 4 };
                _pool = provider.Create(new EnginePolicy(_mlContext, _model));
            }

            _highScoreSlot = FindScoreSlot(_mlContext, _model, PositiveClass);
            _expectedTestAnswer = SelfCheck();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The risk model from '{settings.ManifestPath}' could not be loaded: {ex.Message}", ex);
        }
    }

    public ActiveModelInfo Active { get; }

    public RiskPredictionResponse Predict(TransactionRiskData input)
    {
        var (predictedRisk, highScore) = Score(input);

        return new RiskPredictionResponse(
            PredictedRisk: predictedRisk,
            HighRiskScore: highScore,
            FlaggedHighRisk: highScore >= _threshold,
            ModelVersion: Active.ModelVersion,
            Threshold: _threshold,
            DecisionPolicyVersion: Active.DecisionPolicyVersion);
    }

    private (string PredictedRisk, float HighScore) Score(TransactionRiskData input)
    {
        if (_pool is null)
        {
            using var engine = _mlContext.Model
                .CreatePredictionEngine<TransactionRiskData, TransactionRiskPrediction>(_model);
            return Read(engine.Predict(input));
        }

        var rented = _pool.Get();
        try
        {
            var result = Read(rented.Predict(input));
            _pool.Return(rented);
            return result;
        }
        catch
        {
            // After an error the engine state is unknown. Do not reuse it.
            rented.Dispose();
            throw;
        }
    }

    private (string PredictedRisk, float HighScore) Read(TransactionRiskPrediction prediction) =>
        (prediction.RiskLevel, prediction.Score[_highScoreSlot]);

    public void Dispose() => (_pool as IDisposable)?.Dispose();

    private sealed class EnginePolicy : IPooledObjectPolicy<PredictionEngine<TransactionRiskData, TransactionRiskPrediction>>
    {
        private readonly MLContext _mlContext;
        private readonly ITransformer _model;

        public EnginePolicy(MLContext mlContext, ITransformer model)
        {
            _mlContext = mlContext;
            _model = model;
        }

        public PredictionEngine<TransactionRiskData, TransactionRiskPrediction> Create() =>
            _mlContext.Model.CreatePredictionEngine<TransactionRiskData, TransactionRiskPrediction>(_model);

        public bool Return(PredictionEngine<TransactionRiskData, TransactionRiskPrediction> engine) => true;
    }

    // The Score column is an unnamed array. The model stores one class name per
    // slot, and ML.NET numbers the classes in the order it first saw them in
    // the training data. Read the slot name. Never assume an index.
    private static int FindScoreSlot(MLContext mlContext, ITransformer model, string positiveClass)
    {
        var inputSchema = mlContext.Data.LoadFromEnumerable(Array.Empty<TransactionRiskData>()).Schema;
        var scoreColumn = model.GetOutputSchema(inputSchema)["Score"];

        VBuffer<ReadOnlyMemory<char>> slotNames = default;
        scoreColumn.GetSlotNames(ref slotNames);

        var names = slotNames.DenseValues().Select(n => n.ToString()).ToList();
        var index = names.IndexOf(positiveClass);
        if (index < 0)
            throw new InvalidOperationException(
                $"The model has no Score slot named '{positiveClass}'. Slots: {string.Join(", ", names)}.");
        return index;
    }

    // One test prediction. It proves the whole pipeline runs, including the
    // custom feature mapping, before the service accepts traffic. The answer is
    // kept, so that VerifyIntegrity can check later that it did not change.
    private (string Risk, float HighScore) SelfCheck()
    {
        var (risk, highScore) = Score(TestInput);

        if (string.IsNullOrEmpty(risk))
            throw new InvalidOperationException("The self-check prediction has no risk level.");
        if (!(highScore >= 0f && highScore <= 1f))
            throw new InvalidOperationException($"The self-check High score {highScore} is not in [0, 1].");
        return (risk, highScore);
    }

    public string? VerifyIntegrity()
    {
        try
        {
            // 1. The manifest on disk must be the one that was loaded.
            ModelManifest manifest;
            if (!File.Exists(_manifestPath)) return "manifest_unavailable";
            try { manifest = ModelManifest.Load(_manifestPath); }
            catch (Exception) { return "manifest_invalid"; }

            if (manifest.ModelVersion != Active.ModelVersion
                || manifest.DecisionPolicyVersion != Active.DecisionPolicyVersion
                || manifest.FeatureSchemaVersion != Active.FeatureSchemaVersion
                || !string.Equals(manifest.ModelSha256, Active.ModelSha256, StringComparison.OrdinalIgnoreCase)
                || manifest.Threshold != Active.Threshold)
                return "manifest_changed";

            // 2. The model file on disk must still have the hash of the manifest and must load.
            var modelPath = Path.Combine(Path.GetDirectoryName(_manifestPath)!, manifest.ModelFile);
            if (!File.Exists(modelPath)) return "model_unavailable";
            try
            {
                // A separate MLContext, so the loaded model is not touched.
                new FinalModelStore(_pathResolver).LoadModel(new MLContext(), modelPath, Active.ModelSha256);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not match the frozen"))
            {
                return "model_hash_mismatch";
            }
            catch (Exception)
            {
                return "model_invalid";
            }

            // 3. The loaded model must still give the same answer for the fixed test input.
            var (risk, highScore) = Score(TestInput);
            if (risk != _expectedTestAnswer.Risk || highScore != _expectedTestAnswer.HighScore)
                return "test_prediction_changed";

            return null;
        }
        catch (Exception)
        {
            return "test_prediction_failed";
        }
    }
}
