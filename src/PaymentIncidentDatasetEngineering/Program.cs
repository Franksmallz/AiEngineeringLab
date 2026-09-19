using Autofac;
using Autofac.Extensions.DependencyInjection;
using FoundationalModel.Models.Configs;
using FoundationalModel.Services.Autofac;
using FoundationalModel.Services.Hosting;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
}