# API-018 — Completar subida y moderar imagen · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Media controla transición, Catalog solo consume asset aprobado mediante interfaz futura.

## Arquitectura existente

No existe `Modules/Media` ni tests; API-017 es dependencia de registro `PendingUpload`. Bucket S3 Terraform existente no demuestra integración Content Safety. Reutilizar guard Identity/Redis, errores y Mongo transversales sin duplicar `products`.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Media/Controllers/MediaController.cs`, `Contracts/CompleteUploadResponse.cs`, `Validators/AssetIdValidator.cs` | POST, ID, resultado/error aprobado. |
| Aplicación | `src/UTTTGO.Api/Modules/Media/Application/CompleteUploadService.cs` | Verificar dueño/objeto, moderar y transicionar sin doble aprobación. |
| Infraestructura | `src/UTTTGO.Api/Modules/Media/Infrastructure/MongoMediaAssetRepository.cs`, `S3MediaObjectStore.cs`, `AzureContentSafetyClient.cs` | CAS `mediaAssets`, HEAD/delete S3, HTTPS Content Safety. |

## Secuencia técnica

Validar JWT y rate limit → buscar `PendingUpload` por assetId/propietario (API-017) → HEAD S3 y cotejar MIME/tamaño/clave con intención → moderar antes de publicación → seguro: CAS `Approved`; inseguro: borrar S3 y CAS `Rejected`; fallo/timeout: permanecer no publicado y definir reintento/limpieza según acta. Carrera de dos complete no duplica aprobación; Catalog recibe solo un contrato lector `Approved`.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| CMP-01/04 | `Modules/Media/Controllers/MediaController.cs`, `Contracts/CompleteUploadResponse.cs`, `Validators/AssetIdValidator.cs`, `Application/CompleteUploadService.cs` | Guard, propiedad y respuesta tras acta. |
| CMP-01/02/03 | `Modules/Media/Infrastructure/MongoMediaAssetRepository.cs`, `S3MediaObjectStore.cs`, `AzureContentSafetyClient.cs`, `src/UTTTGO.Api/Program.cs` | HEAD/delete/CAS y moderación; DI sin reordenar middleware. |
| CMP-01..04 | `tests/UtttGo.UnitTests/CompleteUploadServiceTests.cs`, `tests/UtttGo.IntegrationTests/CompleteUploadEndpointTests.cs`, `tests/UtttGo.SecurityTests/CompleteUploadSecurityTests.cs` | Dobles Content Safety/S3, Mongo/Redis aislados. |

## Orden de implementación

DEV-API018-01 ratifica transición/HTTP; DEV-API018-02 valida objeto/propiedad usando contrato API-017; DEV-API018-03 integra moderación y rechazo seguro; DEV-API018-04 cubre concurrencia/fallos. No publicar productos desde Media.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| Media fase 05, BR-007 | DEV-API018-01/02/03/04 | Acta umbral, compensación, estados/HTTP e idempotencia. |
| API-017 y Content Safety | DEV-API018-02/03 | Registro propio PendingUpload, servicio HTTPS configurable; CI mock. |

## Estrategia de pruebas

Unitarias estado seguro/inseguro/timeout/carrera, integración WebApplicationFactory + Mongo/Redis aislados y S3 emulado o doble; Content Safety siempre mock en CI. Seguridad JWT ajeno/revocado, objeto sustituido, fallo HEAD/delete, 429/503 y no publicar sin Approved. Tras autorización: build Release, format y tres proyectos `dotnet test -c Release --no-build`; nunca servicios reales CI/producción.

## Seguridad, errores y observabilidad

Error único `correlationId`, logs de resultado sin payload o URL/token, S3 privado; fail-closed moderación y rate limit. No alterar middleware.

## Criterios de cierre técnico

CMP-01..04 probados con evidencia fechada, asset inseguro eliminado/no visible, error Content Safety no aprueba, suites/build/formato verdes; contrato de Media aprobado separadamente.
