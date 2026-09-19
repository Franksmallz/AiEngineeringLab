"""Class-balance reporting.

A classifier trained on a skewed dataset learns the skew, not the task.
This module reports per-category counts and flags imbalance beyond a
configurable ratio. The lab's payment dataset is intentionally balanced;
this check keeps it that way as it grows.
"""
from __future__ import annotations

from collections import Counter
from dataclasses import dataclass, field

from dataset_lab.records import Dataset

#: A max/min category-count ratio above this is reported as an error.
DEFAULT_MAX_IMBALANCE_RATIO = 2.0


@dataclass
class BalanceReport:
    dataset: str
    total: int
    counts: dict[str, int]
    min_count: int
    max_count: int
    imbalance_ratio: float  # max_count / min_count, inf if a category is empty
    issues: list[str] = field(default_factory=list)

    @property
    def passed(self) -> bool:
        return not self.issues

    def as_dict(self) -> dict:
        return {
            "dataset": self.dataset,
            "total": self.total,
            "counts": self.counts,
            "min_count": self.min_count,
            "max_count": self.max_count,
            "imbalance_ratio": self.imbalance_ratio,
            "issues": self.issues,
        }


def check_balance(dataset: Dataset,
                  max_imbalance_ratio: float = DEFAULT_MAX_IMBALANCE_RATIO
                  ) -> BalanceReport:
    counts = Counter(r.category for r in dataset.records if r.category)
    total = len(dataset.records)
    if not counts:
        return BalanceReport(dataset.name, total, {}, 0, 0, float("inf"),
                             ["no labeled records found"])

    min_count = min(counts.values())
    max_count = max(counts.values())
    ratio = (max_count / min_count) if min_count else float("inf")

    issues: list[str] = []
    if ratio > max_imbalance_ratio:
        biggest = max(counts, key=counts.get)  # type: ignore[arg-type]
        smallest = min(counts, key=counts.get)  # type: ignore[arg-type]
        issues.append(
            f"class imbalance {ratio:.1f}x exceeds limit {max_imbalance_ratio:.1f}x: "
            f"{biggest!r} has {counts[biggest]} examples, "
            f"{smallest!r} has {counts[smallest]}"
        )

    return BalanceReport(
        dataset=dataset.name,
        total=total,
        counts=dict(sorted(counts.items())),
        min_count=min_count,
        max_count=max_count,
        imbalance_ratio=ratio,
        issues=issues,
    )
