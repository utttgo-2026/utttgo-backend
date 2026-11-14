# API-009 — Autoactivación de vendedor · task

Estado de aprobación de la ficha: PARCIAL

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No repetir TASK-SELLER-020 ya implementada; no asumir que tests con dobles verifican proveedores.

## Tareas ejecutables sin cambiar contrato

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API009-01 · DOCUMENTAL cotejar aprobación atómica | RF-V01..05, ACT-01/02/03/04/05; solo `spec.md`, `plan.md`, `task.md` tras autorización documental | Sin dependencia DEV: solicitar constancia identificable de alcance Sprint 1, gates Identity aplicables y cotejo Excel autorizado; no cambiar contratos por inferencia. **Hecho**: matriz de aprobación y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-009-seller-activation` con 0 errores; no marcar DONE sin acta. | READY |

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API009-02 · QA_TECNICA probar activación real aislada | RF-V01..05/09, ACT-01/02/03/04/05; `tests/UtttGo.IntegrationTests/SellerActivationEndpointTests.cs`, `tests/UtttGo.SecurityTests/SellerActivationSecurityTests.cs` | Tras DEV-API009-01, autorización Mongo/Redis no productivos y gates aplicables: probar upsert/retry, promoción fallida, estado Pending oculto, JWT/revocación, 400/403/409/429/503. **Hecho**: resultados fechados con build, format y tres suites Release; no usar Atlas/Supabase productivos. | BLOCKED |
| DEV-API009-03 · QA_TECNICA acreditar endpoint desplegado | TASK-SELLER-041, ACT-01/02; `tests/UtttGo.IntegrationTests/SellerActivationEndpointTests.cs` solo si se requieren casos adicionales | Tras DEV-API009-02 y autorización operativa explícita del propietario para entorno no productivo: registrar respuesta específica, idempotencia y límites sin tokens/PII; no inferir de `/health` ni repetir despliegue. **Hecho**: artefacto autorizado fechado con entorno/SHA/respuestas sanitizadas y evidencia TASK-SELLER-041, sin afirmar verificación antes. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); validar permiso y gates; no programar otra API ni modificar producción.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API009-01 | DOCUMENTAL | READY | NO | N/A: cotejo de lectura, no cambia contrato sin acta | - | PENDIENTE |
| DEV-API009-02 | QA_TECNICA | BLOCKED | SI | Pendiente: autorización local y gates TASK-AUTH-007/008/010 aplicables | DEV-API009-01 | PENDIENTE |
| DEV-API009-03 | QA_TECNICA | BLOCKED | SI | Pendiente: TASK-SELLER-041 y autorización operativa no productiva | DEV-API009-02 | PENDIENTE |
