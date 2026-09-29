using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations.Providers
{
    public class QwenLoRaFinetunedManagement : IModelProvider
    {
        public Task<SendMessageResponseDto> SendMessage(SendMessageRequestDto request)
        {
            throw new NotImplementedException();
        }

        public Task<SendMessageResponseDto> SendMessageWithTools(SendMessageWithToolsDto request, string systemPrompt)
        {
            throw new NotImplementedException();
        }
    }
}
