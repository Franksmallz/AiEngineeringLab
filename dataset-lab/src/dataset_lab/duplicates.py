"""Exact and near-duplicate detection within one dataset.

Duplicates in training data waste capacity and bias the model toward the
repeated phrasing; near-duplicates (paraphrases of the same example) are the
quieter version of the same bug. Both are reported with provenance so the
curator can decide: keep, merge, or rewrite.
"""
from __future__ import annotations

from collections import defaultdict
from dataclasses import dataclass, field

from dataset_lab.records import Dataset, text_hash
from dataset_lab.similarity import shingles, jaccard

#: Pairs at or above this Jaccard similarity are reported as near-duplicates.
DEFAULT_NEAR_DUP_THRESHOLD = 0.8


@dataclass
class DuplicateGroup:
    """Records that are exactly identical after normalization."""
    text_hash: str
    records: list[tuple[str, int]]  # (source, line_no)

    def as_dict(self) -> dict:
        return {"text_hash": self.text_hash[:12],
                "records": [{"source": s, "line_no": n}
                            for s, n in self.records]}


@dataclass
class NearDuplicatePair:
    record_a: tuple[str, int]
    record_b: tuple[str, int]
    similarity: float

    def as_dict(self) -> dict:
        return {"record_a": {"source": self.record_a[0],
                             "line_no": self.record_a[1]},
                "record_b": {"source": self.record_b[0],
                             "line_no": self.record_b[1]},
                "similarity": round(self.similarity, 3)}


@dataclass
class DuplicateReport:
    dataset: str
    exact_groups: list[DuplicateGroup] = field(default_factory=list)
    near_pairs: list[NearDuplicatePair] = field(default_factory=list)

    @property
    def exact_duplicate_count(self) -> int:
        return sum(len(g.records) - 1 for g in self.exact_groups)

    @property
    def passed(self) -> bool:
        return not self.exact_groups and not self.near_pairs

    def as_dict(self) -> dict:
        return {
            "dataset": self.dataset,
            "exact_groups": [g.as_dict() for g in self.exact_groups],
            "near_duplicate_pairs": [p.as_dict() for p in self.near_pairs],
        }


def find_duplicates(dataset: Dataset,
                    near_dup_threshold: float = DEFAULT_NEAR_DUP_THRESHOLD
                    ) -> DuplicateReport:
    report = DuplicateReport(dataset=dataset.name)

    # Exact duplicates via normalized hash.
    by_hash: dict[str, list[tuple[str, int]]] = defaultdict(list)
    for record in dataset.records:
        if record.input.strip():
            by_hash[text_hash(record.input)].append(
                (record.source, record.line_no))
    for digest, locations in sorted(by_hash.items()):
        if len(locations) > 1:
            report.exact_groups.append(DuplicateGroup(digest, locations))

    # Near-duplicates via shingle Jaccard. O(n^2) is fine for curated
    # datasets of hundreds of examples; note the scaling caveat in README.
    shingle_sets = [shingles(r.input) for r in dataset.records]
    n = len(dataset.records)
    for i in range(n):
        if not dataset.records[i].input.strip():
            continue
        for j in range(i + 1, n):
            if not dataset.records[j].input.strip():
                continue
            # Skip pairs already caught as exact duplicates.
            if text_hash(dataset.records[i].input) == \
               text_hash(dataset.records[j].input):
                continue
            sim = jaccard(shingle_sets[i], shingle_sets[j])
            if sim >= near_dup_threshold:
                report.near_pairs.append(NearDuplicatePair(
                    (dataset.records[i].source, dataset.records[i].line_no),
                    (dataset.records[j].source, dataset.records[j].line_no),
                    sim,
                ))

    report.near_pairs.sort(key=lambda p: p.similarity, reverse=True)
    return report
