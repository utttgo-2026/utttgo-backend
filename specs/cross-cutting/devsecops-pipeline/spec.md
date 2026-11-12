# spec.md — Pipeline DevSecOps con Quality Gate · Backend UTTT-GO

> Versión documental **0.1.0 (borrador)** · 2026-11-12 · **No autoriza aún modificar los YAML**: esta spec se entrega a revisión del propietario de producto y solo tras su aprobación se crean `plan.md` y `task.md` y se implementa.
> Autoridad: `docs/constitution.md` v2.5 (§§ 2, 15, 17) → esta spec → `plan.md` → `task.md` → `.github/workflows/ci-1.yml`.
> **Relación con `specs/cross-cutting/ci-cd-devsecops/spec.md` v0.5.0:** la complementa y enmienda tres puntos que esa spec dejó explícitamente pendientes de ratificación: (a) SAST pasa de «piloto informativo» a **gate bloqueante** (su § 5 lo permitía «tras nueva aprobación»), (b) **Stryker y el fuzzing** dejan de estar «fuera de alcance» (su § 1) y (c) se introduce un gate de PR único que agrega todos los jobs. La aprobación de este documento es esa ratificación; al implementar se concilian ambos specs y `AGENTS.md`.
> Nombre conceptual del pipeline: el usuario/propietario se refiere al flujo como `ci-cd.yml`; los archivos reales son **`.github/workflows/ci-1.yml`** (CI, este spec) y **`.github/workflows/cd-deploy.yml`** (CD-1, sin cambios de contrato).

## 1. Objetivo y alcance

Evolucionar CI-1 de un job monolítico a una arquitectura DevSecOps de **jobs paralelos que convergen en un `quality-gate` bloqueante** con *Hardening Report* combinado, incorporando:

1. **SAST bloqueante** con Semgrep para C# (hoy piloto informativo).
2. **Pruebas de mutación** con Stryker.NET en paralelo al build/tests normal.
3. **Seguridad de contenedores** con Dockle sobre la imagen Docker real de la API.
4. **DAST + fuzzing** con OWASP ZAP y RESTler contra la API levantada en el runner con dependencias efímeras (MongoDB, Redis).
5. **PR Quality Gate**: job final que converge todos los trabajos y bloquea el PR si alguna métrica falla.

**Fuera de alcance (aplican las restricciones del propietario):**

- **Pact / Contract Tests**: no hay frontend ni consumidores que exijan contratos; se descarta.
- **Herramientas SQL/EF** (EF Core, migraciones, DBA linters): el stack es MongoDB/Redis; descartadas.
- **NBomber, ChaosTest, pruebas de carga**: sin umbrales NFR aprobados (AGENTS § 4); descartados.
- **SonarCloud**: servicio externo con token; no aporta frente a Semgrep OSS local; descartado en esta fase.
- **Escaneo CVE de paquetes de la imagen (Trivy)**: no pedido; véase § 10. Dockle cubre configuración/buenas prácticas/secretos en imagen, **no CVE de SO/librerías** — los CVE .NET ya los cubre NuGetAudit (bloqueante).
- Publicación de imágenes, CD, cambios de contratos de API, `terraform apply` en PR: sin cambios.
- Expansión de permisos del workflow (se mantiene `permissions: contents: read`; SARIF/code-scanning queda como decisión abierta § 11).

## 2. Fuentes y trazabilidad de requisitos

| ID | Fuente | Obligación | Evidencia |
|---|---|---|---|
| DV-R01 | Solicitud directa del propietario (2026-11-12) con restricciones explícitas | SAST Semgrep + Stryker + Dockle + ZAP + RESTler + Quality Gate con Hardening Report, en rama `feature/devsecops-pipeline`, spec antes de YAML | Este documento aprobado; PR de implementación posterior con jobs reales |
| DV-R02 | `AGENTS.md` § 1.4 (orden de CI) y § 6 («SAST se vuelve bloqueante cuando el equipo lo habilite formalmente») | Habilitación formal de gates bloqueantes por spec aprobada; el orden de pipeline se actualiza con este spec | § 9 (activación) y enmienda de `AGENTS.md` al aprobar |
| DV-R03 | `specs/cross-cutting/ci-cd-devsecops/spec.md` v0.5.0 §§ 1, 3, 5 (CI-R04/R09) | Todo PR ejecuta todos los checks (sin filtros de rutas que omitan gates); omisión nunca cuenta como verde; SAST bloquea tras ratificación | AC-01, AC-03, AC-09, AC-12 |
| DV-R04 | `AGENTS.md` § 7 (testing) y § 10.5 (herramientas descartadas) | **Enmienda:** Stryker y fuzzing dejan de estar descartados; resto (Pact, NBomber, ChaosTest…) sigue descartado | § 9; actualización de `AGENTS.md` § 10.5 al aprobar |
| DV-R05 | `docs/constitution.md` v2.5 § 11 (costo $0) y DEC-018 | Sin servicios de pago: todo usa imágenes/actions OSS; repo público ⇒ minutos de Actions ilimitados | § 7 (sin secretos nuevos, sin costos) |
| DV-R06 | `docs/referencias-profesor-backend.md` (PDF puntos 5/7/8: SDD y DevSecOps) | Pipeline DevSecOps verificable por SDD | Spec/plan/task y evidencia de runs |
| DV-R07 | Límite de `ci-cd-devsecops` (CI-2 = integración real con Mongo/Redis **aprobada aparte**) | **Enmienda acotada:** DAST/Fuzz levantan Mongo/Redis **efímeros y aislados en el runner de PR**, sin credenciales ni servicios externos (Supabase inaccesible ⇒ 401); no abre CI-2 completo | § 5.5, AC-06, decisión abierta § 11 |

## 3. Arquitectura objetivo (CI-1)

```
pull_request → main  |  push → feature/**, main
│
├─ backend            restore + NuGetAudit + formato + build Release + 3 suites + resumen + secret scan (Gitleaks)   [existente, bloqueante]
├─ sast               Semgrep C# bloqueante (reglas locales + p/csharp, baseline contra origin/main)                   [nuevo, bloqueante]
├─ mutation           Stryker.NET sobre UtttGo.UnitTests (paralelo al job backend)                                    [nuevo, bloqueante Fase A/B]
├─ container          docker build de la imagen real + Dockle                                                          [nuevo, bloqueante]
│      └─(artefacto: imagen tarball)
├─ dast               stack efímero (mongo + redis + api) + OWASP ZAP (baseline + API scan con /openapi/v1.json)       [nuevo, bloqueante]
├─ fuzz               stack efímero propio + RESTler (compile desde /openapi/v1.json + light fuzz acotado)             [nuevo, bloqueante]
│
└─ quality-gate       needs: [backend, sast, mutation, container, dast, fuzz] · if: always()
                      → Hardening Report combinado (step summary + artefacto) → conclusión del workflow
```

- **Un solo workflow** (`ci-1.yml`): mantiene el vínculo automático de CD-1 (`workflow_run` exige `conclusion == success`; un gate rojo ⇒ CD-1 no corre) y evita dos required-checks dispersos.
- Los jobs son **independientes entre sí** (cada uno con su `checkout`; sin dependencias cruzadas salvo `container → dast/fuzz` por artefacto de imagen) y corren **en paralelo**, sin filtros de rutas (DV-R03): un PR documental también ejecuta todo.
- `concurrency` global existente se conserva (cancela runs obsoletos del mismo evento).
- El job `backend` **no se renombra** para no romper el required check actual de branch protection; `quality-gate` se añade como required check nuevo (§ 9).

| Job | Herramienta (versión a pinear por digest/SHA) | Timeout | Bloqueante | Salida/evidencia |
|---|---|---|---|---|
| `backend` | .NET SDK 10 + Gitleaks 8.30.1 (ya pineado) | 45 min | Sí (ya) | `ci-evidence/*` (existente) |
| `sast` | `semgrep/semgrep:1.183.0@sha256:2e45517a…` (ya usado en repo) + `.semgrep/ci-1.yaml` + `p/csharp` | 15 min | Sí (Fase A) | `semgrep-summary.json`, conteo de hallazgos ERROR nuevos |
| `mutation` | `dotnet-stryker` (versión a verificar con .NET 10, § 10) | 60 min | Sí (Fase A/B, § 5.3) | `mutation-summary.json` con score |
| `container` | `docker build` + `goodwithtech/dockle:<ver>@sha256:<digest>` | 30 min | Sí (nivel error) | `dockle-report.json` + imagen `utttgo/api:ci-<sha>` |
| `dast` | `ghcr.io/zaproxy/zaproxy:<ver>@sha256:<digest>` | 30 min | Sí (WARN+ y salud) | reporte ZAP (JSON/HTML) + logs acotados |
| `fuzz` | `microsoft/restler:<ver>@sha256:<digest>` | 30 min | Sí (buckets 5xx y ejecución) | `stats.json` + `bug_buckets` |
| `quality-gate` | script propio (python3 del runner) | 10 min | Es el gate | `hardening-report.md` (step summary + artefacto 7 días) |

Todos los jobs: `runs-on: ubuntu-24.04`, sin secretos de nube, `permissions` heredadas `contents: read`.

## 4. SAST — job `sast` (Semgrep bloqueante)

- **Ejecución:** mismo contenedor ya fijado en el repo (sin cambio de versión): `semgrep/semgrep:1.183.0@sha256:2e45517a85e13a9cca14a2a00df6bb48affd66b8ebfae1b0991cd57578362fb0`, `--metrics=off`, sin red salvo pull de la imagen.
- **Reglas:** `--config .semgrep/ci-1.yaml` (regla local CORS, ya existente) **+** `--config p/csharp` (reglas públicas OWASP/CWE para C#). Ampliar `.semgrep/` solo con reglas locales revisadas; nada copiado de otros repos.
- **Política de fallo (con baseline):** se ejecuta `--baseline-ref origin/main`; **solo bloquean hallazgos `ERROR` introducidos por el PR** (los preexistentes quedan como deuda visible en el reporte, no impiden el merge del cambio que no los toca). Hallazgos `WARNING`/`INFO` → informe (la regla CORS local actual es WARNING y no bloquea).
- **Fallo de la herramienta** (imagen no descarga, `p/csharp` inaccesible, JSON ilegible) ⇒ **job rojo** (mismo criterio de «indisponibilidad ≠ verde» que NuGetAudit en `ci-cd-devsecops` § 3). Nunca `continue-on-error`.
- **Evidencia:** `ci-evidence/semgrep-summary.json` (modo «gate-blocking», código de salida, total ERROR nuevos, ruleIds) sin fragmentos de código en logs; el JSON crudo solo si se confirma que no contiene snippets (verificar en plan).

## 5. Jobs nuevos

### 5.1 `mutation` — Stryker.NET

- **Objetivo:** medir qué proporción de mutantes sobreviven a `tests/UtttGo.UnitTests` (las suites de integración/seguridad quedan fuera de mutación en esta fase).
- **Paralelismo:** job propio con checkout + `setup-dotnet` + restore propio; **no** depende de `backend` (pedido explícito: correr en paralelo al build normal).
- **Configuración en repo:** `stryker.json` a definir en `plan.md` (proyecto de tests, mutadores estándar, `concurrency` según cores del runner, reporte JSON).
- **Política de fallo por fases:**
  - **Fase A (primeros runs tras implementar):** el job es obligatorio; **bloquea** si la herramienta falla o si `score < 50%` (piso provisional de supervivencia; el número concreto es decisión abierta § 11). El score real se publica en el Hardening Report.
  - **Fase B (ratchet):** tras el propietario revisa los primeros scores, fija umbral = floor más alto que sostengan los runs verdes (reducción de mutantes sobrevivientes tolerados). Cambio de umbral = edición de este spec.
- **Compatibilidad:** condicionada a que exista versión de `dotnet-stryker` compatible con .NET 10/C# 14 (spike obligatorio en `task.md`; si no existe, el job se implementa como «sin ejecutar ⇒ gate rojo» documentado y se espera versión — nunca reportar verde sin ejecutar, DV-R03).
- **Evidencia:** `ci-evidence/mutation-summary.json` (score, mutantes totales/killed/survived, versión, duración) + artefacto `mutation-report` de Stryker (7 días).

### 5.2 `container` — Dockle

- **Construye la imagen real** del `Dockerfile` del repo: `docker build -t utttgo/api:ci-<sha> .` (contexto raíz). Es la primera vez que la imagen se construye en CI (hoy solo se construye en la EC2).
- **Dockle:** contenedor pineado por digest, `--exit-level error` ⇒ **bloquea a nivel `error`** (CIS Docker + buenas prácticas: usuario, capas, secretos embebidos, tags); `warn`/`info` → informe con ratchet posterior (misma filosofía de arranque conservadora).
- **Artefacto compartido:** `docker save | gzip` → `actions/upload-artifact` (nombre `api-image-<sha>`, 7 días) que consumen `dast` y `fuzz` (`docker load`). Un solo build por run (costo de minutos: repo público, $0; tiempo ~4–8 min).
- **Evidencia:** `ci-evidence/dockle-report.json` + código de salida por nivel.

### 5.3 `dast` — OWASP ZAP con stack efímero

- **Stack:** nuevo `docker-compose.ci.yml` (override, sin tocar el compose de producción) que: publica `api` en `127.0.0.1:8080`, escala a 1 réplica, desactiva `nginx` (no aplica sin Certbot), activa perfil `local` (mongo) y redis; red interna por defecto del compose; memoria acotada.
- **Imagen:** `docker load` del artefacto de `container` (`needs: container`) — sin re-build.
- **Entorno:** `.env.ci` en el runner con **valores ficticios** para Supabase/S3 (p. ej. `SUPABASE_URL=https://example.supabase.co`, JWT secret/issuer/audience de prueba): la app arranca, `/health` solo exige MongoDB+Redis; **todo endpoint autenticado debe responder 401** (no hay credenciales reales — límite documentado, no se disfraza de cobertura completa).
- **Precondición:** poll de `http://127.0.0.1:8080/health` hasta `200` (máx. 120 s); si no, **rojo** con `docker compose logs --tail 100`.
- **ZAP:** contenedor pineado con red `host` contra `http://127.0.0.1:8080` (alternativa: `zaproxy/action-*` pineada por SHA — se decide en `plan.md`; contrato idéntico):
  1. **Baseline scan** (pasivo) sobre `/`.
  2. **API scan** importando `http://127.0.0.1:8080/openapi/v1.json` (OpenAPI real generada por la app) — refuerza cobertura de endpoints documentados.
- **Política de fallo:** cualquiera de los scans con alertas `WARN` o superiores ⇒ job rojo (los `FAIL`/`INFO` solo informan); fallo de ejecución o imposibilidad de alcanzar el target ⇒ rojo. Active scan completo (fuzzing ZAP) fuera de Fase A (§ 11).
- **Evidencia:** reportes JSON/HTML de ZAP como artefacto (7 días), resumen de conteos por severidad en `ci-evidence/zap-summary.json`. Sin credenciales, no puede haber tokens en el reporte.

### 5.4 `fuzz` — RESTler

- **Stack efímero propio** (mismo patrón y override que § 5.3; `needs: container` para la imagen). ZAP y RESTler no comparten API para aislar resultados y paralelizar.
- **Flujo RESTler:** (1) `compile` generando la gramática desde `http://127.0.0.1:8080/openapi/v1.json` descargado del stack; (2) **light fuzz** acotado por presupuesto de tiempo (candidato: `--time_budget` ≈ 10 min; flag exacto a confirmar en el spike de `task.md`), sin bootstrap auth (no hay credenciales).
- **Política de fallo:** el job **rojo** si: falla la compilación/fuzz; hay **buckets de bugs con respuesta 5xx o timeout** en los endpoints alcanzables; o el artefacto de resultados no se genera. Las respuestas `401/404/400` **no** cuentan como bugs (esperadas sin sesión).
- **Límite honesto:** sin token válido, la cobertura real se limita a endpoints públicos (`/health`, `/openapi*`) y a la verificación de rechazo 401 del resto; está documentado como «cobertura de autenticación **no** acreditada». Una futura fase con token de prueba Supabase depende de TASK-AUTH-007/SELLER-041 y se hará por spec aparte.
- **Evidencia:** `ci-evidence/restler-summary.json` (número de endpoints compilados, iteraciones, buckets por tipo) + artefacto `restler-results` (7 días), sin payloads sensibles (no los hay).

### 5.5 Cambio acotado de frontera CI-1/CI-2 (DV-R07)

`ci-cd-devsecops` reserva Mongo/Redis reales a CI-2 (aprobación específica). Este spec autoriza **solo** su uso **efímero, local al runner y sin credenciales** para DAST/Fuzz: no toca la EC2, no usa Atlas/Supabase reales, no publica nada, termina con el job (runner efímero). No implica dar por abierta CI-2 ni sus requisitos de aislamiento/entornos.

## 6. Quality Gate y Hardening Report — job `quality-gate`

- `needs: [backend, sast, mutation, container, dast, fuzz]`, **`if: always()`** (corre aunque algún job hermano falle), `timeout-minutes: 10`.
- **Regla de decisión:** el gate pasa **solo si los 6 jobs terminan `success`**. Cualquier `failure`, `cancelled` o `skipped` ⇒ gate **rojo** (omisión nunca es verde — `ci-cd-devsecops` § 3). No se permite `continue-on-error` en ningún job requerido.
- **Hardening Report** (`ci-evidence/hardening-report.md`), escrito siempre (también en rojo):
  - Cabecera: SHA, evento, fecha, conclusión global.
  - Tabla: `Gate | Herramienta | Versión | Resultado | Métrica | Umbral | Evidencia (run/artefacto)` con una fila por job (tests, secretos, NuGet, SAST, mutación, contenedor, DAST, fuzz).
  - Bloque de deuda visible: hallazgos preexistentes (Semgrep baseline), WARN de Dockle, score de mutación en Fase A.
  - Se publica en el **job summary** (`$GITHUB_STEP_SUMMARY`) y como artefacto `hardening-report-<sha>` (retención 7 días).
- **Contracción con CD-1:** workflow `ci-1.yml` en rojo ⇒ `workflow_run` de `cd-deploy.yml` no cumple `conclusion == success` ⇒ no despliega (mecanismo existente, sin tocar).

## 7. Seguridad, permisos y costo

- **Sin secretos nuevos**: Semgrep, Stryker, Dockle, ZAP y RESTler son OSS y corren sin tokens; el `.env.ci` usa valores ficticios; ningún valor real de Supabase/S3/ AWS llega al runner (los runners de PR de forks no reciben secretos — ya no los hay).
- **Permisos**: se mantiene `permissions: contents: read`. Subir SARIF a code scanning exigiría `security-events: write` → decisión abierta § 11 (no en Fase A).
- **Costo $0** (DEC-015/018): repo público ⇒ Actions sin costo; imágenes locales; sin servicios de pago. Presupuesto de duración del run completo: objetivo **≤ 45 min de reloj** (paralelismo real; el cuello de botella previsto es `mutation`), con timeouts por job como red de seguridad.
- **Logs**: resúmenes sin fragmentos de código hallados, sin valores de `.env.ci` (imprimir solo claves, no valores) ni payloads de fuzz; artefactos con retención 7 días.

## 8. Criterios de aceptación (Given/When/Then)

- **DV-AC-01 (DV-R01/03):** Dado un PR con una suite roja o secretos detectados, cuando corre `quality-gate`, entonces el gate y el run completos quedan `failure` y CD-1 no se dispara.
- **DV-AC-02 (DV-R02):** Dado un PR que introduce un hallazgo `ERROR` nuevo de Semgrep en C#, cuando corre `sast`, entonces el job falla (con baseline: el mismo hallazgo preexistente en main **no** bloquea PRs que no lo tocan).
- **DV-AC-03 (DV-R03):** Dado un fallo de red al descargar `p/csharp` o la imagen Semgrep, cuando termina `sast`, entonces falla como indisponibilidad y **no** reporta «sin hallazgos».
- **DV-AC-04 (DV-R01):** Dado un `mutation-summary.json` con score bajo el piso de Fase A, cuando corre `mutation`, entonces el job falla; un error de la herramienta también falla; un score sobre el piso publica la métrica en el reporte sin bloquear por sí solo.
- **DV-AC-05 (DV-R01):** Dado que alguien elimina `USER app` del `Dockerfile`, cuando corre `container`, entonces Dockle marca `error` y el job falla; un `warn` queda en informe sin bloquear (Fase A).
- **DV-AC-06 (DV-R07):** Dado que la API no levanta en el runner (o `/health` no llega a 200 en 120 s), cuando corre `dast`, entonces falla con logs acotados del compose; con stack sano y ZAP detectando una alerta `WARN+`, también falla.
- **DV-AC-07 (DV-R01):** Dado RESTler que detecta respuestas 5xx en endpoints alcanzables, cuando termina `fuzz`, entonces falla; respuestas 401 sin sesión no cuentan; la omisión del job o de su reporte ⇒ rojo.
- **DV-AC-08 (DV-R01):** Dado `backend` en rojo, cuando terminan los demás jobs, entonces `quality-gate` **aún corre** (`if: always()`), genera el Hardening Report con el estado real de los 6 jobs y concluye `failure`.
- **DV-AC-09 (DV-R03):** Dado cualquier job en `cancelled` o `skipped`, cuando evalúa el gate, entonces concluye `failure` (nunca verde por omisión).
- **DV-AC-10 (DV-R01):** Dado un run completo en verde, cuando termina, entonces existe `hardening-report-<sha>` con las 8 filas (7 gates + conclusión) y el job summary visible en la UI del PR.
- **DV-AC-11 (DV-R05):** Dado el diff de implementación, cuando se revisa, entonces no añade secretos, ni `permissions` ampliadas, ni acciones sin pinear por SHA/digest.
- **DV-AC-12 (DV-R03):** Dado un PR solo de documentación, cuando corre CI-1, entonces los 6 jobs se ejecutan igualmente (sin filtros de rutas).

## 9. Activación, enmiendas y conciliación (al aprobar)

1. Aprobación de este spec por el propietario ⇒ ratificación formal de SAST bloqueante (DV-R02) y de Stryker/fuzzing (DV-R04).
2. Crear `plan.md` y `task.md` de esta carpeta y solo entonces modificar `.github/workflows/ci-1.yml`, añadir `.semgrep` ampliado, `stryker.json`, `docker-compose.ci.yml` y scripts de resumen.
3. **Enmienda de `AGENTS.md`** (en el PR de implementación): § 1.4 orden nuevo del pipeline (`… secret scan → SAST → mutation → container → dast → fuzz → quality gate → terraform (si aplica)`) y § 10.5 quitando Stryker/fuzzing de la lista de descartados (añadiendo el resto de descartes confirmados: Pact, NBomber, ChaosTest, SonarCloud, Trivy por ahora).
4. **Branch protection:** añadir `quality-gate` como required check (conservar `backend`); acción manual del propietario en GitHub.
5. **Conciliar `specs/cross-cutting/ci-cd-devsecops/spec.md`**: marcar como superados sus párrafos de «SAST piloto informativo» y «Stryker fuera de alcance», sin tocar sus requisitos de CI-1/CI-2/CD-1 no relacionados.
6. Primera ejecución en verde de todos los jobs como línea base; registrar scores iniciales (mutación) y deuda (Semgrep/WARN) en este spec antes de endurecer umbrales (Fase B).

## 10. Riesgos y limitaciones conocidas

- **Stryker + .NET 10:** puede no existir versión compatible aún; spike obligatorio antes de prometer el job (si no hay, gate rojo documentado, nunca verde falso).
- **Duración:** `mutation` y `dast` alargan el PR gate (objetivo ≤ 45 min); mitigación: paralelismo, presupuestos de tiempo acotados, `concurrency` ya existente.
- **DAST sin Supabase real:** no valida flujos autenticados; el 401 es el comportamiento esperado, no una pass.
- **RESTler en light fuzz:** sensibilidad limitada; los buckets 5xx son el criterio, no cobertura de código.
- **Dockle ≠ CVE:** no escanea vulnerabilidades de paquetes de la imagen; el SCA lo hace NuGetAudit (bloqueante). Candidato futuro: Trivy (spec aparte).
- **Falsos positivos de ZAP:** Fase A bloquea en `WARN+`; si el ruido inicial es alto, la revisión puede bajar el nivel vía este spec (nunca `continue-on-error`).
- **Frontera CI-2 (DV-R07):** el uso efímero de Mongo/Redis en PR debe mantenerse sin credenciales ni tocar la EC2; cualquier expansión exige CI-2 por SDD propio.

## 11. Decisiones abiertas (para la revisión del propietario)

| # | Decisión | Propuesta de esta spec |
|---|---|---|
| D1 | Piso de mutación Fase A | 50% provisional + ratchet en Fase B; ¿conforme? |
| D2 | Nivel de fallo Dockle | `error` bloquea, `warn` informa (Fase A) |
| D3 | Nivel de fallo ZAP | `WARN+` bloquea; ¿bajar a `FAIL` solo si hay ruido? |
| D4 | RESTler: presupuesto de fuzz | ~10 min en light mode; ¿subir a 20 si el run lo permite? |
| D5 | SARIF a GitHub code scanning | Fuera de Fase A (requiere `security-events: write`); ¿habilitar después? |
| D6 | Filtros de rutas para PRs documentales | No (conforme a `ci-cd-devsecops` § 3); ¿revisitar con datos de duración? |
| D7 | ZAP vía scripts del contenedor (red `host`) o action oficial pineada | Definir en `plan.md` con spike |
| D8 | Actualizar `AGENTS.md` § 1.4/§ 10.5 en el PR de implementación | Sí (requisito de § 9.3) |

## 12. Estado de aprobación

- [ ] **Aprobado por el propietario de producto** → habilita `plan.md`/`task.md` y la modificación de los YAML.
- [ ] Spike de compatibilidad Stryker/.NET 10 registrado en `task.md`.
- [ ] Decisiones D1–D8 cerradas en la revisión.
- [ ] Conciliación con `ci-cd-devsecops/spec.md` y `AGENTS.md` ejecutada en el PR de implementación.
