namespace FoundationalModel.Models.Dtos.Requests
{
    public class SendToolCallRequestDto
    {
        public string ToolUseId { get; set; }
        public string Result { get; set; }
        public object Tools { get; set; }
        public string Prompt { get; set; }
    }
}
