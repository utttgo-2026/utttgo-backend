# API-019 — Listar categorías · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Sin Sprint 2 aprobado no hay tarea técnica READY.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: DTO/orden/slug y visibilidad de categoría pendiente.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API019-01 · DOCUMENTAL ratificar listado | RF-12, CAT-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | Producto/API/Catalog acuerdan DTO, orden, slug, estados visibles, pageSize máximo e invalidación; cotejar Excel. **Hecho**: acta Sprint 2 y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-019-list-categories` con 0 errores. | BLOCKED |
| DEV-API019-02 · IMPLEMENTACION GET paginado | RF-12, CAT-01/02/03; `Modules/Catalog/Controllers/CategoriesController.cs`, `Contracts/CategoryListQuery.cs`, `Contracts/CategoryListResponse.cs`, `Validators/CategoryListQueryValidator.cs`, `Application/ListCategoriesService.cs`, `Infrastructure/CategoryDocument.cs`, `MongoCategoryRepository.cs`, DI `src/UTTTGO.Api/Program.cs` | Tras DEV-API019-01 y modelo común API-020: implementar GET público sin alias, filtro/orden/paginación acordados, rate limit Redis. **Hecho**: `ListCategoriesServiceTests.cs` y `ListCategoriesEndpointTests.cs` verifican activo/inactivo y páginas Mongo locales, build Release verde. | BLOCKED |
| DEV-API019-03 · QA_TECNICA caché y límites | RF-12, CAT-03/04; `tests/UtttGo.IntegrationTests/ListCategoriesEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListCategoriesSecurityTests.cs`, Redis read gate Catalog | Tras DEV-API019-02 y contratos API-021/022: probar escritura Admin seguida de GET, pageSize inválido, 429, caída cache (consulta Mongo) y rate limit Redis cerrado. **Hecho**: evidencia fechada build/formato/tres suites Release verdes con Mongo/Redis aislados. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); confirmar modelo de categoría aprobado, no crear `/products/categories`.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API019-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: acta Sprint 2 y contrato categoría | - | PENDIENTE |
| DEV-API019-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API019-01 y modelo común API-020 | DEV-API019-01 | PENDIENTE |
| DEV-API019-03 | QA_TECNICA | BLOCKED | SI | Pendiente: permisos Mongo/Redis locales y escrituras API-021/022 | DEV-API019-02 | PENDIENTE |
