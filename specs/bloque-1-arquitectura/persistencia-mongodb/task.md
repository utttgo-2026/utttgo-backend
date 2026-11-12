# Persistencia MongoDB — tareas

[Constitución v2.6](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → task. Los ID originales se ubican individualmente en [base-modular/plan.md](../base-modular/plan.md#correspondencia-exhaustiva-de-fase-00); no equivalen a `[x]`.

- [ ] **TASK-BE-010/011**: inspeccionar opciones, DI y acceso a la base en entorno no productivo, distinguir ping de permiso a la base (HP-11).
- [ ] **T-004 (roles/propiedad)**: ejecutar caso con rol JWT discordante de perfil local y recurso ajeno; `docs/qa-api-006.md` registra solo dobles, no Mongo/Atlas real.
- [ ] Revisar índice único parcial de Sellers con histórico y enlace `users.sellerId`, compensación ante fallo parcial; no asumir que un test de índices prueba el flujo completo.
