# ADR-001: Production Inference Configuration and Feedback Architecture

**Status:** Accepted  
**Date:** 2026-09-28  
**Decision Owners:** AI Engineering  
**System:** Payment Incident Classification and Action Recommendation Service

## Context

The payment-incident system uses a fine-tuned `Qwen/Qwen2.5-0.5B` model to classify payment incidents and returns a structured response containing:

```text
Category: ...
Retryable: Yes/No
Action: ...
```

The model was fine-tuned using LoRA and evaluated against a frozen 20-case payment-incident evaluation set.

Inference optimization was carried out across three production candidates:

- FP16 Batch 16
- INT8 Batch 16
- INT4 NF4 Batch 16

The objective was to improve inference performance while sustaining:

- throughput
- runtime
- GPU memory
- output stability
- semantic correctness
- operational reliability

A feedback feature was added which allows incorrect model outputs to be captured, reviewed, converted into approved training examples, then incorporated into future dataset and newer versions of models.

---

## Decision

The inference system in production will be running using the following Configuration:

```text
Model: Qwen2.5-0.5B + payment LoRA adapter
Precision: FP16
Batch size: 16
Max new tokens: 80
Sampling: Disabled
```

The Precision and BatchSize was chosen because it provided the balance between inference performance and behavioral stability after evaluation.

The system also contains the following feature:

- inference lineage tracking
- model deployment/version tracking
- structured-output validation
- fallback handling
- inference telemetry
- user/operator feedback capture
- human review for incorrect outputs
- generation of training-example from user-feedback
- versioned dataset export

Quantized configurations will stay in experimental phase until they are evaluated against a larger production-representative dataset.

---

## Evidence

### FP16 Batch 1 baseline

The FP16 Batch 1 configuration produced:

| Metric | Result |
|---|---:|
| Throughput | 19.17 tokens/sec |
| Total runtime | 37.97 sec |
| Peak GPU memory | 2,368.77 MB |
| Total generated tokens | 728 |

This configuration provided the baseline from which Inference optimization was based on.

### FP16 Batch 16

Increasing the batch size to 16 produced:

| Metric | Result |
|---|---:|
| Throughput | 91.54 tokens/sec |
| Total runtime | 7.95 sec |
| Amortized processing time | 397.66 ms/case |
| Peak GPU memory | 3,315.08 MB |
| Total generated tokens | 728 |
| Exact parity vs FP16 Batch 1 | 20/20 |

This represented approximately a **4.8× increase in throughput** while the 20 generated output remained same to the baseline output.

The significant trade-off was only increased GPU memory consumption.

### INT8 Batch 16

INT8 produced:

| Metric | Result |
|---|---:|
| Throughput | 60.89 tokens/sec |
| Total runtime | 26.28 sec |
| Peak GPU memory | 2,024.37 MB |
| Total generated tokens | 1,600 |
| Exact parity vs FP16 Batch 16 | 7/20 |
| Cases hitting token limit | 20/20 |

INT8 reduced GPU memory significantly, however, it performed worse in runtime and throughput under the dataset used.

It also produced significant drift in output generated.

### INT4 NF4 Batch 16

INT4 NF4 produced:

| Metric | Result |
|---|---:|
| Throughput | 174.25 tokens/sec |
| Total runtime | 9.18 sec |
| Peak GPU memory | 2,545.13 MB |
| Total generated tokens | 1,600 |
| Exact parity vs FP16 Batch 16 | 3/20 |
| Cases hitting token limit | 20/20 |

INT4 achieved the significant increase in throughput and significant reduction in memory relative to FP16.

However, all 20 cases generated went beyond th configured output token and only three responses matched the  expected output response. 
As a result record high drift in generated output response .

---

## Semantic Evaluation

Furthermore, semantic evaluation was carried out on the generated responses for each Configuration because exact-matching is not proof enough for generation drift.  

Hence, the three configurations were judged to see if their meaning correlated to the expected output response.

| Configuration | Category Correct | Retryable Correct | Action Correct | Contradiction / Hallucination |
|---|---:|---:|---:|---:|
| FP16 Batch 16 | 15/20 | 12/20 | 2/20 | 11/20 |
| INT8 Batch 16 | 15/20 | 13/20 | 4/20 | 10/20 |
| INT4 NF4 Batch 16 | 16/20 | 14/20 | 4/20 | 8/20 |

According to the results, the quantized models did not show clear semantic degradation on the 20 evaluation set.

However, the evaluation set was small, and both quantized configurations showed substantial changes in generation behavior.

As a result, semantic similarity alone was not enough to use a quantized model for production.

---

## Rationale

FP16 Batch 16 was chosen because it demonstrated strongest operational stability.

It preserved:

```text
20/20 exact behavioral parity
```

with the original FP16 baseline while increasing throughput significantly.

Although INT4 produced higher throughput, the system generated twice as many tokens and generated more the require max token.

The behavior introduced additional production risks:

- unpredictable response lengths
- increased GPU utilization
- higher tail latency
- repetition
- unstructured output
- behavioral uncertainity under large evaluation set

INT8 showed reduction in memory but was slower than FP16 for the tested workload.

Therefore, neither quantized configuration provided a strong overall advantage to replace FP16 Batch 16.

---

## Production Architecture Decision

Each model deployment will be represented by a versioned deployment record containing:

```text
DeploymentId
ModelName
ModelVersion
AdapterVersion
Precision
BatchSize
MaxNewTokens
DoSample
CreatedAt
```

Every inference will reference the exact deployment that generated it.

The lineage will be represented by:

```text
ModelDeployment
      ↓
InferenceRecord
      ↓
InferenceFeedback
      ↓
FeedbackReviewItem
      ↓
TrainingExampleCandidate
      ↓
TrainingDatasetVersion
```

This allows incorrect outputs to be traced to:

- the original input
- raw model output
- parsed model output
- exact model version
- adapter version
- inference configuration
- latency
- user feedback
- human correction
- future training example

---

## Structured Output Validation

Model output will not be trusted solely on completion of inference i.e successful generation of output.

Every response must return the expected structured output:

```text
Category
Retryable
Action
```

A parser validates the generated response before it is returned to the caller.

If the model fails to return a valid structured response, the system will return a fallback result:

```text
Category: Manual Review Required
Retryable: No
Action: Escalate the incident for manual review.
```

The original raw model output is also persisted for further investigation and malformed outputs will be recorded also as an observability signal.
.

---

## Feedback Decision

User feedback is not treated as training data automatically.

Incorrect responses move through the feedback pipeline:

```text
Inference
   ↓
Negative Feedback
   ↓
Pending Review
   ↓
Human Review
   ├── Rejected
   └── Approved
          ↓
TrainingExampleCandidate
```

Only approved corrections make it to future training dataset.

This prevents:

- incorrect user feedback from poisoning the dataset
- inconsistent corrections
- low-quality labels
- accidental changes to model behavior

---

## Dataset Versioning

Approved training candidates will be exported into versioned datasets.

Example:

```text
training-dataset-v3.jsonl
```

Each row will contain their Inference lineage:

```json
{
  "input": "...",
  "expected": "Category: ...\nRetryable: ...\nAction: ...",
  "sourceInferenceId": "...",
  "sourceFeedbackId": "..."
}
```

Hence, every future training dataset can be traced back to the production events that generated it.

---

## Observability

The production system tracks at minimum:

```text
Total inference requests
Average inference latency
Inference failures
Invalid structured outputs
Feedback rate
Positive feedback
Negative feedback
Approved corrections
Rejected corrections
```

These metrics are tracked per deployment.

Thereby, allowing different model versions and inference configurations to be compared using production behavior rather than only offline benchmarks.

---

## Alternatives Considered

### FP16 Batch 1

Rejected as the production configuration because Batch 16 produced substantially higher throughput without changing generated outputs.

### INT8 Batch 16

Not selected because it reduced memory but increased runtime. Although, it recorded increased throughput for the tested workload, the output token went beyond the approved max-token


### INT4 NF4 Batch 16

Not selected because despite its high token throughput.

It showed:

- low exact output parity
- 20/20 cases reached the token limit
- substantial behavioral drift

Although the semantic evaluation did not demonstrate clear quality degradation, the evaluation set was too small to conclude that the configuration was safe for production.

INT4 remains a candidate for future testing.

---

## Consequences

### Positive

The selected configuration provides:

- substantially higher throughput than the original baseline
- deterministic generation
- stable behavior relative to FP16 Batch 1
- simpler operational debugging
- predictable model behavior
- clear model lineage
- traceable feedback
- controlled dataset improvement
- safe handling of malformed model outputs

### Negative

FP16 consumes higher GPU memory than the quantized alternatives.

The selected configuration requires approximately:

```text
3.3 GB peak GPU memory
```

for the measured Batch 16 workload.

The feedback pipeline also introduced additional components such as:

- inference datastore
- feedback review mechanism
- dataset export management
- model deployment tracker
- monitoring

---

## Future Reconsideration Criteria

This decision will be revisited when one or more of the following occurs:

- the evaluation dataset grows significantly beyond 20 cases
- real production feedback becomes available
- GPU memory becomes a deployment constraint
- traffic volume requires greater inference throughput
- a larger or instruction-tuned model is introduced
- the fine-tuning dataset is expanded
- model output reliability improves
- quantized models demonstrate stable termination behavior
- INT4 or INT8 achieves acceptable production quality across a larger evaluation suite

A future quantized configuration should not be promoted solely because it is faster or uses less memory.

It must demonstrate acceptable performance across:

```text
latency
throughput
memory
semantic correctness
structured-output compliance
generation stability
production feedback
```

---

## Final Decision

**Use FP16 Batch 16 as the initial production inference configuration.**

Keep INT8 and INT4 NF4 as experimental Quantized candidates.

All production inference must be versioned, observable, validated, traceable, and connected to a human-reviewed feedback loop before corrections are allowed to influence future training datasets.
