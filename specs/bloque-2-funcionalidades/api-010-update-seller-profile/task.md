# API-010 — Editar perfil propio de vendedor · task

Estado de aprobación de la ficha: PARCIAL

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. PATCH y CAS existentes; solo brechas de evidencia, sin implementar Media o cambios de rol.

## Tareas ejecutables sin cambiar contrato

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API010-01 · DOCUMENTAL ratificar alcance PATCH | RF-V07/08/09, EDIT-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` con futura autorización | Sin dependencia DEV: solicitar acta de aprobación individual, confirmar gates y cotejar Excel autorizado; anotar exclusión de avatar DEC-019.e, sin reescribir fase 02. **Hecho**: acta y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-010-update-seller-profile` sin errores, no declarar DONE por esta ficha. | READY |

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API010-02 · QA_TECNICA probar CAS y seguridad | RF-V07/08/09, EDIT-01/02/03/04; `tests/UtttGo.IntegrationTests/UpdateOwnSellerProfileEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateOwnSellerProfileSecurityTests.cs` | Tras DEV-API010-01, permiso Mongo/Redis aislados y gates aplicables: dos PATCH concurrentes (uno `200`, otro `409`), relectura, otro usuario `404`, campos extra `400`, JWT revocado y Redis caído sin escritura. **Hecho**: evidencia fechada con build Release, format, tres suites Release y versiones observadas sin PII. | BLOCKED |
| DEV-API010-03 · QA_TECNICA acreditar PATCH desplegado | TASK-SELLER-041, EDIT-01/02; `tests/UtttGo.IntegrationTests/UpdateOwnSellerProfileEndpointTests.cs` solo si faltan casos | Tras DEV-API010-02 y autorización operativa no productiva: registrar respuesta PATCH específica y relectura sin token/datos personales ni cambios de infraestructura. **Hecho**: artefacto fechado con SHA/entorno/respuestas sanitizadas; `/health` no acredita PATCH. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md). Revisar código antes de editar tests; ejecutar únicamente pruebas autorizadas no productivas, no corregir defectos no demostrados como parte de QA.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API010-01 | DOCUMENTAL | READY | NO | N/A: lectura; modificación de contrato exige acta | - | PENDIENTE |
| DEV-API010-02 | QA_TECNICA | BLOCKED | SI | Pendiente: TASK-AUTH-007/008/010 aplicables y autorización local Mongo/Redis | DEV-API010-01 | PENDIENTE |
| DEV-API010-03 | QA_TECNICA | BLOCKED | SI | Pendiente: TASK-SELLER-041 y permiso operativo no productivo | DEV-API010-02 | PENDIENTE |
