# spec.md — API-039 · Listar usuarios y vendedores

> Fase `phase-02-auth-users` · Módulo `Admin` · Versión documental **0.2.0** · 2026-11-04 · **Contrato candidato ratificado para SDD local por la responsable técnica; implementación todavía no autorizada**.
> Subordinada a `docs/constitution.md` v2.3 y a `specs/phase-02-auth-users/spec.md` v1.5. Esta ratificación es distinta de la aprobación integral del equipo, de la aprobación local previa para otras rutas y de la integración/despliegue. No modifica API-003, API-040 ni los contratos de las rutas ya candidatas.

## 1. Objetivo, alcance y fuentes

Permitir a un administrador consultar un listado **acotado, buscable y paginado** de cuentas para gestión de la comunidad (RF-07). Solo comprende `GET /api/v1/admin/users`: no suspende, reactiva, asigna roles, exporta datos ni ejecuta acciones de API-040. API-003 sigue pendiente. No se crean rutas nuevas.

| Procedencia comprobada | Aporta | Estado |
|---|---|---|
| `material/UTTT_GO_Punto_4_Especificacion_API_v1.xlsx`, hoja «Inventario endpoints» fila 40 y hoja «API-039 Listar usuarios y vendedores» filas 1–23 | Método/ruta, Bearer JWT + Admin, seis parámetros, `200` paginado, errores `400/401/403/429/503`, auditoría y datos mínimos | La ficha original sigue **«Propuesta a validar»** y el Excel permanece intacto; las precisiones de § 2 son decisiones **locales**, no aprobación integral de la ficha. |
| `material/IC-Agile-User-Story-UTTT-GO-Figma-v2.xlsx`, «Historia de usuario Agile» fila 51, «Trazabilidad Figma» fila 46 | **HU-049**, **UI-043 Usuarios y vendedores**; administración de usuarios, vendedores y solicitudes pendientes | HU/UI verificadas; mencionar solicitudes/acciones **no** autoriza incluirlas en este GET. No asignar por inferencia un PB/HU de otro documento. |
| `specs/phase-02-auth-users/spec.md` v1.5 §§ 2–4/5/9 | HU-AUTH-08, **RF-07**, RNF-01/03/04/07/08/09/12/17, **AC-005** global, ruta en § 7 | RF-07 exige listar, buscar, paginar y exponer rol/estado; **AC-005 también exige API-040**, no se cierra solo con API-039. |
| `docs/constitution.md` §§ 4–6, 12, 14, 17 | Fronteras modulares, JWT/Redis fail-closed, rol local Admin, paginación, errores, no secretos | Reglas obligatorias; código prohibido sin `spec.md` aprobado para esta funcionalidad. |

## 2. Contrato HTTP candidato ratificado localmente

`GET /api/v1/admin/users?search=...&role=...&accountStatus=...&page=...&pageSize=...&sortBy=...` sobre HTTPS; sin body. Solo admite sesión válida: Bearer JWT de Supabase validado (firma, issuer, audience, exp, `sub`, blacklist Redis), confirmación actual confiable del mismo `sub` y perfil local **Active** con rol `Admin` asignado por servidor. Nunca confiar en roles del JWT, query o body. Token ausente/inválido/revocado, perfil ausente/no Active o identidad no habilitada ⇒ `401` genérico; perfil Active sin rol local Admin ⇒ `403`; dependencia crítica no disponible ⇒ `503`. No revelar estado de cuenta en errores.

| Parámetro | Decisión **aprobada para el contrato local** |
|---|---|
| `search` | Opcional; buscar **solo en `displayName` normalizado**, máximo 100 caracteres; nunca buscar por email. Validar longitud y rechazar búsquedas no acotadas; concretar normalización/coincidencia sin alterar este alcance. |
| `role` | Opcional; solo `Buyer`, `Seller`, `Admin`, por pertenencia a `roles` del perfil local. Seller no implica datos de negocio ni acceso a Sellers. |
| `accountStatus` | Opcional; solo `Active`, `PendingEmailVerification`, `Suspended`. **Pendiente verificar** que el modelo persistido soporte exactamente los tres valores antes de implementar el filtro; no inventar un estado ni incluir datos eliminados. |
| `page` | Obligatorio, entero positivo desde 1. |
| `pageSize` | Obligatorio, entero entre 1 y el límite configurable, **inicialmente 50** (máximo inicial 50); no permitir 0, negativo ni más del límite vigente. |
| `sortBy` | Opcional; solo `createdAtDesc` o `createdAtAsc`; por defecto `createdAtDesc`. Desempatar por ID único con orden determinista; verificar disponibilidad de `createdAt` en el modelo de lectura. |

Filtros se combinan en el módulo dueño de `users`; consulta y conteo usan los mismos filtros. Validar overflow de paginación; no devolver colecciones ilimitadas. **DTO JSON aprobado para el candidato local** (`items` contiene exactamente los cuatro campos indicados):

```json
{
  "items": [
    { "id": "<id-local>", "displayName": "<nombre>", "roles": ["Buyer"], "accountStatus": "Active" }
  ],
  "page": 1,
  "pageSize": 1,
  "total": 1
}
```

`id` serializado como string; `items` vacío y `total: 0` si no hay coincidencias. `total` es el número de coincidencias con **todos los filtros aplicados**, antes de paginar; no promete instantánea consistente entre peticiones concurrentes. El `items` de cada usuario incluye **únicamente** `id`, `displayName`, `roles`, `accountStatus`; no incluir `email`, `phone`, avatar/SAS, `supabaseUserId`, `_id` Mongo como campo separado, credenciales, JWT, refresh, tokens de dispositivo ni datos privados de vendedores. No consultar directamente colecciones de Sellers, solicitudes ni otros módulos; HU-049/UI-043 no amplían esta ruta a dichas solicitudes. RF-07 exige rol y estado; los restantes campos y metadatos son decisión local ratificada.

| HTTP | Contrato local ratificado; toda respuesta de error usa `{correlationId,status,code,message,details}` de constitución |
|---|---|
| `200` | Admin autorizado; lista paginada (vacía si no hay coincidencias). Solo después de completar auditoría obligatoria. |
| `400` | Query inválida, enum no permitido, parámetro duplicado ambiguo, paginación fuera de rango o `sortBy` no admitido; validar antes de consultar datos. |
| `401` | Bearer ausente/inválido/expirado/revocado, `sub`/confirmación/estado local no habilitados; sin revelar cuál falló. |
| `403` | Sesión válida y habilitada sin rol local `Admin`; no conceder acceso por claim JWT. |
| `429` | Política de rate limit distribuido deniega. |
| `503` | Redis/blacklist/rate limit, Supabase, almacén crítico o **auditoría obligatoria** indisponibles; nunca devolver listado si falla la auditoría o una dependencia necesaria. |

Los códigos HTTP anteriores están ratificados **para el candidato local**, no implican aprobación integral de la ficha original. No cachear el listado en este alcance. Auditar obligatoriamente cada lectura administrativa (actor local, acción, instante UTC, `correlationId`, resultado) sin token, email buscado, búsqueda en bruto, parámetros sensibles ni filas devueltas. Si no puede completarse el registro obligatorio, **fallar cerrado con `503`**, sin emitir `200`; precisar mecanismo y orden de registro en implementación sin comprometer datos sensibles.

## 3. Escenarios Given/When/Then del contrato local

1. **Dado** JWT válido no revocado, correo confirmado y perfil Active con rol local `Admin`, **cuando** pide página dentro del límite, **entonces** recibe `200` con solo campos permitidos, orden estable y metadatos de paginación.
2. **Dado** un Admin válido y filtros combinados `search` sobre `displayName` normalizado, `role` y `accountStatus`, **cuando** consulta, **entonces** solo aparecen coincidencias, `total` cuenta todas las coincidencias filtradas antes de paginar; sin coincidencias se devuelve lista vacía y `total: 0`.
3. **Dado** token ausente/inválido/expirado/revocado o confirmación/estado local inválidos, **cuando** consulta, **entonces** no obtiene filas (`401`); no se consulta listado antes del gate.
4. **Dado** token válido de Buyer/Seller sin rol Admin local, aunque el JWT diga `Admin`, **cuando** consulta, **entonces** recibe `403` sin filas; cambios de rol/estado locales afectan la siguiente consulta.
5. **Dado** `page` ausente/menor que 1, `pageSize` ausente/fuera de 1..50 con límite inicial 50, `search` superior a 100 caracteres, filtro/sort desconocido o parámetro duplicado, **cuando** consulta, **entonces** recibe `400` con `correlationId`, sin consulta administrativa.
6. **Dado** rate limit excedido, **cuando** consulta, **entonces** recibe `429`; **dado** Redis/confirmación/almacén caídos, **entonces** `503` fail-closed; ningún doble habilita el camino de producción.
7. **Dado** `sortBy` ausente o permitido, **cuando** se ordena, **entonces** se usa `createdAtDesc` por defecto o `createdAtAsc` solicitado y desempate estable por ID; no se promete instantánea entre páginas solicitadas en momentos diferentes.
8. **Dado** una lectura Admin, **cuando** se registra auditoría, **entonces** se correlaciona actor/acción/UTC sin PII ni tokens; si falla el registro obligatorio, **entonces** `503` sin listado.

## 4. Criterios de aceptación y pendientes

- [x] **Decisión documental local:** la responsable técnica ratificó el contrato candidato de § 2; esta marca no acredita implementación, QA, integración ni aprobación integral del equipo. Una autorización expresa posterior es necesaria para escribir código. TASK-AUTH-007/008 permanecen abiertos.
- [ ] Antes de implementar el filtro/orden, comprobar contra el modelo real `Suspended` y los otros valores de estado, `createdAt`, ID y datos normalizados de `displayName`; definir coincidencia/normalización segura e índices sin alterar las decisiones ratificadas ni mover colecciones.
- [ ] `200` paginado cumple RF-07 y no revela PII no autorizada; no-admin y tokens/confirmación/estados inválidos nunca obtienen registros; Redis/Mongo/Supabase caídos fallan cerrado.
- [ ] Pruebas unitarias, integración `WebApplicationFactory` y seguridad verifican todos los escenarios con dobles **solo en tests**; separar sus resultados de pruebas reales Mongo/Redis/Supabase, todavía pendientes.
- [ ] Auditoría obligatoria sin información sensible, `503` cuando falle, rate limit distribuido y pruebas del **nuevo** contrato de lectura publicado por el módulo dueño de `users` (actualmente `Identity`); no existe todavía un contrato de listado.
- [ ] **AC-005 global sigue `[ ]`** hasta API-040 y su evidencia; API-039 no suspende/reactiva cuentas. Sin pruebas externas, no cerrar tareas de integración ni declarar aptitud para despliegue.

## 5. Historial documental

| Versión | Fecha | Cambio / alcance |
|---|---|---|
| 0.1.0 | 2026-11-04 | Propuesta SDD modular; campos, filtros y auditoría pendientes de ratificación. |
| 0.2.0 | 2026-11-04 | Decisiones del contrato ratificadas por responsable técnica para SDD local; verificación del modelo/contrato de listado, autorización de código, integración real y aprobación integral pendientes. |
