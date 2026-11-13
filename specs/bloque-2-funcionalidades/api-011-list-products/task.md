# API-011 — Buscar productos · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Sprint 2 sin aprobar; no programar sin acta ni crear API-016.

## Tareas ejecutables sin cambiar contrato

No hay tarea técnica READY. La decisión de filtros, HTTP y esquema es previa a codificar.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API011-01 · DOCUMENTAL cerrar query y DTO | RF-07/08/09/13, LST-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | TASK-CATALOG-002: producto/API/Reviews/Catalog definen acceso, filtros, orden, límites, ratings, estados, DTO/HTTP e índices; cotejan Excel autorizado. **Hecho**: acta Sprint 2, ficha conciliada y validador individual 0 errores. | BLOCKED |
| DEV-API011-02 · IMPLEMENTACION lectura paginada | RF-07/09, LST-01/04; `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ProductListQuery.cs`, `Contracts/ProductListResponse.cs`, `Validators/ProductListQueryValidator.cs`, `Application/ListProductsService.cs`, `Infrastructure/MongoProductRepository.cs`, registro DI en `Program.cs` | Tras DEV-API011-01 y modelo `products` API-013: crear GET público, filtros/orden aprobados, paginar, excluir estados prohibidos sin globalizar productos; registrar DI sin alterar middleware. **Hecho**: `ListProductsServiceTests.cs` unitario y `ListProductsEndpointTests.cs` integración devuelven dos vendedores y paginación correcta, Release verde. | BLOCKED |
| DEV-API011-03 · INTEGRACION calificaciones y caché | RF-08/13, LST-02/03; `Application/ListProductsService.cs`, `Infrastructure/RedisProductReadGate.cs`, `tests/UtttGo.UnitTests/ListProductsServiceTests.cs` | Tras DEV-API011-01/02 y contratos Sellers/Reviews: consumir readers publicados, no colecciones ajenas; cache fail-open con invalidación acordada, sin inventar rating. **Hecho**: unitarias rating ausente/fallo, cache caída consulta Mongo y escritura invalida según contrato; resultados fechados. | BLOCKED |
| DEV-API011-04 · QA_TECNICA visibilidad/seguridad | RF-07/09/13, LST-01/03/04; `tests/UtttGo.IntegrationTests/ListProductsEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListProductsSecurityTests.cs` | Tras DEV-API011-02/03 y permiso Mongo/Redis local: cubrir categoría y vendedor inactivos, 400/429/503, sin PII y orden/paginación. **Hecho**: evidencia fechada, build/formato y tres suites Release verdes, sin usar producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); verificar acta y contratos dueños antes de editar. Ejecutar en entorno aislado; documentar límites sin secretos.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API011-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-CATALOG-002 y acta Sprint 2 | - | PENDIENTE |
| DEV-API011-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API011-01, modelo API-013 y aprobación Sprint 2 | DEV-API011-01 | PENDIENTE |
| DEV-API011-03 | INTEGRACION | BLOCKED | SI | Pendiente: TASK-CATALOG-002, interfaces Sellers/Reviews y gates | DEV-API011-01, DEV-API011-02 | PENDIENTE |
| DEV-API011-04 | QA_TECNICA | BLOCKED | SI | Pendiente: Sprint 2, autorización Mongo/Redis local y contratos de categoría | DEV-API011-02, DEV-API011-03 | PENDIENTE |
