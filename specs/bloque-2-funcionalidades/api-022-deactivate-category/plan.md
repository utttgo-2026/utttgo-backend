# API-022 — Desactivar categoría · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Catalog escribe únicamente su categoría; no borrar registros `products` ni tocar Orders.

## Arquitectura existente

No existe Catalog ni tests categoría. Reutilizar modelo/repositorio propuestos por API-020/021, lector API-019, guard JWT/rol local Identity y Redis; no inventar segundo repositorio de categorías.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Validators/CategoryIdValidator.cs` | DELETE, validar ruta y error aprobado. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/DeactivateCategoryService.cs` | Verificar Admin/estado, desactivar sin tocar productos. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/MongoCategoryRepository.cs`, Redis read gate Catalog | Actualización condicional e invalidación caché. |

## Secuencia técnica

JWT/blacklist, Admin local y rate limit → validar categoryId → leer categoría → cambiar solo `isActive`/versión según acta con filtro condicional → invalidar cache API-019/011/012 → responder; Orders comprueba categoría vigente en contrato Catalog al comprar. No eliminar física ni modificar pedidos existentes. Resolver antes qué hacer si Mongo commit ocurre y Redis invalidación falla.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| CATD-01/03 | `Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Validators/CategoryIdValidator.cs`, `Application/DeactivateCategoryService.cs`, `src/UTTTGO.Api/Program.cs` | DELETE con guard Admin y DI sin alterar middleware. |
| CATD-02/04 | `Modules/Catalog/Infrastructure/MongoCategoryRepository.cs`, Redis read gate Catalog | CAS/repetición/invalidez según acta, sin tocar products. |
| CATD-01..04 | `tests/UtttGo.UnitTests/DeactivateCategoryServiceTests.cs`, `tests/UtttGo.IntegrationTests/DeactivateCategoryEndpointTests.cs`, `tests/UtttGo.SecurityTests/DeactivateCategorySecurityTests.cs` | Pruebas nuevas, Orders por contrato. |

## Orden de implementación

DEV-API022-01 ratifica HTTP/semántica; DEV-API022-02 implementa acción/guard; DEV-API022-03 realiza actualización condicional e invalidación; DEV-API022-04 prueba propiedad, fallo Redis y elegibilidad vía lector Catalog (no implementar Orders aquí).

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| Sprint 2 API-019..022 | DEV-API022-01/02/03/04 | Acta ID, HTTP, repetición, CAS e invalidación. |
| API-019/020/021, API-011/012/023 | DEV-API022-03/04 | Modelo compartido/visibilidad/contrato elegibilidad; no reescribir productos ni pedidos. |

## Estrategia de pruebas

Unitarias transición/repetición; WebApplicationFactory Mongo/Redis aislados: productos permanecen tras desactivar, GET ya no los muestra como comprables según contrato, reactivación API-021; seguridad Buyer/token revocado/ID inválido/Redis caído; carrera de dos Admin y compra mock. Tras aprobación: build Release, format y tres proyectos `dotnet test -c Release --no-build`, sin producción.

## Seguridad, errores y observabilidad

JWT validado, rol local Admin, rate limit fail-closed, error único `correlationId`; no PII/token en logs, invalidación coherente; no alterar middleware.

## Criterios de cierre técnico

CATD-01..04 demostrados en entorno no productivo, documentos products intactos, nuevas compras excluidas por elegibilidad, suites/build/formato verdes.
