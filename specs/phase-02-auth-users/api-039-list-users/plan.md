# plan.md — API-039 · Listar usuarios y vendedores

> Fase `phase-02-auth-users` · Versión documental **0.2.0** · 2026-11-04 · **Diseño del contrato ratificado para SDD local; sin autorización de implementación**. Deriva de `api-039-list-users/spec.md` 0.2.0.
> Prevalecen `docs/constitution.md` v2.3 y `specs/phase-02-auth-users/spec.md` v1.5. Aprobación integral del equipo, integraciones y despliegue pendientes. No modificar flujo/códigos/DTO de las rutas previas ni implementar API-003/040.

## 1. Fronteras y flujo del candidato local

Ruta prevista `src/UTTTGO.Api/Modules/Admin/{Controllers,Application,Contracts,Validators}/` para orquestación Admin; **el código actual de perfiles vive en `src/UTTTGO.Api/Modules/Identity/`** (`Application/ILoginDependencies.cs`: `LocalLoginProfile`, `ILoginProfileRepository`; este último solo publica `FindBySubjectAsync`). El plan general § 1 denomina `IdentityAndUsers` al dueño documental de `users`, pero no existe módulo `Users` implementado ni contrato de listado publicado. Para API-039, **el dueño actual Identity deberá publicar una interfaz de aplicación de lectura paginada de perfiles**, separada del repositorio de login; Admin consumirá solo esa interfaz. No mover colecciones ni resolver la divergencia de nombres mediante acceso cruzado; si se formaliza `Users` en el futuro, conciliar primero su frontera y dependencias acíclicas. **Admin no recibe `IMongoCollection<users>` ni repositorio de Identity/Users**, ni consulta Sellers, solicitudes u otros módulos. Registrar dirección de dependencia/mapeo al hacer la futura conciliación de fase; no cambiar otros documentos en esta ratificación.

Flujo propuesto:

1. Controller recibe `GET /api/v1/admin/users`; valida Bearer y parámetros: `search` opcional normalizado sobre `displayName` (≤ 100 caracteres), `role`/`accountStatus` de lista blanca, `page` positivo requerido, `pageSize` requerido 1..límite configurable inicialmente 50, `sortBy` opcional `createdAtDesc`/`createdAtAsc` (defecto descendente). Rechazar parámetros inválidos/duplicados y overflow antes de consultar. DTO/query explícito; error normalizado con `correlationId`.
2. Reutilizar `ISessionService.AuthenticateAsync` (`Modules/Identity/Application/SessionService.cs`) **solo como frontera de autorización**: sujeto del JWT validado, blacklist Redis, confirmación actual Supabase y perfil local Active. Verificar rol `Admin` **en el perfil local**, no del JWT ni del query; sin rol, `403`, sin lectura de lista; identidad/sesión no habilitada `401`, dependencia crítica caída `503`. No tocar orden de middleware de `Program.cs` sin aprobación de AGENT-BE-01 y actualización de spec. Reutilizar mecanismo no equivale a validar JWT/Redis reales: los adaptadores actuales están sin configurar y fallan cerrado.
3. Aplicar rate limit distribuido conforme a configuración; cupo excedido ⇒ `429`, Redis no disponible ⇒ `503` + alerta. Cuotas/partición operativas por configurar sin relajar el límite general ni usar estado del proceso.
4. Servicio Admin llama a **contrato publicado por Identity**, con filtros/sort/paginación ya validados. Devuelve exclusivamente `id`, `displayName`, `roles`, `accountStatus` y metadatos `page`, `pageSize`, `total` (coincidencias filtradas antes de paginar), sin documentos Mongo ni campos privados. No cachear este listado en el alcance local ratificado.
5. **Antes de emitir `200`**, completar el registro obligatorio de lectura administrativa (actor local, acción, UTC, resultado y `correlationId`) sin búsqueda en bruto, filtros sensibles, tokens ni filas. Si el registro falla, `503` fail-closed, sin listado. Diseño del sink y manejo de intentos fallidos quedan para implementación/integración sin relajar esta regla.

## 2. Contratos y persistencia futuros (ningún adaptador implementado)

| Frontera | Responsabilidad del candidato local | Verificación/implementación pendiente |
|---|---|---|
| `ISessionService` existente o wrapper de aplicación | Convertir sesión validada en decisión `401`/`403`/`503`; rol Admin local Active por solicitud | Adaptadores JWT/Redis/Supabase reales; no duplicar lector JWT ni cambiar middleware. |
| Contrato nuevo de lectura paginada **publicado por Identity** (nombre concreto por definir) | Recibir filtros tipados y devolver página con solo los cuatro campos ratificados y `total` | `ILoginProfileRepository` solo permite `FindBySubjectAsync`; no exponerlo a Admin. Verificar `Suspended`, `createdAt` y normalización de nombre antes de implementar. |
| `IAdminRateLimitGate` | Política distribuida para lectura administrativa; `429` al superar cupo, `503` al fallar Redis | Partición, cuotas/configuración y pruebas multinstancia; sin límites numéricos inventados. |
| `IAdministrativeReadAudit` | Registro obligatorio sin PII previo al `200`; error ⇒ `503` | Sink real y retención pendientes; tests de fail-closed. |

**Repositorio Mongo futuro exclusivamente detrás del contrato de Identity:** filtros tipados sobre `roles` y `accountStatus`, `search` solo en `displayName` normalizado (máximo 100 caracteres), sin regex arbitraria; sort `createdAt` desc/asc, desc por defecto, con desempate por ID único estable; `Skip`/`Limit` acotados y proyección en servidor. Validar overflow; `total` cuenta coincidencias con **los mismos filtros**, antes de paginar; las peticiones concurrentes pueden observar cambios y no se promete snapshot entre ellas. Verificar valores reales de `accountStatus`, `createdAt` y campo normalizado antes de diseñar consulta/índices; justificar índices con consultas y `explain`/tests, sin adoptar por inercia el índice `{email:1}` de la ficha (API-039 no busca email). No crear joins ni leer `sellerProfiles` o solicitudes desde Admin.

**Contrato HTTP ratificado localmente** en `spec.md` 0.2.0 § 2: `page` positivo y `pageSize` entre 1 y límite configurable inicial 50, ambos obligatorios; filtros enum, sort determinista por defecto descendente, `200` con `items` y metadatos, errores `400/401/403/429/503`. Verificar soporte real de `Suspended`, `createdAt` y normalización; configuración no secreta de paginación/rate limit se valida al iniciar y credenciales reales viven fuera de Git. No generar endpoints adicionales.

## 3. Estrategia de pruebas y gates

- **Unitarias locales:** validador de query (ausentes, duplicados, `search` > 100, `page` < 1, `pageSize` < 1 o > límite inicial 50, overflow, enum/sort), autorización (`Admin` local Active vs Buyer/Seller/rol JWT forjado), cuatro campos exactos sin PII, búsqueda solo por nombre, filtros combinados, `total` previo a paginación, sort por defecto/desempate y auditoría obligatoria `503` si falla; ningún doble se registra en producción.
- **Integración local `WebApplicationFactory`:** contrato JSON exacto de página vacía/no vacía y errores con `correlationId`; no-admin nunca invoca lector; JWT ausente/inválido/revocado y Redis/Supabase/perfil caídos no entregan listas; rate limit `429`/`503`, auditoría fallida `503`; página máxima y búsqueda segura. Dobles de Identity/Mongo/Redis **solo en tests**; default no configurado ⇒ `503` cuando no se puede autorizar, auditar o listar.
- **Seguridad local:** exclusión de PII y secretos, cambios de estado/rol entre solicitudes, email/rol/ID inyectados en query no conceden acceso, logs/auditoría sin búsqueda en bruto, tokens ni filas; no usar JWT simulado en producción.
- **Integración real posterior:** adaptadores JWT/Supabase/Redis y MongoDB local con índices justificados; rate limiting distribuido entre instancias, paginación bajo inserciones/ediciones concurrentes, filtros e índice medidos y política de auditoría probada. Ni test con dobles ni build acreditan esto. No contactar producción en CI.

**Gate SDD:** contrato del `spec.md` 0.2.0 ratificado para SDD local; **no es autorización de código**. Esperar instrucción explícita para implementación local y comprobar modelo/contrato de lectura sin mover colecciones. TASK-AUTH-007/008, aprobación integral e integración real siguen pendientes. AC-005 también requiere API-040. Reportar resultados solo tras ejecutar restore, formato, build Release y suites; este diseño no acredita despliegue ni costo cero.

## 4. Historial documental

| Versión | Fecha | Cambio / alcance |
|---|---|---|
| 0.1.0 | 2026-11-04 | Propuesta inicial de fronteras y lectura Admin. |
| 0.2.0 | 2026-11-04 | Alineado al contrato local ratificado en `spec.md` 0.2.0; Identity dueño actual, nuevo contrato paginado requerido, auditoría fail-closed; integración y aprobación integral pendientes. |
