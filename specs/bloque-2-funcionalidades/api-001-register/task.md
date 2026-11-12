# API-001 — Registro · tareas

> Lote 2a · **Pendiente de aprobación**. Estas casillas describen trabajo por acreditar; no cambian el estado de [TASK-AUTH de fase 02](../../phase-02-auth-users/task.md).

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo documentación API-001; sin código ni curl nuevos.

- [x] **Ficha documental:** cotejar ruta, DTO, validador, servicio, filtro de 8 KiB y gate Redis con el código; separar `202` individualmente aprobado de los demás HTTP/DTO candidatos. Resultado: este [spec](spec.md#alcance-y-contrato-vigente-código-no-ratificación). No equivale a TASK-AUTH-020 terminado.
- [x] **Evidencia acotada:** registrar con etiqueta propia tests con dobles de `docs/qa-api-001.md`, dos resultados locales del 2026-11-06 y presencia de ruta en OpenAPI AWS del 2026-11-12, sin curl nuevo ni datos sensibles. No afirmar verificación AWS.
- [ ] **TASK-AUTH-002 / T-AUTH-14:** aprobar y verificar flujo OTP/correo institucional, estado pendiente, reconciliación segura y SMTP; RF-15/AC-011 abiertos. No habilitar acceso privado por `202` ni introducir ruta sin ficha.
- [ ] **TASK-AUTH-007 → 008 → 010:** obtener aprobación identificable del equipo/delegación para política de versiones y SDD; ratificar DTO/HTTP de API-001 y límite de tamaño. TASK-AUTH-001/006 siguen `[x]` **solo documentalmente**.
- [ ] **TASK-AUTH-011/012/020/030; T-AUTH-01/03/05/15/18/19:** probar Supabase/Atlas/Redis de prueba, duplicados reales, perfil `Buyer` pendiente, fallos parciales, rate limit distribuido y error fail-closed sin secretos. Los dobles y dos respuestas reales locales no cierran este conjunto.
- [ ] **RF-06 (fuera de API-001):** mantener recuperación por enlace en Supabase Auth como funcionalidad pendiente de diseño técnico; nueva ruta backend solo con ficha aprobada.
- [ ] **Cierre del lote:** revisión humana de esta ficha y posterior conciliación de trazabilidad con el clerk al cerrar el lote; fase 02 se conserva en su ubicación actual.
