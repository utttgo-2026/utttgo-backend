# API-040 — Cambiar estado de cuenta de usuario · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo se contempla `PATCH /api/v1/admin/users/{userId}/status`; no se cambia rol, no se elimina cuenta y no se crean rutas adicionales. Si afecta Identity o login, se consigna como dependencia, no se implementa aquí sin contrato aprobado.

## Tareas ejecutables sin cambiar contrato

Ninguna tarea técnica READY. Puede realizarse únicamente la conciliación documental de DEV-API040-01, siempre que exista autorización explícita para ratificar el contrato; actualmente permanece BLOCKED por las decisiones abiertas de DTO, sesiones, login, consistencia, auditoría y rate limit.

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API040-01 · DOCUMENTAL · Objetivo: ratificar el contrato de API-040 | API-040, ADMS-01/04/05/06/07; solo `spec.md`, `plan.md`, `task.md` | **Acción:** ratificar cuerpo cerrado, `Active/Suspended`, códigos, mismo estado, revocación, login, consistencia y auditoría, sin editar código. **Hecho:** existe un acta de autorización identificable y las tres fichas documentales concilian esas decisiones; `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-040-update-user-status` no presenta errores estructurales. **Evidencia:** acta y salida del validador. No se autoriza implementación. | BLOCKED |

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API040-02 · IMPLEMENTACIÓN contrato Identity y endpoint | API-040, ADMS-01/04; `Modules/Identity/Application/IAdminUserStatusWriter.cs`, contrato de revocación, `Modules/Admin/Controllers/AdminUsersStatusController.cs`, DI | Depende de DEV-API040-01 y autorización de código: publicar contrato detrás de Identity, exponer PATCH y respetar fronteras; resultado: endpoint con test concreto de transición/rechazos. Evidencia: test identificado y build, sin producción. | BLOCKED |
| DEV-API040-03 · IMPLEMENTACIÓN guardas, sesiones y auditoría | API-040, ADMS-02/03/05/06; servicio Admin, Identity, Redis y auditoría del dueño | Depende de DEV-API040-02: exigir Admin local `Active`, objetivo distinto, revocar sesiones al suspender, rechazar login suspendido y auditar fail-closed; resultado: nunca `200` parcial. Evidencia: unitarias de guardas/Redis/auditoría. | BLOCKED |
| DEV-API040-04 · QA_TECNICA escenarios y fallos | API-040, ADMS-01..07; tests Unit/Integration/Security previstos en plan | Depende de DEV-API040-02/03: cubrir JWT, rate limit, Redis/Mongo/Identity/auditoría caídos, mismo estado, login bloqueado y reactivación con sesión nueva. Resultado: evidencia fechada de tests, build y format sin producción. | BLOCKED |
| DEV-API040-05 · INTEGRACIÓN · Objetivo: validar API-040 en entorno no productivo | API-040, AC-005/AC-009; suites aisladas y dobles documentados | **Acción:** después de DEV-API040-01/04, ejecutar pruebas aisladas contra MongoDB, Redis y Supabase no productivos, o dobles explícitamente identificados; no usar producción. **Hecho:** existe un resultado fechado, enlace o artefacto verificable que cubre AC-005/AC-009 y declara el límite de entorno/dobles. **Evidencia:** resultado fechado y límite de entorno declarado. La tarea no autoriza implementación por sí misma. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), estas tareas y [guía SDD](../../../docs/sdd-guia-api-atomica.md). Comprobar autorización y dependencias antes de actuar; elegir solo una tarea READY. Consultar el código vigente sin inferir aprobación por su existencia. Ejecutar únicamente los comandos definidos después de autorización, en entorno no productivo, registrar fecha, resultado y evidencia sanitizada; no modificar otras APIs, no cerrar gates por inferencia y no probar contra producción.

### Control de ejecución

Una fila por ID detallado. `Cambio técnico=SI` incluye código o tests funcionales. Una tarea técnica READY requiere acta aprobada de ficha y gates cerrados; `DONE` requiere evidencia concreta, no `PENDIENTE`.

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API040-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: ratificación HTTP/DTO, sesiones, consistencia y auditoría | - | PENDIENTE |
| DEV-API040-02 | IMPLEMENTACIÓN | BLOCKED | SI | Pendiente: DEV-API040-01 y contrato Identity aprobado | DEV-API040-01 | PENDIENTE |
| DEV-API040-03 | IMPLEMENTACIÓN | BLOCKED | SI | Pendiente: DEV-API040-02, revocación/login/auditoría aprobados | DEV-API040-02 | PENDIENTE |
| DEV-API040-04 | QA_TECNICA | BLOCKED | SI | Pendiente: DEV-API040-02/03 y entorno aislado | DEV-API040-02, DEV-API040-03 | PENDIENTE |
| DEV-API040-05 | INTEGRACIÓN | BLOCKED | SI | Pendiente: DEV-API040-01/04 y AC-005/AC-009 | DEV-API040-01, DEV-API040-04 | PENDIENTE |

## Decisiones abiertas

1. Ratificar DTO y códigos exactos, especialmente `409 ACCOUNT_STATUS_UNCHANGED`.
2. Definir cómo Identity enumera y revoca todas las sesiones y mantiene TTL hasta `exp`.
3. Elegir consistencia entre estado, Redis y auditoría sin resultado parcial observable.
4. Definir dueño, retención e idempotencia de auditoría.
5. Ratificar política y ventana de rate limit de la mutación.
6. Confirmar contrato de login rechazado en `Suspended` y reingreso únicamente con sesión nueva.

## Evidencia

No hay evidencia `Verificado`: no se ejecutaron comandos ni tests y todas las tareas permanecen bloqueadas. El cierre deberá identificar el test o curl, fecha, resultado y límite de dobles/entorno.
