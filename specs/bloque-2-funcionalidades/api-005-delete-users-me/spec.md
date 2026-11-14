# API-005 — Eliminar cuenta propia · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-005 `DELETE /api/v1/users/me` según [trazabilidad](../../../docs/traceability.md) y [fase 02](../../phase-02-auth-users/spec.md); [constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente en este worktree.

## Estado de aprobación

BR-011/DEC-003 y RF-11 ratifican eliminación de información propia, incluidas publicaciones e historial del vendedor. Diseño de datos compartidos, operaciones activas, orden de borrado y HTTP siguen pendientes; no equivaler baja lógica/anonimización a eliminación.

## Responsabilidad única

Iniciar y completar la eliminación de la cuenta autenticada y datos propios, coordinando módulos dueños. No elimina cuentas ajenas, no cambia estado Admin (API-040), no es baja puntual de producto ni logout API-003.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md), [memoria](../../../memoria.md) | BR-011, DEC-003 | Propiedad y borrado obligatorio; compartidos por diseñar. |
| [Fase 02](../../phase-02-auth-users/spec.md), [plan histórico](../../phase-02-auth-users/plan.md) | RF-11, AC-007, T-AUTH-10 | DELETE propio, integración Supabase/Media/otros módulos pendiente. |
| [Trazabilidad](../../../docs/traceability.md) | API-005 | Ruta y estado; sin tarea API-005 cerrada. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Eliminar información propia, incluidos productos/publicaciones/historial vendedor | BR-011, DEC-003 | No resuelve pedidos de otro participante ni ejecución. |
| Identidad derivada de JWT; no confiar en `userId`/roles del cliente | BR-009, constitución § 6 | HTTP y política de reintentos pendientes. |

## Comportamiento implementado candidato

[`UsersController.cs`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs) solo tiene GET y PATCH `/me`; no hay acción DELETE ni orquestación. `MongoUserRepositories.cs` es repositorio Identity, no autoriza borrar colecciones ajenas.

## Funcionalidad requerida faltante

Diseño de eliminación coordinada con Identity/Supabase, Sellers, Catalog, Media, Orders, Chat y datos compartidos. Interfaces de borrado por dueño, idempotencia y tratamiento de fallos parciales todavía no están definidos ni implementados.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato | Falta decidir |
|---|---|---|---|
| Entrada | Bearer válido; sujeto extraído del token | DELETE sin cuerpo ni `userId` en URL | Autenticación durante reintento, confirmación y operaciones activas. |
| Resultado | Eliminar información propia efectivamente | Sin DTO ni HTTP ratificados | `204`/aceptación asíncrona, verificación, plazo y respuesta repetida. |
| Fallos | Error único `correlationId`, no exponer PII | Ningún mapeo implementado | Conflicto por pedidos activos, dependencia caída, fallo parcial y recuperación. |

## Seguridad y validaciones

Usar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Autorización por sujeto y propiedad en cada módulo; no borrar pedidos o chat de terceros sin política; invalidar sesiones conforme a API-003 o contrato compartido, fallar cerrado si no es verificable. Sin PII o tokens en logs; no tocar middleware.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| DEL-01 | Comprador autenticado sin recursos compartidos | Elimina su cuenta | Desaparecen identidad y datos propios, no los ajenos | RF-11, AC-007 | Integración por crear | Diseño pendiente |
| DEL-02 | Vendedor con publicaciones/media/historial | Elimina su cuenta | Recursos propios se eliminan y no quedan accesibles | BR-011, T-AUTH-10 | Integración intermodular por crear | DEC-003 técnica pendiente |
| DEL-03 | Pedidos compartidos/en curso o fallo parcial | Solicita borrado/reintenta | Resultado consistente según política aprobada sin pérdida indebida | DEC-003 | Pruebas fallos/reintento pendientes | BLOCKED |
| DEL-04 | JWT inválido o sujeto ajeno | Solicita DELETE | No se borra ningún dato ajeno | BR-009 | Seguridad por crear | Regla vigente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | Tarea DEV |
|---|---|---|---|
| RF-11, AC-007, T-AUTH-10 | [Fase 02](../../phase-02-auth-users/spec.md) | DEL-01/02/03 | DEV-API005-01/02/03/04 |
| BR-011, DEC-003 | [Constitución](../../../docs/constitution.md) | DEL-02/03 | DEV-API005-01/03 |
| BR-009 | [Constitución](../../../docs/constitution.md) | DEL-04 | DEV-API005-04 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| DEL-01 | NO_IMPLEMENTADO | Sin endpoint ni borrado Identity/Supabase | DEV-API005-02 |
| DEL-02 | NO_IMPLEMENTADO | Faltan contratos de borrado de módulos dueños | DEV-API005-03 |
| DEL-03 | BLOQUEADO | Privacidad de compartidos, fallos parciales, plazo/HTTP | DEV-API005-01; DEC-003 |
| DEL-04 | NO_IMPLEMENTADO | Falta prueba autorización/borrado | DEV-API005-04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | `UsersController.cs` sin DELETE; fase 02 AC-007 abierta | Borrado real, privacidad o consistencia. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| DEC-003 | [Constitución § 16](../../../docs/constitution.md) | Tratamiento de datos compartidos, pendientes, reintentos y orden por diseñar | DEV-API005-01/02/03/04 | Producto, privacidad, Identity y dueños de datos: acta técnica. |
| TASK-AUTH-007→008→010 | [Fase 02](../../phase-02-auth-users/task.md) | No inferir autorización desde BR-011 | DEV-API005-02/03/04 | Equipo/delegación, contrato por ficha y permisos técnicos. |

## Criterios de preparación para desarrollo

Alcance: eliminación integral, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta diseño DEC-003 y gates. Aceptación: pruebas comprador/vendedor, recursos compartidos y fallo parcial con persistencia aislada, sin residuos propios ni eliminación de otros participantes.
