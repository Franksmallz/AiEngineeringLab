using Autofac;
using Autofac.Extensions.DependencyInjection;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Autofac;
using FoundationalModel.Services.Hosting;
using FoundationalModel.Services.Implementations;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
var fileManager = scope.Resolve<IEvalFileManager>();
var expectedAnswerParser = scope.Resolve<IExpectedAnswerParser>();
var schemaValidator = scope.Resolve<IEvaluationSchemaValidator>();
var structuredFieldEvaluator = scope.Resolve<IStructuredFieldEvaluator>();
var datasetWriter = scope.Resolve<IDatasetWriter>();
var reportGenerator = scope.Resolve<IEvaluationReportGenerator>();


var v2Results = fileManager.Load("finetuned-results-v2.json");
var v1Results = fileManager.Load("finetuned-results.json");

if(v1Results.Count != v2Results.Count)
{
    throw new InvalidOperationException("V1 and V2 do not contain the same number of evaluation cases.");
}

for(var i = 0; i< v1Results.Count; i++)
{
    if (!string.Equals(v1Results[i].Input, v2Results[i].Input, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Evaluation input mismatch at case {i + 1}.");
    }
}


Console.WriteLine("V1 and V2 use the same frozen evaluation cases");

var v1SchemaCompliant = v1Results
    .Count(x => schemaValidator.IsCompliant(x.Actual));

var v2SchemaCompliant = v1Results
    .Count(x => schemaValidator.IsCompliant(x.Actual));

var v1StructuredScores = v1Results
    .Select(x => structuredFieldEvaluator.Score(
        x.Expected,
        x.Actual))
    .ToList();

var v2StructuredScores = v1Results
    .Select(x => structuredFieldEvaluator.Score(
        x.Expected,
        x.Actual))
    .ToList();

Console.WriteLine();
Console.WriteLine(
    $"V1 exact category: {v1StructuredScores.Count(x => x.CategoryCorrect)}/{v1StructuredScores.Count}");

Console.WriteLine(
    $"V1 exact retryable: {v1StructuredScores.Count(x => x.RetryableCorrect)}/{v1StructuredScores.Count}");

Console.WriteLine(
    $"V1 exact action: {v1StructuredScores.Count(x => x.ActionCorrect)}/{v1StructuredScores.Count}");

Console.WriteLine();
Console.WriteLine(
    $"V1 exact category: {v2StructuredScores.Count(x => x.CategoryCorrect)}/{v2StructuredScores.Count}");

Console.WriteLine(
    $"V1 exact retryable: {v2StructuredScores.Count(x => x.RetryableCorrect)}/{v2StructuredScores.Count}");

Console.WriteLine(
    $"V1 exact action: {v2StructuredScores.Count(x => x.ActionCorrect)}/{v2StructuredScores.Count}");

var v1ManualTemplate = ManualEvaluation.Create("V1", v1Results);
var v2ManualTemplate = ManualEvaluation.Create("V2", v2Results);

await datasetWriter.WriteManualEvaluationTemplate("evaluations/week-08", "manual_evalution_v1.json", v1ManualTemplate);
await datasetWriter.WriteManualEvaluationTemplate("evaluations/week-08", "manual_evalution_v2.json", v2ManualTemplate);

var manualV1 = fileManager.LoadManualEvaluation("manual_evalution_v1.json", "evaluations/week-08");
var manualV2 = fileManager.LoadManualEvaluation("manual_evalution_v2.json", "evaluations/week-08");

var v1Summary = ManualEvaluationSummary.Calculate(manualV1);
var v2Summary = ManualEvaluationSummary.Calculate(manualV2);

PrintSummary(v1Summary);
PrintSummary(v2Summary);
var report = reportGenerator.Genrate(v1Summary, v2Summary, v1SchemaCompliant, v2SchemaCompliant);
await datasetWriter.WriteReport("dataset_engineering_summary.md", "docs", report);
static async Task PrintSummary(ManualEvaluationSummary summary)
{
    Console.WriteLine();
    Console.WriteLine($"===== {summary.ModelVersion} =====");

    Console.WriteLine(
        $"Category correct: {summary.CategoryCorrect}/{summary.TotalCases}");

    Console.WriteLine(
        $"Retryable correct: {summary.RetryableCorrect}/{summary.TotalCases}");

    Console.WriteLine(
        $"Action correct: {summary.ActionCorrect}/{summary.TotalCases}");

    Console.WriteLine(
        $"Contradictions/Hallucinations: " +
        $"{summary.ContradictionsOrHallucinations}/{summary.TotalCases}");
}