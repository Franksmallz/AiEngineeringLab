using System.Globalization;
using System.Text.Json;
using Anthropic;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Autofac;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var prompt = GetArgValue(args, "--prompt") ?? "Explain eventual consistency in exactly three bullet points";
//var temperatures = ResolveTemperatures(args);
//var values = ResolveMaxTokens(args);
//var values = ResolveTopP(args);
var values = ResolvePrompts(args);
var outputPath = ResolveOutputPath(args);

var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var anthropicSettings = configuration.GetSection(AnthropicProviderSettings.SectionName).Get<AnthropicProviderSettings>()
    ?? throw new InvalidOperationException($"Missing configuration section '{AnthropicProviderSettings.SectionName}'.");

if (string.IsNullOrWhiteSpace(anthropicSettings.ApiKey))
{
    throw new InvalidOperationException(
        "Anthropic API key is not configured. Set it via appsettings.Development.json or the " +
        "ModelProviderSeetings__Anthropic__ApiKey environment variable.");
}

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.Configure<AnthropicProviderSettings>(configuration.GetSection(AnthropicProviderSettings.SectionName));
services.AddScoped(_ => new AnthropicClient
{
    ApiKey = anthropicSettings.ApiKey,
    Timeout = TimeSpan.FromSeconds(anthropicSettings.Timeout),
    MaxRetries = anthropicSettings.MaxRetries,
});

var containerBuilder = new ContainerBuilder();
containerBuilder.Populate(services);
containerBuilder.RegisterModule<AutofacContainerModule>();
await using var container = containerBuilder.Build();
await using var scope = container.BeginLifetimeScope();

var generateService = scope.Resolve<IGenerateService>();

var results = new List<ExperimentRunResult>();
var runNumber = 1;

foreach (var value in values)
{
    Console.WriteLine($"Run {runNumber}/{values.Length}: value={value}");

    var request = new SendMessageRequestDto
    {
        Prompt = value,
        Temperature = 0.7,
        MaxToken = 1024,
        TopK = 0,
        TopP = 1.0,
    };

    var response = await generateService.SendMessage(request);

    if (!response.Success)
    {
        Console.WriteLine($"  Failed: {response.ErrorMessage}");
    }

    results.Add(new ExperimentRunResult(
        runNumber,
        request.Prompt,
        response.Text,
        response.InputTokens,
        response.OutputTokens,
        response.LatencyMs,
        request.Temperature,
        response.EstimatedCost,
        request.MaxToken,
        request.TopP,
        response.Success,
        response.ErrorMessage));

    runNumber++;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
});
await File.WriteAllTextAsync(outputPath, json);

Console.WriteLine($"Saved {results.Count} run(s) to {outputPath}");

static string? GetArgValue(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static double[] ResolveTemperatures(string[] args)
{
    var value = GetArgValue(args, "--temperatures");
    if (string.IsNullOrWhiteSpace(value))
    {
        return [0.0, 0.3, 0.7,1.0];
    }

    return value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture))
        .ToArray();
}

static string[] ResolvePrompts(string[] args)
{
    var value = GetArgValue(args, "--prompts");
    if (string.IsNullOrWhiteSpace(value))
    {
        return ["Explain eventual consistency in exactly three bullet points",
            "Explain idempotency and give use cases. Then go ahead to give real life examples and systems where idempotency can be used",
            "I am looking to go on vacation to europe. however i don't know if I have the entire money to spend. I am looking at a budget of 500 euro for 1 week. can you help me find top three hotels in paris where i can stay for one week and still have money for feeding and other curricular activies"
        ];
    }

    return value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static int[] ResolveMaxTokens(string[] args)
{
    var value = GetArgValue(args, "--maxtokens");
    if (string.IsNullOrWhiteSpace(value))
    {
        return [32, 64, 128, 256];
    }

    return value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(v => Convert.ToInt32(v, CultureInfo.InvariantCulture))
        .ToArray();
}

static double[] ResolveTopP(string[] args)
{
    var value = GetArgValue(args, "--topp");
    if (string.IsNullOrWhiteSpace(value))
    {
        return [1.0, 1.0, 1.0, 1.0,1.0];
    }

    return value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(v => Convert.ToDouble(v, CultureInfo.InvariantCulture))
        .ToArray();
}

static string ResolveOutputPath(string[] args)
{
    var explicitPath = GetArgValue(args, "--output");
    if (!string.IsNullOrWhiteSpace(explicitPath))
    {
        return Path.GetFullPath(explicitPath);
    }

    var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Could not locate the repository root. Pass --output explicitly.");

    return Path.Combine(repoRoot, "experiments", "week-02", "runs_variableprompts.json");
}

static string? FindRepoRoot(string startDirectory)
{
    var directory = new DirectoryInfo(startDirectory);
    while (directory is not null)
    {
        if (directory.GetFiles("*.slnx").Length > 0 || directory.GetFiles("*.sln").Length > 0)
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return null;
}

record ExperimentRunResult(
    int Run,
    string Prompt,
    string Text,
    long InputTokens,
    long OutputTokens,
    long LatencyMs,
    double Temperature,
    decimal EstimatedCost,
    int MaxtToken,
    double TopP,
    bool Success,
    string? ErrorMessage);
