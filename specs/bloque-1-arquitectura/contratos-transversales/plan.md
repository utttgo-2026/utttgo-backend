# Contratos transversales — plan

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Origen y destinos detallados en [matriz](../base-modular/plan.md#correspondencia-exhaustiva-de-fase-00).

1. Cotejar error único, correlationId y validadores con fichas/QA específicas, sin imponer un DTO de negocio global.
2. Revisar paginación de listados y CORS API en ambiente no productivo; el rate limit y errores 429/503 se coordinan con [Redis](../redis-rate-limit-revocacion/spec.md).
3. Revisar orden middleware solo mediante aprobación AGENT-BE-01; destino operativo de logging/health en lote 4, no cerrar aquí.
