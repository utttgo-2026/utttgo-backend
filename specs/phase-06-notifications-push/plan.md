# Plan documental — Notificaciones push FCM

**Estado:** implementación autorizada en `feature/fcm-push-v2`; no autoriza
credenciales reales, IAM/SCP, Terraform apply ni despliegue desde esta rama.

## Secuencia propuesta

1. Aprobar las preguntas abiertas de `spec.md` y el contrato de registro/baja.
2. Publicar el contrato de hechos de Orders sin introducir una dependencia
   síncrona hacia FCM.
3. Definir el modelo `deviceTokens`, índices, cifrado y política de borrado.
4. Definir el outbox y la idempotencia de eventos.
5. Implementar `INotificationSender` y el adaptador Firebase Admin SDK con
   doble para pruebas.
6. Implementar el dispatcher no bloqueante con reintentos acotados.
7. Configurar secretos mediante SSM Parameter Store y revisar permisos con
   Infraestructura/Security, sin ejecutar `terraform apply` en PR.
8. Probar creación de pedido, transiciones, Ready, logout, API-005,
   `UNREGISTERED`, errores transitorios y duplicados.

## Dependencias

- Orders: hechos `OrderCreated`, `OrderStatusChanged` y `DeliveryTokenReady`.
- Identity: usuario autenticado, logout y borrado de cuenta.
- MongoDB: `deviceTokens` y outbox durable.
- Redis: rate limit del registro y deduplicación temporal si se aprueba.
- Firebase Admin SDK: adaptador de envío exclusivamente server-side.
- AWS SSM/IAM/SSM deploy: solo después de aprobación y revisión separada.

## No Incluye

API-035, API-036, Chat, SignalR, UI móvil ni cambios de infraestructura.
