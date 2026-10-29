# memoria.md — Backend UTTT-GO
> Registro acumulativo de decisiones, contexto, dependencias y glosario.  
> Mantenido por AGENT-BE-06 (Memory Keeper). Alineado con `constitution.md` v2.1.

---

## Log de decisiones

| ID | Fecha | Decisión | Justificación | Estado | Decidido por |
|---|---|---|---|---|---|
| DEC-001 | 2026-10-29 | Compra directa e individual sin carrito de compras | Decisión de negocio: se elimina el carrito; cada compra se ejecuta sobre una sola oferta. | **Resuelta** | Equipo UTTT-GO |
| DEC-002 | 2026-10-29 | Activación de vendedor por autoactivación sin aprobación admin (MVP) | El usuario completa su perfil de negocio (nombre, descripción, punto de recogida) y se activa por autoservicio; no hay revisión manual en el MVP. Impacta UI-027/028 y API-009. | **Resuelta** | Equipo UTTT-GO |
| DEC-003 | — | Retención/anonimización al eliminar cuenta | Necesario para cumplir aviso de privacidad. Afecta órdenes históricas, reseñas y auditoría. | **Abierta** | Pendiente asesoría legal/docente |
| DEC-004 | 2026-10-29 | Opciones Comparativas (tipo Eneba) como propuesta de valor | Confirmada la comparación de ofertas de un mismo producto; se distingue producto canónico de oferta. | **Resuelta** | Equipo UTTT-GO |
| DEC-005 | — | Persistencia del historial de chat | MongoDB (durable) vs solo en memoria de sesión (WebSocket). Impacta colección `chatMessages`. | **Abierta** | Pendiente |
| DEC-006 | 2026-10-29 | Despliegue oficial en Azure App Service detrás de Azure Load Balancer | Azure App Service + Load Balancer/Application Gateway, API stateless e IaC con Terraform. | **Resuelta** | Equipo UTTT-GO |
| DEC-007 | 2026-10-29 | Cómputo oficial en Azure App Service | Azure App Service es el entorno de cómputo oficial en Azure; reemplaza la alternativa Container Apps/Containers. | **Resuelta** | Equipo UTTT-GO |
| DEC-008 | 2026-10-29 | Método de pago único: efectivo presencial | No se integra pasarela de pago; `PaymentMethod` es fijo `Cash` y se cobra en el campus UTTT. | **Resuelta** | Equipo UTTT-GO |
| DEC-009 | 2026-10-29 | Eliminación de menús y horarios | La disponibilidad depende solo del stock/estado de la oferta, no de franjas horarias. | **Resuelta** | Equipo UTTT-GO |
| DEC-010 | 2026-10-29 | Moderación previa de imágenes con Azure AI Content Safety | Toda imagen se sube con SAS generada por el backend y se modera antes de publicarse. | **Resuelta** | Equipo UTTT-GO |
| DEC-011 | 2026-10-29 | Notificaciones push exclusivamente con FCM Admin SDK | El backend es el único emisor; el cliente nunca conoce el service account. | **Resuelta** | Equipo UTTT-GO |
| DEC-012 | 2026-10-29 | Producto canónico autoaprobado con unicidad por nombre normalizado + categoría | El vendedor crea producto+oferta en un flujo; la única validación es la moderación de imagen. Un duplicado normalizado responde `409` con el `productId` existente. | **Resuelta** | Equipo UTTT-GO |
| DEC-013 | 2026-10-29 | Stock reservado atómicamente al crear el pedido | Se descuenta al crear el pedido y se libera al rechazar/cancelar; stock 0 implica `SoldOut`. Evita sobreventa bajo concurrencia. | **Resuelta** | Equipo UTTT-GO |
| DEC-014 | 2026-10-29 | Catálogo y comparativa de lectura pública con caché Redis | Lectura sin sesión con rate limit; escritura solo para vendedor autenticado. Caché `cache:catalog:*` con TTL 60 s, invalidación en escritura y fail-open. | **Resuelta** | Equipo UTTT-GO |
| DEC-015 | 2026-10-29 | Alcance MVP de ofertas en phase-03 | Entran edición, pausa/reactivación, baja lógica, suspensión admin y búsqueda/filtros/orden. Fuera: CRUD de categorías, galería multi-imagen, reseñas y favoritos. | **Resuelta** | Equipo UTTT-GO |

---

## Contexto del proyecto

### Fuentes consultadas
- `UTTT_GO_Punto_4_Especificacion_API_v1.xlsx` — 42 fichas de endpoint (v0.1 borrador).
- `IC-Agile-User-Story-UTTT-GO_ACTUALIZADO.xlsx` — 44 historias de usuario.
- `UTTT-GO_Requerimientos_Definicion_Funcional_Figma.docx` — Definición funcional y maqueta.
- `IC-Sprint-Planning-11596_ES.xlsx` — 5 sprints, 13/10/2026 – 04/12/2026.

### Módulos identificados (42 endpoints API-001 a API-042)
| Módulo | Endpoints | Endpoints nuevos/en duda |
|---|---|---|
| Identidad | API-001 a API-005 | — |
| Usuarios | API-006 a API-008 | — |
| Vendedores | API-009 a API-010 | API-009 (autoactivación sin aprobación admin, DEC-002) |
| Productos | API-011 a API-018 | API-016 (comparativa — núcleo), API-017/018 (SAS + moderación) |
| Categorías | API-019 a API-022 | — |
| Pedidos | API-023 a API-029 | API-028/029 (QR entrega) |
| Calificaciones | API-030 a API-031 | — |
| Favoritos | API-032 a API-034 | — |
| Notificaciones | API-035 a API-036 | HU-027 incompleta |
| Chat | API-037 a API-038 | Sin HU explícita — agregar |
| Administración | API-039 a API-042 | — |

---

## Dependencias externas

| Servicio | Uso | Estado de acceso |
|---|---|---|
| Supabase Auth | Autenticación y JWT; roles en el perfil local | Por configurar (URL + JWT secret/ANON KEY) |
| MongoDB Atlas | Persistencia principal NoSQL | Por configurar connection string de Atlas (local solo contenedor de desarrollo) |
| Azure Cache for Redis | Blacklist JWT + caché + rate limiting distribuido | Por configurar (host, puerto, TLS, auth) |
| Azure Blob Storage | Imágenes con URLs SAS pre-firmadas por el backend | Por configurar (cuenta + contenedores staging/aprobados) |
| Azure AI Content Safety | Moderación previa de imágenes antes de publicar | Por configurar (endpoint + key en Key Vault) |
| Firebase Cloud Messaging (FCM Admin SDK) | Notificaciones push enviadas únicamente por el backend | Por configurar (service account en Key Vault) |
| Azure App Service | Cómputo oficial detrás de Load Balancer/Application Gateway | Por configurar con Terraform |
| Azure Key Vault | Secretos de Supabase, MongoDB, Redis, Blob, Content Safety y FCM | Por configurar con Terraform |

---

## Mapa de colecciones por módulo

| Módulo | Colecciones | Estado |
|---|---|---|
| `IdentityAndUsers` | `users`, `sellerProfiles` | Planificadas en `specs/phase-02-auth-users/plan.md` |
| `Offers` / `Catalog` | `products`, `offers`, `categories` | Spec v1.1 y plan aprobados; task pendiente |
| `Orders` | `orders` | Pendiente de spec/plan de módulo |
| Demás módulos | — | Pendiente de spec/plan de módulo |

---

## Glosario del dominio

| Término | Definición |
|---|---|
| Buyer | Usuario con rol de comprador (rol por defecto al registrarse) |
| Seller | Usuario con rol de vendedor activo por autoactivación (sin aprobación admin en el MVP) |
| Admin | Administrador con acceso total; nunca asignado desde el cliente |
| Product | Producto canónico: qué se vende (nombre, descripción, categoría e imagen aprobada). No incluye precio ni vendedor |
| Offer | Oferta de un vendedor para un producto canónico: precio, stock y condiciones. Es la unidad de compra y comparación |
| ComparativeOptions | Vista de opciones de compra que compara ofertas del mismo producto (tipo Eneba) |
| Order | Pedido de una única oferta/producto con cantidad; pago en efectivo al entregar |
| PaymentMethod | Método de pago; único valor permitido: `Cash` (efectivo presencial en UTTT) |
| DeliveryToken | Código/QR generado por el comprador para confirmar entrega física |
| AvailabilityStatus | Estado de la oferta: `Available`, `SoldOut` (no depende de horarios) |
| OrderStatus | `Pending`, `Accepted`, `InPreparation`, `Ready`, `Delivered`, `Rejected`, `CancelledByBuyer` |
| jti | JWT ID — identificador único del token, usado como clave en la blacklist de Redis (`revoked:{jti}`) |
| SAS | Shared Access Signature — URL temporal generada por el backend para subir o leer un único blob; alcance mínimo y TTL corto |
| MediaAsset | Registro de una imagen en Azure Blob con estado `PendingUpload`, `Approved` o `Rejected` |
| DeviceToken | Token FCM por dispositivo; se elimina si FCM lo rechaza |
| FCM | Firebase Cloud Messaging — canal único de notificaciones push, emitido por el backend con Admin SDK |
| Azure App Service | Entorno de cómputo oficial del backend en Azure, detrás de Load Balancer/Application Gateway |
| correlationId | Identificador único de request, propagado en todos los logs del ciclo de vida |

---

## Log de cambios a constitution.md

| Versión | Fecha | Cambio | Autor |
|---|---|---|---|
| 1.0 | 2026-10-28 | Creación inicial | Equipo UTTT-GO |
| 2.0 | 2026-10-29 | .NET 10/C# 14 y monolito modular; Azure App Service + Load Balancer + Terraform; MongoDB Atlas; reglas de negocio (sin carrito, efectivo presencial, sin menús/horarios, opciones comparativas); SAS + Content Safety; FCM Admin SDK; cierre de DEC-001/004/006 | Equipo UTTT-GO |
| 2.1 | 2026-10-29 | Cierre de DEC-007: Azure App Service como entorno de cómputo oficial en Azure | Equipo UTTT-GO |

---

## Historial de tareas

| Fecha | Tarea | Resultado |
|---|---|---|
| 2026-10-29 | Reubicar documentos base al árbol SDD `specs/` | `spec.md`, `plan.md` y `task.md` movidos a `specs/phase-00-constitution/00-01-principles-stack/`; la raíz queda sin estos archivos |
| 2026-10-29 | Formalizar el spec constitucional | `specs/phase-00-constitution/00-01-principles-stack/spec.md` reescrito con la estructura SDD formal (contexto, requisitos, diseño, contratos N/A, tests, criterios de aceptación, límites y aprobación); estado **Aprobado** |
| 2026-10-29 | Crear spec de autenticación y usuarios | `specs/phase-02-auth-users/spec.md` creado con RF-01..RF-11, RNF-01..RNF-14, casos límite y DoD; estado **Aprobado**; cierra DEC-002 (autoactivación de vendedores) y deja DEC-003 abierta |
| 2026-10-29 | Crear plan de arquitectura e implementación de autenticación y usuarios | `specs/phase-02-auth-users/plan.md` creado con estructura del módulo `IdentityAndUsers`, POCOs `users`/`sellerProfiles`, contratos `/auth`, `/users`, `/sellers`, `/admin`, JwtBearer + revocación Redis, rate limiting fail-closed, estrategia de pruebas y matriz de trazabilidad RF/RNF/CL; estado **Aprobado** |
| 2026-10-29 | Revisar plan de autenticación y usuarios | `specs/phase-02-auth-users/plan.md` v1.1: se agrega ejemplo JSON del formato estándar de error y matriz RF-01..RF-11 → sección del plan → prueba; sin código de producción |
| 2026-10-29 | Desglosar tareas atómicas de autenticación y usuarios | `specs/phase-02-auth-users/task.md` creado con TASK-AUTH-001..049 en 6 bloques (preparación, dominio/datos/DTOs, servicios e integración Supabase/Redis, endpoints/middleware, pruebas unitarias/integración/seguridad, cierre); cada tarea con RF y "Hecho cuando" verificable; sin código de producción |
| 2026-10-29 | Crear spec de catálogo y ofertas comparables | `specs/phase-03-catalog-offers/spec.md` creado tras discovery de 5 preguntas con RF-01..RF-15, RNF-01..RNF-14, casos límite, contratos y DoD; registra DEC-012..DEC-015 (producto autoaprobado/unicidad, reserva de stock, catálogo público con caché, alcance MVP de ofertas) |
| 2026-10-29 | Alinear rutas del spec de catálogo | `specs/phase-03-catalog-offers/spec.md` v1.1: prefijos canónicos `/api/v1/products/*` y `/api/v1/offers/*` en lugar de `/catalog` y `/seller` |
| 2026-10-29 | Crear plan de arquitectura e implementación de catálogo y ofertas | `specs/phase-03-catalog-offers/plan.md` creado con estructura `CatalogAndOffers`, POCOs `products`/`offers`/`categories`, contratos `/products` y `/offers`, caché Redis `cache:catalog:*` con invalidación por generación y fail-open, reserva atómica de stock, pruebas con Testcontainers/Azurite y matriz RF-01..RF-15; estado **Aprobado**; sin código de producción |
| 2026-10-29 | Desglosar tareas atómicas de catálogo y ofertas | `specs/phase-03-catalog-offers/task.md` creado con TASK-CATALOG-001..059 en 7 bloques (preparación, dominio/modelos/DTOs, repositorios/servicios, caché/invalidación, endpoints/autorización, pruebas unitarias/integración/seguridad, cierre); cada tarea con RF y "Hecho cuando" verificable; sin código de producción |
| 2026-10-29 | Inicializar repositorio Git y publicar el primer push | `git init` en la raíz del backend; `.gitignore` (\.NET 10, VS Code, secretos, Terraform, temporales) y `README.md` creados; commit raíz `09ecc92` con 20 archivos; remoto `origin` → `https://github.com/utttgo-2026/utttgo-backend.git`; rama `main`. Tras agregar a `CraxkerAlda` como colaborador, el push a `origin/main` se completó con éxito y la rama quedó con seguimiento configurado |

---

## Notas y pendientes

- [ ] Completar HU-027 (notificaciones) con descripción funcional.
- [ ] Agregar HU para chat (API-037, API-038) en el documento de historias de usuario.
- [ ] Especificar la comparativa de ofertas (API-016) en el `spec.md` del módulo Offers; ya es propuesta de valor confirmada.
- [ ] Resolver DEC-005 (persistencia de chat) antes de su módulo.
- [ ] Definir política de privacidad para DEC-003 antes del Sprint 4.
- [ ] Configurar Atlas, Azure Cache for Redis, Blob Storage, Content Safety, FCM y Terraform para `dev`, `staging` y `prod`.
- [ ] Actualizar las referencias a `spec.md`, `plan.md` y `task.md` en `AGENTS.md`, `constitution.md` y skills tras la reubicación.
