# spec.md — Historial temporal de chat por pedido · Backend UTTT-GO
> Fase `phase-07-chat-realtime` · Versión 0.2 · **Aprobada para API-037/API-038** · 2026-11-25.
> Deriva de `docs/constitution.md` v2.6, BR-012 y DEC-005. Esta aprobación cubre las dos rutas REST de esta entrega; SignalR, notificaciones y la purga programada quedan fuera.

## Decisión funcional confirmada

Los mensajes entre participantes de un pedido se conservan **temporalmente mientras el pedido esté en curso**. **El historial de esa conversación debe eliminarse dentro de los tres días posteriores a la finalización del pedido.** La eliminación de cuenta del participante, que elimina su información propia, también debe coordinarse con la política de datos compartidos de DEC-003, sin asumir que se conserva el chat por defecto.

## Contratos existentes

| Ficha | Ruta exacta del inventario | Alcance |
|---|---|---|
| API-037 | `GET /api/v1/orders/{orderId}/messages` | Lectura autorizada del historial temporal durante el plazo permitido, paginada conforme a la ficha. |
| API-038 | `POST /api/v1/orders/{orderId}/messages` | Envío autorizado entre participantes mientras el pedido permite chat; validar longitud y pertenencia. |

No introducir rutas adicionales ni compartir secretos o mensajes por notificaciones no autorizadas. SignalR requiere backplane si hay varias instancias; los endpoints REST y el hub deben aplicar autorización por pedido.

## Contrato ratificado para esta entrega

- API-037 acepta `page` (mínimo 1) y `pageSize` (1..50), por defecto `1` y `20`; responde `{items,page,pageSize,totalCount}` ordenado por `createdAt` ascendente y luego ID.
- API-038 acepta únicamente `{ "content": "..." }`, con trim y sanitización básica de HTML/control characters; el texto resultante debe tener 1..1000 caracteres.
- API-038 requiere `Idempotency-Key`; repetir la misma clave y contenido devuelve `201` sin duplicar, y reutilizarla con otro contenido devuelve `409 IDEMPOTENCY_KEY_REUSED`.
- Un usuario que no sea comprador o vendedor asignado recibe `404 ORDER_NOT_FOUND`, sin revelar la existencia del pedido.
- Los estados terminales `Delivered`, `Rejected` y `CancelledByBuyer` rechazan envíos con `409 ORDER_CHAT_CLOSED`; la lectura permanece disponible mientras exista historial no purgado.
- Los mensajes se almacenan en MongoDB en `orderMessages`; Chat consume la participación/estado mediante Orders y no lee su colección.
- La purga posterior se coordina con `completedAt` y debe eliminar el historial dentro de tres días UTC; el mecanismo programado queda como tarea posterior.

## Pendientes técnicos posteriores a esta entrega

1. Implementar y verificar el job de purga con `completedAt`, reloj UTC, reintentos y evidencia de ausencia de contenido residual.
2. Resolver coordinación de eliminación de cuenta con historial compartido conforme a DEC-003; no inventar una excepción a BR-011.
3. Diseñar SignalR con backplane Redis y cualquier notificación FCM en una ficha posterior aprobada.

## Evidencia de pruebas

- Participante autorizado lee y envía mientras el pedido está en curso; tercero recibe `404`.
- No se pierden mensajes requeridos durante un pedido activo por reinicio/múltiples instancias.
- La purga elimina el historial dentro de los tres días desde el evento aprobado; prueba con reloj controlado y fallo/reintento; el historial ya eliminado no reaparece en API-037, SignalR o cachés.
- Cancelación/rechazo, si aplica, sigue la política aprobada para cierre de pedido; eliminación de cuenta respeta DEC-003.

**Gate:** API-037/API-038 y la fase REST quedan aprobadas para esta implementación con los límites anteriores. La purga, SignalR y notificaciones requieren tareas y aprobación separadas.

Build Release, `dotnet format --verify-no-changes` y las suites Unit/Integration/Security se ejecutaron con Docker activo el 2026-11-25: 409/308/172 pruebas superadas. Las pruebas no sustituyen evidencia de la purga, SignalR o FCM, que están fuera de esta entrega.
