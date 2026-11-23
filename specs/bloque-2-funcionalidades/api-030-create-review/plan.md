# API-030 — Crear reseña vinculada a pedido · plan

Estado de aprobación de la ficha: APROBADA; implementación completada 2026-11-23

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Reviews dueño de reseñas y métricas, Orders dueño de pedido. Ruta bajo orders no otorga propiedad de la colección a Orders.

## Arquitectura existente

Reviews implementa sus colecciones y consume `IDeliveredOrderReader` publicado por Orders. RF-08 se atiende mediante `ISellerRatingsReader` e `IProductRatingsReader`; no existe dependencia inversa ni lectura directa de `orders`.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Reviews/Controllers/ReviewsController.cs`, `Contracts/CreateReviewRequest.cs`, `Contracts/ReviewResponse.cs`, `Validators/CreateReviewRequestValidator.cs` | POST en ruta inventariada, DTO solo tras acta. |
| Aplicación | `src/UTTTGO.Api/Modules/Reviews/Application/CreateReviewService.cs` | Verificar buyer y entrega acreditada; atribuir IDs del hecho. |
| Datos | `src/UTTTGO.Api/Modules/Reviews/Infrastructure/ReviewDocument.cs`, `MongoReviewRepository.cs`, `ReviewMongoIndexes.cs` | Colección propia candidata `reviews`, unicidad/índices según consultas y decisión. |
| Interfaz | Contrato de hecho entregado publicado por Orders (o mecanismo acíclico aprobado) | Evidencia buyer/seller/product/Delivered; no repositorio Orders. |

## Secuencia técnica

JWT/blacklist y limitador Redis → validar orderId y DTO una vez aprobado → obtener evidencia `Delivered` y comprador del contrato Orders acíclico, con garantía de entrega/reintento → derivar seller/product del hecho → inserción Reviews con política de duplicados aprobada (índice condicional si procede) → responder DTO; si hecho no existe o está retrasado, fallar sin crear reseña y recuperar según acta. Si agregados de rating fallan, no publicar valores falsos: diseñar actualización idempotente/reconciliación antes de implementar.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| REV-01/02 | `Modules/Reviews/Controllers/ReviewsController.cs`, `Contracts/CreateReviewRequest.cs`, `Contracts/ReviewResponse.cs`, `Validators/CreateReviewRequestValidator.cs`, `Application/CreateReviewService.cs`, `src/UTTTGO.Api/Program.cs` | DTO/guard/DI solo tras contrato; middleware intacto. |
| REV-01/03/04 | `Modules/Reviews/Infrastructure/ReviewDocument.cs`, `MongoReviewRepository.cs`, `ReviewMongoIndexes.cs` | Colección e índice propios tras política cardinalidad. |
| REV-01..04 | `tests/UtttGo.UnitTests/CreateReviewServiceTests.cs`, `tests/UtttGo.IntegrationTests/CreateReviewEndpointTests.cs`, `tests/UtttGo.SecurityTests/CreateReviewSecurityTests.cs` | Tests locales nuevos. |

## Orden de implementación

DEV-API030-01..04 se completan en esta rama: contrato, DTO/guard, inserción idempotente, métricas y pruebas. API-029 permanece en Orders; API-031 se implementa en el mismo módulo sin duplicar el escritor.

## Dependencias y bloqueos

| Gate transversal o decisión particular | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| Contrato API-030 de fase 08 y cotejo Excel | DEV-API030-01..04 | Acta payload, escala, cardinalidad, HTTP, política datos. |
| API-029, D-ORD-03 y entrega acíclica | DEV-API030-01/02/04 | Hecho persistido/garantía de entrega aprobados por Orders/Reviews. |
| DEC-003/D-ORD-09 | DEV-API030-04 solo caso baja | Política de datos compartidos aprobada; no inventar purga. |

## Estrategia de pruebas

Unitarias buyer/Delivered/validación tras acta; WebApplicationFactory Mongo/Redis aislados, doble hecho Orders: retraso, duplicado, carrera de POST e índices; seguridad tercero/revocado/Redis caído. Tras aprobación: build Release, format, tres suites `dotnet test -c Release --no-build`; no producción ni métricas ficticias.

## Seguridad, errores y observabilidad

Sujeto del JWT comparado con buyer del hecho, seller/product server-side, rate limit y blacklist fail-closed; error `correlationId`, no PII/contenido en logs. Respetar fronteras, no cambiar middleware.

## Criterios de cierre técnico

REV-01..04 con evidencia de autorización y política de duplicados/ratings aprobada, fallos recuperables, build/formato/suites verdes; evidencia local fechada sin datos de usuarios reales.
