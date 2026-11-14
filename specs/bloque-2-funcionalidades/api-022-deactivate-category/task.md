# API-022 — Desactivar categoría · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No equivale a DELETE de productos ni a cancelar pedidos.

## Tareas ejecutables sin cambiar contrato

Ninguna tarea técnica READY: Sprint 2, semántica HTTP/CAS e invalidación sin acta.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API022-01 · DOCUMENTAL ratificar desactivación | RF-09/12, CATD-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | Producto/API/Catalog decide ID, HTTP/DTO, repetición, visibilidad y CAS/invalidación, sin borrar productos; coteja Excel. **Hecho**: acta Sprint 2 y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-022-deactivate-category` sin errores. | BLOCKED |
| DEV-API022-02 · IMPLEMENTACION acción Admin | RF-12, CATD-01/03; `Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Validators/CategoryIdValidator.cs`, `Application/DeactivateCategoryService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API022-01, modelo API-020 y guard Identity: validar JWT Admin local, categoría/estado; responder contrato aprobado sin borrar productos. **Hecho**: `DeactivateCategoryServiceTests.cs` y `DeactivateCategorySecurityTests.cs` prueban Admin/Buyer y recurso inexistente; build Release verde. | BLOCKED |
| DEV-API022-03 · IMPLEMENTACION desactivar e invalidar | RF-09/12, CATD-01/02/04; `Modules/Catalog/Infrastructure/MongoCategoryRepository.cs`, Redis read gate Catalog, `tests/UtttGo.IntegrationTests/DeactivateCategoryEndpointTests.cs` | Tras DEV-API022-01/02: update condicional solo categoría, cache API-019/011/012 coherente e interfaz de elegibilidad Catalog para Orders; no escribir products/Orders. **Hecho**: integración Mongo/Redis aislados verifica duplicado, documentos intactos y nueva compra rechazada por lector mock, resultado fechado. | BLOCKED |
| DEV-API022-04 · QA_TECNICA concurrencia y fallo | RF-09, CATD-02/03/04; `tests/UtttGo.IntegrationTests/DeactivateCategoryEndpointTests.cs`, `tests/UtttGo.SecurityTests/DeactivateCategorySecurityTests.cs` | Tras DEV-API022-02/03 y permiso local: cubrir doble DELETE, Redis/Mongo caídos, token revocado y Buyer, carrera con compra por interfaz. **Hecho**: evidencia fechada build/formato/tres suites Release verdes, sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md). Verificar acta y modelo API-020; no resolver por cuenta propia política Orders.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API022-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: Sprint 2 e invalidación/HTTP | - | PENDIENTE |
| DEV-API022-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: acta de API-022 y modelo API-020 | DEV-API022-01 | PENDIENTE |
| DEV-API022-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: contrato Catalog/Orders e invalidación aprobada | DEV-API022-01, DEV-API022-02 | PENDIENTE |
| DEV-API022-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permiso local Mongo/Redis y Sprint 2 | DEV-API022-02, DEV-API022-03 | PENDIENTE |
