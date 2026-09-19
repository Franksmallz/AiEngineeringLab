from transformers import AutoTokenizer, AutoModelForCausalLM
from peft import LoraConfig, get_peft_model
from transformers import TrainingArguments, Trainer, DataCollatorForLanguageModeling
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


def format_example(example):
    text = f"""Incident:
{example["input"]}

Response:
{example["output"]}"""

    return {"text": text}

formatted_dataset = dataset["train"].map(format_example)

def tokenize_example(example):
    return tokenizer(
        example["text"],
        truncation=True,
        max_length=512
    )

tokenized_dataset = formatted_dataset.map(
    tokenize_example
)

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

data_collator = DataCollatorForLanguageModeling(
    tokenizer=tokenizer,
    mlm=False
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

trainer.train()

model.save_pretrained("outputs/qwen-payment-lora")
tokenizer.save_pretrained("outputs/qwen-payment-lora")

print("Training complete.")
