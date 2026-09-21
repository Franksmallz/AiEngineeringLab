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
using OpenAI.Embeddings;
using System.Text.Encodings.Web;
using System.Text.Json;

var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
    ?? throw new InvalidOperationException("Could not locate the repository root.");
var outputPath = ResolveOutputPath(args, repoRoot);
var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var anthropicSettings = configuration.GetSection(AnthropicProviderSettings.SectionName).Get<AnthropicProviderSettings>()
    ?? throw new InvalidOperationException($"Missing configuration section '{AnthropicProviderSettings.SectionName}'.");

var openAiSettings = configuration.GetSection(OpenAiProviderSettings.SectionName).Get<OpenAiProviderSettings>()
    ?? throw new InvalidOperationException($"Missing configuration section '{OpenAiProviderSettings.SectionName}'.");

if (string.IsNullOrWhiteSpace(anthropicSettings.ApiKey))
{
    throw new InvalidOperationException(
        "Anthropic API key is not configured. Set it via appsettings.Development.json or the " +
        "ModelProviderSeetings__Anthropic__ApiKey environment variable.");
}

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddConsoleHostEnvironment(environmentName, repoRoot);
services.Configure<AnthropicProviderSettings>(configuration.GetSection(AnthropicProviderSettings.SectionName));
services.Configure<OpenAiProviderSettings>(configuration.GetSection(OpenAiProviderSettings.SectionName));
services.AddScoped(_ => new AnthropicClient
{
    ApiKey = anthropicSettings.ApiKey,
    Timeout = TimeSpan.FromSeconds(anthropicSettings.Timeout),
    MaxRetries = anthropicSettings.MaxRetries,
});

services.AddScoped(_ => new EmbeddingClient(openAiSettings.Model, openAiSettings.ApiKey));

var containerBuilder = new ContainerBuilder();
containerBuilder.Populate(services);
containerBuilder.RegisterModule<AutofacContainerModule>();
await using var container = containerBuilder.Build();
await using var scope = container.BeginLifetimeScope();

var generateService = scope.Resolve<IGenerateService>();
var documentChunkService = scope.Resolve<IDocumentChunkService>();
var embeddingService = scope.Resolve<IEmbeddingService>();
var vectorRetriever = scope.Resolve<IVectorRetrieverService>();

var files = Directory.GetFiles(Path.Combine(repoRoot, "data"), "*.md");

var allChunks = new List<DocumentChunk>();
foreach (var file in files)
{
    var document = await File.ReadAllTextAsync(file);
    var chunks = documentChunkService.ChunkDocument(document);

    for (int i = 0; i < chunks.Count; i++)
    {
        allChunks.Add(new DocumentChunk
        {
            Id = $"{Path.GetFileNameWithoutExtension(file)}-{i + 1}",
            Source = Path.GetFileName(file),
            Content = chunks[i]
        });
    }
}

foreach (var chunk in allChunks)
{
    chunk.Embedding = await embeddingService.CreateEmbeddingAsync(chunk.Content);
}

Console.WriteLine("Ask a question, or press Ctrl+C to exit.");

while (true)
{
    Console.Write("\n> ");
    var question = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(question))
    {
        break;
    }

    Console.Write("Expected answer: ");
    var expectedAnswer = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(expectedAnswer))
    {
        break;
    }

    var questionEmbedding = await embeddingService.CreateEmbeddingAsync(question)
        ?? throw new InvalidOperationException("The embedding provider returned an empty question embedding.");
    var retrievedChunks = vectorRetriever.Retrieve(questionEmbedding, allChunks);
    var context = string.Join("\n\n---\n\n", retrievedChunks.Select(x => $"{x.Source}\n{x.Content}"));
    var input = $"Answer the question using only the context below. If the answer is not in the context, say I don't know. Do not add facts, assumptions, or examples that are not in the context\n\nContext:\n{context}\n\nQuestion: {question}";

    var response = await generateService.SendMessage(new SendMessageRequestDto
    {
        Prompt = input,
        MaxToken = anthropicSettings.MaxTokens
    });

    var ragEvaluationResult = new RagEvaluationResult
    {
        ActualResponse = System.Text.RegularExpressions.Regex.Unescape(response.Text),
        EstimatedCost = response.EstimatedCost,
        InputTokens = response.InputTokens,
        OutputTokens = response.OutputTokens,
        LatencyMs = response.LatencyMs,
        RetrievedChunks = retrievedChunks,
        Question = question,
        ExpectedAnswer = expectedAnswer

    };

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    var json = JsonSerializer.Serialize(ragEvaluationResult, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });
    await File.AppendAllTextAsync(outputPath, json + Environment.NewLine);
}

static string ResolveOutputPath(string[] args, string repoRoot)
{
    var explicitPath = GetArgValue(args, "--output");
    if (!string.IsNullOrWhiteSpace(explicitPath))
    {
        return Path.GetFullPath(explicitPath);
    }

    return Path.Combine(repoRoot, "experiments", "week-06", "rag-embeddings-results.json");
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
