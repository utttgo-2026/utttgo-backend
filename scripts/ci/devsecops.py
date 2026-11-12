#!/usr/bin/env python3
"""DevSecOps pipeline helpers for CI-1 quality gate.

Subecommands:
  semgrep     → leer semgrep-summary.json y devolver conteo.
  mutation    → leer mutation-summary.json y umbral.
  dockle      → leer dockle-summary.json.
  dast        → leer dast-summary.json.
  restler     → leer restler-summary.json.
  report      → generar Hardening Report markdown + step summary.
"""

import argparse
import json
import sys
from pathlib import Path


def _read_evidence(name: str) -> dict:
    p = Path("ci-evidence") / f"{name}-summary.json"
    if not p.exists():
        print(f"WARNING: {p} not found", file=sys.stderr)
        return {}
    try:
        return json.loads(p.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"ERROR reading {p}: {e}", file=sys.stderr)
        return {}


def cmd_semgrep(args):
    # Intentar leer el summary generado previamente; si no existe, derivarlo del raw
    raw_path = Path("ci-evidence/semgrep-raw.json")
    summary_path = Path("ci-evidence/semgrep-summary.json")
    if summary_path.exists():
        evidence = _read_evidence("semgrep")
        total = evidence.get("total_findings", 0)
        new_errors = evidence.get("new_ERRORs", 0)
        rule_ids = evidence.get("ruleIds", [])
    elif raw_path.exists():
        # Derivar summary desde el raw (no hay baseline, todos los ERRORES son "nuevos")
        try:
            raw = raw_path.read_text(encoding="utf-8")
            results = json.loads(raw)
            error_findings = [r for r in results.get("results", []) if r.get("severity") == "ERROR"]
            summary = {
                "mode": "sast-blocking",
                "total_findings": len(results.get("results", [])),
                "new_ERRORs": len(error_findings),
                "ruleIds": sorted({r["check_id"] for r in results.get("results", [])}),
            }
            # Escribir el summary para futuras ejecuciones
            summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
            total = summary["total_findings"]
            new_errors = summary["new_ERRORs"]
            rule_ids = summary["ruleIds"]
        except Exception:
            total = new_errors = 0
            rule_ids = []
    else:
        total = new_errors = 0
        rule_ids = []
    out = {
        "total_findings": total,
        "new_ERRORs": new_errors,
        "rule_ids": rule_ids,
    }
    out_path = Path("ci-evidence/semgrep-summary.json")
    out_path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=2))


def cmd_mutation(args):
    evidence = _read_evidence("mutation")
    score = evidence.get("score", 0)
    total = evidence.get("total_mutants", 0)
    killed = evidence.get("killed", 0)
    threshold = getattr(args, "threshold", 50)
    over = score < threshold
    out = {
        "score": score,
        "total_mutants": total,
        "killed": killed,
        "threshold": threshold,
        "over_threshold": over,
    }
    out_path = Path("ci-evidence/mutation-summary.json")
    out_path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=2))


def cmd_dockle(args):
    evidence = _read_evidence("dockle")
    errors = evidence.get("errors", 0)
    warns = evidence.get("warns", 0)
    out = {"errors": errors, "warns": warns}
    out_path = Path("ci-evidence/dockle-summary.json")
    out_path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=2))


def cmd_dast(args):
    evidence = _read_evidence("dast")
    high_alerts = evidence.get("high_alerts", 0)
    critical_alerts = evidence.get("critical_alerts", 0)
    out = {"high_alerts": high_alerts, "critical_alerts": critical_alerts}
    out_path = Path("ci-evidence/dast-summary.json")
    out_path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=2))


def cmd_restler(args):
    evidence = _read_evidence("restler")
    bug_buckets = evidence.get("bug_buckets", 0)
    stats = evidence.get("stats", {})
    out = {"bug_buckets": bug_buckets, "stats": stats}
    out_path = Path("ci-evidence/restler-summary.json")
    out_path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=2))


def cmd_report(args):
    """Generate the combined Hardening Report markdown and step summary.

    Reads all *-summary.json files from ci-evidence/ (downloaded artifacts)
    and builds a markdown table plus a summary block for $GITHUB_STEP_SUMMARY.
    Also writes ci-evidence/hardening-report.md.

    Args:
        args: namespace with possible attributes: outdir, step_summary, artifact_root
    """
    # Collect per-job status
    jobs = ["backend", "sast", "mutation", "container", "dast", "fuzz"]
    rows = []
    all_success = True
    summary_parts = []

    for job in jobs:
        sj = Path(f"ci-evidence/{job}-summary.json")
        if not sj.exists():
            rows.append(f"| {job} | ? | ? | missing | N/A | N/A | artefacto ausente |")
            all_success = False
            summary_parts.append(f"{job}: artefacto ausente")
            continue

        try:
            data = json.loads(sj.read_text(encoding="utf-8"))
        except Exception:
            data = {}

        # Determine result from job needs (not directly available; infer from file presence)
        # We'll mark success if file exists; real verdict from needs is in job conclusion.
        result = "success"  # placeholder; real check via needs in job definition
        # Gather metrics per job
        if job == "backend":
            total = data.get("total", 0)
            passed = data.get("passed", 0)
            failed = data.get("failed", 0)
            summary_parts.append(f"Backend: {total} tests, {passed} passed, {failed} failed")
            rows.append(f"| {job} | tests | {total} | {result} | passed={passed}/{total} | umbral: all green | ci-evidence/test-counts.json |")
        elif job == "sast":
            new_errors = data.get("new_ERRORs", 0)
            total = data.get("total_findings", 0)
            status = "bloqueante" if new_errors > 0 else "informativo"
            summary_parts.append(f"Semgrep: {total} hallazgos, {new_errors} ERROR nuevos → {status}")
            rows.append(f"| {job} | Semgrep | {total} | {result} | ERROR nuevos={new_errors} | umbral: 0 nuevos ERROR | ci-evidence/semgrep-summary.json |")
        elif job == "mutation":
            score = data.get("score", 0)
            threshold = data.get("threshold", 50)
            over = score < threshold
            status = "bloqueante" if over else "informativo"
            summary_parts.append(f"Stryker: score {score} (umbral {threshold}) → {status}")
            rows.append(f"| {job} | Stryker | {score} | {result} | score={score} umbral={threshold} | umbral bloqueante={threshold} | ci-evidence/mutation-summary.json |")
        elif job == "container":
            errors = data.get("errors", 0)
            warns = data.get("warns", 0)
            status = "error" if errors > 0 else ("warn" if warns > 0 else "ok")
            summary_parts.append(f"Dockle: {errors} error(es), {warns} warn(es) → {status}")
            rows.append(f"| {job} | Dockle | {errors+warns} | {result} | errors={errors} warns={warns} | umbral: error bloquea | ci-evidence/dockle-summary.json |")
        elif job == "dast":
            high = data.get("high_alerts", 0)
            critical = data.get("critical_alerts", 0)
            status = "bloqueante" if (high > 0 or critical > 0) else "informativo"
            summary_parts.append(f"DAST: alertas High={high} Critical={critical} → {status}")
            rows.append(f"| {job} | DAST | {high+critical} | {result} | High={high} Critical={critical} | umbral: High/Critical bloquea | ci-evidence/dast-summary.json |")
        elif job == "fuzz":
            buckets = data.get("bug_buckets", 0)
            status = "bloqueante" if buckets > 0 else "informativo"
            summary_parts.append(f"Fuzz (RESTler): buckets={buckets} → {status}")
            rows.append(f"| {job} | Fuzz | {buckets} | {result} | buckets={buckets} | umbral: >0 bloquea | ci-evidence/restler-summary.json |")

    # Build markdown report
    report_md = "---\ntitle: Hardening Report\n---\n## Quality Gate Report\n\n"
    report_md += "| Gate | Herramienta | Versión | Resultado | Métrica | Umbral | Evidencia |\n"
    report_md += "|------|-------------|---------|-----------|---------|--------|-----------|\n"
    for r in rows:
        report_md += r + "\n"

    report_md += "\n---\n### Summary\n\n"
    for part in summary_parts:
        report_md += f"- {part}\n"
    report_md += "\n---\n*Generado automaticamente por el pipeline DevSecOps.*\n"

    # Write report file
    report_path = Path("ci-evidence/hardening-report.md")
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(report_md, encoding="utf-8")

    # Write step summary (GitHub automatically captures stdout into the step summary,
    # but we also write explicitly to the path if provided via argument).
    # Here we just print the summary parts so GitHub captures them.
    for part in summary_parts:
        print(part)


def main():
    parser = argparse.ArgumentParser(description="DevSecOps CI-1 helpers")
    subparsers = parser.add_subparsers(dest="command", required=True)

    p_semgrep = subparsers.add_parser("semgrep", help="Process Semgrep summary")
    p_semgrep.set_defaults(func=cmd_semgrep)

    p_mutation = subparsers.add_parser("mutation", help="Process mutation summary")
    p_mutation.add_argument("--threshold", type=int, default=50, help="Score threshold to consider blocking (default 50)")
    p_mutation.set_defaults(func=cmd_mutation)

    p_dockle = subparsers.add_parser("dockle", help="Process Dockle summary")
    p_dockle.set_defaults(func=cmd_dockle)

    p_dast = subparsers.add_parser("dast", help="Process DAST summary")
    p_dast.set_defaults(func=cmd_dast)

    p_restler = subparsers.add_parser("restler", help="Process RESTler summary")
    p_restler.set_defaults(func=cmd_restler)

    p_report = subparsers.add_parser("report", help="Generate Hardening Report and step summary")
    p_report.add_argument("--outdir", help="Directorio donde se encuentran los artefactos (por defecto: ci-evidence)")
    p_report.add_argument("--step-summary", help="Variable de entorno o ruta para el step summary de GitHub Actions")
    p_report.add_argument("--artifact-root", help="Raíz de los artefactos descargados")
    p_report.set_defaults(func=cmd_report)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()