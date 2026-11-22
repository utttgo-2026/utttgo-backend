# API-005 - Eliminar cuenta propia - plan

Estado de aprobacion de la ficha: APROBADA por el propietario, 2026-11-22.

## Autoridad y alcance

Constitucion -> spec.md -> este plan -> task.md. Identity coordina la baja y solo modifica `users`; los modulos dueños exponen contratos para sus propios datos.

## Arquitectura existente

`UsersController` reutiliza el guard de Identity. `users` pertenece a Identity; Catalog y Orders se consumen mediante `IAccountDeletionCatalog` e `IActiveOrderReader`. No se accede a colecciones ajenas desde Identity.

## Secuencia tecnica aprobada

Validar sujeto con el gate existente -> consultar pedidos activos -> anonimizar `users` con CAS -> pausar productos mediante Catalog -> revocar la sesion actual en Redis -> solicitar logout global de Supabase best-effort -> responder `204`. `deletedUserRef` es un UUID aleatorio estable. Mongo o Redis fallidos devuelven `503` fail-closed.

## Componentes

| Capa | Componente | Responsabilidad |
|---|---|---|
| Controller | `Modules/Identity/Controllers/UsersController.cs` | DELETE `/api/v1/users/me`. |
| Application | `Modules/Identity/Application/DeleteOwnAccountService.cs` | Orquestacion e idempotencia. |
| Infrastructure | `MongoUserRepositories.cs`, `SupabaseAuthClient.cs` | Persistencia de Identity y logout best-effort. |
| Contratos | `IActiveOrderReader`, `IAccountDeletionCatalog` | Consultar pedidos y pausar productos sin cruzar colecciones. |

## Dependencias y gates

| Gate | Tarea | Evidencia |
|---|---|---|
| DEC-003 y AC-007 | DEV-API005-01/02/03/04 | Decision aprobada; contratos minimos publicados. |
| Orders, Catalog y API-003 | DEV-API005-03/04 | Interfaces y pruebas aisladas. |
| Docker/Testcontainers | DEV-API005-04 | Integracion y seguridad completas pendientes. |

## Archivos

| Ruta | Cambio |
|---|---|
| `UsersController.cs` | Accion DELETE autenticada. |
| `DeleteOwnAccountService.cs` | Coordinacion de baja. |
| `MongoUserRepositories.cs` | Anonimizacion CAS. |
| `tests/UtttGo.UnitTests/DeleteOwnAccountServiceTests.cs` | Orden, fallos e idempotencia. |
| `tests/UtttGo.IntegrationTests/` y `tests/UtttGo.SecurityTests/` | Evidencia con Mongo/Redis aislados cuando Docker este disponible. |

## Seguridad y errores

El cliente no decide identidad, estado ni referencias. El error usa el contrato unico con `correlationId`; no se registran PII, tokens ni secretos. Redis es fail-closed para la revocacion.

## Criterios de cierre

DEL-01..04 con pruebas de comprador, vendedor, pedidos activos, repeticion y fallos parciales; build, formato y las tres suites verdes. La evidencia de contenedores sigue pendiente.
