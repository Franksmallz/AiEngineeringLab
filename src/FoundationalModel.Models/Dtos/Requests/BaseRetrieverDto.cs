namespace FoundationalModel.Models.Dtos.Requests
{
    public class BaseRetrieverDto
    {
        public string Source { get; set; } = "";
        public string Content { get; set; } = "";
        public double Score { get; set; }
    }
}

