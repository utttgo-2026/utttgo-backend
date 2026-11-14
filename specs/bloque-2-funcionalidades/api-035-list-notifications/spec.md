# API-035 — Listar notificaciones propias · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-035 `GET /api/v1/notifications`, módulo `Notifications`, según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

Trazabilidad confirma paginación y alcance al usuario propio; constitución §8 obliga push FCM Admin SDK solo backend, resultado de eventos de dominio e idempotencia visible; §5 obliga `page`/`pageSize`. [Migración](../../../docs/migracion-sdd.md) deja fase 06 sin spec histórico aprobado. No está definido si cada push origina registro persistente, qué retener/exponer, TTL, filtros, orden, HTTP o DTO; no asumirlo.

## Responsabilidad única

Leer bandeja persistente de notificaciones **si se aprueba su modelo** para el usuario autenticado, con paginación y privacidad. No crear hechos de Orders/Chat, no enviar FCM al consultar, no administrar deviceTokens y no marcar como leídas (API-036). Ningún payload de chat/Orders sensible se devuelve sin contrato.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-008, §§4/5/6/8/14 v2.6; DEC-011 | FCM server-only, eventos, idempotencia, listas seguras. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-035, 2026-11-11 | GET paginado propio; fase 06 pendiente. |
| [Fase 04](../../phase-04-orders-cash/spec.md), [fase 07](../../phase-07-chat-realtime/spec.md) | RF-ORD-29 (borrador); DEC-005 | Publicador de hechos propuesto; chat temporal, no payload push autorizado. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Lectura paginada de notificaciones propias | [Trazabilidad API-035](../../../docs/traceability.md), [Constitución §5](../../../docs/constitution.md) | No aprueba colección/TTL/DTO ni acceso público. |
| Push solo backend FCM y derivado de hechos de dominio, sin duplicados visibles | BR-008, [Constitución §8](../../../docs/constitution.md) | No equiparar entrega FCM a entrada de bandeja sin decisión. |

## Comportamiento implementado candidato

No hay `src/UTTTGO.Api/Modules/Notifications/` ni tests Notification, adaptador FCM, bandeja o endpoints. Propuestas `INotificationPublisher` de Orders v0.1 sin aprobación; ninguna evidencia de notificación entregada.

## Funcionalidad requerida faltante

Modelo/lector persistente propio Notifications y política de emisión idempotente desde hechos aprobados; interfaz para Orders/Chat sin acceso de Notifications a sus colecciones. Índice `recipientUserId`+orden si ratificado, estado leído para API-036 y tratamiento de expiración/borrado de cuenta BR-011/DEC-003. Definir si errores FCM afectan persistencia de bandeja; no bloquear lectura propia por FCM caído salvo política aprobada.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | GET, `page`/`pageSize` y usuario propio; sujeto servidor | JWT, página ≥1, límite configurable; `unread` filtro opcional | Defaults, filtros, estado cuenta y orden. |
| Éxito | Solo notificaciones propias, nunca lista ilimitada | `200 {items,page,pageSize,total}` con ID, resumen seguro, readAt y fecha | DTO/campos, retención, orden, vacía, estado leído y payload permitido. |
| Error | Único `correlationId`, Redis fail-closed para rate limit/blacklist | `400/401/429/503` candidatos | HTTP exacto, Mongo caído/FCM caído, filtrado de referencias ya borradas. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist y rate limit Redis fail-closed; filtrar por recipientUserId del perfil local **antes** de paginar. No aceptar recipientUserId del query, exponer deviceTokens/FCM secrets ni incluir texto de chat purgado o datos de terceros. Reintentos de hechos no duplican registros si se aprueba persistencia; logs con correlationId sin PII/tokens.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| NOTL-01 | Dos usuarios con registros separados | GET paginado | Solo registros propios y página estable según acta | API-035 | Integración pendiente | Paginación vigente, DTO pendiente |
| NOTL-02 | Evento de Orders duplicado o push FCM fallido | GET | No duplica visibles si existe bandeja; política de fallo aún por definir | BR-008, §8 | Doble publicador pendiente | Modelo persistencia pendiente |
| NOTL-03 | JWT revocado, page inválida o Redis/Mongo caídos | GET | No hay filtración ni bypass de rate limit; error único | §6, API-035 | Seguridad pendiente | HTTP pendiente |
| NOTL-04 | Mensaje/pedido vinculado expiró o usuario eliminó cuenta | GET | Sin payload sensible residual conforme privacidad aprobada | BR-011/012, DEC-005 | Integración pendiente | Retención/DEC-003 pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| API-035 (sin RF/AC fase 06 publicados) | [Trazabilidad](../../../docs/traceability.md) | NOTL-01..04 | DEV-API035-01..04 |
| BR-008/011/012, DEC-011; RF-ORD-29 borrador | [Constitución](../../../docs/constitution.md), [fase 04](../../phase-04-orders-cash/spec.md) | NOTL-02/04 | DEV-API035-01/03/04; API-036/005 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| NOTL-01/03 | NO_IMPLEMENTADO | Sin bandeja/lector/guard ni pruebas | DEV-API035-02/04 |
| NOTL-02 | BLOQUEADO | Modelo bandeja/evento/FCM idempotente no aprobado | DEV-API035-01/03 |
| NOTL-04 | OTRA_FICHA | Chat/Orders y borrado API-005; filtro propio pendiente aquí | DEV-API035-04; API-005/037/038 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Trazabilidad](../../../docs/traceability.md), módulo ausente | Persistencia, FCM, lectura ni tests reales. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad |
|---|---|---|---|---|
| API-035 fase 06 sin spec | [Migración](../../../docs/migracion-sdd.md) | Bandeja persistente/TTL y evento idempotente, filtros, DTO, HTTP sin aprobación | DEV-API035-01..04 | Producto/Notifications/seguridad: acta/Excel. |
| BR-012, DEC-003/005 | [Constitución](../../../docs/constitution.md) | Retener resumen vs prohibir contenido chat expurgado; eliminación propia | NOTL-04, API-005 | Privacidad/Chat/Notifications sin inventar política. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta bandeja/evento aprobados; NOTL-01..04 requieren aislamiento, paginación, privacidad y pruebas locales sin FCM real.
