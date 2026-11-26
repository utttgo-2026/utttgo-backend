# API-005 - Eliminar cuenta propia - plan

Estado de aprobacion de la ficha: APROBADA por el propietario, 2026-11-22.

## Autoridad y alcance

Constitucion -> spec.md -> este plan -> task.md. Identity coordina la baja y solo modifica `users`; los modulos dueños exponen contratos para sus propios datos.

## Arquitectura existente

`UsersController` reutiliza el guard de Identity. `users` pertenece a Identity; Catalog y Orders se consumen mediante `IAccountDeletionCatalog` e `IActiveOrderReader`. No se accede a colecciones ajenas desde Identity.

## Secuencia tecnica aprobada

Validar sujeto con el gate existente -> consultar pedidos activos -> anonimizar `users` con CAS -> pausar productos mediante Catalog -> persistir outbox de Media -> revocar la sesion actual en Redis -> solicitar logout global de Supabase best-effort -> responder `204`. `deletedUserRef` es un UUID aleatorio estable. Mongo, Redis, Catalog o la persistencia del outbox fallidos devuelven `503` fail-closed; la purga S3 posterior usa reintentos con lease.

## Componentes

| Capa | Componente | Responsabilidad |
|---|---|---|
| Controller | `Modules/Identity/Controllers/UsersController.cs` | DELETE `/api/v1/users/me`. |
| Application | `Modules/Identity/Application/DeleteOwnAccountService.cs` | Orquestacion e idempotencia. |
| Infrastructure | `MongoUserRepositories.cs`, `SupabaseSessionRevoker.cs` | Persistencia de Identity, revocacion local y logout best-effort. |
| Contratos | `IActiveOrderReader`, `IAccountDeletionCatalog`, `IAccountDeletionMedia` | Consultar pedidos, pausar productos y registrar purga de assets sin cruzar colecciones. |
| Media outbox | `MediaDeletionOutbox.cs`, `MongoMediaRepository.cs`, `S3MediaObjectStore.cs` | Eliminar staging/approved en S3 y marcar `mediaAssets` como `Deleted` de forma reintentable. |

## Dependencias y gates

| Gate | Tarea | Evidencia |
|---|---|---|
| DEC-003 y AC-007 | DEV-API005-01/02/03/04 | Decision aprobada; contratos minimos publicados. |
| Orders, Catalog y API-003 | DEV-API005-03/04 | Interfaces y pruebas aisladas. |
| Docker/Testcontainers | DEV-API005-04 | Integracion, seguridad y dispatcher de Media con dependencias aisladas. |

## Archivos

| Ruta | Cambio |
|---|---|
| `UsersController.cs` | Accion DELETE autenticada. |
| `DeleteOwnAccountService.cs` | Coordinacion de baja. |
| `MongoUserRepositories.cs` | Anonimizacion CAS. |
| `Modules/Media/Infrastructure/MediaDeletionOutbox.cs` | Outbox idempotente, lease, reintentos y purga de assets. |
| `tests/UtttGo.UnitTests/DeleteOwnAccountServiceTests.cs` | Orden, fallos e idempotencia. |
| `tests/UtttGo.IntegrationTests/` y `tests/UtttGo.SecurityTests/` | Evidencia con Mongo/Redis aislados cuando Docker este disponible. |

## Seguridad y errores

El cliente no decide identidad, estado ni referencias. El error usa el contrato unico con `correlationId`; no se registran PII, tokens ni secretos. Redis es fail-closed para la revocacion.

## Criterios de cierre

DEL-01..06 con pruebas de comprador, vendedor, pedidos activos, repeticion, outbox y fallos parciales; build, formato y las tres suites verdes. La revocacion global Supabase sigue best-effort sin persistir tokens; la evidencia S3 requiere doble/emulador no productivo.
