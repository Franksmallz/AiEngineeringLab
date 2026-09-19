"""Schema and label validation.

Catches the dataset bugs that silently corrupt training and evaluation:
malformed lines, missing fields, empty inputs, labels that don't follow the
agreed format, and labels outside the controlled category vocabulary
(a new category appearing unannounced is a data-pipeline bug, not a feature).
"""
from __future__ import annotations

from dataclasses import dataclass, field

from dataset_lab.records import Dataset

#: Controlled category vocabulary for the payment-incident classifier.
KNOWN_CATEGORIES = [
    "Provider Timeout",
    "Insufficient Funds",
    "Webhook Delivery Failure",
    "Invalid Beneficiary Details",
    "Provider Unavailable",
    "Expired Card",
    "Duplicate Request",
    "Pending Provider Confirmation",
    "Debit Without Final Status",
    "Provider Authentication Failure",
]

#: Allowed values for the Retryable label line.
KNOWN_RETRYABLE = {"Yes", "No"}


@dataclass
class Issue:
    """One validation finding."""

    severity: str  # "error" or "warning"
    code: str  # machine-readable, e.g. "missing_input"
    message: str
    source: str = ""
    line_no: int = 0

    def as_dict(self) -> dict:
        return {
            "severity": self.severity,
            "code": self.code,
            "message": self.message,
            "source": self.source,
            "line_no": self.line_no,
        }


@dataclass
class ValidationResult:
    issues: list[Issue] = field(default_factory=list)

    @property
    def errors(self) -> list[Issue]:
        return [i for i in self.issues if i.severity == "error"]

    @property
    def warnings(self) -> list[Issue]:
        return [i for i in self.issues if i.severity == "warning"]

    @property
    def passed(self) -> bool:
        return not self.errors

    def as_dict(self) -> dict:
        return {
            "passed": self.passed,
            "errors": [i.as_dict() for i in self.errors],
            "warnings": [i.as_dict() for i in self.warnings],
        }

    def add(self, severity: str, code: str, message: str,
            source: str = "", line_no: int = 0) -> None:
        self.issues.append(Issue(severity, code, message, source, line_no))


def validate_schema(dataset: Dataset,
                     known_categories: list[str] | None = None) -> ValidationResult:
    """Validate every record's structure and label against the contract."""
    known = known_categories if known_categories is not None else KNOWN_CATEGORIES
    result = ValidationResult()

    if not dataset.records:
        result.add("error", "empty_dataset",
                   f"dataset '{dataset.name}' contains no records")
        return result

    seen_lines: set[int] = set()
    for record in dataset.records:
        loc = {"source": record.source, "line_no": record.line_no}
        if not isinstance(record.input, str) or not record.input.strip():
            result.add("error", "missing_input",
                       "record has no usable 'input' text", **loc)
        if not isinstance(record.label, str) or not record.label.strip():
            result.add("error", "missing_label",
                       "record has no usable label ('output'/'expected')", **loc)
            continue

        category = record.category
        if category is None:
            result.add("error", "malformed_label",
                       "label has no 'Category:' line", **loc)
        elif category not in known:
            result.add("error", "unknown_category",
                       f"category {category!r} is outside the controlled "
                       f"vocabulary ({len(known)} known)", **loc)

        retryable = record.retryable
        if retryable is None:
            result.add("warning", "missing_retryable",
                       "label has no 'Retryable:' line", **loc)
        elif retryable not in KNOWN_RETRYABLE:
            result.add("error", "invalid_retryable",
                       f"Retryable value {retryable!r} must be Yes or No", **loc)

        if record.line_no in seen_lines:
            result.add("warning", "duplicate_line_number",
                       "line number seen twice; provenance may be unreliable",
                       **loc)
        seen_lines.add(record.line_no)

    return result
