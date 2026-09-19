import json
from pathlib import Path

from transformers import AutoTokenizer, AutoModelForCausalLM

model_id = "Qwen/Qwen2.5-0.5B"

tokenizer = AutoTokenizer.from_pretrained(model_id)

model = AutoModelForCausalLM.from_pretrained(
    model_id
)

import json

prompts = []

data_path = Path(__file__).resolve().parent / "data" / "eval-20-baseline.jsonl"   
with open(
    data_path,
    "r",
    encoding="utf-8"
) as file:

    for line_number, line in enumerate(file, start=1):
        line = line.strip()

        if not line:
            continue

        try:
            prompts.append(json.loads(line))
        except json.JSONDecodeError:
            print(f"Invalid JSON on line {line_number}:")
            print(line)
            break

print("Loaded cases:", len(prompts))

results = []

for prompt in prompts:
    userInput = f"""Incident:
    {prompt["input"]}

    Response:
    """

    inputs = tokenizer(
        userInput,
        return_tensors="pt"
    ).to(model.device)

    tokenizer.pad_token = tokenizer.eos_token
    outputs = model.generate(
        **inputs,
        max_new_tokens=80,
        do_sample=False,
        pad_token_id=tokenizer.eos_token_id
    )

    generated_tokens = outputs[0][inputs["input_ids"].shape[1]:]

    response = tokenizer.decode(
        generated_tokens,
        skip_special_tokens=True
    ).strip()

    results.append({
        "input": prompt["input"],
        "expected": prompt["expected"],
        "actual": response
    })

output_path = Path(__file__).resolve().parent / "outputs" / "baseline-results.json"
output_path.parent.mkdir(parents=True, exist_ok=True)

with output_path.open("w", encoding="utf-8") as file:
    json.dump(results, file, indent=2)

print("Baseline complete.")
