# constitution.md — Backend UTTT-GO
> Versión 2.3 · Archivo raíz de la metodología Spec-Driven Development
> Decisiones funcionales de v2.3 confirmadas por el equipo; **conciliación documental integral pendiente de revisión**. Los contratos API contradictorios no quedan aprobados por este cambio.
> Todo archivo del proyecto (AGENTS.md, memoria.md, spec.md, plan.md, task.md, skills) debe estar alineado con este documento.
> Cualquier decisión aquí listada como **innegociable** no puede revertirse sin una nueva versión explícita de este archivo.

---

## 1. Identidad del proyecto

| Campo | Valor |
|---|---|
| Nombre | UTTT-GO — Backend API |
| Tipo | REST API + WebSocket + Push Notifications |
| Dominio | Venta de alimentos universitarios en la UTTT |
| Propuesta de valor | Buscar y comparar alimentos publicados independientemente por distintos vendedores |
| Versión actual | 2.0 — Arquitectura y reglas de negocio definidas |
| Repositorio | `utttgo-backend` (por confirmar URL) |
| Equipo | 4 integrantes del equipo de software UTTT |

---

## 2. Stack tecnológico canónico (innegociable)

| Capa | Tecnología | Versión mínima | Notas |
|---|---|---|---|
| Lenguaje | C# | 14 | Incluido en .NET 10 |
| Runtime / Framework | .NET 10 + ASP.NET Core | 10 LTS | Minimal API o Controllers según módulo |
| Arquitectura | Monolito Modular | — | Un solo despliegue, módulos con fronteras estrictas (§ 4) |
| Nube | Microsoft Azure | — | Único proveedor cloud autorizado |
| Cómputo | Azure App Service | — | Entorno de cómputo oficial en Azure (DEC-007 resuelta) |
| Balanceo | Azure Load Balancer / Application Gateway | — | Obligatorio: la API debe ser stateless |
| IaC | Terraform | 1.9+ | Toda la infraestructura Azure se declara como código |
| Base de datos | MongoDB Atlas (NoSQL) | 7.x | Driver oficial `MongoDB.Driver`; TLS en entornos no locales |
| Autenticación | Supabase Auth | Managed | JWT firmados por Supabase; roles en perfil local |
| Redis | Azure Cache for Redis | 7.x | Caché + Rate Limit distribuido + Blacklist JWT |
| Tiempo real | SignalR | Incluido en ASP.NET Core 10 | Requiere backplane por el balanceador (§ 10) |
| Almacenamiento de imágenes | Azure Blob Storage + SAS | API estable | SAS generadas **solo** por el backend |
| Moderación de contenido | Azure AI Content Safety API | API estable | Moderación previa a la publicación de imágenes |
| Notificaciones push | Firebase Cloud Messaging (FCM Admin SDK) | SDK oficial .NET | El backend es el único emisor |
| Contenerización | Docker + Docker Compose | 25+ | Entorno local obligatorio |
| CI | GitHub Actions | — | Mínimo: lint, build, tests en PR |
| Versión de API | `/api/v1` | — | Prefijo obligatorio en todas las rutas |

---

## 3. Reglas de negocio innegociables

Estas reglas son **duras**: ninguna `spec.md`, `plan.md` ni implementación puede contradecirlas.

| ID | Regla | Implicación técnica |
|---|---|---|
| **BR-001** | **No existe carrito de compras.** La compra es directa e individual. | Prohibido crear entidades, endpoints o estados de carrito. El checkout se ejecuta sobre un producto publicado por un vendedor. |
| **BR-002** | Cada pedido corresponde a **un único producto publicado por un vendedor** con su cantidad. | Un `Order` referencia un solo `productId` y conserva el precio aplicable de forma segura; no hay pedidos multi-vendedor ni multi-producto. El contrato definitivo de Orders requiere conciliación. |
| **BR-003** | El **único método de pago es efectivo presencial**. | No se integra pasarela de pago. `PaymentMethod` es un valor fijo `Cash`. El backend no procesa, valida ni almacena datos financieros. |
| **BR-004** | **No existen menús ni horarios.** | Prohibidas entidades, campos y validaciones de menú, horario, franja o disponibilidad temporal. La disponibilidad depende del estado y existencias del producto. Los ingredientes del alimento no constituyen un menú. |
| **BR-005** | La propuesta de valor central es **comparar opciones de distintos vendedores** al buscar un alimento. | La búsqueda y los filtros presentan productos propios de cada vendedor con sus ingredientes, precios y calificaciones disponibles del producto y vendedor; no se exige equivalencia exacta ni producto maestro compartido. |
| **BR-006** | **Cada vendedor posee y administra sus propios productos/publicaciones.** | El producto pertenece a un vendedor e incluye su precio, existencias y estado. No existe relación obligatoria producto canónico → ofertas de varios vendedores. |
| **BR-007** | Toda imagen se sube a Azure Blob mediante **URL SAS pre-firmada generada por el backend** y se **modera antes de publicarse**. | El cliente nunca recibe claves de storage; ver § 7. |
| **BR-008** | Las notificaciones push se envían **exclusivamente** por el backend vía FCM Admin SDK. | Ver § 8. |
| **BR-009** | El comprador no determina precio, total, estado, `userId`, `sellerId` ni roles. | Un vendedor propietario puede solicitar cambios de precio, existencias y publicación de su producto mediante contratos autorizados y validados; el backend deriva identidades, verifica propiedad y controla transiciones. No se confía en importes/estados aportados durante la compra. |
| **BR-010** | El precio mostrado y cobrado corresponde al producto del vendedor seleccionado. | El backend consulta el precio vigente y calcula el total como `price × quantity`; la política de concurrencia y snapshot de precio en Orders queda por diseñar. |
| **BR-011** | Eliminar una cuenta implica eliminar la información perteneciente al usuario. | Para un vendedor incluye productos, publicaciones, contenido e historial propios. El tratamiento de registros compartidos, como pedidos del otro participante, y la ejecución coordinada quedan pendientes de diseño; no se presume retención de datos propios. |
| **BR-012** | El historial de chat es temporal. | Se conserva mientras el pedido está en curso y se elimina dentro de los tres días posteriores a su finalización. El disparador exacto y mecanismo de purga se definen en el diseño de Chat. |
| **BR-013** | El usuario puede recuperar una contraseña olvidada por enlace enviado a su correo registrado. | Supabase Auth gestiona credenciales; decidir si el flujo requiere endpoint backend nuevo antes de exponerlo. |

---

## 4. Arquitectura: monolito modular

1. **Un solo artefacto desplegable** (API), organizado internamente en módulos de dominio con fronteras explícitas.
2. **Módulos previstos**: `Identity`, `Users`, `Sellers`, `Catalog` (productos por vendedor y categorías), `Orders`, `Reviews`, `Favorites`, `Notifications`, `Chat`, `Media`, `Admin`. La separación técnica de un módulo `Offers` ya no es obligatoria; requiere justificación si se propone.
3. **Regla de frontera**: un módulo no accede a las colecciones ni a los repositorios de otro módulo. La comunicación es mediante interfaces de aplicación/contratos publicados por el módulo dueño.
4. **Shared kernel mínimo**: solo tipos transversales (resultados, contratos de error, primitivas). Nada de lógica de negocio compartida.
5. **Cada módulo** contiene sus propias capas: `Endpoints/Controllers`, `Application/Services`, `Domain`, `Infrastructure/Repositories`, `Contracts/DTOs`, `Validators`.
6. **Sin base de datos compartida por conveniencia**: cada módulo es dueño de sus colecciones MongoDB (documentado en `memoria.md`).
7. **Dependencias entre módulos** deben ser acíclicas y quedar registradas en `memoria.md`.
8. **Servicios stateless**: no se guarda estado de sesión en memoria del proceso; estado efímero vive en Redis para soportar el balanceador.

---

## 5. Principios de arquitectura

1. **Separación de responsabilidades** — Endpoints/Controllers reciben peticiones; Services contienen lógica de negocio; Repositories acceden a datos. Nunca lógica de negocio en Controllers.
2. **Sin confianza en el cliente** — `userId`, `sellerId`, `total` y `roles` del body se ignoran o rechazan; precio, stock y disponibilidad solicitados por un vendedor se validan y autorizan por propiedad. Para pedidos se toman precio y estado del servidor, nunca de importes enviados por el comprador.
3. **Validación server-side obligatoria** — Toda entrada pasa por validadores explícitos antes de tocar la base de datos.
4. **Paginación en todos los listados** — `page` + `pageSize` (máximo configurable). Nunca devolver colecciones ilimitadas.
5. **Idempotencia donde sea posible** — Operaciones críticas (crear perfil local, publicar producto, confirmar entrega y eliminar cuenta) deben ser seguras de reintentar.
6. **Logging estructurado** — JSON con `correlationId` en cada request. Ningún log contiene contraseñas, tokens, datos personales sensibles ni claves de Azure/FCM.
7. **Fail-closed en seguridad** — Ver § 6.3. Si Redis no responde, la revocación de tokens no se ignora silenciosamente.
8. **Infraestructura como código** — Ningún recurso Azure de staging/producción se crea a mano. Todo vive en Terraform (§ 9).

---

## 6. Seguridad — reglas no negociables

### 6.1 Reglas generales
- HTTPS en todos los entornos (incluido desarrollo con certificado local).
- Secretos solo en variables de entorno o Azure Key Vault. Prohibido en código fuente, archivos comiteados, Terraform con valores reales o logs.
- JWT validado en cada request: firma, issuer, audience, expiración y lista de revocados (Redis).
- Autorización por recurso: comprobar que el recurso pertenece al usuario autenticado, no solo que el usuario tiene el rol correcto.
- Rate limiting distribuido en todas las rutas públicas, más estricto en `/auth/*`.
- `correlationId` generado por middleware al inicio de cada request y propagado a todos los logs.
- CORS con orígenes explícitos; prohibido `*` en producción.
- Los roles **nunca** se leen del JWT; se leen del perfil local en MongoDB.

### 6.2 Redis: tres usos con namespaces separados

| Uso | Namespace | TTL |
|---|---|---|
| Blacklist JWT | `revoked:{jti}` | Igual al tiempo restante hasta `exp` |
| Caché | `cache:{recurso}:{clave}` | Configurable por recurso (inicio: 60 s catálogo) |
| Rate limiting distribuido | `ratelimit:{policy}:{partición}` | Ventana de la política |

### 6.3 Matriz de degradación si Redis no responde

| Uso | Comportamiento | Resultado |
|---|---|---|
| Blacklist JWT | **Fail-closed** | Rechazar el token (`401`/`503`) |
| Rate limit en `/auth/*` | **Fail-closed** | Rechazar requests de autenticación |
| Rate limit general | **Fail-closed con alerta** | Responder `503` y registrar incidente |
| Caché | **Fail-open** | Ir a MongoDB y registrar degradación |

---

## 7. Almacenamiento de imágenes y moderación (innegociable)

- **Azure Blob Storage es el único destino** para imágenes de producto.
- **Las URLs SAS solo las genera el backend.** El cliente nunca recibe `AZURE_STORAGE_CONNECTION_STRING`, account key ni SAS de amplio alcance.
- La SAS debe ser de **alcance mínimo**: un único blob, permiso de escritura/creación, TTL corto (≤ 5 minutos), content-type y tamaño restringidos.
- **Ningún contenedor es público.** La lectura de imágenes aprobadas se sirve mediante SAS de lectura de corta duración o CDN con acceso controlado.
- **Flujo obligatorio de subida y moderación**:
  1. El cliente solicita intención de subida al backend.
  2. El backend valida JWT, MIME permitido, extensión y tamaño; crea el registro `mediaAssets` en estado `PendingUpload` y devuelve una SAS de escritura.
  3. El cliente sube la imagen **directamente** a un contenedor de staging.
  4. El cliente notifica la finalización; el backend invoca **Azure AI Content Safety API**.
  5. Si es segura → estado `Approved`; si no → se elimina el blob y estado `Rejected`.
  6. Solo un `mediaAssetId` en estado `Approved` puede asociarse a un producto publicado.
- La moderación es **previa a la publicación**; una imagen no moderada nunca es visible.
- Sin moderación o con error de Content Safety se aplica **fail-closed**: la imagen permanece no publicada.

---

## 8. Notificaciones (innegociable)

- **Firebase Cloud Messaging (FCM Admin SDK)** es el único mecanismo de push.
- Las credenciales del service account se leen desde Azure Key Vault / variables de entorno; nunca se comitean.
- El backend registra y depura `deviceTokens` por usuario; los tokens inválidos se eliminan tras el error de FCM.
- El cliente **no** envía notificaciones directamente a FCM ni conoce el service account.
- Las notificaciones son el resultado de eventos de dominio (cambio de estado de pedido, mensaje de chat, etc.), nunca de lógica en el cliente.
- Las notificaciones deben ser idempotentes: un reintento no genera duplicados visibles.

---

## 9. Infraestructura, despliegue e IaC

- **Azure App Service** detrás de **Load Balancer / Application Gateway** con health probes.
- La API es **stateless**; cualquier estado efímero va a Redis.
- **SignalR requiere backplane** (Azure SignalR Service o backplane Redis) para funcionar con múltiples instancias detrás del balanceador.
- **Terraform** declara toda la infraestructura: App Service, load balancer, Key Vault, Blob Storage, Azure Cache for Redis, Content Safety, networking, diagnósticos y entornos (`dev`, `staging`, `prod`).
- **Estado remoto de Terraform** con backend remoto y bloqueo; jamás estado local en el repositorio.
- Prohibido crear o modificar recursos manualmente en Azure para staging/producción. Cambios fuera de Terraform se consideran incidentes.
- MongoDB Atlas se aprovisiona por su propio mecanismo (Terraform provider o proceso documentado); connection strings viven en Key Vault.
- Promoción entre entornos mediante pipeline, no mediante cambios manuales.

---

## 10. Tiempo real (SignalR)

- SignalR se usa para estado de pedido y chat.
- Los grupos de conexión se derivan de identidades autorizadas (`userId`, `orderId`), nunca de IDs enviados sin validar.
- Con balanceador y múltiples instancias, el backplane es obligatorio.
- La reconexión y el estado deben residir en Redis/MongoDB, no en memoria del proceso.

---

## 11. Convenciones de código

| Aspecto | Convención |
|---|---|
| Idioma del código | Inglés (variables, clases, métodos, comentarios técnicos) |
| Idioma de la documentación | Español (specs, planes, tasks, ADRs) |
| Formato de fechas | ISO 8601 UTC (`2026-10-13T12:00:00Z`) |
| Dinero | Decimal en MXN; nunca `float` o `double` para precios |
| IDs | `ObjectId` de MongoDB, representado como `string` en JSON |
| Enums | PascalCase, valores estables (no cambiar una vez publicados) |
| Nombres de colecciones MongoDB | camelCase plural (`users`, `products`, `orders`); `offers` fue parte del diseño histórico superado |
| Nombres de endpoints | kebab-case en ruta, camelCase en JSON |
| Versión de API | Prefijo `/api/v1` obligatorio; cambiar solo con breaking change |
| Formato / lint | `dotnet format` debe pasar; gate en CI |

---

## 12. Roles del sistema

| Rol | Descripción | Cómo se asigna |
|---|---|---|
| `Buyer` | Comprador — rol por defecto al registrarse | Automático en registro |
| `Seller` | Vendedor activo | Autoactivación al completar nombre de negocio, descripción y punto de recogida; sin aprobación administrativa previa en el MVP (DEC-002). El rol se asigna desde el backend al perfil local, nunca desde el cliente. |
| `Admin` | Administrador de la plataforma | Asignado manualmente; nunca desde cliente |
| Ninguno de los anteriores puede ser enviado desde el cliente | — | — |

---

## 13. Máquina de estados de pedido

Cada pedido corresponde a **un solo producto de un vendedor** (BR-001, BR-002) y el pago es **en efectivo al entregar** (BR-003).

```
Pending → Accepted → InPreparation → Ready → Delivered   (Delivered = pago en efectivo recibido)
Pending → Rejected            (terminal)
Pending → CancelledByBuyer    (terminal)
Accepted → CancelledByBuyer   (solo si el equipo lo aprueba)
```
Transiciones inválidas retornan `409 Conflict`. No se agregan estados de pago online.

---

## 14. Gestión de errores

| Código | Significado | Cuándo usarlo |
|---|---|---|
| 400 | Bad Request | Validación de entrada fallida |
| 401 | Unauthorized | Token ausente, inválido, expirado o revocado |
| 403 | Forbidden | Token válido pero sin permiso para el recurso |
| 404 | Not Found | Recurso inexistente o no visible para el actor |
| 409 | Conflict | Conflicto de negocio (duplicado, transición inválida) |
| 422 | Unprocessable Entity | Datos válidos en forma pero inválidos en semántica de negocio |
| 429 | Too Many Requests | Rate limit excedido |
| 502/503 | Bad Gateway / Service Unavailable | Dependencia externa caída (MongoDB, Redis, Content Safety, FCM) |

Contrato único de error:
```json
{
  "correlationId": "string",
  "status": 400,
  "code": "VALIDATION_ERROR",
  "message": "Descripción para el desarrollador",
  "details": []
}
```

---

## 15. Pruebas — niveles mínimos requeridos

1. **Unitarias** — Lógica de negocio y validaciones en Services sin dependencias externas (mocks).
2. **Integración** — Endpoints con MongoDB en testcontainers; Redis mock o testcontainer.
3. **Seguridad** — Al menos: token inválido rechazado, rol incorrecto rechazado, recurso ajeno rechazado, rate limit activo, revocación en Redis aplicada.
4. **Media/IA** — SAS generada solo por backend, alcance mínimo, moderación antes de publicar, rechazo de contenido no seguro.
5. **Notificaciones** — Envío FCM con SDK mockeado, depuración de tokens inválidos, idempotencia.
6. **Aceptación** — Escenarios Given/When/Then derivados de cada ficha de endpoint en `spec.md`.

Ninguna tarea se marca completada sin pruebas ejecutadas.

---

## 16. Decisiones: resueltas y abiertas

### 16.1 Resueltas

| ID | Decisión | Resolución |
|---|---|---|
| DEC-001 | ¿Carrito o compra directa? | **Resuelta**: no existe carrito (BR-001). |
| DEC-002 | ¿Requiere aprobación administrativa para activar vendedor? | **Resuelta en v2.2**: autoactivación tras completar los datos obligatorios del negocio, sin aprobación administrativa previa en el MVP (§ 12). |
| DEC-003 | Eliminación de cuenta | **Resuelta funcionalmente**: eliminar información perteneciente al usuario; para vendedor incluye productos, publicaciones, contenido e historial. Pendiente diseño de datos compartidos y ejecución. |
| DEC-004 | Comparación de opciones entre vendedores | **Revisada**: buscar/filtrar productos propios de distintos vendedores; sin producto maestro compartido ni ofertas asociadas a él (BR-005, BR-006). |
| DEC-005 | Historial de chat temporal | **Resuelta funcionalmente**: conservar durante pedido en curso; eliminar dentro de tres días de completarse. Pendiente diseño de almacenamiento, disparador y purga. |
| DEC-006 | Patrón de despliegue | **Resuelta**: Azure con Load Balancer, IaC en Terraform e instancias stateless (§ 9). |
| DEC-007 | Cómputo final en Azure | **Resuelta**: Azure App Service como entorno de cómputo oficial (§ 2, § 9). |
| DEC-008 | Método de pago | **Resuelta**: efectivo presencial únicamente (BR-003). |
| DEC-009 | Menús y horarios | **Resuelta**: eliminados del alcance (BR-004). |
| DEC-010 | Moderación de imágenes | **Resuelta**: Azure AI Content Safety previa a publicación (§ 7). |
| DEC-011 | Canal de notificaciones push | **Resuelta**: FCM Admin SDK exclusivamente (§ 8). |

### 16.2 Detalles pendientes de diseño (sin reabrir decisiones funcionales)

| ID | Decisión | Estado |
|---|---|---|
| DEC-003 | Datos compartidos entre usuarios, operaciones activas, consistencia, reintentos y eliminación de identidad/activos | **Pendiente técnico/privacidad**; no contradice BR-011. |
| DEC-005 | Almacenamiento, definición de pedido completado, cronograma y mecanismo verificable de purga | **Pendiente técnico**; no contradice BR-012. |
| API-016 | Ficha de comparativa basada en producto maestro | **Pendiente aprobación formal**: proponer retiro/sustitución funcional con API-011 (búsqueda/filtros), sin alterar aún el Excel ni redirigir la ruta. |

---

## 17. Lo que este archivo NO autoriza hacer

- Escribir código de producción antes de que exista un `spec.md` aprobado para esa funcionalidad.
- Cambiar el stack, la nube o las reglas innegociables sin actualizar este `constitution.md` y los agentes afectados.
- Introducir carrito, menús, horarios, pasarelas de pago, storage alternativo o notificaciones fuera de FCM.
- Permitir que el cliente genere URLs SAS o suba imágenes sin moderación previa.
- Agregar endpoints no listados en la especificación API sin crear primero su ficha.

## Nota de conciliación funcional (2026-11-03)

Las cinco decisiones confirmadas por el equipo sustituyen las premisas de producto canónico compartido, baja lógica como política final y chat indefinido. Esta revisión **no aprueba automáticamente** nuevos payloads, la retirada de API-016 ni una ruta de recuperación de contraseña. Se debe conciliar el inventario Excel antes de implementar contratos contradictorios.
- Comitear secretos, contraseñas, tokens, connection strings o service accounts.
- Crear recursos Azure manualmente fuera de Terraform en staging/producción.

---

## 18. Registro de cambios

| Versión | Fecha | Cambio |
|---|---|---|
| 1.0 | 2026-10-28 | Creación inicial |
| 2.0 | 2026-10-29 | .NET 10/C# 14 y monolito modular; Azure + Load Balancer + Terraform; MongoDB Atlas; reglas de negocio (sin carrito, efectivo presencial, sin menús/horarios, comparación de ofertas); SAS + Content Safety obligatorios; FCM Admin SDK; cierre de DEC-001/004/006; nuevas decisiones DEC-008 a DEC-011 |
| 2.1 | 2026-10-29 | Cierre de DEC-007: Azure App Service como entorno de cómputo oficial en Azure; actualización de § 2 y § 9 |
| 2.2 | 2026-11-02 | Ratificación del equipo de DEC-002: autoactivación de vendedor en el MVP al completar el perfil de negocio; actualización de § 12 y § 16. DEC-003 y DEC-005 siguen abiertas. |
| 2.3 | 2026-11-03 | Cinco decisiones funcionales confirmadas por el equipo: productos independientes por vendedor y búsqueda comparativa, eliminación de datos propios, chat temporal, recuperación por enlace y control de publicaciones. DEC-003/005 cerradas funcionalmente; implementación y contratos contradictorios pendientes. |

---

*Este documento es la fuente de verdad del proyecto backend. Cualquier conflicto entre un archivo subordinado y este constitution.md se resuelve a favor de este constitution.md hasta que sea actualizado explícitamente por el equipo.*
