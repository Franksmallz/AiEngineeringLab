"""Train/eval leakage detection.

If an eval example is identical (or near-identical) to a training example,
the eval measures memorization, not generalization -- and the reported
metrics quietly lie. This module checks both exact input overlap and
near-duplicate leakage across two splits.
"""
from __future__ import annotations

from dataclasses import dataclass, field

from dataset_lab.duplicates import DEFAULT_NEAR_DUP_THRESHOLD, NearDuplicatePair
from dataset_lab.records import Dataset, text_hash
from dataset_lab.similarity import shingles, jaccard


@dataclass
class LeakageReport:
    train: str
    eval: str
    exact_overlaps: list[dict] = field(default_factory=list)
    near_pairs: list[NearDuplicatePair] = field(default_factory=list)

    @property
    def passed(self) -> bool:
        return not self.exact_overlaps and not self.near_pairs

    def as_dict(self) -> dict:
        return {
            "train": self.train,
            "eval": self.eval,
            "exact_overlaps": self.exact_overlaps,
            "near_duplicate_pairs": [p.as_dict() for p in self.near_pairs],
        }


def check_leakage(train: Dataset, eval: Dataset,
                  near_dup_threshold: float = DEFAULT_NEAR_DUP_THRESHOLD
                  ) -> LeakageReport:
    report = LeakageReport(train=train.name, eval=eval.name)

    # Index train inputs by normalized hash for exact-overlap lookup.
    train_by_hash: dict[str, tuple[str, int]] = {}
    for record in train.records:
        if record.input.strip():
            train_by_hash.setdefault(
                text_hash(record.input), (record.source, record.line_no))

    eval_shingles = [shingles(r.input) for r in eval.records]
    train_shingles = [shingles(r.input) for r in train.records]

    for k, erec in enumerate(eval.records):
        if not erec.input.strip():
            continue
        digest = text_hash(erec.input)
        if digest in train_by_hash:
            tloc = train_by_hash[digest]
            report.exact_overlaps.append({
                "train": {"source": tloc[0], "line_no": tloc[1]},
                "eval": {"source": erec.source, "line_no": erec.line_no},
            })
            continue  # exact overlap already reported; skip near-dup for it
        for m, trec in enumerate(train.records):
            if not trec.input.strip():
                continue
            sim = jaccard(eval_shingles[k], train_shingles[m])
            if sim >= near_dup_threshold:
                report.near_pairs.append(NearDuplicatePair(
                    (trec.source, trec.line_no),
                    (erec.source, erec.line_no),
                    sim,
                ))

    report.near_pairs.sort(key=lambda p: p.similarity, reverse=True)
    return report
