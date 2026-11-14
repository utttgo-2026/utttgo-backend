# API-008 — Perfil público de vendedor · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-008 `GET /api/v1/sellers/{sellerId}` según [trazabilidad](../../../docs/traceability.md) y [fase 02](../../phase-02-auth-users/spec.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

RN-FIG-06 confirma visibilidad de vendedor inactivo sin nuevos pedidos; acceso público/autenticado, DTO/HTTP/composición no ratificados (TASK-AUTH-005/026). Código candidato no cierra gate fase 02.

## Responsabilidad única

Leer proyección de vendedor por ID sin PII. No devuelve perfil privado (`GET /sellers/me`, DEC-019.d), no activa/edita vendedor (API-009/010), ni lista productos (API-011) ni decide pedidos (Orders).

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Fase 02](../../phase-02-auth-users/spec.md), [tareas](../../phase-02-auth-users/task.md) | RF-13, AC-013, T-AUTH-12/17, TASK-AUTH-005/026 | Figma HU-015/UI-014, RN-FIG-06, PB-012/HU-12; acceso/composición pendiente. |
| [Fase 03](../../phase-03-catalog-offers/spec.md) | T-CAT-V10/11, DEC-019 | Estados Sellers y proyección pública; Sprint 1 sin imágenes. |
| [QA local](../../../docs/qa-api-008.md), [trazabilidad](../../../docs/traceability.md) | API-008 | Dobles 2026-11-04; AWS `200/404` reportado 2026-11-06, no otras ramas. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Vendedor `Inactive` visible; sin nuevos pedidos | RN-FIG-06, AC-013 | Orders debe impedir pedidos; lectura no lo prueba. |
| No revelar email/teléfono, roles internos o token | RF-13, constitución § 6 | Acceso público y composición por decidir. |

## Comportamiento implementado candidato

[`SellersController.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Controllers/SellersController.cs) expone GET sin Bearer; [`SellerProfileService.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Application/SellerProfileService.cs) filtra `PendingActivation`/`Suspended`/`Deleted`, mantiene `Inactive`, ratings opcionales por interfaz Reviews, Redis rate limit y Mongo Sellers configurados en AWS. [`SellerProfileResponse.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Contracts/SellerProfileResponse.cs): `sellerId`, `displayName`, `ratingAverage`, `ratingCount`, `isActive`. Sin productos.

## Funcionalidad requerida faltante

Aprobar acceso/composición; consumir contrato Reviews real cuando exista, sin consultas cruzadas; catálogo por API-011 aún no documentada; validar Orders para inactividad por su ficha, no aquí.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato actual | Falta decidir |
|---|---|---|---|
| Entrada | Ruta con `sellerId`; no PII | Público; ObjectId hex 24, mayúsculas normalizadas; sin body | Acceso y formato ID definitivos. |
| Éxito | Inactivo visible | `200` cinco campos; ratings `null` si ausentes/fallo Reviews | Ratings sin datos y productos: API-011 vs composición paginada. |
| Error | Error único `correlationId`, no `404` por inactividad | `400 INVALID_SELLER_ID`, `404 SELLER_NOT_FOUND`, `429 RATE_LIMITED`, `503 SELLER_UNAVAILABLE` | Ratificar HTTP, rate limit y estados publicados. |

## Seguridad y validaciones

[Errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Validar ID antes de consulta; rate limit distribuido fail-closed; Sellers solo lee `sellers`; Reviews mediante `ISellerRatingsReader`, Catalog mediante API-011; no PII ni query que altere identidad. Sin prueba de bloqueo de Orders por esta ruta.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| PUB-01 | Vendedor activo | GET con su ID | `200` proyección sin PII, candidato | RF-13, T-CAT-V10 | `SellerProfileEndpointTests` con dobles; curl reportado | Acceso pendiente |
| PUB-02 | Vendedor inactivo | GET con su ID | Visible `isActive:false`; nuevos pedidos denegados por Orders | AC-013 | `SellerProfileServiceTests` con dobles; Orders pendiente | Visibilidad aprobada |
| PUB-03 | ID inválido/ausente o perfil oculto | GET | `400/404` candidato; sin exposición | RF-13, T-CAT-V11 | QA local y curl `404` para ausente | HTTP pendiente |
| PUB-04 | Redis caído/ratings sin fuente | GET | `503` fail-closed Redis; ratings `null` candidato sin PII | RF-13 | Dobles; integración real pendiente | Composición pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | Tarea DEV |
|---|---|---|---|
| RF-13, AC-013, T-AUTH-12/17, TASK-AUTH-005/026 | [Fase 02](../../phase-02-auth-users/spec.md) | PUB-01/02/03/04 | DEV-API008-01/02/03 |
| T-CAT-V10/11 | [Fase 03](../../phase-03-catalog-offers/spec.md) | PUB-01/03 | DEV-API008-03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| PUB-01 | IMPLEMENTADO_PROBADO | Solo dobles y lectura AWS limitada, contrato no ratificado | DEV-API008-01/03 |
| PUB-02 | OTRA_FICHA | Visibilidad con dobles; prohibición de compra pertenece a Orders | DEV-API008-03; Orders |
| PUB-03 | SIN_EVIDENCIA | Códigos/estados faltan pruebas con dependencias reales | DEV-API008-01/03 |
| PUB-04 | BLOQUEADO | Reviews real y degradación/integración Redis por demostrar | DEV-API008-02/03 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-04 | Dobles locales | [QA API-008](../../../docs/qa-api-008.md): 23 unitarias, 16 integración, 7 seguridad | Redis/Mongo/Reviews reales. |
| 2026-11-06 | AWS reportado | [Migración](../../../docs/migracion-sdd.md): perfil `200` y ausente `404` | Inactividad, ratings, Orders ni aprobación DTO. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| TASK-AUTH-005/026 | [Fase 02](../../phase-02-auth-users/task.md) | Público o autenticado; API-011 o lista paginada por contrato Catalog | DEV-API008-01/02/03 | Producto/API/seguridad, Reviews/Catalog: acta de acceso/DTO/HTTP. |
| TASK-AUTH-007→008→010 | [Fase 02](../../phase-02-auth-users/task.md) | Código no sustituye gate | DEV-API008-02/03 | Equipo/delegación, pruebas no productivas autorizadas. |

## Criterios de preparación para desarrollo

Alcance: contrato integral y verificación real, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) por TASK-AUTH-005/026 y gates. No reimplementar GET: aprobar contrato y probar Redis/Mongo y límites; Orders valida pedidos inactivos en su propia ficha.
