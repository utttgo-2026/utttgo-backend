# API-039 — Listar usuarios y vendedores · spec

Estado de aprobación de la ficha: PARCIAL
Estado operativo: IMPLEMENTED_PENDING_VERIFICATION

## Identificación y autoridad

API-039 `GET /api/v1/admin/users` según [trazabilidad](../../../docs/traceability.md) y el trío histórico [phase-02](../../phase-02-auth-users/api-039-list-users/spec.md) v0.2.0 (2026-11-04), cuya ratificación local se compila aquí sin sustituirlo. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

PARCIAL: la responsable técnica ratificó el contrato local el 2026-11-04 (phase-02 v0.2.0) para SDD; no equivale a aprobación integral del equipo ni a autorización de despliegue. TASK-AUTH-007/008 siguen abiertos y AC-005 exige además API-040. El Excel original permanece «Propuesta a validar»; las precisiones locales no lo modifican.

## Responsabilidad única

Leer un listado acotado, buscable y paginado de cuentas para gestión de la comunidad (RF-07). No suspende ni reactiva cuentas (corresponde [API-040](../api-040-update-user-status/spec.md)), no asigna roles, no exporta datos, no consulta colecciones de Sellers ni responde solicitudes de vendedores.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md) | API-039, corte 2026-11-08 | Fila del inventario; verificación real sin acreditar. |
| [Phase-02 API-039](../../phase-02-auth-users/api-039-list-users/spec.md) | v0.2.0, 2026-11-04 | Contrato local ratificado y 8 escenarios; no es aprobación integral. |
| [QA local](../../../docs/qa-api-039.md) | 2026-11-04 | Resultados con dobles; no acredita integración externa. |
| [Phase-02 general](../../phase-02-auth-users/spec.md) | v1.5, RF-07, RNF-01/03/04/07/08/09/12/17, AC-005 | Requisitos y gates; AC-005 compartida con API-040. |
| [Constitución](../../../docs/constitution.md) | §§ 4/5/6/14, v2.6 | Fronteras, JWT/Redis fail-closed, paginación y error único. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Listar, buscar, paginar y exponer rol/estado | RF-07 (phase-02) | Solo cuatro campos: `id`, `displayName`, `roles`, `accountStatus`. |
| Búsqueda solo en `displayName` normalizado, máximo 100 caracteres; nunca por email | Ratificación 2026-11-04 | Normalización exacta por concretar sin alterar el alcance. |
| `page` ≥ 1 y `pageSize` 1..50 (límite inicial 50), ambos obligatorios | Ratificación 2026-11-04 | Límite configurable no secreto; sin colecciones ilimitadas. |
| Auditoría obligatoria previa al `200`; su fallo ⇒ `503` | Ratificación 2026-11-04 | Sink real sin configurar (`UnconfiguredAdminUsersAudit`). |

## Comportamiento implementado candidato

Implementación local autorizada el 2026-11-04 y verificada con dobles: `GET /api/v1/admin/users` con controlador, servicio, contrato, validador, gate de rate limit y dependencias sin configurar en el módulo `Admin`; Identity publica el contrato de lectura `IAdminUserListReader` separado de `FindBySubjectAsync`. Los adaptadores reales de JWT/Redis/Supabase/auditoría están sin configurar y fallan cerrado (`503`); el `200` real en AWS no está acreditado.

## Funcionalidad requerida faltante

Verificación del modelo real (`Suspended`, `createdAt`, normalización de `displayName`), índices y consulta Mongo detrás del contrato de Identity, adaptadores JWT/Redis/Supabase reales, sink de auditoría persistente, rate limit distribuido real entre instancias, evidencia de `200` con servicios no productivos y cierre de AC-005 (requiere API-040).

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir o verificar |
|---|---|---|---|
| Query `GET /api/v1/admin/users` | RF-07, ratificación 2026-11-04 | `search`, `role`, `accountStatus`, `page`, `pageSize`, `sortBy` con valores de lista blanca | Soporte real de los tres estados y de `createdAt` |
| `200` paginado de cuatro campos | RF-07, ratificación | `items` + `page`, `pageSize`, `total` filtrado antes de paginar | Snapshot ante concurrencia (no prometido) |
| `400` por query inválida | Ratificación | Enum/duplicado/fuera de rango antes de consultar | Códigos de detalle interno |
| `401`/`403` | Ratificación, § 6 | Sin rol local Admin ⇒ `403`; sesión no habilitada ⇒ `401` | No divulgar causa en el cuerpo |
| `429`/`503` | Ratificación | Rate limit y dependencias caídas fail-closed | Cuotas y partición operativas por configurar |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT con firma/issuer/audience/exp y blacklist Redis fail-closed; rol `Admin` y estado `Active` tomados del perfil local, nunca del JWT; autorización por recurso; rate limit distribuido `429`/`503`; auditoría sin PII, tokens ni filas; sin `email` ni datos de Sellers en la respuesta; error único con `correlationId`; logs sin búsqueda en bruto.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| ADMU-01 | JWT válido no revocado, perfil Active con rol local `Admin` | Pide página dentro del límite | `200` con solo campos permitidos, orden estable y metadatos de paginación | RF-07 | Verificada con dobles (qa-api-039) | Ratificada 2026-11-04; código local |
| ADMU-02 | Admin y filtros combinados `search`, `role`, `accountStatus` | Consulta | Solo coincidencias; `total` filtrado antes de paginar; sin coincidencias ⇒ `items` vacío y `total: 0` | RF-07 | Verificada con dobles | Ratificada 2026-11-04; código local |
| ADMU-03 | Token ausente/inválido/expirado/revocado o confirmación/estado local inválidos | Consulta | `401` sin filas; no se consulta el listado antes del gate | RF-07 | Verificada con dobles | Fail-closed constitucional |
| ADMU-04 | Token de Buyer/Seller sin rol Admin local, aunque el JWT diga `Admin` | Consulta | `403` sin filas; cambios locales afectan la siguiente consulta | RF-07 | Verificada con dobles | Rol local exigido |
| ADMU-05 | `page` inválido, `pageSize` fuera de 1..50, `search` > 100, filtro/sort desconocido o parámetro duplicado | Consulta | `400` con `correlationId` y sin consulta administrativa | RF-07 | Verificada con dobles | Ratificada 2026-11-04 |
| ADMU-06 | Rate limit excedido, o Redis/confirmación/almacén caídos | Consulta | `429` o `503` fail-closed; ningún doble habilita producción | RF-07 | Rate limit real pendiente | Fail-closed constitucional |
| ADMU-07 | `sortBy` ausente o permitido | Ordena | `createdAtDesc` por defecto o `createdAtAsc` y desempate estable por ID | RF-07 | Verificada con dobles | Ratificada 2026-11-04 |
| ADMU-08 | Lectura administrativa | Registra auditoría | Actor/acción/UTC correlacionados sin PII; si falla el registro obligatorio ⇒ `503` sin listado | RF-07 | Sink real pendiente | Ratificada 2026-11-04 |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-039, RF-07, RNF-01/03/04/07/08/09/12/17 | [Phase-02 general](../../phase-02-auth-users/spec.md) | ADMU-01..07 | DEV-API039-02..04 |
| AC-005 (compartida con API-040) | [Phase-02 general](../../phase-02-auth-users/spec.md) | ADMU-04 | DEV-API039-06 |
| TASK-039-001/002/003/010..013/020/021 | [Phase-02 task](../../phase-02-auth-users/api-039-list-users/task.md) | ADMU-08 | DEV-API039-01/05/06 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| ADMU-01 | IMPLEMENTADO_PROBADO | `200` real en AWS sin acreditar (integración pendiente) | DEV-API039-06 |
| ADMU-02 | IMPLEMENTADO_PROBADO | Normalización de `displayName` sin verificar en modelo real | DEV-API039-01 |
| ADMU-03 | IMPLEMENTADO_PROBADO | Adaptadores JWT/Supabase reales sin configurar | DEV-API039-06 |
| ADMU-04 | IMPLEMENTADO_PROBADO | AC-005 permanece abierta hasta API-040 | DEV-API039-06 |
| ADMU-05 | IMPLEMENTADO_PROBADO | Soporte de `Suspended` y `createdAt` sin verificar | DEV-API039-01 |
| ADMU-06 | SIN_EVIDENCIA | Rate limit distribuido real entre réplicas sin probar | DEV-API039-05 |
| ADMU-07 | IMPLEMENTADO_PROBADO | Índices Mongo y `explain` sin medir | DEV-API039-06 |
| ADMU-08 | SIN_EVIDENCIA | `UnconfiguredAdminUsersAudit` sin sink persistente | DEV-API039-05 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-04 | QA local con dobles | [qa-api-039](../../../docs/qa-api-039.md): resultado de suites unitarias, endpoint y seguridad en verde | Integración externa ni `200` real |
| 2026-11-04 | Documental | [Phase-02 API-039](../../phase-02-auth-users/api-039-list-users/spec.md) v0.2.0: contrato ratificado localmente | Aprobación integral ni despliegue |
| 2026-11-08 | Operación documental | [Trazabilidad](../../../docs/traceability.md): fila API-039 operativa con dobles | Verificación con servicios reales |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| TASK-AUTH-007/008 abiertos | [Phase-02 task](../../phase-02-auth-users/api-039-list-users/task.md) | Aprobación integral del equipo vs ratificación local vigente | DEV-API039-06 | Equipo: aprobación integral antes de despliegue. |
| AC-005 compartida | [Phase-02 general](../../phase-02-auth-users/spec.md) | Cerrar con API-039 sola (prohibido) vs esperar a API-040 | DEV-API039-06 | Gate de fase: exige API-040 y su evidencia. |
| Auditoría sin sink | Ratificación 2026-11-04 | Adaptador real en Admin vs interfaz `UnconfiguredAdminUsersAudit` fail-closed | DEV-API039-05 | Identidad/Admin: diseño del registro persistente sin PII. |

## Criterios de preparación para desarrollo

Alcance [IMPLEMENTED_PENDING_VERIFICATION](../../../docs/sdd-guia-api-atomica.md#estados-operativos): implementación local con dobles verificada el 2026-11-04; ADMU-01..08 quedan por verificar con servicios no productivos, adaptadores reales y sink de auditoría. Faltan TASK-039-001/020/021 y AC-005 (junto con API-040).
