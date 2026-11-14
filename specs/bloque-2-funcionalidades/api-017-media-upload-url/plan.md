# API-017 — Solicitar URL de subida de imagen · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Media gestiona intención, no moderación ni creación de producto.

## Arquitectura existente

Sin `Modules/Media` ni tests. Reutilizar gate Identity/Redis, Mongo transversal; bucket privado y perfil EC2 existentes en `infra/terraform/` solo como dependencia, no como endpoint implementado. `mediaAssets` pertenecerá a Media, nunca a Catalog.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Media/Controllers/MediaController.cs`, `Contracts/UploadIntentRequest.cs`, `Contracts/UploadIntentResponse.cs`, `Validators/UploadIntentRequestValidator.cs` | JWT, metadatos, errores, DTO según acta. |
| Aplicación | `src/UTTTGO.Api/Modules/Media/Application/CreateUploadIntentService.cs` | Derivar actor, crear `PendingUpload` y pedir firma un objeto. |
| Infraestructura | `src/UTTTGO.Api/Modules/Media/Infrastructure/MediaAssetDocument.cs`, `MongoMediaAssetRepository.cs`, `S3UploadPresigner.cs` | Persistir metadata y firmar PUT staging vía Instance Profile. |

## Secuencia técnica

Tras aprobación fase 05: leer JWT validado/blacklist, autorización y rate limit → validar MIME/extensión/tamaño → generar clave privada no confiada al cliente → registrar `PendingUpload` con propietario/vencimiento → firmar PUT único TTL ≤5 min con content-type y restricción de tamaño comprobable → devolver DTO; fallo de firma/almacén nunca marca Approved ni deja URL pública; definir limpieza de huérfanos y expiración. El cliente sube directo a S3 y notifica a API-018.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| UPL-01/02 | `Modules/Media/Controllers/MediaController.cs`, `Contracts/UploadIntentRequest.cs`, `Contracts/UploadIntentResponse.cs`, `Validators/UploadIntentRequestValidator.cs`, `Application/CreateUploadIntentService.cs` | POST/actor/DTO validados. |
| UPL-01/03 | `Modules/Media/Infrastructure/MediaAssetDocument.cs`, `MongoMediaAssetRepository.cs`, `S3UploadPresigner.cs`, `src/UTTTGO.Api/Program.cs` | Registro/firmador e inyección sin alterar middleware. |
| UPL-01..04 | `tests/UtttGo.UnitTests/CreateUploadIntentServiceTests.cs`, `tests/UtttGo.IntegrationTests/UploadIntentEndpointTests.cs`, `tests/UtttGo.SecurityTests/UploadIntentSecurityTests.cs` | Tests nuevos con doble S3, Mongo/Redis aislados. |

## Orden de implementación

DEV-API017-01 ratifica contrato y límites; DEV-API017-02 implementa petición/metadata; DEV-API017-03 implementa firma y tratamiento de fallos; DEV-API017-04 prueba políticas del PUT. API-018 consumirá asset por interfaz Media, sin duplicar repositorio.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| Ficha Media fase 05, BR-007 | DEV-API017-01/02/03/04 | Acta MIME, tamaño, actor, HTTP, firma y expiración. |
| AWS S3/IAM/CORS | DEV-API017-03/04 | Política privada Terraform y emulador S3; cambiar infra solo por ficha/permiso propios. |

## Estrategia de pruebas

Unitarias MIME/tamaño/actor/TTL y error, integración WebApplicationFactory Mongo/Redis aislados y S3 emulado o doble verificable, seguridad JWT inválido/revocado, usuario ajeno, 429/503, URL de un objeto sin GET público. Nunca bucket real en CI. Cuando autorizado: build Release, format y tres proyectos `dotnet test -c Release --no-build`.

## Seguridad, errores y observabilidad

S3 privado, credenciales solo Instance Profile, rate limit fail-closed, logs con `correlationId` sin URL presigned completa, JWT, access key o datos personales. Error único; no alterar middleware.

## Criterios de cierre técnico

UPL-01..04 probados por nivel/fecha con firma acotada, registro `PendingUpload`, sin publicación; build/formato/suites verdes y contrato fase 05 aprobado separadamente.
