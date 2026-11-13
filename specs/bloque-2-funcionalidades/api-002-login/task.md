# API-002 — Login · tareas

> Lote 2a · **Pendiente de aprobación**. Las marcas documentales no cambian las casillas de [TASK-AUTH originales](../../phase-02-auth-users/task.md) ni acreditan integración externa.

Estado de aprobación de la ficha: PENDIENTE

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Alcance: exclusivamente `POST /api/v1/auth/login`; sin código, tests ni curl nuevos.

## A. Actividades documentales del lote 2a

- [x] **Contrato documental:** cotejados controlador, DTO, validador, servicio, adaptadores y cuotas; `200/400/401/429/503` son código candidato, no aprobación, y el DTO **actual** no tiene refresh; resultado: [contrato](spec.md#alcance-y-contrato-vigente-código-no-ratificación).
- [x] **Trazabilidad documental:** cotejados RF/RNF/AC y T-AUTH de la [fase 02](../../phase-02-auth-users/spec.md), y TASK-AUTH de sus [tareas](../../phase-02-auth-users/task.md), sin cerrar ninguno; resultado: [tabla](spec.md#trazabilidad-y-escenarios-propuestos).
- [x] **Evidencia acotada:** leídos los tres archivos `Login*Tests` y QA: **43 añadidos / 96 totales históricos**, no resultado actual; `200` local/AWS solo atribuido a documentación; sin curl primario AWS ni ejecución nueva. Resultado: [evidencia](spec.md#evidencia-disponible-no-ejecutada-en-este-lote).
- [x] **Drift documental:** catalogados [DRIFT-API002-01..06](spec.md#discrepancias-y-pendientes). El `401` del pendiente confirmado en tests con doble sin promoción es **coherente**; falta cobertura HTTP del caso con promoción, no corrección automática de tests.
- [x] **Preparación documental:** cotejados semántica de IDs, enlaces, anclajes, tablas y archivos nuevos; se preservan pendientes y estado «Pendiente de aprobación». Este check no aprueba el contrato ni certifica build/tests actuales.

## B. Dependencias y verificaciones técnicas pendientes fuera del cierre documental

Las referencias siguientes conservan el significado y estado `[ ]` de las [TASK-AUTH originales](../../phase-02-auth-users/task.md). No autorizan implementar otras rutas desde API-002; su resolución no bloquea por sí sola el **commit documental**.

- [ ] **TASK-AUTH-002 — verificación OTP (dependencia):** evidencia de decisión técnica del equipo sobre código/SDK/SMTP, confirmación y reconciliación, más resultados OTP incorrecto/vencido; corresponde al flujo externo RF-15, no a una ruta nueva de login.
- [ ] **TASK-AUTH-003 — refresh/sesiones (dependencia):** decisión registrada para emisión/renovación/revocación y borrado móvil; prueba de ausencia de reingreso por renovación. No inferir política del DTO sin refresh.
- [ ] **TASK-AUTH-007 — versiones (gate de fase):** evidencia de aprobación de equipo/delegación de matriz y política de versiones y responsables de aprobación operativa, sin equipararla a aprobación individual previa.
- [ ] **TASK-AUTH-008 — SDD de fase (gate de fase):** aprobación documentada de spec → plan → task **históricos de fase 02** tras sus prerequisitos; distinta de revisar esta ficha atómica o autorizar su commit documental.
- [ ] **TASK-AUTH-010 — contrato de siete fichas (parte API-002):** acta de revisión del equipo sobre DTO, `401`/`403`, estado y antienumeración específicos de login; no cambiar respuesta por simple propuesta.
- [ ] **TASK-AUTH-011 — Supabase/perfiles (parte API-002):** pruebas identificadas con dependencias autorizadas de confirmación, lookup `sub`, promoción idempotente y fallo parcial; SMTP institucional acreditado aparte como dependencia OTP.
- [ ] **TASK-AUTH-012 — seguridad distribuida (transversal):** resultados identificados de firma/issuer/audience/exp, Redis/blacklist, fail-closed y réplicas; resolver `jti`/`session_id` con autoridad constitucional. La escritura de revocación/logout pertenece a **API-003 (TASK-AUTH-025)**, no a este endpoint.
- [ ] **TASK-AUTH-021 — login:** resultados de casos login activo/inválido, no verificado, perfil ausente/suspendido, rate limit, fallo de dependencias, y promoción/CAS bajo concurrencia Mongo; añadir en trabajo técnico autorizado un caso HTTP con doble que **sí** promueva. No hay expectativa `401` contradictoria con iguales precondiciones.
- [ ] **TASK-AUTH-030 — suite por ruta (transversal):** reporte reproducible y vigente de unitarias/integración local/seguridad con errores, `correlationId` y logs; evidencia OTP/SMTP por sus responsables. Los 96 verdes de 2026-11-04 no sirven como ejecución actual.
- [ ] **Aprobación funcional individual de API-002:** decisión humana expresa para esta ficha y sus DRIFT; **no** es requisito para el commit documental ni se suple con código, tests escritos o el estado de TASK-AUTH-008.

## Cola DEV: convención

Las casillas anteriores son antecedentes, no permiso de programación. `DEV-API002-*` no reemplaza TASK-AUTH ni cierra DRIFT-API002-01..06. **Lista** = verificar sin fijar contrato; **bloqueada** = no cambiar producción/tests ni probar servicios externos antes de gates. Ninguna tarea DEV se ejecutó aquí; registrar comando/artefacto, fecha, entorno, resultado y límite (no basta «código implementado»).

## Tareas ejecutables sin cambiar contrato

| ID · clase/objetivo | Requisito y escenarios; archivos/componentes | Dependencia; acción y criterio de finalización/evidencia | Estado |
|---|---|---|---|
| DEV-API002-01 · pruebas: línea base actual | LOG-01..09; `tests/UtttGo.*Tests/`, [QA](../../../docs/qa-api-002.md) solo lectura | Sin gate; restore/build Release/format/tres suites de guía. **Hecho**: reporte comando/exit code/recuento o impedimento reproducible; 43 añadidas/96 históricas no acreditan actualidad. | Lista, no ejecutada. |
| DEV-API002-02 · evidencia local/AWS | LOG-01, DRIFT-API002-06; `docs/migracion-sdd.md`, `docs/traceability.md`, `docs/qa-api-002.md` (lectura) | Sin gate; buscar artefacto primario de `200` local 2026-11-06 y AWS 2026-11-08, separar seis intentos locales `503/429`. **Hecho**: matriz fuente/fecha/entorno/alcance o «pendiente de fuente»; no curl nuevo. | Lista. |
| DEV-API002-03 · pruebas: auditar precondición de promoción | LOG-02/03/09, DRIFT-API002-02; `LoginServiceTests`, `LoginEndpointTests`, `LoginSecurityTests`, `ILoginDependencies` (lectura) | Tras 01; identificar método unitario que configura `Active`/`Suspended` y doble HTTP que hereda retorno `null`. **Hecho**: tabla configuración → resultado esperado `200`/`401` y cobertura HTTP/Mongo pendiente, sin «corregir» tests. | Lista. |
| DEV-API002-04 · cierre documental local | RF-02/15, AC-002/003, DRIFT-API002-01..06; trío local (único editable), históricos (lectura) | Tras 02/03; cotejar enlaces/IDs/estados y reporte de brechas. **Hecho**: revisión reproducible que deja abiertos seis DRIFT y TASK-AUTH, no aprobación ficticia. | Lista. |

## Tareas bloqueadas hasta decisión y gates SDD

**Gate común para ejecutar cambios técnicos, no para solicitar decisiones:** [TASK-AUTH-007 → 008 → 010](../../phase-02-auth-users/task.md), ficha aprobada y TASK-AUTH-002/003/011/012/021 según alcance ([decisiones](../../../docs/decisiones-pendientes-fase-02.md) § «Orden y bloqueos expresos»). Obtener un acta en DEV-API002-05/10 no presupone haber cerrado el TASK-AUTH de la misma decisión. Los tests históricos y desarrollo local previo no dispensan aprobación.

| ID · clase/objetivo | Requisito y escenarios; componentes | Dependencia; acción posterior y criterio de finalización/evidencia | Estado |
|---|---|---|---|
| DEV-API002-05 · decisión HTTP/DTO | LOG-01..07, DRIFT-API002-03; DF02-03/07, TASK-AUTH-010; spec/`AuthController`/DTO | Solicitar ratificación al equipo, sin presuponer cierre de TASK-AUTH-010; decidir `401` actual vs `403` propuesto, JSON, antienumeración, `200/400/429/503`, rol local y refresh como decisión aparte. **Hecho**: acta por escenario, sin modificar código. | Bloqueada: equipo. |
| DEV-API002-06 · corrección de diferencias ratificadas | LOG-01/03..07; controlador, `LoginRequest`, validador, `LoginResponse`, `LoginService`, suites Login* | Tras 05; implementar solo diferencias aprobadas, no adoptar `403` por propuesta ni refresh por intuición. **Hecho**: respuestas/errores con `correlationId` coinciden con acta, build/format/suites verdes; si no difieren, informe sin cambio. | Bloqueada: 05. |
| DEV-API002-07 · pruebas faltantes: promoción/CAS | LOG-02/09, DRIFT-API002-02; `LoginEndpointTests`, `MongoUserRepositories`, integración Mongo local | Gate común, DF02-09/TASK-AUTH-011/021 aprobados; agregar prueba HTTP con doble `ActivatePendingAsync → Active` y pruebas Mongo de activación idempotente y suspensión concurrente. **Hecho**: `200` solo con Active coherente y `401` ante null/suspensión, build/format/tres suites verdes; no alterar expectativas del doble `null` actual. | Bloqueada: diseño/gate. |
| DEV-API002-10 · decisión renovación/revocación | LOG-08/10, DRIFT-API002-04/05; DF02-04, TASK-AUTH-003/025; `LoginResponse`, ficha API-003 | Solicitar decisión al equipo, sin presuponer cierre de TASK-AUTH-003/025; resolver refresh, coordinación de logout móvil/Supabase y tokens sin `jti` conforme constitución. **Hecho**: acta y casos de reingreso/revocación; escritor `revoked:{jti}` pertenece a API-003, no editarlo aquí. | Bloqueada: equipo. |
| DEV-API002-08 · validación JWT/Redis/proxy | LOG-01/06..08, DRIFT-API002-04; `RedisJwtGate`, `RedisAuthRateLimitGate`, proxy y Redis local | Tras gate común y decisión de 10; probar en entorno no productivo JWT firma/issuer/audience/exp, blacklist fail-closed y cuota/IP en dos réplicas. **Hecho**: resultados reproducibles con rechazo `401` vs fallo `503`, sin secretos; fallback `session_id` no ratificado. | Bloqueada: decisión/entorno. |
| DEV-API002-11 · validación OTP dependiente | LOG-04, DRIFT-API002-01; DF02-01, TASK-AUTH-002; ficha OTP/guard privado/SMTP | Tras gate común y decisión OTP; **solo en ficha externa autorizada** probar confirmación correcta/errónea/vencida y bloqueo privado aun con JWT; API-002 registra dependencia, no desarrolla OTP. **Hecho**: referencia a evidencia externa reproducible sin secretos. | Bloqueada: técnica/entorno. |
| DEV-API002-09 · cierre de trazabilidad e integraciones | LOG-01..10, DRIFT-API002-01..06; trío local, artefactos QA/integración | Tras 05..08, 10/11 y TASK-AUTH-030; cotejar resultados actuales con AC y decisiones, identificar fuentes primarias local/AWS. **Hecho**: matriz escenario → prueba/resultado/entorno y actas; solo cerrar DRIFT con evidencia propia y aprobación expresa, no por despliegue previo. | Bloqueada: gates/evidencia. |

## Guía de ejecución con OpenCode

### Control de ejecución

Este registro normaliza estados/dependencias de las tareas detalladas arriba, sin crear tareas adicionales. Una autorización «Pendiente» exige acta y gate cerrado; ningún DONE se infiere de evidencia histórica.

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API002-01 | QA | READY | NO | N/A: lectura y comandos locales | - | PENDIENTE |
| DEV-API002-02 | EVIDENCIA | READY | NO | N/A: lectura | - | PENDIENTE |
| DEV-API002-03 | QA | READY | NO | N/A: lectura | DEV-API002-01 | PENDIENTE |
| DEV-API002-04 | DOCUMENTAL | READY | NO | N/A: revisión del trío | DEV-API002-02, DEV-API002-03 | PENDIENTE |
| DEV-API002-05 | DECISION | BLOCKED | NO | Pendiente: DF02-03/07, TASK-AUTH-010 | - | PENDIENTE |
| DEV-API002-06 | CORRECCION | BLOCKED | SI | Pendiente: acta 05 y TASK-AUTH-007/008/010/021 | DEV-API002-05 | PENDIENTE |
| DEV-API002-07 | QA_TECNICA | BLOCKED | SI | Pendiente: DF02-09, TASK-AUTH-007/008/010/011/021 | - | PENDIENTE |
| DEV-API002-10 | DECISION | BLOCKED | NO | Pendiente: DF02-04, TASK-AUTH-003/025 | - | PENDIENTE |
| DEV-API002-08 | INTEGRACION | BLOCKED | SI | Pendiente: acta 10 y TASK-AUTH-007/008/010/012 | DEV-API002-10 | PENDIENTE |
| DEV-API002-11 | DEPENDENCIA_EXTERNA | BLOCKED | NO | Pendiente: DF02-01/10, ficha externa, TASK-AUTH-002 | - | PENDIENTE |
| DEV-API002-09 | DOCUMENTAL | BLOCKED | NO | Pendiente: actas, pruebas pertinentes y TASK-AUTH-030 | DEV-API002-05, DEV-API002-06, DEV-API002-07, DEV-API002-08, DEV-API002-10, DEV-API002-11 | PENDIENTE |

Objetivo, escenario, archivos, acción, resultado y prueba están en la fila detallada única de cada ID; las dos tablas deben coincidir.

1. Verificar rama/HEAD/estado Git. Leer [constitución v2.6](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → estas tareas; cotejar [fase 02](../../phase-02-auth-users/spec.md), [TASK-AUTH](../../phase-02-auth-users/task.md), [decisiones](../../../docs/decisiones-pendientes-fase-02.md) y [trazabilidad](../../../docs/traceability.md). Constitución prevalece ante contradicción; ni código ni guía móvil autorizan contrato nuevo.
2. Seleccionar **solo 01..04** sin acta nueva, una tarea a la vez. Para 05..11 comprobar gate y decisión concreta; consultar código/suites de [plan](plan.md#secuencia-de-implementación-condicionada). Para perfil pendiente, especificar siempre retorno de `ActivatePendingAsync`; `null` y `Active` son precondiciones distintas.
3. En raíz si el entorno permite: `dotnet restore UTTTGO.Backend.slnx`; `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`; `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`; `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build`, y análogos para `UtttGo.IntegrationTests` y `UtttGo.SecurityTests`. Registrar fallos del entorno; jamás probar contra producción.
4. Reportar ID/escenario, doble o entorno real, comando/exit code, fecha, evidencia, ausencia de fuente y límites sin tokens/PII/secretos. En esta intervención no editar código/tests/infra/históricos, no añadir rutas OTP/logout ni cambiar middleware; futuras tareas autorizadas deben ceñirse a su alcance. No marcar TASK-AUTH, DRIFT ni aprobación sin acta/prueba específica.
