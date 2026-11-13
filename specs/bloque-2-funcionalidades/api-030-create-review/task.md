# API-030 — Crear reseña vinculada a pedido · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No inventar escala, payload, aprobación de Reviews ni leer `orders` directamente.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: falta spec fase 08 aprobada y contrato de hecho entregado.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API030-01 · DOCUMENTAL ratificar reseña | API-030, REV-01..04; `spec.md`, `plan.md`, `task.md` tras acta | Producto/Reviews/Orders/seguridad: decidir DTO/escala, unicidad, política texto/HTTP y contrato hecho entregado acíclico; cotejar Excel; DEC-003 fuera del handler. **Hecho**: acta identificable y validador individual 0 errores. | BLOCKED |
| DEV-API030-02 · IMPLEMENTACION guard y DTO | API-030, REV-01/02/04; `Modules/Reviews/Controllers/ReviewsController.cs`, `Contracts/CreateReviewRequest.cs`, `Contracts/ReviewResponse.cs`, `Validators/CreateReviewRequestValidator.cs`, `Application/CreateReviewService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API030-01/API-029: validar JWT comprador y evidencia Delivered por contrato Orders sin dependencia inversa, derivar seller/product server-side. Hecho: `CreateReviewServiceTests.cs` niega Pending/tercero y build Release verde. | BLOCKED |
| DEV-API030-03 · IMPLEMENTACION persistencia/duplicados | API-030, REV-01/03; `Modules/Reviews/Infrastructure/ReviewDocument.cs`, `MongoReviewRepository.cs`, `ReviewMongoIndexes.cs`, `Application/CreateReviewService.cs` | Tras DEV-API030-01/02 y política cardinalidad: insertar reseña propia con índice y agregados solo si aprobados, resolver replay/carrera sin doble rating. **Hecho**: `CreateReviewEndpointTests.cs` verifica éxito y POST concurrentes contra Mongo aislado. | BLOCKED |
| DEV-API030-04 · QA_TECNICA seguridad/fallos | API-030, REV-01..04; `tests/UtttGo.UnitTests/CreateReviewServiceTests.cs`, `tests/UtttGo.IntegrationTests/CreateReviewEndpointTests.cs`, `tests/UtttGo.SecurityTests/CreateReviewSecurityTests.cs` | Tras DEV-API030-02/03: hecho ausente/tardío, Redis caído, tercero, revocado; DEC-003 solo con política aprobada. Hecho: evidencia fechada build/formato/tres suites Release verdes sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); comprobar actas y código antes de editar.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API030-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: fase 08 y contrato hecho Orders | - | PENDIENTE |
| DEV-API030-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: API-029 y acta de payload/eligibilidad | DEV-API030-01 | PENDIENTE |
| DEV-API030-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: cardinalidad/índices/ratings aprobados | DEV-API030-02 | PENDIENTE |
| DEV-API030-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y política compartidos para baja | DEV-API030-02/03 | PENDIENTE |
