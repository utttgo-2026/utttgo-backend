# API-031 — Listar reseñas de vendedor · task

Estado de aprobación de la ficha: APROBADA por autorización explícita del propietario 2026-11-23

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No duplicar escritor API-030 ni inferir datos públicos de API-008.

## Tareas ejecutables sin cambiar contrato

Contrato ratificado: listado público, paginación 1/20 máximo 50, orden estable, promedio y desglose 1..5 sin PII.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API031-01 · DOCUMENTAL ratificar listado | API-031, LIST-01..04 | Autorización explícita y contrato mínimo registrado en esta ficha. | DONE |
| DEV-API031-02 · IMPLEMENTACION lectura paginada | API-031, LIST-01/02/03 | Controller, validator, filtro Mongo, índice, proyección y DI. | DONE |
| DEV-API031-03 · INTEGRACION métricas legítimas | RF-08/RF-V12 Catalog, LIST-02/04 | Interfaces `ISellerRatingsReader`/`IProductRatingsReader`, promedio y desglose desde Reviews. | DONE |
| DEV-API031-04 · QA_TECNICA privacidad/fallos | API-031, LIST-01..04 | Pruebas unitarias, integración, seguridad y OpenAPI. | DONE |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); verificar acta, código y pruebas reales antes de editar.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API031-01 | DOCUMENTAL | DONE | NO | Autorización explícita y contrato ratificado | - | Evidencia en esta ficha |
| DEV-API031-02 | IMPLEMENTACION | DONE | SI | API-030 y contrato de listado disponibles | DEV-API031-01 | Código y tests |
| DEV-API031-03 | INTEGRACION | DONE | SI | Interfaces de métricas publicadas | DEV-API031-02 | Código y tests |
| DEV-API031-04 | QA_TECNICA | DONE | SI | Suites Release y OpenAPI | DEV-API031-02/03 | Evidencia de rama |
