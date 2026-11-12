# Migración SDD atómica — plan de ejecución por lotes

> **Plan aprobado con ajustes; lote 0 en preparación.** Línea base `origin/main` (al iniciar: `31bd1a4`), rama `docs/sdd-atomico` creada en worktree separado. Fuente única de verdad: [constitución v2.6](constitution.md). Un PR por lote; CI verde **antes** de fusionar. Esta matriz no aprueba contratos nuevos, no modifica las 42 fichas del Excel ni despliega nada. Referencias metodológicas: [API_SDD](https://github.com/edelomeza/API_SDD) (tríos `spec/plan/task`, criterios y agentes) y [WebAPIDevSecOpsScalling](https://github.com/edelomeza/WebAPIDevSecOpsScalling) (gates y evidencia); sus tecnologías no sustituyen el stack constitucional.

## Reglas y árbol de destino

- Tres bloques: **1 arquitectura/base**, **2 una ficha por endpoint o función independiente**, **3 infraestructura/operación**. Cada ficha activa tiene `spec.md`, `plan.md`, `task.md`; `spec.md` máximo ~2 páginas, enlace a constitución, alcance, RF/AC originales, Given/When/Then, **Evidencia** (prueba exacta, entorno, fecha y límites) y aprobación identificable. La autoridad de una decisión no se transfiere automáticamente por copiarla.
- `specs/_archivo/` conserva íntegros los documentos de origen, incluidos los textos superados; archivar **después** de verificar el destino de cada ID. `specs/_pendiente/README.md` será el **único archivo** bajo `_pendiente/`: tabla de ID, método/ruta, estado y origen histórico. Sin subdirectorios vacíos ni fichas ficticias. Hasta el lote correspondiente no se crea este archivo.
- Nunca crear spec nueva de **API-016**: retirada funcional por DEC-019, sin implementación ni redirección; Excel de 42 fichas intacto; su función la cubre API-011. La ficha adicional aprobada `GET /api/v1/sellers/me` conserva ID **DEC-019.d**; no renumerarla como API-043. `GET /health` es sonda, no ficha del Excel.
- «Implementada» = código presente; «Verificado» solo por curl documentado del 2026-11-06 o test concreto (rotulado **con dobles** si corresponde). Los curl acreditan: register `202`, login `200`, auth/me `200`, users/me `200`, PATCH users/me `200`, sellers `200/404`, admin/users `403`. **NO** acreditan admin/users `200`. API-009/010/DEC-019.d: **«Implementada con dobles; sin verificación externa documentada»** hasta disponer de evidencia específica; no inferirla del despliegue de infraestructura. La sonda y SSM tienen evidencias operativas distintas, no prueban esos tres endpoints.

```text
specs/
├── bloque-1-arquitectura/
│   ├── base-modular/{spec,plan,task}.md
│   ├── identidad-compartida/{spec,plan,task}.md
│   ├── persistencia-mongodb/{spec,plan,task}.md
│   ├── redis-rate-limit-revocacion/{spec,plan,task}.md
│   └── contratos-transversales/{spec,plan,task}.md
├── bloque-2-funcionalidades/
│   ├── api-001-register/{spec,plan,task}.md
│   ├── api-002-login/{spec,plan,task}.md
│   ├── api-004-auth-me/{spec,plan,task}.md
│   ├── api-006-users-me/{spec,plan,task}.md
│   ├── api-007-update-users-me/{spec,plan,task}.md
│   ├── api-008-seller-public-profile/{spec,plan,task}.md
│   ├── api-009-seller-activation/{spec,plan,task}.md
│   ├── api-010-update-seller-profile/{spec,plan,task}.md
│   ├── dec-019-d-seller-own-profile/{spec,plan,task}.md
│   └── api-039-list-users/{spec,plan,task}.md
├── bloque-3-infraestructura/
│   ├── aws-terraform/{spec,plan,task}.md
│   ├── docker-nginx/{spec,plan,task}.md
│   ├── ci-devsecops/{spec,plan,task}.md
│   ├── cd-ssm/{spec,plan,task}.md
│   ├── seguridad-operativa/{spec,plan,task}.md
│   └── observabilidad-health/{spec,plan,task}.md
├── _pendiente/README.md             # solo índice de las fichas no implementadas
└── _archivo/                       # conservar árbol y archivos originales de specs/
```

**Propiedad transversal sin segunda constitución:** `docs/constitution.md` define stack, BR, seguridad, fallos Redis, errores y DEC; bloque 1 explica decisiones de aplicación y dependencias, no repite la regla. `persistencia-mongodb` trata colección `users`/`sellers`, índices y frontera; `redis-rate-limit-revocacion` documenta gate compartido y evidencia; `contratos-transversales` limita errores/paginación/correlationId. Políticas de release/DEP: [requisitos académicos](academic-requirements-phase-02.md). Inventario y estado HTTP: [trazabilidad](traceability.md) y Excel original. Historial: `memoria.md`; operación actual: `PROJECT_STATUS.md`.

## Correspondencia por archivo de origen y estado

En las filas con `/{spec,plan,task}.md` se migran **los tres archivos** por separado y se archiva cada original bajo `specs/_archivo/` con su misma ruta relativa; chat solo dispone de `spec.md`. Los ID de fase se conservan textualmente, incluso cuando un RF/AC global referencia dos fichas; una definición canónica y referencias cruzadas evitan renumerar o duplicar.

| Archivo(s) de origen en `specs/` | Requisitos/tareas originales → destino dueño | Contrato, prueba/evidencia, estado y referencias a actualizar |
|---|---|---|
| `phase-00-constitution/00-01-principles-stack/{spec,plan,task}.md` | `REQ-001..020`, `T-001..011`, `AC-001..013` → bloque 1 `base-modular`, `persistencia-mongodb`, `redis-rate-limit-revocacion`, `contratos-transversales`; requisitos de nube/CI → bloque 3 mediante referencia constitucional. | Reglas de gobierno, sin endpoint; v1.3 **borrador**, v1.1 aprobación histórica. Referencias: AGENTS §11, README SDD, `memoria.md`, `docs/traceability.md`. No reeditar constitución. |
| `phase-02-auth-users/{spec,plan,task}.md` | `RF-01..15`, `RNF-01..17`, `AC-001..013`, `TASK-AUTH-001..008/010..012/020..026/030..032` → `identidad-compartida`, fichas API-001..010/039/040 y bloque 3 para gate 006/007/031; `TASK-AUTH-020..026` se enlazan desde su API, 002..005/010..012/030/032 permanecen compartidas con referencia desde cada ficha. **No se crean TASK-AUTH-009/013..019/027..029.** | API-001/002/004/006/007/008 implementadas y QA `docs/qa-api-001/002/004/006/007/008.md`; 003/005/040 **pendientes**. 001/006 cerradas solo documentalmente; 007/008 y cierre integral abiertos. RF-06 (recuperación) y RF-15 (OTP) no autorizan ruta nueva. Comparar variantes locales de fase 02 antes de consolidar; referencias: trazabilidad general y de siete endpoints, README, AGENTS, memoria. |
| `phase-02-auth-users/api-039-list-users/{spec,plan,task}.md` | `RF-07`, `RNF-01/03/04/07/08/09/12/17`, `TASK-039-001/002/003/010..013/020/021` → API-039; `AC-005` global enlaza API-039 **y** API-040. | Contrato candidato local 0.2.0, `AdminUsers*Tests`, `docs/qa-api-039.md`; código implementado, auditoría persistente/contrato integral pendientes. Curl admin `403` sí; `200` real **no acreditado**. Referencias: docs/traceability*, README, memoria. |
| `phase-03-catalog-offers/{spec,plan,task}.md` — Sprint 1 | `RF-V01..RF-V12`, `T-CAT-V01..V12`, `TASK-SELLER-010..012/020..024/030..032/040..042`, `TASK-CATALOG-001/003/005` → API-009, API-010, DEC-019.d; modelo/fuentes compartidas → bloque 1. **Sin inventar TASK-SELLER intermedias.** | Sprint 1 aprobado por DEC-019; `SellerActivation*Tests`, `OwnSellerProfile*Tests`, `UpdateOwnSellerProfile*Tests`; **implementada con dobles; sin verificación externa documentada** para cada una de las tres rutas. `TASK-SELLER-041` abierto. API-008 refiere proyección pública sin copiarla. Referencias: trazabilidad, PROJECT_STATUS, memoria. |
| `phase-03-catalog-offers/{spec,plan,task}.md` — Sprint 2 | `RF-01..14`, `T-CAT-01..08`, `TASK-CATALOG-002/004` → índice `_pendiente` API-011..015/019..022, API-023 y Media 017/018 según propietario; `TASK-CATALOG-003` evidencia retirada de API-016. | **No aprobado/implementado**; RF-06 baja lógica de producto y ficha de eliminación puntual pendiente, sin inventar ID/ruta. Sprint 1/2 sin imágenes por DEC-019. Referencias: trazabilidad, memoria y docs de Orders/Media cuando existan. |
| `phase-07-chat-realtime/spec.md` | DEC-005/BR-012, API-037/038 → índice `_pendiente` y referencia al archivo histórico. | Función confirmada; diseño purga/almacenamiento y contratos pendientes; sin plan/task ni pruebas ejecutadas. Referencias: trazabilidad, memoria. |
| `cross-cutting/api-health-probe/{spec,plan,task}.md` | `HP-01..11`, `HP-AC-01..11` → bloque 3 `observabilidad-health`; HP-07/08/09 referenciados por Docker/CD. | Aprobada e implementada; `Health*Tests`, pruebas locales Mongo/Redis y evidencia HTTP AWS `/health`; no ficha API numerada. Referencias: AGENTS §1.2, PROJECT_STATUS, trazabilidad, infra, CI. |
| `cross-cutting/infra-aws-free-tier/{spec,plan,task}.md` | `INFRA-01..16`, `INFRA-AC-01..13`, `INFRA-T01..T21` → bloque 3 `aws-terraform`, `docker-nginx`, `cd-ssm`, `seguridad-operativa`, `observabilidad-health` (dueño único por ID). | Terraform/Compose/SSM/Budget/logs y tareas `[ ]` enlazados a `PROJECT_STATUS.md`, `docs/traceability.md`, `infra/terraform/` y evidencia CI/CD; TLS, backend remoto y memoria pendientes. Textos originales SSH/22/CORS `*` **históricos superados** por DEC-020; no copiarlos como vigentes. |
| `cross-cutting/ci-cd-devsecops/{spec,plan,task}.md` | `CI-R01..13`, `CI-AC-01..13`, tareas CI y `TASK-AUTH-031` → `ci-devsecops` y `cd-ssm`; versiones → enlace a documento académico. | CI-1/CD materializados; runs, `ci-evidence/`, workflows de `origin/main`. Textos antiguos sobre SSH/OIDC o «CD sin ejecutar» superados por DEC-020/021; `TASK-AUTH-007/031` no se cierran por mover documentos. Referencias: README, AGENTS, PROJECT_STATUS y trazabilidad. |

### Inventario completo del bloque 2 (sin alterar fichas)

| Estado al iniciar | IDs y destino | Fuente histórica / evidencia |
|---|---|---|
| Implementadas, aprobación total **no inferida** | API-001, 002, 004, 006, 007 → fichas activas en bloque 2, lote 2a | `phase-02-auth-users/`, `docs/qa-api-{001,002,004,006,007}.md`; curl 2026-11-06 o tests con dobles según escenario. |
| Implementadas | API-008, 009, 010, 039 y DEC-019.d → fichas activas en bloque 2, lote 2b | API-008 curl `200/404`, API-039 curl `403` y QA con dobles; **API-009/010/DEC-019.d: «Implementada con dobles; sin verificación externa documentada»**. Orígenes fase 02, ficha 039 y fase 03 v2.1. |
| Pendientes: Identity/Users | API-003, 005, 040 → solo `_pendiente/README.md` | `phase-02-auth-users/`; logout/refresh, borrado compartido DEC-003, cambio de estado Admin pendientes. |
| Pendientes: catálogo/Media/categorías | API-011, 012, 013, 014, 015, 017, 018, 019, 020, 021, 022 → solo índice | `phase-03-catalog-offers/`, constitución §7 y Excel; API-011 cubre función retirada de API-016; Media sin spec propia aprobada. |
| **Retirada** | API-016 → **ninguna spec nueva**, solo fila histórica de índice/trazabilidad | Excel sin cambios; DEC-019 y `phase-03-catalog-offers/` archivada; no implementar/redirigir. |
| Pendientes: Orders/Reviews/Favorites/Notifications/Chat/Admin | API-023, 024, 025, 026, 027, 028, 029, 030, 031, 032, 033, 034, 035, 036, 037, 038, 041, 042 → solo índice | Excel y `docs/traceability.md`; chat además `phase-07-chat-realtime/spec.md`. Ningún endpoint supuesto por ausencia de ficha. |

## Referencias que cambiarán, sin cambiar contratos

1. `docs/traceability.md`: abreviaturas `A/C/H`, 42 filas spec/plan/task, DEC-019.d, estado API-016, salud e infraestructura; conservar Excel y su método/ruta.
2. `docs/traceability-siete-endpoints.md` y `docs/qa-api-{001,002,004,006,007,008,039}.md`: enlaces a fichas atómicas, separación curl/tests con dobles/operación AWS, sin alterar resultados históricos; **sin admin `200` verificado**.
3. `AGENTS.md`, `README.md`, `CHECKLIST_PR.md`: flujo, mapa, agentes y enlaces; las tablas de permisos viven en `.opencode/agents/`, no se duplican. `memoria.md` y `PROJECT_STATUS.md`: cambio de rutas y estado sin borrar historia.
4. `docs/academic-requirements-phase-02.md`, `docs/decisiones-pendientes-fase-02.md`, `docs/referencias-profesor-backend.md`, `skills/be-*.md`: enlaces a la base vigente, sin copiar requisitos constitucionales ni importar SQL/EF del profesor.
5. Referencias internas de los 22 archivos de `specs/` (en `origin/main`): antes de archivarlos identificar su nuevo destino, referencias de otros documentos y enlaces históricos. No reescribir originales para simular una aprobación posterior.

## Lotes, responsable y gates de cierre

En cada lote @traceability-clerk **emite** tabla `ID/origen | destino | evidencia | estado | drift`; @spec-keeper la **guarda** en `docs/migracion-sdd/lote-N.md` y añade una fila a `memoria.md`. El clerk comprueba cada requisito de origen y cada «Verificado»; @security-reviewer reporta riesgos sin edición. No hay commits automáticos ni merge sin PR y CI verde.

| Lote | Responsable y entrega limitada | Criterio objetivo de cierre |
|---|---|---|
| **0** | @spec-keeper: cinco archivos `.opencode/agents/` (el responsable crea estos metadatos fuera del permiso de edición habitual del agente), este plan, `CHECKLIST_PR.md`, enlaces en AGENTS/README, wrappers de seis skills sin duplicación; clerk sin editar. | `origin/main` base, diff **solo documental** revisado antes de commit, `docs/migracion-sdd/lote-0.md` y fila en memoria, cinco agentes con permisos explícitos, ninguna ficha/contrato/constitución/código modificados. PR independiente y CI verde antes de fusionar. |
| **1** | @spec-keeper: bloque 1 mínimo `base-modular`, `persistencia-mongodb`, `redis-rate-limit-revocacion`, `contratos-transversales`; archivar fase 00. | `REQ-001..020`, `AC-001..013`, `T-001..011` tienen destino; cada spec ≤~2 páginas, enlaces a v2.6 y evidencia trazable; clerk emite reporte. |
| **2a** | @spec-keeper: `identidad-compartida`, API-001/002/004/006/007; archivar fuente fase 02 **solo después de** confirmar destinos de su contenido (puede posponer traslado físico a 2b). | RF/RNF/AC/TASK-AUTH aplicables enlazados; cinco fichas con test/curl exacto, gates no completados intactos; clerk verifica y registra pendientes hacia 2b. |
| **2b** | @spec-keeper: API-008/009/010/DEC-019.d/039, archivar fase 02 y Sprint 1 fase 03 una vez conciliadas las partes de Sprint 2 y el archivo histórico. | `RF-V01..12`, `T-CAT-V01..12`, `TASK-SELLER-*`, `TASK-039-*`, `AC-005` y QA sin pérdidas; las tres rutas Seller sin curl no dicen «Verificado»; admin `200` sin verificación. |
| **3** | @spec-keeper: crear **solo** `_pendiente/README.md` (42 filas con método/ruta/estado/origen, más fila DEC-019.d diferenciada), archivar catálogo/chat conservando Sprint 1; completar matriz. | 42/42 IDs, método/ruta del Excel inalterados; API-016 retirada sin spec; RF-06/15, RF-Catalog y chat/DEC-003/005 con destino o referencia histórica; clerk verifica. |
| **4** | @spec-keeper: bloque 3 y archivo `cross-cutting/`. @infra-builder revisa correspondencia, sin editar artefactos. | `HP-*`, `INFRA-*`, `CI-*` y tareas abiertas rastreables; DEC-020/021 prevalecen sobre SSH/22/OIDC históricos; evidencia operativa no se atribuye a endpoints Seller. Clerk y security-reviewer reportan. |
| **5** | @spec-keeper: enlaces, memoria, checklist y conciliación final; aprobación humana de cada spec. | 42 fichas + DEC-019.d y salud identificadas; referencias sin roturas, todos los IDs de origen con destino, «Verificado» con prueba real específica, CI verde y PR por lote. |

## Riesgos de conservación

- Mezclar rama `feature/siete-endpoints-locales` (v2.3 y cambios locales sin integrar) con `origin/main` v2.6 revertiría DEC-018..021. Contrastar sus variantes, nunca sobrescribirlas ni adoptarlas como autoridad.
- La fase 03 v2.1 **solo aprueba Sprint 1**; API-016 está retirada, pero API-011..015/023 y ficha de eliminación siguen pendientes. `RF-06` (recuperación), `RF-15` (OTP), `AC-005` compartido y `TASK-AUTH-007` pueden perderse al dividir archivos.
- `infra-aws-free-tier/spec.md` y `ci-cd-devsecops/spec.md` retienen textos antiguos incompatibles con DEC-020/021. Archivarlos como historia; nuevas fichas enlazan la constitución v2.6 y evidencia de ejecución posterior.
- Un test con dobles, un `403` de Admin o `/health 200` no prueban `GET /admin/users 200` ni los tres endpoints Seller. El estado «Verificado» requiere escenario y artefacto apropiados.
