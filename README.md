# AI Engineering Hands-On

A small, production-minded ASP.NET Core API used to practise AI-assisted software engineering.

The repository is deliberately simple. Each exercise should leave behind working code, tests, an experiment record, and an evaluation. AI may help with exploration or implementation, but a human owns the final design, review, security decision, and test result.

## Highlights (measured results)

Every experiment here ends in numbers, not vibes. The headline evaluations so far:

**Model comparison: Haiku vs Sonnet** ([full report](evaluations/week-03-04/evaluation-summary.md))
An 11-case dataset scored 1–5 on correctness, relevance, and instruction-following, with latency and cost recorded automatically.

| Metric | Haiku | Sonnet |
|---|---:|---:|
| Overall quality | 4.45 | 4.64 |
| Avg latency | 5.07s | 7.21s |
| Avg cost per call | $0.00189 | $0.00581 |

Takeaway: Sonnet scored higher, but Haiku delivered ~96% of the quality at roughly a third of the cost and ~30% lower latency. The harder the instruction-following case, the wider the gap: Haiku once answered a question with contradictory answers and violated an "Italian only" instruction by adding an English explanation.

**RAG retrieval: keyword vs embedding** ([full report](experiments/week-06/chapter-6-rag-evaluation-report.md))
18-question evaluation over the same corpus, scored on retrieval relevance, correctness, and groundedness.

| Metric | Keyword | Embedding |
|---|---:|---:|
| Retrieval relevance | 3.22/5 | 4.83/5 |
| Correctness | 3.33/5 | 4.61/5 |
| Groundedness | 5.00/5 | 4.94/5 |

Takeaway: embedding retrieval never lost a retrieval-relevance case (13 wins, 5 ties), with the biggest gains on semantically paraphrased questions. Keyword retrieval still held its own on direct lexical matches, and generation cost differed by only ~2%.

Also in the lab: sampling-parameter experiments (temperature, top-p, max tokens, structured output, run-to-run consistency) in [`experiments/week-02`](experiments/week-02/), prompt versioning in [`experiments/week-05`](experiments/week-05/), and weekly reflection notes in [`docs`](docs/).

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
