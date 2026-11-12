# Contratos transversales — tareas

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → task. [Destino de ID históricos](../base-modular/plan.md#correspondencia-exhaustiva-de-fase-00).

- [ ] **TASK-BE-014**: comprobar política CORS API con orígenes explícitos, sin inferir resultado de CORS S3.
- [ ] **TASK-BE-016/017/018**: contrastar correlación en logs/respuesta, error sanitizado ante excepción y formato JSON en entorno aislado; no reusar `500` histórico como contrato nuevo sin revisión.
- [ ] **TASK-BE-019**: documentar orden observado y obtener aprobación AGENT-BE-01 antes de cualquier cambio.
- [ ] Revisar paginación `page`/`pageSize` por listado; `AdminUsersEndpointTests` con dobles no acreditan listado real universal.
