using Autofac;
using Autofac.Extensions.DependencyInjection;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Autofac;
using FoundationalModel.Services.Hosting;
using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddConsoleHostEnvironment(environmentName);

var containerBuilder = new ContainerBuilder();
containerBuilder.Populate(services);
containerBuilder.RegisterModule<AutofacContainerModule>();
await using var container = containerBuilder.Build();
await using var scope = container.BeginLifetimeScope();

var pathResolver = scope.Resolve<IPathResolver>();
var dataWriter = scope.Resolve<IInferenceBenchmarkWriter>();


var baselineConfig = new InferenceExperimentConfig
{
    ExperimentName = "baseline-fp16",
    ModelName = "Qwen2.5-0.5B-payment",
    EvaluationFile = "evaluation.jsonl",
    BatchSize = 1,
    MaxNewTokens = 128,
    Temperature = 0,
    TopP = 1.0,
    DoSample = false,
    Precision = "fp16",
    WarmupRuns = 2,
    Repetitions = 3,
    OutputFile = "baseline-fp16-result.json"
};

var path = pathResolver.ResolveConfiguredPath("experiments/chapter-09");

var options = new JsonSerializerOptions
{
    WriteIndented = true,
};

var json = JsonSerializer.Serialize(baselineConfig, options);

await dataWriter.Write(json, path, "baseline-fp-16.json");

var fp16Results = JsonSerializer.Deserialize<List<InferenceResult>>(
    File.ReadAllText(Path.Combine(path, "baseline-fp16-batch16-outputs.json"))
)!;

var int8Results = JsonSerializer.Deserialize<List<InferenceResult>>(
    File.ReadAllText(Path.Combine(path, "int8-batch16-outputs.json"))
)!;

var int4Results = JsonSerializer.Deserialize<List<InferenceResult>>(
    File.ReadAllText(Path.Combine(path, "int4-batch16-outputs.json"))
    )!;


var int8Evaluation = CreateEvaluationTemplate(int8Results);
var int4Evaluation = CreateEvaluationTemplate(int4Results);
var fp16Evaluation = CreateEvaluationTemplate(fp16Results);

var evalPath = pathResolver.ResolveConfiguredPath("evaluations/chapter-09");

File.WriteAllText(
    Path.Combine(evalPath, "int8-semantic-evaluation.json"),
    JsonSerializer.Serialize(int8Evaluation, options)
);

File.WriteAllText(
    Path.Combine(evalPath, "int4-semantic-evaluation.json"),
    JsonSerializer.Serialize(int4Evaluation, options)
);

File.WriteAllText(
    Path.Combine(evalPath, "fp-16-batch-16-semantic-evaluation.json"),
    JsonSerializer.Serialize(fp16Evaluation, options)
);


static List<SemanticEvaluation> CreateEvaluationTemplate(
    IEnumerable<InferenceResult> results)
{
    return results.Select(x => new SemanticEvaluation
    {
        Input = x.Input,
        Expected = x.Expected,
        Actual = x.Actual
    }).ToList();
}


var fp16JudgedResults = JsonSerializer.Deserialize<List<SemanticEvaluation>>(
    File.ReadAllText(Path.Combine(evalPath, "fp-16-batch-16-semantic-evaluation-judged.json"))
)!;

var int8JudgedResults = JsonSerializer.Deserialize<List<SemanticEvaluation>>(
    File.ReadAllText(Path.Combine(evalPath, "int4-semantic-evaluation-judged.json"))
)!;

var int4JudgedResults = JsonSerializer.Deserialize<List<SemanticEvaluation>>(
    File.ReadAllText(Path.Combine(evalPath, "int8-semantic-evaluation-judged.json"))
    )!;


PrintSummary("FP16 Batch 16", fp16JudgedResults);
PrintSummary("INT8 Batch 16", int8JudgedResults);
PrintSummary("INT4 Batch 16", int4JudgedResults);


static void PrintSummary(
    string experimentName,
    List<SemanticEvaluation> evaluations)
{
    Console.WriteLine(experimentName);
    Console.WriteLine(
        $"Category: {evaluations.Count(x => x.CategoryCorrect)}/{evaluations.Count}"
    );
    Console.WriteLine(
        $"Retryable: {evaluations.Count(x => x.RetryableCorrect)}/{evaluations.Count}"
    );
    Console.WriteLine(
        $"Action: {evaluations.Count(x => x.ActionCorrect)}/{evaluations.Count}"
    );
    Console.WriteLine(
        $"Hallucination/Contradiction: " +
        $"{evaluations.Count(x => x.HasContradictionOrHallucination)}/{evaluations.Count}"
    );
}