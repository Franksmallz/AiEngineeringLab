namespace FoundationalModel.Services.Interfaces
{
    public interface IEmbeddingProvider
    {
        Task<float[]> CreateEmbeddingAsync(string text);
    }
}
