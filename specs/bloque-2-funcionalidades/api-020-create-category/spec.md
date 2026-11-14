# API-020 — Crear categoría · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-020 `POST /api/v1/admin/categories`, módulo dueño `Catalog` con autorización Admin del perfil local, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

RF-12 confirma administración de categorías y permanencia de API-019..022; Sprint 2 no aprobado. JSON, slug, política de unicidad, estado inicial, auditoría y HTTP por conciliar; no inferirlos de rutas Admin existentes.

## Responsabilidad única

Crear categoría propia del catálogo como Admin autorizado para que aparezca en API-019 y se utilice en productos API-013. No crea productos ni asigna roles Admin; no edita (API-021) ni desactiva (API-022), no convierte categorías en menú/horario.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-004/006/009, § 16 API-019..022 | Roles locales y categorías, DTO/slug/orden pendientes. |
| [Fase 03](../../phase-03-catalog-offers/spec.md), [plan](../../phase-03-catalog-offers/plan.md) | RF-12, Sprint 2 § 6.3 | `categories` propuestas (`name` normalizado, `isActive`, orden, fechas), no implementadas. |
| [Trazabilidad](../../../docs/traceability.md) | API-020; TASK-CATALOG-011/T-CAT-05 históricos | Ruta Admin, sin contrato/QA específico aprobado. |

## Contrato aprobado

| Regla | Fuente expresa | Límite |
|---|---|---|
| Admin gestiona categorías; su ocultación no borra publicaciones | RF-12 [fase 03](../../phase-03-catalog-offers/spec.md) | DTO, nombres y transiciones pendientes. |
| Roles de perfil local, nunca claims aportados por cliente | [Constitución § 6](../../../docs/constitution.md) | Forma de auditoría/HTTP por ratificar. |

## Comportamiento implementado candidato

No existe `Modules/Catalog/` ni ruta Admin categories, `CategoryDocument`, validador o pruebas de categoría. API-039 existente de Identity lista usuarios Admin pero no crea categorías ni constituye autorización de este contrato.

## Funcionalidad requerida faltante

Escritor de `categories` del dueño Catalog, validación de nombre/slug/orden aprobados, control de duplicados/concurrencia e invalidación de API-019; API-013 consume contrato de categoría activa de Catalog, no accede a colección ajena.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | POST Admin JWT válido, rol/estado de perfil local | JSON `name` y eventualmente `order`/`slug` derivados | Campos, longitudes, normalización, slug generado/manual, whitelist y límites. |
| Persistencia | Colección `categories` del módulo Catalog | ID ObjectId, name normalizado, `isActive`, orden/fechas propuestos por fase 03 | Unicidad (p.ej. nombre/slug), índice justificado, estado inicial y auditoría. |
| Éxito | Categoría creada por servidor | `201` + DTO candidato | HTTP/DTO, visibilidad inmediata, cache invalidada. |
| Error | Contrato único `correlationId`, rate limit distribuido fail-closed | `400/401/403/409/429/503` candidatos | Duplicados, perfil ausente, Redis/Mongo caídos, respuesta exacta. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT y blacklist; Admin/estado de `users` por interfaz Identity, jamás confiar en `roles` del body; validación FluentValidation, clave única/normalización solo tras contrato; limitar entradas para evitar regex/inyección y carreras; rate limit fail-closed, sin datos sensibles en logs; error `correlationId`.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| NEW-01 | Admin validado y categoría válida | POST | Categoría creada y visible según estado aprobado, sin producto asociado automático | RF-12 | Mongo integración por crear | Función confirmada, DTO pendiente |
| NEW-02 | Buyer o JWT inválido/revocado | POST | Sin escritura; error autorizado | BR-009 | Seguridad por crear | Rol local aprobado |
| NEW-03 | Nombre/slug duplicado o campos inválidos | POST | Rechazo/resultado según unicidad ratificada, sin carrera/duplicado | RF-12 | Unit/integración por crear | Unicidad/HTTP pendiente |
| NEW-04 | Redis o Mongo falla después de validar | POST | No se confirma éxito engañoso; cache de API-019 no sirve dato incorrecto | RF-12 | Integración fallos por crear | Fallo parcial pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-12 | [Fase 03](../../phase-03-catalog-offers/spec.md) | NEW-01/02/03/04 | DEV-API020-01/02/03/04 |
| TASK-CATALOG-011, T-CAT-05 históricos | [Trazabilidad](../../../docs/traceability.md) | NEW-01/03 | DEV-API020-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| NEW-01 | NO_IMPLEMENTADO | Sin POST ni colección | DEV-API020-02/03 |
| NEW-02 | NO_IMPLEMENTADO | Falta guard Admin/seguridad | DEV-API020-02/04 |
| NEW-03 | BLOQUEADO | Unicidad y validación sin contrato | DEV-API020-01/03 |
| NEW-04 | NO_IMPLEMENTADO | Fallos e invalidación sin pruebas | DEV-API020-03/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Fase 03 § 6.3](../../phase-03-catalog-offers/spec.md) y ausencia Catalog | Creación real ni aprobación Sprint 2. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| API-019..022 Sprint 2, TASK-CATALOG-011 histórico | [Constitución](../../../docs/constitution.md), [trazabilidad](../../../docs/traceability.md) | Slug, orden, duplicados, auditar y HTTP | DEV-API020-01/02/03/04 | Producto/API/seguridad/Catalog: acta Sprint 2 y cotejo Excel. |

## Criterios de preparación para desarrollo

Alcance: creación Admin de categoría, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta aprobación Sprint 2. Cierre: rol local, validación/concurrencia, visibilidad en API-019 y pruebas Mongo/Redis aisladas verdes, sin mutar productos.
