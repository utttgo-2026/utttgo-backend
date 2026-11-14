# API-018 — Completar subida y moderar imagen · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Sin implementación; no exponer asset hasta moderación ni crear código de Catalog aquí.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: falta ficha Media fase 05 y política de fallos parciales.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API018-01 · DOCUMENTAL aprobar transición | BR-007, CMP-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | Producto/Media/seguridad definen JSON/HTTP, umbral Content Safety, verificación S3, CAS/idempotencia, timeout y compensación S3/Mongo; cotejar Excel. **Hecho**: acta fase 05, ficha conciliada y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-018-media-complete` con 0 errores. | BLOCKED |
| DEV-API018-02 · IMPLEMENTACION validar asset y objeto | BR-007, CMP-01/04; `Modules/Media/Controllers/MediaController.cs`, `Contracts/CompleteUploadResponse.cs`, `Validators/AssetIdValidator.cs`, `Application/CompleteUploadService.cs`, `Infrastructure/MongoMediaAssetRepository.cs`, `S3MediaObjectStore.cs` | Tras DEV-API018-01 y API-017: resolver dueño, verificar PendingUpload y HEAD S3 (tipo/tamaño/clave) antes de moderar; sin bytes en body. **Hecho**: `CompleteUploadServiceTests.cs` cubre ajeno/ausente/sustituido, build Release verde. | BLOCKED |
| DEV-API018-03 · IMPLEMENTACION moderar y transicionar | BR-007, CMP-01/02/03; `Application/CompleteUploadService.cs`, `Infrastructure/AzureContentSafetyClient.cs`, `MongoMediaAssetRepository.cs`, `S3MediaObjectStore.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API018-01/02: integrar HTTPS Content Safety, CAS seguro Approved, inseguro borrar objeto y Rejected, timeout no aprobado; DI sin tocar middleware. **Hecho**: tests Content Safety mock/S3 emulado y Mongo aislado prueban 3 ramas y estado persistente. | BLOCKED |
| DEV-API018-04 · QA_TECNICA carreras/fallo cerrado | BR-007, CMP-02/03/04; `tests/UtttGo.IntegrationTests/CompleteUploadEndpointTests.cs`, `tests/UtttGo.SecurityTests/CompleteUploadSecurityTests.cs` | Tras DEV-API018-02/03 y permiso local: probar dos complete, rechazo y borrado fallido, Redis/Content Safety caídos, JWT ajeno/revocado y no publicación. **Hecho**: evidencia fechada, build/formato y tres suites Release verdes, sin usar proveedor real en CI. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); verificar API-017 y acta fase 05, usar solo dobles/emulador no productivo.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API018-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: ficha Media fase 05 y acta de fallo parcial | - | PENDIENTE |
| DEV-API018-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API018-01 y registro API-017 disponible | DEV-API018-01 | PENDIENTE |
| DEV-API018-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: umbral/moderación/compensación aprobados | DEV-API018-01, DEV-API018-02 | PENDIENTE |
| DEV-API018-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permisos Mongo/Redis/S3 emulado y contrato fase 05 | DEV-API018-02, DEV-API018-03 | PENDIENTE |
