# plan.md — Diseño de CI/CD DevSecOps transversal · Backend UTTT-GO

> Versión documental **0.3.0** · 2026-11-04 · Estado: **CI-1 autorizada para implementar, ejecución remota pendiente**. Deriva de `spec.md` 0.3.0 (CI-R01..09; CI-AC-01..10), subordinada a `docs/constitution.md` v2.3 y `AGENTS.md`. TASK-AUTH-007/031 abiertos. Referencias globales en `docs/referencias-profesor-backend.md`.

## 1. Alcance y arquitectura de entrega

CI-1 de PR y push feature sin servicios de aplicación externos ni nube. La solución real es `UTTTGO.Backend.slnx` (`src/UTTTGO.Api/` + `tests/UtttGo.UnitTests/`, `UtttGo.IntegrationTests/`, `UtttGo.SecurityTests/`). Solo se prueba código local con dobles. Los 120/140/60 casos reportados son referencia histórica, no número fijo. Restore y escáneres pueden necesitar registros/advisories públicos; no acceden a servicios del proyecto.

Estructura autorizada de CI-1: `.github/workflows/ci-1.yml`, `scripts/ci/nuget_audit.py` y `.semgrep/ci-1.yaml` (regla piloto local), reportes efímeros como artefactos. No instalar `.specify/` ni trasladar `API_SDD/`, `UnitTest/`, Dockerfile, SQL o workflow AWS. IaC solo cuando exista `infra/terraform/` aprobado. `TASK-AUTH-031` sigue siendo cierre integral de fase 02, no una tarea competidora.

**Procedencia de herramientas (selección CI-1, no aprobación integral de versiones):** `actions/checkout@11d5960a326750d5838078e36cf38b85af677262`, `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` y `actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02` resuelven los tags v4 consultados en GitHub API; licencias MIT en sus repositorios. Gitleaks v8.30.1 MIT, [release oficial](https://github.com/gitleaks/gitleaks/releases/tag/v8.30.1) y SHA-256 Linux x64 `551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb` (digest declarado por API). Semgrep OSS v1.183.0 [LGPL-2.1 en repositorio](https://github.com/semgrep/semgrep/blob/v1.183.0/LICENSE), imagen Linux amd64 en Docker Hub fijada por digest `sha256:2e45517a85e13a9cca14a2a00df6bb48affd66b8ebfae1b0991cd57578362fb0`; uso separado de la aplicación, sin incorporar su código ni redistribuirlo. Compilación local con SDK .NET preview y Docker Engine indisponible: compatibilidad de Semgrep y SDK estable **pendiente de runner**.

### Matriz provisional de versiones **solo para identificar CI local**

Fuente: `docs/academic-requirements-phase-02.md` § «Matriz inicial y política de versiones», aprobada **individualmente**, con TASK-AUTH-007 aún abierto. Una ejecución de CI lleva SHA y run ID, **no** DEP ni versión de release nueva.

| Componente | Línea base ya documentada | Uso en CI-1 / límite |
|---|---|---|
| Contrato REST `/api/v1` | `1.0.0-rc.1` | Candidato documental; CI no aprueba fichas, DTO ni compatibilidad real. |
| Backend .NET 10 | `0.1.0` | Primer artefacto funcional **previsto**; CI compila SHA sin afirmar release `0.1.0` publicado. |
| Aplicación móvil | `0.1.0` | Prevista; su CI y build de tienda no se ejecutan ni se acreditan aquí. |
| Terraform | `0.1.0` | Previsto, archivos aún inexistentes; CI-1 no ejecuta plan/apply ni crea recursos. |
| Configuración `dev` / `staging` / `prod` | `0.1.0` **cada una** | Revisiones previstas separadas, sin entornos creados ni valores de secretos. |
| SDD transversal | `0.3.0` documental | Ratificación CI-1 limitada; independiente de versiones de artefactos; registrar SHA del SDD. |
| Workflow y ejecución CI-1 | **Sin SemVer propio aprobada** | Al implementarlo identificar archivo+SHA/versión de actions y GitHub run ID/resultado; no reutilizar `DEP-0001` (ejemplo ilustrativo de despliegue, ninguno realizado). |

No elevar números por la sola documentación/ejecución de CI. Versiones definitivas, política SemVer/DEP y compatibilidad móvil siguen sujetas a TASK-AUTH-007 para trabajo funcional/release y despliegues; no son prerequisito constitucional del workflow CI-1.

## 2. Secuencia propuesta de CI-1

| Orden (`AGENTS.md` § 1.4) | Acción prevista | Gate y evidencia |
|---|---|---|
| 1 | `pull_request` hacia `main` y `push` a `feature/**`; checkout con `contents: read`, runner Linux efímero + SDK .NET 10 estable y actions de procedencia/versiones revisadas. No inyectar secretos externos. | Identidad SHA/evento, toolchain y permisos; ningún paso de CD. |
| 2 | `python3 scripts/ci/nuget_audit.py`: restore de `UTTTGO.Backend.slnx` con NuGetAudit true, Mode=all, Level=low. | NU1903/4 bloquean; NU1901/2 informan; NU1900 se reintenta una vez y bloquea por indisponibilidad; otros fallos de restore bloquean. |
| 3 | `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`. | 0 cambios pendientes; si falla, PR rojo. |
| 4 | `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`. | 0 errores, build Release para los tres tests; no confundir aviso SDK preview local con objetivo estable de runner. |
| 5 | `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build` y reporte TRX cuando se implemente. | Suite ejecutada, total/fallidas/omitidas visibles; no omitir proyectos. |
| 6 | Igual para `tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj`. | Reporte marcado **integración local con dobles**, no Mongo/Redis/Supabase reales. |
| 7 | Igual para `tests/UtttGo.SecurityTests/UtttGo.SecurityTests.csproj`. | Reporte marcado **seguridad local con dobles**, no JWT/SAS/distribución reales. |
| 8 | Gitleaks OSS v8.30.1 (MIT), archivo oficial Linux x64 y digest SHA-256 fijado; commits desde `main` y árbol rastreado. | Hallazgo o error del escáner bloquea; salida cruda privada, resumen sin coincidencias. Base ausente bloquea. |
| 9 | Semgrep OSS v1.183.0, regla local C# de CORS en `.semgrep/ci-1.yaml`. | Piloto informativo; informe de código de salida y número de hallazgos sin fuente; fallo no equivale a pasada. |
| 10 | Solo si se modificó `infra/terraform/` **después de aprobarla**: `terraform fmt -check -recursive`, `terraform init -backend=false`, `terraform validate`. | Sin `apply`, estado real ni recursos en PR; cuando la ruta no existe, «no aplica», no «Terraform verde». |

**SCA:** el script de restore evalúa NU1900 (advisories inaccesibles), NU1901/2 (Low/Moderate) y NU1903/4 (High/Critical). `NuGetAuditMode=all` incluye transitivas; un resultado limpio depende de que la fuente de advisories esté disponible. No `tee`/`|| true` en gates. Probar clasificación y reintento localmente; ejecución del runner pendiente. Excepciones solo con issue y aprobación; SBOM/Dependabot posteriores, sin umbrales de cobertura inventados.

**Duplicados:** cancelar ejecuciones obsoletas dentro del mismo evento/rama o PR, sin cancelar PR por push. Una rama feature con PR abierto puede provocar dos ejecuciones; no omitir checks basados en una consulta adicional de permisos sin aprobarla.

## 3. Evidencias, calidad y seguridad del runner

- El artefacto `ci-1-backend-local-doubles` conserva SHA/evento/SDK, conteos de las tres suites extraídos de TRX (no TRX crudos), códigos NuGetAudit, estado Gitleaks y resumen Semgrep; no subir fuentes, datos de prueba, tokens ni coincidencias. Retención inicial de 7 días sujeta a política del repositorio. Gates obligatorios sin `continue-on-error`; Semgrep informativo identificado.
- No copiar el agregador de WebAPI que requiere jobs omitidos por eventos/filtros: los controles exigibles corren tanto en PR como en push feature no redundante; suite requerida `skipped` no puede dar verde. PR de forks con permisos mínimos; evitar `pull_request_target` ejecutando código no confiable con secretos. Solo `contents: read`; sin `id-token: write`, `packages: write` ni `pull-requests: write`.
- El escáner de secretos sobre el contenido del PR y los controles de logs/configuración no sustituyen revisión humana. Prueba de canario debe ser inocua y efímera; no guardar credenciales reales ni testear contra producción.
- Los PR documentales con SDD deben recibir revisión de precedencia/versiones/trazabilidad; un optimizador de rutas no puede saltar el gate documental. Verificadores derivados de [API_SDD `check_sdd.ps1`](https://github.com/edelomeza/API_SDD/blob/main/scripts/check_sdd.ps1) se implementarán solo si cubren estructura UTTT-GO, sin validar por simple coincidencia de texto una aprobación inexistente.
- Referencias técnicas: [API_SDD CI](https://github.com/edelomeza/API_SDD/blob/main/.github/workflows/ci-pr.yml) muestra secuencia sin CD; [WebAPI CI](https://github.com/edelomeza/WebAPIDevSecOpsScalling/blob/main/.github/workflows/ci-cd.yml) muestra reportes y gates por evento. No trasladar Semgrep `rules: []` ni Trivy `exit-code: '0'` como si bloquearan.

## 4. Etapas posteriores separadas

**CI-2 real no productiva (CI-R02/05, CI-AC-04):** SDD de cada módulo define consultas, índices, fallos y seguridad; preparar MongoDB 7, Redis 7 y, si aplica, Azurite en contenedores locales, sin Atlas ni Azure de producción. Supabase Auth y OTP/SMTP requieren estrategia de sandbox/cuentas autorizada o dobles claramente etiquetados; FCM y Content Safety se simulan en CI según AGENTS, no se llaman a producción. Pruebas en múltiples instancias y JWT/blacklist/rate limit fail-closed no se declaran verdes hasta verificarse realmente. Esta etapa exige archivos de entorno/test nuevos y aprobación, **no** forma parte de CI-1 documental.

**CD Azure posterior (CI-R03/05/07, CI-AC-06):** constitución ya resolvió Azure App Service y Terraform. Resolver primero viabilidad simultánea de dos instancias y costo cero, accesos/entornos, backend remoto de estado, OIDC/Key Vault, inventario de artefactos, health checks, versión y registro DEP, compatibilidad móvil y aprobador operativo real. Pipeline de despliegue separado con revisión de plan Terraform, aprobación antes de apply y smoke/rollback a combinación probada, sin `destroy` automático. AWS del profesor no es alternativa habilitada: exigiría enmienda constitucional del equipo. No diseñar hoy nombres de secretos, servicios ni costos ficticios.

**Móvil:** acordar interfaz documental de versión backend/API/móvil, evidencias de pruebas y compatibilidad, y quién aprueba promoción conjunta. El equipo móvil mantiene su CI/CD SDD separado; el reporte del backend no acredita su cumplimiento académico.

## 5. Verificación del diseño y riesgos abiertos

| Verificación al revisar este plan | Evidencia prevista / riesgo |
|---|---|
| `spec.md` CI-R01..09 ↔ `plan.md` §§ 1–4 ↔ `task.md` tareas CI-001.. | Autorización CI-1 no cierra TASK-AUTH-007/031 ni aprueba CD. |
| `python3 scripts/ci/nuget_audit.py`, formato, build Release y tres suites | Comprobación local separada de ejecución remota de GitHub Actions. |
| Pruebas CI-AC-01..10 contra workflow futuro | PR y push feature completos; canario secreto, advisory High/Critical y servicio caído no dan falso verde; sin credenciales ni ejecución antes de aprobar workflow. |
| Viabilidad económica, herramientas y licencias | Costo de runners/servicios y derechos de reutilización requieren constatación, no asumir gratuidad por usar GitHub Actions ni por README ajeno. |

### Aprobación

- [x] Primera CI local autorizada expresamente por responsable técnica tras conformidad visible de tres participantes; no atribuir unanimidad, roles ni fecha a la captura.
- [ ] Ejecución GitHub Actions y validación de escáneres en runner.
- [ ] TASK-AUTH-007/031, CI-2 y CD siguen abiertos.
