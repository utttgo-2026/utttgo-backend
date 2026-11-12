# Base modular — plan de conciliación

Autoridad: [constitución v2.6](../../../docs/constitution.md) → [spec](spec.md) → este plan → [task](task.md). Migración documental, no plan de implementación aprobado. Fuente intacta tras el cotejo: `../../_archivo/phase-00-constitution/00-01-principles-stack/{spec,plan,task}.md`.

1. Conciliar estructura real `.slnx` y módulos con § 4, sin repetir el esqueleto histórico `.sln` ni declarar completadas sus tareas `[ ]`.
2. Desglosar por dueño: [persistencia](../persistencia-mongodb/spec.md), [Redis](../redis-rate-limit-revocacion/spec.md), [contratos](../contratos-transversales/spec.md); fase 02/03 y bloque 3 siguen en lotes posteriores.
3. Revisar pruebas y aprobaciones individualmente; no confundir QA con dobles, `/health` y un build con seguridad completa o contrato de negocio aprobado.

## Correspondencia exhaustiva de fase 00

**Origen:** `specs/phase-00-constitution/00-01-principles-stack/spec.md` para REQ/T/AC; `task.md` para TASK-BE. Cada destino es dueño de la **traza**, no aprobación ni ejecución. `B` = `specs/bloque-1-arquitectura/base-modular/`, `M` = `specs/bloque-1-arquitectura/persistencia-mongodb/`, `R` = `specs/bloque-1-arquitectura/redis-rate-limit-revocacion/`, `C` = `specs/bloque-1-arquitectura/contratos-transversales/`; las rutas de bloque 3 y bloque 2 son **destinos previstos, NO creados en lote 1**. Cada fila especifica archivo destino, sin rangos implícitos.

| ID | Destino explícito / estado de conciliación |
|---|---|
| REQ-001 | `B/spec.md` · arquitectura/runtime; cotejo de código pendiente. |
| REQ-002 | `C/spec.md` · prefijo/contratos; SignalR/backplane queda en `B/spec.md` como dependencia futura. |
| REQ-003 | `specs/bloque-3-infraestructura/docker-nginx/spec.md` · futuro; DEC-018/020/021 vigentes. |
| REQ-004 | `specs/bloque-3-infraestructura/aws-terraform/spec.md` · futuro. |
| REQ-005 | `M/spec.md` · driver, índices y fronteras. |
| REQ-006 | `M/spec.md` · rol del perfil; autenticación funcional → futuro `specs/bloque-1-arquitectura/identidad-compartida/spec.md` (lote 2a). |
| REQ-007 | `R/spec.md` · revocación; logout API-003 sigue pendiente (índice lote 3). |
| REQ-008 | `R/spec.md` · tres usos y degradación. |
| REQ-009 | `specs/_pendiente/README.md` (lote 3, API-017/018; índice, no ficha); fuente `docs/constitution.md` § 7; ficha Media individual y ruta por aprobar. |
| REQ-010 | `specs/_pendiente/README.md` (lote 3, API-035/036); fuente vigente `docs/constitution.md` § 8. |
| REQ-011 | `specs/_pendiente/README.md` (lote 3, API-023..029); Orders fase 04 borrador, no aprobado. |
| REQ-012 | `specs/_pendiente/README.md` (lote 3, API-023..029); § 3 BR-003. |
| REQ-013 | `specs/_pendiente/README.md` (lote 3, API-011..015); § 3 BR-004/005/006. |
| REQ-014 | `specs/_pendiente/README.md` (lote 3, API-011..015/023..029); § 3 BR-009/010. |
| REQ-015 | `specs/bloque-3-infraestructura/seguridad-operativa/spec.md` · futuro; secretos y logs. |
| REQ-016 | `B/spec.md` · gate SDD. |
| REQ-017 | `specs/bloque-2-funcionalidades/api-009-seller-activation/spec.md` · futuro lote 2b; DEC-002. |
| REQ-018 | `specs/_pendiente/README.md` (lote 3, API-040); DEC-003 datos compartidos aún sin diseño. |
| REQ-019 | `specs/_pendiente/README.md` (lote 3, API-037/038); DEC-005 mecanismo de purga pendiente. |
| REQ-020 | `specs/bloque-1-arquitectura/identidad-compartida/spec.md` · futuro lote 2a, decisión de canal pendiente; ninguna ruta nueva. |
| T-001 | `B/task.md` · build/formato, evidencia histórica QA, repetir en CI. |
| T-002 | `B/task.md` · revisión estática pendiente; `.slnx` es la solución REAL, no tecnología prohibida (error histórico). |
| T-003 | `B/task.md` · negocio Orders/Catalog; pruebas pendientes, sin inferir por base. |
| T-004 | `R/task.md` · JWT/blacklist con límite de dobles; propiedad/roles → `M/task.md`. |
| T-005 | `R/task.md` · degradación Redis; caída real pendiente. |
| T-006 | `specs/_pendiente/README.md` (lote 3, API-017/018); Media fase 05, no probado aquí. |
| T-007 | `specs/_pendiente/README.md` (lote 3, API-035/036); FCM pendiente. |
| T-008 | `specs/bloque-3-infraestructura/aws-terraform/task.md` · futuro lote 4; fmt/validate no bastan para ausencia de secretos. |
| T-009 | `B/task.md` · conciliación documental ID/origen/destino, requiere clerk. |
| T-010 | `specs/bloque-3-infraestructura/seguridad-operativa/task.md` · futuro; escaneo sin ejecutar en lote 1. |
| T-011 | `specs/bloque-2-funcionalidades/api-009-seller-activation/task.md` · futuro; tests con dobles y gate EC2 pendiente. |
| AC-001 | `B/spec.md` · revisión integral stack pendiente. |
| AC-002 | `B/spec.md` · build/formato histórico, repetir en PR. |
| AC-003 | `B/spec.md` · BR-001..013: comprobación funcional por lotes 2/3 pendiente. |
| AC-004 | `B/spec.md` · revisión ausencia artefactos prohibidos pendiente. |
| AC-005 | `R/spec.md` · logout API-003 aún no implementado. |
| AC-006 | `R/spec.md` · 429/documentación de gate; prueba distribuida pendiente. |
| AC-007 | `specs/_pendiente/README.md` (lote 3, API-017/018), § 7; pendiente Media. |
| AC-008 | `specs/_pendiente/README.md` (lote 3, API-035/036), § 8; pendiente FCM. |
| AC-009 | `specs/bloque-3-infraestructura/aws-terraform/spec.md` · futuro; drift/manualidad no inferidos. |
| AC-010 | `specs/bloque-3-infraestructura/seguridad-operativa/spec.md` · futuro; escaneo pendiente. |
| AC-011 | `B/spec.md` · matriz completa, pruebas T no todas ejecutadas. |
| AC-012 | `B/spec.md` · aprobación integral humana pendiente; v1.1 histórica solamente. |
| AC-013 | `specs/bloque-2-funcionalidades/api-009-seller-activation/spec.md` · futuro; EC2 pendiente. |
| TASK-BE-001 | `B/task.md` · repo histórico, no reabrir como creación. |
| TASK-BE-002 | `B/task.md` · .gitignore histórico; revisar, no cerrar. |
| TASK-BE-003 | `B/task.md` · protección de ramas requiere evidencia humana. |
| TASK-BE-004 | `B/task.md` · README histórico, revisión pendiente. |
| TASK-BE-005 | `B/task.md` · .env.example histórico, sin valores reales. |
| TASK-BE-006 | `B/task.md` · solución real `.slnx`, no crear `.sln`. |
| TASK-BE-007 | `B/task.md` · suites existentes; no afirmar 0 tests. |
| TASK-BE-008 | `B/task.md` · estructura modular real, inspección pendiente. |
| TASK-BE-009 | `B/task.md` · dependencias reales/restore a comprobar. |
| TASK-BE-010 | `M/task.md` · opciones tipadas; Redis/Supabase/Storage por contrato dueño, revisión pendiente. |
| TASK-BE-011 | `M/task.md` · DI Mongo, comprobar contra /health; no solo ping. |
| TASK-BE-012 | `R/task.md` · DI Redis, comprobar sin asumir integración real. |
| TASK-BE-013 | `R/task.md` · validación JWT y blacklist, prueba externa pendiente. |
| TASK-BE-014 | `C/task.md` · CORS API con orígenes explícitos, no confundir con CORS S3. |
| TASK-BE-015 | `R/task.md` · rate limit distribuido, prueba de caída pendiente. |
| TASK-BE-016 | `C/task.md` · Serilog/correlationId; DEC-021 logs EC2 aparte. |
| TASK-BE-017 | `C/task.md` · correlación middleware, decisión de header histórica no ratificada. |
| TASK-BE-018 | `C/task.md` · error global sanitizado, 500 histórico a cotejar. |
| TASK-BE-019 | `C/task.md` · orden de middleware: aprobación AGENT-BE-01 requerida. |
| TASK-BE-020 | `specs/bloque-3-infraestructura/observabilidad-health/task.md` · futuro lote 4; HP-11 prueba acceso Mongo, no basta ping. |
| TASK-BE-021 | `specs/bloque-3-infraestructura/docker-nginx/task.md` · futuro lote 4; Compose actual de 2 réplicas y Mongo solo perfil local. |
| TASK-BE-022 | `specs/bloque-3-infraestructura/docker-nginx/task.md` · futuro; override histórico de credenciales no se presupone, `docker-compose.prod.yml` es override logs EC2. |
| TASK-BE-023 | `B/task.md` · checklist transversal y aprobación aún abiertos. |
| TASK-BE-024 | `specs/bloque-3-infraestructura/seguridad-operativa/task.md` · futuro; no ejecutar comando histórico de borrado de historia. |

**Gaps explícitos:** los destinos futuros del bloque 2/3 y `_pendiente/README.md` no existen en este lote; REQ-009 no se transforma en una ruta ni una ficha por conjetura. Los identificadores TASK-BE-001..024 permanecen históricos `[ ]`, incluso si existen artefactos, hasta cotejo/evidencia individual. AC-012 requiere aprobación humana. T-002 menciona erróneamente `.slnx` como prohibición: prevalece la solución existente, según `AGENTS.md` § 1.2. Plan de fase 00 P0..P6 no contiene IDs adicionales y permanece íntegro archivado; sus pasos futuros de infraestructura/health se derivan a bloque 3.
