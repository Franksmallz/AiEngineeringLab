using Anthropic;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Autofac;
using FoundationalModel.Services.Hosting;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
var outputPath = ResolveOutputPath(args);
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
services.AddConsoleHostEnvironment(environmentName);
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

var evaluationCase = new EvaluationCase
{
    Id = 0,
    Input = string.Empty
};

var response = await generateService.SendMessage(new SendMessageRequestDto { Prompt = evaluationCase.Input, Temperature = 0, MaxToken = 1024 });
evaluationCase.ActualResponse = System.Text.RegularExpressions.Regex.Unescape(response.Text);
evaluationCase.LatencyMs = response.LatencyMs;
evaluationCase.EstimatedCost = response.EstimatedCost;
evaluationCase.InputToken = response.InputTokens;
evaluationCase.OutputToken = response.OutputTokens;

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var json = JsonSerializer.Serialize(evaluationCase, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
});
await File.AppendAllTextAsync(outputPath, json + Environment.NewLine);

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

static string ResolveOutputPath(string[] args)
{
    var explicitPath = GetArgValue(args, "--output");
    if (!string.IsNullOrWhiteSpace(explicitPath))
    {
        return Path.GetFullPath(explicitPath);
    }

   var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Could not locate the repository root. Pass --output explicitly.");

    return Path.Combine(repoRoot, "evaluations", "week-05", "v3-results-test.json");
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

