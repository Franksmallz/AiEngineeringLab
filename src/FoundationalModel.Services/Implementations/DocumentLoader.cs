using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace FoundationalModel.Services.Implementations
{
    public class DocumentLoader : IDocumentLoader
    {
        private readonly IDocumentChunkService _chunkService;
        private readonly IPathResolver _pathResolver;
        private readonly RagSettings _settings;
        private readonly IEmbeddingManagement _embeddingManagement;


        public DocumentLoader(IDocumentChunkService chunkService,
            IPathResolver pathResolver,
            IOptions<RagSettings> options,
            IEmbeddingManagement embeddingManagement)
        {
            _chunkService = chunkService;
            _pathResolver = pathResolver;
            _settings = options.Value;
            _embeddingManagement = embeddingManagement;
        }

        public async Task<List<DocumentChunk>> LoadChunksAsync(CancellationToken cancellationToken)
        {
            var dataPath = _pathResolver.ResolveConfiguredPath(_settings.DataDirectory);
            var files = Directory.GetFiles(dataPath, "*.md").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) throw new InvalidOperationException($"No markdown documents found in '{dataPath}'.");
            var chunks = new List<DocumentChunk>();
            foreach (var file in files)
            {
                var document = await File.ReadAllTextAsync(file, cancellationToken);
                var pieces = _chunkService.ChunkDocument(document);
                for (var i = 0; i < pieces.Count; i++)
                    chunks.Add(new DocumentChunk { Id = $"{Path.GetFileName(file)}-{i + 1}", Source = Path.GetFileName(file), Content = pieces[i] });
            }

            await _embeddingManagement.EnsureEmbeddingsAsync(chunks, Path.Combine(dataPath, _settings.EmbeddingFileName), cancellationToken);
            return chunks;
        }
    }
}
