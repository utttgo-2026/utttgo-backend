# Spec mínima — Notificaciones push FCM

**Estado:** aprobada para implementación; rutas nuevas limitadas a registro y
baja de `deviceTokens`. API-035/036, Chat e infraestructura IAM/SSM quedan
fuera de esta entrega.

**Rama:** `docs/spec-fcm`

**Alcance:** registro y baja de tokens de dispositivo, envío push derivado del
ciclo de Orders y diseño de integración con Firebase Cloud Messaging. No
implementa API-035/036 ni Chat.

## 1. Objetivo

Permitir que el backend envíe notificaciones push de pedidos a los dispositivos
activos de compradores y vendedores mediante Firebase Cloud Messaging (FCM)
Admin SDK. El cliente móvil nunca llama FCM directamente y nunca recibe la
credencial Firebase.

## 2. Registro De Dispositivos

### 2.1 Registrar o actualizar token

**Ruta propuesta:** `POST /api/v1/notifications/device-tokens`

**Acceso:** Bearer; cuenta `Active`. Aplica a compradores y vendedores.

**Headers:**

```http
Authorization: Bearer <accessToken>
Content-Type: application/json
```

**Request:**

```json
{
  "token": "<fcm-device-token>",
  "installationId": "<stable-installation-id>",
  "platform": "android",
  "appVersion": "1.0.0"
}
```

**Validación:**

- `token` obligatorio, no vacío, recortado y con máximo configurable de 4096 caracteres.
- `installationId` obligatorio, estable por instalación y máximo 128 caracteres.
- `platform` obligatorio: `android` o `ios`.
- `appVersion` opcional, máximo 32 caracteres.
- No aceptar `userId`, roles ni estado desde el cliente.
- La identidad se obtiene exclusivamente del JWT/perfil local.
- No registrar el token completo en logs, respuestas de error, trazas ni métricas.

**Respuesta nueva `201` o actualización idempotente `200`:**

```json
{
  "deviceTokenId": "665f2e508b3f2a0012ab3456",
  "platform": "android",
  "installationId": "<stable-installation-id>",
  "active": true,
  "updatedAt": "2026-11-23T19:00:00Z"
}
```

**Errores principales:**

- `400 VALIDATION_ERROR`
- `401 UNAUTHORIZED`
- `403 ACCOUNT_NOT_ACTIVE`
- `429 RATE_LIMITED`
- `503 NOTIFICATION_UNAVAILABLE`

### 2.2 Varios dispositivos

Un usuario puede registrar varios dispositivos simultáneamente. La colección
`deviceTokens` debe mantener como mínimo:

- `deviceTokenId`.
- `userId` del perfil local.
- Token FCM almacenado de forma protegida y nunca expuesto.
- `tokenHash` para deduplicación y diagnóstico sin imprimir el token.
- `installationId`.
- `platform`.
- `active`.
- `lastSeenAt`.
- `createdAt` y `updatedAt`.

Índices propuestos:

- Único por `userId + tokenHash`.
- Único por `userId + installationId`, con reemplazo atómico al rotar el token.
- Consulta por `userId + active` para obtener destinatarios.

El registro es idempotente: repetir el mismo token no crea otro documento.
Rotar el token de una instalación actualiza el documento existente sin afectar
los demás dispositivos del usuario.

### 2.3 Baja explícita del dispositivo

**Ruta propuesta:** `DELETE /api/v1/notifications/device-tokens/{deviceTokenId}`

**Acceso:** Bearer del propietario del dispositivo.

**Headers:**

```http
Authorization: Bearer <accessToken>
```

**Respuesta:** `204 No Content`.

**Errores principales:** `401`, `404 DEVICE_TOKEN_NOT_FOUND`, `503`.

La ruta solo puede afectar el `deviceTokenId` del usuario autenticado. No debe
aceptar un `userId` en la URL ni en el body.

### 2.4 Logout y borrado de cuenta

- El logout debe desactivar o eliminar los tokens asociados con la sesión o
  instalación que el cliente indique mediante el mecanismo aprobado.
- Debe existir una operación interna idempotente
  `RemoveDeviceTokensForUserAsync(userId)` para eliminar todos los tokens al
  completar API-005.
- La eliminación de cuenta no depende de que FCM responda.
- Un fallo de FCM o Mongo durante la limpieza debe quedar registrado sin
  restaurar la cuenta ni revertir el borrado ya confirmado; la operación de
  limpieza se reintenta de forma controlada.

## 3. Eventos De Pedido Que Disparan Push

Las notificaciones se derivan de hechos de dominio/outbox de Orders. El evento
debe incluir únicamente identificadores técnicos y el nuevo estado, nunca un
payload privado completo.

| Evento | Destinatario | Mensaje visible | `data` mínima |
|---|---|---|---|
| Pedido creado | Vendedor propietario | `Tienes un nuevo pedido.` | `eventType=order.created`, `orderId`, `status=Pending` |
| Cambio de estado | Comprador | `Tu pedido tiene una actualización.` | `eventType=order.status.changed`, `orderId`, `status` |
| Pedido listo/token disponible | Comprador | `Tu pedido está listo para recoger.` | `eventType=order.ready`, `orderId`, `status=Ready` |

### 3.1 Pedido creado

Se emite después de que Orders haya persistido el pedido y la reserva de stock
haya terminado correctamente. El destinatario se obtiene del `sellerId` del
pedido, nunca del cliente.

La notificación no debe incluir nombre, correo, teléfono, dirección, nota del
comprador, token de entrega ni el total del pedido.

### 3.2 Cambio de estado

Se emite después de un cambio de estado confirmado por CAS. El destinatario es
el comprador del pedido. La entrega de la notificación no participa en la
transición de estado.

No enviar notificaciones por una transición rechazada ni por un intento con
versión obsoleta.

### 3.3 Pedido listo/token de entrega

Se emite cuando el pedido llega a `Ready` y cuando el token de entrega queda
disponible para el comprador. El push nunca contiene el token. La aplicación
debe solicitarlo mediante la API protegida de entrega.

Si la emisión del token se repite, no generar mensajes duplicados para el mismo
hecho lógico.

## 4. Contrato De Mensaje FCM

El adaptador debe enviar una notificación visible y datos mínimos:

```json
{
  "notification": {
    "title": "Actualización de pedido",
    "body": "Tu pedido tiene una actualización."
  },
  "data": {
    "eventType": "order.status.changed",
    "orderId": "665f1d408b3f2a0012ab34ce",
    "status": "InPreparation"
  }
}
```

Reglas:

- No incluir PII, notas, correo, teléfono, dirección, token de entrega,
  credenciales, JWT, `jti` ni contenido completo del pedido.
- `data` debe permitir que la app navegue al detalle y vuelva a consultar el
  estado real; el push no es fuente de verdad.
- El servidor decide destinatarios, texto y evento.
- Los logs solo registran `eventType`, resultado, cantidad de destinatarios,
  `correlationId` y un identificador hash/no reversible del token.

## 5. Diseño Técnico

### 5.1 Frontera de aplicación

Notifications será dueño de `deviceTokens` y del envío FCM. Orders solo publica
hechos; no conoce credenciales ni SDK de Firebase.

```csharp
public interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}
```

El contrato real se definirá en la spec de implementación. Como mínimo,
`NotificationMessage` contiene `eventType`, destinatario interno, título,
body, `data`, `correlationId` e identificador idempotente del evento.

El adaptador de producción será `FirebaseAdminNotificationSender`, construido
con Firebase Admin SDK. Las pruebas usan un doble; CI nunca llama FCM real.

### 5.2 Envío no bloqueante

El request de Orders no espera la respuesta de FCM:

1. Orders persiste el cambio de negocio.
2. Orders registra/publica el hecho en un outbox durable.
3. Un dispatcher de Notifications toma el hecho.
4. El dispatcher resuelve los dispositivos activos.
5. `INotificationSender` envía a FCM fuera del request HTTP.
6. El resultado se registra y el outbox queda completado o pendiente de reintento.

FCM nunca forma parte de la transacción de pedido. Un fallo de FCM nunca
revierte creación, aceptación, preparación, disponibilidad, generación de
token ni entrega.

### 5.3 Reintento acotado

Reintentar únicamente errores transitorios:

- Timeout o error de red.
- Respuesta FCM `429`.
- Respuestas FCM `5xx`.

Política propuesta:

- Máximo 3 intentos por evento y destinatario.
- Backoff exponencial acotado, por ejemplo 1 s, 5 s y 30 s.
- Sin reintento para credencial inválida, payload inválido o token inválido.
- Después del máximo, marcar `Failed` y emitir métrica/alerta sin afectar Orders.
- Idempotency key basada en `eventId + deviceTokenId` para evitar duplicados.

### 5.4 Tokens inválidos

Cuando FCM devuelva `UNREGISTERED`:

1. Marcar el token como inactivo o eliminarlo de `deviceTokens`.
2. No reintentar ese token.
3. Continuar con los demás dispositivos del usuario.
4. Registrar solo `deviceTokenId`/hash, código FCM y `correlationId`.

También se debe documentar el tratamiento de `INVALID_ARGUMENT`: solo eliminar
el token cuando el diagnóstico confirme que el problema es el token, no cuando
el payload completo sea inválido.

## 6. Secretos Y Despliegue

### 6.1 Almacenamiento propuesto

La credencial de Firebase se almacenará en AWS Systems Manager Parameter Store
como parámetros `SecureString`, fuera de Git y fuera de Terraform con valores:

```text
/utttgo/{environment}/fcm/project-id
/utttgo/{environment}/fcm/client-email
/utttgo/{environment}/fcm/private-key
```

Alternativa no preferida para desarrollo local: variables de entorno en el
`.env` local ignorado por Git. Nunca almacenar la credencial en:

- Código fuente.
- `appsettings*.json`.
- Terraform variables versionadas.
- Docker image.
- Logs, snapshots o respuestas HTTP.
- El cliente Expo Go.

### 6.2 Cambios requeridos para el despliegue

La implementación futura requerirá, previa aprobación separada:

- Habilitar el proyecto Firebase y obtener la credencial sin incluir valores
  en el repositorio.
- Conceder al Instance Profile únicamente `ssm:GetParameters` sobre los tres
  parámetros FCM y `kms:Decrypt` sobre la clave específica, si aplica.
- Actualizar el script/documento SSM de despliegue para leer los parámetros y
  exportarlos al contenedor como variables de entorno.
- Añadir configuración no secreta de Firebase al contrato de configuración:
  `FCM_PROJECT_ID`, `FCM_CLIENT_EMAIL` y `FCM_PRIVATE_KEY`.
- Actualizar health/readiness solo si se decide que FCM es dependencia crítica;
  por defecto, FCM no debe volver `unhealthy` el API de Orders.
- Añadir pruebas mockeadas de FCM, limpieza `UNREGISTERED`, reintentos e
  idempotencia.
- Revisar permisos y despliegue mediante Terraform/SSM en una tarea separada.

Esta rama no modifica IAM, SCP, Terraform, SSM ni despliegues.

## 7. Fuera De Alcance

- API-035 `GET /api/v1/notifications`.
- API-036 `PATCH /api/v1/notifications/{notificationId}/read`.
- Historial de notificaciones dentro de la aplicación.
- Chat y SignalR.
- Envío FCM desde Expo Go.
- Topics públicos o suscripciones controladas por el cliente.
- Emails, SMS o WhatsApp.
- Firebase service account en el móvil.

## 8. Preguntas Abiertas

1. ¿Se aprueba `POST /api/v1/notifications/device-tokens` como ruta nueva,
   dado que API-035/036 permanecen fuera de alcance?
2. ¿La baja explícita será `DELETE /device-tokens/{deviceTokenId}` o se hará
   exclusivamente mediante logout interno y API-005?
3. ¿Debe el logout retirar un solo dispositivo, todos los dispositivos de la
   sesión o todos los dispositivos del usuario?
4. ¿Cuál será el identificador estable de instalación en Expo Go y cómo se
   rotará al reinstalar la aplicación?
5. ¿El token FCM se cifra a nivel de aplicación, se protege mediante cifrado
   de MongoDB Atlas o basta el cifrado en reposo del proveedor?
6. ¿Se aprueba MongoDB Outbox como mecanismo durable entre Orders y
   Notifications, o existe un bus/evento institucional que deba usarse?
7. ¿Cuál es el TTL de eventos fallidos y quién revisa la métrica de push
   fallidos?
8. ¿Se enviará un push por dispositivo o se usará multicast/batch de FCM?
9. ¿Se requiere localización de títulos y cuerpos para español/inglés?
10. ¿`Ready` y `delivery-token-issued` serán eventos distintos o un único
    evento idempotente para la UX móvil?
11. ¿Qué política se aplicará a tokens `INVALID_ARGUMENT` cuando FCM no permita
    distinguir token inválido de payload inválido?
12. ¿FCM será dependencia opcional en `/health` o se añadirá una sonda de
    configuración sin bloquear Orders?
13. ¿Se requiere conservar auditoría de cada entrega de push para soporte, sin
    almacenar contenido sensible?
