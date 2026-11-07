# spec.md — Constitución: Principios y Stack · Backend UTTT-GO
> Fase: `phase-00-constitution` · Módulo: `00-01-principles-stack`
> Versión: 1.3 · Estado: **Borrador de conciliación** · 2026-11-06 (v1.1 aprobada; v1.2/v1.3 pendientes de revisión integral)
> Documento subordinado a `docs/constitution.md` v2.4. En caso de conflicto, prevalece la constitución.

---

## 1. Contexto

Esta es la spec de la constitución del backend UTTT-GO. Registra los **principios no negociables** y el **stack tecnológico obligatorio** que todos los specs, planes, tareas, agentes y decisiones posteriores deben respetar.

Su propósito es evitar desviaciones de arquitectura y de negocio antes de escribir código: cualquier módulo funcional deberá derivar de estas reglas y no podrá introducir tecnologías, flujos o conceptos prohibidos.

Alcance:

- Backend API de UTTT-GO para venta de alimentos en la UTTT.
- Arquitectura de monolito modular sobre .NET 10.
- Reglas de negocio: compra directa, pago exclusivo en efectivo y opciones comparativas de compra.
- Integraciones oficiales: AWS (EC2, S3, IAM, CloudWatch), MongoDB Atlas, Supabase Auth, Redis contenerizado, Azure AI Content Safety y Firebase Cloud Messaging (DEC-018).

Referencias:

- `docs/constitution.md` v2.4 — fuente de verdad del proyecto.
- `AGENTS.md` v2.3 — roles, sub-agentes y reglas operativas.
- `memoria.md` — decisiones, dependencias y glosario.
- `specs/phase-00-constitution/00-01-principles-stack/plan.md` y `task.md` — ejecución de esta fase.

---

## 2. Requisitos

| ID | Requisito | Prioridad | Fuente |
|---|---|---|---|
| REQ-001 | El backend se implementa en **.NET 10 LTS / C# 14** con ASP.NET Core 10, bajo un **monolito modular** con fronteras explícitas entre módulos. | Obligatorio | `constitution.md` § 2, § 4 |
| REQ-002 | La API expone rutas bajo `/api/v1`; los contratos se documentan por módulo. Tiempo real, si un módulo lo requiere, usa SignalR con backplane. | Obligatorio | `constitution.md` § 2, § 10 |
| REQ-003 | El despliegue oficial es **una instancia EC2 t3.micro** (Ubuntu 24.04, `us-east-2`) con **Elastic IP**, la API stateless y la escalabilidad académica cubierta por **2 réplicas Docker Compose detrás de Nginx** dentro de la instancia (DEC-018). | Obligatorio | `constitution.md` § 2, § 9 |
| REQ-004 | Toda la infraestructura AWS se declara con **Terraform** en `infra/terraform`; prohibido crear o modificar recursos manualmente en staging/producción. | Obligatorio | `constitution.md` § 9 |
| REQ-005 | La persistencia principal es **MongoDB Atlas** con `MongoDB.Driver`; sin ORM, sin migraciones y con índices definidos por módulo. | Obligatorio | `constitution.md` § 2, § 11 |
| REQ-006 | La autenticación es **Supabase Auth**; los roles se leen del perfil local en MongoDB, nunca del JWT. | Obligatorio | `constitution.md` § 6, § 12 |
| REQ-007 | El logout revoca el JWT agregando su `jti` a la blacklist de **Redis** (`revoked:{jti}`) con TTL hasta `exp`. | Obligatorio | `constitution.md` § 6 |
| REQ-008 | **Redis** se usa para caché, rate limiting distribuido y blacklist JWT, con namespaces separados y comportamiento fail-closed donde corresponde. | Obligatorio | `constitution.md` § 6 |
| REQ-009 | Las imágenes se suben **solo** a un **bucket S3 privado** mediante URLs presignadas generadas por el backend y se moderan antes de publicarse con **Azure AI Content Safety**. | Obligatorio | `constitution.md` § 7 |
| REQ-010 | Las notificaciones push se envían **solo** desde el backend con **Firebase Cloud Messaging (FCM Admin SDK)**. El cliente nunca conoce el service account. | Obligatorio | `constitution.md` § 8 |
| REQ-011 | **No existe carrito de compras**: la compra es directa e individual. Cada pedido corresponde a un producto de un vendedor con su cantidad. | Obligatorio | `constitution.md` § 3 |
| REQ-012 | El **único método de pago es efectivo presencial** en el campus UTTT. No se integra pasarela de pago ni se almacenan datos financieros. | Obligatorio | `constitution.md` § 3 |
| REQ-013 | **No existen menús ni horarios**. La propuesta de valor es comparar productos independientes de distintos vendedores mediante búsqueda/filtros con ingredientes, precio y calificaciones disponibles, sin producto maestro compartido. | Obligatorio | `constitution.md` § 3 |
| REQ-014 | El vendedor dueño puede solicitar cambios validados de precio/stock/estado de su producto; el comprador nunca fija precio/total/identidades/roles de la compra. | Obligatorio | `constitution.md` § 3, § 5 |
| REQ-015 | Secretos solo en variables de entorno de la instancia/contenedor; prohibido en código, configuración comiteada, Terraform con valores reales o logs. | Obligatorio | `constitution.md` § 6 |
| REQ-016 | Ningún módulo genera código de producción sin un `spec.md` aprobado y trazabilidad en `plan.md`/`task.md`. | Obligatorio | `AGENTS.md` § 1, § 2 |
| REQ-017 | La activación de vendedor es automática al completar nombre de negocio, descripción y punto de recogida en el MVP; no requiere aprobación administrativa previa ni acepta roles enviados por el cliente (DEC-002). | Obligatorio | `docs/constitution.md` § 12, § 16 |
| REQ-018 | Eliminar cuenta elimina información propia, incluyendo publicaciones/contenido/historial de vendedor; datos compartidos requieren diseño técnico (DEC-003). | Obligatorio | `docs/constitution.md` § 3, § 16 |
| REQ-019 | Chat temporal: mensajes conservados durante pedido activo y eliminados dentro de tres días de completarse (DEC-005). | Obligatorio | `docs/constitution.md` § 3, § 16 |
| REQ-020 | Recuperación de contraseña mediante enlace enviado al correo registrado, credenciales en Supabase; ruta backend nueva requiere ficha aprobada. | Obligatorio | `docs/constitution.md` § 3 |

---

## 3. Diseño

### 3.1 Principios y consecuencia práctica

| Principio | Consecuencia práctica | Verificación |
|---|---|---|
| **Stateless** | La API no guarda estado de sesión en memoria del proceso; el estado efímero vive en Redis. El balanceador puede enrutar cualquier request a cualquier instancia. SignalR usa backplane. | Pruebas de integración con múltiples instancias/health probes; revisión de que no existan campos estáticos de sesión. |
| **Fail-closed** | Si Redis no responde, la blacklist y el rate limit de `/auth/*` rechazan la operación. Sin moderación aprobada, la imagen no se publica. | Pruebas de caída de Redis y de Content Safety; matriz de degradación de `constitution.md` § 6.3. |
| **Sin confianza en el cliente** | El vendedor propietario solicita cambios validados de precio/stock/estado de su publicación; el comprador no fija precio/total/roles/identidades. | Pruebas de autorización por recurso y payloads manipulados. |
| **Compra directa en efectivo** | No hay carrito ni checkout múltiple. Se compra un producto de un vendedor; `PaymentMethod` es fijo `Cash`; entrega implica efectivo recibido. | Un pedido = un producto, ausencia de carrito/pasarela. |
| **Opciones comparativas** | El comprador busca/filtra publicaciones propias de vendedores distintos; no hay producto maestro compartido. | Pruebas de catálogo, homónimos por vendedor y filtros. |

### 3.2 Stack obligatorio por capa

| Capa | Tecnología oficial | Versión / configuración | Restricción |
|---|---|---|---|
| **Runtime/API** | .NET 10 LTS, C# 14, ASP.NET Core 10, monolito modular | REST `/api/v1`; Minimal API o Controllers por módulo; SignalR con backplane si hay tiempo real | Prohibido otro runtime, framework o arquitectura de microservicios |
| **Datos NoSQL** | MongoDB Atlas + `MongoDB.Driver` | MongoDB 7.x; TLS en entornos no locales; `ObjectId` como string en JSON | Sin ORM, sin migraciones, sin SQL Server/EF Core |
| **Cache/Rate Limit** | Redis (contenedor Docker; local y en la EC2) + `StackExchange.Redis` | Redis 7.x; namespaces `revoked:{jti}`, `cache:{recurso}:{clave}`, `ratelimit:{policy}:{partición}` | Rate limit distribuido obligatorio; prohibido rate limit en memoria en producción; sin ElastiCache por costo $0 (DEC-018) |
| **Auth/Identity** | Supabase Auth | JWT validado por firma, issuer, audience y expiración; roles desde MongoDB local | Prohibido Argon2id/BCrypt local, lockout o 2FA propio; roles nunca desde el JWT |
| **Storage/CDN** | Amazon S3 + URLs presignadas | Bucket privado (Block Public Access); presigned de un solo objeto, TTL ≤ 5 min; lectura por presigned GET del backend | Presigned solo backend; prohibido bucket/objeto público o access keys en el cliente |
| **IA Moderación** | Azure AI Content Safety API | Moderación previa a la publicación de cada imagen | Prohibido publicar imágenes sin resultado aprobado; error = no publicación |
| **Push** | Firebase Cloud Messaging (FCM Admin SDK) | Service account en variables de entorno de la instancia; `deviceTokens` por usuario; depuración de tokens inválidos | El cliente no envía push ni conoce credenciales; envíos idempotentes |
| **Tests** | xUnit, FluentAssertions, Moq/NSubstitute, `WebApplicationFactory`, Testcontainers (MongoDB/Redis), dobles o emulación S3 | Unitarias, integración y seguridad; Content Safety y FCM mockeados en CI | Prohibido Pact, NBomber, Stryker, ChaosTest y pruebas contra producción |
| **IaC/Nube** | AWS (`us-east-2`): EC2 t3.micro + EIP + Security Group + S3 privado + IAM/Instance Profile + CloudWatch + Budget, con Terraform | Terraform 1.9+; estado local fuera de Git en el MVP, backend remoto (S3 + DynamoDB) antes de producción | Prohibido CloudFormation, cambios manuales y estado Terraform en Git; sin ALB/ASG/ElastiCache/RDS |

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
| T-003 | Negocio | Ausencia de carrito, menús, horarios y pasarela; un pedido = un producto; `PaymentMethod = Cash` | Pruebas unitarias/integración de Orders y revisión de specs |
| T-004 | Seguridad | JWT inválido/expirado/revocado rechazado; roles desde MongoDB; autorización por recurso | `UtttGo.SecurityTests` |
| T-005 | Resiliencia | Blacklist y rate limit fail-closed; caché fail-open; matriz de degradación de Redis | Pruebas de integración con Redis caído |
| T-006 | Media/IA | Presigned solo backend, alcance mínimo, bucket privado, moderación previa y rechazo de contenido no seguro | Pruebas con emulación/dobles de S3 + Content Safety mockeado |
| T-007 | Push | FCM solo backend, service account no expuesto, depuración de tokens, idempotencia | Pruebas con FCM mockeado |
| T-008 | IaC | `terraform fmt -check` y `terraform validate`; sin secretos ni estado en Git | Pipeline/CI y revisión de `infra/terraform` |
| T-009 | Trazabilidad | Cada REQ se mapea a constitución, spec de módulo y test, incluidos REQ-018..020 | Matriz `docs/traceability.md` |
| T-010 | Secretos | Ningún secreto en el diff; referencias a variables de entorno | Secret scanning del CI + revisión manual |
| T-011 | Vendedores | Autoactivación sin aprobación previa, con datos de negocio completos y rol asignado solo por el backend (REQ-017). | `specs/phase-02-auth-users/spec.md` RF-05 y T-AUTH-09; pruebas de integración y seguridad previstas |

---

## 6. Criterios de aceptación

- [ ] **AC-001** El stack implementado coincide exactamente con la tabla de § 3.2 y con `constitution.md` § 2.
- [ ] **AC-002** El backend compila en .NET 10 y `dotnet format --verify-no-changes` pasa.
- [ ] **AC-003** Las reglas BR-001 a BR-013 de la constitución están reflejadas en requisitos y pruebas.
- [ ] **AC-004** No existen artefactos ni endpoints de carrito, menús, horarios o pasarela de pago.
- [ ] **AC-005** El logout revoca el JWT en Redis y el sistema falla cerrado si Redis no responde.
- [ ] **AC-006** El rate limiting es distribuido con Redis y responde `429` con el contrato de error.
- [ ] **AC-007** Las imágenes usan URLs presignadas generadas por el backend y moderación previa; ningún objeto del bucket es público.
- [ ] **AC-008** Las notificaciones push salen exclusivamente del backend con FCM Admin SDK.
- [ ] **AC-009** La infraestructura AWS se declara en Terraform y no hay cambios manuales en staging/producción.
- [ ] **AC-010** No hay secretos, connection strings, access keys ni service accounts en el repositorio.
- [ ] **AC-011** Existen pruebas de gobernanza T-001 a T-011 y la trazabilidad está actualizada.
- [ ] **AC-012** Las ediciones documentales vigentes de este spec quedan aprobadas por el equipo y registradas en `memoria.md` (v1.1 aprobada; v1.2/v1.3 pendientes de aprobación integral).
- [ ] **AC-013** La autoactivación de vendedor de DEC-002 respeta la constitución § 12 y se verifica con T-011.

---

## 7. Límites

Este spec **no**:

- Define endpoints, DTOs, esquemas de MongoDB, índices ni contratos HTTP; eso corresponde a los specs de módulo.
- Define por su cuenta la política técnica de datos compartidos DEC-003 o almacenamiento/purga DEC-005; decisiones funcionales ya constan en `docs/constitution.md` v2.4.
- Autoriza escribir código de producción sin un `spec.md` de módulo aprobado.
- Define umbrales numéricos de rendimiento o cobertura; esos irán en un spec NFR.
- Cubre interfaz de usuario, aplicación móvil, runbooks de operación o soporte.
- Define seed de datos, migraciones ni estrategias de carga masiva.
- Sustituye a `constitution.md`; es un documento subordinado y operativo.

---

## 8. Aprobación y control de cambios

| Campo | Valor |
|---|---|
| Estado | **Borrador de conciliación** (v1.1 aprobada) |
| Fecha | 2026-11-06 |
| Versión | 1.3 |
| Autor | AGENT-BE-01 (Architect) |
| Revisores | AGENT-BE-04 (Security Reviewer), AGENT-BE-06 (Memory Keeper) |
| Aprobado por | Equipo UTTT-GO (v1.1); v1.2 pendiente de aprobación integral |
| Próxima revisión | Al cambiar la constitución o al cerrar la fase 00 |

### 8.1 Registro de versiones

| Versión | Fecha | Cambio | Aprobado por |
|---|---|---|---|
| 1.0 | 2026-10-29 | Formalización del spec constitucional: principios, stack obligatorio, tests, criterios de aceptación y límites | Equipo UTTT-GO |
| 1.1 | 2026-11-02 | Alineación con `docs/constitution.md` v2.2: REQ-017 incorpora DEC-002 ratificada; DEC-003 y DEC-005 continúan abiertas. | Equipo UTTT-GO (confirmación comunicada por la usuaria) |
| 1.2 | 2026-11-03 | REQ-011/013/014 y nuevos REQ-018..020 alineados a decisiones funcionales de producto propio, eliminación, chat y recuperación por enlace. | Pendiente aprobación integral |
| 1.3 | 2026-11-06 | Alineación con `docs/constitution.md` v2.4/DEC-018: nube AWS (EC2 + EIP + Nginx/2 réplicas Compose), S3 privado + presigned, Redis contenerizado, secretos por variables de entorno; REQ-003/004/009/015, tabla § 3.2, T-006/T-010 y AC-007/009/010/012 actualizados. | Pendiente aprobación integral |

### 8.2 Reglas de control de cambios

1. Cambiar un principio, una regla de negocio o el stack exige actualizar primero `constitution.md` y luego este spec.
2. Todo cambio en este documento requiere nueva versión, registro en `memoria.md` y actualización de la trazabilidad.
3. No se permiten ediciones silenciosas ni excepciones por módulo sin enmienda aprobada.
4. Los agentes deben rechazar tareas que contradigan este spec o la constitución.
