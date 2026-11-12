# Contratos transversales — spec

> Lote 1 · 2026-11-12 · **Pendiente de aprobación**; no crea endpoint nuevo.

Autoridad: [constitución v2.6](../../../docs/constitution.md) §§ 5–6, 11, 14 → esta ficha → [plan](plan.md) → [task](task.md). [Trazas](../base-modular/plan.md#correspondencia-exhaustiva-de-fase-00). [API](../../../skills/be-api-design.md) · [arquitectura](../../../skills/be-architecture.md) · [memoria](../../../skills/be-memory.md).

## Alcance

REQ-002: prefijo `/api/v1` de negocio, DTOs de cada módulo y shared kernel mínimo: error uniforme, `correlationId`, paginación `page`/`pageSize` con máximo configurable. `GET /health` es sonda operativa con ficha propia, no una excepción que habilite rutas funcionales nuevas. CORS de API usa orígenes explícitos; CORS de S3 pertenece a DEC-020/bloque 3. No se modifica el orden de middleware sin aprobación de AGENT-BE-01.

## Aceptación verificable

| Dado / cuando | Entonces |
|---|---|
| Validación o dependencia falla | HTTP semántico según § 14, cuerpo de error único con `correlationId` y sin detalles sensibles. |
| Listado es consultado | Pagina y valida máximo configurable; ningún listado ilimitado; pruebas por ficha. |
| Petición con correlación y log en ciclo de vida | Correlación consistente entre respuesta de error y logs, sin tokens/contraseñas; revisión de logs real aún pendiente. |
| Cambia middleware o se pide ruta no inventariada | Requiere aprobación/SDD por dueño; este documento no autoriza el cambio. |

## Evidencia

- [**Real en AWS: tres curl, fecha y límites**](../base-modular/spec.md#evidencia-en-aws-2026-11-12): sonda `200`, lectura pública `200` y error `404 SELLER_NOT_FOUND` con `correlationId`/`status`/`code`/`message` **en esa ruta**; no prueban paginación ni correlación en logs.
- `docs/qa-api-001.md` (2026-11-04): `RegisterEndpointTests` prueban errores con `correlationId` **con dobles**; `docs/qa-api-039.md`: `AdminUsersEndpointTests` prueban paginación **con dobles**, no auditoría persistida ni límite universal.
- `specs/cross-cutting/api-health-probe/spec.md` HP-02/03 documenta contrato `503` de sonda; no demuestra por sí mismo todas las rutas. `memoria.md` entrada 2026-11-07 registra traslado de `ApiError` a Shared y prueba local de `/health`, no prueba correlación end-to-end de toda la API.
- **Pendiente de contratos:** cotejar orden de `Program.cs`, correlación en logs, paginación y uniformidad del error en todas las rutas; los curl no se reejecutaron aquí.
