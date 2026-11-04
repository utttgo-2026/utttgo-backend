# task.md — API-039 · Listar usuarios y vendedores

> Fase `phase-02-auth-users` · Versión documental **0.2.0** · 2026-11-04 · **Implementación y QA locales con dobles ejecutadas; integraciones reales no verificadas**.
> Deriva de `spec.md` 0.2.0 → `plan.md` 0.2.0 de esta subcarpeta; subordinado a `docs/constitution.md` v2.3. Autorización expresa de implementación **solo local** recibida el 2026-11-04; no equivale a aprobación integral del equipo ni despliegue. API-003 y TASK-AUTH-007/008 siguen pendientes.

## A. Ratificación registrada y gates aún pendientes

**Decisión local ratificada (2026-11-04):** acceso Admin/Active; respuesta exclusivamente `id`, `displayName`, `roles`, `accountStatus`; `search` normalizado solo en `displayName` (≤ 100), `role` Buyer/Seller/Admin, `accountStatus` Active/PendingEmailVerification/Suspended sujeto a comprobar modelo; `page` positivo y `pageSize` 1..50 (límite configurable inicial 50), ambos obligatorios; sort desc por defecto o asc y desempate por ID; `total` filtrado sin snapshot entre peticiones; auditoría obligatoria sin datos sensibles con `503` si falla; errores `400/401/403/429/503`. **Ratificación documental, no marca de trabajo realizado.**

- [ ] **TASK-039-001 — Verificar modelo de lectura antes de código.** Depende de: ratificación local en `spec.md` 0.2.0. Hecho cuando: se confirma en el modelo real soporte de `Active`, `PendingEmailVerification`, **`Suspended`**, `createdAt`, ID único y `displayName` normalizado; se define normalización/coincidencia segura sin búsqueda por email. `LocalLoginProfile.AccountStatus` actual es `string`; `Suspended` y `createdAt` no están acreditados por ese contrato. Si hay incompatibilidad, detener solo el filtro/orden afectados y solicitar conciliación, sin cambiarlos unilateralmente.
- [x] **TASK-039-002 — Publicar contrato de lectura del dueño actual (interfaz local).** Depende de: autorización expresa de implementación local; 001 sigue como gate del adaptador real. Hecho cuando: Identity publica `IAdminUserListReader` con fecha de creación requerida y proyección mínima, separada de `FindBySubjectAsync`, sin mover colecciones ni exponer repositorios a Admin. **No hay consulta/índices Mongo**; éstos permanecen en 020.
- [x] **TASK-039-003 — Gate de autorización de código local.** Depende de: SDD local 0.2.0 ratificado. Hecho cuando: se recibe instrucción **explícita posterior** para implementar API-039 localmente (recibida 2026-11-04); no acredita verificación 001, aprobación integral/TASK-AUTH-007/008 ni despliegue; middleware intacto.

## B. Implementación **local candidata**, autorizada y verificada con dobles

- [x] **TASK-039-010 — Contratos y validador Admin.** Depende de: 003. Hecho localmente: ruta `GET /api/v1/admin/users`, DTO de cuatro campos, paginación/query y validación de límites/enum/sort/duplicados. No se cambiaron API-003/040 ni contratos de seis candidatos previos.
- [x] **TASK-039-011 — Autorización, rate limit y auditoría (interfaces locales).** Depende de: 003/010. Hecho localmente: reutiliza sesión, rol Admin/Active local; `401/403/429/503`; auditoría obligatoria antes de `200`, fallo ⇒ `503`. Interfaces de Redis/auditoría sin adaptadores reales: default fail-closed; pruebas solo con dobles. **No declarar rate limit distribuido real.**
- [x] **TASK-039-012 — Lectura paginada por contrato (candidato local).** Depende de: 002/010/011. Hecho localmente: Admin solo consume `IAdminUserListReader` de Identity; filtros, búsqueda, orden/desempate, `total` y cuatro campos verificados mediante datos de prueba con fechas explícitas. La ausencia de `createdAt` persistido y del adaptador Mongo **no se declara resuelta**; 001/020 continúan abiertos y el host normal devuelve `503`.
- [x] **TASK-039-013 — QA local reproducible con dobles.** Depende de: 010..012. Hecho localmente: pruebas unitarias, `WebApplicationFactory` y seguridad para autorización, proyección, filtros/paginación/orden, fallos y auditoría; restore, build Release, formato y suite completos en verde. Cifras y comandos en `docs/qa-api-039.md`. No acredita integración externa ni 001/020.

## C. Integración real y cierre **separados**

- [ ] **TASK-039-020 — Integraciones externas y distribución.** Depende de: 001/002/013, TASK-AUTH-011/012 y aprobación integral cuando aplique. Hecho cuando: JWT real (firma/issuer/audience/exp/`jti`), blacklist y rate limit Redis compartidos, confirmación Supabase, rol/estado, paginación/índices Mongo y auditoría obligatoria se verifican con servicios no productivos; fallos Redis/Mongo/Supabase/auditoría no abren acceso, sin secretos en Git. Validar cambios de roles/estados y estabilidad/conteo bajo concurrencia; CI no llama a producción.
- [ ] **TASK-039-021 — Cierre de trazabilidad, auditoría y alcance.** Depende de: 013/020 y aprobaciones integrales requeridas por fase. Hecho cuando: evidencia real de tests/observabilidad/auditoría y matriz `spec ↔ plan ↔ task ↔ endpoint ↔ test` actualizada; nunca declarar API-040 implementada ni AC-005 terminada por listar solamente; no declarar listo para despliegue sin gates académicos/infraestructura/CI pendientes.

## Historial documental

| Versión | Fecha | Cambio / alcance |
|---|---|---|
| 0.1.0 | 2026-11-04 | Tareas propuestas con decisiones del contrato pendientes. |
| 0.2.0 | 2026-11-04 | Contrato SDD ratificado localmente y dependencias alineadas a `spec.md`/`plan.md` 0.2.0; implementación/QA/integración y aprobación integral siguen sin completarse. |
