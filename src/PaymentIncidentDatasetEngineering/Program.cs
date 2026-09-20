using Autofac;
using Autofac.Extensions.DependencyInjection;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
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
services.Configure<DatasetEngineeringSettings>(configuration.GetSection(DatasetEngineeringSettings.SectionName));

var datasetEngineeringSettings = configuration.GetSection(DatasetEngineeringSettings.SectionName).Get<DatasetEngineeringSettings>()
    ?? throw new InvalidOperationException($"Missing configuration section '{DatasetEngineeringSettings.SectionName}'.");


var containerBuilder = new ContainerBuilder();
containerBuilder.Populate(services);
containerBuilder.RegisterModule<AutofacContainerModule>();
await using var container = containerBuilder.Build();
await using var scope = container.BeginLifetimeScope();

var pathResolver = scope.Resolve<IPathResolver>();
var dataLoader = scope.Resolve<IDataLoader>();
var trainingDataParser = scope.Resolve<ITrainingDataParser>();
var datasetValidator = scope.Resolve<IDatasetValidator>();
var textSimilarityScorer = scope.Resolve<ITextSimilarityScorer>();
var datasetWriter = scope.Resolve<IDatasetWriter>();
var datasetAuditService = scope.Resolve<IDatasetAuditService>();
var datasetReportGenerator = scope.Resolve<IDatasetReportGenerator>();


var dataset = dataLoader.Load(datasetEngineeringSettings.TrainingDataFileName);

var parsedDataset = dataset
    .Select(trainingDataParser.Parse)
    .ToList();

var issues = datasetValidator.Validate(parsedDataset);

Console.WriteLine($"Training examples: {parsedDataset.Count}");
Console.WriteLine($"Validation issues: {issues.Count}");

var categoryCounts = parsedDataset
    .GroupBy(x => x.Category)
    .Select(group => new
    {
        Category = group.Key,
        Count = group.Count()
    })
    .OrderByDescending(x => x.Count)
    .ThenBy(x => x.Category)
    .ToList();

Console.WriteLine();
Console.WriteLine("Category distribution");

foreach(var item in categoryCounts)
{
    Console.WriteLine($"{item.Category}: {item.Count}");
}

var expectedCountPerCategory = 5;

var imbalanceCategories = categoryCounts
    .Where(x => x.Count != expectedCountPerCategory)
    .ToList();


if(imbalanceCategories.Count == 0)
{
    Console.WriteLine("Dataset is balanced across categories");
}
else
{
    Console.WriteLine("Imbalanced categories found:");
    foreach(var item in imbalanceCategories)
    {
        Console.WriteLine($"{item.Category}: {item.Count}");
    }
}

var duplicateInputs = parsedDataset
    .GroupBy(x => x.Input.Trim(), StringComparer.OrdinalIgnoreCase)
    .Where(group => group.Count() > 1)
    .Select(group => new
    {
        Input = group.Key,
        Count = group.Count()
    })
    .ToList();

if(duplicateInputs.Count == 0)
{
    Console.WriteLine("No exact duplicate inputs found");
}
else
{
    Console.WriteLine("Duplicate inputs found:");

    foreach(var duplicate in duplicateInputs)
    {
        Console.WriteLine();
        Console.WriteLine($"Count: {duplicate.Count}");
        Console.WriteLine($"Input: {duplicate.Input}");
    }
}

const double similarityThreshold = 0.65;

var similarPairs = new List<(string Category, string First, string Second, double score)>();

foreach(var categoryGroup in parsedDataset.GroupBy(x => x.Category))
{
    var examples = categoryGroup.ToList();

    for (var i = 0; i < examples.Count; i++)
    {
        for (var j = i + 1; j < examples.Count; j++)
        {
            var score = textSimilarityScorer.JaccardSimilarity(examples[i].Input, examples[j].Input);

            if (score >= similarityThreshold)
            {
                similarPairs.Add(
                    (
                        categoryGroup.Key,
                        examples[i].Input,
                        examples[j].Input,
                        score
                    ));
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Highle similar pairs: {similarPairs.Count}");

    foreach(var pair in similarPairs)
    {
        Console.WriteLine();
        Console.WriteLine($"Category: {pair.Category}");
        Console.WriteLine($"Similarity: {pair.score:P0}");
        Console.WriteLine($"1: {pair.First}");
        Console.WriteLine($"2: {pair.Second}");
    }

    var retryableByCategory = parsedDataset
        .GroupBy(x => x.Category)
        .Select(group => new
        {
            Category = group.Key,
            RetryAbleValues = group
            .Select(x => x.Retryable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        })
        .ToList();

    Console.WriteLine();
    Console.WriteLine("Retryable consistency:");

    foreach(var item in retryableByCategory)
    {
        Console.WriteLine($"{item.Category}: {string.Join(", ", item.RetryAbleValues)}");
    }

    var inconsistentRetryableCategories = retryableByCategory
        .Where(x => x.RetryAbleValues.Count() > 1)
        .ToList();

    Console.WriteLine() ;   

    if(inconsistentRetryableCategories.Count == 0)
    {
        Console.WriteLine("Retryable labels are consistent within each category.");
    }
    else
    {
        Console.WriteLine("Inconsistent Retryable labels found:");

        foreach (var item in inconsistentRetryableCategories)
        {
            Console.WriteLine($"{item.Category}: {string.Join(", ", item.RetryAbleValues)}");
        }
    }

    var actionsByCategory = parsedDataset
        .GroupBy(x => x.Category)
        .Select(group => new
        {
            Category = group.Key,
            Actions = group
            .Select(x => x.Action.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        })
        .OrderBy(x => x.Category)
        .ToList();

    Console.WriteLine();
    Console.WriteLine("Action variation by category:");

    foreach(var item in actionsByCategory)
    {
        Console.WriteLine();
        Console.WriteLine($"{item.Category} ({item.Actions.Count} unique actions");

        foreach(var action in item.Actions)
        {
            Console.WriteLine($"_{action}");
        }
    }

    var categoriesWithSingleAction = actionsByCategory
        .Where(x => x.Actions.Count == 1)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"Categories with only one unique action: {categoriesWithSingleAction.Count}");

    foreach(var item in categoriesWithSingleAction)
    {
        Console.WriteLine($"{item.Category}: {item.Actions[0]}");
    }


    var categoryToInspect = "Provider Timeout";

    var examplesForCategory = parsedDataset
        .Where(x => string.Equals(x.Category, categoryToInspect, StringComparison.OrdinalIgnoreCase))
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"Inspecting category: {categoryToInspect}");

    for(var i = 0; i < examplesForCategory.Count; i++)
    {
        var example = examplesForCategory[i];
        Console.WriteLine();
        Console.WriteLine($"Example {i + 1}");
        Console.WriteLine($"Input: {example.Input}");
        Console.WriteLine($"Retryable: {example.Retryable}");
        Console.WriteLine($"Action: {example.Action}");
    }

    var datasetV2 = DatasetScenario.Scenarios
        .Select(x => TraininExampleV2.Create(
                x.Input,
                x.Category,
                x.Retryable,
                x.Action
            ))
        .ToList();

    foreach(var example in datasetV2)
    {
        Console.WriteLine();
        Console.WriteLine(example.Input);
        Console.WriteLine(example.Output);
    }

    var filename = "dataset_v2.jsonl";
    await datasetWriter.WriteTrainingV2Jsonl(filename, datasetV2);

    Console.WriteLine();
    Console.WriteLine($"V2 dataset written with filename: {filename}");

    var v1 = dataLoader.Load("train.jsonl")
        .Select(trainingDataParser.Parse)
        .ToList();
    var v2 = dataLoader.Load(filename)
        .Select(trainingDataParser.Parse)
        .ToList();

    datasetAuditService.PrintSummary("V1", v1);
    datasetAuditService.PrintSummary("V2", v2);


    var evaluationSet = dataLoader.Load("eval.jsonl");

    var v2Inputs = v2
        .Select(x => x.Input.Trim())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var leakedExamples = evaluationSet
        .Where(x => v2Inputs.Contains(x.Input.Trim()))
        .ToList();

    foreach (var example in leakedExamples)
    {
        Console.WriteLine(example.Input);
    }

    const double leakageThreshold = 0.65;

    var possibleLeakage = new List<(string Train, string Eval, double Score)>();

    foreach(var trainExample in v2)
    {
       foreach(var evalExample in evaluationSet)
        {
            var score = textSimilarityScorer.JaccardSimilarity(trainExample.Input, evalExample.Input);

            if(score >= leakageThreshold)
            {
                possibleLeakage.Add(
                    (
                        trainExample.Input,
                        evalExample.Input,
                        score
                    ));
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Potential near train/eval leakage: {possibleLeakage.Count}");

    foreach(var item in possibleLeakage)
    {
        Console.WriteLine();
        Console.WriteLine($"Similarity: {item.Score:P0}");
        Console.WriteLine($"Train: {item.Train}");
        Console.WriteLine($"Eval: {item.Eval}");
    }

    var metadata = new DatasetMetadata
    {
        Name = "Payment Incident Training Dataset",
        Version = "2.0",
        ExampleCount = v2.Count,
        CategoryCount = v2
         .Select(x => x.Category)
         .Distinct(StringComparer.OrdinalIgnoreCase)
         .Count(),

        CreatedAtUtc = DateTime.UtcNow,

        Description =
         "Improved payment incident dataset with increased scenario diversity, " +
         "context-sensitive actions, retryability variation, and boundary cases.",

        Source ="Derived from payment incident dataset V1 during dataset engineering."
    };

    await datasetWriter.WriteDatasetMetadataJson("dataset_v2_metadata.json", metadata);

    var reportV1 = datasetReportGenerator.Generate("Payment Incident Dataset V1", v1);

    await datasetWriter.WriteDatasetReport("dataset_v1_quality_report.md", reportV1);

    var reportV2 = datasetReportGenerator.Generate("Payment Incident Dataset V2", v2);

    await datasetWriter.WriteDatasetReport("dataset_v2_quality_report.md", reportV2);

    Console.WriteLine("Dataset quality report generated");
}