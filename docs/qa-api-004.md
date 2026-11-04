# QA local de API-004 — sesión autenticada

> 2026-11-04 · Solo `GET /api/v1/auth/me`; candidato local bajo spec fase 02 v1.5. Sin servicios externos ni JWT reales verificados.

## Reproducir

```powershell
dotnet restore UTTTGO.Backend.slnx
dotnet build UTTTGO.Backend.slnx -c Release --no-restore
dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore
dotnet test UTTTGO.Backend.slnx -c Release --no-build --verbosity minimal
```

Resultado observado: restore correcto; build Release **0 errores, 0 advertencias**; format sin diferencias; **49 unitarias, 59 integración con WebApplicationFactory, 21 seguridad = 129 aprobadas, 0 fallidas, 0 omitidas**. API-004 añade **13 unitarias, 12 integración y 8 seguridad**; las 96 de API-001/002 siguen verdes. SDK instalado `10.0.400-preview.0.26322.102` (preview, no LTS estable).

## Casos y contrato candidato

- `200`: Bearer bien formado, `sub` obtenido *solo* por el doble del lector de JWT validado, doble del gate compartido con API-002 acepta token/sub y revocación, proveedor confirma correo actualmente, perfil local `Active` consistente. JSON exactamente `id,email,displayName,roles,accountStatus` del perfil: **sin JWT, refresh token ni campos editables de API-006**.
- `401` genérico con `correlationId`: token ausente/malformado, lector sin sujeto (simula JWT inválido o expirado), gate rechaza (simula revocación), correo no confirmado, perfil pendiente/suspendido o identidad local ajena/inconsistente. Parámetros de consulta como `sub`/`id` nunca determinan identidad. Los dobles no prueban firma, issuer, audience, expiración ni revocación Redis reales.
- `404 PROFILE_NOT_FOUND` propuesto en la ficha cuando el perfil no existe después de validación de JWT/confirmación; revisar exposición y aprobación definitiva del contrato. `503` genérico ante excepción de lector/gate/Redis/Supabase/Mongo; sin adaptadores configurados, Bearer bien formado produce `503` fail-closed. Los errores no incluyen token ni detalles internos.
- Pruebas: `tests/UtttGo.UnitTests/SessionServiceTests.cs`, `tests/UtttGo.IntegrationTests/SessionEndpointTests.cs`, `tests/UtttGo.SecurityTests/SessionSecurityTests.cs`. Dobles exclusivamente en tests; no hay autenticación simulada en el registro de DI real. No se alteró el orden del middleware.

## Integraciones pendientes

1. Lector real de JWT Supabase que valide firma, issuer, audience, expiración y devuelva `sub` únicamente tras validación criptográfica; configurar llaves/rotación sin secretos en Git.
2. Adaptador real del `ILoginJwtGate` compartido con API-002 para vínculo token/`sub`, `jti`, blacklist Redis con fail-closed y rate limiting distribuido; actualmente no existe.
3. Consulta confiable a Supabase para confirmación actual del correo por `sub` y tratamiento de indisponibilidad.
4. Repositorio Mongo por `supabaseUserId` con `displayName`, email, roles y estado local vigentes; no hay consulta real ni invalidación de caché (no se cachea aquí).
5. Aprobación final de semántica `404` vs `401` para perfil ausente y prueba de antienumeración; promoción segura pendiente→activo tras OTP y pruebas con servicios locales reales. TASK-AUTH-022 y dependencias 021/012 continúan abiertos; no desplegar.
