# API-033 — Marcar producto favorito · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. PUT por producto, no por oferta ni carrito.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: contrato fase 09 y Catalog pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción, resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API033-01 · DOCUMENTAL ratificar PUT | API-033, FAVA-01..04; `spec.md`, `plan.md`, `task.md` tras acta | Producto/Favorites/Catalog definen elegibilidad, duplicados, body/HTTP y cotejan Excel. **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-033-add-favorite` 0 errores. | BLOCKED |
| DEV-API033-02 · IMPLEMENTACION guard y elegibilidad | API-033, FAVA-01/03/04; `Modules/Favorites/Controllers/FavoritesController.cs`, `Validators/FavoriteProductIdValidator.cs`, `Application/AddFavoriteService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API033-01 y lector Catalog: validar JWT/userId, producto vía interfaz, no stock/cesta; registrar DI. **Hecho**: `AddFavoriteServiceTests.cs` prueba actor/visibilidad, build Release verde. | BLOCKED |
| DEV-API033-03 · IMPLEMENTACION escritura única | API-033, FAVA-01/02; `Modules/Favorites/Infrastructure/FavoriteDocument.cs`, `MongoFavoriteRepository.cs`, `FavoriteMongoIndexes.cs`, `Application/AddFavoriteService.cs` | Tras DEV-API033-01/02: upsert e índice por userId/productId según política; no duplicar API-032/034. **Hecho**: `AddFavoriteEndpointTests.cs` demuestra dos PUT no duplican, HTTP conforme acta. | BLOCKED |
| DEV-API033-04 · QA_TECNICA carrera/fallos | API-033, FAVA-02..04; `tests/UtttGo.UnitTests/AddFavoriteServiceTests.cs`, `tests/UtttGo.IntegrationTests/AddFavoriteEndpointTests.cs`, `tests/UtttGo.SecurityTests/AddFavoriteSecurityTests.cs` | Tras DEV-API033-02/03: probar carrera, tercero, JWT revocado, Redis/Catalog caídos. **Hecho**: evidencia fechada build/formato/tres suites Release verdes sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); verificar acta y código antes de programar.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API033-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: contrato fase 09 y Catalog | - | PENDIENTE |
| DEV-API033-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: acta y lector Catalog | DEV-API033-01 | PENDIENTE |
| DEV-API033-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: política unicidad/reintento | DEV-API033-02 | PENDIENTE |
| DEV-API033-04 | QA_TECNICA | BLOCKED | SI | Pendiente: pruebas aisladas autorizadas | DEV-API033-02/03 | PENDIENTE |
