import unittest

from dataset_lab.records import Dataset, load_jsonl
from dataset_lab.schema import validate_schema
from tests.helpers import DatasetLabTestCase, make_dataset, make_record


class SchemaTest(DatasetLabTestCase):
    def test_valid_dataset_passes(self):
        ds = make_dataset("train", ["first incident", "second incident"])
        self.assertPassed(validate_schema(ds))

    def test_empty_dataset_errors(self):
        result = validate_schema(Dataset(name="empty"))
        self.assertFalse(result.passed)
        self.assertEqual(result.errors[0].code, "empty_dataset")

    def test_missing_input_errors(self):
        ds = Dataset(name="t", records=[make_record("")])
        result = validate_schema(ds)
        self.assertFalse(result.passed)
        self.assertTrue(any(i.code == "missing_input" for i in result.errors))

    def test_missing_label_errors(self):
        rec = make_record("some input")
        rec.label = "   "
        ds = Dataset(name="t", records=[rec])
        result = validate_schema(ds)
        codes = [i.code for i in result.errors]
        self.assertIn("missing_label", codes)

    def test_malformed_label_errors(self):
        rec = make_record("some input")
        rec.label = "no category line here"
        ds = Dataset(name="t", records=[rec])
        result = validate_schema(ds)
        self.assertTrue(any(i.code == "malformed_label"
                            for i in result.errors))

    def test_unknown_category_errors(self):
        ds = make_dataset("t", ["x"], category="Alien Invasion")
        result = validate_schema(ds)
        self.assertTrue(any(i.code == "unknown_category"
                            for i in result.errors))

    def test_invalid_retryable_errors(self):
        ds = make_dataset("t", ["x"], retryable="Maybe")
        result = validate_schema(ds)
        self.assertTrue(any(i.code == "invalid_retryable"
                            for i in result.errors))

    def test_missing_retryable_is_warning_only(self):
        rec = make_record("some input")
        rec.label = "Category: Provider Timeout\nAction: Do a thing."
        ds = Dataset(name="t", records=[rec])
        result = validate_schema(ds)
        self.assertTrue(result.passed)  # warnings don't fail
        self.assertTrue(any(i.code == "missing_retryable"
                            for i in result.warnings))

    def test_invalid_json_line_reported_not_crashed(self):
        import tempfile, os
        with tempfile.NamedTemporaryFile("w", suffix=".jsonl",
                                         delete=False) as fh:
            fh.write('{"input": "ok", "output": "Category: Provider Timeout\\n'
                     'Retryable: Yes\\nAction: x."}\n')
            fh.write("this is not json\n")
            path = fh.name
        try:
            ds = load_jsonl(path, split="train")
            result = validate_schema(ds)
            self.assertFalse(result.passed)
            self.assertTrue(any(i.code == "missing_input"
                                for i in result.errors))
        finally:
            os.unlink(path)


if __name__ == "__main__":
    unittest.main()
