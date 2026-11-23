# API-030 — Crear reseña vinculada a pedido · task

Estado de aprobación de la ficha: APROBADA por autorización explícita del propietario 2026-11-23

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No inventar escala, payload, aprobación de Reviews ni leer `orders` directamente.

## Tareas ejecutables sin cambiar contrato

Contrato ratificado: `rating` 1..5, `comment` opcional ≤1000, una reseña por pedido, `201`, `409 REVIEW_ALREADY_EXISTS` y lectura de entrega mediante `IDeliveredOrderReader`.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API030-01 · DOCUMENTAL ratificar reseña | API-030, REV-01..04 | Autorización explícita y contrato mínimo registrado en esta ficha. | DONE |
| DEV-API030-02 · IMPLEMENTACION guard y DTO | API-030, REV-01/02/04 | Controller, DTO, validator, servicio y DI; comprador/Delivered y datos server-side. | DONE |
| DEV-API030-03 · IMPLEMENTACION persistencia/duplicados | API-030, REV-01/03 | Mongo `reviews`, índice único por `orderId`, métricas y concurrencia de inserción. | DONE |
| DEV-API030-04 · QA_TECNICA seguridad/fallos | API-030, REV-01..04 | Pruebas unitarias, integración, seguridad y OpenAPI; evidencia Release sin producción. | DONE |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); comprobar actas y código antes de editar.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API030-01 | DOCUMENTAL | DONE | NO | Autorización explícita y contrato ratificado | - | Evidencia en esta ficha |
| DEV-API030-02 | IMPLEMENTACION | DONE | SI | API-029 y contrato `IDeliveredOrderReader` disponibles | DEV-API030-01 | Código y tests |
| DEV-API030-03 | IMPLEMENTACION | DONE | SI | Unicidad/índices y métricas implementados | DEV-API030-02 | Código y tests |
| DEV-API030-04 | QA_TECNICA | DONE | SI | Suites Release y OpenAPI | DEV-API030-02/03 | Evidencia de rama |
