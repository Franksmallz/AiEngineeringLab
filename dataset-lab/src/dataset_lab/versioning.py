"""Dataset versioning: freeze a manifest, detect drift.

A dataset version is the set of file hashes plus record counts. `freeze`
writes the manifest; `check` compares the working files against it and
reports additions, removals, or silent edits. This is what makes an eval
result reproducible: the report can point at a manifest and anyone can
verify the data hasn't moved underneath it.
"""
from __future__ import annotations

import datetime
import json
from dataclasses import dataclass, field

from dataset_lab.records import Dataset, file_hash


@dataclass
class Manifest:
    version: str
    created_at: str
    files: dict[str, dict]  # path -> {sha256, records}

    def as_dict(self) -> dict:
        return {"version": self.version,
                "created_at": self.created_at,
                "files": self.files}

    def save(self, path: str) -> None:
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(self.as_dict(), fh, indent=2)
            fh.write("\n")

    @staticmethod
    def load(path: str) -> "Manifest":
        with open(path, encoding="utf-8") as fh:
            obj = json.load(fh)
        return Manifest(version=obj["version"],
                        created_at=obj["created_at"],
                        files=obj["files"])


def freeze(datasets: dict[str, Dataset], paths: dict[str, str],
           version: str) -> Manifest:
    """Snapshot the current state of the dataset files."""
    files: dict[str, dict] = {}
    for name, dataset in datasets.items():
        path = paths[name]
        files[path] = {
            "sha256": file_hash(path),
            "records": len(dataset),
            "split": name,
        }
    return Manifest(
        version=version,
        created_at=datetime.datetime.now(
            datetime.timezone.utc).isoformat(timespec="seconds"),
        files=files,
    )


@dataclass
class DriftReport:
    drifted: bool
    details: list[str] = field(default_factory=list)

    def as_dict(self) -> dict:
        return {"drifted": self.drifted, "details": self.details}


def check_drift(manifest: Manifest) -> DriftReport:
    """Compare working files against a frozen manifest."""
    report = DriftReport(drifted=False)
    for path, info in manifest.files.items():
        try:
            current = file_hash(path)
        except OSError:
            report.drifted = True
            report.details.append(f"{path}: file missing "
                                  f"(manifest has {info['records']} records)")
            continue
        if current != info["sha256"]:
            report.drifted = True
            report.details.append(
                f"{path}: content changed since manifest "
                f"version {manifest.version}")
    return report
