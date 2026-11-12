# Base modular — spec

> Migración documental lote 1 · 2026-11-12 · **Pendiente de aprobación**. La aprobación histórica de fase 00 v1.1 no aprueba su borrador v1.3 ni esta ficha.

Autoridad: [constitución v2.6](../../../docs/constitution.md) §§ 2–5, 15–17 → esta ficha → [plan](plan.md) → [tareas](task.md). [Matriz ID por ID](plan.md#correspondencia-exhaustiva-de-fase-00). Guías: [arquitectura](../../../skills/be-architecture.md), [diseño API](../../../skills/be-api-design.md), [memoria](../../../skills/be-memory.md).

## Alcance y aceptación

Un solo artefacto desplegable, módulos dueños de su lógica, contratos publicados entre módulos y dependencias acíclicas. Estado de sesión fuera del proceso para las dos réplicas; la solución real es `UTTTGO.Backend.slnx`, no la `.sln` sugerida en el plan histórico. No define endpoints de negocio ni resuelve gates de infraestructura. REQ-001, REQ-016 y el aspecto de composición de REQ-002 tienen dueño aquí; REQ-003/004 y reglas de negocio REQ-009..014/017..020 se conservan como referencias constitucionales y destinos posteriores en la matriz, sin convertir esta ficha en una segunda constitución.

| Escenario verificable | Resultado exigido |
|---|---|
| Dado un módulo que necesita datos de otro, cuando se revisa su dependencia | Usa contrato del dueño; no accede a su repositorio/colección y no crea ciclo (§ 4). |
| Dadas dos réplicas detrás de Nginx, cuando cambia la réplica que atiende una petición | No depende de sesión en memoria; tiempo real requiere backplane (§§ 4, 10). Prueba multirréplica aún pendiente. |
| Dada una modificación funcional, cuando se pide implementación | Hay spec aprobada y plan/task trazados; esta migración no concede esa aprobación. |

**AC-001/002/003/004/011/012 (fase 00):** aceptación transversal, no marcada completa aquí: revisión del stack y BR contra constitución, build/formato, ausencia de artefactos prohibidos, pruebas y trazabilidad ID por ID, aprobación humana identificada. AC-012 permanece abierta para v1.2/v1.3 y estas fichas.

## Evidencia

- Documental: `docs/constitution.md` §§ 2–5, `AGENTS.md` § 1.2 y `memoria.md` documentan solución real y módulos; no equivalen a prueba multirréplica ni aprobación.
- Histórica (no ejecutada en este lote): `docs/qa-api-001.md` § Evidencia reproducible registra build/formato y `RegisterEndpointTests` con dobles (2026-11-04, SDK preview). No prueba cumplimiento global de REQ-001 ni AC-002 hoy.
- Pendiente: revisión de dependencias acíclicas, CI en PR y aprobación humana de esta ficha; no se atribuye «Verificado» al traslado.

## Evidencia en AWS (2026-11-12)

Curl **comunicados por el propietario**, sin credenciales, contra EC2 detrás de Nginx con TLS (`nip.io`); no se reejecutaron en este cambio. Cada resultado acredita **solo** el escenario indicado:

| Etiqueta | Curl y resultado | Qué acredita / límite |
|---|---|---|
| **Real en AWS** | `GET /health` → `200 {"status":"Healthy","checks":{"mongodb":"Healthy","redis":"Healthy"}}` | Sonda con Mongo y Redis alcanzables en AWS; no prueba fallo de dependencias, índices ni blacklist. |
| **Real en AWS** | `GET /api/v1/sellers/507f1f77bcf86cd799439011` → `200` | Lectura Mongo de vendedor vía API y petición admitida por el gate Redis de rate limit; no prueba cuota agotada ni dos réplicas. |
| **Real en AWS** | `GET /api/v1/sellers/507f1f77bcf86cd799439012` → `404 SELLER_NOT_FOUND` con `correlationId`/`status`/`code`/`message` | Respuesta para vendedor ausente con contrato de error en ese endpoint; no prueba uniformidad de todas las rutas ni correlación en logs. |
