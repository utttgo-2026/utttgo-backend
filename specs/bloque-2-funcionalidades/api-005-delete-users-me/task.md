# API-005 - Eliminar cuenta propia - task

Estado de aprobacion de la ficha: APROBADA por el propietario, 2026-11-22.

## Autoridad y alcance

Constitucion -> spec.md -> plan.md -> tareas. DEC-003 esta aprobado; Identity no borra datos de otro modulo.

## Tareas

| ID | Tipo | Alcance | Estado | Evidencia |
|---|---|---|---|---|
| DEV-API005-01 | DOCUMENTAL | Politica de baja, anonimización, `deletedUserRef` UUID, bloqueo por pedidos activos, pausa de productos, Redis y logout best-effort. | DONE | `spec.md`, aprobacion 2026-11-22 |
| DEV-API005-02 | IMPLEMENTACION | DELETE, servicio Identity, CAS en `users`, contratos Orders/Catalog, Redis y logout best-effort. | DONE | Código existente y pruebas unitarias |
| DEV-API005-03 | INTEGRACION | Consumir contratos de Orders/Catalog/Media sin escribir colecciones ajenas; outbox idempotente para assets. | DONE | `IAccountDeletionMedia`, `mediaDeletionOutbox`, dispatcher y DI |
| DEV-API005-04 | QA_TECNICA | Repeticion, pedido activo, JWT invalido/revocado, dependencia caida, pausa de publicaciones y orden de purga. | IN_PROGRESS | Build/format y suites con Docker: Unit 409, Integration 308, Security 172; falta prueba dedicada del dispatcher con doble S3 |

## Restricciones

No usar Supabase `service_role` ni Admin API. No persistir access tokens. No introducir acceso cross-module ni eliminar pedidos, reseñas o chat desde Identity. No ejecutar contra producción.

## Evidencia requerida

Build Release, formato y las tres suites se ejecutaron con Docker. Registrar resultados y actualizar `docs/traceability.md` y `memoria.md` sin afirmar verificacion no realizada. La purga S3 requiere una prueba dedicada con doble o emulador local, nunca con el bucket real.
