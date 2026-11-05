"""Export only aggregate test counts; TRX may contain sensitive test output."""

import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def main() -> int:
    root = Path(sys.argv[1])
    summary = {}
    for name in ("unit", "integration", "security"):
        reports = list((root / name).glob("*.trx"))
        if len(reports) != 1:
            summary[name] = {"status": "not-run-or-report-missing"}
            continue
        try:
            tree = ET.parse(reports[0])
            node = tree.find(".//{*}Counters")
            if node is None:
                raise ValueError("missing counters")
            summary[name] = {key: int(node.attrib[key]) for key in
                             ("total", "passed", "failed")}
            summary[name]["skipped"] = int(node.attrib["notExecuted"])
        except (ET.ParseError, ValueError, KeyError):
            summary[name] = {"status": "invalid-report"}
    Path("ci-evidence/test-counts.json").write_text(
        json.dumps({"scope": "local doubles only", "suites": summary}, indent=2) + "\n",
        encoding="utf-8",
    )
    return 0 if all(suite.get("total", 0) > 0 and suite["failed"] == 0
                    and suite["skipped"] == 0 for suite in summary.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
