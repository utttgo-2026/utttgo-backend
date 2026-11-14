# API-032 — Listar favoritos propios · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No implementar productos ni bajas de cuenta aquí.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: fase 09 no aprobada.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción, resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API032-01 · DOCUMENTAL ratificar listado | API-032, FAVL-01..04; `spec.md`, `plan.md`, `task.md` tras acta | Producto/Favorites/Catalog resuelven DTO, acceso, orden, pageSize, retirados y HTTP; cotejan Excel. **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-032-list-favorites` 0 errores. | BLOCKED |
| DEV-API032-02 · IMPLEMENTACION lector propio | API-032, FAVL-01/03; `Modules/Favorites/Controllers/FavoritesController.cs`, `Contracts/FavoritesPageResponse.cs`, `Validators/FavoritesQueryValidator.cs`, `Application/ListFavoritesService.cs`, `Infrastructure/MongoFavoriteRepository.cs`, `FavoriteMongoIndexes.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API032-01 y modelo API-033: filtrar userId server-side, paginar/ordenar en Mongo, registrar DI. **Hecho**: `ListFavoritesEndpointTests.cs` aísla usuarios y `dotnet build UTTTGO.Backend.slnx -c Release --no-restore` verde. | BLOCKED |
| DEV-API032-03 · INTEGRACION estado Catalog | API-032, FAVL-02; `Modules/Favorites/Application/ListFavoritesService.cs`, `tests/UtttGo.IntegrationTests/ListFavoritesEndpointTests.cs` | Tras DEV-API032-01/02 y lector Catalog: enriquecer/ocultar retirados solo según acta, sin tocar products. **Hecho**: test con doble Catalog demuestra estado/visibilidad ratificados sin compra implícita. | BLOCKED |
| DEV-API032-04 · QA_TECNICA seguridad y baja | API-032, FAVL-01..04; `tests/UtttGo.UnitTests/ListFavoritesServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListFavoritesEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListFavoritesSecurityTests.cs` | Tras DEV-API032-02/03: JWT revocado, Redis caído, paginación inválida; baja API-005 solo cuando aprobada. **Hecho**: evidencia fechada build/formato/tres suites Release verdes, sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md) y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar actas/código antes de ejecutar tareas.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API032-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: acta fase 09/listado | - | PENDIENTE |
| DEV-API032-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: modelo API-033 y contrato API-032 | DEV-API032-01 | PENDIENTE |
| DEV-API032-03 | INTEGRACION | BLOCKED | SI | Pendiente: lector Catalog y visibilidad ratificados | DEV-API032-02 | PENDIENTE |
| DEV-API032-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado, baja API-005 si aplica | DEV-API032-02/03 | PENDIENTE |
