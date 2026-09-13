namespace FoundationalModel.Services.Interfaces
{
    public interface IEmbeddingService : IAutoDependencyService
    {
        Task<float[]> CreateEmbeddingAsync(string text);
    }
}
