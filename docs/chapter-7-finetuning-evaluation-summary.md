# Chapter 7 Fine-Tuning Evaluation Summary

## Objective

Compare the behavior of the **base Qwen2.5-0.5B model** against the **LoRA fine-tuned version** on the same 20 frozen payment-incident evaluation cases.

The target output format was:

```text
Category: ...
Retryable: ...
Action: ...
```

## High-Level Result

The fine-tuned model showed a clear improvement in task understanding compared with the base model.

The base model often behaved like a generic text-completion model. It frequently repeated the incident, generated unrelated prose, produced arbitrary JSON or HTTP responses, or continued the text instead of classifying the payment issue.

The fine-tuned model was more likely to understand the payment scenario and respond with something relevant to the incident.

However, the fine-tuned model still did not consistently follow the required structured output format.

## Comparison

| Area | Base Model | Fine-Tuned Model |
|---|---|---|
| Understands incident meaning | Weak / inconsistent | Clearly improved |
| Gives relevant action | Rarely | More often |
| Repetition / continuation behavior | Very common | Reduced |
| Exact category label | Poor | Improved conceptually, but often not explicit |
| `Retryable` field | Almost never | Still not reliably emitted |
| Exact 3-field format | Poor | Still poor |
| Hallucinations | Frequent | Reduced, but still present |

## What Improved

The fine-tuned model became more task-aware.

It handled scenarios such as:

- insufficient funds
- invalid beneficiary details
- provider unavailability
- expired cards
- pending provider confirmation

more sensibly than the base model.

This shows that the fine-tuning process successfully shifted the model toward the intended payment-incident classification behavior.

## Remaining Problems

The fine-tuned model still had several weaknesses.

### 1. Output-format adherence

The expected response format was:

```text
Category: ...
Retryable: ...
Action: ...
```

But the fine-tuned model frequently responded in ordinary prose instead.

This means the model learned some of the task semantics, but did not learn the response schema strongly enough.

### 2. Incorrect reasoning on some cases

Some difficult cases were still wrong.

Examples included:

- a maintenance scenario where the model incorrectly said the transaction had been submitted
- a duplicate-payment scenario where the model incorrectly said the original payment was not completed
- a debit-without-final-status scenario where the model invented a completed transfer, receipt, and confirmation email

### 3. Hallucination

Fine-tuning reduced hallucination compared with the base model, but did not eliminate it.

This reinforces the idea that fine-tuning changes model behavior but does not guarantee factual correctness.

## Main Learning

The experiment demonstrated that fine-tuning can shift a model toward a specific task even with a small dataset.

However, 50 training examples over 3 epochs were not enough to make Qwen2.5-0.5B reliably:

- emit the exact expected format
- classify every difficult case correctly
- avoid unsupported information

The result showed that dataset quality and design are just as important as the training process itself.

## Chapter 7 Conclusion

Fine-tuning changed the model's behavior in the intended direction.

The fine-tuned model became more task-aware and produced more relevant payment-incident responses than the base model.

However, a small 50-example dataset was not enough to reliably enforce the target structured output format or eliminate incorrect generations.

The experiment demonstrated that fine-tuning can improve behavioral consistency, but its effectiveness depends heavily on:

- dataset size
- dataset diversity
- quality of target outputs
- coverage of edge cases
- evaluation methodology

## Next Experiment

The next step should not simply be increasing the number of epochs.

A better next experiment is to improve the dataset by:

- adding more training examples
- increasing variation within each category
- including harder and ambiguous cases
- keeping the output structure extremely consistent
- adding examples that distinguish similar incident types
- testing whether the model generalizes to unseen cases

This naturally leads into **Chapter 8: Dataset Engineering**.

## Key Takeaway

```text
Fine-tuning does not magically make a model correct.

It shifts behavior toward patterns represented in the training data.

Better training data
→ better learned behavior
→ better evaluation results
```
