# Chapter 8 Dataset Engineering Experiment

## Objective

Evaluate whether improved dataset diversity and response-only loss improve payment-incident fine-tuning performance.

## Dataset Changes

- V1: 50 examples across 10 categories.
- V2: 50 examples across the same 10 categories.
- V2 increased scenario diversity and action diversity.
- V2 introduced context-sensitive retryability where appropriate.
- Exact duplicates and train/eval leakage were not found.

## Training Change

- V1 used full-sequence loss.
- V2 used response-only loss by masking prompt tokens with -100.

## Evaluation Results

| Metric | V1 | V2 |
|---|---:|---:|
| Category correct | 16/20 | 15/20 |
| Retryable correct | 8/20 | 9/20 |
| Action correct | 9/20 | 7/20 |
| Schema compliant | 0/20 | 0/20 |
| Contradiction / hallucination | 6/20 | 7/20 |

## Conclusion

V2 improved dataset quality and used a more appropriate training objective, but it did not improve overall model performance on the frozen evaluation set.

The experiment suggests that better dataset structure alone was insufficient with only 50 examples and the Qwen2.5-0.5B base model.

The next experiment should increase supervision density by providing multiple examples for each scenario pattern while keeping the frozen evaluation set unchanged.

