{
  "ExperimentName": "payment-incident-v2-response-only",
  "TrainingDataset": "dataset_v2.jsonl",
  "TrainingDatasetVersion": "2.0",
  "EvaluationDataset": "evaluation.jsonl",
  "TrainingExampleCount": 50,
  "EvaluationExampleCount": 20,
  "BaseModel": "Qwen/Qwen2.5-0.5B",
  "LossStrategy": "Response-only loss",
  "Notes": "Uses the Chapter 8 V2 dataset with improved scenario diversity. The frozen Chapter 7 evaluation set remains unchanged.",
  "CreatedAtUtc": "2026-09-21T04:29:50.4123917Z"
}
