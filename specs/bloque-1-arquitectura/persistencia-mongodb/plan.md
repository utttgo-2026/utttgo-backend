# Persistencia MongoDB — plan

[Constitución v2.6](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Fuente histórica y [tabla ID individual](../base-modular/plan.md#correspondencia-exhaustiva-de-fase-00).

1. Cotejar `MongoOptions`, registro DI y ownership `users` (Identity)/`sellers` (Sellers) con código existente; no trasladar la colección obsoleta `sellerProfiles`.
2. Verificar índices pertinentes en Mongo local (incluido parcial `ownerUserId_unique`), datos históricos y caso de reintento/compensación sin exigir transacción no diseñada.
3. Contrastar roles locales con autorización de Identity y la sonda HP-11; trazar tareas pendientes de Identity/Users y Sellers sin reclamar cierre por este plan.
