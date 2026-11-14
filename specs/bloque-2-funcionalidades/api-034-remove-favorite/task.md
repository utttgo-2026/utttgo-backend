# API-034 — Quitar producto favorito · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Sin borrado físico de producto ni de favoritos ajenos.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: contrato fase 09 pendiente.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción, resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API034-01 · DOCUMENTAL ratificar DELETE | API-034, FAVR-01..04; `spec.md`, `plan.md`, `task.md` tras acta | Producto/Favorites/seguridad deciden ID/body, ausente/HTTP, retirado y carrera PUT-DELETE; cotejan Excel. **Hecho**: acta y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-034-remove-favorite` 0 errores. | BLOCKED |
| DEV-API034-02 · IMPLEMENTACION guard | API-034, FAVR-01/03/04; `Modules/Favorites/Controllers/FavoritesController.cs`, `Validators/FavoriteProductIdValidator.cs`, `Application/RemoveFavoriteService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API034-01/API-033: resolver actor, validar ID, DELETE sin acceso Catalog salvo acta, DI. **Hecho**: `RemoveFavoriteServiceTests.cs` verifica userId propio, build Release verde. | BLOCKED |
| DEV-API034-03 · IMPLEMENTACION borrado filtrado | API-034, FAVR-01/02; `Modules/Favorites/Infrastructure/MongoFavoriteRepository.cs`, `Application/RemoveFavoriteService.cs` | Tras DEV-API034-02: DeleteOne por userId+productId, replay y cache según acta, sin editar producto. **Hecho**: `RemoveFavoriteEndpointTests.cs` prueba dos usuarios, doble DELETE, ninguno ajeno eliminado. | BLOCKED |
| DEV-API034-04 · QA_TECNICA carreras y seguridad | API-034, FAVR-02..04; `tests/UtttGo.UnitTests/RemoveFavoriteServiceTests.cs`, `tests/UtttGo.IntegrationTests/RemoveFavoriteEndpointTests.cs`, `tests/UtttGo.SecurityTests/RemoveFavoriteSecurityTests.cs` | Tras DEV-API034-02/03: PUT-DELETE, JWT revocado, Redis/Mongo caídos y retirado según acta. **Hecho**: evidencia fechada build/formato/tres suites Release verdes sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); comprobar código de API-033 y acta.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API034-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: contrato fase 09 | - | PENDIENTE |
| DEV-API034-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: API-033 y acta | DEV-API034-01 | PENDIENTE |
| DEV-API034-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: política replay/cache | DEV-API034-02 | PENDIENTE |
| DEV-API034-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado | DEV-API034-02/03 | PENDIENTE |
