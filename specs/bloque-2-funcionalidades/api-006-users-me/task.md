# API-006 — Leer perfil propio · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo `GET /api/v1/users/me`; no implementar sesión API-004, edición API-007, OTP, login/logout, subida ni moderación de Media. Los IDs DEV no cierran TASK-AUTH-004/022/023. Una tarea READY documental no habilita ninguna técnica.

## Tareas ejecutables sin cambiar contrato

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API006-01 · DOCUMENTAL cotejar fuentes del perfil | RF-03/04/12; PRF-01/02/03/04/05/06; solo [spec](spec.md), [plan](plan.md), [task](task.md) en una futura intervención documental autorizada | Sin dependencia DEV; contrastar trazabilidad, migración, fase 02 y código; cuando se disponga del Excel autorizado, cotejar ficha directamente sin copiar worktree ajeno. Registrar diferencias como pendientes sin aprobarlas. **Hecho**: inventario de fuentes y brechas fechado, salida `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-006-users-me` con 0 errores; si falta Excel registrar expresamente pendiente de fuente. | READY |

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API006-02 · DOCUMENTAL ratificar HTTP/DTO y lectura avatar | RF-03/04/12, T-AUTH-17; PRF-01/03/04/05; solo [spec](spec.md), [plan](plan.md), [task](task.md) bajo aprobación explícita | Depende de DEV-API006-01; DF02-05/07, TASK-AUTH-004/010/023: equipo Identity/Users, Media, API Designer y seguridad coteja Excel autorizado y decide `401` o `404`, seis campos, phone/avatar nulos, propiedad, activo `Approved` y política de fallo Media. Actualizar ficha **solo** según acta. **Hecho**: acta fechada identificable y revisión humana del alcance, validador de ficha 0 errores; enlace por sí solo no prueba aprobación. | BLOCKED |
| DEV-API006-03 · QA_TECNICA verificar guard y proyección con servicios aislados | RF-12, REQ-006/007; PRF-01/02/03/06; [`OwnProfileServiceTests`](../../../tests/UtttGo.UnitTests/OwnProfileServiceTests.cs), [`OwnProfileEndpointTests`](../../../tests/UtttGo.IntegrationTests/OwnProfileEndpointTests.cs), [`OwnProfileSecurityTests`](../../../tests/UtttGo.SecurityTests/OwnProfileSecurityTests.cs); sin editar producción | Depende de DEV-API006-02; DF02-01/04/08/09, TASK-AUTH-007→008→010/012/022/023 y autorización no productiva. Verificar JWT firmado de prueba, revocación Redis, confirmación Supabase controlada, Mongo aislado, propiedad, estados y fallos fail-closed; no reimplementar guard API-004. **Hecho**: informe fechado por PRF-01/02/03/06 con resultados `dotnet restore UTTTGO.Backend.slnx`, `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore` y suites unitarias/integración/seguridad indicadas en [plan](plan.md); discrepancias abren corrección autorizada separada. | BLOCKED |
| DEV-API006-04 · INTEGRACION conectar solo lectura Media aprobada | RF-04, BR-007; PRF-04/05; [`OwnProfileService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/OwnProfileService.cs), [`Program.cs`](../../../src/UTTTGO.Api/Program.cs), contrato `IApprovedAvatarReadAccess` del dueño Media y [`OwnProfileEndpointTests`](../../../tests/UtttGo.IntegrationTests/OwnProfileEndpointTests.cs) | Depende de DEV-API006-02; DF02-05/07/08, TASK-AUTH-004/007→008→010/023, contrato Media aprobado y adaptador de lectura existente/autorizado; autorización del dueño y prueba no productiva. Sustituir adaptador nulo solo mediante interfaz que compruebe activo propio `Approved` y devuelva presigned GET backend de S3 privado; **no** implementar subida, moderación ni acceso directo a colección Media en Identity. **Hecho**: prueba fechada de asset propio aprobado/ajeno/no aprobado/fallo Media, URL privada temporal o `null`, sin secretos, con build Release, formato y suites pertinentes en verde; no afirmar integración por doble. | BLOCKED |

## Guía de ejecución con OpenCode

En rama/worktree correcto leer [constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → estas tareas y [guía](../../../docs/sdd-guia-api-atomica.md). Revisar `git status --short --branch` y elegir solo READY con dependencias cumplidas. DEV-API006-01 admite inventario documental; no marcar DONE por redactar esta ficha. Las tareas BLOCKED exigen actas humanas verificadas, aprobación SDD y permisos de entorno antes de editar tests/producción. Ejecutar comandos de la tarea únicamente en local/no productivo, anexar resultado fechado y sanitizado; no cambiar otras APIs, middleware ni estado sin autorización. Si una decisión modifica contrato, actualizar el trío antes de generar corrección específica.

### Control de ejecución

Una fila por tarea detallada; `Cambio técnico=SI` incluye tests y adaptadores. Ninguna tarea técnica está READY y ninguna tarea se marca DONE por evidencia histórica con dobles.

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API006-01 | DOCUMENTAL | READY | NO | N/A: lectura y cotejo documental sin aprobar contrato | - | PENDIENTE |
| DEV-API006-02 | DOCUMENTAL | BLOCKED | NO | Pendiente: DF02-05/07, TASK-AUTH-004/010/023 y acta API-006 por equipo | DEV-API006-01 | PENDIENTE |
| DEV-API006-03 | QA_TECNICA | BLOCKED | SI | Pendiente: DF02-01/04/08/09, TASK-AUTH-007/008/010/012/022/023 y permiso no productivo | DEV-API006-02 | PENDIENTE |
| DEV-API006-04 | INTEGRACION | BLOCKED | SI | Pendiente: DF02-05/07/08, TASK-AUTH-004/007/008/010/023, contrato Media y autorización del dueño | DEV-API006-02 | PENDIENTE |
