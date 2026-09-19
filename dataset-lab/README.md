# dataset-lab

Dataset curation and testing pipeline for the AI Engineering Lab.
**Chapter 8 (Dataset Engineering) artifact** — the code that keeps the
lab's datasets honest as they grow.

## What it does

Runs the checks chapter 8 argues every serious dataset needs, as a CLI
with CI-friendly exit codes (0 = pass, 1 = fail):

| Check | What it catches |
|---|---|
| Schema & label validation | Malformed JSONL, missing fields, empty inputs, labels outside the controlled category vocabulary |
| Class balance | Skewed category distribution beyond a configurable ratio |
| Exact duplicates | Byte-identical examples after normalization (SHA-256) |
| Near-duplicates | Paraphrased repeats via token-shingle Jaccard similarity |
| Train/eval leakage | Eval examples identical or near-identical to training ones (metrics that measure memorization, not generalization) |
| Versioning | Frozen SHA-256 manifests; `check` detects silent edits, additions, or deletions |

## Quickstart

No dependencies beyond the Python standard library.

```bash
cd dataset-lab
export PYTHONPATH=src

# Run all checks on the lab's payment dataset (from repo root)
python -m dataset_lab validate \
  --train ../finetuning/data/train.jsonl \
  --eval ../finetuning/data/eval.jsonl \
  --report-md reports/chapter-8-dataset-report.md \
  --report-json reports/chapter-8-dataset-report.json

# Freeze a version manifest, then verify no drift later
python -m dataset_lab freeze --train ../finetuning/data/train.jsonl \
  --eval ../finetuning/data/eval.jsonl \
  --version v1 --manifest manifests/dataset-manifest.json
python -m dataset_lab check --manifest manifests/dataset-manifest.json

# Run the test suite (31 tests, stdlib unittest)
PYTHONPATH=src:tests python -m unittest discover -s tests
```

Tune sensitivity with `--threshold` (near-duplicate Jaccard, default 0.8)
and `--max-imbalance` (class ratio, default 2.0).

## Results on the lab's payment dataset

`reports/chapter-8-dataset-report.md` is the committed run against the
chapter-7 fine-tuning data (50 train / 20 eval payment-incident cases):

- Schema: PASS, 0 errors, 0 warnings
- Balance: PASS, 1.00x ratio (5 per category in train, 2 in eval)
- Duplicates: PASS, no exact or near-duplicate pairs
- Leakage: PASS, no train/eval overlap

The dataset that produced the fine-tuning metrics is now verified clean
*and* frozen — `manifests/dataset-manifest.json` pins the exact bytes the
numbers were computed from.

## Design notes

- **Standard library only.** Dataset tooling should run anywhere, including
  locked-down CI runners. No numpy, no sklearn.
- **Normalization before comparison.** Lowercasing, punctuation stripping,
  and whitespace collapsing mean `"Timeout!"` and `"timeout"` compare equal.
- **Near-duplicate detection is O(n²).** Fine for curated datasets of
  hundreds of examples. The similarity layer (`similarity.py`) is isolated
  so MinHash/LSH can replace brute force without touching the rest.
- **Controlled vocabulary is explicit.** `KNOWN_CATEGORIES` in `schema.py`
  means a new category appearing in the data is a loud error, not a silent
  schema drift.
- **Manifests make evals reproducible.** A reported metric is only as
  trustworthy as the data it ran on; the manifest lets anyone verify the
  data hasn't moved underneath the number.
