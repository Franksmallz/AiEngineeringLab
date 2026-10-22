# DMLS Chapter 6 — Model Optimization

## Research question

> Which model, preprocessing and decision threshold produces the lowest business cost for High-risk transactions? Does that choice hold on data that took no part in the training?

The business objective is asymmetric:

| Error | Meaning | Cost |
|---|---|---:|
| False negative (FN) | a High-risk transaction is missed | $1,000 |
| False positive (FP) | a transaction is flagged High by mistake | $50 |

**Business cost = FN × $1,000 + FP × $50.** High is the positive class. A policy may flag at most 40% of transactions as High (the Chapter 2 constraint).

All numbers in this document come from `evaluations/risk-classification/chapter-06-results.json`.

## Result in one table

| Stage | Model and policy | High recall | High precision | Business cost |
|---|---|---:|---:|---:|
| Baseline | SDCA, 6 raw features, default prediction | 2.2% | 2.6% | $228,550 (5,000 CV) |
| Normalization | SDCA, 10 features, normalized, default prediction | 74.6% | 89.8% | $57,950 (5,000 CV) |
| Threshold | the same model, nested threshold selection | 100% | 57.9% | $8,150 (5,000 CV) |
| **Final test** | **frozen model, threshold 0.1121** | **100% (44/44)** | **61.1%** | **$1,400 (1,000 test)** |

On the test set, the threshold policy cost $1,400. The model's default prediction rule cost $11,400 on the same records, so the threshold policy cost 87.7% less.

## 1. Dataset and splits

- **Generator:** `RiskDatasetGenerator`, seed 42, 6,000 records. The label noise bug was fixed (`score += noise`; the old code added a constant 1.0). There is no oversampling.
- **Split:** the first 5,000 records go to `data/risk-classification/chapter-06/train-5000.csv`, and the next 1,000 go to `test-1000.csv`.
- **Checks:** there are 0 duplicates inside each file, 0 duplicates across the two files, and 0 overlap with the original 400 records. A duplicate means all six features are equal.

| File | Low | Medium | High | Total |
|---|---:|---:|---:|---:|
| train-5000.csv | 2,567 | 2,209 | 224 | 5,000 |
| test-1000.csv | 525 | 431 | 44 | 1,000 |

**Cross-validation:** `DatasetSplitter.StratifiedKFold`, 5 folds, seed 42. Each class is shuffled with Fisher-Yates and dealt round-robin. Every fold holds exactly 1,000 records and 44 or 45 High records. Every experiment below uses these same folds.

**Aggregation:** the metrics come from all 5,000 out-of-fold predictions together, not from an average of the folds. With about 45 High records per fold, an average of small ratios would add noise.

**Test set rule:** step 1 read the test file only to count its classes and to check for duplicates. No model, threshold or choice used it until the final test.

## 2. Evaluation harness

`CrossValidationEvaluator` trains a new model for each fold. It reads the class of each `Score` slot from the model's slot names. It then stores each score by class name, so no code depends on a slot position. The trainer is passed in as a function, so every model runs through the same folds, features and metrics.

**Reproducibility controls:**

- SDCA runs with `NumberOfThreads = 1`. Multi-threaded SDCA gave a different model on every run, even with a fixed seed. The first multi-threaded baseline was discarded.
- LightGBM runs with `Seed = 42` and `NumberOfThreads = 1`. Its auto-tuned values are frozen in code: `LearningRate` 0.2, `NumberOfLeaves` 20, `MinimumExampleCountPerLeaf` 6, `UseSoftmax` false, `UseCategoricalSplit` false, and `NumberOfIterations` 100.
- Each fold uses a new `MLContext(seed: 42)` and a seeded `ShuffleRows`.
- Every experiment ran at least twice. Each repeat gave byte-identical predictions files (5,000 labels and 15,000 class scores). After each code change, the earlier predictions files were checked with SHA-256, and none of them changed.

## 3. Candidates

| Candidate | Features | Scaling | Accuracy | High P | High R | High F1 | Cost |
|---|---|---|---:|---:|---:|---:|---:|
| SDCA | 6 raw | none | 59.38% | 2.55% | 2.23% | 0.024 | $228,550 |
| SDCA | 10 (6 + 4 engineered) | none | 59.46% | 2.54% | 2.23% | 0.024 | $228,600 |
| LightGBM | 6 raw | none | 89.16% | 87.1% | 66.5% | 0.754 | $76,100 |
| SDCA | 6 raw | NormalizeMeanVariance | 89.78% | 89.4% | 71.4% | 0.794 | $64,950 |
| SDCA | 10 | NormalizeMeanVariance | 90.82% | 89.8% | 74.6% | 0.815 | $57,950 |

**The four engineered features** are `IsNewBeneficiary` (age < 7 days), `IsLargeTransaction` (amount > 3,000), `NewBeneficiaryLargeAmount` (both), and `IsLateNight` (hour ≤ 4). They copy the thresholds the synthetic generator uses to build the label. They are **oracle-informed**: they show the effect of a good representation, not features found from real data.

## 4. Normalization experiment

Unscaled SDCA failed. Amount goes up to 10,000, while the indicators are 0 or 1. The engineered features alone changed almost nothing.

`NormalizeMeanVariance` runs on the feature vector, just before the trainer, inside the pipeline. So each fold fits the scaling on its own 4,000 training records only.

| High F1 (cost) | No normalization | Normalization |
|---|---:|---:|
| **6 features** | 0.024 ($228,550) | 0.794 ($64,950) |
| **10 features** | 0.024 ($228,600) | 0.815 ($57,950) |

- **Normalization alone** raised High F1 from 0.024 to 0.794. This is most of the improvement.
- **The engineered features** helped only after normalization: 7 more High records found, with the same 19 false positives.
- **The lesson:** check the data representation and preprocessing before you blame the model family. A normalized linear model beat the tree model.

## 5. Learning curves

The two strongest candidates trained on nested, stratified subsets of each fold's 4,000 training records. Each subset model was scored on the same 1,000 validation records.

| Training size per fold | SDCA (10, normalized) High F1 | Cost | LightGBM (6) High F1 | Cost |
|---:|---:|---:|---:|---:|
| 300 | 0.723 | $75,150 | 0.432 | $155,500 |
| 600 | 0.787 | $59,600 | 0.561 | $130,850 |
| 1,200 | 0.810 | $55,300 | 0.686 | $96,150 |
| 2,400 | 0.822 | $54,050 | 0.733 | $78,500 |
| 4,000 | 0.815 | $57,950 | 0.754 | $76,100 |

- SDCA learns from little data and stops improving at about 1,200 records. The changes after that are inside the fold noise.
- LightGBM still improves at 4,000 records, but more slowly.
- The best SDCA point (2,400) is not selected. The difference is small, and many configurations were inspected on the same folds.

## 6. Nested threshold selection

The default rule predicts the class with the largest score, so it ignores the size of the High score. The threshold policy flags High when `score(High) ≥ threshold`.

**Score semantics:** SDCA gives softmax scores between 0 and 1 that add up to 1. They are close to calibrated, but not exactly. A threshold chosen by search does not need calibration, because a monotone calibration keeps the order of the scores. The cost-ratio rule 50 / 1,050 ≈ 0.048 does need calibrated scores. It flagged 98 more records than the searched threshold.

**Selection rule:** the lowest business cost with a flag rate of 40% or less. If two thresholds have the same cost, the higher one is kept.

**Why nested:** if a threshold is chosen and graded on the same predictions, its grade is too good. So for each locked outer fold, the other 4 folds run their own 4-fold cross-validation. The threshold is chosen on those inner scores, which come from models that never saw the locked fold. It is then applied once to the locked fold. Each candidate trains 25 models.

| Candidate | Fold thresholds | TP | FP | FN | High R | High P | Flag rate | **Nested cost** |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| **SDCA, 10, normalized** | 0.087–0.163 | 224 | 163 | 0 | 100% | 57.9% | 7.74% | **$8,150** |
| LightGBM, 6 | 0.0009–0.0033 | 219 | 316 | 5 | 97.8% | 40.9% | 10.70% | $20,800 |

**SDCA wins** on cost, recall, precision and flag rate, and in every fold. The threshold accepts 144 more false alarms ($7,200) to remove 57 misses ($57,000). F1 falls from 0.815 to 0.733, because F1 treats both errors as equal.

## 7. Frozen model and policy

| Item | Value |
|---|---|
| Model | SDCA Maximum Entropy, 10 features, NormalizeMeanVariance, 1 thread, seed 42 |
| Training data | all 5,000 training records |
| Model file | `Models/chapter-06-risk-classifier.zip` (the full pipeline, with preprocessing) |
| Content SHA-256 | `0fd45f32162ab491340192b4023e1990a3d13935cd050f7b133745e8e309b571` |
| Deployment threshold | 0.1121, chosen on the 5,000 out-of-fold training scores |
| Policy file | `Models/chapter-06-risk-classifier.policy.json` |

- **The threshold is versioned configuration.** The policy file holds the threshold, the comparison (`>=`) and the class mapping. It is not inside the model.
- **The content hash** covers the zip entries and their bytes. It does not use the file hash, because the zip stores write times and the file hash changes on every save. Three selection runs gave the same hash and the same threshold.
- **The threshold choice is training-only.** It was chosen on pooled out-of-fold scores from models trained on 4,000 records. The nested result in section 6 is the less biased estimate of the whole procedure.

**Deployment requirements:**

1. Read the High score by the slot name `High` from the loaded model's `Score` column. Never use a fixed index. The older `CalculateMetricsForThreshold` reads `Score[1]`, which is Medium in this harness.
2. Use the same score as in validation: the SDCA softmax score, compared with `>=`.
3. Register the `RiskClassificationLab` assembly with `mlContext.ComponentCatalog` before loading. The engineered-feature mapping is custom code with the contract name `EngineeredFeatures`.

## 8. Held-out test result

`POST /risk/final-test` loads the frozen model and checks its content hash. It then scores `test-1000.csv` once and applies the frozen threshold. It writes the result with no overwrite, and it refuses a second run. After the test, model selection also refuses to run, so the frozen model cannot change.

| Metric | Nested estimate (per 1,000) | **Test, threshold 0.1121** | Test, default rule |
|---|---:|---:|---:|
| TP | 44.8 | **44** | 33 |
| FP | 32.6 | **28** | 8 |
| FN | 0 | **0** | 11 |
| High recall | 100% | **100%** | 75.0% |
| High precision | 57.9% | **61.1%** | 80.5% |
| High F1 | 0.733 | **0.759** | 0.777 |
| Business cost | $1,630 | **$1,400** | $11,400 |
| High flag rate | 7.74% | **7.20%** | 4.10% |

**Three-class confusion matrix on the test set (default rule, accuracy 89.6%):**

| Actual \ Predicted | Low | Medium | High |
|---|---:|---:|---:|
| Low | 477 | 48 | 0 |
| Medium | 37 | 386 | 8 |
| High | 0 | 11 | 33 |

The test result is close to the nested estimate. So the selection procedure did not simply exploit the validation folds.

## 9. Limitations

- **The data is synthetic.** The engineered features copy the label rules, so the classes separate more cleanly than real fraud data will.
- **100% recall is an observed result, not a guarantee.** The test set has only 44 High records. One miss would add $1,000 to the test cost.
- **The deployment threshold came from pooled out-of-fold scores.** The nested outer-fold result is the less biased estimate.
- **The SDCA scores are not calibrated.** The threshold is valid only for this model and its score semantics. A retrained model needs a new threshold.
- **Many configurations shared the same folds.** Small differences, such as F1 0.815 against 0.822, are inside the fold noise.

## Reproduce

Run the API with `dotnet run --project src/RiskClassificationLab`. Then call these endpoints with POST:

| Step | Endpoint | Output |
|---|---|---|
| 1 | `/risk/generate-large-dataset` | `data/risk-classification/chapter-06/*.csv` |
| 2 | `/risk/cross-validation-folds` | `cross-validation-folds-results.json` |
| 3 | `/risk/cross-validation/sdca` | `sdca-cross-validation-predictions.csv` |
| 3 | `/risk/cross-validation/lightgbm` | `lightgbm-cross-validation-predictions.csv` |
| 4 | `/risk/cross-validation/sdca-engineered` | `sdca-engineered-cross-validation-predictions.csv` |
| 5 | `/risk/cross-validation/sdca-engineered-normalized` | `sdca-engineered-normalized-cross-validation-predictions.csv` |
| 6 | `/risk/cross-validation/sdca-normalized` | `sdca-normalized-cross-validation-predictions.csv` |
| 7 | `/risk/learning-curves` | API response; `learning-curve-results.json` |
| 8 | `/risk/threshold/sdca-nested` | `sdca-engineered-normalized-threshold-sweep.csv` |
| 9 | `/risk/model-selection` | `chapter-06-frozen-model.json`, the model zip |
| 9 | `/risk/final-test` | `chapter-06-final-test-results.json` (one run only) |

All output files are under `evaluations/risk-classification/` unless shown otherwise. Step 1 does not overwrite existing dataset files. Both step 9 endpoints refuse to run after the final test, so the frozen record stays as it is.
