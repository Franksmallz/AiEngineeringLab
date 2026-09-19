"""Command-line interface.

    python -m dataset_lab.cli validate --train train.jsonl --eval eval.jsonl \\
        --report-md reports/report.md --report-json reports/report.json

    python -m dataset_lab.cli freeze --train train.jsonl --eval eval.jsonl \\
        --version v1 --manifest manifests/dataset-manifest.json

    python -m dataset_lab.cli check --manifest manifests/dataset-manifest.json

Exit code is 0 when all checks pass, 1 otherwise (CI-friendly).
"""
from __future__ import annotations

import argparse
import sys

from dataset_lab import __version__, load_jsonl
from dataset_lab.balance import check_balance
from dataset_lab.duplicates import find_duplicates
from dataset_lab.leakage import check_leakage
from dataset_lab.report import PipelineReport
from dataset_lab.schema import validate_schema
from dataset_lab.versioning import Manifest, check_drift, freeze


def cmd_validate(args: argparse.Namespace) -> int:
    train = load_jsonl(args.train, split="train")
    datasets = {"train": train}
    if args.eval:
        datasets["eval"] = load_jsonl(args.eval, split="eval")

    report = PipelineReport(
        schemas={n: validate_schema(d) for n, d in datasets.items()},
        balances={n: check_balance(d, args.max_imbalance)
                  for n, d in datasets.items()},
        duplicates={n: find_duplicates(d, args.threshold)
                    for n, d in datasets.items()},
        leakage=(check_leakage(datasets["train"], datasets["eval"],
                               args.threshold)
                 if "eval" in datasets else None),
    )

    if args.report_json:
        report.save_json(args.report_json)
        print(f"wrote {args.report_json}")
    if args.report_md:
        report.save_markdown(args.report_md)
        print(f"wrote {args.report_md}")
    if not args.report_json and not args.report_md:
        print(report.to_markdown())

    print(f"overall: {'PASS' if report.passed else 'FAIL'}")
    return 0 if report.passed else 1


def cmd_freeze(args: argparse.Namespace) -> int:
    datasets = {"train": load_jsonl(args.train, split="train")}
    paths = {"train": args.train}
    if args.eval:
        datasets["eval"] = load_jsonl(args.eval, split="eval")
        paths["eval"] = args.eval
    manifest = freeze(datasets, paths, args.version)
    manifest.save(args.manifest)
    print(f"froze version {args.version} -> {args.manifest}")
    return 0


def cmd_check(args: argparse.Namespace) -> int:
    manifest = Manifest.load(args.manifest)
    drift = check_drift(manifest)
    if drift.drifted:
        print(f"DRIFT detected against version {manifest.version}:")
        for detail in drift.details:
            print(f"  - {detail}")
        return 1
    print(f"no drift: working files match manifest version {manifest.version}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="dataset-lab",
        description="Dataset curation and testing pipeline (ch. 8 artifact).")
    parser.add_argument("--version", action="version", version=__version__)
    sub = parser.add_subparsers(dest="command", required=True)

    p_validate = sub.add_parser("validate", help="run all curation checks")
    p_validate.add_argument("--train", required=True)
    p_validate.add_argument("--eval", default=None)
    p_validate.add_argument("--report-md", default=None)
    p_validate.add_argument("--report-json", default=None)
    p_validate.add_argument("--threshold", type=float, default=0.8,
                            help="near-duplicate Jaccard threshold")
    p_validate.add_argument("--max-imbalance", type=float, default=2.0,
                            help="max allowed class imbalance ratio")
    p_validate.set_defaults(func=cmd_validate)

    p_freeze = sub.add_parser("freeze", help="write a version manifest")
    p_freeze.add_argument("--train", required=True)
    p_freeze.add_argument("--eval", default=None)
    p_freeze.add_argument("--version", required=True)
    p_freeze.add_argument("--manifest", required=True)
    p_freeze.set_defaults(func=cmd_freeze)

    p_check = sub.add_parser("check", help="check working files vs manifest")
    p_check.add_argument("--manifest", required=True)
    p_check.set_defaults(func=cmd_check)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
