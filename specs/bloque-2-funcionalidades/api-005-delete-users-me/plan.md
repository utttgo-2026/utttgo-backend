# API-005 - Eliminar cuenta propia - plan

Estado de aprobacion de la ficha: APROBADA por el propietario, 2026-11-22.

## Autoridad y alcance

Constitucion -> spec.md -> este plan -> task.md. Identity coordina la baja y solo modifica `users` y `accountDeletionOperations`; los modulos dueños exponen contratos para sus propios datos.

## Arquitectura existente

`UsersController` reutiliza el guard de Identity. `users` y `accountDeletionOperations` pertenecen a Identity; Catalog y Orders se consumen mediante `IAccountDeletionCatalog` e `IActiveOrderReader`. No se accede a colecciones ajenas desde Identity.

## Secuencia tecnica aprobada

Validar JWT y `auth_time` reciente -> consultar pedidos activos -> crear `accountDeletionOperations` -> revocar Redis -> anonimizar `users` con CAS -> pausar productos -> persistir outbox de Media -> responder `204`; el logout global de Supabase es best-effort. El worker recupera operaciones no completadas tras reinicios. `deletedUserRef` es un UUID aleatorio estable. La purga S3 posterior usa reintentos con lease.

## Componentes

| Capa | Componente | Responsabilidad |
|---|---|---|
| Controller | `Modules/Identity/Controllers/UsersController.cs` | DELETE `/api/v1/users/me`. |
| Application | `Modules/Identity/Application/DeleteOwnAccountService.cs` | Orquestacion e idempotencia. |
| Infrastructure | `MongoUserRepositories.cs`, `SupabaseSessionRevoker.cs` | Persistencia de Identity, revocacion local y logout best-effort. |
| Contratos | `IActiveOrderReader`, `IAccountDeletionCatalog`, `IAccountDeletionMedia` | Consultar pedidos, pausar productos y registrar purga de assets sin cruzar colecciones. |
| Media outbox | `MediaDeletionOutbox.cs`, `MongoMediaRepository.cs`, `S3MediaObjectStore.cs` | Eliminar staging/approved en S3 y marcar `mediaAssets` como `Deleted` de forma reintentable. |
| Operation store | `MongoAccountDeletionOperationStore.cs` | Persistir estado, etapa y prueba hasheada para recuperar la baja. |
| Recovery | `DeleteOwnAccountService.cs` | Reanudar etapas pendientes sin reutilizar access tokens. |

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

El cliente no decide identidad, estado ni referencias. El error usa el contrato unico con `correlationId`; no se registran PII, tokens, pruebas ni secretos. Redis es fail-closed para la revocacion. La consulta de estado valida la prueba hasheada y no requiere el JWT revocado.

## Criterios de cierre

DEL-01..06 con pruebas de comprador, vendedor, pedidos activos, repeticion, outbox y fallos parciales; build, formato y las tres suites verdes. La revocacion global Supabase sigue best-effort sin persistir tokens; la evidencia S3 requiere doble/emulador no productivo. Las suites de integracion y seguridad deben ejecutarse con Docker Engine activo.
