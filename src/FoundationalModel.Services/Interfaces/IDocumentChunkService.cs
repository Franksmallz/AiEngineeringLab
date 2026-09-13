namespace FoundationalModel.Services.Interfaces
{
    public interface IDocumentChunkService : IAutoDependencyService
    {
        List<string> ChunkDocument(string document, int maxCharacters = 1000);
    }
}
