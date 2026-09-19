"""Text similarity primitives for near-duplicate detection.

Uses Jaccard similarity over token n-gram shingles: simple, deterministic,
dependency-free, and good enough to catch paraphrased near-duplicates in
small curated datasets. For very large corpora this would be replaced by
MinHash/LSH; the interface (shingles -> similarity) is deliberately kept
separate so that swap is mechanical.
"""
from __future__ import annotations

from dataset_lab.records import normalize_text


def shingles(text: str, n: int = 3) -> set[str]:
    """Set of token n-grams from the normalized text."""
    tokens = normalize_text(text).split()
    if len(tokens) < n:
        return {" ".join(tokens)} if tokens else set()
    return {" ".join(tokens[i:i + n]) for i in range(len(tokens) - n + 1)}


def jaccard(a: set[str], b: set[str]) -> float:
    """Jaccard similarity between two shingle sets, in [0, 1]."""
    if not a and not b:
        return 1.0
    if not a or not b:
        return 0.0
    return len(a & b) / len(a | b)


def similarity(text_a: str, text_b: str, n: int = 3) -> float:
    """Similarity between two raw texts via shingle Jaccard."""
    return jaccard(shingles(text_a, n), shingles(text_b, n))
