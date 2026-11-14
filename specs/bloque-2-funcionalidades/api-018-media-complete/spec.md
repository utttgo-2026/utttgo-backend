# API-018 — Completar subida y moderar imagen · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-018 `POST /api/v1/media/{assetId}/complete`, módulo `Media`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-007 y constitución § 7 obligan moderación Azure AI Content Safety **antes** de publicación, `Approved` solo si seguro y `Rejected` con borrado S3 si inseguro. Fase 05 no tiene spec aprobada; HTTP, thresholds, reintentos y fallos parciales requieren contrato propio. Sprint 2 de Catalog publica sin imágenes (DEC-019).

## Responsabilidad única

Confirmar la subida de un asset `PendingUpload` propio registrado por API-017, comprobar objeto S3 y moderarlo; actualizar estado en Media. No recibe imagen en el body, no publica producto ni modifica `products`, no entrega GET presigned (lectura futura del dueño Media).

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-007, § 7, DEC-018/019 | S3 privado, Content Safety, flujo `PendingUpload`→`Approved/Rejected` y fail-closed. |
| [Fase 03](../../phase-03-catalog-offers/spec.md) | RF-11 | Solo assets Approved pueden asociarse a publicaciones; Catalog Sprint 2 sin media. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-018 | Método/ruta; sin código ni spec Media aprobada. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Tras upload, backend modera con Content Safety; seguro `Approved`, inseguro borrar objeto y `Rejected` | [Constitución § 7](../../../docs/constitution.md) | Umbrales/severidades, orden de acciones y error parcial no fijados. |
| Sin moderación aprobada o proveedor fallido: imagen no publicada | BR-007, [constitución § 7](../../../docs/constitution.md) | Reintento/estado tras timeout pendiente. |

## Comportamiento implementado candidato

No existen `Modules/Media/`, colección `mediaAssets`, adaptador Content Safety ni pruebas relacionadas. Infraestructura S3 privada no constituye moderación; Catalog Sprint 2 no tiene imágenes y no debe actuar como sustituto de Media.

## Funcionalidad requerida faltante

Servicio idempotente de confirmación, comprobación de objeto/metadata S3 contra intención API-017, autorización propietario, invocación segura Content Safety y transición persistente con coordinación S3/Mongo (rechazo y limpieza). Contrato consumidor Catalog solo permite `Approved`, sin lectura directa de `mediaAssets`.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato, NO ratificado | Falta decidir |
|---|---|---|---|
| Entrada | JWT válido; `assetId` de asset propio `PendingUpload`; objeto subido por cliente a staging S3 | POST sin bytes; body vacío como opción | ID/formato, propietario/uso, expiración, metadata S3 comprobada, repetición. |
| Éxito/resultado | Seguro `Approved`; inseguro `Rejected` y borrar S3 | `200` con mediaAssetId/status o `202` pendiente solo como alternativas | HTTP/DTO, umbral de moderación, transición por reintento. |
| Error | Fail-closed Content Safety/S3/Mongo/Redis; error `correlationId` | `400/401/403/404/409/429/503` candidatos | Objeto ausente, conflicto, estado tras timeout, borrado S3 fallido y compensación. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT/blacklist, propietario de `mediaAssets` (no solo rol), rate limit fail-closed, comprobar objeto/tipo/tamaño antes de moderar y negar sustitución de objeto por tercero; estado atómico/compare-and-swap para doble complete; nunca registrar bytes, URLs completas, tokens ni claves Content Safety. Nada publicado sin `Approved`; S3 privado con borrado de rechazado.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| CMP-01 | Asset propio PendingUpload y objeto válido/seguro | POST complete | Media modera y marca `Approved`; aún no se publica producto | BR-007 | Unit e integración mock Content Safety por crear | Flujo aprobado, DTO pendiente |
| CMP-02 | Objeto inseguro | POST complete | Rechaza y borra objeto S3, no aparece publicado | BR-007 | S3 emulado por crear | Flujo aprobado |
| CMP-03 | Proveedor caído/objeto faltante/borrado falla | POST complete | No se aprueba ni se publica; resultado recuperable según acta | BR-007 | Integración de fallos por crear | Reintentos pendientes |
| CMP-04 | Asset ajeno/ya procesado o JWT revocado | POST complete | No modera asset ajeno ni duplica transición; respuesta ratificada | BR-009, BR-007 | Seguridad por crear | HTTP/idempotencia pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| BR-007, § 7, API-018 | [Constitución](../../../docs/constitution.md), [trazabilidad](../../../docs/traceability.md) | CMP-01/02/03/04 | DEV-API018-01/02/03/04 |
| RF-11 | [Fase 03](../../phase-03-catalog-offers/spec.md) | CMP-01/02/03 | Catalog/API-013, API-017 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| CMP-01 | NO_IMPLEMENTADO | Sin lector asset, objeto ni Content Safety | DEV-API018-02/03 |
| CMP-02 | NO_IMPLEMENTADO | Sin borrado S3 y rechazo persistente | DEV-API018-03 |
| CMP-03 | BLOQUEADO | Fallos/reintentos sin protocolo aprobado | DEV-API018-01/03/04 |
| CMP-04 | BLOQUEADO | HTTP, propiedad/idempotencia por aprobar y probar | DEV-API018-01/02/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Constitución § 7](../../../docs/constitution.md), ausencia de `Modules/Media` y tests | Moderación real ni contrato individual aprobado. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| Ficha Media fase 05 | [Migración](../../../docs/migracion-sdd.md) | Umbral Content Safety, idempotencia, timeout, compensación S3/Mongo, HTTP | DEV-API018-01/02/03/04 | Producto, seguridad y Media: acta fase 05. |
| DEC-019 | [Constitución](../../../docs/constitution.md) | Sin imágenes Sprint 2 | DEV-API018-02 | Asociación Catalog posterior por contrato dueño, no aquí. |

## Criterios de preparación para desarrollo

Alcance: completar/moderar asset propio, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta contrato Media fase 05. Aceptación: seguro aprobado, inseguro rechazado y borrado, proveedor caído fail-closed, ninguna imagen no moderada publicada.
