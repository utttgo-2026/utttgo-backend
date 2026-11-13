# API-007 — Editar perfil propio · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo PATCH propio. La aprobación individual de la responsable backend no cierra [paquete Identity](../../../docs/aprobaciones-sdd-lote-2a.md), DF02-05/07 ni TASK-AUTH-004/007/008/010/023/024. DEV no sustituye TASK histórico. No tocar API-004/006, login/OTP/logout ni implementar Media en Identity sin contrato aprobado.

## Tareas ejecutables sin cambiar contrato

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API007-01 · DOCUMENTAL inventariar PATCH frente a fuentes | RF-04; EDT-01/02/03/04/05/06/07/08; solo [spec](spec.md), [plan](plan.md), [task](task.md) en una futura intervención documental autorizada | Sin dependencia DEV; cotejar ruta, decisiones, código Mongo actual y QA histórico desfasado; localizar Excel solo por vía autorizada. Registrar discrepancias sin editar fuentes ni aprobarlas. **Hecho**: matriz fechada de fuentes/ausencias/cotejo pendiente y salida `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-007-update-users-me` con 0 errores; no marcar DONE por esta ficha. | READY |

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API007-02 · DOCUMENTAL decidir contrato PATCH | RF-04, CL-12; EDT-01/02/03/07/08; solo [spec](spec.md), [plan](plan.md), [task](task.md) bajo acta futura | Depende de DEV-API007-01 y decisión DF02-05/07, TASK-AUTH-004/010/023 según alcance; TASK-AUTH-024 se cierra **después** de esta decisión y las pruebas: solicitar acta específica equipo API Designer/Identity/Users/Media/seguridad sobre whitelist, nombre/teléfono/null/borrado, avatar escritura, `400/401/409/503`, retry y DTO, con cotejo Excel autorizado. **Hecho**: decisión expresa fechada (incluida alternativa descartada), trío conciliado y validador ficha 0 errores; no inferir autorización por existencia del paquete. | BLOCKED |
| DEV-API007-03 · QA_TECNICA verificar CAS Mongo y relectura | RF-04, RNF-17; EDT-01/05/06/08; [`MongoUserRepositories.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) solo inspección, [`ConcurrentOwnProfileUpdateTests.cs`](../../../tests/UtttGo.UnitTests/ConcurrentOwnProfileUpdateTests.cs), [`UpdateOwnProfileEndpointTests.cs`](../../../tests/UtttGo.IntegrationTests/UpdateOwnProfileEndpointTests.cs) | Depende de DEV-API007-02, DF02-07/08, TASK-AUTH-007→008→010/011/012/023 y autorización Mongo **no productivo**. Probar con contenedor Mongo aislado dos PATCH del mismo `Version` (un éxito, otro `409`), carrera con suspensión, perfil desaparecido `401`, fallo de escritura y GET posterior; comprobar versión +1 y campos prohibidos/omitidos preservados. **Hecho**: artefacto fechado con configuración sanitizada, resultado real por EDT, comandos Release/formato/tres suites del [plan](plan.md), sin atribuir CAS real a doble; evidencia para futuro cierre TASK-AUTH-024, no requisito previo. | BLOCKED |
| DEV-API007-04 · QA_TECNICA verificar guard y fallos críticos | RF-04, REQ-007; EDT-03/04/06; [`UpdateOwnProfileSecurityTests.cs`](../../../tests/UtttGo.SecurityTests/UpdateOwnProfileSecurityTests.cs), [`UpdateOwnProfileEndpointTests.cs`](../../../tests/UtttGo.IntegrationTests/UpdateOwnProfileEndpointTests.cs); guard API-004 solo lectura | Depende de DEV-API007-02 y DF02-01/08/09, TASK-AUTH-007→008→010/012/022/023; autorización local/no productiva para JWT firmado, Redis, Mongo y Supabase controlados. Verificar sin escritura ante JWT inválido/revocado, cuenta pendiente/suspendida, cuerpo privilegiado, sujeto ajeno, Redis/Supabase/Mongo caídos; medir respuesta/error `correlationId` sin secretos. **Hecho**: informe fechado por EDT-03/04/06 con comandos Release/formato/tres suites del [plan](plan.md), estado/entorno/prueba; evidencia para futuro cierre TASK-AUTH-024, no modificar middleware ni reimplementar guard. | BLOCKED |
| DEV-API007-05 · INTEGRACION asociación de avatar propio aprobado | RF-04, BR-007, T-AUTH-17; EDT-07; [`UpdateOwnProfileRequest.cs`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/UpdateOwnProfileRequest.cs), [`UpdateOwnProfileService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/UpdateOwnProfileService.cs), [`MongoUserRepositories.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) y contrato publicado de Media, tests PATCH/seguridad | Depende de DEV-API007-02, DF02-05/07/08, TASK-AUTH-004/007→008→010/023, acta **de escritura** y dueño Media con servicio autorizado/no productivo. Solo tras actualizar ficha al contrato elegido, permitir vincular o, **si se aprueba eliminación**, borrar asset propio `Approved` con comprobación Media y CAS Mongo preservando campos ajenos; no aceptar URL arbitraria, publicar S3, subir ni moderar desde Identity. **Hecho**: prueba fechada de activo propio/aprobado, ajeno, pendiente/rechazado, carrera/fallo parcial y respuesta segura; build/formato/suites y contrato Media auditables; evidencia para futuro cierre TASK-AUTH-024; sin adaptador aprobado continuar BLOCKED. | BLOCKED |

## Guía de ejecución con OpenCode

Verificar rama/HEAD/estado Git; leer [constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas y [guía](../../../docs/sdd-guia-api-atomica.md). Seleccionar solo READY cuyas dependencias/alcance sean compatibles: 01 es documental, no programación. Para 02..05 revisar acta humana, delegación y cadena histórica; aun con acta de contrato, exigir permisos de pruebas separados y contrato Media para escritura. Ejecutar comandos solo en local/no productivo, guardar fecha/salida/alcance/límites sin secretos; si aparece defecto, proponer corrección trazada antes de editar producción de forma adicional. No marcar DONE sin evidencia ni cambiar otra ficha o estados por inferencia.

### Control de ejecución

Una fila por ID. `Cambio técnico=SI` incluye tests con servicios aislados. Ninguna tarea técnica READY, ninguna DONE por QA histórico o AWS comunicado.

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API007-01 | DOCUMENTAL | READY | NO | N/A: lectura documental sin contrato aprobado | - | PENDIENTE |
| DEV-API007-02 | DOCUMENTAL | BLOCKED | NO | Pendiente: DF02-05/07, TASK-AUTH-004/010/023 y acta equipo por PATCH; 024 se cierra después | DEV-API007-01 | PENDIENTE |
| DEV-API007-03 | QA_TECNICA | BLOCKED | SI | Pendiente: DF02-07/08, TASK-AUTH-007/008/010/011/012/023 y Mongo local autorizado | DEV-API007-02 | PENDIENTE |
| DEV-API007-04 | QA_TECNICA | BLOCKED | SI | Pendiente: DF02-01/08/09, TASK-AUTH-007/008/010/012/022/023 y permiso no productivo | DEV-API007-02 | PENDIENTE |
| DEV-API007-05 | INTEGRACION | BLOCKED | SI | Pendiente: DF02-05/07/08, TASK-AUTH-004/007/008/010/023 y contrato escritura Media | DEV-API007-02 | PENDIENTE |
