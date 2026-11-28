# API-005 - Staging local aislado

Este entorno es exclusivamente local. No usa la EC2, Atlas, el proyecto de
Supabase ni el bucket S3 de producción.

## Componentes

- MongoDB 7 y Redis 7 se crean con Testcontainers y se destruyen al terminar la
  suite.
- LocalStack crea el bucket `utttgo-account-deletion-local` en un contenedor
  temporal.
- `EndToEndAuthDoubles` emite JWT HMAC efímeros con `sub`, `jti`, `iss`, `aud`,
  `exp` y `auth_time`.
- La validación de firma, expiración y blacklist `revoked:{jti}` ocurre contra
  la clave Redis del contenedor.
- El doble no llama Supabase ni valida credenciales reales. El logout global de
  Supabase sigue siendo un no-op controlado; la revocación local sí es real.
- `tools/UtttGo.LocalStaging` publica Kestrel en `0.0.0.0:5080`, reutiliza ese
  JWT local y crea un bucket LocalStack separado.

## Ejecutar

Requiere Docker Engine activo:

```powershell
dotnet build UTTTGO.Backend.slnx -c Release
dotnet test tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj -c Release --filter "Category=E2E&FullyQualifiedName~AccountDeletionJourneyTests"
```

Para probar desde Android, iniciar el host Kestrel en otra terminal:

```powershell
dotnet run --project tools/UtttGo.LocalStaging/UtttGo.LocalStaging.csproj -c Release
```

La URL del emulador es `http://10.0.2.2:5080`; un dispositivo físico debe usar
la IP LAN del equipo, por ejemplo `http://192.168.1.20:5080`. El proceso se
detiene con `Ctrl+C`; su apagado normal elimina MongoDB, Redis y LocalStack.
Si se interrumpe de forma brusca, listar únicamente sus contenedores con:

```powershell
docker ps -a --filter "label=utttgo.local-staging=true"
```

Después de confirmar que son los del staging local, eliminarlos con:

```powershell
```

Los contenedores y sus datos se eliminan en `DisposeAsync`. Si una ejecución se
interrumpe, revisar y limpiar los recursos de Testcontainers antes de repetirla.
No usar `docker compose` apuntando a Atlas, Redis de producción o variables del
servidor.

## Escenarios

`AccountDeletionJourneyTests` cubre:

1. `409 ACTIVE_ORDER_CONFLICT` sin pausar productos ni cambiar la cuenta.
2. `204`, anonimización, `Completed` y pausa de productos.
3. Revocación del JWT en Redis.
4. Consulta posterior con el mismo `operationId` y proof, sin repetir DELETE.
5. Recuperación de una operación `Processing` mediante el mismo servicio usado
   por el worker después de un reinicio.
6. Eliminación física del objeto LocalStack y `mediaPurgeStatus=Completed`.

Los headers se envían explícitamente en cada DELETE:

- `X-Account-Deletion-Id`: UUID de operación.
- `X-Account-Deletion-Proof`: secreto efímero de al menos 32 caracteres.

La consulta de estado usa únicamente el proof y no envía Bearer.

## Android

Usar `http://10.0.2.2:5080` en el emulador Android o la IP LAN del equipo en un
dispositivo físico. Registrar un usuario y obtener su JWT local:

```powershell
$base = "http://10.0.2.2:5080"
$email = "android-e2e-$([guid]::NewGuid().ToString('N'))@uttt.edu.mx"
$body = @{ email = $email; password = "Local-only-password"; displayName = "Android E2E" } | ConvertTo-Json
Invoke-RestMethod "$base/api/v1/auth/register" -Method Post -ContentType "application/json" -Body $body
$login = Invoke-RestMethod "$base/api/v1/auth/login" -Method Post -ContentType "application/json" -Body $body
$login.accessToken
```

El valor `accessToken` es un JWT firmado por el doble HMAC local. No usarlo en
producción ni confundirlo con un token emitido por Supabase. Para API-005,
generar un UUID para `X-Account-Deletion-Id` y un secreto efímero de al menos
32 caracteres para `X-Account-Deletion-Proof`.

No usar la URL `3.129.142.54.nip.io` para estas pruebas.

## Límites

- Estas pruebas demuestran integración local con MongoDB, Redis y LocalStack,
  no la firma/JWKS, cuotas o logout global de Supabase.
- Los JWT de prueba no son aceptados por producción porque usan issuer,
  audience y clave distintos.
- La prueba de reinicio ejecuta el recovery pass sobre una operación stale; el
  host Kestrel permite repetirlo con los mismos contenedores mientras el
  proceso se reinicia, pero la suite automatizada aún no mata y levanta el
  proceso físicamente.
