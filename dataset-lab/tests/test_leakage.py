import unittest

from dataset_lab.leakage import check_leakage
from tests.helpers import DatasetLabTestCase, make_dataset


class LeakageTest(DatasetLabTestCase):
    def test_disjoint_splits_pass(self):
        train = make_dataset("train", [
            "the payment gateway timed out waiting for a response",
            "the customer had insufficient funds in the account",
        ])
        eval_ = make_dataset("eval", [
            "the webhook endpoint never acknowledged the delivery",
            "the beneficiary bank rejected the account number",
        ])
        self.assertPassed(check_leakage(train, eval_))

    def test_exact_overlap_detected(self):
        train = make_dataset("train", ["the exact same incident wording"])
        eval_ = make_dataset("eval", ["the exact same incident wording"])
        report = check_leakage(train, eval_)
        self.assertFalse(report.passed)
        self.assertEqual(len(report.exact_overlaps), 1)
        overlap = report.exact_overlaps[0]
        self.assertEqual(overlap["train"]["line_no"], 1)
        self.assertEqual(overlap["eval"]["line_no"], 1)

    def test_normalization_insensitive_overlap(self):
        train = make_dataset("train", ["The Exact Same Incident Wording!"])
        eval_ = make_dataset("eval", ["the exact same incident wording"])
        report = check_leakage(train, eval_)
        self.assertEqual(len(report.exact_overlaps), 1)

    def test_near_duplicate_leakage_detected(self):
        train = make_dataset("train", [
            "the payment gateway stopped responding before the transaction completed"])
        eval_ = make_dataset("eval", [
            "the payment gateway stopped responding before the transaction finished"])
        report = check_leakage(train, eval_, near_dup_threshold=0.5)
        self.assertTrue(report.near_pairs)
        self.assertFalse(report.exact_overlaps)


if __name__ == "__main__":
    unittest.main()
