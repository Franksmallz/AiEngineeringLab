#  Class imbalance — Training Data

## Goal

Determine whether the payment-risk classifier can be improved by tweaking its training data.

The model algorithm, feature set, training configuration, and frozen evaluation dataset were kept unchanged for this experiment.

> Can we improve High-risk classification by improving training data and not changing the model?

## Starting Point

The original training dataset contains 300 transactions:

| Risk Level | Count | Percentage |
|---|---:|---:|
| Low | 54 | 18% |
| Medium | 204 | 68% |
| High | 42 | 14% |

The frozen evaluation dataset contains 100 transactions:

| Risk Level | Count |
|---|---:|
| Low | 18 |
| Medium | 68 |
| High | 14 |

The baseline ML.NET SDCA Maximum Entropy classifier achieved:

- Accuracy: 77%
- High-risk precision: 25.0%
- High-risk recall: 14.3%
- High-risk F1: 18.2%

Although overrall accuracy was relatively high, the model detected only 2 of the 14 High-risk transactions in the evaluation set.

This suggested that the High-risk class might underrepresented thereby affecting how the learning of the model.

## Training Data Profile

The original training data showed diversity between the classes.

High-risk transactions generally had higher transaction amounts, higher transaction frequency, more recent failures, and a greater occurrence of high-risk countries than Low-risk transactions.

However, the feature ranges for Medium and High risk transactions overlapped considerably.

High-risk classification therefore depends on combinations of signals rather than a single feature boundary.

## Experiment 1 — Random Oversampling

The first intervention increased the population of the High-risk class without introducing new information.

The original 42 High-risk examples were randomly sampled with replacement until there were 204 High-risk training rows.

The resulting training dataset contained:

| Risk Level | Count |
|---|---:|
| Low | 54 |
| Medium | 204 |
| High | 204 |

Total: 462 rows.

Only 42 of the 204 High-risk rows were unique.

The modified training sets were used to train the model, and the model was evaluated againsted the same frozen 100 evaluation set
### Results

- Accuracy: 52%
- High-risk precision: 19.1%
- High-risk recall: 64.3%
- High-risk F1: 29.5%

The model detected 9 out of the 14 High-risk transactions, compared with only 2 detected by the baseline.

However, it also classified 38 of the non-High transactions as High incorrectly.

Therefore the experiment changed the model's error tradeoff rather than improving the model. Hence, High-risk recall improved substantially, while overall accuracy and High-risk precision declined.

## Experiment 2 — Additional Unique High-Risk Data

The second experiment tested whether adding new High-risk examples would improve coverage rather than duplicating the original 42 examples.

We generated new transactions using the original synthetic labeling process and selected only 162 transactions labelled as high-risk.

The new set were added to the original 42 High-risk examples:

- 42 original High-risk examples
- 162 newly generated High-risk examples
- 204 unique High-risk examples in total

The resulting training dataset had the same high risk class population and total size as the oversampling experiment:

| Risk Level | Count |
|---|---:|
| Low | 54 |
| Medium | 204 |
| High | 204 |

Total: 462 rows.

However, the examples were Uniquely generated rather than a duplicate of the 42 set.

### Results

- Accuracy: 43%
- High-risk precision: 14.3%
- High-risk recall: 71.4%
- High-risk F1: 23.8%
- Transactions predicted High: 70 of 100

The model detected 10 of the 14 actual High-risk transactions, the highest High-risk recall of the three experiments.

However, it also classified 60 non-High transactions as High.

The confusion matrix was:

| Actual ↓ / Predicted → | Low | Medium | High |
|---|---:|---:|---:|
| Medium | 0 | 27 | 41 |
| High | 0 | 4 | 10 |
| Low | 4 | 5 | 9 |

Adding unique High-risk examples therefore improved minority-class coverage, however, changing the population of the training data caused the model to increase it's likely hood of prediciting transactions as high.

Although the test improved the recall metrics, alternatively it increased the false positives while reducing overall accuracy.

## Error Analysis

Furthermore, to determine whether insufficient High-risk coverage was the only problem, the baseline model was evaluated against the High-risk class present in the original training dataset.

Of the 42 High-risk training examples:

- 3 were classified correctly as High.
- 38 were classified as Medium.
- 1 was classified as Low.

The model therefore correctly classified only approximately 7.1% of the baseline training examples.

The misclassifications were not confined to one obvious subtype, but included different combinations of:

- high transaction amounts,
- high transaction frequency,
- multiple recent failures,
- high-risk countries,
- early transaction hours,
- very young beneficiaries.

Thereby suggesting that simply collecting more examples may not address the entire problem.

## Data-Generating Process

Inspection of the synthetic labeling function showed that risk is determined from several additive signals and an explicit interaction between beneficiary age and transaction amount:

```csharp
if (transaction.BeneficiaryAgeDays < 7 &&
    transaction.Amount > 3000)
{
    score += 1.0;
}
```

The classifier receives the individual raw features but does not explicitly receive this interaction as a feature.

That creates an important boundary for this experiment: Chapter 4 changes the training data only. Feature engineering or changing the classifier would introduce another experimental variable which is intentionally deferred to the model-development stage.

## Comparison

| Experiment | Accuracy | High Precision | High Recall | High F1 |
|---|---:|---:|---:|---:|
| Baseline | 77% | 25.0% | 14.3% | 18.2% |
| Random oversampling | 52% | 19.1% | 64.3% | 29.5% |
| Additional unique High data | 43% | 14.3% | 71.4% | 23.8% |

The unique-data experiment produced the highest High-risk recall, while holding the lowest overall accuracy and precision.

As a result neither of the two experiment was a succesful improvement.

## Key Learning

Training-data composition can significantly change model behavior even when the model and training configuration remain unchanged.

The original dataset underrepresented High-risk transactions. Increasing their representation substantially improved High-risk recall:

- 14.3% with the original data
- 64.3% after duplicating existing High-risk examples
- 71.4% after adding unique High-risk examples

But improving minority-class coverage came with a cost.

The unique-data model predicted High for 70 of the 100 evaluation transactions even though only 14 were actually High. Better coverage of the minority class therefore improved sensitivity while creating many false positives.

This demonstrates that "more data" or "more balanced data" is not automatically better. The composition of the training data changes what errors the model learns to avoid.

Error analysis also showed that the baseline classifier failed on diverse High-risk patterns already present in its own training data. This suggests that data quantity is not the only bottleneck.

> Improve the data when the data is the bottleneck. Stop when the evidence suggests the bottleneck has moved elsewhere.

Model selection and feature-representation changes are intentionally deferred to the model-development stage.

## Limitations

The evaluation dataset contains only 100 transactions and only 14 High-risk examples. A single High-risk prediction therefore changes High-risk recall by approximately 7.1 percentage points.

The unique-data experiment adds only High-risk examples rather than increasing all classes proportionally. It therefore measures the effect of targeted minority-class data collection and the resulting distribution shift in the training set.

The dataset and business scenario are synthetic. The reported metrics should be interpreted as experimental results, not real-world payment-risk performance.
