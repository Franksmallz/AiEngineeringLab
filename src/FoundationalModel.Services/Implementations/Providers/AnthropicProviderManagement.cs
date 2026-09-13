using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using FoundationalModel.Models.Configs;
using FoundationalModel.Models.Dtos.Requests;
using FoundationalModel.Models.Dtos.Responses;
using FoundationalModel.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace FoundationalModel.Services.Implementations.Providers
{
    public class AnthropicProviderManagement : IModelProvider
    {
        private readonly AnthropicClient _client;
        private readonly AnthropicProviderSettings _anthropicProviderSettings;
        private readonly IExecuteAgenticToolService _executeAgenticToolService;

        public AnthropicProviderManagement(AnthropicClient client,
            IOptionsMonitor<AnthropicProviderSettings> options,
            IExecuteAgenticToolService executeAgenticToolService)
        {
            _client = client;
            _anthropicProviderSettings = options.CurrentValue;
            _executeAgenticToolService = executeAgenticToolService;
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
                    Model = Model.ClaudeHaiku4_5,
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

                _anthropicProviderSettings.Models.TryGetValue(Model.ClaudeHaiku4_5.ToString(), out var modelCost);
                response = AnthropicResponseMapper.CreateSuccess(
                    Model.ClaudeOpus5.ToString(),
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

        public async Task<SendMessageResponseDto> SendMessageWithTools(SendMessageWithToolsDto request)
        {
            try
            {
                var sw = new Stopwatch();

                var toolChoice = new ToolChoice(new ToolChoiceAuto { DisableParallelToolUse = true });

                var tools = request.Tools as List<ToolUnion>;

                var response = await _client.Messages.Create(new MessageCreateParams
                {
                    Tools = tools,
                    ToolChoice = toolChoice,
                    Messages = [new() { Role = Role.User, Content = request.Prompt }],
                    MaxTokens = _anthropicProviderSettings.MaxTokens,
                    Model = Model.ClaudeHaiku4_5_20251001
                });

                ToolUseBlock toolUse = null;

                foreach (var block in response.Content)
                {

                    if (block.TryPickToolUse(out var picked))
                    {
                        toolUse = picked;
                        break;
                    }
                }


                var result =  await _executeAgenticToolService.ExecuteToolAsync(toolUse.Name, toolUse.Input);


                List<ContentBlockParam> toolResults =
                [
                    new ContentBlockParam(new ToolResultBlockParam()
                {
                    ToolUseID = toolUse.ID,
                    Content = JsonSerializer.Serialize(result),
                }),
            ];

                var followup = await _client.Messages.Create(new MessageCreateParams
                {
                    Model = Model.ClaudeHaiku4_5_20251001,
                    MaxTokens = _anthropicProviderSettings.MaxTokens,
                    Tools = tools,
                    ToolChoice = toolChoice,
                    Messages =
                    [
                        new() { Role = Role.User, Content = request.Prompt },
                    new() { Role = Role.Assistant, Content = response.Content.Select(block => new ContentBlockParam(block.Json)).ToList() },
                    new() { Role = Role.User, Content = new MessageParamContent(toolResults) },
                ],
                });

                foreach (var block in followup.Content)
                {
                    if (block.TryPickText(out var output))
                    {
                        Console.WriteLine(output.Text);
                    }
                }

                var contents = new List<string>();

                foreach (var block in followup.Content)
                {
                    if (block.TryPickText(out var textBlock))
                    {
                        contents.Add(textBlock.Text);
                    }
                }

                var text = contents != null ? string.Join(
                    "\n", contents) : string.Empty;

                _anthropicProviderSettings.Models.TryGetValue(Model.ClaudeHaiku4_5.ToString(), out var modelCost);
                var followupResponse = AnthropicResponseMapper.CreateSuccess(
                    Model.ClaudeOpus5.ToString(),
                    text, followup.Usage.InputTokens,
                    followup.Usage.OutputTokens,
                    sw.ElapsedMilliseconds,
                    modelCost,
                    request.Temperature,
                    request.Prompt);

                return followupResponse;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
