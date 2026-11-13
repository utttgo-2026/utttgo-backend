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

## Conciliación posterior a la invocación, antes del push

El reporte del clerk anterior refleja el corte `31bd1a4`; **no verificó** los tres archivos incorporados después a `origin/main` por el PR #7 (`51dcfb5`). Se revisó ese delta y se actualizó la rama local sobre el nuevo `origin/main` sin tocar `feature/siete-endpoints-locales`. `specs/phase-04-orders-cash/{spec,plan,task}.md` son un **borrador v0.1 pendiente de aprobación** de API-023..029; D-ORD-01..10 y el gate de catálogo Sprint 2 siguen abiertos. Se corrigió `docs/migracion-sdd.md`: 25 archivos originales en lugar de 22, trío Orders como origen histórico, propiedad de API-023 en Orders y clasificación de esas siete fichas como borrador, no como «sin spec» ni «Verificado». `docs/traceability.md` de `origin/main` aún titula API-023..038 «sin spec»: debe conciliarse en su lote, sin declarar aprobados los siete endpoints. Estos ajustes **no son salida del clerk**; requieren revisión de trazabilidad posterior antes del cierre/merge. No se modificó ninguna ficha de pedidos.

**Hallazgos del borrador de Orders:** ver mensaje para el propietario de Orders en la conversación previa (sin constancia de envío); trío no modificado en lote 0.

## Salida de @traceability-clerk sobre el delta posterior al rebase (2026-11-12)

> Segunda invocación real: `opencode run --standalone --agent traceability-clerk --archived --model openai/gpt-6-sol-fast`, frente a `origin/main` `51dcfb5`. Un intento previo con el modelo predeterminado no ejecutó la revisión: `Free usage limit reached`. A continuación se conserva **la respuesta del agente**, separada de la conciliación manual de arriba; no editó archivos.

Revisé el delta frente a `51dcfb5`: cambian la matriz, `lote-0.md` y `memoria.md`; el trío de Orders y `docs/traceability.md` no cambian.

| ID/origen | destino | evidencia | estado | drift |
|---|---|---|---|---|
| RF-ORD-01..32 / `phase-04-orders-cash/spec.md` | API-023..029 y referencias compartidas, **previstas** para lote 3b | Matriz §«Correspondencia»; RF en spec §§3–4 | Borrador; sin prueba de endpoints | Falta asignación individual a fichas, propia de 3b |
| T-ORD-01..20 / `spec.md` §7 | Pruebas de las fichas, previstas para 3b | Casos Given/When/Then en spec; `task.md` TASK-ORD-030..032 propone pruebas | No ejecutadas | No confundir pruebas propuestas con evidencia |
| TASK-ORD-001..004, 010..013, 020..026, 030..032, 040..042 / `task.md` | Tareas de Orders y fichas previstas para 3b | Todos figuran `[ ]`; la matriz incluye **040..042**, también 041 | Pendientes; 004 bloqueada por Catalog Sprint 2 | Falta reparto individual, no ID perdido |
| D-ORD-01..10 / `spec.md` §9.2 | Decisiones a resolver antes de aprobar las fichas | Tabla §9.2 y gate de `plan.md` §7 | Pendientes; **09 bloqueada por DEC-003** | Ninguna puede darse por aprobada al dividir |

**IDs huérfanos en el origen:** ninguno de esos cuatro conjuntos; todos tienen referencia histórica y destino planificado, no destino atómico materializado todavía. No encontré «Verificado» atribuido a Orders sin curl o test concreto: el lote 0 niega expresamente esa verificación. Los tests con dobles descritos en el plan son futuros, no integración externa acreditada.

**Drift documental pendiente:** `docs/traceability.md:51` aún titula API-023..038 «sin spec», y `memoria.md:85` aún dice que Orders está «pendiente de spec/plan», pese al trío borrador existente. La conciliación de `lote-0.md:38` reconoce el primer caso, pero no actualiza esas referencias.

**Dictamen: BLOQUEADO para cerrar/fusionar el lote 0.** La incorporación de Orders está trazada sin aprobarla ni modificar sus fuentes; quedan la conciliación documental indicada y el gate de PR/CI, que `lote-0.md` declara no ejecutado. Esta revisión no firma aprobación final. Guías: [diseño API](../../skills/be-api-design.md) y [memoria](../../skills/be-memory.md).

**Acción posterior al reporte (no atribuible al clerk):** se corrige `memoria.md` §Mapa de colecciones para indicar el borrador existente; el título de `docs/traceability.md` queda pendiente de conciliación en lote 3 y no se declara cierre ni «Verificado». La salida del clerk conserva el estado previo a esa corrección.

**Cambio de plan posterior (2026-11-12, no atribuible al clerk):** por instrucción del propietario, el trío de Orders se archivará íntegro en lote 3 como **borrador no migrado**, con la cobertura RF-ORD/T-ORD/TASK-ORD/D-ORD de la tabla anterior y API-023..029 en el índice. La atomización del lote 3b queda **diferida hasta la aprobación e implementación de Orders**, coordinada con su autor. El trío sigue en su ruta original; archivar no equivale a aprobar ni verificar. La revisión del clerk arriba corresponde al plan previo y no valida este cambio posterior.
