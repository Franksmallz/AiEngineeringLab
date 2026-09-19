"""Report generation: one JSON artifact for machines, one Markdown for humans.

The JSON report is the complete record (every issue, pair, and count);
the Markdown report is the readable summary committed alongside eval
results so a reviewer can see dataset health at a glance.
"""
from __future__ import annotations

import json
from dataclasses import dataclass

from dataset_lab.balance import BalanceReport
from dataset_lab.duplicates import DuplicateReport
from dataset_lab.leakage import LeakageReport
from dataset_lab.schema import ValidationResult


@dataclass
class PipelineReport:
    schemas: dict[str, ValidationResult]
    balances: dict[str, BalanceReport]
    duplicates: dict[str, DuplicateReport]
    leakage: LeakageReport | None

    @property
    def passed(self) -> bool:
        if any(not r.passed for r in self.schemas.values()):
            return False
        if any(not r.passed for r in self.balances.values()):
            return False
        if any(not r.passed for r in self.duplicates.values()):
            return False
        if self.leakage is not None and not self.leakage.passed:
            return False
        return True

    def as_dict(self) -> dict:
        return {
            "passed": self.passed,
            "schemas": {n: {"passed": r.passed,
                            "errors": [i.as_dict() for i in r.errors],
                            "warnings": [i.as_dict() for i in r.warnings]}
                        for n, r in self.schemas.items()},
            "balance": {n: r.as_dict() for n, r in self.balances.items()},
            "duplicates": {n: r.as_dict() for n, r in self.duplicates.items()},
            "leakage": self.leakage.as_dict() if self.leakage else None,
        }

    def save_json(self, path: str) -> None:
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(self.as_dict(), fh, indent=2)
            fh.write("\n")

    def to_markdown(self) -> str:
        lines = ["# Dataset curation report", ""]
        lines.append(f"**Overall: {'PASS' if self.passed else 'FAIL'}**")
        lines.append("")

        lines.append("## Schema & label validation")
        for name, result in self.schemas.items():
            status = "PASS" if result.passed else "FAIL"
            lines.append(
                f"- **{name}**: {status} "
                f"({len(result.errors)} errors, {len(result.warnings)} warnings)")
            for issue in result.errors[:10]:
                lines.append(f"  - ERROR [{issue.code}] {issue.source}:"
                             f"{issue.line_no}: {issue.message}")
            if len(result.errors) > 10:
                lines.append(f"  - ... and {len(result.errors) - 10} more "
                             f"(see JSON report)")
            for issue in result.warnings[:5]:
                lines.append(f"  - warning [{issue.code}] {issue.source}:"
                             f"{issue.line_no}: {issue.message}")
        lines.append("")

        lines.append("## Class balance")
        for name, balance in self.balances.items():
            status = "PASS" if balance.passed else "FAIL"
            lines.append(f"- **{name}**: {status} "
                         f"({balance.total} records, "
                         f"imbalance ratio {balance.imbalance_ratio:.2f}x)")
            for category, count in balance.counts.items():
                lines.append(f"  - {category}: {count}")
            for issue in balance.issues:
                lines.append(f"  - {issue}")
        lines.append("")

        lines.append("## Duplicates")
        for name, dup in self.duplicates.items():
            status = "PASS" if dup.passed else "FAIL"
            lines.append(
                f"- **{name}**: {status} "
                f"({len(dup.exact_groups)} exact groups, "
                f"{len(dup.near_pairs)} near-duplicate pairs)")
            for group in dup.exact_groups[:5]:
                locs = ", ".join(f"{s}:{n}" for s, n in group.records)
                lines.append(f"  - exact: {locs}")
            for pair in dup.near_pairs[:5]:
                lines.append(
                    f"  - near ({pair.similarity:.2f}): "
                    f"{pair.record_a[0]}:{pair.record_a[1]} ~ "
                    f"{pair.record_b[0]}:{pair.record_b[1]}")
        lines.append("")

        lines.append("## Train/eval leakage")
        if self.leakage is None:
            lines.append("- not checked (single split)")
        else:
            status = "PASS" if self.leakage.passed else "FAIL"
            lines.append(
                f"- **{self.leakage.train} vs {self.leakage.eval}**: {status} "
                f"({len(self.leakage.exact_overlaps)} exact overlaps, "
                f"{len(self.leakage.near_pairs)} near-duplicate pairs)")
            for overlap in self.leakage.exact_overlaps[:5]:
                lines.append(
                    f"  - exact: train {overlap['train']['source']}:"
                    f"{overlap['train']['line_no']} == eval "
                    f"{overlap['eval']['source']}:{overlap['eval']['line_no']}")
            for pair in self.leakage.near_pairs[:5]:
                lines.append(
                    f"  - near ({pair.similarity:.2f}): "
                    f"{pair.record_a[0]}:{pair.record_a[1]} ~ "
                    f"{pair.record_b[0]}:{pair.record_b[1]}")
        lines.append("")
        return "\n".join(lines)

    def save_markdown(self, path: str) -> None:
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(self.to_markdown())
