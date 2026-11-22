# Fotografías de productos — contrato funcional fase 05

> Estado: **contrato funcional aprobado por el equipo (comunicación del 2026-11-19)**; seguridad, infraestructura y despliegue NO aprobados ni verificados. Su implementación permanece fail-closed sin moderador y bucket no productivo configurados.
> Autoridad: `docs/constitution.md` § 7 y BR-007 → fichas API-017/018 y API-012/013/014 → este borrador → `plan.md` → `task.md`. Ante conflicto prevalecen constitución y fichas aprobadas.

## Alcance y compatibilidad

> **Decisión posterior del equipo (2026-11-20): aprobado el límite público exacto de 4 000 000 bytes, inclusive**, para Media y Expo Go. El texto histórico siguiente describe cuándo se identificó el cambio, no un estado de aprobación pendiente. Umbrales Azure 0/0/0/0 continúan autorizados **solo** para pruebas controladas, nunca producción.

`PresignedUrlRequest.size` declara mínimo `1` y máximo `4_000_000` en el OpenAPI
generado y snapshot versionado; el validador y servicio repiten el límite. Expo
Go debe comprobarlo antes de solicitar intención; no hay compresión automática.
El emulador LocalStack **admitió PUT con MIME distinto al firmado**: no usarlo
como prueba de integridad/expiración de firma S3. HEAD/lectura/firma binaria de
Media impiden que tal objeto alcance `Approved`; verificar PUT alterada,
caducada y posible reemplazo de un objeto ya aprobado durante el TTL en AWS
controlado y definir mitigación de inmutabilidad antes de despliegue.

**Cambio de contrato Media/Expo Go identificado antes de implementarlo (corte controlado, 2026-11-20):** el contrato anterior `POST /api/v1/media/upload-url` admitía `size <= 5_242_880` (5 MiB) con validación HEAD equivalente; el límite aprobado para el flujo de moderación es **4 MB**. Se eligió de forma conservadora `<= 4_000_000 bytes` **de archivo binario** tanto en la intención (`400 VALIDATION_ERROR`) como en HEAD, lectura S3 y petición Rekognition; `4_000_001` se rechaza. Expo Go debe bloquear/avisar **antes de solicitar URL** y no seguir anunciando 5 MB; el cliente no comprime ni transforma imágenes sin decisión del equipo. PUT presignada por sí sola no impone tope: cotejar HEAD con tamaño declarado y comprobar límite real durante lectura antes de enviar bytes a Rekognition. La ficha original y el snapshot OpenAPI se concilian antes de fusionar; no activar política de producción por este cambio de contrato.

**Compatibilidad de assets preexistentes:** una intención `PendingUpload` creada con el máximo anterior y `size > 4_000_000` ya no se puede confirmar: HEAD devuelve `400 INVALID_OBJECT`, sin moderar ni publicar; el vendedor debe pedir **una nueva intención** con una foto válida. El backend no migra ni reduce imágenes automáticamente. La limpieza de objetos staging huérfanos y la aceptación de este cambio por Expo Go son decisiones operativas pendientes.

| Aspecto | Propuesta mínima | Decisión pendiente |
|---|---|---|
| Identificador persistente | `mediaAssetId` opaco en JSON; en Mongo `products` se guarda solo la referencia, no la URL ni los bytes. Media es dueño de `mediaAssets`. | Aprobar formato del ID, unicidad de uso y posible reutilización entre productos del mismo vendedor. |
| Publicación | Únicamente `Approved`, tras verificar en S3 objeto/tipo/tamaño y moderación **aprobada** por Amazon Rekognition; `Ready` no autoriza publicación. Fallo de proveedor = no publicar. | Aprobar umbral, política de reintentos y compensación si S3/Mongo/Rekognition fallan parcialmente. |
| Propiedad | Media registra `ownerUserId` del perfil local autenticado, nunca del body. Para POST/PATCH Catalog resuelve el vendedor autenticado y solicita a Media comprobación del asset `Approved` perteneciente al usuario dueño de ese vendedor; Catalog nunca consulta la colección `mediaAssets`. | Aprobar relación exacta `users._id` ↔ `sellers.ownerUserId` e identidad utilizada por la interfaz; confirmar tratamiento de cuentas/vendedores inactivos. |
| Lectura | `imageUrl: string|null` en POST/PATCH/GET/listado; Media entrega presigned GET de duración corta para objeto aprobado de S3 privado. Ausencia de imagen → `null`. No se almacena URL ni se construye `publicUrl` permanente. | Aprobar TTL, política de fallo de presigner para lecturas y si la URL se renueva en cada GET. |
| Expo Go | La aplicación conserva `mediaAssetId` para POST/PATCH y usa `imageUrl` solo hasta `expiresAt` o solicita un GET nuevo al vencer. PUT directo a URL firmada con los headers exactos indicados por backend; sin credenciales S3 en el cliente. | Ratificar si se expone también `imageUrlExpiresAt` y compatibilidad/migración del `imageUrl` de entrada ya anunciado por POST. |
| PATCH | Campo `mediaAssetId` omitido preserva imagen; ID válido asigna/reemplaza solo si Media confirma dueño y `Approved`; `null` explícito quita la referencia. CAS de producto por `sellerId` + `version` evita perder cambios concurrentes. | Aprobar eliminación, destino de asset reemplazado, HTTP/errores y política ante carreras de revocación de aprobación. |
| Rutas Media | API-017 `POST /api/v1/media/upload-url` y API-018 `POST /api/v1/media/{mediaAssetId}/complete`; por instrucción funcional posterior `POST /api/v1/media/confirm` acepta `{mediaAssetId}` y comparte lógica/controles con API-018. | Registrar ficha formal de la ruta de compatibilidad; `/presigned-url` permanece descartada. |

## Acuerdos funcionales comunicados por el equipo (2026-11-19)

Se aprueban `PendingUpload`, `PendingModeration`, `Approved`, `Rejected`, `ProcessingFailed`; solo `Approved` es publicable. Media persiste por usuario autenticado, verifica HEAD/tipo/tamaño, modera fail-closed, confirma idempotentemente y responde el estado real (incluido `PendingModeration`). Catalog recibe `mediaAssetId` opcional en POST; PATCH omisión conserva, ID agrega/reemplaza y `null` quita, siempre con propiedad/CAS. Las respuestas de creación/listado/detalle incluyen `imageUrl` y `imageUrlExpiresAt` nulos o presigned GET temporal. Se aprueba también `GET /api/v1/media/{mediaAssetId}` autorizado por propietario. **No hay acta de aprobación de seguridad/IAM/despliegue ni de umbrales del proveedor**; ningún fallback permite afirmar `Approved`. Por instrucción funcional posterior del equipo se ofrece además `POST /api/v1/media/confirm` (`{ "mediaAssetId": "..." }`) como compatibilidad con API-018 `POST /api/v1/media/{mediaAssetId}/complete` (sin body): ambas usan la misma autorización, rate limit, servicio y transición idempotente. Registrar la ruta adicional en el inventario formal sigue pendiente.

## Requisitos y escenarios verificables propuestos

- **IMG-01:** dado un usuario autenticado, solicitar intención crea asset propio `PendingUpload` persistido en Mongo; backend firma PUT de **un objeto staging** con tipo/tamaño acotados y TTL ≤ 5 min. Sin configuración real S3 o Redis/Mongo disponibles se falla cerrado; jamás devolver URL mock en la API.
 - **IMG-02:** completar una intención propia hace HEAD/cotejo de objeto y metadatos; Rekognition sin etiquetas sobre el umbral → CAS a `Approved`; inseguro → borrar objeto y registrar `Rejected`. Repetición segura e idempotente del mismo resultado sin aprobar otra vez ni permitir actor ajeno.
- **IMG-03:** POST de producto sin imagen conserva el comportamiento actual; con ID inexistente, ajeno, `PendingUpload` o `Rejected` no escribe producto; con ID propio `Approved`, guarda referencia después de validar vendedor y categoría.
- **IMG-04:** PATCH de producto ajeno no muta ni revela su existencia; PATCH propio añade/reemplaza/quita referencia con control de versión. Media ajena o no aprobada nunca aparece en proyección pública.
- **IMG-05:** respuestas de producto sin imagen contienen `imageUrl:null`; con imagen aprobada solo una presigned GET temporal del backend. Si no puede acreditarse aprobación, no se expone URL. Probar ausencia de URL pública y que caducidad no se persiste como dato de producto.

## Política provisional para prueba controlada (NO aprobada para producción)

**No activar en producción sin aprobación operativa.** Amazon Rekognition usa
`MODERATION_MIN_CONFIDENCE`, con valor por defecto `75`, solo entre `0` y `100`.
No escribir credenciales ni valores sensibles en `.env` versionado, variables de la
EC2 productiva, Terraform ni `appsettings`. Un fallo de configuración, proveedor o
respuesta produce `ProcessingFailed`; nunca se aprueba por degradación. Esta
política cubre las etiquetas visuales devueltas por Rekognition; no certifica que
la imagen sea comida, genuina, legal o apta para consumo.
(`AnalyzeImageOptions`, `ImageCategory`, `ImageCategoriesAnalysis`) y
[quickstart oficial de imagen](https://learn.microsoft.com/en-us/azure/ai-services/content-safety/quickstart-image)
(`POST {endpoint}/contentsafety/image:analyze?api-version=2024-09-01`).
El adaptador envía bytes base64 en `image.content`, las cuatro categorías de imagen,
`outputType: FourSeverityLevels` y clave de suscripción solo desde el backend. Según la
[escala oficial de daño](https://learn.microsoft.com/en-us/azure/ai-services/content-safety/concepts/harm-categories),
el **modelo de imagen** devuelve solo `0` (Safe), `2` (Low), `4` (Medium) o `6` (High);
**no** aplicar la escala de texto 0–7. La especificación no exige `severity` en cada
objeto, pero nuestra política sí la exige para las cuatro categorías: ausente, duplicada,
desconocida o no perteneciente a `{0,2,4,6}` = error de análisis, jamás aprobación.

| Categoría REST | Máximo **propuesto** para aprobar | Rechazar si | Motivo para fotos de comida |
|---|---:|---|---|
| `Hate` | `0` | `2`, `4` o `6` | No permitir símbolos o mensajes discriminatorios en publicaciones universitarias. |
| `SelfHarm` | `0` | `2`, `4` o `6` | No corresponde exhibir daño autoinfligido en un catálogo de alimentos. |
| `Sexual` | `0` | `2`, `4` o `6` | Mantener un catálogo apto para el campus; no se necesita contenido sugerente. |
| `Violence` | `0` | `2`, `4` o `6` | Permitir utensilios de cocina **cuando Azure los clasifique 0**; no admitir armas/escenas violentas (2+). Pueden surgir falsos positivos con cuchillos, carne cruda o salsas rojas: validar con corpus autorizado, sin aprobar manualmente vía API. |

Regla propuesta **AND inclusiva**: `Approved` solo si **las cuatro** severidades
están presentes y son `<=` a su máximo configurado; para esta propuesta eso equivale
a cuatro ceros. Una sola categoría válida `2/4/6` implica resultado inseguro;
no promediar severidades ni interpretar falta de categoría como `0`. No hay umbrales
instalados por defecto ni override para autores/vendedores.

### Transiciones observables y fallos (código existente; no cambia Catalog)

| Condición | Estado resultante / respuesta | Qué no ocurre |
|---|---|---|
| Intención creada, objeto aún sin verificar | `PendingUpload` | No se publica ni firma GET. |
| HEAD de objeto propio coincide en MIME/tamaño; CAS `PendingUpload → PendingModeration` | `PendingModeration` **durante** lectura S3/espera Azure. Otra lectura puede observarlo. | Ningún GET aprobado; una confirmación concurrente puede devolver el estado persistido. |
| Respuesta Azure HTTP exitosa, JSON válido, cuatro categorías distintas y severidades reconocidas, **todas <= máximos aprobados y explícitos**, CAS exitoso | `Approved` | No aprobar si falta política, respuesta o CAS; POST/PATCH Catalog no cambia aquí. |
| Análisis válido con al menos una severidad **> máximo**, borrado del objeto staging completado y CAS exitoso | `Rejected` | No conservar objeto inseguro ni firmar GET; repetir confirmación devuelve estado terminal sin llamar Azure. |
| Política ausente/inválida, fallo de lectura S3 **después del HEAD**, timeout/HTTP no 2xx de Azure, cuerpo inválido, severidad/categoría incompleta/desconocida, o borrado del objeto inseguro fallido | `ProcessingFailed` después del CAS desde `PendingModeration` (si se pudo persistir); nunca `Approved` | No confundir fallo técnico con `Rejected`; no firmar GET. |
| HEAD ausente/MIME/tamaño distinto **antes** del CAS | Permanece `PendingUpload`; `400 INVALID_OBJECT` | No llamar Azure. Error de acceso HEAD previo a CAS deja `PendingUpload` y respuesta `503`, sin aprobar. |
| Cancelación del request durante Azure o CAS no completado por competencia/fallo de almacenamiento | Puede **permanecer `PendingModeration`**, sin GET; una confirmación autenticada posterior a 2 minutos hace CAS a `ProcessingFailed` cuando hay `moderationStartedAt` | No afirmar que todas las interrupciones se convierten inmediatamente en `ProcessingFailed`; un legado sin fecha o un asset sin nueva confirmación necesita revisión operativa. |

**Corte de implementación para prueba (sin despliegue):** se valida en inicio `<=4_000_000` bytes
y coincidencia extensión/MIME; HEAD coteja tamaño y Content-Type; lectura S3
vuelve a verificar MIME/tamaño y limita bytes reales; firma binaria JPEG/PNG/WebP
debe coincidir antes de llamar Azure. Si tras HEAD se detecta formato/tamaño
inválido se borra staging y se marca `Rejected` por CAS; si falla el borrado,
`ProcessingFailed`. La firma binaria no sustituye decodificación profunda por
Azure ni acredita soporte real de WebP: incluirlo en prueba autorizada.
Solo el CAS `PendingUpload → PendingModeration` inicia el análisis; una
confirmación simultánea devuelve el estado actual sin iniciar otra evaluación.
Repetir `Rejected`, `Approved` o `ProcessingFailed` no relanza Azure ni cambia
estado. Una confirmación autenticada sobre `PendingModeration` con
`moderationStartedAt` anterior a 2 minutos marca `ProcessingFailed` por CAS
(sin aprobar ni reintentar). La primera petición aún activa puede terminar,
pero su CAS por versión ya no puede aprobar. Los pendientes sin nueva llamada
**no** se recuperan automáticamente; recuperación/apelación manual requiere
procedimiento futuro aprobado.

Errores HTTP 429/5xx, `HttpRequestException` y timeout Azure permiten hasta
**3 intentos en el mismo claim** (10 segundos máximo por intento; espera 1 s
y 2 s entre intentos); 4xx distinto de 429, JSON/análisis inválido no se
reintentan. Solo el resultado final válido puede aprobar/rechazar. El backend
registra **solo** cuatro severidades categóricas de una respuesta válida
en logs estructurados, sin clave S3, URL, bytes, identificadores personales
ni payload Azure. Consulta evidencia y pasos autorizados en
`docs/media-azure-controlled-validation.md`. No calcular tasas a partir de
dobles ni asumir proveedor/bucket de pruebas disponibles.

**Criterios de aceptación propuestos** (verificar en unitarias con HTTP simulado):

- **IMG-MOD-01:** los cuatro ceros pasan a `Approved`, solo tras HEAD y CAS;
  cualquier categoría `2/4/6` produce borrado y `Rejected` si el borrado termina;
  repetición de terminal no vuelve a llamar Azure ni a borrar.
- **IMG-MOD-02:** mientras el proveedor no devuelve resultado, el estado
  observable es `PendingModeration` y no existe GET presignado.
- **IMG-MOD-03:** 429/5xx/4xx, timeout, JSON inválido, categoría ausente/extra/
  duplicada, severidad ausente, no numérica, negativa, impar o `>6` dan
  `ProcessingFailed` sin URL GET ni borrado por «inseguro» no demostrado.
- **IMG-MOD-04:** sin umbrales completos y válidos no hay aprobación;
  expiración/cancelación del request y falla de persistencia requieren criterio
  de recuperación independiente antes de producción.
- **IMG-MOD-05:** calibración posterior **autorizada**: conjunto mínimo de fotos
  propias/licenciadas y consentidas de plato servido, bebida empaquetada, comida
  con cuchillo de cocina en uso, carne cruda/salsa roja y etiqueta legible;
  incluir variaciones de iluminación y fondo. Registrar para cada imagen
  (identificador no personal, consentimiento, severidades, decisión esperada de
  revisión humana y discrepancias/falsos positivos), **sin subir originales,
  tokens ni contenido dañino a Git**. Los casos técnicos de rechazo se
  representan mediante **JSON ficticio**, no fotografías sensibles. La clasificación
  real y los falsos negativos no están validados por estas pruebas.

**Decisión requerida antes de producción:** aprobar o ajustar los **cuatro
máximos 0/0/0/0** según evidencia realmente observada y quién revisa falsos
positivos; ratificar recuperación de `ProcessingFailed`, apelación y criterio de
muestra autorizada. Solo entonces actualizar el estado SDD; no habilitar
producción por este cambio.

## Brecha conocida y gates de aprobación

El código candidato ya sustituyó el repositorio en memoria y las URL simuladas: `mediaAssets` en Mongo, PUT/GET firmados por S3, HEAD y CAS, con aprobación controlada **solo en pruebas aisladas**. Amazon Rekognition recibe los bytes leídos de staging S3 mediante `DetectModerationLabels`; las etiquetas devueltas con confianza igual o superior a `MODERATION_MIN_CONFIDENCE` producen rechazo. El valor por defecto es `75`; cualquier error del proveedor falla cerrado con `ProcessingFailed`. La identidad AWS se obtiene mediante la cadena por defecto del SDK y nunca se entrega al cliente. Revisión humana, apelaciones, recuperación operativa y compensación entre S3/Mongo siguen abiertas. La prueba local con LocalStack verifica subida/HEAD/GET firmado y Catalog, pero LocalStack devolvió `200` también a una GET sin firma y **no acredita privacidad real de S3**.

Antes de desplegar se requieren revisión de seguridad e IAM/CORS, aceptación de Content Safety, registro formal de las dos rutas adicionales y verificación de bucket privado por su responsable; ninguna se presupone por la aprobación funcional ni por las pruebas locales. No consultar producción para aprobar la ficha ni modificar el Excel de 42 rutas.

Contrato generado desde la aplicación en `docs/openapi/media-catalog-pr33.json` (prueba de sincronía), ejemplos DTO frontend en `docs/media-catalog-pr33-frontend.md` y pasos aún pendientes del dueño AWS en `docs/media-pr33-aws-owner-checklist.md`. El Terraform actual no concede acciones S3 al Instance Profile EC2; resolverlo por plan/aprobación de Terraform, no dando credenciales a la aplicación.
