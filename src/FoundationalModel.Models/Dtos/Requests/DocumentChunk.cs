namespace FoundationalModel.Models.Dtos.Requests
{
    public class DocumentChunk
    {
        public string Id { get; set; } = "";
        public string Source { get; set; } = "";
        public string Content { get; set; } = "";
        public float[] Embedding { get; set; } = [];
    }
}
