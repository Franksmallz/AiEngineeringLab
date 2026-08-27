using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace FoundationalModel.Services.Implementations.Providers
{
    public class AnthropicProviderManagement : IModelProvider
    {
        private readonly AnthropicClient _client;
        private readonly AnthropicProviderSettings _anthropicProviderSettings;

        public AnthropicProviderManagement(AnthropicClient client,
            IOptionsMonitor<AnthropicProviderSettings> options)
        {
            _client = client;
            _anthropicProviderSettings = options.CurrentValue;
        }

        public async Task<SendMessageResponseDto> SendMessage(SendMessageRequestDto request)
        {
            var response = new SendMessageResponseDto();
            var sw = new Stopwatch();
            try
            {
                var parameters = new MessageCreateParams
                {
                    MaxTokens = _anthropicProviderSettings.MaxTokens,
                    Model = Model.ClaudeHaiku4_5,
                    Messages = [
                      new MessageParam
                        {
                            Role = Role.User,
                            Content = request.Prompt
                        },
                    ]
                };

                sw.Start();
                var message = await _client.Messages.Create(parameters);
                response = AnthropicResponseMapper.CreateSuccess(
                    message.Model,
                    message.Content is null
                        ? string.Empty
                        : string.Join("\n", message.Content.OfType<TextBlock>().Select(x => x.Text)),
                    message.Usage.InputTokens,
                    message.Usage.OutputTokens,
                    sw.ElapsedMilliseconds,
                    GetModelCost(Model.ClaudeHaiku4_5));
            }
            catch(AnthropicException ex)
            {
                response.ErrorMessage = ex.Message;
            }
            catch(Exception ex)
            {
                response.ErrorMessage = "Unable to process a message please try again later";
            }

            return response;

        }

        private ModelCost? GetModelCost(Model model)
        {
            _anthropicProviderSettings.Models.TryGetValue(model.ToString(), out var modelCost);
            return modelCost;
        }
    }
}
