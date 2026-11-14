# API-036 — Marcar notificación propia como leída · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo marca propia, no FCM ni acuse de pedido/chat.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: fase 06 y modelo API-035 pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción, resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API036-01 · DOCUMENTAL ratificar lectura | API-036, NOTR-01..04; `spec.md`, `plan.md`, `task.md` tras acta | Producto/Notifications/seguridad deciden readAt/replay, body, HTTP tercero/ausente/expirado y cotejan Excel. **Hecho**: acta y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-036-mark-notification-read` 0 errores. | BLOCKED |
| DEV-API036-02 · IMPLEMENTACION PATCH protegido | API-036, NOTR-01/02/04; `Modules/Notifications/Controllers/NotificationsController.cs`, `Validators/NotificationIdValidator.cs`, `Contracts/MarkNotificationReadResponse.cs`, `Application/MarkNotificationReadService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API036-01/API-035: derivar recipientUserId JWT, validar ID, DI sin tocar middleware/FCM. **Hecho**: `MarkNotificationReadServiceTests.cs` cubre actor/ID; build Release verde. | BLOCKED |
| DEV-API036-03 · IMPLEMENTACION update condicional | API-036, NOTR-01/03; `Modules/Notifications/Infrastructure/MongoNotificationRepository.cs`, `Application/MarkNotificationReadService.cs` | Tras DEV-API036-02: actualizar solo `_id+recipientUserId`, conservar fecha si replay aprobado e invalidar caché API-035 si existe. **Hecho**: `MarkNotificationReadEndpointTests.cs` prueba repetición/carrera con 2 usuarios sin afectar ajeno. | BLOCKED |
| DEV-API036-04 · QA_TECNICA propiedad/fallos | API-036, NOTR-01..04; `tests/UtttGo.UnitTests/MarkNotificationReadServiceTests.cs`, `tests/UtttGo.IntegrationTests/MarkNotificationReadEndpointTests.cs`, `tests/UtttGo.SecurityTests/MarkNotificationReadSecurityTests.cs` | Tras DEV-API036-02/03: JWT revocado, Redis/Mongo caídos, recurso expirado, FCM mock sin llamados; DEC-003 solo si aprobada. **Hecho**: evidencia fechada build/formato/tres suites Release verdes sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); no inferir aprobación por existencia de código o tests con dobles.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API036-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: fase 06/lectura ratificada | - | PENDIENTE |
| DEV-API036-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: API-035 y acta | DEV-API036-01 | PENDIENTE |
| DEV-API036-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: replay/fecha/caché aprobados | DEV-API036-02 | PENDIENTE |
| DEV-API036-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado, DEC-003 si aplica | DEV-API036-02/03 | PENDIENTE |
