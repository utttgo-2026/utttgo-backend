#!/usr/bin/env python3
"""Validación local de fichas SDD atómicas (stdlib, sin servicios externos).

Uso: python scripts/validate-sdd-atomic.py [ruta/de/ficha]
     python scripts/validate-sdd-atomic.py --templates
"""

from __future__ import annotations

import argparse
import re
import shutil
import sys
import tempfile
import unicodedata
from collections import Counter
from pathlib import Path
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parent.parent
BLOCK = ROOT / "specs" / "bloque-2-funcionalidades"
TEMPLATE = ROOT / "specs" / "_templates" / "api-atomica"
LEGACY_FOLDERS = {
    (BLOCK / "api-001-register").resolve(),
    (BLOCK / "api-002-login").resolve(),
}
FILES = ("spec.md", "plan.md", "task.md")
STATES = {"READY", "BLOCKED", "DONE", "NOT_APPLICABLE"}
OPERATIVE = {
    "DOCUMENTADA", "READY_PARTIAL", "READY_FOR_IMPLEMENTATION", "BLOCKED",
    "IMPLEMENTED_PENDING_VERIFICATION", "VERIFIED",
}
APPROVAL = {"PENDIENTE", "PARCIAL", "APROBADA"}
PLACEHOLDER = re.compile(r"\{\{[A-Z][A-Z0-9_-]*\}\}")
LINK = re.compile(r"(?<!!)\[[^\]\n]*\]\((<[^>]+>|[^)]+)\)|!\[[^\]\n]*\]\((<[^>]+>|[^)]+)\)")
HEADING = re.compile(r"^ {0,3}(#{1,6})[ \t]+(.+?)[ \t]*#*[ \t]*$", re.M)
LEGACY = {
    "spec.md": (
        "Alcance y contrato vigente", "Trazabilidad y escenarios", "Evidencia",
        "Niveles de autoridad", "Escenarios locales", "Decisiones bloqueantes",
    ),
    "plan.md": ("Secuencia de implementación condicionada",),
    "task.md": (
        "Tareas ejecutables sin cambiar contrato", "Tareas bloqueadas hasta decisión",
        "Guía de ejecución con OpenCode", "Control de ejecución",
    ),
}
CANONICAL = {
    "spec.md": (
        "Identificación y autoridad", "Estado de aprobación", "Responsabilidad única",
        "Fuentes consultadas", "Contrato aprobado", "Comportamiento implementado candidato",
        "Funcionalidad requerida faltante", "Contrato de entrada/salida",
        "Seguridad y validaciones", "Escenarios Given/When/Then",
        "Trazabilidad RF/RNF/AC/T-AUTH/TASK", "Matriz de brechas",
        "Evidencia con nivel y fecha", "DRIFT y decisiones pendientes",
        "Criterios de preparación para desarrollo",
    ),
    "plan.md": (
        "Autoridad y alcance", "Arquitectura existente", "Componentes involucrados",
        "Secuencia técnica", "Componentes reutilizables del Bloque 1",
        "Archivos que se crearán o modificarán cuando corresponda",
        "Orden de implementación", "Dependencias y bloqueos", "Estrategia de pruebas",
        "Seguridad, errores y observabilidad", "Criterios de cierre técnico",
    ),
    "task.md": (
        "Autoridad y alcance", "Tareas ejecutables sin cambiar contrato",
        "Tareas bloqueadas hasta decisión y gates SDD", "Guía de ejecución con OpenCode",
        "Control de ejecución",
    ),
}


def slug(text: str) -> str:
    """Aproxima github-slugger: conserva acentos/guiones bajos, descarta puntuación."""
    text = re.sub(r"\[([^]]+)\]\([^)]*\)", r"\1", text)
    text = re.sub(r"<[^>]+>|[`*_~]", "", text).strip().lower()
    text = "".join(ch for ch in text if ch in " -_" or unicodedata.category(ch)[0] in "LN")
    return text.replace(" ", "-")


def anchors(text: str) -> set[str]:
    seen: Counter[str] = Counter()
    result = set()
    for _, title in HEADING.findall(text):
        name = slug(title)
        suffix = seen[name]
        seen[name] += 1
        result.add(f"{name}-{suffix}" if suffix else name)
    return result


def cells(line: str) -> list[str]:
    """Divide filas GFM sin separar pipes escapadas ni pipes dentro de código."""
    if not line.startswith("|") or not line.endswith("|"):
        return []
    parts, value, ticks = [], "", 0
    for i, ch in enumerate(line[1:-1]):
        if ch == "`" and (i == 0 or line[i] != "\\"):
            ticks = 0 if ticks else 1
        if ch == "|" and not ticks and (i == 0 or line[i] != "\\"):
            parts.append(value.strip())
            value = ""
        else:
            value += ch
    parts.append(value.strip())
    return parts


def references(text: str, prefix: str) -> set[str]:
    """Expande ID-01, ID-01/03..05 e ID-01..05; evita subcadenas ambiguas."""
    found = set()
    pattern = re.compile(rf"(?<![\w-]){re.escape(prefix)}-(\d{{2,3}})((?:/\d{{2,3}}|\.\.\d{{2,3}})*)")
    for match in pattern.finditer(text):
        size = len(match.group(1))
        first = int(match.group(1))
        found.add(f"{prefix}-{first:0{size}d}")
        tail = match.group(2)
        while tail:
            piece = re.match(r"(\.\.|/)(\d{2,3})", tail)
            assert piece is not None
            next_number = int(piece.group(2))
            if piece.group(1) == "..":
                found.update(f"{prefix}-{n:0{size}d}" for n in range(first, next_number + 1))
            else:
                found.add(f"{prefix}-{next_number:0{size}d}")
            first = next_number
            tail = tail[len(piece.group(0)):]
    return found


def scenario_definitions(text: str) -> list[str]:
    """Solo filas de la sección Escenarios: celda ID sola o `ID · descripción`."""
    section = re.search(r"(?ms)^## Escenarios [^\n]*\n(.*?)(?=^## |\Z)", text)
    if section is None:
        return []
    result = []
    for line in section.group(1).splitlines():
        row = cells(line)
        if not row:
            continue
        match = re.fullmatch(r"([A-Z]{2,8}-\d{2,3})(?:\s*·\s*.+)?", row[0])
        if match:
            result.append(match.group(1))
    return result


def check(folder: Path, template: bool = False) -> list[str]:
    issues: list[str] = []

    def error(message: str) -> None:
        issues.append(f"{folder}: {message}")

    docs = {}
    for name in FILES:
        file = folder / name
        if not file.is_file():
            error(f"falta {name}")
            continue
        try:
            docs[name] = file.read_text(encoding="utf-8-sig")
        except UnicodeError as exc:
            error(f"{name}: UTF-8 inválido: {exc}")
    if len(docs) != 3:
        return issues

    approvals = set()
    for name, text in docs.items():
        headings = [title for _, title in HEADING.findall(text)]
        duplicates = [h for h, count in Counter(headings).items() if count > 1]
        if duplicates:
            error(f"{name}: secciones duplicadas: {duplicates}")
        required = LEGACY[name] if not template and folder.resolve() in LEGACY_FOLDERS else CANONICAL[name]
        for section in required:
            if not any(h.startswith(section) for h in headings):
                error(f"{name}: falta sección {section}")
        if not template and PLACEHOLDER.search(text):
            error(f"{name}: marcador de plantilla sin sustituir")
        matches = re.findall(r"^Estado de aprobación de la ficha:\s*(\S+)", text, re.M)
        if len(matches) != 1 or matches[0] not in APPROVAL:
            error(f"{name}: estado de aprobación ausente/duplicado/inválido")
        else:
            approvals.add(matches[0])

        # Tablas: encabezado, separador y datos con idéntico número de celdas.
        lines = text.splitlines()
        table_rows: list[str] = []
        for index, line in enumerate(lines):
            if not re.fullmatch(r"\|[\s|:\-]+\|", line):
                continue
            count = len(cells(line))
            if index == 0 or len(cells(lines[index - 1])) != count:
                error(f"{name}:{index + 1}: separador de tabla sin encabezado compatible")
            pos = index + 1
            while pos < len(lines) and lines[pos].startswith("|"):
                if len(cells(lines[pos])) != count:
                    error(f"{name}:{pos + 1}: fila de tabla con columnas distintas ({count} esperadas)")
                if lines[pos] in table_rows:
                    error(f"{name}:{pos + 1}: fila de tabla duplicada")
                table_rows.append(lines[pos])
                pos += 1

        for raw in LINK.findall(text):
            url = raw[0] or raw[1]
            if url.startswith("<"):
                url = url[1:-1]
            url = url.split(None, 1)[0]  # título Markdown opcional
            if urlsplit(url).scheme or url.startswith(("//", "mailto:")):
                continue
            path, _, fragment = url.partition("#")
            target = (folder / unquote(path)).resolve() if path else folder / name
            if not target.is_file():
                error(f"{name}: enlace roto {url}")
                continue
            if fragment:
                try:
                    source = target.read_text(encoding="utf-8-sig")
                except UnicodeError:
                    error(f"{name}: destino UTF-8 inválido {url}")
                    continue
                if unquote(fragment) not in anchors(source):
                    error(f"{name}: anclaje inexistente {url}")

    if len(approvals) > 1:
        error(f"estado de aprobación contradictorio entre documentos: {sorted(approvals)}")
    if template:
        return issues

    spec, plan, task = (docs[name] for name in FILES)
    operating = re.findall(r"^Estado operativo:\s*([A-Z_]+)\b", spec, re.M)
    if len(operating) != 1 or operating[0] not in OPERATIVE:
        error("spec.md: estado operativo ausente/duplicado/inválido")

    scenario_ids = scenario_definitions(spec)
    if not scenario_ids:
        error("spec.md: sin escenarios locales en tabla")
    for key, count in Counter(scenario_ids).items():
        if count > 1:
            error(f"spec.md: escenario duplicado {key}")

    detailed = []
    control = []
    for line in task.splitlines():
        cols = cells(line)
        if not cols:
            continue
        if re.match(r"^DEV-[A-Z0-9]+-\d{2,3}\s*·", cols[0]):
            detailed.append((re.match(r"DEV-[A-Z0-9]+-\d{2,3}", cols[0]).group(), cols))
        elif re.fullmatch(r"DEV-[A-Z0-9]+-\d{2,3}", cols[0]):
            control.append((cols[0], cols))
    detailed_ids = [item[0] for item in detailed]
    details_by_id = dict(detailed)
    controls = [item[0] for item in control]
    for label, ids in (("tarea detallada", detailed_ids), ("control", controls)):
        for key, count in Counter(ids).items():
            if count > 1:
                error(f"task.md: {label} duplicado {key}")
    if set(detailed_ids) != set(controls) or not detailed_ids:
        error("task.md: tareas detalladas y control no coinciden uno a uno")
    if len({key.split("-")[1] for key in detailed_ids}) > 1:
        error("task.md: IDs DEV de distintas APIs")

    for key, row in detailed:
        if len(row) != 4 or not row[1].strip() or not row[2].strip() or "**Hecho**" not in row[2] and "evidencia" not in row[2].lower():
            error(f"task.md: {key} sin objetivo/acción/criterio verificable")
    edges: dict[str, set[str]] = {}
    for key, row in control:
        if len(row) != 7:
            error(f"task.md: control {key} requiere 7 columnas")
            continue
        _, kind, state, technical, auth, dependencies, proof = row
        if state not in STATES:
            error(f"task.md: {key} estado inválido {state}")
        if key in details_by_id:
            detail_state = details_by_id[key][-1].lower()
            expected = {"READY": ("lista", "ready"), "BLOCKED": ("bloqueada", "blocked"),
                        "DONE": ("hecho", "done"), "NOT_APPLICABLE": ("no aplica", "not_applicable")}
            if state in expected and not any(value in detail_state for value in expected[state]):
                error(f"task.md: {key} estado contradictorio entre detalle y control")
        if technical not in {"SI", "NO"}:
            error(f"task.md: {key} Cambio técnico debe ser SI/NO")
        if state == "READY" and technical == "SI" and (not re.match(r"Aprobada:\s*\[[^]]+\]\([^)]+\)", auth) or "pendiente" in auth.lower()):
            error(f"task.md: {key} READY técnico sin autorización aprobada identificable")
        if state == "DONE" and (not re.search(r"\d{4}-\d\d-\d\d", proof)
                                or not re.search(r"\[[^]]+\]\([^)]+\)", proof)
                                or not re.search(r"resultado|artefacto", proof, re.I)):
            error(f"task.md: {key} DONE sin evidencia fechada")
        if state == "NOT_APPLICABLE" and (not proof or proof.upper() in {"PENDIENTE", "N/A", "-"}):
            error(f"task.md: {key} NOT_APPLICABLE sin justificación")
        if state == "BLOCKED" and (not auth or auth.upper() in {"N/A", "-"}):
            error(f"task.md: {key} BLOCKED sin decisión/gate concreto")
        if state == "READY" and technical == "NO" and kind in {"IMPLEMENTACION", "CORRECCION", "QA_TECNICA", "INTEGRACION"}:
            error(f"task.md: {key} tarea técnica READY marcada como no técnica")
        deps = set(re.findall(r"DEV-[A-Z0-9]+-\d{2,3}", dependencies))
        edges[key] = deps
        if dependencies != "-" and not deps:
            error(f"task.md: {key} dependencia sin ID DEV explícito")
        for dep in deps - set(detailed_ids):
            error(f"task.md: {key} dependencia inexistente {dep}")
        if key in deps:
            error(f"task.md: {key} dependencia circular propia")
    for key in edges:
        seen, pending = set(), list(edges[key])
        while pending:
            dep = pending.pop()
            if dep == key:
                error(f"task.md: ciclo de dependencias en {key}")
                break
            if dep not in seen:
                seen.add(dep)
                pending.extend(edges.get(dep, ()))

    if operating and operating[0] in {"READY_PARTIAL", "READY_FOR_IMPLEMENTATION"}:
        if not any(len(row) == 7 and row[2] == "READY" and row[3] == "SI" for _, row in control):
            error("spec.md: ficha READY sin ninguna tarea técnica READY autorizada")
        if operating[0] == "READY_FOR_IMPLEMENTATION" and any(len(row) == 7 and row[2] == "BLOCKED" and row[3] == "SI" for _, row in control):
            error("spec.md: READY_FOR_IMPLEMENTATION con tarea técnica bloqueada")

    prefixes = {item.split("-")[0] for item in scenario_ids}
    for name, text in docs.items():
        for prefix in prefixes:
            for reference in references(text, prefix) - set(scenario_ids):
                error(f"{name}: referencia a escenario inexistente {reference}")
        for prefix in {"DEV-" + key.split("-")[1] for key in detailed_ids}:
            for reference in references(text, prefix) - set(detailed_ids):
                error(f"{name}: referencia a tarea inexistente {reference}")
    return issues


def self_test() -> int:
    """Ficha canónica realista y mutaciones independientes fuera del repositorio."""
    historical = check(BLOCK / "api-001-register") + check(BLOCK / "api-002-login")
    legacy = (BLOCK / "api-001-register" / "spec.md").read_text(encoding="utf-8")
    if historical or "REG-01" not in scenario_definitions(legacy):
        print("FALLO: línea base heredada no válida")
        return 1
    print("OK: API-001/002 y escenarios históricos (línea base sin errores)")

    with tempfile.TemporaryDirectory(prefix="sdd-atomic-") as temporary:
        root = Path(temporary).resolve()
        if root.is_relative_to(ROOT.resolve()):
            print("FALLO: temporales dentro del repositorio")
            return 1
        sample = root / "specs" / "bloque-2-funcionalidades" / "api-099-ficticia"
        sample.mkdir(parents=True)
        (root / "docs").mkdir()
        for name in ("constitution.md", "sdd-guia-api-atomica.md"):
            shutil.copyfile(ROOT / "docs" / name, root / "docs" / name)
        for name in ("base-modular", "contratos-transversales", "persistencia-mongodb",
                     "redis-rate-limit-revocacion"):
            target = root / "specs" / "bloque-1-arquitectura" / name
            target.mkdir(parents=True)
            shutil.copyfile(ROOT / "specs" / "bloque-1-arquitectura" / name / "spec.md", target / "spec.md")

        replacements = {
            "{{API_ID}}": "API-099", "{{NOMBRE}}": "Ficticia",
            "{{ESC-01}}": "SCN-01", "{{DEV-ID-01}}": "DEV-API099-01",
            "{{DEV-ID-02}}": "DEV-API099-02",
            "{{RF_ESC_ARCHIVOS}}": "RF-TEST; SCN-01; spec.md",
            "{{RF_RNF_AC_T_AUTH_TASK}}": "RF-TEST",
            "{{DEV_ID_O_EXTERNA}}": "DEV-API099-01",
            "{{ORDEN_DEV_CON_DEPENDENCIAS}}": "DEV-API099-01 antes de DEV-API099-02",
            "{{DEP_ACCION_RESULTADO_COMANDO_EVIDENCIA}}":
                "Sin dependencias; leer spec.md; entregar informe; comando: python --version; **Hecho**: informe fechado.",
            "{{DECISION_EXACTA_DEP_ACCION_RESULTADO_COMANDO_EVIDENCIA}}":
                "Tras DEV-API099-01 y DEC-099; verificar diseño sin programar; comando: python --version; **Hecho**: acta identificable.",
            "{{DECISION_ORIGINAL_Y_GATE}}": "DEC-099",
            "{{TIPO}}": "DOCUMENTAL",
        }
        for name in FILES:
            text = (TEMPLATE / name).read_text(encoding="utf-8")
            text = PLACEHOLDER.sub(
                lambda match: replacements.get(match.group(), "Dato ficticio " + match.group()[2:-2].lower()), text)
            if name == "task.md":
                text = text.replace("| DEV-API099-02 | DOCUMENTAL |", "| DEV-API099-02 | QA_TECNICA |", 1)
            (sample / name).write_text(text, encoding="utf-8")

        original = {name: (sample / name).read_text(encoding="utf-8") for name in FILES}
        baseline = check(sample)
        if baseline:
            print("FALLO: ficha canónica base debe tener 0 errores:")
            for issue in baseline:
                print("  ", issue)
            return 1
        print("OK: ficha API-099 ficticia canónica (0 errores, READY documental, BLOCKED técnico)")

        # Variante histórica del campo ID, con la misma ficha canónica restante.
        first = "| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado, etiquetar candidato si no aprobado) | RF/AC | Prueba/pendiente | Autorización |"
        scenario_line = next(line for line in original["spec.md"].splitlines() if line.startswith("| SCN-01 |"))
        historical_line = "| SCN-01 · Dado usuario válido / cuando consulta / entonces responde según contrato candidato | RF-TEST; prueba pendiente | Bloqueado por DEC-099 |"
        historical_spec = original["spec.md"].replace(
            first + "\n|---|---|---|---|---|---|---|\n" + scenario_line,
            "| ID · Dado / Cuando / Entonces | Requisito / prueba | Brecha |\n|---|---|---|\n" + historical_line, 1)
        (sample / "spec.md").write_text(historical_spec, encoding="utf-8")
        if check(sample) or scenario_definitions(historical_spec) != ["SCN-01"]:
            print("FALLO: variante histórica de escenario")
            return 1
        print("OK: formato ID · descripción (0 errores)")
        (sample / "spec.md").write_text(original["spec.md"], encoding="utf-8")
        prose = original["spec.md"] + "\nReferencia SCN-01 en texto: no es otra definición.\n"
        (sample / "spec.md").write_text(prose, encoding="utf-8")
        if check(sample) or scenario_definitions(prose) != ["SCN-01"]:
            print("FALLO: referencia en prosa confundida con definición")
            return 1
        print("OK: referencia en prosa no duplica escenarios")
        (sample / "spec.md").write_text(original["spec.md"], encoding="utf-8")

        detailed = next(line for line in original["task.md"].splitlines() if line.startswith("| DEV-API099-01 ·"))
        cases = (
            ("escenario duplicado (celda)", "spec.md", original["spec.md"].replace(scenario_line, scenario_line + "\n" + scenario_line, 1), "escenario duplicado SCN-01"),
            ("escenario duplicado (descripción)", "spec.md", historical_spec.replace(historical_line, historical_line + "\n" + historical_line, 1), "escenario duplicado SCN-01"),
            ("referencia inexistente (descripción)", "spec.md", historical_spec + "\nSCN-99\n", "referencia a escenario inexistente SCN-99"),
            ("tarea DEV duplicada", "task.md", original["task.md"].replace(detailed, detailed + "\n" + detailed, 1), "tarea detallada duplicado DEV-API099-01"),
            ("escenario inexistente", "plan.md", original["plan.md"] + "\nSCN-99\n", "referencia a escenario inexistente SCN-99"),
            ("tarea inexistente", "plan.md", original["plan.md"] + "\nDEV-API099-99\n", "referencia a tarea inexistente DEV-API099-99"),
            ("dependencia circular", "task.md", original["task.md"].replace(
                "| DEV-API099-01 | DOCUMENTAL | READY | NO | N/A: lectura | - |",
                "| DEV-API099-01 | DOCUMENTAL | READY | NO | N/A: lectura | DEV-API099-02 |", 1),
             "ciclo de dependencias"),
            ("READY técnico sin autorización", "task.md", original["task.md"].replace(
                "| DEV-API099-02 | QA_TECNICA | BLOCKED | SI |",
                "| DEV-API099-02 | QA_TECNICA | READY | SI |", 1).replace(
                "| BLOCKED |\n", "| READY |\n", 1), "READY técnico sin autorización aprobada"),
            ("sección canónica ausente", "plan.md", original["plan.md"].replace(
                "## Estrategia de pruebas", "## Sección omitida", 1), "falta sección Estrategia de pruebas"),
            ("enlace roto", "spec.md", original["spec.md"].replace("(plan.md)", "(no-existe.md)", 1), "enlace roto no-existe.md"),
            ("anclaje roto", "spec.md", original["spec.md"].replace("(plan.md)", "(plan.md#no-existe)", 1), "anclaje inexistente"),
            ("aprobación contradictoria", "plan.md", original["plan.md"].replace(
                "Estado de aprobación de la ficha: PENDIENTE", "Estado de aprobación de la ficha: APROBADA", 1),
             "estado de aprobación contradictorio"),
        )
        failures = 0
        for label, name, mutation, expected in cases:
            if mutation == original[name]:
                print(f"FALLO: mutación ineficaz: {label}")
                failures += 1
                continue
            (sample / name).write_text(mutation, encoding="utf-8")
            detected = any(expected in problem for problem in check(sample))
            print(f"{'OK' if detected else 'FALLO'}: {label}")
            failures += not detected
            (sample / name).write_text(original[name], encoding="utf-8")
        if check(sample):
            print("FALLO: línea base no restaurada")
            failures += 1
    print(f"{len(cases)} negativos; {failures} fallo(s); temporales eliminados")
    return 1 if failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("ficha", nargs="?", type=Path, help="directorio de una ficha (omitir: todas las del Bloque 2)")
    parser.add_argument("--templates", action="store_true", help="comprobar estructura de plantillas sin exigir sustituir marcadores")
    parser.add_argument("--self-test", action="store_true", help="ficha canónica temporal y mutaciones aisladas fuera del repositorio")
    args = parser.parse_args()
    if (args.templates or args.self_test) and args.ficha or args.templates and args.self_test:
        parser.error("--templates y --self-test no aceptan ficha ni se combinan")
    if args.self_test:
        return self_test()
    if args.templates:
        folders = [TEMPLATE]
    elif args.ficha:
        folders = [(ROOT / args.ficha).resolve()]
    else:
        folders = sorted(p for p in BLOCK.iterdir() if p.is_dir() and any((p / name).exists() for name in FILES))
    if not folders:
        print("ERROR: ninguna ficha encontrada", file=sys.stderr)
        return 1
    issues = [issue for folder in folders for issue in check(folder, template=args.templates)]
    for issue in issues:
        print("ERROR:", issue)
    print(f"{len(folders)} ficha(s); {len(issues)} error(es)")
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
