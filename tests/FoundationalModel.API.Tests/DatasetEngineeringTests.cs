using System.Text.Json;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Hosting;
using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FoundationalModel.API.Tests;

public sealed class DatasetEngineeringTests
{
    [Fact]
    public void TrainingDataParser_ParsesStructuredResponseFields()
    {
        var result = new TrainingDataParser().Parse(new TrainingExample
        {
            Input = "timeout incident",
            Output = "Category: Provider Timeout\nRetryable: Yes\nAction: Retry with backoff."
        });

        Assert.Equal("timeout incident", result.Input);
        Assert.Equal("Provider Timeout", result.Category);
        Assert.Equal("Yes", result.Retryable);
        Assert.Equal("Retry with backoff.", result.Action);
    }

    [Fact]
    public void DatasetValidator_ReportsMissingAndInvalidFields()
    {
        var issues = new DatasetValidator().Validate([
            new ParsedTrainingExample
            {
                Input = "",
                Category = "",
                Retryable = "Maybe",
                Action = ""
            }
        ]);

        Assert.Equal(4, issues.Count);
        Assert.Contains(issues, x => x.Message == "Input is missing.");
        Assert.Contains(issues, x => x.Message == "Category is missing.");
        Assert.Contains(issues, x => x.Message == "Action is missing.");
        Assert.Contains(issues, x => x.Message == "Invalid Retryable value: Maybe");
    }

    [Fact]
    public void TextSimilarityScorer_IsCaseInsensitiveAndUsesUniqueTokens()
    {
        var scorer = new TextSimilarityScorer();

        Assert.Equal(1d, scorer.JaccardSimilarity("A timeout, occurred", "timeout a occurred"));
        Assert.Equal(0d, scorer.JaccardSimilarity("alpha", "beta"));
        Assert.Equal(1d, scorer.JaccardSimilarity("", ""));
    }

    [Fact]
    public void DatasetGenerator_IncludesQualityMetrics()
    {
        var report = new DatasetGenerator().Generate("Dataset V2", [
            new ParsedTrainingExample { Category = "Timeout", Input = "first", Retryable = "Yes", Action = "Retry" },
            new ParsedTrainingExample { Category = "Timeout", Input = "second", Retryable = "No", Action = "Escalate" }
        ]);

        Assert.Contains("# Dataset V2 Quality Report", report);
        Assert.Contains("- Total examples: 2", report);
        Assert.Contains("- Categories with retryable variations: 1", report);
        Assert.Contains("- Timeout: 2", report);
        Assert.Contains("2 unique action(s)", report);
    }

    [Fact]
    public void TrainingExampleV2_CreateProducesStructuredOutput()
    {
        var example = TraininExampleV2.Create("incident", "Timeout", "Yes", "Retry");

        Assert.Equal("incident", example.Input);
        Assert.Equal("Category: Timeout\nRetryable: Yes\nAction: Retry", example.Output);
        Assert.Equal(50, DatasetScenario.Scenarios.Count);
        Assert.Equal(10, DatasetScenario.Scenarios.Select(x => x.Category).Distinct().Count());
    }

    [Fact]
    public void DatasetAuditService_PrintsDistributionAndVariation()
    {
        var originalOut = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            new DatasetAuditService().PrintSummary("V2", [
                new ParsedTrainingExample { Input = "same", Category = "Timeout", Retryable = "Yes", Action = "Retry" },
                new ParsedTrainingExample { Input = "same", Category = "Timeout", Retryable = "No", Action = "Escalate" }
            ]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var text = output.ToString();
        Assert.Contains("===== V2 =====", text);
        Assert.Contains("Exact duplicate inputs: 1", text);
        Assert.Contains("Timeout: 2 unique value(s)", text);
        Assert.Contains("Timeout: 2 unique action(s)", text);
    }

    [Fact]
    public void ExpectedAnswerParser_AndSchemaValidator_ParseAndValidate()
    {
        var parser = new ExpectedAnswerParser();
        var validator = new EvaluatorSchemaValidator();

        var parsed = parser.Parse("Category: Duplicate Request\r\nRetryable: No\r\nAction: Check idempotency.");

        Assert.Equal("Duplicate Request", parsed.Category);
        Assert.Equal("No", parsed.Retryable);
        Assert.Equal("Check idempotency.", parsed.Action);
        Assert.True(validator.IsCompliant("Category: X\nRetryable: Yes\nAction: Retry"));
        Assert.False(validator.IsCompliant("Category: X\nAction: Retry"));
    }

    [Fact]
    public void StructuredFieldEvaluator_ReturnsFieldLevelMatches()
    {
        var evaluator = new StructuredFieldEvaluator(
            new EvaluatorSchemaValidator(),
            new ExpectedAnswerParser());

        var score = evaluator.Score(
            "Category: Provider Timeout\nRetryable: Yes\nAction: Retry",
            "Category: provider timeout\nRetryable: No\nAction: Retry");

        Assert.True(score.CategoryCorrect);
        Assert.False(score.RetryableCorrect);
        Assert.True(score.ActionCorrect);

        var invalid = evaluator.Score("Category: A\nRetryable: Yes\nAction: X", "not structured");
        Assert.False(invalid.CategoryCorrect);
        Assert.False(invalid.RetryableCorrect);
        Assert.False(invalid.ActionCorrect);
    }

    [Fact]
    public void ManualEvaluation_CreateAndCalculateSummary()
    {
        var evaluation = ManualEvaluation.Create("V2", [
            new EvaluationResult(),
            new EvaluationResult()
        ]);
        evaluation.Scores[0].CategoryCorrect = true;
        evaluation.Scores[0].RetryableCorrect = true;
        evaluation.Scores[1].ActionCorrect = true;
        evaluation.Scores[1].HasContradictionOrHallucination = true;

        var summary = ManualEvaluationSummary.Calculate(evaluation);

        Assert.Equal("V2", summary.ModelVersion);
        Assert.Equal(2, summary.TotalCases);
        Assert.Equal(1, summary.CategoryCorrect);
        Assert.Equal(1, summary.RetryableCorrect);
        Assert.Equal(1, summary.ActionCorrect);
        Assert.Equal(1, summary.ContradictionsOrHallucinations);
    }

    [Fact]
    public void EvaluationReportGenerator_UsesBothModelSummaries()
    {
        var report = new EvaluationReportGenerator().Genrate(
            new ManualEvaluationSummary
            {
                TotalCases = 20,
                CategoryCorrect = 16,
                RetryableCorrect = 8,
                ActionCorrect = 9,
                ContradictionsOrHallucinations = 6
            },
            new ManualEvaluationSummary
            {
                TotalCases = 20,
                CategoryCorrect = 15,
                RetryableCorrect = 9,
                ActionCorrect = 7,
                ContradictionsOrHallucinations = 7
            },
            0,
            0);

        Assert.Contains("| Category correct | 16/20 | 15/20 |", report);
        Assert.Contains("| Retryable correct | 8/20 | 9/20 |", report);
        Assert.Contains("| Action correct | 9/20 | 7/20 |", report);
        Assert.Contains("## Conclusion", report);
    }

    [Fact]
    public async Task ManualEvaluationTemplateWriter_DoesNotOverwriteExistingTemplate()
    {
        var root = CreateTempDirectory();
        try
        {
            var writer = CreateWriter(root);
            var directory = Path.Combine(root, "evaluations");
            Directory.CreateDirectory(directory);
            var filename = "manual.json";
            var path = Path.Combine(directory, filename);
            File.WriteAllText(path, "original");

            await writer.WriteManualEvaluationTemplate(
                    "evaluations",
                    filename,
                    ManualEvaluation.Create("V2", []));

            Assert.Equal("original", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DatasetWriter_WritesManifestAndReportToConfiguredDirectories()
    {
        var root = CreateTempDirectory();
        try
        {
            var writer = CreateWriter(root);
            var experiment = new TrainingExperiment
            {
                ExperimentName = "experiment",
                TrainingDataset = "dataset_v2.jsonl",
                TrainingExampleCount = 50
            };

            await writer.WriteTrainingV2Jsonl("examples.jsonl", [
                TraininExampleV2.Create("incident", "Timeout", "Yes", "Retry")
            ]);
            await writer.WriteDatasetMetadataJson("metadata.json", new DatasetMetadata
            {
                Name = "Dataset V2",
                Version = "2.0",
                ExampleCount = 1
            });
            await writer.WriteTrainingManifest("experiment.json", experiment);
            await writer.WriteReport("report.md", "docs", "# Report");

            Assert.Contains("\"input\":\"incident\"", File.ReadAllText(Path.Combine(root, "dataset_v2", "examples.jsonl")));
            Assert.Contains("\"Name\":\"Dataset V2\"", File.ReadAllText(Path.Combine(root, "dataset_v2", "metadata.json")));
            var manifest = JsonSerializer.Deserialize<TrainingExperiment>(
                File.ReadAllText(Path.Combine(root, "dataset_v2", "experiment.json")));

            Assert.NotNull(manifest);
            Assert.Equal("experiment", manifest!.ExperimentName);
            Assert.Equal("# Report\n", File.ReadAllText(Path.Combine(root, "docs", "report.md")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EvalFileManager_LoadsEvaluationAndManualEvaluationFiles()
    {
        var root = CreateTempDirectory();
        try
        {
            var outputs = Path.Combine(root, "finetuning", "outputs");
            var evaluations = Path.Combine(root, "evaluations");
            Directory.CreateDirectory(outputs);
            Directory.CreateDirectory(evaluations);
            File.WriteAllText(
                Path.Combine(outputs, "results.json"),
                "[{\"Input\":\"incident\",\"Expected\":\"expected\",\"Actual\":\"actual\"}]");
            File.WriteAllText(
                Path.Combine(evaluations, "manual.json"),
                "{\"ModelVersion\":\"V1\",\"Scores\":[]}");

            var manager = new EvalFileManager(new TestPathResolver(root));
            var results = manager.Load("results.json");
            var manual = manager.LoadManualEvaluation("manual.json", "evaluations");

            var result = Assert.Single(results);
            Assert.Equal("incident", result.Input);
            Assert.Equal("V1", manual.ModelVersion);
            Assert.Empty(manual.Scores);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ConsoleHostEnvironmentRegistration_ProvidesConfiguredValues()
    {
        var root = CreateTempDirectory();
        try
        {
            var services = new ServiceCollection();
            services.AddConsoleHostEnvironment("Testing", root);

            using var provider = services.BuildServiceProvider();
            var environment = provider.GetRequiredService<IHostEnvironment>();

            Assert.Equal("Testing", environment.EnvironmentName);
            Assert.Equal(Path.GetFullPath(root), environment.ContentRootPath);
            Assert.NotNull(environment.ContentRootFileProvider);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static DatasetWriter CreateWriter(string root)
    {
        return new DatasetWriter(
            new TestPathResolver(root),
            Options.Create(new DatasetEngineeringSettings
            {
                DataDirectory = "dataset_v2"
            }));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ai-engineering-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestPathResolver(string root) : IPathResolver
    {
        public string ResolveConfiguredPath(string configuredPath)
        {
            var path = Path.Combine(root, configuredPath);
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
