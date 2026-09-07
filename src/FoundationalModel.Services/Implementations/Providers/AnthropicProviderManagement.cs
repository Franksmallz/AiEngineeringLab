using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
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
                    MaxTokens = request.MaxToken,
                    Model = Model.ClaudeSonnet5,
                    Messages = [
                      new MessageParam
                        {
                            Role = Role.User,
                            Content = request.Prompt,
                        },
                    ]
                    //Temperature  = request.Temperature,
                    //TopK = request.TopK,
                };

                sw.Start();
                var message = await _client.Messages.Create(parameters);
                var contents = new List<string>();

                foreach(var block in message.Content)
                {
                    if(block.TryPickText(out var textBlock))
                    {
                        contents.Add(textBlock.Text);
                    }
                }

                var text = contents != null ? string.Join(
                    "\n", contents) : string.Empty;

                _anthropicProviderSettings.Models.TryGetValue(Model.ClaudeSonnet5.ToString(), out var modelCost);
                response = AnthropicResponseMapper.CreateSuccess(
                    Model.ClaudeSonnet5.ToString(),
                    text, message.Usage.InputTokens,
                    message.Usage.OutputTokens,
                    sw.ElapsedMilliseconds,
                    modelCost,
                    request.Temperature,
                    request.Prompt);
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
    }
}
