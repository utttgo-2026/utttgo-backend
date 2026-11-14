# API-001 — Registro · tareas

> Lote 2a · **Pendiente de aprobación**. Estas casillas describen trabajo por acreditar; no cambian el estado de [TASK-AUTH de fase 02](../../phase-02-auth-users/task.md).

Estado de aprobación de la ficha: PENDIENTE

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo documentación API-001; sin código ni curl nuevos.

- [x] **Ficha documental:** cotejar ruta, DTO, validador, servicio, filtro de 8 KiB y gate Redis con el código; separar `202` individualmente aprobado de los demás HTTP/DTO candidatos. Resultado: este [spec](spec.md#alcance-y-contrato-vigente-código-no-ratificación). No equivale a TASK-AUTH-020 terminado.
- [x] **Evidencia acotada:** registrar con etiqueta propia tests con dobles de `docs/qa-api-001.md`, dos resultados locales del 2026-11-06 y presencia de ruta en OpenAPI AWS del 2026-11-12, sin curl nuevo ni datos sensibles. No afirmar verificación AWS.
- [ ] **TASK-AUTH-002 / T-AUTH-14:** [main reporta configuración SMTP Gmail y plantilla OTP](https://github.com/utttgo-2026/utttgo-backend/blob/b4d81b8/docs/auth-mobile-integration.md), no pruebas reproducibles de entrega/costo ni cierre de los criterios históricos del [task de fase 02](../../phase-02-auth-users/task.md). Verificar flujo OTP/correo institucional, estado pendiente, reconciliación segura y SMTP; RF-15/AC-011 abiertos. No habilitar acceso privado por `202` ni introducir ruta sin ficha.
- [ ] **TASK-AUTH-007 → 008 → 010:** obtener aprobación identificable del equipo/delegación para política de versiones y SDD; ratificar DTO/HTTP de API-001 y límite de tamaño. TASK-AUTH-001/006 siguen `[x]` **solo documentalmente**.
- [ ] **TASK-AUTH-011/012/020/030; T-AUTH-01/03/05/15/18/19:** probar Supabase/Atlas/Redis de prueba, duplicados reales, perfil `Buyer` pendiente, fallos parciales, rate limit distribuido y error fail-closed sin secretos. Los dobles y dos respuestas reales locales no cierran este conjunto.
- [ ] **RF-06 (fuera de API-001):** mantener recuperación por enlace en Supabase Auth como funcionalidad pendiente de diseño técnico; nueva ruta backend solo con ficha aprobada.
- [ ] **Cierre del lote:** revisión humana de esta ficha y posterior conciliación de trazabilidad con el clerk al cerrar el lote; fase 02 se conserva en su ubicación actual.

## Cola DEV: convención

Las casillas precedentes son antecedentes del lote, no permiso técnico. Los IDs `DEV-API001-*` no sustituyen TASK-AUTH. **Lista**: comprobación sin fijar contrato; **bloqueada**: no tocar producción/tests ni ejecutar integración externa hasta gates; ninguna tarea de esta cola se ejecutó en esta intervención. Resultado verificable = comando/artefacto, fecha, entorno, resultado y limitación, nunca solo «implementado».

## Tareas ejecutables sin cambiar contrato

| ID · clase/objetivo | Requisito y escenarios; archivos/componentes | Dependencia; acción y criterio de finalización/evidencia | Estado |
|---|---|---|---|
| DEV-API001-01 · pruebas: línea base actual | REG-01..07; `tests/UtttGo.*Tests/`, [QA](../../../docs/qa-api-001.md) solo lectura | Sin gate; ejecutar restore/build Release/format/tres suites del paso 3 de guía. **Hecho**: reporte de comandos/exit codes/recuentos, o impedimento reproducible; no llamar actuales los 53 verdes históricos. | Lista, no ejecutada. |
| DEV-API001-02 · evidencia: separar fuentes | REG-01..09; `docs/qa-api-001.md`, `docs/migracion-sdd.md`, `docs/traceability.md` (lectura) | Sin gate; cotejar 53 dobles, `202/422` local comunicado de 2026-11-06 y OpenAPI AWS 2026-11-12. **Hecho**: matriz fecha/entorno/artefacto/alcance o «pendiente de fuente», sin curl nuevo. | Lista. |
| DEV-API001-03 · pruebas: mapa de cobertura | REG-01..09, T-AUTH-01/03/05/14/15/18/19; `RegistrationServiceTests`, `RegisterEndpointTests`, `RegisterSecurityTests` (lectura) | Tras 01; asignar método real a escenario y listar ausencia de Mongo/Redis reales, OTP, fallo parcial y timing. **Hecho**: matriz método/evidencia/brecha, sin editar tests. | Lista. |
| DEV-API001-04 · cierre documental local | RF-01/03/15, AC-001/011; trío local (único editable), matriz histórica (lectura) | Tras 02/03; validar IDs, enlaces y estados y entregar reporte. **Hecho**: revisión reproducible, sin aprobar ni marcar TASK-AUTH. | Lista. |

## Tareas bloqueadas hasta decisión y gates SDD

**Gate común para ejecutar cambios técnicos (no para solicitar decisiones):** [TASK-AUTH-007 → 008 → 010](../../phase-02-auth-users/task.md), ficha aprobada y 002/011/012/020 según cambio; [decisiones § Orden y bloqueos](../../../docs/decisiones-pendientes-fase-02.md). Ni el código existente ni TASK-AUTH-001/006 `[x]` dispensan este orden. Obtener un acta en DEV-API001-05/07/10 es trabajo de coordinación bloqueante, no programación ni cierre previo de su propio TASK-AUTH.

| ID · clase/objetivo | Requisito y escenarios; componentes | Dependencia; acción posterior y criterio de finalización/evidencia | Estado |
|---|---|---|---|
| DEV-API001-05 · decisión de contrato restante | REG-01..07; DF02-02/07, TASK-AUTH-010; spec/DTO/controlador/filtro | Solicitar revisión al equipo (no depende del cierre previo de TASK-AUTH-010); obtener acta para JSON, validaciones, `400/422/413/429/502/503`, tamaño, cuotas/reintentos/antienumeración, conservando `202`. **Hecho**: contrato ratificado por escenario, sin editar código. | Bloqueada: equipo. |
| DEV-API001-06 · corrección de diferencias ratificadas | REG-01..07; `AuthController`, `RegisterRequest`, validador, filtro, `ApiError`, suites Register* | Tras 05; cambiar solo diferencias aprobadas y tests correspondientes. **Hecho**: HTTP/DTO coinciden con acta, build/format/suites verdes; si no hay diferencia, reportar sin cambio. | Bloqueada: 05. |
| DEV-API001-07 · decisión OTP/SMTP | REG-08, RF-15; DF02-01/10, TASK-AUTH-002; Supabase/móvil/SMTP | Contrastar configuración/plantilla reportadas en main con el equipo (sin dar por probado el envío); ratificar expiración/reenvío, consulta confiable y plan de prueba de entrega/costo. **Hecho**: acta y plan de pruebas OTP sin ruta nueva ni elegir enlace. | Bloqueada: técnica/SMTP. |
| DEV-API001-10 · decisión de reconciliación | REG-07/09; DF02-09, TASK-AUTH-011/012; Supabase/Mongo y contrato de perfil | Solicitar diseño al equipo (no requiere cerrar sus propios TASK-AUTH-011/012); aprobar idempotencia, reparación ante fallo parcial, fuente/frecuencia de confirmación. **Hecho**: acta con casos retry/carrera y criterio de no acceso huérfano, sin editar código. | Bloqueada: diseño. |
| DEV-API001-08 · implementación faltante de reparación | REG-07/09; `RegistrationService`, `SupabaseAuthClient`, `MongoUserRepositories`, tests | Tras 10 y gate común; aplicar diseño aprobado y probar Supabase creado+Mongo fallido, duplicado y carrera en entorno local autorizado. **Hecho**: no `202` ficticio ni acceso huérfano, build/format/suites verdes. | Bloqueada: 10. |
| DEV-API001-09 · validación de límites/proxy | REG-05/06; filtro, Redis gate, proxy, pruebas de integración | Tras 05 y gate común; probar tamaño chunked, IP y cuotas compartidas en dos réplicas no productivas. **Hecho**: mediciones/reintentos y fallos fail-closed reproducibles sin secretos. | Bloqueada: contrato/entorno. |
| DEV-API001-11 · validación OTP dependiente | REG-08, RF-15; ficha externa OTP/guard, SMTP autorizado | Tras 07 y gate común; **solo en ficha externa autorizada**, probar OTP correcto/erróneo/vencido, reenvío y bloqueo privado; API-001 solo registra la dependencia. **Hecho**: referencia a resultados externos sin secretos; no se cierra con `202` de registro. | Bloqueada: técnica/entorno. |

## Guía de ejecución con OpenCode

### Control de ejecución

El registro siguiente normaliza **una vez** los estados y dependencias de las tareas detalladas arriba; las menciones a sus IDs en spec/plan son referencias, no nuevas tareas. `Autorización` pendiente no se satisface por escribir un ID: exige acta identificable y gate cerrado. `Evidencia de cierre: PENDIENTE` impide marcar DONE.

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API001-01 | QA | READY | NO | N/A: lectura y comandos locales | - | PENDIENTE |
| DEV-API001-02 | EVIDENCIA | READY | NO | N/A: lectura | - | PENDIENTE |
| DEV-API001-03 | QA | READY | NO | N/A: lectura | DEV-API001-01 | PENDIENTE |
| DEV-API001-04 | DOCUMENTAL | READY | NO | N/A: revisión del trío | DEV-API001-02, DEV-API001-03 | PENDIENTE |
| DEV-API001-05 | DECISION | BLOCKED | NO | Pendiente: DF02-02/07, TASK-AUTH-010 | - | PENDIENTE |
| DEV-API001-06 | CORRECCION | BLOCKED | SI | Pendiente: acta 05 y TASK-AUTH-007/008/010/020 | DEV-API001-05 | PENDIENTE |
| DEV-API001-07 | DECISION | BLOCKED | NO | Pendiente: DF02-01/10, TASK-AUTH-002 | - | PENDIENTE |
| DEV-API001-10 | DECISION | BLOCKED | NO | Pendiente: DF02-09, TASK-AUTH-011/012 | - | PENDIENTE |
| DEV-API001-08 | IMPLEMENTACION | BLOCKED | SI | Pendiente: acta 10 y TASK-AUTH-007/008/010/020 | DEV-API001-10 | PENDIENTE |
| DEV-API001-09 | INTEGRACION | BLOCKED | SI | Pendiente: acta 05, entorno y TASK-AUTH-007/008/010 | DEV-API001-05 | PENDIENTE |
| DEV-API001-11 | DEPENDENCIA_EXTERNA | BLOCKED | NO | Pendiente: acta 07, ficha externa y TASK-AUTH-002 | DEV-API001-07 | PENDIENTE |

Para campos de objetivo, escenario, archivos, acción, resultado y prueba véase la **fila detallada única** del mismo ID; el validador exige correspondencia uno a uno entre ambas tablas.

1. Verificar rama/HEAD/estado Git; leer [constitución v2.6](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → estas tareas, más [fase 02](../../phase-02-auth-users/spec.md), [TASK-AUTH](../../phase-02-auth-users/task.md), [decisiones](../../../docs/decisiones-pendientes-fase-02.md) y [trazabilidad](../../../docs/traceability.md). Ante conflicto prevalece constitución; no inferir aprobaciones.
2. Elegir **solo 01..04** sin acta nueva, de uno en uno. Para 05..11 verificar gates y decisión concreta. Consultar código y suites citados en [plan](plan.md#secuencia-de-implementación-condicionada); una prueba con doble no demuestra integración real.
3. Desde la raíz, si el entorno lo permite: `dotnet restore UTTTGO.Backend.slnx`; `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`; `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`; `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build` y análogos para `UtttGo.IntegrationTests` y `UtttGo.SecurityTests`. Registrar impedimentos; no usar producción para pruebas.
4. Reportar ID, escenario, configuración doble/local, comando, exit code, fecha, evidencia y límites sin tokens/PII/secretos. Durante esta intervención no cambiar producción/tests/infra/históricos, middleware ni endpoints; en tarea futura autorizada limitar el diff al alcance. No marcar TASK-AUTH ni aprobación sin acta.
