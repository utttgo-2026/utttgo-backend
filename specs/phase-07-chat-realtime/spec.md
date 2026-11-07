# spec.md — Historial temporal de chat por pedido · Backend UTTT-GO
> Fase `phase-07-chat-realtime` · Versión 0.1 · **Borrador funcional; pendiente de diseño y aprobación integral** · 2026-11-03.
> Deriva de `docs/constitution.md` v2.3, BR-012 y DEC-005. No autoriza código de producción, almacenamiento concreto ni nuevas rutas.

## Decisión funcional confirmada

Los mensajes entre participantes de un pedido se conservan **temporalmente mientras el pedido esté en curso**. **El historial de esa conversación debe eliminarse dentro de los tres días posteriores a la finalización del pedido.** La eliminación de cuenta del participante, que elimina su información propia, también debe coordinarse con la política de datos compartidos de DEC-003, sin asumir que se conserva el chat por defecto.

## Contratos existentes

| Ficha | Ruta exacta del inventario | Alcance |
|---|---|---|
| API-037 | `GET /api/v1/orders/{orderId}/messages` | Lectura autorizada del historial temporal durante el plazo permitido, paginada conforme a la ficha. |
| API-038 | `POST /api/v1/orders/{orderId}/messages` | Envío autorizado entre participantes mientras el pedido permite chat; validar longitud y pertenencia. |

No introducir rutas adicionales ni compartir secretos o mensajes por notificaciones no autorizadas. SignalR requiere backplane si hay varias instancias; los endpoints REST y el hub deben aplicar autorización por pedido.

## Pendientes técnicos/contractuales, no decisiones funcionales abiertas

1. Definir qué transición de la máquina de estados de Orders constituye exactamente **pedido completado** y qué sucede con rechazados/cancelados. No equiparar automáticamente entrega con todo tipo de cierre sin aprobación.
2. Determinar almacenamiento duradero/temporal compatible con API-037 y reinicios/instancias múltiples; la antigua opción «solo memoria de sesión» no satisface por sí misma un historial consultable. No fijar `chatMessages`, TTL de MongoDB, Redis o proceso programado antes del plan técnico.
3. Definir ancla temporal de tres días, zona UTC, eliminación verificable, fallos/reintentos y comportamiento de lectura/envío cuando la conversación se completa o expira; impedir acceso residual por cachés, backups y notificaciones según política aprobada.
4. Resolver coordinación de eliminación de cuenta con historial que involucra a dos usuarios y pedidos compartidos (DEC-003); no inventar una excepción de retención a BR-011.

## Pruebas previstas (no ejecutadas)

- Participante autorizado lee y envía mientras el pedido está en curso; tercero recibe `403`.
- No se pierden mensajes requeridos durante un pedido activo por reinicio/múltiples instancias.
- La purga elimina el historial dentro de los tres días desde el evento aprobado; prueba con reloj controlado y fallo/reintento; el historial ya eliminado no reaparece en API-037, SignalR o cachés.
- Cancelación/rechazo, si aplica, sigue la política aprobada para cierre de pedido; eliminación de cuenta respeta DEC-003.

**Gate:** completar `spec.md` con transición de Orders y contratos de API-037/038, luego aprobar `plan.md` y `task.md` antes de implementar. La regla de tres días ya fue confirmada por el equipo; no se somete de nuevo a aprobación.
