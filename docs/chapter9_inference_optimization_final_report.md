# Chapter 9 — Inference Optimization Final Report

## Objective

The goal of this experiment was to optimize inference performance for the fine-tuned `Qwen/Qwen2.5-0.5B` payment-incident model while measuring the trade-offs such as:

- latency and throughput
- batching
- generation limits
- GPU memory consumption
- quantization
- output stability
- semantic quality

The same frozen **20-case payment incident evaluation set** was used  this experiment.

The model was expected to produce its output using the structure below:

```text
Category: ...
Retryable: Yes/No
Action: ...
```

---

## 1. Baseline

Overall the ground-zero for Chapter 9 was:

```text
Precision: FP16
Batch size: 1
max_new_tokens: 80
do_sample: false
Evaluation cases: 20
```

Measured performance:

| Metric | FP16 Batch 1 |
|---|---:|
| Average latency | ~1,898 ms |
| Throughput | 19.17 tokens/sec |
| Total generated tokens | 728 |
| Total run time | 37.97 sec |
| Peak GPU memory | 2,368.77 MB |

This established the reference for which the optimization will be based on.

---

## 2. Generation-Length Experiment

While every other Configuration remained fix `max_new_tokens` was varied.

| Max tokens | Avg latency | Avg generated tokens | Cases hitting limit |
|---:|---:|---:|---:|
| 40 | 1,445 ms | 27.55 | 6/20 |
| 60 | 1,713 ms | 32.75 | 4/20 |
| 80 | 1,898 ms | 36.40 | 3/20 |
| 100 | 2,069 ms | 39.40 | 3/20 |

Reduction of maximum token improved latency, however several cases were truncated, hencen impacting output response.

Furthermore, increasing the max_new_tokens did not resolve the underlying issue as few cases still had their response truncated.


### Decision

`max_new_tokens = 80` was retained as the canonical setting.

The experiment proved that there's correlation between response quality and max_new_tokens. Rather, it is directly proportional to inference cost.

---

## 3. Batching Experiment

The next step was to increase the batch size while other Configurations remained constant:

```text
Precision: FP16
max_new_tokens: 80
do_sample: false
same model
same 20 evaluation cases
```

Results:

| Batch size | Throughput | Total runtime | Amortized time/case | Peak memory |
|---:|---:|---:|---:|---:|
| 1 | 19.17 tok/s | 37.97 s | ~1,898 ms | 2,368.77 MB |
| 2 | 30.92 tok/s | 23.55 s | ~1,178 ms | 3,290.15 MB |
| 4 | 53.02 tok/s | 13.73 s | ~687 ms | 3,293.29 MB |
| 8 | 77.44 tok/s | 9.40 s | 470.05 ms | 3,300.57 MB |
| 16 | **91.54 tok/s** | **7.95 s** | **397.66 ms** | 3,315.08 MB |

FP16 Batch 16 Configuration produced an exact:

```text
20/20
```

output against the FP16 Batch 1 baseline.

### Finding

Batching produced the most Significant inference optimization.

From Batch 1 → Batch 16:

- throughput increased from **19.17 → 91.54 tokens/sec**
- total runtime fell from **37.97 → 7.95 seconds**
- generated behavior remained unchanged on all 20 cases

This represents roughly a **4.8× improvement in throughput** without drift in output generated.

However, there was a Significant increase in GPU memory consumption.

---

## 4. Quantization Experiment

For the quantization experiment **FP16 Batch 16**, was used as the baseline because batch size was fixed with variable Precision.

Configurations:

```text
FP16 Batch 16
INT8 Batch 16
INT4 NF4 Batch 16
```

All used:

```text
batch_size = 16
max_new_tokens = 80
do_sample = false
same LoRA adapter
same frozen evaluation set
```

---

## 5. INT8 Results

| Metric | FP16 Batch 16 | INT8 Batch 16 |
|---|---:|---:|
| Throughput | 91.54 tok/s | 60.89 tok/s |
| Total runtime | 7.95 s | 26.28 s |
| Peak memory | 3,315.08 MB | **2,024.37 MB** |
| Generated tokens | 728 | 1,600 |
| Cases hitting token limit | 3/20 | **20/20** |
| Exact match vs FP16 | Reference | 7/20 |

INT8 reduced gpu memory consumption by **39%**.

However:

- throughput reduced
- total runtime increased significantly
- every request exhausted the 80-token generation limit
- only 7/20 outputs exactly matched FP16

The INT8 model frequently generated long outputs.

### INT8 conclusion

INT8 was effective for **memory optimization**, but didn't have positive impact on speed.

---

## 6. INT4 NF4 Results

The INT4 Configuration used:

```python
BitsAndBytesConfig(
    load_in_4bit=True,
    bnb_4bit_quant_type="nf4",
    bnb_4bit_compute_dtype=torch.float16,
    bnb_4bit_use_double_quant=True
)
```

Results:

| Metric | FP16 Batch 16 | INT4 NF4 Batch 16 |
|---|---:|---:|
| Throughput | 91.54 tok/s | **174.25 tok/s** |
| Total runtime | 7.95 s | 9.18 s |
| Peak memory | 3,315.08 MB | **2,545.13 MB** |
| Generated tokens | 728 | 1,600 |
| Exact match vs FP16 | Reference | 3/20 |

INT4 produced the best throughput.

It achieved:

- approximately **90% higher raw throughput than FP16** and
- approximately **23% lower peak GPU memory than FP16**

However, every INT4 request also generated output beyond the max-token budget.

 The low `3/20` exact-match score initially indicated severe response quality degradation, However, exact-match was insufficient to make a conclusion as different wording can still mean the same answer semantically.

---

## 7. Semantic Quality Evaluation

All three configurations were therefore evaluated against the **ground-truth**, rather than against each other.

Each output was judged on:

```text
CategoryCorrect
RetryableCorrect
ActionCorrect
HasContradictionOrHallucination
```

With ai as Judge and Human review:

| Model | Category correct | Retryable correct | Action correct | Contradiction / hallucination |
|---|---:|---:|---:|---:|
| **FP16 Batch 16** | 15/20 | 12/20 | 2/20 | 11/20 |
| **INT8 Batch 16** | 15/20 | 13/20 | 4/20 | 10/20 |
| **INT4 NF4 Batch 16** | **16/20** | **14/20** | **4/20** | **8/20** |

This changed the interpretation of the quantization experiment.

The low exact-match scores that represented substantial **behavioral drift**, didn't necessarily equate to semantic degradation.

INT4 changed the wording considerably, yet it did not perform worse than FP16 semantically.

---

## 8. Important Model-Level Finding

The biggest problem was not quantization rather it was the model's inability to reliably produce the expected structured output.

Across all configurations, `ActionCorrect` remained poor:

```text
FP16: 2/20
INT8: 4/20
INT4: 4/20
```

The model recognized the incident correctly but failed to provide the required operational action.

This is consistent with the earlier fine-tuning findings:

- 50 training examples are insufficient for strong behavioral reliability
- the 0.5B base model has limited instruction/schema capacity
- repetition and hallucination remain present
- response-only loss improved the training objective but did not eliminate these limitations

Inference optimization cannot correct weaknesses learned during training.

---

## 9. Final Comparison

| Configuration | Throughput | Memory | Semantic quality | Behavior stability |
|---|---|---|---|---|
| FP16 Batch 1 | Low | Moderate | Reference quality | Stable |
| **FP16 Batch 16** | **High** | Highest | Reference quality | **20/20 parity** |
| INT8 Batch 16 | Lower | **Lowest** | Similar | Significant drift |
| INT4 NF4 Batch 16 | **Highest** | Lower than FP16 | Similar/slightly stronger on this eval | Largest drift |

---

## 10. Conclusions

### Batching was the strongest optimization

FP16 Batch 16 increased throughput from:

```text
19.17 → 91.54 tokens/sec
```

while maintaining:

```text
20/20 exact output parity
```

This provided a substantial performance improvement with no observed behavioral change.

### Quantization primarily changed the memory/performance trade-off

INT8 substantially reduced memory but was slower on this hardware/workload.

INT4 NF4 delivered the highest throughput and lower memory than FP16, but substantially changed generated outputs.

### Exact-match is not sufficient for generative model evaluation

INT4 achieved only:

```text
3/20 exact matches
```

against FP16, yet semantic evaluation showed comparable performance against ground truth.

Therefore, inference optimization should evaluate both:

```text
behavioral parity
+
semantic quality
```

rather than relying solely on string equality.

### Faster is not automatically better

An optimized configuration must be evaluated across:

```text
latency
throughput
memory
generation behavior
semantic correctness
```

A configuration that improves one metric can degrade another.

---

## Final Chapter 9 Decision

For this experiment, **FP16 Batch 16 is the safest optimized configuration**.

It provides:

- strong throughput
- substantially lower runtime than single-request inference
- complete behavioral parity with the original FP16 outputs
- no quantization-induced uncertainty

**INT4 NF4 is the most interesting performance candidate** because it produced the highest raw throughput while reducing memory footprint and preserving comparable semantic quality on this small evaluation set. However, because it showed substantial generation drift and all requests hit the token ceiling, it would require broader evaluation before being selected over FP16 Batch 16.

The main engineering lesson from Chapter 9 is therefore:

> **Inference optimization is a multi-objective problem, involving speed, memory, and quality. Changing one variable at a time so the cause of each improvement or regression is identifiable.**
