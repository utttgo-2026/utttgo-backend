# API-017 — Solicitar URL de subida de imagen · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Falta Media; solo tarea de implementación después de aprobar ficha fase 05.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: JSON, autorización y política de tamaño PUT pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API017-01 · DOCUMENTAL definir intención | BR-007, UPL-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras decisión | Equipo Media/seguridad aprueba MIME/extensión/tamaño, actor, firma PUT, expiración y DTO/HTTP; cotejar Excel autorizado. **Hecho**: acta fase 05 y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-017-media-upload-url` sin errores. | BLOCKED |
| DEV-API017-02 · IMPLEMENTACION intención y metadata | BR-007, UPL-01/02; `Modules/Media/Controllers/MediaController.cs`, `Contracts/UploadIntentRequest.cs`, `Contracts/UploadIntentResponse.cs`, `Validators/UploadIntentRequestValidator.cs`, `Application/CreateUploadIntentService.cs`, `Infrastructure/MediaAssetDocument.cs`, `MongoMediaAssetRepository.cs` | Tras DEV-API017-01: validar JWT/actor y límites, guardar `PendingUpload` propio, nunca bytes ni imagen pública. **Hecho**: `CreateUploadIntentServiceTests.cs` y `UploadIntentEndpointTests.cs` prueban metadata y rechazo inválido; build Release verde. | BLOCKED |
| DEV-API017-03 · IMPLEMENTACION firmar PUT acotado | BR-007, UPL-01/03/04; `Modules/Media/Infrastructure/S3UploadPresigner.cs`, `Application/CreateUploadIntentService.cs`, registro DI `src/UTTTGO.Api/Program.cs` | Tras DEV-API017-01/02: generar firma para un objeto staging ≤5 min con content-type/tamaño restringidos y sin credenciales al cliente; manejar fallo de firma/registro. **Hecho**: test S3 doble/emulado comprueba objeto/headers/vencimiento y 503 fail-closed. | BLOCKED |
| DEV-API017-04 · QA_TECNICA abuso/caídas | BR-007, UPL-02/03/04; `tests/UtttGo.IntegrationTests/UploadIntentEndpointTests.cs`, `tests/UtttGo.SecurityTests/UploadIntentSecurityTests.cs` | Tras DEV-API017-02/03 y permiso de contenedores: probar JWT revocado/ajeno, MIME/tamaño, Redis/Mongo/S3 caídos, TTL/CORS sin comodín; no usar S3 productivo. **Hecho**: evidencia fechada de build/formato y tres suites Release verdes, sin secretos/URL en logs. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); verificar acta y entorno S3 emulado antes de editar; no desplegar ni modificar Terraform desde esta tarea.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API017-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: contrato Media fase 05 y autorización | - | PENDIENTE |
| DEV-API017-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API017-01, gate de aprobación | DEV-API017-01 | PENDIENTE |
| DEV-API017-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: BR-007, firma y política de tamaño aprobadas | DEV-API017-01, DEV-API017-02 | PENDIENTE |
| DEV-API017-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permiso S3 emulado/Mongo/Redis y ficha Media | DEV-API017-02, DEV-API017-03 | PENDIENTE |
