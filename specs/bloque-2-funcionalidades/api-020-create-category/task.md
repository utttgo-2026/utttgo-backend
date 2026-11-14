# API-020 — Crear categoría · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Catalog escribe `categories`; no asignar rol Admin ni crear productos.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: Sprint 2 no aprobado, slug/unicidad sin decidir.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API020-01 · DOCUMENTAL aprobar alta | RF-12, NEW-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | Producto/API/seguridad/Catalog ratifican body, slug/normalización, duplicados, estado inicial, orden, HTTP/auditoría; cotejan Excel. **Hecho**: acta Sprint 2 y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-020-create-category` sin errores. | BLOCKED |
| DEV-API020-02 · IMPLEMENTACION POST Admin | RF-12, NEW-01/02; `Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Contracts/CreateCategoryRequest.cs`, `Contracts/CategoryResponse.cs`, `Validators/CreateCategoryRequestValidator.cs`, `Application/CreateCategoryService.cs`, DI `src/UTTTGO.Api/Program.cs` | Tras DEV-API020-01 y contrato Identity: JWT/rol local Admin y body permitido; no confiar roles del cliente. **Hecho**: `CreateCategoryServiceTests.cs`/`CreateCategorySecurityTests.cs` prueban Admin/Buyer y validación, build Release verde. | BLOCKED |
| DEV-API020-03 · IMPLEMENTACION persistencia única | RF-12, NEW-01/03/04; `Modules/Catalog/Infrastructure/CategoryDocument.cs`, `MongoCategoryRepository.cs`, `CatalogCategoryIndexes.cs`, `tests/UtttGo.IntegrationTests/CreateCategoryEndpointTests.cs` | Tras DEV-API020-01/02: insertar categoría en `categories` con unicidad/index aprobado y cache API-019 coherente; no modificar productos. **Hecho**: integración Mongo aislado de alta/duplicado/carrera y lectura, resultado conforme acta. | BLOCKED |
| DEV-API020-04 · QA_TECNICA fallos/seguridad | RF-12, NEW-02/03/04; `tests/UtttGo.IntegrationTests/CreateCategoryEndpointTests.cs`, `tests/UtttGo.SecurityTests/CreateCategorySecurityTests.cs` | Tras DEV-API020-02/03 y permiso local: JWT revocado, rol ajeno, extra `roles`, Redis caído/429 y Mongo caído; verificar sin escritura. **Hecho**: evidencia fechada, build/formato/tres suites Release verdes, sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); confirmar acta y modelo compartido API-019/021 antes de código.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API020-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: contrato Sprint 2 de categorías | - | PENDIENTE |
| DEV-API020-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API020-01 e interfaz Identity Admin | DEV-API020-01 | PENDIENTE |
| DEV-API020-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: unicidad/índices aprobados y permiso Mongo local | DEV-API020-01, DEV-API020-02 | PENDIENTE |
| DEV-API020-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permisos Mongo/Redis locales y Sprint 2 | DEV-API020-02, DEV-API020-03 | PENDIENTE |
