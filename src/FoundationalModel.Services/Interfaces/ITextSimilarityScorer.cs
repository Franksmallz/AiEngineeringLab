namespace FoundationalModel.Services.Interfaces
{
    public interface ITextSimilarityScorer : IAutoDependencyService
    {
        public double JaccardSimilarity(string first, string second);
    }
}
