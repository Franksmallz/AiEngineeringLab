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

**Fine-tuning: dataset quality, V1 vs V2** ([full report](docs/dataset_engineering_summary.md))
50 payment-incident examples across 10 categories each, trained on Qwen2.5-0.5B. V1 used full-sequence loss; V2 added scenario/action diversity, context-sensitive retryability, and response-only loss (prompt tokens masked). Both evaluated on the same frozen 20-case set with manual per-case scoring.

| Metric | V1 | V2 |
|---|---:|---:|
| Category correct | 16/20 | 15/20 |
| Retryable correct | 8/20 | 9/20 |
| Action correct | 9/20 | 7/20 |
| Schema compliant | 0/20 | 0/20 |
| Contradiction / hallucination | 6/20 | 7/20 |

Takeaway: better dataset structure and a more appropriate training objective did not improve overall performance — at 50 examples on a 0.5B model, dataset quality alone wasn't enough. The honest negative result points at the next lever: supervision density (multiple examples per scenario pattern), not just curation.

**Inference optimization: batching vs quantization** ([full report](docs/chapter9_inference_optimization_final_report.md))
Qwen2.5-0.5B payment-incident model, same frozen 20-case eval set. First batching (FP16, batch 1 → 16), then INT8 and INT4 NF4 quantization at batch 16.

| Metric | FP16 Batch 1 | FP16 Batch 16 | INT8 Batch 16 | INT4 NF4 Batch 16 |
|---|---:|---:|---:|---:|
| Throughput | 19.17 tok/s | 91.54 tok/s | 60.89 tok/s | 174.25 tok/s |
| Total runtime | 37.97 s | 7.95 s | 26.28 s | 9.18 s |
| Peak GPU memory | 2,369 MB | 3,315 MB | 2,024 MB | 2,545 MB |
| Exact match vs FP16 | — | 20/20 | 7/20 | 3/20 |
| Category correct (semantic) | 15/20 | 15/20 | 15/20 | 16/20 |
| Action correct (semantic) | 2/20 | 2/20 | 4/20 | 4/20 |

Takeaway: batching was the strongest optimization — ~4.8× throughput with 20/20 output parity. INT4 delivered the highest raw throughput at lower memory but with heavy output drift; semantic evaluation (AI judge + human review) showed drift is not the same as degradation — 3/20 exact match, yet comparable quality to FP16. Decision: FP16 Batch 16 is the safest config. The honest finding carries over from fine-tuning: inference optimization cannot fix training weaknesses — ActionCorrect stayed poor (2–4/20) across every configuration.

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

## Fine-tuning demo

The `finetuning/` directory contains a Hugging Face baseline for `Qwen/Qwen2.5-0.5B`, plus training and evaluation scaffolds. Install its dependencies and run the baseline from the repository root:

```powershell
python -m pip install -r requirements-finetuning.txt
python finetuning/baseline.py
```

The model is downloaded from Hugging Face on first run.
