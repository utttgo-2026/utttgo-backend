# Redis: tareas de verificación

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → task. [Matriz ID](../base-modular/plan.md#correspondencia-exhaustiva-de-fase-00).

- [ ] **T-004 / TASK-BE-013**: probar JWT válido/invalidado y rol local con Redis real no productivo; dobles de `LoginSecurityTests` no bastan.
- [ ] **T-005 / TASK-BE-012/015**: verificar DI, fallo cerrado de limitador general/auth y blacklist, fail-open solo caché; probar dos réplicas y registrar alerta; sin fijar cuotas nuevas.
- [ ] **AC-005/006**: exigir evidencia de logout API-003, TTL y `429` con contrato de error; no cerrar por la prueba manual de sexto login.
