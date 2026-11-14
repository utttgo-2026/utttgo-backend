# API-042 — Reportes semanales administrativos · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-042 `GET /api/v1/admin/reports/weekly` según [trazabilidad](../../../docs/traceability.md), vinculada a `phase-10-admin-moderation` (no existe como carpeta; «Pendiente de fase», alcance de métricas y autorización **por definir**). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente; la fila no registra RF/AC originales.

## Estado de aprobación

PENDIENTA la función: no hay fase-10, ni definición de métricas, ni parámetros, ni autorización aprobada; la fila de trazabilidad la declara «Pendiente de fase». Nada de este documento autoriza código; solo se fija la frontera de seguridad y dependencia entre módulos.

## Responsabilidad única

Entregar un reporte **semanal agregado** al rol Admin. No expone registros individuales ni PII, no modera reseñas ([API-041](../api-041-moderate-review/spec.md)), no lista cuentas ([API-039](../api-039-list-users/spec.md)) ni sustituye métricas de los módulos dueños (Orders, Reviews, Sellers); Admin consume contratos de lectura, no sus colecciones.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md) | API-042, corte 2026-11-08 | Método/ruta; alcance y autorización por definir. |
| [Constitución](../../../docs/constitution.md) | §§ 4/5/6/14 v2.6 | Fronteras, identidad, paginación y error único; sin métricas. |
| [API-039](../api-039-list-users/spec.md) | API-039 | Precedente de autorización Admin y auditoría de lectura. |
| [Fase 04 Orders](../../phase-04-orders-cash/spec.md) | Fase 04 v0.1 | Datos de pedidos aún propuesta; no fuente de métricas aprobada. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Admin consume contratos de lectura de los módulos dueños | [Trazabilidad](../../../docs/traceability.md) API-042, §§ 4/5 | Sin colecciones cruzadas ni métricas propias en Admin. |
| Autorización por recurso con rol local `Admin` | [API-039](../api-039-list-users/spec.md) precedente | Autenticación por definir para reportes. |

## Comportamiento implementado candidato

No existe endpoint de reportes ni recolección de métricas; `Admin` solo implementa el lector de API-039. No hay contratos de agregación publicados por Orders, Reviews ni Sellers.

## Funcionalidad requerida faltante

Definición del alcance (qué métricas, ventana de semana, zona horaria), parámetros de entrada, autorización exacta, contratos de lectura de cada módulo dueño, agregación sin PII, política de caché/TTL, formato de respuesta, códigos de error y pruebas.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta GET del inventario | API-042 (trazabilidad) | Bearer Admin + query de semana (candidato) | Parámetros y ventana exacta |
| Solo rol Admin | API-039 precedente | `401`/`403` sin datos | Autenticación propia de reportes |
| Respuesta agregada sin PII | §§ 4/5/6 | `200` con métricas (candidato) | Métricas, unidades y orden |
| Parámetros inválidos o dependencias caídas | § 14 | `400` o `503` sin datos parciales | Códigos exactos |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT/blacklist y rate limit distribuido fail-closed; rol `Admin` del perfil local, no del JWT; sin PII, emails, tokens ni identificadores individuales en la respuesta; sin caché de datos sensibles; error único con `correlationId`; logs de lectura sin payloads de reporte.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| RPTW-01 | Admin local `Active` y parámetros de semana válidos | GET reports/weekly | `200` con métricas agregadas (candidato) | API-042 | Sin fuente ni implementación | Autorización por definir |
| RPTW-02 | Token ausente/inválido/revocado o sin rol local Admin | GET reports/weekly | `401`/`403` sin datos | § 6 | Sin implementación | Fail-closed constitucional |
| RPTW-03 | Parámetro de semana ausente, fuera de rango o en el futuro | GET reports/weekly | `400` con `correlationId`, sin consulta | API-042 | Parámetros por definir | HTTP pendiente |
| RPTW-04 | Redis/Mongo caídos o rate limit excedido | GET reports/weekly | `429`/`503` fail-closed sin datos parciales | § 6 | Sin implementación | Fail-closed constitucional |
| RPTW-05 | Respuesta construida con datos de varios módulos | Entrega el reporte | Solo agregados autorizados, sin PII ni registros individuales | §§ 4/5/6 | Sin implementación | Frontera de módulos |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-042 (sin RF/AC publicados) | [Trazabilidad](../../../docs/traceability.md) | RPTW-01..05 | DEV-API042-01..04 |
| Fronteras §§ 4/5/6 (precedente API-039) | [Constitución](../../../docs/constitution.md) | RPTW-02/05 | DEV-API042-02/03 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| RPTW-01 | NO_IMPLEMENTADO | Sin fase-10, sin métricas definidas ni recolección | DEV-API042-01/02 |
| RPTW-02 | NO_IMPLEMENTADO | Autorización de reportes sin implementar | DEV-API042-03/04 |
| RPTW-03 | BLOQUEADO | Parámetros y ventana de semana sin definir | DEV-API042-01 |
| RPTW-04 | NO_IMPLEMENTADO | Caché/agregación y degradación sin implementar | DEV-API042-02/04 |
| RPTW-05 | BLOQUEADO | Métricas por módulo sin contratos de lectura publicados | DEV-API042-01/02 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-11 | Documental | [Trazabilidad](../../../docs/traceability.md) fila API-042: «Pendiente de fase» | Métricas, código ni pruebas. |
| 2026-11-03 | Documental | [Fase 04 Orders](../../phase-04-orders-cash/spec.md) v0.1 | Datos de pedidos como propuesta, no fuente aprobada. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| Ausencia de fase-10 | [Trazabilidad](../../../docs/traceability.md) | Crear fase-10-admin-moderation vs definir reportes en otra fase | DEV-API042-01..04 | Equipo: fuente de alcance y métricas. |
| Métricas y ventana | Pendiente de fuente | Semana ISO vs local; recuento por módulo vs snapshot | DEV-API042-01/02 | Negocio: catálogo de métricas, unidades y horario. |
| Autorización de reportes | Pendiente de fuente | Mismo Admin que lectura de cuentas vs permiso dedicado | DEV-API042-03 | Seguridad: política de acceso a métricas agregadas. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos): sin fuente de métricas ni autorización aprobada; RPTW-01..05 requieren alcance, parámetros y contratos de lectura de los módulos dueños antes de código. Faltan pruebas de reporte, seguridad de agregación y toda evidencia de implementación.
