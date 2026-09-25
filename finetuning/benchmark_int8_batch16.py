import argparse


import torch
from transformers import (
    AutoModelForCausalLM,
    AutoTokenizer,
    BitsAndBytesConfig
)
from peft import PeftModel


from inference_benchmark import (
    BenchmarkResult,
    compare_exact_outputs,
    load_eval_cases,
    print_metrics,
    run_benchmark,
    save_benchmark_result,
    save_outputs,
    set_seed
)


MODEL_NAME = "Qwen/Qwen2.5-0.5B"

BATCH_SIZE = 16
MAX_NEW_TOKENS = 80
SEED = 42
WARMUP_RUNS = 2
REPETITIONS = 3


def parse_args():
    parser = argparse.ArgumentParser(
        description=(
            "INT8 Batch 16 inference benchmark"
        )
    )

    parser.add_argument(
        "--adapter-path",
        required=True
    )

    parser.add_argument(
        "--evaluation-file",
        required=True
    )

    parser.add_argument(
        "--fp16-reference",
        required=True,
        help=(
            "FP16 Batch 16 output JSON used "
            "for behavioral parity"
        )
    )

    parser.add_argument(
        "--output-dir",
        default="results"
    )

    return parser.parse_args()


def main():
    args = parse_args()

    set_seed(SEED)

    eval_cases = load_eval_cases(
        args.evaluation_file
    )

    print(
        f"Loaded evaluation cases: "
        f"{len(eval_cases)}"
    )

    tokenizer = AutoTokenizer.from_pretrained(
        MODEL_NAME
    )

    tokenizer.padding_side = "left"

    if tokenizer.pad_token is None:
        tokenizer.pad_token = (
            tokenizer.eos_token
        )

    quantization_config = BitsAndBytesConfig(
        load_in_8bit=True
    )

    print("Loading INT8 base model...")

    base_model = (
        AutoModelForCausalLM.from_pretrained(
            MODEL_NAME,
            quantization_config=(
                quantization_config
            ),
            device_map="auto"
        )
    )

    print(
        f"Loading LoRA adapter: "
        f"{args.adapter_path}"
    )

    model = PeftModel.from_pretrained(
        base_model,
        args.adapter_path
    )

    model.eval()

    benchmark = run_benchmark(
        model=model,
        tokenizer=tokenizer,
        eval_cases=eval_cases,
        batch_size=BATCH_SIZE,
        max_new_tokens=MAX_NEW_TOKENS,
        warmup_runs=WARMUP_RUNS,
        repetitions=REPETITIONS
    )

    exact_matches = compare_exact_outputs(
        args.fp16_reference,
        benchmark["results"]
    )

    outputs_path = (
        f"{args.output_dir}/"
        "int8-batch16-outputs.json"
    )

    summary_path = (
        f"{args.output_dir}/"
        "int8-batch16-result.json"
    )

    save_outputs(
        outputs_path,
        benchmark["results"]
    )

    result = BenchmarkResult(
        ExperimentName="INT8-Batch16",
        ModelName="Qwen2.5-0.5B-payment",
        Configuration=(
            "batch=16,"
            "max_new_tokens=80,"
            "do_sample=false,"
            "INT8"
        ),
        EvaluationCases=len(eval_cases),
        BatchSize=BATCH_SIZE,
        MaxNewTokens=MAX_NEW_TOKENS,
        Precision="INT8",

        AmortizedTimePerCaseMs=(
            benchmark[
                "amortized_time_per_case_ms"
            ]
        ),
        TokensPerSecond=(
            benchmark["tokens_per_second"]
        ),
        MemoryUsedMb=(
            benchmark["peak_memory_mb"]
        ),
        TotalGeneratedTokens=(
            benchmark[
                "total_generated_tokens"
            ]
        ),
        TotalRunTimeSeconds=(
            benchmark[
                "total_run_time_seconds"
            ]
        ),
        CasesHittingTokenLimit=(
            benchmark[
                "cases_hitting_token_limit"
            ]
        ),

        ExactMatchCount=exact_matches,

        Successful=(
            len(benchmark["results"]) == len(eval_cases)
        ),
        Notes=(
            "Chapter 9 INT8 Batch 16"
        )
    )

    save_benchmark_result(
        summary_path,
        result
    )

    print_metrics(
        experiment_name=(
            "INT8 Batch 16"
        ),
        benchmark=benchmark,
        evaluation_cases=len(eval_cases),
        exact_match_count=exact_matches
    )

    print()
    print(f"Outputs: {outputs_path}")
    print(f"Summary: {summary_path}")


if __name__ == "__main__":
    main()
