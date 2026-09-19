import json
import os
import tempfile
import unittest

from dataset_lab.balance import check_balance
from dataset_lab.records import Dataset, load_jsonl
from dataset_lab.versioning import Manifest, check_drift, freeze
from tests.helpers import DatasetLabTestCase, make_dataset, make_record


class BalanceTest(DatasetLabTestCase):
    def test_balanced_passes(self):
        ds = make_dataset("t", ["a", "b"], category="Provider Timeout")
        ds.records += make_dataset("t", ["c", "d"],
                                   category="Expired Card").records
        report = check_balance(ds)
        self.assertPassed(report)
        self.assertEqual(report.imbalance_ratio, 1.0)

    def test_skewed_fails(self):
        ds = make_dataset("t", ["a", "b", "c", "d", "e", "f"],
                          category="Provider Timeout")
        ds.records += make_dataset("t", ["g"], category="Expired Card").records
        report = check_balance(ds, max_imbalance_ratio=2.0)
        self.assertFalse(report.passed)
        self.assertGreater(report.imbalance_ratio, 2.0)
        self.assertIn("Provider Timeout", report.issues[0])

    def test_custom_threshold(self):
        ds = make_dataset("t", ["a", "b", "c"], category="Provider Timeout")
        ds.records += make_dataset("t", ["d"], category="Expired Card").records
        self.assertPassed(check_balance(ds, max_imbalance_ratio=5.0))
        self.assertFalse(check_balance(ds, max_imbalance_ratio=2.0).passed)


class VersioningTest(DatasetLabTestCase):
    def _write(self, directory, name, lines):
        path = os.path.join(directory, name)
        with open(path, "w", encoding="utf-8") as fh:
            fh.writelines(lines)
        return path

    def test_freeze_then_check_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            train = self._write(tmp, "train.jsonl",
                                ['{"input": "a", "output": "x"}\n'])
            manifest_path = os.path.join(tmp, "manifest.json")
            datasets = {"train": load_jsonl(train, split="train")}
            freeze(datasets, {"train": train},
                   version="v1").save(manifest_path)
            manifest = Manifest.load(manifest_path)
            self.assertEqual(manifest.version, "v1")
            self.assertEqual(manifest.files[train]["records"], 1)
            drift = check_drift(manifest)
            self.assertFalse(drift.drifted)

    def test_edited_file_detected_as_drift(self):
        with tempfile.TemporaryDirectory() as tmp:
            train = self._write(tmp, "train.jsonl",
                                ['{"input": "a", "output": "x"}\n'])
            manifest_path = os.path.join(tmp, "manifest.json")
            datasets = {"train": load_jsonl(train, split="train")}
            freeze(datasets, {"train": train},
                   version="v1").save(manifest_path)
            with open(train, "a", encoding="utf-8") as fh:
                fh.write('{"input": "b", "output": "y"}\n')
            drift = check_drift(Manifest.load(manifest_path))
            self.assertTrue(drift.drifted)
            self.assertTrue(any("train.jsonl" in d for d in drift.details))

    def test_missing_file_detected_as_drift(self):
        with tempfile.TemporaryDirectory() as tmp:
            train = self._write(tmp, "train.jsonl",
                                ['{"input": "a", "output": "x"}\n'])
            manifest_path = os.path.join(tmp, "manifest.json")
            datasets = {"train": load_jsonl(train, split="train")}
            freeze(datasets, {"train": train},
                   version="v1").save(manifest_path)
            os.unlink(train)
            drift = check_drift(Manifest.load(manifest_path))
            self.assertTrue(drift.drifted)


class RecordsTest(DatasetLabTestCase):
    def test_category_and_retryable_extraction(self):
        rec = make_record("input text", category="Expired Card",
                          retryable="No")
        self.assertEqual(rec.category, "Expired Card")
        self.assertEqual(rec.retryable, "No")

    def test_malformed_label_extracts_nothing(self):
        rec = make_record("input text")
        rec.label = "freeform text"
        self.assertIsNone(rec.category)
        self.assertIsNone(rec.retryable)


if __name__ == "__main__":
    unittest.main()
