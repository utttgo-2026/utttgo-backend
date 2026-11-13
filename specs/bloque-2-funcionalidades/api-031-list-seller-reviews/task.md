# API-031 — Listar reseñas de vendedor · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No duplicar escritor API-030 ni inferir datos públicos de API-008.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: contrato fase 08 y visibilidad aún pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API031-01 · DOCUMENTAL ratificar listado | API-031, LIST-01..04; `spec.md`, `plan.md`, `task.md` tras acta | Producto/Reviews/Sellers/seguridad: decidir público vs privado, inactivo/ausente, DTO/orden/pageSize/rating y HTTP; cotejar Excel. **Hecho**: acta identificable y validador individual 0 errores. | BLOCKED |
| DEV-API031-02 · IMPLEMENTACION lectura paginada | API-031, LIST-01/02/03; `Modules/Reviews/Controllers/ReviewsController.cs`, `Contracts/SellerReviewsQuery.cs`, `Contracts/SellerReviewsPageResponse.cs`, `Validators/SellerReviewsQueryValidator.cs`, `Application/ListSellerReviewsService.cs`, `Infrastructure/MongoReviewRepository.cs`, `ReviewMongoIndexes.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API031-01/API-030: filtro por sellerId previo a paginación/orden estable, proyección mínima y DI. **Hecho**: `ListSellerReviewsEndpointTests.cs` aísla dos vendedores/página 2 y build Release verde. | BLOCKED |
| DEV-API031-03 · INTEGRACION métricas legítimas | RF-08/RF-V12 Catalog, LIST-02/04; `Modules/Reviews/Application/ListSellerReviewsService.cs`, `Modules/Reviews/Infrastructure/MongoReviewRepository.cs`, `tests/UtttGo.UnitTests/ListSellerReviewsServiceTests.cs` | Tras DEV-API031-01/02 y acta métricas: publicar interfaz Reviews para Sellers/Catalog solo con ratings derivados de reseñas aprobadas, sin escribir sus colecciones; si excluida, documentar no aplicable. **Hecho**: pruebas sin reseñas no devuelven puntuación inventada, agregados coherentes tras inserción/retry. | BLOCKED |
| DEV-API031-04 · QA_TECNICA privacidad/fallos | API-031, LIST-01..04; `tests/UtttGo.UnitTests/ListSellerReviewsServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListSellerReviewsEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListSellerReviewsSecurityTests.cs` | Tras DEV-API031-02/03: probar ID/page inválidos, vendedor inactivo, rate limit/Redis/Mongo caídos, aislamiento/PII; DEC-003 solo tras política aprobada. Hecho: evidencia fechada build/formato/tres suites Release verdes sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); verificar acta, código y pruebas reales antes de editar.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API031-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: fase 08 y visibilidad/DTO/ratings | - | PENDIENTE |
| DEV-API031-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: API-030 y acta de listado | DEV-API031-01 | PENDIENTE |
| DEV-API031-03 | INTEGRACION | BLOCKED | SI | Pendiente: contrato métricas Reviews aprobado | DEV-API031-02 | PENDIENTE |
| DEV-API031-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y decisiones de privacidad | DEV-API031-02/03 | PENDIENTE |
