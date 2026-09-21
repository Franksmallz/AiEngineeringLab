from transformers import AutoTokenizer, AutoModelForCausalLM
from peft import LoraConfig, get_peft_model
from transformers import TrainingArguments, Trainer, DataCollatorForSeq2Seq
import torch

print("CUDA available:", torch.cuda.is_available())

model_id = "Qwen/Qwen2.5-0.5B"

tokenizer = AutoTokenizer.from_pretrained(model_id)

model = AutoModelForCausalLM.from_pretrained(model_id)

from datasets import load_dataset

dataset = load_dataset(
    "json",
    data_files="data/train.jsonl"
)



def tokenize_example(example):
    prompt = f"""Incident:
{example["input"]}

Response:
"""

    response = example["output"]

    prompt_tokens = tokenizer(
        prompt,
        add_special_tokens=False
    )

    response_tokens = tokenizer(
        response,
        add_special_tokens=False
    )

    input_ids = (
        prompt_tokens["input_ids"]
        + response_tokens["input_ids"]
        + [tokenizer.eos_token_id]
    )

    attention_mask = [1] * len(input_ids)

    labels = (
        [-100] * len(prompt_tokens["input_ids"])
        + response_tokens["input_ids"]
        + [tokenizer.eos_token_id]
    )

    return {
        "input_ids": input_ids,
        "attention_mask": attention_mask,
        "labels": labels
    }
        

tokenized_dataset = dataset["train"].map(
    tokenize_example,
    remove_columns = dataset["train"].column_names)

lora_config = LoraConfig(
    r=8,
    lora_alpha=16,
    lora_dropout=0.05,
    bias="none",
    task_type="CAUSAL_LM",
    target_modules=["q_proj", "v_proj"]
)

model = get_peft_model(
    model,
    lora_config
)

data_collator = DataCollatorForSeq2Seq(
    tokenizer=tokenizer,
    model=model,
    padding = True,
    label_pad_token_id = -100,
    return_tensors = "pt"
)

training_args = TrainingArguments(
    output_dir="outputs/qwen-payment-lora",
    num_train_epochs=3,
    per_device_train_batch_size=2,
    gradient_accumulation_steps=4,
    learning_rate=2e-4,
    logging_steps=5,
    save_strategy="epoch",
    report_to="none",
    dataloader_pin_memory=False
)

trainer = Trainer(
    model=model,
    args=training_args,
    train_dataset=tokenized_dataset,
    data_collator=data_collator
)

sample = tokenized_dataset[0]

tokens = tokenizer.convert_ids_to_tokens(sample["input_ids"])

for token, label in zip(tokens, sample["labels"]):
    print(f"{token:20} -> {label}")

masked_count = sum(1 for label in sample["labels"] if label == -100)
trained_count = sum(1 for label in sample["labels"] if label != -100)

print(f"Masked prompt tokens: {masked_count}")
print(f"Response tokens used for loss: {trained_count}")

model.save_pretrained("outputs/qwen-payment-lora")
tokenizer.save_pretrained("outputs/qwen-payment-lora")

print("Training complete.")
