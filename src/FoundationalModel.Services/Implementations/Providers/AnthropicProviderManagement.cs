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
        private const int maxToolCalls = 6;

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

        public async Task<SendMessageResponseDto> SendMessageWithTools(SendMessageWithToolsDto request, string systemPrompt)
        {
            try
            {
                var sw = new Stopwatch();

                var toolChoice = new ToolChoice(new ToolChoiceAuto { DisableParallelToolUse = true });

                var tools = request.Tools as List<ToolUnion>;
               
                List<MessageParam> messages = [new() { Role = Role.User, Content = request.Prompt }];

                var response = await _client.Messages.Create(new MessageCreateParams
                {
                    Tools = tools,
                    ToolChoice = toolChoice,
                    System = string.IsNullOrWhiteSpace(systemPrompt) ? null : new MessageCreateParamsSystem(systemPrompt),
                    Messages = messages,
                    MaxTokens = _anthropicProviderSettings.MaxTokens,
                    Model = Model.ClaudeHaiku4_5_20251001
                });


                int toolCalls = 0;
                while (response.StopReason == StopReason.ToolUse && toolCalls <= maxToolCalls)
                {
                    // A single response can contain multiple tool_use blocks. Process all of
                    // them and return all results together in one user message.
                    List<ContentBlockParam> toolResults = [];
                    foreach (var block in response.Content)
                    {
                        if (block.TryPickToolUse(out var toolUse))
                        {
                            toolResults.Add(new ContentBlockParam(new ToolResultBlockParam()
                            {
                                ToolUseID = toolUse.ID,
                                Content = JsonSerializer.Serialize(await _executeAgenticToolService.ExecuteToolAsync(toolUse.Name, toolUse.Input, request.User))
                            }));
                        }
                    }

                    messages.Add(new()
                    {
                        Role = Role.Assistant,
                        Content = response.Content.Select(block => new ContentBlockParam(block.Json)).ToList(),
                    });
                    messages.Add(new() { Role = Role.User, Content = new MessageParamContent(toolResults) });

                    response = await _client.Messages.Create(new MessageCreateParams
                    {
                        Model = Model.ClaudeHaiku4_5,
                        MaxTokens = 1024,
                        Tools = tools,
                        Messages = messages,
                    });
                }

                if(toolCalls <= maxToolCalls)
                {
                    foreach (var block in response.Content)
                    {
                        if (block.TryPickText(out var output))
                        {
                            Console.WriteLine(output.Text);
                        }
                    }

                    var contents = new List<string>();

                    foreach (var block in response.Content)
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
                        Model.ClaudeHaiku4_5_20251001.ToString(),
                        text, response.Usage.InputTokens,
                        response.Usage.OutputTokens,
                        sw.ElapsedMilliseconds,
                        modelCost,
                        request.Temperature,
                        request.Prompt);

                    return followupResponse;
                }

                throw new InvalidOperationException("Maximum tool-call reached");
            }
            catch (Exception ex)
            {
                return new SendMessageResponseDto
                {
                    ErrorMessage = ex.Message,
                };
            }
        }
    }
}
