# API-005 — Eliminar cuenta propia · task

Estado de aprobación de la ficha: APROBADA

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. DEC-003 fue aprobado el 2026-11-22; las interfaces consumidoras siguen siendo obligatorias y Identity no borra datos de otro módulo.

## Tareas ejecutables sin cambiar contrato

La política y los contratos mínimos de Orders/Catalog están definidos. La evidencia real de Mongo/Redis/Testcontainers sigue pendiente porque Docker no está disponible en el entorno actual.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API005-01 · DOCUMENTAL aprobar política de baja integral | BR-011, RF-11, DEL-01/02/03; solo `spec.md`, `plan.md`, `task.md` | DEC-003 aprobado explícitamente el 2026-11-22: baja lógica/anonimización, `deletedUserRef` UUID, bloqueo por pedidos activos, productos no disponibles, revocación Redis y logout global best-effort. **Hecho**: acta documental en `spec.md`. | DONE; 2026-11-22 |
| DEV-API005-02 · IMPLEMENTACION DELETE y coordinación Identity | RF-11, DEL-01/04; `UsersController.cs`, nuevo `DeleteOwnAccountService.cs`, repositorio Identity, DI | Crear acción y servicio con sujeto JWT, `IActiveOrderReader` y `IAccountDeletionCatalog`, anonimización CAS de `users`, revocación Redis y logout global best-effort; no acceder a otras colecciones. **Hecho**: evidencia parcial en `DeleteOwnAccountServiceTests.cs` y build Release; integración real pendiente. | DONE; evidencia parcial 2026-11-22 |
| DEV-API005-03 · INTEGRACION consumir borrado por dueño | BR-011, DEL-02/03; `DeleteOwnAccountService.cs`, tests de integración, contratos publicados de Catalog/Orders | Interfaces mínimas publicadas; evidencia pendiente: ejecutar Mongo/Redis aislados y verificar pausado de productos con datos reales de prueba. | BLOCKED |
| DEV-API005-04 · QA_TECNICA privacidad y fallos | AC-007, T-AUTH-10, DEL-01/02/03/04; nuevos `DeleteOwnAccountEndpointTests.cs`, `DeleteOwnAccountSecurityTests.cs` | Ejecutar repetición, operación activa, JWT ajeno/revocado, dependencia caída y no pérdida de ajenos con Docker. Las pruebas de endpoint por dobles ya están añadidas; evidencia completa pendiente. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md). Resolver política y gates, confirmar interfaces de dueños antes de cambiar código; pruebas solo locales autorizadas.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API005-01 | DOCUMENTAL | DONE | NO | DEC-003 aprobado explícitamente el 2026-11-22 | - | 2026-11-22, [spec](spec.md): resultado documental aprobado |
| DEV-API005-02 | IMPLEMENTACION | DONE | SI | DEC-003 y contrato API-005 aprobados; interfaces mínimas publicadas | DEV-API005-01 | 2026-11-22, [DeleteOwnAccountServiceTests](../../../tests/UtttGo.UnitTests/DeleteOwnAccountServiceTests.cs): resultado unitario verde; integración pendiente |
| DEV-API005-03 | INTEGRACION | BLOCKED | SI | Pendiente: Mongo/Redis aislados con Docker | DEV-API005-01, DEV-API005-02 | PENDIENTE |
| DEV-API005-04 | QA_TECNICA | BLOCKED | SI | Pendiente: Docker y suites completas | DEV-API005-02, DEV-API005-03 | PENDIENTE |
