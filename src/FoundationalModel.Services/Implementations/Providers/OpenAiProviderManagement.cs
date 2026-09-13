using FoundationalModel.Services.Interfaces;
using OpenAI.Embeddings;

namespace FoundationalModel.Services.Implementations.Providers
{
    public class OpenAiProviderManagement : IEmbeddingProvider
    {
        private readonly EmbeddingClient _embeddingClient;
        public OpenAiProviderManagement(EmbeddingClient embeddingClient)
        {
            _embeddingClient = embeddingClient;
        }

        public async Task<float[]> CreateEmbeddingAsync(string text)
        {
            try
            {

                var embedding = await _embeddingClient.GenerateEmbeddingAsync(text);

                return embedding.Value.ToFloats().ToArray();
            }
            catch(Exception ex)
            {
                return null;
            }
        }

    }
}
