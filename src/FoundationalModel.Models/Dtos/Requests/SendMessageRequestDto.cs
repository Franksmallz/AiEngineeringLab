namespace FoundationalModel.Models.Dtos.Requests
{
    public class SendMessageRequestDto
    {
        public string Prompt { get; set; }
        public double Temperature { get; set; }
        public int MaxToken { get; set; }
        public double TopP { get; set; }
        public long TopK { get; set; }

    }
}
