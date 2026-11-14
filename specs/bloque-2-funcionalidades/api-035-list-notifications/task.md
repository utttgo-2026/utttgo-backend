# API-035 — Listar notificaciones propias · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. GET propio no despacha push ni registra deviceTokens.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: fase 06 no aprobada.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción, resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API035-01 · DOCUMENTAL ratificar bandeja | API-035, NOTL-01..04; `spec.md`, `plan.md`, `task.md` tras acta | Producto/Notifications/seguridad acuerdan persistencia, eventos, DTO/TTL, filtros/page/HTTP, privacidad y cotejan Excel. **Hecho**: acta y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-035-list-notifications` 0 errores. | BLOCKED |
| DEV-API035-02 · IMPLEMENTACION lectura aislada | API-035, NOTL-01/03; `Modules/Notifications/Controllers/NotificationsController.cs`, `Contracts/NotificationsPageResponse.cs`, `Validators/NotificationsQueryValidator.cs`, `Application/ListNotificationsService.cs`, `Infrastructure/MongoNotificationRepository.cs`, `NotificationMongoIndexes.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API035-01 y modelo bandeja: filtrar destinatario servidor, paginar/ordenar en Mongo, DI sin tocar middleware. **Hecho**: `ListNotificationsEndpointTests.cs` separa dos usuarios; build Release verde. | BLOCKED |
| DEV-API035-03 · INTEGRACION eventos/privacidad | BR-008, NOTL-02/04; `Modules/Notifications/Application/ListNotificationsService.cs`, `tests/UtttGo.IntegrationTests/ListNotificationsEndpointTests.cs` | Tras DEV-API035-01/02 y hecho Orders/Chat aprobado: proyectar solo contenido permitido y manejar duplicados según acta, sin FCM en GET; no implementar publisher de Orders. **Hecho**: tests con doble FCM/eventos no exhiben duplicados ni texto purgado. | BLOCKED |
| DEV-API035-04 · QA_TECNICA aislamiento/fallos | API-035, NOTL-01..04; `tests/UtttGo.UnitTests/ListNotificationsServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListNotificationsEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListNotificationsSecurityTests.cs` | Tras DEV-API035-02/03: probar JWT revocado, Redis/Mongo caídos, límites y sin PII; baja solo tras DEC-003. **Hecho**: evidencia fechada build/formato/tres suites Release verdes, sin FCM ni producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); confirmar acta y no usar FCM real en tests.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API035-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: fase 06, persistencia/DTO | - | PENDIENTE |
| DEV-API035-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: modelo bandeja aprobado | DEV-API035-01 | PENDIENTE |
| DEV-API035-03 | INTEGRACION | BLOCKED | SI | Pendiente: política hechos/privacidad aprobada | DEV-API035-02 | PENDIENTE |
| DEV-API035-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y DEC-003 si aplica | DEV-API035-02/03 | PENDIENTE |
