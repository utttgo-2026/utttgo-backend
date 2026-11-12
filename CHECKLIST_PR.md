# Checklist de PR — migración SDD atómica

> Un PR por lote. Fuente normativa: [constitución](docs/constitution.md); destinos y gates: [matriz](docs/migracion-sdd.md). Esta lista no concede aprobación de contratos ni sustituye CI.

- [ ] Rama del lote creada desde `origin/main`; diff acotado al lote, sin cambios de código, constitución, Excel, pruebas, workflows ni infraestructura durante la migración documental.
- [ ] `spec.md` → `plan.md` → `task.md` por ficha afectada; aprobación humana identificada, sin asumirla por mover archivos. Specs ≤~2 páginas, con aceptación verificable y «Evidencia».
- [ ] Cada RF/RNF/AC/TASK/API original conserva ID y destino o referencia histórica; API-016 retirada sin spec/ruta nueva, DEC-019.d independiente y 42 fichas originales intactas.
- [ ] Evidencia «Verificado» cita prueba concreta o curl documentado con fecha/entorno; tests con dobles identificados como tales. No atribuir admin/users `200` ni verificación externa de API-009/010/DEC-019.d sin documento específico.
- [ ] @traceability-clerk emitió tabla de verificación; @spec-keeper guardó `docs/migracion-sdd/lote-N.md` y añadió una fila a `memoria.md`.
- [ ] @security-reviewer informó hallazgos y se resolvieron los bloqueantes; diff sin secretos y enlaces internos vivos.
- [ ] CI del PR en verde antes de fusionar; documentar URL/run y resultado cuando exista. No marcar CI verde antes de su ejecución.
