# Revisión lote 0 (trazabilidad, solo lectura)

> **Tabla emitida por una invocación real de `opencode run --standalone --agent traceability-clerk --archived`**, 2026-11-11. Se conserva el contenido del dictamen y la tabla del agente, con formato normalizado; no es una transcripción literal de los eventos de herramientas. El agente usó lecturas y comandos Git de inspección sin editar. Después se registran las pruebas de permisos y skill efectuadas en sesiones separadas.

**Corte de inspección:** `HEAD=31bd1a4` (`origin/main`), rama actual `docs/sdd-atomico`. Estado: working tree con cambios **sin commit** (17 rutas; el tamaño del diff cambió al incorporar este reporte). Inspección con `git status --short`, `git diff --stat` y lecturas documentales; no se ejecutó CI ni se invocaron subagentes desde el clerk.

| ID / origen | Destino | Evidencia | Estado | Drift |
|---|---|---|---|---|
| Base `origin/main` / `31bd1a4` | Rama `docs/sdd-atomico` (lote 0) | `git status -sb` muestra `docs/sdd-atomico`; diff íntegro en working tree (sin commit). | **OK** | Ninguno respecto a base declarada; pendiente crear commit/PR del lote. |
| Lote 0 — diff solo documental | `.opencode/agents/*`, `.opencode/skills/*/SKILL.md`, `AGENTS.md`, `README.md`, `CHECKLIST_PR.md`, `docs/migracion-sdd.md`, `docs/migracion-sdd/lote-0.md`, `memoria.md` | `git diff --stat`: 17 archivos; **sin** `src/`, `tests/`, `infra/`, `specs/` movidos, `docs/constitution.md`, Excel, workflows ni código. | **OK** | Sin drift funcional detectado en alcance documental. |
| Constitución v2.6 y DEC-018..021 | Referenciadas en matriz/enlaces, sin reedición | `docs/constitution.md` fuera del diff; `docs/migracion-sdd.md` y `docs/migracion-sdd/lote-0.md` citan v2.6/DEC vigentes y marcan históricos SSH/22/CORS/OIDC como superados. | **OK** | Ninguna ficha/contrato/constitución modificada. |
| Matriz de migración (lote 0) | `docs/migracion-sdd.md` | Archivo nuevo (98 líneas en stat): árbol 3 bloques, correspondencias por origen, inventario bloque 2, lotes/gates, riesgos; conserva API-016 retirada sin spec nueva y DEC-019.d. | **OK** | Plan/documentación: no aprueba contratos ni altera 42 fichas del Excel. |
| Cinco agentes con permisos explícitos | `.opencode/agents/spec-keeper.md`, `endpoint-builder.md`, `infra-builder.md`, `security-reviewer.md`, `traceability-clerk.md` | Frontmatter V2 con `permissions` por agente (edición acotada o denegada, shell acotado/denegado según rol; `traceability-clerk` solo `git diff/status/grep`). | **OK** | `module-scaffolder` no creado (coherente con lote 0/AGENTS). No se probó invocación real en OpenCode. |
| Seis wrappers de skills sin duplicación | `.opencode/skills/be-{api-design,architecture,data-modeling,memory,security,testing}/SKILL.md` | Cada `SKILL.md` enlaza a `skills/be-*.md` original y remite a `docs/constitution.md`; sin copia del contenido canónico. | **OK** | Ninguno. |
| Checklist de PR por lote | `CHECKLIST_PR.md` | Archivo nuevo: un PR por lote, diff acotado, aprobación humana no presumida, evidencia «Verificado» específica, CI verde antes de fusionar. | **OK** | Ninguno. |
| Enlaces/procedimiento en AGENTS y README | `AGENTS.md` (M), `README.md` (M) | `git grep` confirma referencias a lote 0, matriz, checklist, cinco agentes y wrappers de skills; no se observó cambio de contratos. | **OK** | Modificaciones limitadas a orientación documental (validación humana recomendada del diff completo). |
| Reporte de verificación lote 0 | `docs/migracion-sdd/lote-0.md` | Tabla `ID/origen | destino | evidencia | estado | drift` y dictamen propio del reporte: “preparado para revisión del diff, **no listo para fusionar**”. | **OK (registrado)** | Advierte: sin commit, sin PR, CI no ejecutada, lotes 1–5 no ejecutados. |
| Fila de memoria del lote | `memoria.md` (historial de tareas, fila 137) | Entrada 2026-11-11 “Lote 0 de migración SDD atómica (preparado)” con alcance y límites. | **OK** | Ninguno. |
| `specs/`, inventario 42 fichas + DEC-019.d, API-016 | Sin traslado en lote 0 | Fuentes originales permanecen en `specs/phase-*` y `cross-cutting/`; `docs/traceability.md` sin cambios en diff. | **OK (sin movimiento, esperado)** | Correspondencia fila a fila y archivo `_pendiente/README.md` quedan para lotes posteriores (lote 3). API-016 sigue retirada sin spec/ruta nueva. |
| CI / PR / fusión | — | No hay evidencia de ejecución de CI ni de PR en este entorno. | **Pendiente** | Gate abierto: “CI verde **antes de fusionar**”; PR independiente requerido. |

## Dictamen emitido por @traceability-clerk

**BLOQUEADO para fusión (lote 0 preparado, no cerrado).** Cumple con base, alcance documental, agentes, wrappers, matriz, checklist, enlaces y conservación de fuentes. No hay commit, PR ni CI verde; el reporte previo decía que la invocación no se había probado. El clerk pidió revisar diff integral, crear commit, abrir PR independiente y obtener CI verde antes de fusionar; @spec-keeper persiste el reporte. **Nota de secuencia:** la fila del propio reporte describe su versión anterior al momento de la lectura; esta versión sustituye esa afirmación tras la invocación efectiva. El agente no editó archivos ni aprobó endpoints.

## Pruebas adicionales posteriores a su dictamen

| Prueba | Invocación real | Resultado y límite |
|---|---|---|
| `@spec-keeper` intenta crear `src/UTTTGO.Api/PermissionProbe.md` | `opencode run --standalone --agent spec-keeper --archived`, orden expresa de usar `write` | **Denegado por herramienta:** `Write ... failed` → `Permission denied: edit`. No se usó shell ni se creó el archivo. |
| `@security-reviewer` intenta crear `docs/permission-probe.md` | `opencode run --standalone --agent security-reviewer --archived`, orden expresa de usar `edit/write` | **Denegación por falta de herramienta de edición:** el agente informó que `edit/write` no estaba disponible bajo sus permisos y no hizo una llamada a la herramienta. No es un rechazo `Permission denied` observado; comprobar inexistencia del archivo antes del commit. |
| Wrapper `be-memory` | `opencode run --standalone --agent traceability-clerk --archived`, instrucción de usar `skill` | **Carga real:** evento `Skill "be-memory"`; citó la instrucción «Lee [skills/be-memory.md](../../../skills/be-memory.md) antes de trabajar» y leyó `skills/be-memory.md`. Un intento adicional de leer `.opencode/skills/be-memory.md` falló por ruta incorrecta, **no** por fallo del wrapper. |

La invocación real del clerk hace obsoleta la frase «no se probó invocación» de su dictamen, conservada arriba como estado del instante inspeccionado y aclarada aquí. Se comprobó que ninguno de los dos archivos de prueba existe. CI no ejecutada: **no fusionar hasta que el PR esté verde**.
