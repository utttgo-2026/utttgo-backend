# API-021 — Editar o reactivar categoría · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Modelo Catalog compartido API-020; no mutar productos ajenos.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: transición, payload/slug y gates Sprint 2 pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API021-01 · DOCUMENTAL ratificar PATCH | RF-12, EDT-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | Producto/API/Catalog ratifican campos, null/omisiones, reactivación, unicidad slug, CAS/HTTP e invalidación; cotejan Excel. **Hecho**: acta Sprint 2 y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-021-update-category` con 0 errores. | BLOCKED |
| DEV-API021-02 · IMPLEMENTACION PATCH Admin | RF-12, EDT-01/03; `Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Contracts/UpdateCategoryRequest.cs`, `Validators/UpdateCategoryRequestValidator.cs`, `Application/UpdateCategoryService.cs`, DI `src/UTTTGO.Api/Program.cs` | Tras DEV-API021-01, modelo API-020 e interfaz Identity: validar rol local Admin/whitelist, modificar únicamente categoría. **Hecho**: `UpdateCategoryServiceTests.cs` verifica Admin/Buyer y campos omitidos, build Release verde. | BLOCKED |
| DEV-API021-03 · IMPLEMENTACION CAS y reactivación | RF-12, EDT-02/04; `Modules/Catalog/Application/UpdateCategoryService.cs`, `Infrastructure/MongoCategoryRepository.cs`, Redis read gate Catalog, `tests/UtttGo.IntegrationTests/UpdateCategoryEndpointTests.cs` | Tras DEV-API021-01/02 y contrato API-019: reactivar solo estado permitido, CAS/índice según acta e invalidación; no editar productos. **Hecho**: Mongo local comprueba dos PATCH concurrentes, GET coherente y productos intactos con evidencia fechada. | BLOCKED |
| DEV-API021-04 · QA_TECNICA autorización/fallos | RF-12, EDT-01/03/04; `tests/UtttGo.IntegrationTests/UpdateCategoryEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateCategorySecurityTests.cs` | Tras DEV-API021-02/03 y permiso Mongo/Redis local: cubrir Buyer, JWT revocado, categoría inexistente, duplicado/extra roles, 429/503; sin mutación ajena. **Hecho**: evidencia fechada build/formato/tres suites Release verdes, sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); confirmar acta y repositorio compartido API-020. No usar usuario Admin como identidad de vendedor.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API021-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: Sprint 2, contrato reactivación/slug/HTTP | - | PENDIENTE |
| DEV-API021-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API021-01 y modelo API-020 | DEV-API021-01 | PENDIENTE |
| DEV-API021-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: CAS/transición aprobados e invalidación API-019 | DEV-API021-01, DEV-API021-02 | PENDIENTE |
| DEV-API021-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permiso Mongo/Redis local y aprobación Sprint 2 | DEV-API021-02, DEV-API021-03 | PENDIENTE |
