# API-013 — Publicar producto propio · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-013 `POST /api/v1/products`, módulo `Catalog`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-006/009 y RF-01..03 confirman vendedor propietario, precio/stock/ingredientes y homónimos entre vendedores. Sprint 2 de [fase 03](../../phase-03-catalog-offers/spec.md) no aprobado: JSON, códigos/validaciones y estado inicial aún por ratificar (TASK-CATALOG-002). Sin primera oferta obligatoria.

## Responsabilidad única

Crear publicación independiente del Seller autenticado, con precio propio en MXN y categoría válida. No crea producto maestro ni oferta, no crea categoría (API-020), no acepta identidad/precio de un pedido, no sube ni modera imágenes (Media API-017/018 fase 05).

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-004/006/007/009, DEC-016/019 | Propiedad, sin menú/imagen Sprint 2; imágenes futuras moderadas. |
| [Fase 03 spec](../../phase-03-catalog-offers/spec.md), [tareas](../../phase-03-catalog-offers/task.md) | RF-01/02/03/09/11, TASK-CATALOG-002 | Colección `products` propuesta; Sprint 2 aún sin aprobación. |
| [Trazabilidad](../../../docs/traceability.md) | API-013, T-CAT-01/04, TASK-CATALOG-010/013 históricos | Contrato anterior exige reconciliar oferta/imagen. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Producto de un Seller, sin oferta ni unicidad global nombre+categoría | BR-006, DEC-016, RF-02/03 | Reglas para duplicado del mismo vendedor por decidir. |
| Precio `decimal` MXN; stock entero `>= 0`; vendedor/rol derivados de perfil local | BR-009, [fase 03 § 4.3](../../phase-03-catalog-offers/spec.md) | Rangos máximos y DTO sin aprobar. |
| Sprint 2 publica sin imágenes | DEC-019.e, [fase 03](../../phase-03-catalog-offers/spec.md) | BR-007 obliga moderación antes de cualquier imagen futura. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Catalog/` ni colección `products`, controlador POST, DTO, validador, servicio ni pruebas. Sellers API-009 implementada proporciona perfil/identidad; Catalog debe consumir contrato del dueño y no escribir `sellers`/`users`.

## Funcionalidad requerida faltante

Modelo `products`, persistencia e índices de consultas reales de API-011/012; POST validado con vínculo a Seller activo y categoría activa API-019..022; invalidar caché de lectura. Media no existe, por lo que el contrato inicial sin imágenes necesita aprobación Sprint 2; no aceptar URL S3 ni `mediaAssetId` no aprobado.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato histórico | Falta decidir |
|---|---|---|---|
| Actor | JWT válido, Seller propio activo según política por aprobar | POST de un producto | Autoridad/estado Seller y fallos de dependencia; no aceptar `sellerId`, `roles`, `ownerUserId`. |
| Body | Nombre, descripción, ingredientes, `price` decimal MXN, `stock` entero ≥0 y categoría activa; sin imagen Sprint 2 | Campos `name,description,ingredients,categoryId,price,stock` son propuesta de [fase 03](../../phase-03-catalog-offers/spec.md), no JSON aprobado | Longitudes, moneda en DTO, stock cero, vacíos/null, categorías y whitelist. |
| Éxito | ID nuevo y pertenencia al vendedor derivado; homónimos distintos permitidos | `201` y DTO de producto candidatos | HTTP, DTO, estado inicial y campos publicados. |
| Errores | Contrato único con `correlationId`, rate limit distribuido | `400/401/403/404/409/429/503` candidatos | Mapeo exacto de seller/categoría, conflicto/caídas, retry/duplicados. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Verificar JWT/blacklist y rol en perfil local; resolver seller propio por interfaz Sellers, categoría por contrato dueño Catalog; precio decimal positivo (signo/rango a ratificar), stock no negativo; rechazar campo privilegiado/imagen ajena; Redis fail-closed. `products` lleva `_id`, `sellerId`, `categoryId`, datos, precio, stock, status/version UTC según modelo previsto; sin índice único global. No guardar imágenes en Mongo.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| CRT-01 | Seller activo, categoría activa | POST datos válidos sin imagen | Producto propio creado, vendedor derivado del backend | RF-01/03 | Integración por crear | Función confirmada, contrato pendiente |
| CRT-02 | Dos Sellers activos | Publican mismo nombre/categoría | Se crean dos productos independientes, sin oferta ni conflicto global | RF-02 | Mongo integración por crear | Regla confirmada |
| CRT-03 | Buyer, categoría inactiva o body privilegiado | POST | Se rechaza sin escritura con error ratificado | RF-01/09 | Seguridad por crear | HTTP pendiente |
| CRT-04 | Media ausente/asset no aprobado o Redis caído | POST con imagen/caída | No se publica imagen; fallo crítico fail-closed | RF-11, BR-007 | Seguridad por crear | Media fase 05 |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-01/02/03/09/11, TASK-CATALOG-002 | [Fase 03](../../phase-03-catalog-offers/spec.md) | CRT-01/02/03/04 | DEV-API013-01/02/03/04 |
| T-CAT-01/04, TASK-CATALOG-010/013 históricos | [Trazabilidad](../../../docs/traceability.md) | CRT-01/02/04 | DEV-API013-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| CRT-01 | NO_IMPLEMENTADO | Sin POST ni repositorio | DEV-API013-02/03 |
| CRT-02 | NO_IMPLEMENTADO | Sin modelo/indexación ni prueba homónimos | DEV-API013-03 |
| CRT-03 | BLOQUEADO | JSON/HTTP/estado categoría pendientes | DEV-API013-01/04 |
| CRT-04 | OTRA_FICHA | Media futuro, Redis gate aún sin Catalog | DEV-API013-01/04; API-017/018 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Fase 03 § 6.3](../../phase-03-catalog-offers/spec.md) y ausencia de `Modules/Catalog` | Persistencia real ni autorización del Sprint 2. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| TASK-CATALOG-002 | [Tareas](../../phase-03-catalog-offers/task.md) | Estado inicial, payload, categoría, errores y media excluida | DEV-API013-01/02/03/04 | Producto/API/Catalog aprueban Sprint 2 y cotejan Excel. |
| RF-01/11 vs DEC-019.e | [Fase 03](../../phase-03-catalog-offers/spec.md) | Texto RF-01 menciona imagen aprobada; Sprint 2 explícito sin imágenes | DEV-API013-01/02 | Decisión ya fija exclusión Sprint 2; Media fase 05 por ficha propia. |

## Criterios de preparación para desarrollo

Alcance: creación de producto sin imagen Sprint 2, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos). Desbloqueo: acta del contrato, modelo Categorías/Sellers y gates; aceptación: dos homónimos de dueños distintos, validaciones/propiedad, índices justificados y suites locales verdes.
