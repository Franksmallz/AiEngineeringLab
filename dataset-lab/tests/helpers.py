"""Shared fixtures for dataset-lab tests."""
import unittest

from dataset_lab.records import Dataset, Record


def make_record(input_text, category="Provider Timeout", retryable="Yes",
                source="test.jsonl", line_no=1, split="train"):
    label = (f"Category: {category}\nRetryable: {retryable}\n"
             f"Action: Test action.")
    return Record(input=input_text, label=label, source=source,
                  line_no=line_no, split=split)


def make_dataset(name, inputs, **kwargs):
    return Dataset(name=name, records=[
        make_record(text, source=f"{name}.jsonl", line_no=i + 1, split=name,
                    **kwargs)
        for i, text in enumerate(inputs)
    ])


class DatasetLabTestCase(unittest.TestCase):
    def assertPassed(self, result):  # noqa: N802
        self.assertTrue(result.passed,
                        f"expected pass, got: {result.as_dict()}")
