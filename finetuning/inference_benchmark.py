import json
import os
import random
import time
from dataclasses import dataclass, asdict
from typing import Any

import numpy as np
import torch


@dataclass
class EvalCase:
    input: str
    expected: str


@dataclass
class BenchmarkResult:
    ExperimentName: str
    ModelName: str
    Configuration: str
    EvaluationCases: int
    BatchSize: int
    MaxNewTokens: int
    Precision: str
    AmortizedTimePerCaseMs: float
    TokensPerSecond: float
    MemoryUsedMb: float
    TotalGeneratedTokens: int
    TotalRunTimeSeconds: float
    CasesHittingTokenLimit: int
    ExactMatchCount: int | None
    Successful: bool
    Notes: str


def set_seed(seed: int = 42) -> None:
    """
    Makes the experiment as deterministic as practical.
    do_sample=False already makes generation deterministic,
    but fixing seeds makes experiment lineage clearer.
    """
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)


def load_eval_cases(path: str) -> list[EvalCase]:
    """
    Loads the frozen JSONL evaluation dataset.
    Expected format per line:
      {"input": "...", "expected": "Category: ...\nRetryable: ...\nAction: ..."}
    """
    cases: list[EvalCase] = []
    with open(path, "r", encoding="utf-8") as file:
        for line_number, line in enumerate(file, start=1):
            line = line.strip()
            if not line:
                continue

            item = json.loads(line)

            if "input" not in item:
                raise ValueError(
                    f"Missing 'input' on line {line_number}"
                )
            if "expected" not in item:
                raise ValueError(
                    f"Missing 'expected' on line {line_number}"
                )

            cases.append(
                EvalCase(
                    input=item["input"],
                    expected=item["expected"]
                )
            )

    return cases


def build_prompt(incident: str) -> str:
    """
    IMPORTANT:
    Keep this identical to the prompt used during evaluation
    of the fine-tuned model.
    """

    return f"""Incident:
{incident}

Response:
"""


def run_benchmark(
    *,
    model,
    tokenizer,
    eval_cases: list[EvalCase],
    batch_size: int,
    max_new_tokens: int,
    warmup_runs: int = 0,
    repetitions: int = 1
) -> dict[str, Any]:
    """
    Runs inference against the frozen evaluation set.
    Measures:
      - averaged wall-clock runtime
      - tokens generated
      - GPU peak memory
      - token-limit hits
    """

    prompts = [
        build_prompt(case.input)
        for case in eval_cases
    ]

    model.eval()

    if torch.cuda.is_available():
        torch.cuda.reset_peak_memory_stats()

    start_time = time.perf_counter()

    results = []

    if batch_size <= 0:
        raise ValueError("batch_size must be greater than zero")
    if max_new_tokens <= 0:
        raise ValueError("max_new_tokens must be greater than zero")
    if warmup_runs < 0:
        raise ValueError("warmup_runs cannot be negative")
    if repetitions <= 0:
        raise ValueError("repetitions must be greater than zero")

    def run_once() -> list[dict[str, Any]]:
        results = []

        for batch_start in range(0, len(prompts), batch_size):
            batch_prompts = prompts[batch_start:batch_start + batch_size]

            inputs = tokenizer(
                batch_prompts,
                return_tensors="pt",
                padding=True
            ).to(model.device)

            prompt_length = inputs["input_ids"].shape[1]

            outputs = model.generate(
                **inputs,
                max_new_tokens=max_new_tokens,
                do_sample=False
            )

            for index in range(len(batch_prompts)):
                generated_tokens = outputs[index][prompt_length:]

                response = tokenizer.decode(
                    generated_tokens,
                    skip_special_tokens=True
                ).strip()

                results.append({
                    "input": eval_cases[batch_start + index].input,
                    "expected": eval_cases[batch_start + index].expected,
                    "actual": response,
                    "generated_tokens": len(generated_tokens),
                    "hit_token_limit": (
                        len(generated_tokens) >= max_new_tokens
                    )
                })

        return results

    for _ in range(warmup_runs):
        run_once()

    if torch.cuda.is_available():
        torch.cuda.synchronize()

    start_time = time.perf_counter()
    measured_results = []

    for _ in range(repetitions):
        measured_results = run_once()

    if torch.cuda.is_available():
        torch.cuda.synchronize()

    total_run_time_seconds = time.perf_counter() - start_time
    average_run_time_seconds = total_run_time_seconds / repetitions
    results = measured_results

    total_generated_tokens = sum(
        item["generated_tokens"]
        for item in results
    )

    cases_hitting_token_limit = sum(
        1
        for item in results
        if item["hit_token_limit"]
    )

    peak_memory_mb = 0.0
    if torch.cuda.is_available():
        peak_memory_mb = (
            torch.cuda.max_memory_allocated() / 1024 / 1024
        )

    tokens_per_second = (
        total_generated_tokens / average_run_time_seconds
        if average_run_time_seconds > 0
        else 0.0
    )

    amortized_time_per_case_ms = (
        average_run_time_seconds * 1000 / len(results)
        if results
        else 0.0
    )

    return {
        "results": results,
        "total_run_time_seconds": average_run_time_seconds,
        "total_generated_tokens": total_generated_tokens,
        "tokens_per_second": tokens_per_second,
        "amortized_time_per_case_ms": amortized_time_per_case_ms,
        "peak_memory_mb": peak_memory_mb,
        "cases_hitting_token_limit": cases_hitting_token_limit
    }


def compare_exact_outputs(
    reference_outputs_path: str,
    current_results: list[dict[str, Any]]
) -> int:
    """
    Compares generated responses against a reference JSON output file.
    Returns the number of exact response matches.
    """
    with open(reference_outputs_path, "r", encoding="utf-8") as file:
        reference_items = json.load(file)

    if len(reference_items) != len(current_results):
        raise ValueError(
            "Reference and current results contain different numbers "
            f"of cases: {len(reference_items)} != {len(current_results)}"
        )

    exact_matches = 0

    for index, (reference, current) in enumerate(
        zip(reference_items, current_results)
    ):
        if reference.get("input") != current.get("input"):
            raise ValueError(
                "Reference and current results are not aligned at case "
                f"{index}"
            )

        reference_actual = reference.get("actual", reference.get("response"))
        current_actual = current.get("actual", current.get("response"))

        if reference_actual is None or current_actual is None:
            raise ValueError(
                f"Missing generated response at case {index}"
            )

        if reference_actual == current_actual:
            exact_matches += 1

    return exact_matches


def save_outputs(
    outputs_path: str,
    results: list[dict[str, Any]]
) -> None:
    """Saves per-case outputs to JSON."""
    os.makedirs(
        os.path.dirname(outputs_path) or ".",
        exist_ok=True
    )

    with open(outputs_path, "w", encoding="utf-8") as file:
        json.dump(results, file, indent=2)


def save_benchmark_result(
    summary_path: str,
    result: BenchmarkResult
) -> None:
    """Saves the benchmark summary record to JSON."""
    os.makedirs(
        os.path.dirname(summary_path) or ".",
        exist_ok=True
    )

    with open(summary_path, "w", encoding="utf-8") as file:
        json.dump(asdict(result), file, indent=2)


def print_metrics(
    *,
    experiment_name: str,
    benchmark: dict[str, Any],
    evaluation_cases: int,
    exact_match_count: int | None
) -> None:
    """Prints the key benchmark metrics."""
    print()
    print("=" * 70)
    print(f"Benchmark: {experiment_name}")
    print("=" * 70)

    print(
        f"Total runtime: "
        f"{benchmark['total_run_time_seconds']:.2f} seconds"
    )

    print(
        f"Total generated tokens: "
        f"{benchmark['total_generated_tokens']}"
    )

    print(
        f"Throughput: "
        f"{benchmark['tokens_per_second']:.2f} tokens/second"
    )

    print(
        f"Amortized time per case: "
        f"{benchmark['amortized_time_per_case_ms']:.2f} ms"
    )

    print(
        f"Peak GPU memory: "
        f"{benchmark['peak_memory_mb']:.2f} MB"
    )

    print(
        f"Cases hitting token limit: "
        f"{benchmark['cases_hitting_token_limit']}"
        f"/{evaluation_cases}"
    )

    if exact_match_count is not None:
        print(
            f"Exact output matches: "
            f"{exact_match_count}"
            f"/{evaluation_cases}"
        )

    print("=" * 70)
