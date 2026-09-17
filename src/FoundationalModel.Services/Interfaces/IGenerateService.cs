using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;

namespace FoundationalModel.Services.Interfaces
{
    public interface IGenerateService : IAutoDependencyService
    {
        Task<SendMessageResponseDto> SendMessage(SendMessageRequestDto request);
        Task<SendMessageResponseDto> SendMessageWithTools(SendMessageRequestDto request);
    }
}
