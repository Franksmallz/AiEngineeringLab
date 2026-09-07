# AI Engineering Hands-On

A small, production-minded ASP.NET Core API used to practise AI-assisted software engineering.

The repository is deliberately simple. Each exercise should leave behind working code, tests, an experiment record, and an evaluation. AI may help with exploration or implementation, but a human owns the final design, review, security decision, and test result.

## Repository layout

| Folder | Purpose |
| --- | --- |
| `src/` | API and application code |
| `tests/` | Automated tests that protect behaviour |
| `experiments/` | experiments on the behaviour of the models on different runs; record the question and response |
| `evaluations/` | Repeatable evaluations for quality, correctness, safety, and AI output |
| `docs/` | Design notes, decisions, API contracts, and learning notes |


## Architecture

- Controller -> GenerateService -> IModelProvider -> AnthropicModelProvider -> ClaudeMessagesSDK -> NormalizedResponse

- Controller - The endpoint for prompting the model configured for a response.
- GenerateService - The service layer that resolves to the configured provider to process the prompts. Uses Autofac keyed resolver to resolve to the implementation for the configured provider.
- IModelProvider - The interface that all model providers must impelement for sending prompts.
- ClaudeMessagesSDK - The official Anthropic SDK that allows us to process a prompt using one of the official anthropic models.
- Runner - The service to test various sampling properties of the configured model.
- Evaluation.Runner - The evaluation harness for evaluating different models using configured datasets

## Run it

```powershell
dotnet restore AiEngineeringLab.slnx --ignore-failed-sources
dotnet run --project src/FoundationalModel.API
##ensure that all configurations are available on appsettings
```

Then visit:

- `GET /api/v1/system/health` - for system health
- `GET /api/v1/system/info` - for system information
- `POST /api/generate - to prompt the configured model for response


## Test it

```powershell
dotnet test AiEngineeringLab.slnx --no-restore
```

## Current API contract

Request : {
  "prompt": "The question or message to be sent to the model"
}

Response: {
  "model": "The model used to process the request",
  "inputTokens": the amount of tokens used to process the input message - Int64,
  "outputTokens": the amount of token used to generate the output response - Int64,
  "latencyMs": the duration of the api request to the time a response comes back - Int64,
  "estimatedCost": the cost of processing the message - decimal,
  "success": true,
  "errorMessage": "string",
  "text": "Response"
}
