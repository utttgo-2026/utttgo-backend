# QA local de API-008 — perfil público de vendedor

> 2026-11-04 · Solo `GET /api/v1/sellers/{sellerId}`. Candidato local: acceso público propuesto, **no** aprobación del contrato final, ni implementación de Orders, Catalog o Reviews.

## Reproducir

Desde la raíz del repositorio:

```powershell
dotnet restore UTTTGO.Backend.slnx
dotnet build UTTTGO.Backend.slnx -c Release --no-restore
dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore
dotnet test UTTTGO.Backend.slnx -c Release --no-build --verbosity minimal
```

Resultado final observado: restore correcto, Release **0 errores y 0 advertencias**, format sin diferencias; **99 unitarias + 122 integración WebApplicationFactory + 51 seguridad = 272 aprobadas, 0 fallidas, 0 omitidas**. API-008 agregó **23 unitarias, 16 integración y 7 seguridad**; las **226 pruebas anteriores** siguen verdes. En la primera ejecución, dos casos de ratings inválidos fallaron por conversión de `InlineData` de xUnit de `int` a `decimal?`; se corrigieron datos de las pruebas y se repitieron build, format y suite completa con el resultado final indicado. SDK local `10.0.400-preview.0.26322.102` (no afirmar validación con .NET 10 LTS estable).

La comprobación adicional `dotnet test UTTTGO.Backend.slnx -c Release --no-build --verbosity minimal --filter 'FullyQualifiedName!~SellerProfile'` confirmó separadamente **76 unitarias + 106 integración + 44 seguridad = 226 previas aprobadas, 0 fallidas**. Es una ejecución filtrada de las mismas suites, **no** se suma al total de 272.

## Contrato y límites demostrados

- `sellerId`: se acepta **solo ObjectId de 24 caracteres hexadecimales** como regla candidata local (no contrato de ficha ratificado); se normalizan letras mayúsculas del ID a minúsculas. ID malformado que alcanza el controller ⇒ `400` con `correlationId`; una ruta que no coincide puede devolver `404` antes del controller. No se consulta almacenamiento por ID inválido.
- Perfil existente `Active` ⇒ `200 isActive:true`; `Inactive` ⇒ `200 isActive:false`, **no `404` por inactividad**. JSON exclusivamente `sellerId,displayName,ratingAverage,ratingCount,isActive`. La bandera `isActive:false` no prueba que Orders rechace pedidos nuevos; esa regla queda pendiente de implementación/verificación en Orders. Ausente, eliminado o suspendido ⇒ `404` candidato; estado desconocido o datos incoherentes ⇒ `503` fail-closed.
- Ratings vienen **solo** del contrato `ISellerRatingsReader` previsto para Reviews; sin datos/fallo de Reviews, ambos campos son `null`, nunca cero o media inventados. Un cero de conteo solo aparece si el doble de Reviews lo suministra explícitamente; datos inconsistentes se omiten. **Degradación segura**: no se esconde el perfil conocido si falla únicamente Reviews. No se incluyen productos ni se consulta Catalog: la pantalla deberá consultar API-011 cuando exista contrato aprobado. No se leen colecciones de otros módulos.
- Rate limiting público distribuido es contrato `IPublicSellerRateLimitGate`: denegación ⇒ `429`; fallo ⇒ `503` fail-closed. Repositorio Sellers ausente/fallo ⇒ `503`; **la DI por defecto no tiene rate limiter ni repositorio funcional**, así que la ruta pública no entrega éxito fuera de dobles. Sin necesidad de Bearer en el candidato y sin reflejar Authorization/query. No se devuelven email, teléfono, JWT ni campos internos.
- Pruebas: `tests/UtttGo.UnitTests/SellerProfileServiceTests.cs`, `tests/UtttGo.IntegrationTests/SellerProfileEndpointTests.cs` y `tests/UtttGo.SecurityTests/SellerProfileSecurityTests.cs`. Los dobles solo están en tests; no hay llamadas a MongoDB Atlas, Reviews, Catalog, Redis ni dependencias productivas.

## Pendiente de aprobación/integración

Acceso público/autenticado definitivo, formato de ID y códigos HTTP, contrato de Reviews (sin ratings vs cero verificable, invalidación/coherencia) y presentación de productos vía API-011 de Catalog; ninguno se ratifica por esta implementación. Diseñar almacenamiento Sellers y rate limit **distribuido** real con fallo cerrado antes de servir perfiles públicos. Validar la máquina de estados Seller/Users y que Orders rechace pedidos nuevos cuando el vendedor esté inactivo; esta lectura por sí sola no hace cumplir esa regla. TASK-AUTH-005/026 permanecen abiertos: tests con dobles no acreditan seguridad distribuida ni persistencia real.
