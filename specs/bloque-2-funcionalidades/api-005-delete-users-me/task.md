# API-005 - Eliminar cuenta propia - task

Estado de aprobacion de la ficha: APROBADA por el propietario, 2026-11-22.

## Autoridad y alcance

Constitucion -> spec.md -> plan.md -> tareas. DEC-003 esta aprobado; Identity no borra datos de otro modulo.

## Tareas

| ID | Tipo | Alcance | Estado | Evidencia |
|---|---|---|---|---|
| DEV-API005-01 | DOCUMENTAL | Politica de baja, anonimización, `deletedUserRef` UUID, bloqueo por pedidos activos, pausa de productos, Redis y logout best-effort. | DONE | `spec.md`, aprobacion 2026-11-22 |
| DEV-API005-02 | IMPLEMENTACION | DELETE, servicio Identity, CAS en `users`, contratos Orders/Catalog, Redis y logout best-effort. | DONE | Unitarias y build Release; integracion pendiente |
| DEV-API005-03 | INTEGRACION | Consumir contratos de Orders/Catalog sin escribir sus colecciones. | BLOCKED | Docker/Testcontainers pendiente |
| DEV-API005-04 | QA_TECNICA | Repeticion, pedido activo, JWT invalido/revocado, dependencia caida y preservacion de ajenos. | BLOCKED | Suites completas pendientes |

## Restricciones

No usar Supabase `service_role` ni Admin API. No introducir acceso cross-module ni eliminar pedidos, reseñas o chat desde Identity. No ejecutar contra producción.

## Evidencia requerida

Cuando Docker este disponible, ejecutar build Release, formato, unitarias, integracion y seguridad. Registrar resultados y actualizar `docs/traceability.md` y `memoria.md` sin afirmar verificacion no realizada.
