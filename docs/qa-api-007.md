# QA local de API-007 — editar perfil propio

> 2026-11-04 · Solo `PATCH /api/v1/users/me`; contrato candidato local bajo spec fase 02 v1.5, sin integración real ni aprobación definitiva del DTO de avatar.

## Reproducir

Desde la raíz del repositorio:

```powershell
dotnet restore UTTTGO.Backend.slnx
dotnet build UTTTGO.Backend.slnx -c Release --no-restore
dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore
dotnet test UTTTGO.Backend.slnx -c Release --no-build --verbosity minimal
```

Ejecución final local tras corrección puntual: restore correcto; build Release **0 errores, 0 advertencias**; format sin diferencias; **76 unitarias + 106 integración WebApplicationFactory + 44 seguridad = 226 aprobadas, 0 fallidas, 0 omitidas**. API-007 cuenta con **20 unitarias, 36 integración y 12 seguridad**; las **158 anteriores** continúan verdes. Un error de formato en la primera pasada de esta corrección se subsanó antes de la verificación final. SDK `10.0.400-preview.0.26322.102` (preview, no verificar .NET 10 LTS estable).

## Comportamiento candidato y pruebas

- PATCH JSON admite **solo** `displayName` y `phone` (casing exacto); rechaza propiedades adicionales o repetidas, incluso `email`, `roles`, `accountStatus`, `sellerStatus`, `userId`, `emailVerified`, `avatarUrl` y `mediaAssetId`. El JSON malformado, `{}`, valores de tipo incorrecto y `null` explícito para ambos campos producen `400` sanitizado. Se preserva cada campo omitido; no se definió eliminación por `null`. Nombre se recorta y se valida longitud **1–100**; teléfono exige **10–15 dígitos con `+` inicial opcional**. Estas reglas son **candidatas locales**, no límites oficiales aprobados.
- Reutiliza `SessionService.AuthenticateAsync` de API-004/006 y parser Bearer: el sujeto procede del JWT validado *por contrato*, nunca del body/query. Rechaza JWT sin sujeto, revocado, correo no confirmado, perfil pendiente/suspendido/ajeno o faltante con `401` genérico. La operación nunca llama a persistencia sin sesión habilitada.
- `IProfileUpdateRepository` requiere actualización condicional **atómica** por `sub`, ID local, versión esperada y estado `Active`, modificando exclusivamente campos admitidos e incrementando versión. El servicio verifica que la respuesta no altere identidad, roles, email, estado ni referencia de avatar. Conflicto concurrente ⇒ `409` sin confirmar escritura; perfil desaparecido ⇒ `401`; error de persistencia/identidad ⇒ `503`. La implementación por defecto **no está configurada** y falla cerrada; solo dobles de prueba simulan escritura, versión y conflictos. No se ha demostrado atomicidad MongoDB real.
- Respuesta `200` usa los mismos seis campos y la **misma proyección segura de avatar** que API-006. El identificador de avatar existente se conserva: sin adaptador Media de lectura, `avatarUrl` es `null` tanto en GET como en PATCH; el doble de Media solo devuelve URL temporal si confirma ID propio aprobado. No se crea, elimina, asocia ni publica un nuevo activo. Sin contrato Media de escritura aprobado, `avatarUrl` de la ficha y `mediaAssetId` alternativo se rechazan con `400`: **no se modifica el Excel ni se adopta silenciosamente un nuevo DTO**.
- **Corrección de concurrencia y relectura:** `ConcurrentOwnProfileUpdateTests` lanza dos llamadas simultáneas al mismo servicio, sincroniza ambas lecturas de versión 4 antes de las escrituras y usa un almacén compartido con bloqueo y CAS atómico: exactamente un éxito, un conflicto, una sola escritura, versión final 5, nombre final del ganador y teléfono/avatar preservados. `UpdateOwnProfileEndpointTests` usa otro almacén de pruebas compartido para PATCH seguido de GET real vía WebApplicationFactory; comprueba valor persistido en memoria, versión +1, referencia de avatar preservada y URL igual en GET/PATCH con y sin Media simulado. Son **dobles locales**: no prueban atomicidad MongoDB ni concurrencia entre procesos.
- **Casos HTTP añadidos:** variantes `DisplayName`/`Phone`/`displayNAME`, propiedad desconocida junto a campo válido, `null` explícito junto a campo válido, tipo numérico/arreglo y límites candidatos exactos de nombre (1, 100, 101) y teléfono (9, 10, 15, 16). Comprueban también que entradas rechazadas no llegan al repositorio falso.
- Tests: `tests/UtttGo.UnitTests/UpdateOwnProfileServiceTests.cs`, `tests/UtttGo.IntegrationTests/UpdateOwnProfileEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateOwnProfileSecurityTests.cs`. Dobles exclusivamente en tests; no se alteró el pipeline de middleware.

## Riesgos y decisiones pendientes

JWT criptográfico (firma/issuer/audience/exp), revocación Redis, verificación Supabase, perfil MongoDB, actualización CAS real y URL SAS de Media para avatar aprobado **no están integrados**. Aprobar límites/semántica `null` de nombre y teléfono, conflictos de escritura, `401` de perfil faltante frente a la ficha, y contrato de escritura/eliminación de avatar (propiedad, moderación, SAS) antes de cerrar TASK-AUTH-024/004. La sincronización CAS probada funciona **solo dentro del proceso de prueba**; falta diseñar y verificar la actualización Mongo por `sub`/ID/`Version`/`Active` y la lectura consistente tras ella. Un `200` con dobles **no acredita** persistencia ni seguridad productiva.
