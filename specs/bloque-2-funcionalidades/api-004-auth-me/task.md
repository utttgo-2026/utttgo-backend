# API-004 — Sesión propia · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → estas tareas. Solo sesión propia; no crear endpoints, escribir OTP/login/logout/refresh ni cambiar contratos de API-001/002. Los DEV son locales y no cierran TASK-AUTH históricos. Un registro READY documental no autoriza programación.

## Tareas ejecutables sin cambiar contrato

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API004-01 · DOCUMENTAL inventariar autoridad de sesión | RF-12; SES-01/02/03/04/05; solo [spec](spec.md), [plan](plan.md), [task](task.md) en futura intervención documental autorizada | Sin dependencia DEV; cotejar fuentes secundarias y, cuando haya Excel autorizado, su fila con esta ficha; registrar diferencias sin aprobarlas ni editar historial. **Hecho**: matriz fechada de fuentes/ausencias y diff documental revisado; comando `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-004-auth-me`, evidencia salida 0 errores y referencia a cotejo pendiente si falta fuente. | READY |

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API004-02 · DOCUMENTAL ratificar contrato específico | RF-12, CL-14; SES-01/04; solo [spec](spec.md), [plan](plan.md), [task](task.md) en futura aprobación explícita | Depende de DEV-API004-01 y DF02-07, TASK-AUTH-010/022: acta del equipo Identity/API Designer/seguridad que coteje Excel autorizado y decida cinco campos, códigos, `404` vs `401` y antienumeración; actualizar únicamente contrato/brechas según acta. **Hecho**: acta fechada enlazada con alcance y prueba `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-004-auth-me` (0 errores), sin inferir aprobación por enlace. | BLOCKED |
| DEV-API004-03 · QA_TECNICA acreditar guard con dependencias aisladas | REQ-007/008, RF-12; SES-01/02/03/04/05; suites [`SessionServiceTests`](../../../tests/UtttGo.UnitTests/SessionServiceTests.cs), [`SessionEndpointTests`](../../../tests/UtttGo.IntegrationTests/SessionEndpointTests.cs), [`SessionSecurityTests`](../../../tests/UtttGo.SecurityTests/SessionSecurityTests.cs); sin cambio de producción por esta tarea | Depende de DEV-API004-02, DF02-01/04/09, TASK-AUTH-012/021, DF02-08 y cadena TASK-AUTH-007→008→010: autorización de pruebas no productivas, actas de confirmación/fallo parcial y estrategia sin `jti` conforme constitución. Probar con JWT firmado de prueba, Redis/Mongo aislados y proveedor controlado: estados, revocación, errores de dependencias y DTO; no usar servicios productivos. **Hecho**: informe fechado por SES-01..05 con comandos `dotnet restore UTTTGO.Backend.slnx`, `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`, `dotnet test UTTTGO.Backend.slnx -c Release --no-build`, resultados y límites; cualquier discrepancia exige tarea de corrección separada tras acta, no reimplementación automática. | BLOCKED |

## Guía de ejecución con OpenCode

En el worktree correcto leer [constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → este control y [guía](../../../docs/sdd-guia-api-atomica.md). Comprobar rama/estado Git y dependencias del ID elegido. DEV-API004-01 permite **solo** inventario y verificación documental; no marcar DONE sin evidencia fechada. Para DEV-API004-02/03 exigir actas genuinas con alcance revisado por personas, no enlaces aparentes ni código como autorización. Preparar cambios por tarea, ejecutar comandos solo en entorno autorizado, registrar salidas sanitizadas y actualizar control tras comprobación. No editar middleware, producción ni otras APIs por inferencia.

### Control de ejecución

Una fila por ID detallado. `Cambio técnico=SI` incluye tests nuevos; las dos tareas BLOCKED no se habilitan por completar una lectura documental.

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API004-01 | DOCUMENTAL | READY | NO | N/A: lectura y validación documental sin aprobar contrato | - | PENDIENTE |
| DEV-API004-02 | DOCUMENTAL | BLOCKED | NO | Pendiente: DF02-07 y TASK-AUTH-010/022; acta específica HTTP/DTO y cotejo Excel autorizado | DEV-API004-01 | PENDIENTE |
| DEV-API004-03 | QA_TECNICA | BLOCKED | SI | Pendiente: DF02-01/04/08/09 y TASK-AUTH-007/008/010/012/021; autorización de entorno no productivo | DEV-API004-02 | PENDIENTE |
