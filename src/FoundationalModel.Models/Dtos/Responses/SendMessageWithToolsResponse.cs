using System.Text.Json;

namespace FoundationalModel.Models.Dtos.Responses
{
    public class SendMessageWithToolsResponse
    {
        public string ToolName { get; set; }
        public string ToolUseId { get; set; }
        public string ToolInput { get; set; }
    }
}
