namespace FoundationalModel.Models.Dtos.Responses
{
    public static class InferenceScenarios
    {
        public static InferenceExperimentConfig Fp16Batch16 =>
            new()
            {
                ExperimentName = "fp16-batch16",
                ModelName = "Qwen2.5-0.5B-payment",
                EvaluationFile = "eval.jsonl",
                BatchSize = 16,
                MaxNewTokens = 80,
                DoSample = false,
                Precision = "FP16",
                OutputFile = "fp16-batch16-result.json"
            };

        public static InferenceExperimentConfig Int8Batch16 =>
            new()
            {
                ExperimentName = "int8-batch16",
                ModelName = "Qwen2.5-0.5B-payment",
                EvaluationFile = "eval.jsonl",
                BatchSize = 16,
                MaxNewTokens = 80,
                DoSample = false,
                Precision = "INT8",
                OutputFile = "int8-batch16-result.json"
            };

        public static InferenceExperimentConfig Int4Nf4Batch16 =>
            new()
            {
                ExperimentName = "int4-nf4-batch16",
                ModelName = "Qwen2.5-0.5B-payment",
                EvaluationFile = "eval.jsonl",
                BatchSize = 16,
                MaxNewTokens = 80,
                DoSample = false,
                Precision = "INT4-NF4",
                OutputFile = "int4-batch16-result.json"
            };
    }
}
