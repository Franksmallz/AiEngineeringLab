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
    Id = 11,
    Input = "A multi-step biomimetic total synthesis of a complex meroterpenoid relies on a key tandem reaction. A solution of \\((2E,4E)\\)-6-hydroxy-2-methylhexa-2,4-dienal is treated with 20 mol % of \\((S)\\)-2-diphenyl(trimethylsilyloxy)methylpyrrolidine (a Hayashi-Jørgensen catalyst) and 1.5 equivalents of a 1,3-cyclohexadiene derivative acting as a diene in the presence of an optimized Brønsted acid co-catalyst at -78 °C in dichloromethane. Over 12 hours, the reaction smoothly proceeds to deliver a heavily functionalized bicyclic core. Extensive 2D-NMR (COSY, HSQC, HMBC, and NOESY) analysis combined with chiral HPLC reveals that the reaction proceeds with 98% ee and exclusive endo-selectivity, establishing three contiguous stereocenters (including a quaternary carbon) with the newly formed ring fused in a defined topology.However, when the bulky silyl ether on the catalyst is replaced with a less sterically demanding trimethylsilyl (TMS) group under identical conditions, the enantiomeric excess drops precipitously to 42% ee, and a significant proportion of the exo-adduct is isolated alongside a regioisomeric [2+2] cyclobutane side-product. Considering the formation of the transient chiral iminium ion intermediate, the conformational preference dictated by the steric bulk of the diaryl-substituted pyrrolidine ring, and the secondary orbital interactions during the concerted [4+2] cycloaddition transition state, which of the following mechanistic rationales best accounts for the observed high facial selectivity and endo-bias in the bulky silyl ether system? summarize the reason for your answer in three bullet points",
    ExpectedBehavior = [
      "Explain facial shiedling",
    "Maintains meaning after summary",
    "Gets the correct answer"
  ]
};

var response = await generateService.SendMessage(new SendMessageRequestDto { Prompt = evaluationCase.Input, Temperature = 0, MaxToken = 1024 });
evaluationCase.ActualResponse = System.Text.RegularExpressions.Regex.Unescape(response.Text);
evaluationCase.LatencyMs = response.LatencyMs;
evaluationCase.EstimatedCost = response.EstimatedCost;

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

    return Path.Combine(repoRoot, "evaluations", "week-03-04", "sonnet5-results.json");
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

