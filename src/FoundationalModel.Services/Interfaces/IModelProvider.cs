using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IModelProvider
    {
        public Task<SendMessageResponseDto> SendMessage(SendMessageRequestDto request);
        public Task<SendMessageResponseDto> SendMessageWithTools(SendMessageWithToolsDto request);
    }
}
