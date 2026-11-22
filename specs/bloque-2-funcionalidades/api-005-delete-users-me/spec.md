# API-005 — Eliminar cuenta propia · spec

Estado de aprobación de la ficha: **PENDIENTE DE APROBACIÓN**
Estado operativo: **BLOCKED**
Estado de DEC-003: **PROPUESTA**, no aprobada para implementación

## Identificación y autoridad

API-005 `DELETE /api/v1/users/me` según la [trazabilidad](../../../docs/traceability.md), RF-11 y BR-011. La autoridad es `docs/constitution.md` → esta ficha → [plan](plan.md) → [task](task.md). La política de esta ficha es una propuesta del propietario; no autoriza código hasta su aprobación explícita.

## Responsabilidad

Iniciar la baja de la cuenta autenticada y coordinar los módulos dueños de los datos. No acepta `userId` en URL o body, no elimina cuentas ajenas, no sustituye API-003, no es la baja puntual de un producto y no permite al cliente llamar la Admin API de Supabase.

## Política propuesta de DEC-003

1. La cuenta local pasa a `accountStatus=Deleted`.
2. Se anonimizan `displayName`, `email`, `phone` y `avatarMediaAssetId`/avatar. La propuesta de valores es `displayName="Cuenta eliminada"`, `phone=null`, `avatarMediaAssetId=null` y un correo no entregable, único y no reversible como `deleted+<deletedUserRef>@invalid.utttgo.local`.
3. Se genera un `deletedUserRef` estable, no derivado del `sub` ni del correo. Los pedidos y reseñas se conservan para la integridad e historial del sistema, pero las referencias al usuario eliminado se sustituyen por ese identificador anonimizado. La identidad del otro participante no se modifica.
4. Si la cuenta es vendedor, sus productos/publicaciones pasan a no disponibles (`status=Paused`) y no pueden recibir nuevos pedidos. No se borran desde Identity ni se eliminan pedidos históricos.
5. La baja se bloquea con `409` si existe al menos un pedido activo donde el usuario sea comprador o vendedor. Para esta propuesta son activos `Pending`, `Accepted`, `InPreparation` y `Ready`; los estados terminales no bloquean.
6. Solo después de superar el preflight de pedidos activos se revoca la sesión actual con el mismo identificador de revocación de API-003: `session_id`, después `jti`, y finalmente SHA-256 del token. La clave es `revoked:{id}` con TTL hasta `exp`.
7. El backend deshabilita la identidad correspondiente en Supabase mediante la Admin API. La `service_role` nunca se entrega al móvil, se registra ni se incluye en respuestas.

La propuesta conserva la información operacional mínima sin conservar la identidad directa. Debe ratificarse si `status=Paused` satisface BR-011 para productos/publicaciones, que funcionalmente exige eliminar la información propia; si no, el dueño de Catalog debe definir una eliminación/anonimización compatible con pedidos históricos.

## Contrato HTTP propuesto

### Solicitud

```http
DELETE /api/v1/users/me
Authorization: Bearer <access-token>
```

Sin body, query `userId` ni identificador alternativo. El sujeto se obtiene únicamente del JWT validado y el usuario local se resuelve por esa identidad.

### Respuestas

| HTTP | Código | Condición | Efecto |
|---|---|---|---|
| `204` | — | Baja completada, sesión revocada y usuario Supabase deshabilitado | Sin body. El móvil debe borrar tokens locales. |
| `401` | `UNAUTHORIZED` | Falta Bearer, JWT inválido/expirado/revocado, sujeto no válido o cuenta ya `Deleted` | No modifica datos. |
| `409` | `ACTIVE_ORDER_CONFLICT` | Hay pedido activo como comprador o vendedor | No modifica datos ni revoca la sesión. |
| `503` | `ACCOUNT_DELETION_UNAVAILABLE` | Redis, MongoDB, módulos dueños o Supabase Admin no están disponibles o no permiten verificar/completar la operación | No confirma la baja; el detalle de consistencia/reintento sigue siendo gate de diseño. |

Todos los errores usan el contrato único y `correlationId`. No se devuelve PII, `sub`, `userId` interno, `service_role`, token ni detalle del proveedor.

## Secuencia propuesta

1. Leer un único Bearer y validar firma, issuer, audience, expiración, sujeto y blacklist Redis mediante el gate compartido.
2. Resolver el usuario local y rechazar estados no operables.
3. Consultar pedidos activos en Orders como comprador y como vendedor. Si existe alguno, responder `409` sin mutaciones.
4. Ejecutar un preflight de referencias propias en Sellers, Catalog, Media, Reviews y Orders mediante interfaces de sus módulos dueños; Identity no accede a colecciones ajenas.
5. Crear o recuperar de forma idempotente `deletedUserRef` y aplicar la anonimización/baja lógica local.
6. Pausar los productos propios del vendedor y retirar el avatar/activos propios según el contrato de Media.
7. Sustituir las referencias del usuario en pedidos y reseñas preservados por `deletedUserRef`, sin alterar al otro participante.
8. Revocar la sesión actual en Redis con la misma política de API-003. Si Redis falla, responder `503` fail-closed.
9. Deshabilitar el usuario en Supabase con una interfaz server-side de Admin API. La credencial privilegiada solo puede existir en el entorno de backend.
10. Responder `204` solo cuando la política aprobada considere completada la coordinación. La operación debe ser segura al reintentarse.

La atomicidad entre MongoDB, Redis, módulos dueños y Supabase no se da por resuelta: el plan debe elegir transacción/outbox/estado de proceso o una compensación verificable antes de implementar.

## Seguridad y datos

- El cliente no puede decidir identidad, estado, referencias, anonimización ni qué productos pausar.
- La `service_role` de Supabase no se incluye en móvil, OpenAPI, logs, variables públicas, `appsettings` ni repositorio.
- Redis es fail-closed para la revocación de la sesión actual.
- La operación debe ser autorizada por propiedad del sujeto y no por un rol enviado por el cliente.
- Los pedidos y reseñas conservados no deben retener correo, teléfono, avatar, JWT, `sub` ni referencias directas al usuario eliminado.
- Los logs solo pueden registrar resultado, `correlationId` y categoría segura del fallo.

## Escenarios Given/When/Then

| ID | Dado | Cuando | Entonces | Estado |
|---|---|---|---|---|
| DEL-01 | Usuario autenticado sin pedidos activos | DELETE `/users/me` | Se anonimiza el perfil, se revoca la sesión, se deshabilita Supabase y responde `204` | Propuesta |
| DEL-02 | Comprador con pedido `Pending`, `Accepted`, `InPreparation` o `Ready` | DELETE `/users/me` | Responde `409 ACTIVE_ORDER_CONFLICT` y no muta datos | Propuesta |
| DEL-03 | Vendedor sin pedidos activos | DELETE `/users/me` | Sus productos pasan a `Paused`; pedidos/reseñas se conservan con `deletedUserRef` | Propuesta |
| DEL-04 | JWT ausente, inválido, expirado o revocado | DELETE `/users/me` | Responde `401 UNAUTHORIZED` sin mutaciones | Propuesta |
| DEL-05 | Redis no disponible antes de revocar | DELETE `/users/me` | Responde `503`; no confirma la baja ni llama a Supabase Admin | Propuesta |
| DEL-06 | Supabase Admin falla después de cambios locales | DELETE/reintento | Responde `503` sin afirmar éxito; el mecanismo de recuperación/idempotencia debe evitar inconsistencias | Abierta |
| DEL-07 | Solicitud repetida con cuenta ya `Deleted` | DELETE `/users/me` | Responde `401` sin revelar ni reactivar la cuenta | Propuesta |

## Trazabilidad

| Fuente | Cobertura |
|---|---|
| RF-11 / AC-007 / T-AUTH-10 | Baja propia, datos del vendedor y seguridad de la operación |
| BR-009 / BR-011 | Identidad server-side, propiedad y tratamiento de información propia |
| DEC-003 | Datos compartidos, pedidos activos, consistencia, reintentos y Supabase |
| API-003 | Identificador y TTL de revocación de la sesión actual |

## Riesgos

1. La baja lógica propuesta puede contradecir la obligación constitucional de eliminar productos/publicaciones propios.
2. Una operación distribuida puede dejar datos anonimizados localmente mientras Supabase continúa habilitado, o viceversa.
3. Orders, Reviews, Catalog, Sellers y Media todavía no exponen contratos aprobados de borrado/anonimización por dueño.
4. La `service_role` tiene privilegios elevados; una filtración permitiría administrar identidades del proyecto Supabase.
5. El almacenamiento de la `service_role` en la EC2/SSM aún no tiene una decisión operativa documentada ni evidencia de rotación.
6. Reseñas y mensajes históricos pueden contener PII dentro del texto aunque se anonimice el autor.
7. La retención de pedidos puede tener requisitos académicos, legales o de auditoría que no están definidos.
8. Cambiar referencias a un identificador anonimizado puede afectar consultas, índices, reportes y futuras notificaciones.
9. La existencia de pedidos activos debe evaluarse consistentemente entre las dos réplicas y MongoDB.

## Preguntas abiertas y gates de aprobación

1. ¿Se aprueba que los productos del vendedor queden `Paused` y se conserven, pese a BR-011, o deben eliminarse/anonymizarse mediante Catalog?
2. ¿Cuál es el contrato exacto de Supabase Admin para deshabilitar una identidad y qué duración/estado representa “deshabilitado”?
3. ¿La `service_role` se guardará como secreto de la EC2, en AWS Systems Manager Parameter Store o mediante otro mecanismo aprobado? ¿Quién rota y audita ese secreto?
4. ¿Se autoriza que la EC2 lea ese secreto desde SSM mediante Instance Profile, sin exponerlo a Docker logs ni al cliente?
5. ¿Qué estrategia garantiza consistencia entre MongoDB, Redis, Supabase y módulos dueños: transacción, outbox, saga local o estado de proceso?
6. ¿Qué debe ocurrir si Supabase Admin falla después de anonimizar MongoDB: reintento automático, estado `DeletionPending` o compensación?
7. ¿El `deletedUserRef` debe ser UUID aleatorio, HMAC con secreto del servidor u otro pseudónimo estable?
8. ¿Debe anonimizarse también el contenido textual de reseñas/mensajes y qué ventana de retención aplica?
9. ¿Se eliminan físicamente los objetos S3 del avatar y cuándo se purgan sus metadatos `mediaAssets`?
10. ¿Se requiere auditoría administrativa de la baja y cuánto tiempo puede conservar eventos sin PII?

## Criterios para desbloquear código

- Aprobación explícita de esta política y de las respuestas a las preguntas abiertas críticas.
- Contratos publicados de Orders, Sellers, Catalog, Media y Reviews para consultar/borrar/anonymizar por dueño.
- Decisión de secreto Supabase Admin y evidencia de configuración segura fuera de Git.
- Estrategia de consistencia, reintento e idempotencia aprobada.
- Pruebas de comprador, vendedor, pedidos activos, Redis/Supabase caídos, repetición y ausencia de PII.
