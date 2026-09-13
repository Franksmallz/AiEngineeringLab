using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class DocumentChunkService : IDocumentChunkService
    {
        public List<string> ChunkDocument(string document, int maxCharacters = 1000)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);

            var paragraphs = document.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

            var chunks = new List<string>();
            var currentChunk = "";

            foreach (var paragraph in paragraphs)
            {
                var normalizedParagraph = paragraph.Trim();
                if (normalizedParagraph.Length == 0)
                {
                    continue;
                }

                if (normalizedParagraph.Length > maxCharacters)
                {
                    if (!string.IsNullOrWhiteSpace(currentChunk))
                    {
                        chunks.Add(currentChunk.Trim());
                    }

                    currentChunk = "";
                    for (var offset = 0; offset < normalizedParagraph.Length; offset += maxCharacters)
                    {
                        chunks.Add(normalizedParagraph.Substring(offset, Math.Min(maxCharacters, normalizedParagraph.Length - offset)));
                    }
                }
                else if (string.IsNullOrEmpty(currentChunk))
                {
                    currentChunk = normalizedParagraph;
                }
                else if (currentChunk.Length + 2 + normalizedParagraph.Length <= maxCharacters)
                {
                    currentChunk += "\n\n" + normalizedParagraph;
                }
                else
                {
                    chunks.Add(currentChunk.Trim());
                    currentChunk = normalizedParagraph;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
            }

            return chunks;
        }
    }
}
