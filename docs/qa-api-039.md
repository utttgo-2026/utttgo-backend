# QA API-039 — GET /api/v1/admin/users (candidato local)

> 2026-11-04 · SDD `specs/phase-02-auth-users/api-039-list-users/{spec,plan,task}.md` v0.2.0. **Sin adaptadores reales ni autorización de despliegue.**

## Evidencia ejecutada

| Comando | Resultado final |
|---|---|
| `dotnet restore UTTTGO.Backend.slnx` | Correcto. |
| `dotnet build UTTTGO.Backend.slnx -c Release --no-restore` | Correcto, 0 errores, 0 advertencias. SDK instalado .NET 10 preview (`NETSDK1057` informativo). |
| `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore` | Correcto, sin diferencias. |
| `dotnet test UTTTGO.Backend.slnx -c Release --no-build` | **120 unitarias + 140 integración + 60 seguridad = 320 aprobadas, 0 fallidas, 0 omitidas.** |

Anteriormente se reportaron **99 + 122 + 51 = 272**. API-039 agrega **21 unitarias + 18 integración + 9 seguridad = 48**; el resto permaneció verde. En un primer intento hubo un `using` faltante (build) y dos expectativas erróneas en tests nuevos (página ausente y desempate); se corrigieron y se repitieron los cuatro gates hasta obtener el resultado final indicado. No se deshabilitaron pruebas existentes.

## Alcance demostrado con dobles

- `AdminUsersTests`: consulta pura de selección con perfiles **de prueba** de fecha explícita, filtro por `displayName` normalizado/rol/estado, conteo antes de paginar, orden `createdAt` y desempate por ID; validador 1..50/overflow/enum/longitud/configuración; autorización Active/Admin local y fallos de lector, rate limit y auditoría.
- `AdminUsersEndpointTests` (`WebApplicationFactory`): `200` exacto con cuatro campos por elemento y metadatos, filtros/orden, errores `400`, host sin adaptadores `503`, rate limit `429` y dependencias/auditoría fallidas `503` sin listado.
- `AdminUsersSecurityTests` (`WebApplicationFactory`): token faltante, perfil no Admin o no Active, sesión inválida/indisponible y auditoría fallida; ninguna lectura para sujetos no autorizados, sin email/tokens/excepciones en respuesta. Las pruebas de sesión preexistentes cubren gates JWT/Redis **simulados**.

## Límites y bloqueantes de integración

`LocalLoginProfile` usa `AccountStatus` como `string`; `PendingEmailVerification` está en registro y `Suspended` en RF-08/pruebas, **no** se confirmó modelo Mongo persistido para `Suspended`. Tampoco hay `createdAt` en el perfil local actual: `AdminUserListEntry.CreatedAt` es **obligatorio y UTC en el contrato de lectura**, provisto por fixtures únicamente en tests, nunca inferido del ID ni del reloj en producción. La comprobación contra el esquema/datos reales y los índices queda en TASK-039-001/020. El servicio de lista del host normal no puede entregar `200`: JWT/Supabase/Redis, lector Identity, rate limit y auditoría tienen registros fail-closed sin adaptadores reales. El algoritmo de selección probado no constituye un adaptador Mongo, y `429` no prueba rate limiting distribuido. `total` no garantiza snapshot entre peticiones.

No se ejecutaron MongoDB/Redis/Supabase reales, no se verificó auditoría externa, privacidad en logs distribuidos ni despliegue/costo cero. API-003 y API-040 siguen pendientes y **AC-005 continúa abierto**; aprobación integral del equipo pendiente.
