"""dataset-lab: dataset curation and testing pipeline for the AI Engineering Lab.

Chapter 8 (Dataset Engineering) artifact. Provides schema/label validation,
class-balance reporting, exact and near-duplicate detection, train/eval
leakage checks, and versioned dataset manifests -- as code, runnable in CI.

Standard library only. No third-party dependencies.
"""

from dataset_lab.records import Dataset, Record, load_jsonl

__all__ = ["Dataset", "Record", "load_jsonl"]
__version__ = "0.1.0"
