"""Allow `python -m dataset_lab ...` as well as `python -m dataset_lab.cli ...`."""
from dataset_lab.cli import main

if __name__ == "__main__":
    raise SystemExit(main())
