import os

import torch
from peft import PeftModel
from transformers import AutoModelForCausalLM, AutoTokenizer


BASE_MODEL = "Qwen/Qwen2.5-0.5B"

ADAPTER_MODEL = "obumodoh/qwen-payment-incident-lora-v2"

ADAPTER_REVISION = (
    "55d10b137d1b1b874c386121e8b0115eeb01e7da"
)


class PaymentIncidentModel:
    def __init__(self):
        hf_token = os.getenv("HF_TOKEN")

        print("Loading tokenizer...")

        self.tokenizer = AutoTokenizer.from_pretrained(
            BASE_MODEL,
            token=hf_token
        )

        self.tokenizer.padding_side = "left"

        if self.tokenizer.pad_token is None:
            self.tokenizer.pad_token = (
                self.tokenizer.eos_token
            )

        print("Loading base model...")

        base_model = AutoModelForCausalLM.from_pretrained(
            BASE_MODEL,
            torch_dtype=torch.float16,
            device_map="auto",
            token=hf_token
        )

        print("Loading LoRA adapter...")

        self.model = PeftModel.from_pretrained(
            base_model,
            ADAPTER_MODEL,
            revision=ADAPTER_REVISION,
            token=hf_token
        )

        self.model.eval()

        print("Model loaded successfully.")

    def generate(
        self,
        incident: str,
        max_new_tokens: int = 80
    ) -> str:

        prompt = self.build_prompt(incident)

        inputs = self.tokenizer(
            prompt,
            return_tensors="pt"
        ).to(self.model.device)

        with torch.no_grad():
            outputs = self.model.generate(
                **inputs,
                max_new_tokens=max_new_tokens,
                do_sample=False,
                pad_token_id=self.tokenizer.eos_token_id
            )

        prompt_length = inputs["input_ids"].shape[1]

        generated_tokens = outputs[0][
            prompt_length:
        ]

        return self.tokenizer.decode(
            generated_tokens,
            skip_special_tokens=True
        ).strip()

    @staticmethod
    def build_prompt(incident: str) -> str:
        return f"""Incident:
{incident}

Response:
"""