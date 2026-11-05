"""CI-1: restore with NuGetAudit, failing closed without printing package URLs/values.

NU1900 means the advisory source could not be consulted; NU1903/4 block the
build, while NU1901/2 remain visible as counts. Never treat missing restore
output or a failed restore as a clean security result.
"""

import argparse
import json
import re
import subprocess
from pathlib import Path


SOLUTION = "UTTTGO.Backend.slnx"
CODES = re.compile(r"\bNU190[0-4]\b")
HIGH = {"NU1903", "NU1904"}


def outcome(exit_code: int, codes: set[str]) -> str:
    if codes & HIGH:
        return "vulnerable"
    if "NU1900" in codes:
        return "advisories_unavailable"
    if exit_code != 0:
        return "restore_failed"
    return "passed"


def run_restore() -> tuple[int, set[str]]:
    command = [
        "dotnet", "restore", SOLUTION, "--force-evaluate",
        "-p:NuGetAudit=true", "-p:NuGetAuditMode=all", "-p:NuGetAuditLevel=low",
    ]
    result = subprocess.run(command, capture_output=True, text=True, errors="replace", check=False)
    # Do not echo the raw NuGet log: sources or package metadata may be private.
    return result.returncode, set(CODES.findall(result.stdout + result.stderr))


def audit(restore=None) -> tuple[str, set[str], int]:
    if restore is None:
        restore = run_restore
    attempts = 0
    try:
        code, codes = restore()
        attempts += 1
        status = outcome(code, codes)
        if status == "advisories_unavailable":
            code, codes = restore()
            attempts += 1
            status = outcome(code, codes)
    except OSError:
        codes, status = set(), "restore_failed"
    return status, codes, attempts


def self_test() -> None:
    cases = [
        (0, set(), "passed"), (0, {"NU1901"}, "passed"),
        (0, {"NU1902"}, "passed"), (0, {"NU1903"}, "vulnerable"),
        (0, {"NU1904"}, "vulnerable"),
        (0, {"NU1900"}, "advisories_unavailable"),
        (1, {"NU1900"}, "advisories_unavailable"),
        (1, set(), "restore_failed"),
    ]
    for exit_code, codes, expected in cases:
        assert outcome(exit_code, codes) == expected
    for sequence, expected in [
        ([(0, {"NU1900"}), (0, {"NU1900"})], "advisories_unavailable"),
        ([(0, {"NU1900"}), (0, set())], "passed"),
        ([(0, {"NU1900"}), (0, {"NU1903"})], "vulnerable"),
    ]:
        responses = iter(sequence)
        status, _, attempts = audit(lambda: next(responses))
        assert (status, attempts) == (expected, 2)
    print("NuGetAudit decisions and retries: 11/11 passed")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--report", default="ci-evidence/nuget-audit.json")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0

    status, codes, attempts = audit()

    report = {"status": status, "attempts": attempts, "warningCodes": sorted(codes),
              "scope": "NuGetAuditMode=all; direct and transitive packages"}
    destination = Path(args.report)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"NuGetAudit: {status}; attempts={attempts}; codes={','.join(sorted(codes)) or 'none'}")
    if status == "advisories_unavailable":
        print("Advisory lookup failed after retry; this is NOT a clean scan.")
    return 0 if status == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
