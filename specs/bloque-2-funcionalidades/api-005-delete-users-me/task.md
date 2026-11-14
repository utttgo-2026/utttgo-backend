# API-005 — Eliminar cuenta propia · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No empezar código antes de resolver DEC-003; interfaces consumidoras, no borrar datos de otro módulo desde Identity.

## Tareas ejecutables sin cambiar contrato

No hay tarea técnica READY: la política de datos compartidos es indispensable. Las tareas DEV siguientes quedan explícitamente bloqueadas.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API005-01 · DOCUMENTAL decidir borrado integral | BR-011, RF-11, DEL-01/02/03; solo `spec.md`, `plan.md`, `task.md` | DEC-003: pedir acta de producto/privacidad/dueños sobre compartidos, pedidos activos, plazo, orden Supabase, reintentos, respuesta e interfaces. **Hecho**: acta fechada, ficha conciliada con Excel autorizado y validador de ficha sin errores. | BLOCKED |
| DEV-API005-02 · IMPLEMENTACION DELETE y coordinación Identity | RF-11, DEL-01/04; `UsersController.cs`, nuevo `DeleteOwnAccountService.cs`, repositorio Identity, DI | Tras DEV-API005-01 y TASK-AUTH-007→008→010: crear acción y servicio con sujeto JWT, interfaz para dueños, eliminación de `users`/Supabase en orden aprobado; no acceder a otras colecciones. **Hecho**: tests unitarios `DeleteOwnAccountServiceTests.cs`, endpoint `DeleteOwnAccountEndpointTests.cs`, Release verde. | BLOCKED |
| DEV-API005-03 · INTEGRACION consumir borrado por dueño | BR-011, DEL-02/03; `DeleteOwnAccountService.cs`, tests de integración, contratos publicados de Sellers/Catalog/Media/Orders/Chat | Tras DEV-API005-01/02 y disponibilidad de interfaces aprobadas: inyectar consumidores, coordinar estados, reintento/fallo parcial; implementación interna sigue en módulos dueños. **Hecho**: pruebas de vendedor con publicaciones, media e historial propios eliminados y pedido ajeno preservado en entorno aislado. | BLOCKED |
| DEV-API005-04 · QA_TECNICA privacidad y fallos | AC-007, T-AUTH-10, DEL-01/02/03/04; nuevos `DeleteOwnAccountEndpointTests.cs`, `DeleteOwnAccountSecurityTests.cs` | Tras DEV-API005-02/03 y permiso Mongo/Redis no productivo: probar repetición, operación activa, JWT ajeno/revocado, dependencia caída y no pérdida de ajenos. **Hecho**: evidencia fechada con build/formato y suites Release, estado de datos final y errores `correlationId` sin secretos. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md). Resolver política y gates, confirmar interfaces de dueños antes de cambiar código; pruebas solo locales autorizadas.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API005-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: DEC-003 y política de compartidos | - | PENDIENTE |
| DEV-API005-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEC-003, TASK-AUTH-007/008/010 y contrato API-005 | DEV-API005-01 | PENDIENTE |
| DEV-API005-03 | INTEGRACION | BLOCKED | SI | Pendiente: DEC-003, contratos dueños y gates SDD | DEV-API005-01, DEV-API005-02 | PENDIENTE |
| DEV-API005-04 | QA_TECNICA | BLOCKED | SI | Pendiente: DEC-003 y pruebas aisladas autorizadas | DEV-API005-02, DEV-API005-03 | PENDIENTE |
