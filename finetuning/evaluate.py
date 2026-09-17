import json
from transformers import AutoTokenizer, AutoModelForCausalLM
from peft import PeftModel
from pathlib import Path

base_model_id = "Qwen/Qwen2.5-0.5B"
adapter_path = "outputs/qwen-payment-lora"

tokenizer = AutoTokenizer.from_pretrained(adapter_path)

base_model = AutoModelForCausalLM.from_pretrained(
    base_model_id,
    device_map="auto"
)

model = PeftModel.from_pretrained(
    base_model,
    adapter_path
)

model.eval()

eval_cases = []

eval_path = Path(__file__).resolve().parent / "data" / "eval.jsonl"
with open(
   eval_path,
    "r",
    encoding="utf-8"
) as file:

     for line_number, line in enumerate(file, start=1):
        line = line.strip()

        if not line:
            continue

        try:
            eval_cases.append(json.loads(line))
        except json.JSONDecodeError:
                break

print("Loaded cases:", len(eval_cases))



results = []

for case in eval_cases:
    prompt = f"""Incident:
    {case["input"]}

    Response:
    """

    inputs = tokenizer(
        prompt,
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

output_path = Path(__file__).resolve().parent / "outputs" / "finetuned-results.json"
output_path.parent.mkdir(parents=True, exist_ok=True)

with output_path.open("w", encoding="utf-8") as file:
    json.dump(results, file, indent=2)

print("Evaluation complete.")