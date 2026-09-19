"""Record loading and normalization.

A record is one JSONL line with an ``input`` (the incident description) and
a label (``output`` in train files, ``expected`` in eval files). The label is
a small structured text block:

    Category: <name>
    Retryable: Yes|No
    Action: <free text>
"""
from __future__ import annotations

import hashlib
import json
import re
import string
from dataclasses import dataclass, field


@dataclass
class Record:
    """One dataset example with provenance."""

    input: str
    label: str
    source: str  # file path the record came from
    line_no: int  # 1-based line number in that file
    split: str = ""  # e.g. "train" / "eval"

    @property
    def category(self) -> str | None:
        """Extract the Category value from the label block, if present."""
        match = re.search(r"^Category:\s*(.+)$", self.label, re.MULTILINE)
        return match.group(1).strip() if match else None

    @property
    def retryable(self) -> str | None:
        """Extract the Retryable value from the label block, if present."""
        match = re.search(r"^Retryable:\s*(.+)$", self.label, re.MULTILINE)
        return match.group(1).strip() if match else None


@dataclass
class Dataset:
    """A named collection of records from one or more files."""

    name: str
    records: list[Record] = field(default_factory=list)

    def __len__(self) -> int:
        return len(self.records)


def _label_key(obj: dict) -> str | None:
    if "output" in obj:
        return "output"
    if "expected" in obj:
        return "expected"
    return None


def load_jsonl(path: str, split: str = "") -> Dataset:
    """Load a JSONL file into a Dataset, keeping line numbers.

    Lines that are not valid JSON are kept as records with empty fields so
    schema validation can report them precisely instead of crashing here.
    """
    records: list[Record] = []
    with open(path, encoding="utf-8") as fh:
        for line_no, line in enumerate(fh, start=1):
            line = line.strip()
            if not line:
                continue
            try:
                obj = json.loads(line)
            except json.JSONDecodeError:
                records.append(Record(input="", label="", source=path,
                                      line_no=line_no, split=split))
                continue
            if not isinstance(obj, dict):
                records.append(Record(input="", label="", source=path,
                                      line_no=line_no, split=split))
                continue
            label_key = _label_key(obj)
            records.append(Record(
                input=obj.get("input", ""),
                label=obj.get(label_key, "") if label_key else "",
                source=path,
                line_no=line_no,
                split=split,
            ))
    return Dataset(name=split or path, records=records)


_PUNCT_TABLE = str.maketrans("", "", string.punctuation)


def normalize_text(text: str) -> str:
    """Canonical form for duplicate comparison: lowercase, no punctuation,
    collapsed whitespace."""
    text = text.lower().translate(_PUNCT_TABLE)
    return re.sub(r"\s+", " ", text).strip()


def text_hash(text: str) -> str:
    """SHA-256 of the normalized text; stable identity for exact matching."""
    return hashlib.sha256(normalize_text(text).encode("utf-8")).hexdigest()


def file_hash(path: str) -> str:
    """SHA-256 of a file's raw bytes; used for version manifests."""
    digest = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()
