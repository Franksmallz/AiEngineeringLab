using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class TextSimilarityScorer : ITextSimilarityScorer
    {
        public double JaccardSimilarity(string first, string second)
        {
            var firstwords = Tokenize(first);
            var secondwords = Tokenize(second);

            if (firstwords.Count == 0 && secondwords.Count == 0) return 1.0;

            var intersection = firstwords.Intersect(secondwords).Count();
            var union = firstwords.Union(secondwords).Count();

            return union == 0 ? 0 : (double)intersection/union;
        }

        private HashSet<string> Tokenize(string text)
        {
            return text
                .ToLowerInvariant()
                .Split(
                    new[] { ' ', '.', ',', ':', ';', '-', '_', '/', '\\', '(', ')', '[', ']' },
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToHashSet();
        }
    }
}
