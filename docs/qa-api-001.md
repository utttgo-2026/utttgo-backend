# QA local de API-001 — registro

> 2026-11-04 · Alcance: `POST /api/v1/auth/register` únicamente. Spec v1.5 aprobada individualmente para desarrollo local; contrato final, TASK-AUTH-007/008 e integraciones externas pendientes.

## Evidencia reproducible

Desde la raíz del repositorio, ejecutar en orden:

```powershell
dotnet restore UTTTGO.Backend.slnx
dotnet build UTTTGO.Backend.slnx -c Release --no-restore
dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore
dotnet test UTTTGO.Backend.slnx -c Release --no-build --verbosity minimal
```

Última ejecución local: restore correcto (3/4 proyectos ya actualizados); build Release **0 errores y 0 advertencias**; format sin diferencias; test **16 unitarias, 28 de integración, 9 de seguridad**, **53 aprobadas, 0 fallidas, 0 omitidas**. SDK instalado `10.0.400-preview.0.26322.102` (NETSDK1057 informativo); no afirmar verificación con .NET 10 LTS estable. No se ejecutaron servicios reales ni pruebas sobre producción.

## Cobertura ejecutada

| Área | Evidencia local y límite |
|---|---|
| JSON y validación | `tests/UtttGo.IntegrationTests/RegisterEndpointTests.cs`: faltan/son inválidos contraseña, email o nombre; JSON malformado, tipos incorrectos, campos privilegiados `roles`, `accountStatus`, `isAdmin`; `400` genérico con `correlationId` sin datos sensibles. No hay política aprobada de longitud/fortaleza de contraseña, por lo que no se inventó una. |
| Dominio | Casos de dominio externo, subdominio, sufijos parecidos, `@` duplicada y formato inválido; unidad verifica coincidencia exacta con `@uttt.edu.mx`. |
| Antienumeración y secretos | `tests/UtttGo.SecurityTests/RegisterSecurityTests.cs` y pruebas de integración: mismo `202`, cuerpo y tipo de contenido para nuevo/existente **simulados**; respuesta no incluye correo, nombre, contraseña, OTP, JWT, `userId`, rol ni estado; `Location` ausente. **No** se evaluó resistencia temporal ni semántica real de duplicados en Supabase. |
| Fallos | Redis no configurado `503` (fail-closed); rate limit denegado `429`; preflight/persistencia fallidos, identidad Supabase ausente/ajena y excepciones inesperadas de proveedor/almacenamiento no devuelven `202`; cuerpos de error no exponen excepciones internas. Son dobles, no MongoDB/Redis/Supabase reales. |
| Tamaño | Cuerpos mayores a **8 KiB**, con y sin `Content-Length`, retornan `413` antes de registrar, con error/correlationId para el caso probado. Límite **candidato local**, no umbral NFR aprobado; `RequestSizeLimit` por sí solo devolvió `202` en TestServer, por lo que se añadió filtro de recurso con lectura acotada. Verificar también Kestrel/proxy reales antes de desplegar. |
| Reintentos/concurrencia | Unidad ejecuta 16 llamadas concurrentes con repositorio *fake* que deduplica por `sub`: solo un perfil en memoria. **No** demuestra atomicidad, unicidad, idempotencia ni recuperación ante fallos en MongoDB/Supabase reales. |

## Defectos corregidos y pendientes

- Excepciones no previstas de adaptadores podían filtrar detalles internos en entorno Development; `RegistrationService` ahora falla cerrado con `503` sin reflejar excepción (cancelación del cliente no se convierte en éxito).
- `[RequestSizeLimit]` no se aplicaba en `WebApplicationFactory`: prueba inicial `OversizedJsonIsRejectedBeforeRegistration` falló (`202` en vez de `413`). Filtro de recurso adicional limita la lectura **antes de model binding**, incluyendo cuerpo sin longitud declarada. Suite repetida verde.
- Pendientes reales: proveedor Supabase, SMTP/OTP institucional, persistencia/índices e idempotencia MongoDB, rate limit Redis distribuido, pruebas de logs de extremo a extremo, temporización/observación de red para antienumeración, límites de proxy/Kestrel, política aprobada de contraseñas y valoración del límite candidato de 8 KiB. No marcar TASK-AUTH-002/007/008/020/030 como completadas.
