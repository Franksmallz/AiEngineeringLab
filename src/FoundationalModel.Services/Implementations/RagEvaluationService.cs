using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Core.Enums;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoundationalModel.Services.Implementations;

public sealed class RagEvaluationService : IRagEvaluationService
{
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static readonly SemaphoreSlim ResultLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly IDocumentChunkService _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRetrieverService _retriever;
    private readonly IVectorRetrieverService _vectorRetriever;
    private readonly IGenerateService _generator;
    private readonly IHostEnvironment _environment;
    private readonly RagSettings _settings;
    private readonly ILogger<RagEvaluationService> _logger;

    public RagEvaluationService(
        IDocumentChunkService chunker,
        IEmbeddingService embeddingService,
        IRetrieverService retriever,
        IVectorRetrieverService vectorRetriever,
        IGenerateService generator,
        IHostEnvironment environment,
        IOptions<RagSettings> settings,
        ILogger<RagEvaluationService> logger)
    {
        _chunker = chunker;
        _embeddingService = embeddingService;
        _retriever = retriever;
        _vectorRetriever = vectorRetriever;
        _generator = generator;
        _environment = environment;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RagCombinedResult> EvaluateAsync(RagEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExpectedAnswer);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExpectedSource);
        if (request.MatchingType == MatchingType.None)
            throw new ArgumentException("MatchingType must be semantic or direct.", nameof(request.MatchingType));

        var dataPath = ResolveConfiguredPath(_settings.DataDirectory);
        var chunks = await LoadChunksAsync(dataPath, cancellationToken);
        await EnsureEmbeddingsAsync(chunks, Path.Combine(dataPath, _settings.EmbeddingFileName), cancellationToken);

        var keywordChunks = _retriever.Retrieve(request.Question, chunks, _settings.TopK);
        var questionEmbedding = await _embeddingService.CreateEmbeddingAsync(request.Question);
        if (questionEmbedding is null || questionEmbedding.Length == 0)
            throw new InvalidOperationException("The embedding provider returned an empty question embedding.");

        var embeddingChunks = _vectorRetriever.Retrieve(questionEmbedding, chunks, _settings.TopK);
        var keywordResponse = await GenerateAsync(request.Question, keywordChunks, cancellationToken);
        var embeddingResponse = await GenerateAsync(request.Question, embeddingChunks, cancellationToken);

        var result = new RagCombinedResult
        {
            Question = request.Question,
            ExpectedAnswer = request.ExpectedAnswer,
            ExpectedSource = request.ExpectedSource,
            KeywordRetrievedChunks = keywordChunks.Select(ToResultChunk).ToList(),
            EmbeddingRetrievedChunks = embeddingChunks.Select(ToResultChunk).ToList(),
            KeywordResponse = keywordResponse.Text ?? "",
            EmbeddingResponse = embeddingResponse.Text ?? "",
            KeywordLatencyMs = keywordResponse.LatencyMs,
            EmbeddingLatencyMs = embeddingResponse.LatencyMs,
            KeywordEstimatedCost = keywordResponse.EstimatedCost,
            EmbeddingEstimatedCost = embeddingResponse.EstimatedCost,
            TotalLatencyMs = keywordResponse.LatencyMs + embeddingResponse.LatencyMs,
            TotalEstimatedCost = keywordResponse.EstimatedCost + embeddingResponse.EstimatedCost
        };

        var outputPath = ResolveConfiguredPath(_settings.ResultPath);
        await AppendResultAsync(result, outputPath, cancellationToken);
        return result;
    }

    private async Task<List<DocumentChunk>> LoadChunksAsync(string dataPath, CancellationToken cancellationToken)
    {
        var files = Directory.GetFiles(dataPath, "*.md").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0) throw new InvalidOperationException($"No markdown documents found in '{dataPath}'.");
        var chunks = new List<DocumentChunk>();
        foreach (var file in files)
        {
            var document = await File.ReadAllTextAsync(file, cancellationToken);
            var pieces = _chunker.ChunkDocument(document);
            for (var i = 0; i < pieces.Count; i++)
                chunks.Add(new DocumentChunk { Id = $"{Path.GetFileName(file)}-{i + 1}", Source = Path.GetFileName(file), Content = pieces[i] });
        }
        return chunks;
    }

    private async Task EnsureEmbeddingsAsync(List<DocumentChunk> chunks, string path, CancellationToken cancellationToken)
    {
        await CacheLock.WaitAsync(cancellationToken);
        try
        {
            var cache = new Dictionary<string, CachedEmbedding>(StringComparer.Ordinal);
            if (File.Exists(path))
            {
                try
                {
                    cache = JsonSerializer.Deserialize<Dictionary<string, CachedEmbedding>>(await File.ReadAllTextAsync(path, cancellationToken), JsonOptions)
                        ?? cache;
                }
                catch (JsonException ex) { _logger.LogWarning(ex, "Ignoring invalid embedding cache at {Path}.", path); }
            }

            var changed = false;
            foreach (var chunk in chunks)
            {
                var hash = Hash(chunk.Content);
                if (cache.TryGetValue(chunk.Id, out var cached) && cached.ContentHash == hash && cached.Embedding.Length > 0)
                    chunk.Embedding = cached.Embedding;
                else
                {
                    chunk.Embedding = await _embeddingService.CreateEmbeddingAsync(chunk.Content);
                    if (chunk.Embedding is null || chunk.Embedding.Length == 0)
                        throw new InvalidOperationException($"The embedding provider returned an empty embedding for '{chunk.Id}'.");
                    cache[chunk.Id] = new CachedEmbedding { ContentHash = hash, Embedding = chunk.Embedding };
                    changed = true;
                }
            }
            if (changed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporaryPath = path + ".tmp";
                await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(cache, JsonOptions), cancellationToken);
                File.Move(temporaryPath, path, true);
            }
        }
        finally { CacheLock.Release(); }
    }

    private async Task<FoundationalModel.Models.Dtos.Responses.SendMessageResponseDto> GenerateAsync(string question, IEnumerable<RetrievedChunk> chunks, CancellationToken cancellationToken)
    {
        var context = string.Join("\n\n---\n\n", chunks.Select(x => $"{x.Source}\n{x.Content}"));
        return await _generator.SendMessage(new SendMessageRequestDto
        {
            Prompt = $"Answer the question using only the context below. If the answer is not in the context, say I don't know. Do not add facts, assumptions, or examples that are not in the context.\n\nContext:\n{context}\n\nQuestion: {question}",
            MaxToken = _settings.MaxTokens
        });
    }

    private async Task AppendResultAsync(RagCombinedResult result, string path, CancellationToken cancellationToken)
    {
        await ResultLock.WaitAsync(cancellationToken);
        try
        {
            var results = new List<RagCombinedResult>();
            if (File.Exists(path))
            {
                try
                {
                    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken));
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                        results = JsonSerializer.Deserialize<List<RagCombinedResult>>(document.RootElement.GetRawText(), JsonOptions) ?? results;
                    else if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var existing = JsonSerializer.Deserialize<RagCombinedResult>(document.RootElement.GetRawText(), JsonOptions);
                        if (existing is not null) results.Add(existing);
                    }
                }
                catch (JsonException ex) { _logger.LogWarning(ex, "Ignoring invalid RAG result file at {Path}.", path); }
            }

            result.Id = results.Count == 0 ? 1 : results.Max(x => x.Id) + 1;
            results.Add(result);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(results, JsonOptions), cancellationToken);
        }
        finally { ResultLock.Release(); }
    }

    private static RagRetrievedChunk ToResultChunk(RetrievedChunk chunk) => new()
    {
        Source = chunk.Source,
        Content = chunk.Content,
        Score = chunk.Score
    };
    private string ResolveConfiguredPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath)) return configuredPath;
        var directory = new DirectoryInfo(_environment.ContentRootPath);
        while (directory is not null)
        {
            if (directory.GetFiles("*.slnx").Length > 0 || directory.GetFiles("*.sln").Length > 0)
                return Path.Combine(directory.FullName, configuredPath);
            directory = directory.Parent;
        }
        return Path.Combine(_environment.ContentRootPath, configuredPath);
    }

    private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    private sealed class CachedEmbedding { public string ContentHash { get; set; } = ""; public float[] Embedding { get; set; } = []; }
}
