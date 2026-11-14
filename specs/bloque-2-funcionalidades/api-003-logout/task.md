# API-003 — Cerrar sesión · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No implementar hasta contrato/gates; DEV no equivale a TASK-AUTH-025.

## Tareas ejecutables sin cambiar contrato

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API003-01 · DOCUMENTAL resolver sesiones | RF-10, LOG-03; solo `spec.md`, `plan.md`, `task.md` tras decisión | DF02-04 y TASK-AUTH-003: obtener acta de Identity/móvil/seguridad que elija refresh, cuerpo, repetición, sin `jti` y HTTP; conciliar ficha con Excel autorizado. **Hecho**: acta identificada y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-003-logout` sin errores; no aprobar por inferencia. | BLOCKED |

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API003-02 · IMPLEMENTACION revocar JWT | RF-10, LOG-01; `AuthController.cs`, nuevo `LogoutService.cs`, adaptador Redis y DI de Identity | Tras DEV-API003-01 y TASK-AUTH-007→008→010: añadir ruta, guard, escritor `revoked:{jti}` con TTL a `exp` y respuesta ratificada; sin refresh implícito. **Hecho**: unitarias de TTL y endpoint revocado con `LogoutServiceTests`/`LogoutEndpointTests`, Release verde. | BLOCKED |
| DEV-API003-03 · QA_TECNICA fallo cerrado | AC-006, LOG-01/02; nuevos `LogoutEndpointTests.cs`, `LogoutSecurityTests.cs` | Tras DEV-API003-02 y permiso local Redis: probar entre dos clientes/réplicas, JWT inválido/sin `jti`, Redis caído y rate limit sin confirmar éxito. **Hecho**: artefacto fechado con tests Release de integración y seguridad, formato verde y error con `correlationId`. | BLOCKED |
| DEV-API003-04 · INTEGRACION refresh según acta | TASK-AUTH-003, LOG-03; `LogoutService.cs`, adaptador Supabase si se aprueba, `LogoutServiceTests.cs` | Tras DEV-API003-01/02 y decisión sobre refresh: implementar solo opción ratificada; simular proveedor caído/repetición y probar que no se renueva acceso. **Hecho**: pruebas fechadas y contrato móvil coordinado, sin tokens en logs ni pruebas productivas. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); verificar gates antes de cada cambio; usar servicios no productivos; registrar comandos y fecha sin secretos.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API003-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: DF02-04, TASK-AUTH-003 y acta de contrato | - | PENDIENTE |
| DEV-API003-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API003-01 y TASK-AUTH-007/008/010 | DEV-API003-01 | PENDIENTE |
| DEV-API003-03 | QA_TECNICA | BLOCKED | SI | Pendiente: TASK-AUTH-007/008/010 y prueba local Redis autorizada | DEV-API003-02 | PENDIENTE |
| DEV-API003-04 | INTEGRACION | BLOCKED | SI | Pendiente: DF02-04, TASK-AUTH-003 y autorización de integración | DEV-API003-01, DEV-API003-02 | PENDIENTE |
