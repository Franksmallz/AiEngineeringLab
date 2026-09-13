using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Implementations.Providers;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Implementations
{
    public class GenerateService : IGenerateService
    {
        private readonly IImplementationResolverService _implementationResolverService;

        public GenerateService(IImplementationResolverService implementationResolverService)
        {
            _implementationResolverService = implementationResolverService;
        }

        public async Task<SendMessageResponseDto> SendMessage(SendMessageRequestDto request)
        {
            var providerImplementation = _implementationResolverService.ResolveProvider(Core.Enums.Providers.ANTHROPIC);
            return await providerImplementation.SendMessage(request);
        }

        public async Task<SendMessageResponseDto> SendMessageWithTools(SendMessageRequestDto request)
        {
            var providerImplementation = _implementationResolverService.ResolveProvider(Core.Enums.Providers.ANTHROPIC);

            var messageWithTools = new SendMessageWithToolsDto
            {
                Prompt = request.Prompt,
                Tools = ProviderAgenticTools.PaymentTools
            };
            return await providerImplementation.SendMessageWithTools(messageWithTools);
        }
    }
}
