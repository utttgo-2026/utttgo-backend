# QA local de API-002 — login

> 2026-11-04 · Solo `POST /api/v1/auth/login`. Implementación local incremental bajo spec fase 02 v1.5 aprobada individualmente; contrato definitivo e integraciones reales pendientes. API-001 no fue modificada salvo la inyección de dependencias de login en su mismo Controller.

## Evidencia reproducible

Desde la raíz del repositorio, ejecutar en orden:

```powershell
dotnet restore UTTTGO.Backend.slnx
dotnet build UTTTGO.Backend.slnx -c Release --no-restore
dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore
dotnet test UTTTGO.Backend.slnx -c Release --no-build --verbosity minimal
```

Resultado final observado: restore actualizado; build Release **0 errores y 0 advertencias**; format sin diferencias; **36 unitarias, 47 integración WebApplicationFactory, 13 seguridad: 96 aprobadas, 0 fallidas, 0 omitidas**. De ellas, API-002 añade **20 unitarias, 19 de integración y 4 de seguridad**; las 53 de API-001 continúan verdes. Un error inicial de compilación CS8382 en un test (igualdad de tuplas dentro de expresión FluentAssertions) se corrigió antes del resultado final. SDK local `10.0.400-preview.0.26322.102`: no afirmar verificación con SDK .NET 10 LTS estable.

## Contrato candidato y cobertura

- `200` únicamente cuando el doble del servidor Supabase entrega autenticación y confirmación actual, `sub`/email coherentes, perfil local existente `Active` con roles válidos y el doble del gate de JWT/blacklist autoriza token y sujeto. Respuesta: `accessToken`, `expiresIn` del proveedor, `tokenType:Bearer`, `user:{id,roles,accountStatus}` del perfil; sin refresh token. Los tests usan **token de prueba no firmado**, nunca aceptado por adaptadores reales.
- `400` con `correlationId` para cuerpo malformado, campos ausentes, email no válido y campos privilegiados como `roles`/`accountStatus`; contraseña no se valida mediante algoritmo propio ni se persiste.
- `401 INVALID_CREDENTIALS` genérico, sin token, para contraseña incorrecta, dominio no institucional, email no confirmado, perfil `PendingEmailVerification`/`Suspended`/`Deleted`, ausente o discordante, y rechazo del gate JWT. El `403 EMAIL_VERIFICATION_REQUIRED` propuesto **no se habilita**: no hay política antienumeración aprobada para diferenciar cuentas. Igualdad de código/mensaje de errores simulados no demuestra ausencia de canales temporales.
- `429` ante denegación del gate de rate limit. `503` genérico y sin tokens si rate limiter/Redis, proveedor, perfil o gate JWT están ausentes/caídos o lanzan errores; la aplicación sin configuración externa siempre falla cerrada. Una prueba captura logs de un login simulado y comprueba que no contienen contraseña ni token de prueba.
- Pruebas en `tests/UtttGo.UnitTests/LoginServiceTests.cs`, `tests/UtttGo.IntegrationTests/LoginEndpointTests.cs` y `tests/UtttGo.SecurityTests/LoginSecurityTests.cs`. Dobles únicamente en proyectos tests, sin secretos ni servicios externos.

## Riesgos y próximos gates

- No hay adaptadores reales Supabase Auth, MongoDB ni Redis, ni verificación criptográfica/issuer/audience/exp/jti o blacklist distribuida ejecutada; `ILoginJwtGate` exige ese contrato pero está **fail-closed** fuera de tests. No se acredita autenticación segura real ni pruebas distribuidas.
- No se implementó reconciliación `PendingEmailVerification` → `Active` tras OTP. Un perfil pendiente sigue denegado incluso si Supabase confirma el correo; aprobación y pruebas de promoción atómica/idempotente con Mongo siguen pendientes. No se crean perfiles desde JWT.
- Refresh token, renovación/revocación, comportamiento de sesiones del SDK, SMTP institucional y costo cero siguen abiertos; nunca se emite refresh token desde este candidato. TASK-AUTH-002/003/007/008/011/012/021 siguen `[ ]`; no desplegar ni declarar listo para producción.
