# QA local de API-006 — perfil propio

> 2026-11-04 · `GET /api/v1/users/me` únicamente; candidato local autorizado bajo spec fase 02 v1.5. No se implementó API-007 ni otra ruta.

## Reproducción

Desde la raíz del repositorio:

```powershell
dotnet restore UTTTGO.Backend.slnx
dotnet build UTTTGO.Backend.slnx -c Release --no-restore
dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore
dotnet test UTTTGO.Backend.slnx -c Release --no-build --verbosity minimal
```

Ejecución local: restore correcto; Release **0 errores, 0 advertencias**; formato sin diferencias; **56 unitarias, 70 integración WebApplicationFactory, 32 seguridad = 158 aprobadas, 0 fallidas, 0 omitidas**. API-006 agregó **7 unitarias, 11 integración y 11 seguridad**. Las **129 anteriores** continúan verdes. SDK `10.0.400-preview.0.26322.102` (no afirmar pruebas con .NET 10 LTS estable).

## Cobertura y decisiones candidatas

- `200` con dobles: reutiliza `SessionService.AuthenticateAsync` de API-004 (lector de `sub` validado, gate JWT/Redis compartido, confirmación actual y perfil local `Active` consistente). Respuesta limitada exactamente a `id,displayName,email,phone,avatarUrl,roles` del perfil validado. API-004 conserva su DTO de sesión con `accountStatus` y sin `phone`/`avatarUrl`.
- `401 UNAUTHORIZED` genérico para Bearer ausente/malformado, token inválido/expirado simulado, revocado simulado, correo sin confirmar, estado pendiente/suspendido, identidad ajena y **perfil faltante**. Este último es una **decisión provisional específica de API-006**, distinta del `404` propuesto en la ficha Excel y del `404` candidato de API-004. Revisar/ratificar semántica y antienumeración antes de cerrar contrato; el Excel no se modificó. Los parámetros `id`/`userId` del query se ignoran para decidir el perfil.
- `503` sanitizado para falla de una dependencia crítica de autorización o adaptadores no configurados. Sin integraciones externas, Bearer bien formado no produce `200`.
- Avatar: solo se consulta a `IApprovedAvatarReadAccess` con `AvatarMediaAssetId` y propietario del perfil **después** de autorizar. Su contrato exige comprobar activo propio `Approved` y producir URL privada temporal desde Media. Si no hay activo, no está aprobado o falta/falla Media, `avatarUrl` es `null`; nunca se interpreta como aprobación un ID o URL almacenada ni se genera URL pública. La URL del caso positivo existe únicamente en dobles de prueba: aprobación/propiedad/SAS reales **no verificadas**.
- Se comprueba JSON exacto, separación de API-004, ausencia de JWT, refresh token, contraseña, estado interno y campos Mongo; tests en `OwnProfileServiceTests.cs`, `OwnProfileEndpointTests.cs` y `OwnProfileSecurityTests.cs`. No se alteró el orden de middleware ni se configuraron tokens simulados en producción.

## Pendiente antes de uso real

Adaptadores reales para firma/issuer/audience/exp de JWT, blacklist Redis y vínculo token/`sub`; Supabase para confirmación actual; MongoDB para perfil (teléfono, roles, estado y referencia a media); integración Media para aprobación, propiedad y URL SAS de lectura corta. Faltan pruebas contra servicios locales reales, validación de política de avatar de API-007 y ratificación del `401` provisional de perfil faltante. TASK-AUTH-023/004 y dependencias continúan abiertos; las pruebas con dobles no acreditan seguridad productiva.
