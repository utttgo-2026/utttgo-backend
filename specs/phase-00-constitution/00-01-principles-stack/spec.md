# spec.md — Constitución: Principios y Stack · Backend UTTT-GO
> Fase: `phase-00-constitution` · Módulo: `00-01-principles-stack`
> Versión: 1.0 · Estado: **Aprobado** · Fecha: 2026-10-29
> Documento subordinado a `constitution.md` v2.1. En caso de conflicto, prevalece la constitución.

---

## 1. Contexto

Esta es la spec de la constitución del backend UTTT-GO. Registra los **principios no negociables** y el **stack tecnológico obligatorio** que todos los specs, planes, tareas, agentes y decisiones posteriores deben respetar.

Su propósito es evitar desviaciones de arquitectura y de negocio antes de escribir código: cualquier módulo funcional deberá derivar de estas reglas y no podrá introducir tecnologías, flujos o conceptos prohibidos.

Alcance:

- Backend API de UTTT-GO para venta de alimentos en la UTTT.
- Arquitectura de monolito modular sobre .NET 10.
- Reglas de negocio: compra directa, pago exclusivo en efectivo y opciones comparativas de compra.
- Integraciones oficiales: Azure, MongoDB Atlas, Supabase Auth, Redis, Azure Blob Storage, Azure AI Content Safety y Firebase Cloud Messaging.

Referencias:

- `constitution.md` v2.1 — fuente de verdad del proyecto.
- `AGENTS.md` v2.0 — roles, sub-agentes y reglas operativas.
- `memoria.md` — decisiones, dependencias y glosario.
- `specs/phase-00-constitution/00-01-principles-stack/plan.md` y `task.md` — ejecución de esta fase.

---

## 2. Requisitos

| ID | Requisito | Prioridad | Fuente |
|---|---|---|---|
| REQ-001 | El backend se implementa en **.NET 10 LTS / C# 14** con ASP.NET Core 10, bajo un **monolito modular** con fronteras explícitas entre módulos. | Obligatorio | `constitution.md` § 2, § 4 |
| REQ-002 | La API expone rutas bajo `/api/v1`; los contratos se documentan por módulo. Tiempo real, si un módulo lo requiere, usa SignalR con backplane. | Obligatorio | `constitution.md` § 2, § 10 |
| REQ-003 | El despliegue oficial es **Azure App Service** detrás de **Azure Load Balancer / Application Gateway**, con API stateless y health probes. | Obligatorio | `constitution.md` § 2, § 9 |
| REQ-004 | Toda la infraestructura Azure se declara con **Terraform**; prohibido crear o modificar recursos manualmente en staging/producción. | Obligatorio | `constitution.md` § 9 |
| REQ-005 | La persistencia principal es **MongoDB Atlas** con `MongoDB.Driver`; sin ORM, sin migraciones y con índices definidos por módulo. | Obligatorio | `constitution.md` § 2, § 11 |
| REQ-006 | La autenticación es **Supabase Auth**; los roles se leen del perfil local en MongoDB, nunca del JWT. | Obligatorio | `constitution.md` § 6, § 12 |
| REQ-007 | El logout revoca el JWT agregando su `jti` a la blacklist de **Redis** (`revoked:{jti}`) con TTL hasta `exp`. | Obligatorio | `constitution.md` § 6 |
| REQ-008 | **Redis** se usa para caché, rate limiting distribuido y blacklist JWT, con namespaces separados y comportamiento fail-closed donde corresponde. | Obligatorio | `constitution.md` § 6 |
| REQ-009 | Las imágenes se suben **solo** a **Azure Blob Storage** mediante URLs SAS pre-firmadas generadas por el backend y se moderan antes de publicarse con **Azure AI Content Safety**. | Obligatorio | `constitution.md` § 7 |
| REQ-010 | Las notificaciones push se envían **solo** desde el backend con **Firebase Cloud Messaging (FCM Admin SDK)**. El cliente nunca conoce el service account. | Obligatorio | `constitution.md` § 8 |
| REQ-011 | **No existe carrito de compras**: la compra es directa e individual. Cada pedido corresponde a una única oferta/producto con su cantidad. | Obligatorio | `constitution.md` § 3 |
| REQ-012 | El **único método de pago es efectivo presencial** en el campus UTTT. No se integra pasarela de pago ni se almacenan datos financieros. | Obligatorio | `constitution.md` § 3 |
| REQ-013 | **No existen menús ni horarios**. La propuesta de valor son las **opciones comparativas tipo Eneba** sobre un mismo producto, distinguiendo producto canónico de oferta. | Obligatorio | `constitution.md` § 3 |
| REQ-014 | Precio, total, estado, `userId`, `sellerId` y roles nunca se confían al cliente; se derivan del JWT o de la base de datos. | Obligatorio | `constitution.md` § 5 |
| REQ-015 | Secretos solo en **Azure Key Vault** o variables de entorno; prohibido en código, configuración comiteada, Terraform con valores reales o logs. | Obligatorio | `constitution.md` § 6 |
| REQ-016 | Ningún módulo genera código de producción sin un `spec.md` aprobado y trazabilidad en `plan.md`/`task.md`. | Obligatorio | `AGENTS.md` § 1, § 2 |

---

## 3. Diseño

### 3.1 Principios y consecuencia práctica

| Principio | Consecuencia práctica | Verificación |
|---|---|---|
| **Stateless** | La API no guarda estado de sesión en memoria del proceso; el estado efímero vive en Redis. El balanceador puede enrutar cualquier request a cualquier instancia. SignalR usa backplane. | Pruebas de integración con múltiples instancias/health probes; revisión de que no existan campos estáticos de sesión. |
| **Fail-closed** | Si Redis no responde, la blacklist y el rate limit de `/auth/*` rechazan la operación. Sin moderación aprobada, la imagen no se publica. | Pruebas de caída de Redis y de Content Safety; matriz de degradación de `constitution.md` § 6.3. |
| **Sin confianza en el cliente** | `price`, `total`, `status`, `userId`, `sellerId` y roles enviados por el cliente se ignoran o rechazan; todo se valida y calcula server-side. | Pruebas de seguridad con payloads manipulados; revisión de autorización por recurso. |
| **Compra directa en efectivo** | No hay carrito ni checkout múltiple. Se compra una oferta; `PaymentMethod` es fijo `Cash`; la entrega implica efectivo recibido. | Pruebas de negocio: un pedido = una oferta, ausencia de carrito y de pasarela de pago. |
| **Opciones comparativas tipo Eneba** | El comprador compara ofertas de distintos vendedores para el mismo producto canónico y elige una. El producto no contiene precio ni vendedor; la oferta sí. | Pruebas de catálogo/comparativa; revisión de modelo `Product` vs `Offer`. |

### 3.2 Stack obligatorio por capa

| Capa | Tecnología oficial | Versión / configuración | Restricción |
|---|---|---|---|
| **Runtime/API** | .NET 10 LTS, C# 14, ASP.NET Core 10, monolito modular | REST `/api/v1`; Minimal API o Controllers por módulo; SignalR con backplane si hay tiempo real | Prohibido otro runtime, framework o arquitectura de microservicios |
| **Datos NoSQL** | MongoDB Atlas + `MongoDB.Driver` | MongoDB 7.x; TLS en entornos no locales; `ObjectId` como string en JSON | Sin ORM, sin migraciones, sin SQL Server/EF Core |
| **Cache/Rate Limit** | Azure Cache for Redis + `StackExchange.Redis` | Redis 7.x; namespaces `revoked:{jti}`, `cache:{recurso}:{clave}`, `ratelimit:{policy}:{partición}` | Rate limit distribuido obligatorio; prohibido rate limit en memoria en producción |
| **Auth/Identity** | Supabase Auth | JWT validado por firma, issuer, audience y expiración; roles desde MongoDB local | Prohibido Argon2id/BCrypt local, lockout o 2FA propio; roles nunca desde el JWT |
| **Storage/CDN** | Azure Blob Storage + SAS | Contenedores privados; SAS de un solo blob, TTL ≤ 5 min; lectura por SAS corta o CDN controlado | SAS solo backend; prohibido contenedor público o account key en el cliente |
| **IA Moderación** | Azure AI Content Safety API | Moderación previa a la publicación de cada imagen | Prohibido publicar imágenes sin resultado aprobado; error = no publicación |
| **Push** | Firebase Cloud Messaging (FCM Admin SDK) | Service account en Key Vault; `deviceTokens` por usuario; depuración de tokens inválidos | El cliente no envía push ni conoce credenciales; envíos idempotentes |
| **Tests** | xUnit, FluentAssertions, Moq/NSubstitute, `WebApplicationFactory`, Testcontainers (MongoDB/Redis), Azurite | Unitarias, integración y seguridad; Content Safety y FCM mockeados en CI | Prohibido Pact, NBomber, Stryker, ChaosTest y pruebas contra producción |
| **IaC/Nube** | Azure App Service + Load Balancer/Application Gateway + Terraform + Azure Key Vault | Terraform 1.9+; backend remoto de estado con bloqueo; entornos `dev`, `staging`, `prod` | Prohibido AWS/CloudFormation, cambios manuales en Azure y estado Terraform en Git |

Cualquier tecnología, servicio o patrón no listado en esta tabla requiere una enmienda aprobada a `constitution.md` antes de usarse.

---

## 4. Contratos

**N/A — spec constitucional.**

Este documento no define endpoints, DTOs, esquemas de colecciones ni contratos HTTP. Los contratos de API se especifican en el `spec.md` de cada módulo funcional, respetando:

- prefijo `/api/v1`;
- contrato de error de la constitución/spec del módulo;
- validación server-side;
- paginación en listados;
- autorización por recurso.

---

## 5. Tests

Los tests de esta spec son de gobernanza y verificación de cumplimiento; no son pruebas de endpoints.

| ID | Nivel | Qué verifica | Evidencia |
|---|---|---|---|
| T-001 | Build | Compilación en .NET 10 y formato correcto | `dotnet build -c Release`, `dotnet format --verify-no-changes` |
| T-002 | Estático | Ausencia de tecnologías prohibidas (SQL Server, EF, MassTransit, SQS, NBomber, Pact, Argon2id, `.slnx`) | Revisión/script de búsqueda en specs y código |
| T-003 | Negocio | Ausencia de carrito, menús, horarios y pasarela de pago; un pedido = una oferta; `PaymentMethod = Cash` | Pruebas unitarias/integración del módulo Orders y revisión de specs |
| T-004 | Seguridad | JWT inválido/expirado/revocado rechazado; roles desde MongoDB; autorización por recurso | `UtttGo.SecurityTests` |
| T-005 | Resiliencia | Blacklist y rate limit fail-closed; caché fail-open; matriz de degradación de Redis | Pruebas de integración con Redis caído |
| T-006 | Media/IA | SAS solo backend, alcance mínimo, contenedor privado, moderación previa y rechazo de contenido no seguro | Pruebas con Azurite + Content Safety mockeado |
| T-007 | Push | FCM solo backend, service account no expuesto, depuración de tokens, idempotencia | Pruebas con FCM mockeado |
| T-008 | IaC | `terraform fmt -check` y `terraform validate`; sin secretos ni estado en Git | Pipeline/CI y revisión de `infra/terraform` |
| T-009 | Trazabilidad | Cada REQ se mapea a constitución, spec de módulo y test | Matriz `docs/traceability.md` |
| T-010 | Secretos | Ningún secreto en el diff; referencias a Key Vault/variables de entorno | Secret scanning del CI + revisión manual |

---

## 6. Criterios de aceptación

- [ ] **AC-001** El stack implementado coincide exactamente con la tabla de § 3.2 y con `constitution.md` § 2.
- [ ] **AC-002** El backend compila en .NET 10 y `dotnet format --verify-no-changes` pasa.
- [ ] **AC-003** Las reglas BR-001 a BR-010 de la constitución están reflejadas en requisitos y pruebas.
- [ ] **AC-004** No existen artefactos ni endpoints de carrito, menús, horarios o pasarela de pago.
- [ ] **AC-005** El logout revoca el JWT en Redis y el sistema falla cerrado si Redis no responde.
- [ ] **AC-006** El rate limiting es distribuido con Redis y responde `429` con el contrato de error.
- [ ] **AC-007** Las imágenes usan SAS generadas por el backend y moderación previa; ningún contenedor es público.
- [ ] **AC-008** Las notificaciones push salen exclusivamente del backend con FCM Admin SDK.
- [ ] **AC-009** La infraestructura Azure se declara en Terraform y no hay cambios manuales en staging/producción.
- [ ] **AC-010** No hay secretos, connection strings ni service accounts en el repositorio.
- [ ] **AC-011** Existen pruebas de gobernanza T-001 a T-010 y la trazabilidad está actualizada.
- [ ] **AC-012** Este spec está marcado como **Aprobado**, con fecha 2026-10-29, y registrado en `memoria.md`.

---

## 7. Límites

Este spec **no**:

- Define endpoints, DTOs, esquemas de MongoDB, índices ni contratos HTTP; eso corresponde a los specs de módulo.
- Resuelve las decisiones abiertas DEC-002, DEC-003 y DEC-005.
- Autoriza escribir código de producción sin un `spec.md` de módulo aprobado.
- Define umbrales numéricos de rendimiento o cobertura; esos irán en un spec NFR.
- Cubre interfaz de usuario, aplicación móvil, runbooks de operación o soporte.
- Define seed de datos, migraciones ni estrategias de carga masiva.
- Sustituye a `constitution.md`; es un documento subordinado y operativo.

---

## 8. Aprobación y control de cambios

| Campo | Valor |
|---|---|
| Estado | **Aprobado** |
| Fecha | 2026-10-29 |
| Versión | 1.0 |
| Autor | AGENT-BE-01 (Architect) |
| Revisores | AGENT-BE-04 (Security Reviewer), AGENT-BE-06 (Memory Keeper) |
| Aprobado por | Equipo UTTT-GO |
| Próxima revisión | Al cambiar la constitución o al cerrar la fase 00 |

### 8.1 Registro de versiones

| Versión | Fecha | Cambio | Aprobado por |
|---|---|---|---|
| 1.0 | 2026-10-29 | Formalización del spec constitucional: principios, stack obligatorio, tests, criterios de aceptación y límites | Equipo UTTT-GO |

### 8.2 Reglas de control de cambios

1. Cambiar un principio, una regla de negocio o el stack exige actualizar primero `constitution.md` y luego este spec.
2. Todo cambio en este documento requiere nueva versión, registro en `memoria.md` y actualización de la trazabilidad.
3. No se permiten ediciones silenciosas ni excepciones por módulo sin enmienda aprobada.
4. Los agentes deben rechazar tareas que contradigan este spec o la constitución.
