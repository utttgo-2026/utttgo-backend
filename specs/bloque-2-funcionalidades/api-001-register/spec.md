# API-001 — Registro · spec

> Lote 2a · 2026-11-12 · **Pendiente de aprobación**. La autorización individual histórica para desarrollo local y el `202` genérico no aprueban el contrato completo ni esta ficha.

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED (alcance técnico pendiente; hay código candidato, sin verificación integral).

Autoridad: [constitución vigente](../../../docs/constitution.md) §§ 3, 6, 14 → esta ficha → [plan](plan.md) → [tareas](task.md). Origen conservado: [fase 02](../../phase-02-auth-users/spec.md) §§ 4–5, 9 y [tareas de fase 02](../../phase-02-auth-users/task.md); la ficha API-001 del Excel sigue «Propuesta a validar».

## Alcance y contrato vigente (código, no ratificación)

| Contrato vigente | Comportamiento y fuente real |
|---|---|
| Ruta/método | `POST /api/v1/auth/register` ([`AuthController`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs)). |
| Autenticación | Público ([`AuthController`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs)). |
| Entrada (campos y validaciones) | JSON camelCase `{email, password, displayName}` ([DTO](../../../src/UTTTGO.Api/Modules/Identity/Contracts/RegisterRequest.cs)); el [validador](../../../src/UTTTGO.Api/Modules/Identity/Validators/RegisterRequestValidator.cs) exige los tres campos, email bien formado y ningún campo extra (incluidos `roles`/`accountStatus`); no fija longitud ni fortaleza de contraseña. El [controlador](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs) normaliza email con `Trim`/minúsculas y nombre con `Trim`; solo admite dominio **exacto** `@uttt.edu.mx` (sin subdominios, ignorando mayúsculas). |
| Límites | [Filtro](../../../src/UTTTGO.Api/Modules/Identity/Controllers/RegistrationRequestSizeFilter.cs) y [`RequestSizeLimit`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs): **8 KiB, candidato local, no RNF aprobado**; exceso → `413 REQUEST_TOO_LARGE`. [Gate Redis](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisIdentityGates.cs) `auth-register` por IP; [`RateLimiting.AuthRegister` en `appsettings.json`](../../../src/UTTTGO.Api/appsettings.json): 5 solicitudes/900 s por IP, **provisionalmente, por decisión del responsable, no aprobado**. |
| Salidas | [Controlador](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs): tabla de respuestas vigente a continuación; códigos distintos del `202`, texto exacto del DTO y tamaño siguen sujetos a ratificación. |
| Dependencias | [Servicio](../../../src/UTTTGO.Api/Modules/Identity/Application/RegistrationService.cs): primero [gate Redis](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisIdentityGates.cs), comprueba disponibilidad del [repositorio Mongo `users`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs), delega `signUp` a [Supabase](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/SupabaseAuthClient.cs) y, para identidad nueva con `sub`/email coincidentes, guarda perfil local `Buyer` pendiente de verificación ([contrato de perfil](../../../src/UTTTGO.Api/Modules/Identity/Application/IRegistrationDependencies.cs)); un duplicado procesable recibe el mismo `202` sin crear otro perfil. No persiste contraseña ni acepta rol del cliente; la sincronización sigue sujeta a ratificación. |

| Resultado actual | Condición / contenido público |
|---|---|
| `202 Accepted` | Solicitud válida y procesable, sea identidad nueva o ya existente: `{"message":"Si el registro puede procesarse, recibirás instrucciones para verificar tu correo."}`; sin identificador, rol, estado ni JWT. **Único éxito aprobado individualmente**, sin `409` por duplicado; no garantiza entrega de correo. |
| `400 VALIDATION_ERROR` | Body inválido, malformado o con campos extra; error con `correlationId`. |
| `422 INSTITUTIONAL_EMAIL_REQUIRED` | Correo bien formado de dominio no institucional. |
| `429 RATE_LIMITED` | Redis deniega cuota; `Retry-After` en segundos cuando el limitador calcula el reintento. |
| `503 AUTH_UNAVAILABLE` | Dependencia crítica/identidad inconsistente o Redis no disponible: fail-closed, sin confirmar registro. |

## Trazabilidad y escenarios de aceptación

Registro: **RF-01/03/15**; **RNF-02/03/04/05/07/10/11/15/17** (RNF-17 es propuesta OTP); **AC-001/002/003/008/010/011/012**. [Fase 02 § 4.2](../../phase-02-auth-users/spec.md#42-contratos-individuales-prioritarios-alcance-y-procedencia): Figma `HU-004/UI-004`, backlog `PB-002/HU-02` y verificación `PB-003/HU-03` (fuentes con numeraciones distintas). TASK-AUTH-001/006 `[x]` documentales; **002/007/008/010/011/012/020/030 `[ ]`**: el traslado no las cierra. Pruebas correspondientes: **T-AUTH-01/03/05/14/15/18/19**.

El desglose Given/When/Then por escenario, cobertura y autorización está en [REG-01..09](#escenarios-locales-y-brechas-ejecutables); los resultados HTTP actuales de la tabla anterior son candidatos salvo el `202` parcial.

## Evidencia separada y pendientes

- **Con dobles** ([QA API-001](../../../docs/qa-api-001.md)): 53 pruebas informadas (16 unitarias, 28 integración, 9 seguridad): `RegistrationServiceTests`, `RegisterEndpointTests`, `RegisterSecurityTests`; validación, antienumeración simulada, `413`, `429` y `503`. No prueban proveedores reales ni resistencia temporal.
- **Real local (2026-11-06; Atlas + Redis + Supabase reales)**: registro de prueba con correo institucional → `202`; correo fuera del dominio (gmail) → `422 INSTITUTIONAL_EMAIL_REQUIRED`. Evidencia comunicada para estos dos casos, **no reejecutada aquí**; no demuestra OTP, entrega de correo, duplicados reales ni recuperación.
- **AWS (2026-11-12)**: `POST /api/v1/auth/register` figura en el OpenAPI del despliegue. **Ruta presente ≠ «Verificado»**: no hay curl de registro en AWS en esta evidencia.

**Pendiente:** RF-15, pruebas del flujo OTP y reconciliación de perfil; RF-06/BR-013, recuperación por enlace al correo registrado (sin inventar ruta backend). [Guía móvil incorporada en `origin/main` (2026-11-13)](https://github.com/utttgo-2026/utttgo-backend/blob/b4d81b8/docs/auth-mobile-integration.md) **reporta** SMTP Gmail configurado en Supabase y plantilla Confirm sign up `{{ .Token }}`; no aporta en este corte prueba reproducible de OTP erróneo/vencido/reenvío, entrega a correo institucional, cuotas ni costo cero. El [TASK-AUTH-002 histórico](../../phase-02-auth-users/task.md) conserva otros criterios abiertos y no se cierra aquí. Aprobar contrato restante y acreditar flujo móvil/entrega y pruebas reales antes de cerrar tareas o AC; conservar fase 02 sin archivar.

## Niveles de autoridad y responsabilidades

**A — aprobado en su alcance:** AF02-01/03/04/05 obligan a correo/contraseña Supabase, dominio exacto, Buyer exclusivamente backend y perfil pendiente; AF02-02/06/07/08 obligan a OTP, confirmación confiable antes de activar o acceder a rutas privadas (aun con JWT) y verificación/reenvío móvil sin endpoint nuevo. [DF02-02](../../../docs/decisiones-pendientes-fase-02.md) aprueba **individual y parcialmente** `202` genérico nuevo/existente procesable, sin identidad/rol/estado/JWT y sin `409` por duplicado; no aprueba JSON literal, errores ni el contrato integral. Reglas constitucionales de secretos, HTTPS, fail-closed y `correlationId` aplican ([contratos compartidos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md)).

**B — implementado candidato:** la tabla «Alcance y contrato vigente» enumera DTO/HTTP, obligatoriedad de `email`, `password`, `displayName` (sin opcionales admitidos), normalización, `413` de 8 KiB exclusivo de registro, cuota IP 5/900 s provisional, `Buyer/PendingEmailVerification` guardado por `sub`, respuesta literal y errores. El controlador es público sin JWT; los errores usan `ApiError`/`correlationId`. No hay política de fortaleza de contraseña ratificada. El orden es Redis → preflight Mongo → `signUp` Supabase → guardado Mongo si identidad nueva coherente; no persiste contraseña. **No** usar ese código para ratificar su contrato.

**C — requerido no implementado integralmente:** RF-15/AC-011 pruebas OTP/SMTP de entrega real, comprobación confiable y reconciliación Supabase–Mongo ante fallos y reintentos; la configuración SMTP/plantilla reportada no acredita estos resultados. Protección privada integral requiere su propio alcance. RF-06/BR-013 recuperación por enlace es otra funcionalidad; no agregar ruta aquí. Registro no inicia sesión ni emite JWT.

## Escenarios locales y brechas ejecutables

IDs `REG-01..09` no sustituyen AC/T-AUTH. Estado: **A parcial** = solo la regla aprobada; **candidato** = se prueba lo vigente sin consagrar HTTP/DTO; **bloqueado** = requiere decisión. Prueba citada = archivo escrito o QA histórico, **no ejecución actual**.

| ID · Dado / Cuando / Entonces | RF/AC; prueba o evidencia | Brecha específica y autorización |
|---|---|---|
| REG-01 · Supabase, Redis y Mongo disponibles; correo institucional nuevo válido / registrar / `202` genérico sin JWT y Buyer pendiente ligado al `sub` | RF-01/03, AC-001; `RegistrationServiceTests.NewAccountStoresPendingBuyer`, `RegisterEndpointTests.NewAndExistingAccountHaveSamePublicAcceptance` | Parcial: dobles y `202` local comunicado; persistencia real, JSON exacto y entrega no acreditados. A parcial; resto bloqueado DF02-02/09. |
| REG-02 · Cuenta existente procesable / registrar / mismo código/cuerpo público que REG-01, sin `409` ni segundo perfil | RF-03, AC-001; `ExistingAccountDoesNotCreateProfile`, `RegisterSecurityTests.NewAndExistingSameCodeBodyAndContentTypeNotProofOfTimingResistance` | Implementado con dobles; timing y duplicado Supabase real sin probar. A parcial; prueba real bloqueada por gate. |
| REG-03 · Dominio ajeno, parecido o subdominio / registrar / rechazo sin llamar Supabase (`422` vigente) | RF-01; `DomainMustMatchExactly`, `InvalidDomainDoesNotCallProvider`, `SimilarOrInvalidDomainsAreRejected` | Implementado con dobles y `422` local comunicado; HTTP candidato DF02-02. |
| REG-04 · JSON inválido, campo extra/rol/estado inyectado / registrar / error (`400` vigente), sin asignar rol | RNF-03/07, AC-010; `InvalidOrPrivilegedJsonReturnsSanitized400`, `PrivilegedJsonFieldsCannotBeAssigned` | Implementado con dobles; esquema/HTTP por ratificar DF02-02. |
| REG-05 · Body >8 KiB con o sin Content-Length / registrar / `413` antes del servicio | RNF-02; `OversizedJsonIsRejectedBeforeRegistration`, `OversizedChunkedJsonWithoutContentLengthIsRejected` | Implementado con dobles; umbral candidato, Kestrel/proxy real no acreditados. Bloqueado DF02-02 para normativa. |
| REG-06 · Redis deniega cuota / registrar / `429` y `Retry-After` cuando disponible sin llamar proveedor | RNF-02; `RateLimitDenialDoesNotCallSupabase` | Parcial: prueba con dobles; IP detrás de proxy y cuotas entre réplicas sin verificar; cuota provisional. |
| REG-07 · Redis, Mongo o Supabase falla o identidad incoherente / registrar / sin `202` ficticio (`503` vigente) | RNF-04/17; `RedisFailureCannotBeAccepted`, `ProviderFaultReturnsSanitized503`, `FailedMongoSaveDoesNotReturnAccepted` | Parcial: dobles; resultado HTTP por ratificar, fallo real y reparación de identidad huérfana ausentes. Bloqueado DF02-02/09. |
| REG-08 · Código OTP erróneo/vencido, o JWT de cuenta pendiente / intentar acceso privado / no activar ni autorizar | RF-15, AC-011; pruebas OTP/SMTP/guard pendientes | No implementado integralmente; esta ruta no verifica OTP. Bloqueado DF02-01/10 y ficha dependiente. |
| REG-09 · Supabase creó usuario y falla Mongo, o hay reintento concurrente / reintentar / no duplicar perfil ni habilitar huérfano | RF-03/15, T-AUTH-19; `ConcurrentRetryIsIdempotentOnlyWhenRepositoryEnforcesUniqueness` | Parcial con doble; reparación y concurrencia reales sin evidencia. Bloqueado DF02-09. |

## Decisiones bloqueantes

| ID y fuente | Alternativas documentadas / impacto concreto | Tareas afectadas y desbloqueo |
|---|---|---|
| [DF02-01/10](../../../docs/decisiones-pendientes-fase-02.md), TASK-AUTH-002 | OTP aprobado; [guía nueva de main](https://github.com/utttgo-2026/utttgo-backend/blob/b4d81b8/docs/auth-mobile-integration.md) reporta plantilla `{{ .Token }}` y SMTP Gmail configurados, pero no acredita pruebas/costo ni cierra el gate histórico. [Guía anterior](../../../docs/mobile-integration.md) § 5 conserva el ejemplo contradictorio de enlace; no sustituye OTP ni autoriza `signInWithOtp` passwordless. | REG-08, DEV-API001-07/11: ratificar detalles SDK/seguridad y aportar pruebas OTP/SMTP verificables. |
| DF02-02/07, TASK-AUTH-010; Excel | `201/status:Active/409` del Excel frente a `202` parcial aprobado; faltan JSON exacto, validaciones, `400/422/413/429/502/503`, reintentos y antienumeración temporal. | REG-01..07, DEV-API001-05/06: acta de ratificación por ficha del resto sin revocar `202`. |
| DF02-09, TASK-AUTH-011/012 | Fuentes/frecuencia de confirmación, idempotencia y reparación de cuenta Supabase sin perfil no resueltas; no crear perfil de JWT arbitrario. | REG-07..09, DEV-API001-10/08: aprobar diseño y prueba de fallos parciales. |
| DF02-08, TASK-AUTH-007/008 y [task histórico](../../phase-02-auth-users/task.md) | Aprobación individual de SemVer ≠ aprobación de equipo/delegación; 008 depende de 002..007 y 010 de 007/008. | Toda programación DEV-API001-05..11: cerrar cadena SDD formalmente. |

Evidencia histórica de § «Evidencia separada y pendientes» no prueba estado actual de la suite ni satisface estos gates.
