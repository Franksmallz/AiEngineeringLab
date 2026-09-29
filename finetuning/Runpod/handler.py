import runpod

from model import PaymentIncidentModel


model = PaymentIncidentModel()


def parse_output(raw_output: str):
    category = None
    retryable = None
    action = None

    for line in raw_output.splitlines():
        line = line.strip()

        if line.lower().startswith("category:"):
            category = line.split(":", 1)[1].strip()

        elif line.lower().startswith("retryable:"):
            value = line.split(":", 1)[1].strip().lower()

            if value == "yes":
                retryable = True
            elif value == "no":
                retryable = False

        elif line.lower().startswith("action:"):
            action = line.split(":", 1)[1].strip()

    is_valid = (
        category is not None
        and retryable is not None
        and action is not None
    )

    return {
        "category": category,
        "retryable": retryable,
        "action": action,
        "isValid": is_valid,
        "rawOutput": raw_output
    }


def handler(job):
    try:
        job_input = job.get("input", {})

        incident = job_input.get("incident")

        if not incident:
            return {
                "error": "incident is required"
            }

        raw_output = model.generate(incident)

        parsed = parse_output(raw_output)

        if not parsed["isValid"]:
            return {
                "category": "Manual Review Required",
                "retryable": False,
                "action": "Escalate the incident for manual review.",
                "isValid": False,
                "rawOutput": raw_output
            }

        return parsed

    except Exception as ex:
        return {
            "error": str(ex)
        }


runpod.serverless.start(
    {
        "handler": handler
    }
)