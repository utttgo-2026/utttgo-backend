# plan.md — Arquitectura base · Backend UTTT-GO
> Plan de ejecución para dejar el esqueleto del proyecto listo.  
> Alineado con `spec.md` de arquitectura base y `constitution.md` v1.0.  
> **No incluye tareas de funcionalidad específica.**

---

## Objetivo del plan

Tener el repositorio backend en un estado en el que cualquier miembro del equipo pueda:
1. Clonar el repositorio.
2. Copiar `.env.example` a `.env` y completar sus valores locales.
3. Ejecutar `docker-compose up` + `dotnet run` y obtener una API corriendo.
4. Ejecutar `dotnet test` y ver los proyectos de prueba compilados y listos.

---

## Fases y orden de ejecución

### FASE 0 — Preparación del repositorio
> Prerrequisito: ninguno. Bloquea todas las demás fases.

| Paso | Descripción | Responsable |
|---|---|---|
| P0-1 | Crear repositorio `utttgo-backend` en el proveedor elegido (GitHub/GitLab) | Equipo |
| P0-2 | Inicializar `.gitignore` para .NET (plantilla oficial de GitHub) | Equipo |
| P0-3 | Crear rama `main` protegida y `develop` como rama de trabajo | Equipo |
| P0-4 | Agregar `README.md` con descripción del proyecto y requisitos del entorno | AGENT-BE-01 |
| P0-5 | Agregar `.env.example` con todas las variables sin valores reales | AGENT-BE-01 |

---

### FASE 1 — Estructura del proyecto .NET
> Prerrequisito: P0 completo.

| Paso | Descripción | Responsable |
|---|---|---|
| P1-1 | Crear solución `utttgo-backend.sln` | AGENT-BE-01 |
| P1-2 | Crear proyecto `UtttGo.Api` (ASP.NET Core 8 Web API) | AGENT-BE-01 |
| P1-3 | Crear proyectos de prueba vacíos: `UtttGo.UnitTests`, `UtttGo.IntegrationTests`, `UtttGo.SecurityTests` | AGENT-BE-05 |
| P1-4 | Agregar referencias de proyecto entre Api y proyectos de prueba | AGENT-BE-01 |
| P1-5 | Crear estructura de carpetas según `spec.md § 1` | AGENT-BE-01 |
| P1-6 | Verificar que `dotnet build` pasa sin errores | AGENT-BE-01 |

---

### FASE 2 — Configuración y dependencias NuGet
> Prerrequisito: P1 completo.

| Paso | Descripción | NuGet Package |
|---|---|---|
| P2-1 | Agregar cliente MongoDB | `MongoDB.Driver` |
| P2-2 | Agregar cliente Redis | `StackExchange.Redis` |
| P2-3 | Agregar autenticación JWT | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| P2-4 | Agregar rate limiting (incluido en .NET 7+) | `System.Threading.RateLimiting` (built-in) |
| P2-5 | Agregar validación | `FluentValidation.AspNetCore` |
| P2-6 | Agregar logging estructurado | `Serilog.AspNetCore` + sinks según decisión |
| P2-7 | Agregar correlationId | `CorrelationId` (NuGet) o middleware propio |
| P2-8 | Agregar health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` + extensiones de Mongo y Redis |

---

### FASE 3 — Configuración de entorno y servicios base
> Prerrequisito: P2 completo.

| Paso | Descripción | Agente |
|---|---|---|
| P3-1 | Crear clases de opciones tipadas (`MongoDbOptions`, `RedisOptions`, `SupabaseOptions`, `StorageOptions`) | AGENT-BE-01 |
| P3-2 | Registrar opciones con `services.Configure<T>()` desde variables de entorno | AGENT-BE-01 |
| P3-3 | Registrar `IMongoClient` y `IMongoDatabase` como singleton en DI | AGENT-BE-03 |
| P3-4 | Registrar `IConnectionMultiplexer` de Redis como singleton en DI | AGENT-BE-03 |
| P3-5 | Configurar `AddJwtBearer` con parámetros de Supabase (issuer, audience, secret) | AGENT-BE-04 |
| P3-6 | Configurar CORS desde variable `API_CORS_ALLOWED_ORIGINS` | AGENT-BE-04 |
| P3-7 | Configurar Rate Limiting: políticas `AuthPolicy` y `GeneralPolicy` | AGENT-BE-04 |
| P3-8 | Configurar Serilog con sink de consola JSON y `correlationId` en cada log | AGENT-BE-06 |

---

### FASE 4 — Middleware base
> Prerrequisito: P3 completo.

| Paso | Descripción | Agente |
|---|---|---|
| P4-1 | Implementar middleware `CorrelationIdMiddleware` (genera o propaga `X-Correlation-Id`) | AGENT-BE-01 |
| P4-2 | Implementar middleware `GlobalExceptionHandlerMiddleware` (captura excepciones, retorna contrato de error estándar) | AGENT-BE-01 |
| P4-3 | Registrar middleware en `Program.cs` en el orden exacto de `spec.md § 3` | AGENT-BE-01 |
| P4-4 | Agregar endpoint `GET /health` con checks de MongoDB y Redis | AGENT-BE-01 |

---

### FASE 5 — Docker Compose y entorno local
> Prerrequisito: P3 completo (puede ejecutarse en paralelo con P4).

| Paso | Descripción | Agente |
|---|---|---|
| P5-1 | Crear `docker-compose.yml` con servicios MongoDB 7 y Redis 7 | AGENT-BE-01 |
| P5-2 | Crear `docker-compose.override.yml` (local, no comitear si tiene secretos) | Equipo |
| P5-3 | Verificar `docker-compose up` levanta ambos servicios sin errores | Equipo |
| P5-4 | Verificar conexión de la API a MongoDB y Redis al iniciar | AGENT-BE-03 |

---

### FASE 6 — Verificación y cierre
> Prerrequisito: P4 y P5 completos.

| Paso | Descripción | Criterio |
|---|---|---|
| P6-1 | Ejecutar `dotnet build` — sin errores ni warnings críticos | Build verde |
| P6-2 | Ejecutar `dotnet run` — `GET /health` retorna `200` con estado de Mongo y Redis | Health check verde |
| P6-3 | Verificar que una petición sin token a `/api/v1/` retorna `401` con contrato de error | Seguridad base |
| P6-4 | Ejecutar `dotnet test` — proyectos de prueba compilan y tests vacíos pasan | Test runner verde |
| P6-5 | Revisar que `.env.example` tiene todas las variables del `spec.md § 2` | Revisión manual |
| P6-6 | Revisar que no hay secretos en ningún archivo del repositorio | AGENT-BE-04 |

---

## Dependencias entre fases

```
P0 → P1 → P2 → P3 → P4 → P6
                   ↘ P5 ↗
```

P5 puede ejecutarse en paralelo con P4 una vez P3 esté completo.

---

## Estimación (referencia, no compromiso)

| Fase | Esfuerzo estimado |
|---|---|
| FASE 0 | 1 hora |
| FASE 1 | 1 hora |
| FASE 2 | 1 hora |
| FASE 3 | 2 horas |
| FASE 4 | 2 horas |
| FASE 5 | 1 hora |
| FASE 6 | 1 hora |
| **Total** | **~9 horas** |

---

## Lo que este plan NO incluye

- Implementación de ningún endpoint funcional.
- Seed de datos de categorías u otros catálogos.
- Configuración de CI/CD.
- Despliegue a ningún entorno cloud.

*Esas fases estarán en los `plan.md` de cada módulo funcional.*
