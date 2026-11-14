# API-041 — Moderar reseña · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-041 `PATCH /api/v1/admin/reviews/{reviewId}/moderation` según [trazabilidad](../../../docs/traceability.md), vinculada a `phase-10-admin-moderation` (no existe como carpeta; «Pendiente de fase»). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente; la fila de trazabilidad no registra RF/AC originales.

## Estado de aprobación

PENDIENTE: no hay fase-10, ni RF/AC, ni contrato de estados de moderación, ni payload aprobado; la [API-030](../api-030-create-review/spec.md) declara la moderación de texto explícitamente **no aprobada**. La única regla vigente es fronteriza: Admin consume el contrato del dueño de Reviews, nunca su colección. Nada de este documento autoriza código.

## Responsabilidad única

Cambiar el estado de moderación de **una reseña** desde Admin mediante contrato publicado por el módulo dueño de Reviews. No crea ni edita reseñas ([API-030](../api-030-create-review/spec.md)), no las lista ([API-031](../api-031-list-seller-reviews/spec.md)), no modera imágenes (BR-007 es de Media), no toca cuentas ([API-040](../api-040-update-user-status/spec.md)) ni lee colecciones de Reviews directamente.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md) | API-041, corte 2026-11-08 | Método/ruta; «Pendiente de fase» y contrato del dueño exigido. |
| [API-030](../api-030-create-review/spec.md) | API-030 | Moderación de texto **no aprobada**; no inventar payload. |
| [API-031](../api-031-list-seller-reviews/spec.md) | API-031 | Lector de reseñas; frontera de solo lectura. |
| [Constitución](../../../docs/constitution.md) | §§ 4/5/6 v2.6 | Fronteras modulares, identidad y seguridad; sin contrato de moderación. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Admin consume contrato del dueño de Reviews, no su colección | [Trazabilidad](../../../docs/traceability.md) API-041 | Interfaz de moderación aún inexistente. |
| Moderación de texto sin aprobar ⇒ sin payload ni estados inventados | [API-030](../api-030-create-review/spec.md) | Todo DTO/estado/código queda pendiente de fuente. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Reviews/` ni contrato de moderación ni endpoint Admin de reseñas; el módulo `Admin` solo implementa lectura de cuentas (API-039). Ninguna acción de moderación está implementada.

## Funcionalidad requerida faltante

Fase-10 como fuente: estados de moderación y transiciones, DTO del PATCH, códigos ante inexistente/inválido, contrato de escritura publicado por Reviews, auditoría de la acción, interacción con visibilidad de reseñas en API-031, índices y evidencia de pruebas.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta PATCH del inventario | API-041 (trazabilidad) | Bearer Admin + `{reviewId}` + cuerpo de estado | Estados, DTO y validación |
| Escritura vía contrato del dueño | Trazabilidad API-041 | Interfaz `IModerationWriter` de Reviews (prevista) | Nombre y forma del contrato |
| Reseña inexistente sin divulgación | § 6 | `404` uniforme (candidato) | Código exacto |
| Dependencias caídas sin cambio parcial | § 6 | `503` fail-closed | Orden transaccional |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT/blacklist y rate limit distribuido fail-closed; rol `Admin` del perfil local, no del JWT; autorización por recurso; auditoría obligatoria de la moderación sin PII ni contenido de la reseña en logs; sin acceso cruzado a colecciones; error único con `correlationId`.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| ADMR-01 | Admin local `Active` y reseña existente con estado distinto al destino | PATCH moderation | `200` con estado actualizado vía contrato del dueño (candidato) | API-041 | Sin fuente ni implementación | Contrato pendiente de fase-10 |
| ADMR-02 | `reviewId` inexistente o reseña fuera del alcance | PATCH | `404` uniforme sin escritura | API-041 | Sin implementación | Código pendiente de acta |
| ADMR-03 | Token ausente/inválido/revocado o sin rol local Admin | PATCH | `401`/`403` sin escritura | § 6 | Sin implementación | Fail-closed constitucional |
| ADMR-04 | Cuerpo ausente, estado inválido o transición no permitida | PATCH | `400`/`409` uniforme sin cambio | API-041 | Sin implementación | Estados pendientes de fuente |
| ADMR-05 | Redis/Mongo/auditoría caídos o rate limit excedido | PATCH | `429`/`503` fail-closed sin cambio parcial | § 6 | Sin implementación | Fail-closed constitucional |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-041 (sin RF/AC publicados) | [Trazabilidad](../../../docs/traceability.md) | ADMR-01..05 | DEV-API041-01..04 |
| Regla de frontera Reviews (API-030/031) | [API-030](../api-030-create-review/spec.md) | ADMR-01 | DEV-API041-02 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| ADMR-01 | NO_IMPLEMENTADO | Sin fase-10, sin contrato de Reviews ni endpoint | DEV-API041-01/02 |
| ADMR-02 | BLOQUEADO | Código ante reseña inexistente sin acta | DEV-API041-01 |
| ADMR-03 | NO_IMPLEMENTADO | Autorización de mutación sin implementar | DEV-API041-03/04 |
| ADMR-04 | BLOQUEADO | Estados y transiciones de moderación sin definir | DEV-API041-01 |
| ADMR-05 | NO_IMPLEMENTADO | Auditoría y tasa de mutación sin implementar | DEV-API041-03/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-11 | Documental | [Trazabilidad](../../../docs/traceability.md) fila API-041: «Pendiente de fase» | Contrato, código ni pruebas. |
| 2026-11-11 | Documental | [API-030](../api-030-create-review/spec.md): moderación de texto no aprobada | Estados ni DTO de moderación. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| Ausencia de fase-10 | [Trazabilidad](../../../docs/traceability.md) | Crear fase-10-admin-moderation vs trazar la función en fase Reviews (pendiente) | DEV-API041-01..04 | Equipo/fase Reviews: definir fuente de estados y transiciones. |
| Moderación de texto sin aprobar | [API-030](../api-030-create-review/spec.md) | Estados manuales Admin vs revisión asistida | DEV-API041-01/02 | Negocio: política de contenido y estados; sin payload inventado. |
| Visibilidad según moderación | [API-031](../api-031-list-seller-reviews/spec.md) | Filtrar reseñas moderadas en listados vs exponer todas con marca | DEV-API041-02 | Reviews: contrato de lectura coherente con la moderación. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos): sin fuente de contrato (fase-10 inexistente) y con moderación de texto sin aprobar; ADMR-01..05 requieren estados, DTO y contrato de Reviews antes de código. Faltan pruebas de moderación, guardas de rol y evidencia alguna.
