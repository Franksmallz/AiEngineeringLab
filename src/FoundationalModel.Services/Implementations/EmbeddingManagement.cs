using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Helpers;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FoundationalModel.Services.Implementations
{
    public class EmbeddingManagement : IEmbeddingManagement
    {
        private static readonly SemaphoreSlim CacheLock = new(1, 1);
        private static readonly SemaphoreSlim ResultLock = new(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private readonly ILogger<EmbeddingManagement> _logger;
        private readonly IEmbeddingService _embeddingService;

        public EmbeddingManagement(ILogger<EmbeddingManagement> logger, 
            IEmbeddingService embeddingService)
        {
            _logger = logger;
            _embeddingService = embeddingService;
        }

        public async Task EnsureEmbeddingsAsync(List<DocumentChunk> chunks, string path, CancellationToken cancellationToken)
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
                    var hash = chunk.Content.ToHexHash();
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
    }
}
