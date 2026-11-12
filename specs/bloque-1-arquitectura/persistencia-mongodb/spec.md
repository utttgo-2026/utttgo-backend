# Persistencia MongoDB — spec

> Lote 1 · 2026-11-12 · **Pendiente de aprobación**, sin traslado de aprobación histórica.

Autoridad: [constitución v2.6](../../../docs/constitution.md) §§ 2, 4–6, 11, 15–16 → esta ficha → [plan](plan.md) → [task](task.md). [Mapa exhaustivo de ID](../base-modular/plan.md#correspondencia-exhaustiva-de-fase-00). [Arquitectura](../../../skills/be-architecture.md) · [API](../../../skills/be-api-design.md) · [memoria](../../../skills/be-memory.md).

## Alcance

REQ-005: dueño de datos/índices por módulo, driver oficial y proyecciones DTO sin exponer documentos. REQ-006: roles del perfil `users` local, no del JWT; su gate de autenticación se concreta en Identity (lote 2a). Según DEC-019, `Sellers` escribe `sellers` (ID propio, `ownerUserId` con índice único parcial para documentos heredados), `users.sellerId` es vínculo inverso: actualización del perfil local solo mediante contrato del dueño Identity. No reintroducir `sellerProfiles`; índices de Orders/Catalog requieren sus fichas, no se inventan aquí. El mapa vigente está en [memoria](../../../memoria.md#mapa-de-colecciones-por-módulo).

## Aceptación verificable

| Dado / cuando | Entonces / límite |
|---|---|
| Un módulo lee o escribe una colección de otro | No accede directamente; usa contrato publicado por el dueño; revisar dependencias y test de autorización por recurso. |
| Se activa vendedor dos veces o hay `sellers` históricos sin `ownerUserId` | Verificar índice parcial y reintento seguro con Mongo 7 local; vínculo en `users` coherente tras fallos parciales; este último caso queda pendiente. |
| Un JWT afirma rol distinto del perfil local | Solo el rol del perfil local autoriza; test de seguridad con perfil Mongo real pendiente. |
| Se declara base disponible | Sonda verifica acceso a la base configurada además del ping (HP-11); la sonda no prueba índices ni transacciones. |

## Evidencia

- [**Real en AWS: tres curl, fecha y límites**](../base-modular/spec.md#evidencia-en-aws-2026-11-12): sonda MongoDB `Healthy` y lectura de vendedor existente/ausente vía API; no prueba índices, compensación ni roles locales.
- Histórica: `memoria.md` entrada 2026-11-08 documenta comprobación del índice único parcial en contenedor MongoDB 7 y suites de vendedor **con dobles**, sin acreditar Atlas ni compensación multi-documento. `docs/qa-api-039.md` § Límites: `AdminUsers*Tests` usan fixtures; no hay prueba allí de índice/lector persistido.
- `specs/cross-cutting/api-health-probe/spec.md` HP-11 documenta test de clasificación de acceso denegado `ClassifyCommandCode`; `/health 200` no prueba autorización de datos de negocio.
- **Pendiente de MongoDB:** coherencia del vínculo `users.sellerId`, roles locales, índices/fronteras y compensación ante fallos; los curl no se reejecutaron aquí.
