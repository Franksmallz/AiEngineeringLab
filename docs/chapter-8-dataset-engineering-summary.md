# Chapter 8 Dataset Engineering Summary

## Objective

Build the dataset curation and testing pipeline chapter 8 calls for, and
run it against the lab's real fine-tuning data: the 50-example training set
and 20-example eval set of payment-incident classifications from chapter 7.

Chapter 8's core argument: model behavior is downstream of dataset quality,
and dataset quality is not a vibe — it is a set of checks you run as code,
on every change, with versioned data.

## What was built

`dataset-lab/` — a standard-library-only Python package with a CLI:

- **Schema & label validation** (`schema.py`): every record must parse,
  carry a non-empty input and label, follow the
  `Category / Retryable / Action` label format, and use a category from the
  controlled vocabulary. Unknown categories are errors, not warnings.
- **Class-balance reporting** (`balance.py`): per-category counts plus a
  max/min imbalance ratio with a configurable limit (default 2.0x).
- **Exact duplicate detection** (`duplicates.py`): SHA-256 over normalized
  text, grouped with file/line provenance.
- **Near-duplicate detection** (`similarity.py`, `duplicates.py`): Jaccard
  similarity over token 3-shingles, threshold 0.8. Catches paraphrased
  repeats, the quieter version of the duplication bug.
- **Train/eval leakage checks** (`leakage.py`): exact input overlap plus
  near-duplicate leakage across splits. A leaked eval measures
  memorization; the reported metrics would quietly lie.
- **Versioning** (`versioning.py`): `freeze` writes a SHA-256 manifest of
  the dataset files; `check` detects silent edits, additions, or deletions.
  Eval results can now point at a manifest instead of "the data as of
  whenever."
- **Reports** (`report.py`): machine-readable JSON plus a human-readable
  Markdown summary, exit code 0/1 for CI.

31 unit tests (`unittest`, no dependencies) cover schema edge cases,
similarity math, duplicate/leakage detection, balance thresholds, and the
freeze/drift round-trip.

## Results on the lab's payment dataset

The committed run (`dataset-lab/reports/chapter-8-dataset-report.md`):

| Check | Result |
|---|---|
| Schema & labels (train / eval) | PASS, 0 errors |
| Class balance | PASS, 1.00x (5 per category train, 2 per category eval) |
| Duplicates | PASS, none exact or near |
| Train/eval leakage | PASS, no overlap |

The fine-tuning metrics from chapter 7 were computed on data that is now
verified clean *and* frozen (`dataset-lab/manifests/dataset-manifest.json`
pins the exact bytes). Any future edit to the datasets will fail
`dataset-lab check` until the manifest is deliberately re-frozen — which is
exactly the point.

## What this changes going forward

- New examples get validated before they enter the dataset, not after a
  confusing training run.
- The eval set is guarded: leakage checks run before any metric is trusted.
- Dataset versions are explicit, so chapter 9's inference benchmarks and
  chapter 10's feedback loop will run against pinned data.

## Honest limitations

- Near-duplicate detection is O(n²) brute force — fine for hundreds of
  examples, not for millions. The similarity module is isolated so
  MinHash/LSH can replace it later.
- Shingle Jaccard catches paraphrases, not semantic duplicates with very
  different wording. Embedding-based dedup is a possible follow-up.
- The controlled category vocabulary is hand-maintained; growing to dozens
  of categories will want a schema file instead of a constant.
