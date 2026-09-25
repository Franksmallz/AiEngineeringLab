using System.Text.Json;
using FoundationalModel.Models.Dtos;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.API.Tests;

public sealed class InferenceOptimizationTests
{
    [Fact]
    public void Inference_scenarios_define_expected_batch_16_precision_variants()
    {
        var scenarios = new[]
        {
            InferenceScenarios.Fp16Batch16,
            InferenceScenarios.Int8Batch16,
            InferenceScenarios.Int4Nf4Batch16
        };

        Assert.Equal(["FP16", "INT8", "INT4-NF4"], scenarios.Select(x => x.Precision));
        Assert.All(scenarios, scenario =>
        {
            Assert.Equal(16, scenario.BatchSize);
            Assert.Equal(80, scenario.MaxNewTokens);
            Assert.False(scenario.DoSample);
            Assert.Equal("Qwen2.5-0.5B-payment", scenario.ModelName);
            Assert.Equal("eval.jsonl", scenario.EvaluationFile);
        });
    }

    [Fact]
    public void Inference_scenarios_use_distinct_experiment_and_output_names()
    {
        var scenarios = new[]
        {
            InferenceScenarios.Fp16Batch16,
            InferenceScenarios.Int8Batch16,
            InferenceScenarios.Int4Nf4Batch16
        };

        Assert.Equal(3, scenarios.Select(x => x.ExperimentName).Distinct().Count());
        Assert.Equal(3, scenarios.Select(x => x.OutputFile).Distinct().Count());
        Assert.Contains("fp16", scenarios[0].OutputFile);
        Assert.Contains("int8", scenarios[1].OutputFile);
        Assert.Contains("int4", scenarios[2].OutputFile);
    }

    [Fact]
    public void Inference_result_serializes_using_benchmark_schema_names()
    {
        var result = new InferenceResult
        {
            Input = "payment timed out",
            Actual = "Category: Provider Timeout",
            Expected = "Category: Provider Timeout",
            GeneratedTokens = 12
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var properties = document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);

        Assert.Equal("payment timed out", properties["input"].GetString());
        Assert.Equal("Category: Provider Timeout", properties["actual"].GetString());
        Assert.Equal("Category: Provider Timeout", properties["expected"].GetString());
        Assert.Equal(12, properties["generated_tokens"].GetInt32());
    }

    [Fact]
    public void Inference_benchmark_result_preserves_metrics_and_success_state()
    {
        var result = new InferenceBenchmarkResult
        {
            ExperimentName = "int8-batch16",
            ModelName = "Qwen2.5-0.5B-payment",
            Configuration = "batch=16, precision=INT8",
            EvaluationCases = 20,
            AverageLatencyMs = 42.5,
            TokensPerSecond = 123.4,
            MemoryUsedMb = 512,
            TotalGeneratedTokens = 800,
            QualityScore = 0.95,
            SchemaCompliance = 19,
            SemanticAccuracy = 18,
            GPUTimeSeconds = 3.2,
            Successful = true,
            Notes = "completed"
        };

        Assert.Equal("int8-batch16", result.ExperimentName);
        Assert.Equal(20, result.EvaluationCases);
        Assert.Equal(42.5, result.AverageLatencyMs);
        Assert.Equal(123.4, result.TokensPerSecond);
        Assert.Equal(512, result.MemoryUsedMb);
        Assert.Equal(800, result.TotalGeneratedTokens);
        Assert.Equal(0.95, result.QualityScore);
        Assert.Equal(19, result.SchemaCompliance);
        Assert.Equal(18, result.SemanticAccuracy);
        Assert.True(result.Successful);
    }

    [Fact]
    public void Semantic_evaluation_defaults_to_no_claimed_quality()
    {
        var evaluation = new SemanticEvaluation();

        Assert.Equal(string.Empty, evaluation.Input);
        Assert.Equal(string.Empty, evaluation.Expected);
        Assert.Equal(string.Empty, evaluation.Actual);
        Assert.False(evaluation.CategoryCorrect);
        Assert.False(evaluation.RetryableCorrect);
        Assert.False(evaluation.ActionCorrect);
        Assert.False(evaluation.HasContradictionOrHallucination);
        Assert.Equal(string.Empty, evaluation.Notes);
    }

}
