# task.md — Arquitectura base · Backend UTTT-GO
> Lista de tareas atómicas derivadas de `plan.md`.  
> Cada tarea es ejecutable por un solo agente o miembro del equipo, tiene criterio de "hecho" claro y no solapa con otras.  
> Alineado con `constitution.md` v1.0. **No ejecutar aún — solo definición.**

---

## Estado de tareas

| Símbolo | Significado |
|---|---|
| `[ ]` | Pendiente |
| `[~]` | En progreso |
| `[x]` | Completada |
| `[!]` | Bloqueada |

---

## FASE 0 — Preparación del repositorio

- [ ] **TASK-BE-001** · Crear repositorio `utttgo-backend`
  - Acción: Crear el repositorio en GitHub/GitLab con visibilidad acorde al equipo.
  - Criterio de hecho: Repositorio existe, equipo tiene acceso de escritura.
  - Agente: Equipo completo.

- [ ] **TASK-BE-002** · Inicializar `.gitignore` para .NET
  - Acción: Usar la plantilla oficial de GitHub para Visual Studio / .NET. Agregar reglas para `.env`, `*.user`, `bin/`, `obj/`.
  - Criterio de hecho: `.gitignore` comiteado; `git status` no muestra archivos de build ni `.env`.
  - Agente: AGENT-BE-01.

- [ ] **TASK-BE-003** · Configurar ramas `main` y `develop`
  - Acción: Crear rama `develop`; proteger `main` (requiere PR y review).
  - Criterio de hecho: `main` protegida; `develop` existe como rama base de trabajo.
  - Agente: Equipo.

- [ ] **TASK-BE-004** · Crear `README.md` inicial
  - Acción: Documentar: propósito del proyecto, requisitos del entorno (SDK .NET 8, Docker, herramientas), instrucciones de setup en 5 pasos.
  - Criterio de hecho: Un nuevo miembro puede seguir el README sin preguntar nada adicional.
  - Agente: AGENT-BE-01.

- [ ] **TASK-BE-005** · Crear `.env.example`
  - Acción: Listar todas las variables del `spec.md § 2` con nombres exactos y valores de ejemplo ficticios (`<valor-aqui>`). Sin valores reales.
  - Criterio de hecho: Archivo comiteado; ninguna variable real en el archivo.
  - Agente: AGENT-BE-01.

---

## FASE 1 — Estructura del proyecto .NET

- [ ] **TASK-BE-006** · Crear solución y proyecto principal
  - Acción: `dotnet new sln -n utttgo-backend` + `dotnet new webapi -n UtttGo.Api`.
  - Criterio de hecho: `dotnet build` pasa sin errores.
  - Agente: AGENT-BE-01.

- [ ] **TASK-BE-007** · Crear proyectos de prueba
  - Acción: `dotnet new xunit -n UtttGo.UnitTests` + `UtttGo.IntegrationTests` + `UtttGo.SecurityTests`. Agregar todos a la solución.
  - Criterio de hecho: `dotnet test` ejecuta y reporta 0 tests, 0 errores.
  - Agente: AGENT-BE-05.

- [ ] **TASK-BE-008** · Crear estructura de carpetas
  - Acción: Crear carpetas `Controllers/`, `Services/`, `Repositories/`, `Models/`, `DTOs/`, `Middleware/`, `Validators/`, `Configuration/`, `Extensions/` dentro de `UtttGo.Api`. Agregar `.gitkeep` en cada una.
  - Criterio de hecho: Estructura de carpetas visible en el repositorio.
  - Agente: AGENT-BE-01.

---

## FASE 2 — Dependencias NuGet

- [ ] **TASK-BE-009** · Agregar paquetes NuGet base
  - Acción: Instalar los paquetes listados en `plan.md FASE 2` en los proyectos correspondientes.
  - Criterio de hecho: `dotnet restore` pasa; `dotnet build` pasa sin conflictos de versión.
  - Agente: AGENT-BE-01.
  - Nota: Registrar versiones exactas en `memoria.md`.

---

## FASE 3 — Configuración de servicios base

- [ ] **TASK-BE-010** · Crear clases de opciones tipadas
  - Acción: Crear `MongoDbOptions`, `RedisOptions`, `SupabaseOptions`, `StorageOptions` en `Configuration/`. Registrar con `services.Configure<T>(configuration.GetSection("..."))`.
  - Criterio de hecho: Aplicación inicia sin errores con variables de entorno del `.env.example` completadas.
  - Agente: AGENT-BE-01.

- [ ] **TASK-BE-011** · Registrar MongoDB en DI
  - Acción: Crear `Extensions/MongoDbExtensions.cs` con método `AddMongoDb()` que registra `IMongoClient` (singleton) e `IMongoDatabase` (singleton).
  - Criterio de hecho: Un servicio puede recibir `IMongoDatabase` por inyección y conectarse en `GET /health`.
  - Agente: AGENT-BE-03.

- [ ] **TASK-BE-012** · Registrar Redis en DI
  - Acción: Crear `Extensions/RedisExtensions.cs` con método `AddRedis()` que registra `IConnectionMultiplexer` (singleton).
  - Criterio de hecho: Un servicio puede recibir `IConnectionMultiplexer` y ejecutar un `PING` exitoso en `GET /health`.
  - Agente: AGENT-BE-03.

- [ ] **TASK-BE-013** · Configurar autenticación JWT (Supabase)
  - Acción: Configurar `AddAuthentication().AddJwtBearer()` con validación de firma (`SUPABASE_JWT_SECRET`), issuer, audience y lifetime.
  - Criterio de hecho: Una petición con token válido pasa la autenticación; una con token expirado retorna `401`.
  - Agente: AGENT-BE-04.
  - Seguridad: No loguear el secret. Nunca hardcodearlo.

- [ ] **TASK-BE-014** · Configurar CORS
  - Acción: Leer `API_CORS_ALLOWED_ORIGINS` del entorno. Registrar política nombrada `UtttGoPolicy` con origins explícitos.
  - Criterio de hecho: Request de origin permitido pasa; origin no listado recibe `403`/`blocked`.
  - Agente: AGENT-BE-04.

- [ ] **TASK-BE-015** · Configurar Rate Limiting
  - Acción: Crear política `AuthPolicy` (10 req/min) y `GeneralPolicy` (60 req/min) usando `AddRateLimiter` de .NET 8.
  - Criterio de hecho: Superar el límite en `/auth/*` retorna `429`.
  - Agente: AGENT-BE-04.

- [ ] **TASK-BE-016** · Configurar Serilog
  - Acción: Configurar Serilog con sink de consola en formato JSON estructurado. Incluir `correlationId` en cada evento de log.
  - Criterio de hecho: Los logs aparecen en formato JSON al ejecutar `dotnet run`. El `correlationId` aparece en cada línea de log de un request.
  - Agente: AGENT-BE-06.

---

## FASE 4 — Middleware base

- [ ] **TASK-BE-017** · Implementar CorrelationIdMiddleware
  - Acción: Si la request trae `X-Correlation-Id`, usarlo. Si no, generar un `Guid`. Propagarlo en la respuesta y en el contexto de Serilog.
  - Criterio de hecho: Toda respuesta incluye el header `X-Correlation-Id`. El mismo ID aparece en todos los logs del request.
  - Agente: AGENT-BE-01.

- [ ] **TASK-BE-018** · Implementar GlobalExceptionHandlerMiddleware
  - Acción: Capturar cualquier excepción no manejada y retornar el contrato de error estándar (`spec.md § 4`) con el `correlationId` del request.
  - Criterio de hecho: Una excepción lanzada en un controller retorna `500` con el contrato JSON, no el HTML de Kestrel. El `correlationId` está en la respuesta.
  - Agente: AGENT-BE-01.
  - Seguridad: No incluir stack traces en la respuesta en producción.

- [ ] **TASK-BE-019** · Registrar middleware en Program.cs en orden correcto
  - Acción: Registrar todos los middlewares en el orden exacto de `spec.md § 3`.
  - Criterio de hecho: El orden es verificable leyendo `Program.cs`. Un cambio de orden requiere aprobación de AGENT-BE-01.
  - Agente: AGENT-BE-01.

- [ ] **TASK-BE-020** · Crear endpoint GET /health
  - Acción: Registrar health check de MongoDB (ping) y Redis (ping). Exponer en `GET /health` con respuesta JSON de estado.
  - Criterio de hecho: `GET /health` retorna `200` con estado `Healthy` de ambas dependencias cuando están disponibles. Retorna `503` si alguna no responde.
  - Agente: AGENT-BE-01.

---

## FASE 5 — Docker Compose

- [ ] **TASK-BE-021** · Crear docker-compose.yml
  - Acción: Incluir servicios `mongodb:7` y `redis:7-alpine` con configuración del `spec.md § 8`.
  - Criterio de hecho: `docker-compose up` levanta ambos servicios. MongoDB acepta conexiones en `27017`. Redis acepta conexiones en `6379`.
  - Agente: AGENT-BE-01.

- [ ] **TASK-BE-022** · Crear docker-compose.override.yml
  - Acción: Archivo local para credenciales de desarrollo. Agregar al `.gitignore`.
  - Criterio de hecho: Archivo existe localmente; no aparece en `git status`.
  - Agente: Equipo.

---

## FASE 6 — Verificación final

- [ ] **TASK-BE-023** · Ejecutar checklist de aceptación completo
  - Acción: Verificar cada criterio del `spec.md § 9` y marcar como cumplido.
  - Criterio de hecho: Todos los criterios marcados como `[x]` en `spec.md`.
  - Agente: AGENT-BE-04 + AGENT-BE-05.

- [ ] **TASK-BE-024** · Revisar que no hay secretos en el repositorio
  - Acción: Ejecutar `git log --all --full-diff -p | grep -i "password\|secret\|key\|token"` y revisar manualmente archivos de configuración.
  - Criterio de hecho: Ningún resultado positivo. Si se encuentra uno, se elimina del historial con `git filter-branch` o `BFG`.
  - Agente: AGENT-BE-04.

---

## Resumen de tareas

| ID | Fase | Estado |
|---|---|---|
| TASK-BE-001 a 005 | Repositorio | `[ ]` |
| TASK-BE-006 a 008 | Proyecto .NET | `[ ]` |
| TASK-BE-009 | NuGet | `[ ]` |
| TASK-BE-010 a 016 | Servicios base | `[ ]` |
| TASK-BE-017 a 020 | Middleware | `[ ]` |
| TASK-BE-021 a 022 | Docker | `[ ]` |
| TASK-BE-023 a 024 | Verificación | `[ ]` |

**Total: 24 tareas · 0 completadas · 0 bloqueadas**
