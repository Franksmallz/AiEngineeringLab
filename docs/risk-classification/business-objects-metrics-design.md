# Objective & Metric Design

## Goal

Extend the transaction risk classifier to demonstrate how business objectives, ML metrics, decision thresholds, and operational constraints affect the design of an ML system.

## Starting Point

Previously the ML classifier achieved higher overall accuracy than the deterministic rules classifier, but performed poorly on High-risk classification.

This exposed a problem with optimizing only for overall model accuracy, because different prediction errors do not necessarily have equal business consequences.

## Business Objective

Reduce losses by flagging genuinely high-risk transactions while minimizing unnecessary friction by not flagging legitimate transactions.

## ML Objective

Increase High-risk predictions while minimizing non-high risk transactions being flagged as high-risk.

## Metrics

The experiment tracks:

- High-risk precision
- High-risk recall
- High-risk F1
- False positives
- False negatives
- Estimated business cost
- Percentage of transactions flagged High

## Business-Cost Model

For this experiment:

- False negative cost: $1,000
- False positive cost: $50

Estimated business cost is:

`FN × $1,000 + FP × $50`

These values are synthetic assumptions for demonstrating system-design tradeoffs and do not represent real fraud-loss or review costs.

## Default Model

Using the model's default multiclass decision produced:

- True positives: 2
- False positives: 6
- False negatives: 12
- High-risk recall: 14.3%
- High-risk precision: 25%
- High-risk F1: 18.2%
- Estimated business cost: $12,300

Although the classifier performed well on the majority of Medium-risk class, it detected only 2 out of the 14 genuine High-risk transactions.

## Threshold Optimization

Rather than selecting the class with the highest prediction score, the system applies a separate decision threshold to the High-risk prediction score.

A transaction is flagged High when:

`HighRiskScore >= threshold`

Lower thresholds generally increase High-risk recall while increasing false positives.

An unconstrained threshold search found that a threshold of `0.002` reduced estimated business cost to $5,000.

However, it flagged 93% of evaluation set as High-risk.

This would make the Objective impractical as a lot of transactions will be sent for additional review.

## Operational Constraint

The experiment therefore introduces a synthetic review-capacity constraint:

`Flag rate <= 40%`

Thresholds violating this constraint are considered infeasible regardless of their estimated business cost.

## Threshold Selection

Rather than testing random thresholds, candidate thresholds are derived from the actual High-risk scores produced on the evaluation dataset.

Every meaningful candidate is evaluated using the same metrics and business-cost function.

The selection rule is:

**Minimize estimated business cost subject to flag rate <= 40%.**

The selected threshold is:

`0.07525592`

Results:

- True positives: 7
- False positives: 32
- False negatives: 7
- True negatives: 54
- Precision: 17.95%
- Recall: 50%
- F1: 26.42%
- Flag rate: 39%
- Estimated business cost: $8,600

## Result

The selected policy reduces estimated cost from $12,300 to $8,600 while increasing High-risk recall from 14.3% to 50%.

The unconstrained threshold achieved lower estimated cost of $5,000, but violated the operational constraint.

## Key Learning

The best ML-system decision cannot necessarily be determined by model accuracy or even by a single ML metric.

This experiment produced different choices depending on the objective:

- Model accuracy favored the original ML classifier over the rules baseline.
- F1 favored one decision threshold.
- Estimated business cost favored a much more aggressive threshold.
- Operational capacity made that threshold infeasible.

The final decision therefore became a constrained optimization problem:

**Minimize business cost subject to operational capacity.**

The model itself was not retrained. The improvement came from changing how its predictions were translated into a business decision.

This demonstrates the separation between model behavior and system-level decision.