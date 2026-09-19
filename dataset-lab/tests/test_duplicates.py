import unittest

from dataset_lab.duplicates import find_duplicates
from dataset_lab.similarity import jaccard, shingles, similarity
from tests.helpers import DatasetLabTestCase, make_dataset


class SimilarityTest(DatasetLabTestCase):
    def test_identical_texts_score_one(self):
        self.assertEqual(similarity("the cat sat", "the cat sat"), 1.0)

    def test_disjoint_texts_score_zero(self):
        self.assertEqual(similarity("alpha beta gamma delta",
                                    "one two three four"), 0.0)

    def test_case_and_punctuation_ignored(self):
        self.assertEqual(similarity("Hello, World!", "hello world"), 1.0)

    def test_empty_texts(self):
        self.assertEqual(jaccard(set(), set()), 1.0)
        self.assertEqual(jaccard({"a"}, set()), 0.0)

    def test_shingles_short_text(self):
        self.assertEqual(shingles("hi"), {"hi"})


class DuplicatesTest(DatasetLabTestCase):
    def test_clean_dataset_passes(self):
        ds = make_dataset("t", [
            "the payment gateway timed out waiting for a response",
            "the customer had insufficient funds in the account",
            "the webhook delivery failed after three retries",
        ])
        self.assertPassed(find_duplicates(ds))

    def test_exact_duplicates_found(self):
        ds = make_dataset("t", ["same incident text", "same incident text",
                                "something else entirely different here"])
        report = find_duplicates(ds)
        self.assertFalse(report.passed)
        self.assertEqual(len(report.exact_groups), 1)
        self.assertEqual(report.exact_duplicate_count, 1)

    def test_exact_duplicates_normalization_insensitive(self):
        ds = make_dataset("t", ["Same Incident Text!", "same incident text"])
        report = find_duplicates(ds)
        self.assertEqual(len(report.exact_groups), 1)

    def test_near_duplicates_found(self):
        ds = make_dataset("t", [
            "the payment gateway stopped responding before the transaction completed",
            "the payment gateway stopped responding before the transaction finished",
            "a completely unrelated report about expired cards today",
        ])
        report = find_duplicates(ds, near_dup_threshold=0.5)
        self.assertTrue(report.near_pairs)
        self.assertGreaterEqual(report.near_pairs[0].similarity, 0.5)

    def test_high_threshold_ignores_distant_pairs(self):
        ds = make_dataset("t", [
            "the payment gateway stopped responding before completion",
            "the customer card expired last tuesday morning",
        ])
        report = find_duplicates(ds, near_dup_threshold=0.95)
        self.assertPassed(report)


if __name__ == "__main__":
    unittest.main()
