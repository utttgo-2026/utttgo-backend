# API-017 — Solicitar URL de subida de imagen · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-017 `POST /api/v1/media/upload-url`, módulo `Media`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con el Excel pendiente de fuente.

## Estado de aprobación

BR-007 y constitución § 7 obligan al flujo presigned S3 privado; no hay ficha Media de fase 05 aprobada. DTO, MIME/longitudes exactas, semántica HTTP y asociación a recurso requieren contrato propio. DEC-019 separa Media del Sprint 2 de Catalog sin imágenes.

## Responsabilidad única

Registrar una intención de subida y emitir una URL presignada PUT de **un objeto** en staging para actor autorizado. No sube bytes al backend, no modera (API-018), no publica imagen ni genera URL GET pública. API-016 sigue retirada.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-007, § 7, DEC-018/019 | S3 privado, PUT backend ≤ 5 min y `PendingUpload`; fase 05 pendiente. |
| [Fase 03](../../phase-03-catalog-offers/spec.md) | RF-11 | Media aprobada antes de asociar imagen; Sprint 2 sin imágenes. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-017 | Método/ruta inventariados; no existe spec Media aprobada. |

## Contrato aprobado

| Regla | Fuente expresa | Límite |
|---|---|---|
| Validar JWT, MIME, extensión, tamaño; registrar `mediaAssets.PendingUpload` antes de PUT | [Constitución § 7](../../../docs/constitution.md) | Listas/límites numéricos de archivos por aprobar. |
| Presigned PUT solo backend, un objeto staging, TTL ≤ 5 min, content-type y tamaño restringidos; bucket privado | [Constitución § 7](../../../docs/constitution.md) | Mecanismo verificable de tamaño PUT y política de expiración por fijar. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Media/` ni pruebas de Media; el bucket S3/IAM/CORS están declarados en `infra/terraform/`, pero **infraestructura existente no acredita** firmas PUT, subida, registro Mongo ni moderación. Ninguna imagen publicada en Catalog Sprint 2.

## Funcionalidad requerida faltante

Implementar intención, autorización y propiedad del asset en Media, colección `mediaAssets` del dueño Media, presigner S3 por Instance Profile y limpieza/expiración de intenciones; API-018 consumirá registro. Decidir política de CORS por origen permitido sin usar `*`, mediante Terraform por su dueño, no crear infraestructura desde esta ficha.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato de diseño, NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | POST con JWT válido; MIME, extensión y tamaño validables | JSON con metadatos de tipo/tamaño/nombre, nunca bytes ni `sellerId` confiado | Campos obligatorios, tamaño máximo, MIME/extensiones, propietario/uso autorizado y rate limit. |
| Éxito | Registrar `PendingUpload` y retornar URL PUT corta para objeto staging | `mediaAssetId`, `uploadUrl`, vencimiento y headers exigidos | Nombres JSON, HTTP, expiración exacta ≤ 5 min y manejo de fallo registro/firma. |
| Error | Error único `correlationId`, no exponer secretos, fail-closed si firma/Redis/Mongo caen | `400/401/403/429/503` solo alternativas | HTTP específico, repetición/huérfanos, validación técnica de tamaño PUT. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT firma/issuer/audience/exp/blacklist; rol y propietario locales según destino aprobado; rate limit distribuido fail-closed; S3 IAM mínimo, Block Public Access, firma un objeto PUT con content-type y límite comprobable de tamaño. No incluir claves/tokens/presigned completa en logs; metadatos e ID en Mongo, no bytes. Firma no autoriza publicar sin API-018.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| UPL-01 | Actor autorizado y metadatos válidos | POST intención | Se guarda asset propio `PendingUpload` y PUT un objeto ≤5 min | BR-007 | Unit/integración S3 doble por crear | Flujo aprobado; JSON pendiente |
| UPL-02 | MIME/extensión/tamaño inválidos o rol ajeno | POST intención | Se rechaza sin URL útil ni publicación | BR-007 | Seguridad por crear | Valores/HTTP pendientes |
| UPL-03 | Redis, Mongo o presigner fallan | POST intención | Error cerrado; sin asset aprobado ni URL reutilizable | Constitución § 7 | Integración fallos por crear | Reintentos pendientes |
| UPL-04 | Se reutiliza URL o se intenta leer público | PUT/GET S3 | Firma restringe objeto, tiempo, tipo/tamaño; ningún GET anónimo | BR-007 | Emulador/doble S3 por crear | Política técnica pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| BR-007, § 7, API-017 | [Constitución](../../../docs/constitution.md), [trazabilidad](../../../docs/traceability.md) | UPL-01/02/03/04 | DEV-API017-01/02/03/04 |
| RF-11, DEC-019 | [Fase 03](../../phase-03-catalog-offers/spec.md), [constitución](../../../docs/constitution.md) | UPL-01/02 | API-018/Media fase 05 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| UPL-01 | NO_IMPLEMENTADO | Sin intención, colección ni presigner | DEV-API017-02/03 |
| UPL-02 | BLOQUEADO | Lista MIME, límite, actor/HTTP sin ratificar | DEV-API017-01/04 |
| UPL-03 | NO_IMPLEMENTADO | Sin atomicidad de registro/firma ni fallo cerrado probado | DEV-API017-03/04 |
| UPL-04 | BLOQUEADO | Restricciones PUT tamaño/CORS y pruebas sin diseño aprobado | DEV-API017-01/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Constitución § 7](../../../docs/constitution.md), [trazabilidad](../../../docs/traceability.md), `Modules/Media` ausente | Endpoint ni presigned probado. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| Media fase 05 sin spec | [Migración](../../../docs/migracion-sdd.md) | JSON, dueño, TTL exacto, tipos y límites, mecanismo S3 de tamaño | DEV-API017-01/02/03/04 | Producto/API, seguridad y Media: acta de ficha fase 05 y prueba de firma. |
| DEC-019 | [Constitución](../../../docs/constitution.md) | Sprint 2 sin imágenes | DEV-API017-02 | No adelantar asociación a Catalog. |

## Criterios de preparación para desarrollo

Alcance: intención PUT Media, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta ficha fase 05 y autorización. Cierre técnico: prueba firmada con emulador/doble S3 de alcance/tamaño/TTL, `PendingUpload` propio persistido y fallo cerrado, sin publicar imagen.
