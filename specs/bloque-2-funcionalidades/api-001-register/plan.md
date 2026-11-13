# API-001 — Registro · plan

> Lote 2a · **Pendiente de aprobación**; plan documental, sin autorización de cambios de producción.

Estado de aprobación de la ficha: PENDIENTE

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → este plan → [tareas](task.md). Mantener intactos [spec](../../phase-02-auth-users/spec.md), [plan](../../phase-02-auth-users/plan.md) y [task](../../phase-02-auth-users/task.md) de fase 02; no renumerar IDs.

1. **Contrato real:** cotejar `AuthController`, `RegisterRequest`, `RegisterRequestValidator` y `RegistrationRequestSizeFilter` en `src/UTTTGO.Api/Modules/Identity/`; conservar `202` genérico/no `409` como aprobación individual, describir `400/422/429/503` y `413` de 8 KiB como comportamiento candidato, no aprobación integral ni umbral RNF. Contrato de error con `correlationId` según constitución.
2. **Persistencia y límites:** rastrear `RegistrationService` → `RedisAuthRateLimitGate` (partición `auth-register` por IP; `RateLimiting.AuthRegister` 5/900 s provisional) → Supabase `signUp` → repositorio de `users`. Contrastar `Buyer/PendingEmailVerification`, unicidad por `sub`, ausencia de contraseña local, antienumeración de duplicados y `503` fail-closed sin atribuir a dobles garantías de Atlas/Supabase; probar después fallos parciales y Redis compartido. No modificar middleware ni políticas en este lote.
3. **Verificación:** mapear RF-01/03/15 y RNF/AC/T-AUTH de [spec](spec.md#trazabilidad-y-escenarios-de-aceptación) contra `docs/qa-api-001.md` y sus tres suites; separar los dos resultados reales locales de 2026-11-06 de la mera presencia en OpenAPI AWS de 2026-11-12. No ejecutar curl ni declarar registro AWS verificado.
4. **Gates abiertos:** TASK-AUTH-002 (OTP/SMTP/estado y móvil), 007/008 (aprobaciones), 010/011/012/020/030 (contrato, integraciones, seguridad y pruebas); RF-06 de recuperación se conserva fuera de API-001. Aprobar ficha/HTTP restantes y comprobar OTP/reconciliación sin crear endpoint nuevo implícito. El lote no mueve/archiva fase 02 ni llama al clerk; su trazabilidad se concilia al cierre del lote.

## Secuencia de implementación condicionada

Primero ejecutar únicamente [DEV-API001-01..04](task.md#tareas-ejecutables-sin-cambiar-contrato); son verificaciones sin cambio de contrato. Antes de tocar producción/tests, acreditar [TASK-AUTH-007/008/010/020](../../phase-02-auth-users/task.md) y resolver [DF02-01/02/09/10](../../../docs/decisiones-pendientes-fase-02.md) según el cambio. El cierre documental 001/006 no habilita programación. Si hay decisión nueva, aprobar esta ficha antes de cambiar código.

| Orden posterior a gates | Brecha; componente a intervenir solo tras autorización | Criterio técnico |
|---|---|---|
| 1 · contrato | REG-01..07: `AuthController.Register`, `RegisterRequest`, `RegisterRequestValidator`, `RegistrationRequestSizeFilter`, `ApiError` | Ratificar JSON/HTTP/validaciones/8 KiB y `Retry-After`; mantener `202` genérico sin `409`, error con `correlationId`. |
| 2 · datos | REG-07/09: `RegistrationService`, `ISupabaseRegistration`, `SupabaseAuthClient`, `IUserRegistrationRepository`, `MongoUserRepositories` (`users` y unicidad de `supabaseUserId`) | Diseñar reparación/idempotencia y probar duplicado, fallo tras `signUp`, reintento y concurrencia real; nunca persistir contraseña ni crear perfil desde JWT arbitrario. |
| 3 · límites | REG-05/06: filtro de tamaño, `RedisAuthRateLimitGate`, `RateLimiting.AuthRegister`, proxy confiable | Probar Kestrel/proxy, IP efectiva y cuota Redis entre 2 réplicas; 8 KiB y 5/900 s son candidatos, Redis auth fail-closed. |
| 4 · función externa | REG-08: OTP/SMTP, Supabase Confirm Email/móvil y guard privado **en sus propias fichas** | OTP correcto/incorrecto/vencido/reenvío, no acceso privado y reconciliación; sin añadir ruta ni absorber RF-06 recuperación. |

Pruebas de referencia: [`RegistrationServiceTests`](../../../tests/UtttGo.UnitTests/RegistrationServiceTests.cs), [`RegisterEndpointTests`](../../../tests/UtttGo.IntegrationTests/RegisterEndpointTests.cs), [`RegisterSecurityTests`](../../../tests/UtttGo.SecurityTests/RegisterSecurityTests.cs). [QA histórico](../../../docs/qa-api-001.md) usa dobles. Reutilizar [contratos Bloque 1](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md), sin duplicarlos. Integración local autorizada, nunca servicios productivos en tests; despliegue exige aprobaciones propias. Véase [matriz de brechas](spec.md#escenarios-locales-y-brechas-ejecutables).
