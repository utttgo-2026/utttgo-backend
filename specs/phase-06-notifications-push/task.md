# Tareas documentales — Notificaciones push FCM

**Estado:** aprobada para implementación en `feature/fcm-push-v2`. La
configuración productiva de la credencial y la evidencia con Docker quedan
pendientes de entorno; no se incluyen valores secretos.

| ID | Tarea | Estado |
|---|---|---|
| FCM-001 | Aprobar registro y baja de `deviceTokens`. | IMPLEMENTADA; endpoints y Mongo |
| FCM-002 | Aprobar hechos de Orders y destinatarios. | IMPLEMENTADA; outbox de Orders |
| FCM-003 | Aprobar interfaz `INotificationSender` y adaptador Firebase. | IMPLEMENTADA; FirebaseAdmin 3.4.0 |
| FCM-004 | Aprobar outbox, no bloqueo de Orders, reintentos e idempotencia. | IMPLEMENTADA; reintentos 1/5/30 s, máximo 3 |
| FCM-005 | Aprobar limpieza `UNREGISTERED` y política de errores FCM. | IMPLEMENTADA; desactivación sin reintento |
| FCM-006 | Aprobar almacenamiento de secretos en SSM y cambios requeridos del deploy. | PARCIAL; env/path y Compose listos, IAM/SSM fuera de esta tarea |
| FCM-007 | Definir pruebas mockeadas y evidencia sin credenciales reales. | PARCIAL; unit/integration/security FCM aisladas, Docker pendiente |
| FCM-008 | Mantener API-035/036 y Chat fuera de esta fase. | RESUELTA POR ALCANCE |
