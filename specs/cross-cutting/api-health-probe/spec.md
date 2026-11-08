# spec.md — Sonda operativa de salud `GET /health` · Backend UTTT-GO

> Versión documental **1.1** · 2026-11-07 · Estado: **aprobada** por el propietario de producto (solicitud explícita: implementar `GET /health` y el healthcheck de Nginx con ficha SDD). v1.1 añade HP-11 (la sonda comprueba además el acceso del usuario a la base configurada) tras detectar en la EC2 que un `ping` puede ser correcto mientras las consultas reales fallan por falta de permisos sobre esa base.
> Autoridad: `docs/constitution.md` v2.4 (§ 8 Observabilidad: «`GET /health` verifica MongoDB y Redis; devuelve `503` si una dependencia crítica falla»; § 6.1 degradación de Redis; § 5.3 Redis) → `AGENTS.md` v2.3 (§ 4, § 8, § 11) → este `spec.md` → `plan.md` → `task.md`.

## 1. Objetivo y alcance

Exponer una **sonda operativa** `GET /health` que verifique las dependencias críticas del backend (**MongoDB** y **Redis**) y que sirva como criterio real de disponibilidad para el despliegue en la EC2, corrigiendo el falso positivo actual: el healthcheck de Nginx consulta `http://127.0.0.1/`, ruta que **no existe** en la API, por lo que el contenedor queda `unhealthy` aunque el tráfico funcione.

Alcance de esta spec: el endpoint de sonda, su contrato, su límite de uso, el healthcheck del balanceador y la evidencia del pipeline de despliegue. **No** incluye métricas, trazas, alertas, `/health/live` separado ni la implementación de los módulos de negocio pendientes (productos, pedidos, chat, imágenes, push).

## 2. Requisitos

| ID | Requisito | Verificación |
|---|---|---|
| HP-01 | `GET /health` responde `200` con el estado de MongoDB y Redis cuando ambas dependencias están operativas | Test de integración con dobles que responden |
| HP-02 | `GET /health` responde `503` si MongoDB **o** Redis fallan, con el contrato de error único (`correlationId`, `status`, `code`, `message`) | Test de integración con dobles que fallan |
| HP-03 | La respuesta **no** expone credenciales, URIs de conexión, mensajes de excepción ni detalles del proveedor: solo nombre de dependencia y estado | Test de seguridad que busca cadenas sensibles en el cuerpo |
| HP-04 | La sonda es **pública y sin autenticación** (la usan el balanceador, el CD y el monitoreo) y **no** devuelve datos de negocio ni de usuario | Test de integración y revisión del DTO |
| HP-05 | La sonda aplica **rate limiting distribuido con Redis** (política `health`), con respuesta `429` y `Retry-After`; si Redis no responde, la sonda falla cerrada (`503`) | Test de integración con límite y con Redis caído |
| HP-06 | La verificación de dependencias usa **tiempos de espera acotados** (≤ 5 s por dependencia) para no bloquear el balanceador ni el pipeline | Test unitario del tiempo de espera |
| HP-07 | El **healthcheck del contenedor Nginx** consulta una ruta real de la API (`/health`) y refleja el estado real de las dependencias | `docker compose ps` tras el despliegue; `nginx` `healthy` solo con dependencias sanas |
| HP-08 | El **healthcheck del contenedor de la API** se mantiene como sonda de **liveness** (TCP al puerto 8080): una caída de Atlas no debe reiniciar las réplicas ni bloquear el arranque de Nginx | `docker-compose.yml`; `depends_on` de Nginx intacto |
| HP-09 | El despliegue CD-1 registra en su evidencia el resultado de `GET /health` sin abortar el despliegue si la dependencia está caída | Script de CD-1 y artefacto `cd-deploy.txt` |
| HP-10 | La sonda **no** requiere ni habilita endpoint de negocio nuevo, ni cambia el orden del middleware de `Program.cs` | Revisión de `Program.cs` |
| HP-11 | La sonda comprueba, además del `ping`, que el usuario de MongoDB tiene **acceso a la base configurada** (`MONGODB_DATABASE`): un `ping` correcto no implica permisos sobre esa base, y sin ellos la API responde `503` en sus endpoints de datos aunque la conexión funcione | Clasificación del código de comando `13` (no autorizado) como `authentication_failed`; test unitario de `ClassifyCommandCode` |

## 3. Contrato

### 3.1 Respuesta satisfactoria (`200`)

```json
{
  "status": "Healthy",
  "checks": { "mongodb": "Healthy", "redis": "Healthy" }
}
```

### 3.2 Respuesta con dependencia caída (`503`)

Se aplica el **contrato de error único** de la constitución:

```json
{
  "correlationId": "<id-correlacion>",
  "status": 503,
  "code": "DEPENDENCY_UNAVAILABLE",
  "message": "Servicio temporalmente no disponible."
}
```

El detalle de qué dependencia falló (y su categoría: `unreachable_or_timeout`, `authentication_failed`, `invalid_configuration`) queda **solo en los logs del servidor**, correlacionados por `correlationId`; nunca en el cuerpo de la respuesta.

### 3.3 Límite de uso (`429`)

```json
{
  "correlationId": "<id-correlacion>",
  "status": 429,
  "code": "RATE_LIMITED",
  "message": "Intenta de nuevo más tarde."
}
```

## 4. Límites y exclusiones

- **No** es un endpoint de negocio: no vive bajo `/api/v1`, no expone entidades ni devuelve identificadores de usuario.
- **No** introduce SDKs nuevos: se reutilizan `MongoDB.Driver` y `StackExchange.Redis`, ya presentes en el stack aprobado.
- **No** cambia el orden del middleware de `Program.cs`; la sonda se registra como health check del framework y se expone por el mismo enrutador de controladores.
- **No** reemplaza la verificación de arranque: el healthcheck de la API sigue siendo de liveness (HP-08).
- **No** se loguean URIs de conexión, credenciales, mensajes de excepción del proveedor ni `jti`.

## 5. Criterios de aceptación

| ID | Criterio |
|---|---|
| HP-AC-01 | Con MongoDB y Redis simulados como operativos, `GET /health` devuelve `200` y el cuerpo solo contiene `status` y `checks` con ambos en `Healthy`. |
| HP-AC-02 | Con MongoDB caído, la sonda devuelve `503`, `code: DEPENDENCY_UNAVAILABLE` y `correlationId` presente. |
| HP-AC-03 | Con Redis caído, la sonda devuelve `503` con el mismo contrato. |
| HP-AC-04 | El cuerpo de la sonda nunca contiene la URI de Mongo, la contraseña, el mensaje de excepción ni el texto `internal-secret` usado por los dobles de prueba. |
| HP-AC-05 | Al superar el límite de la política `health`, la sonda devuelve `429` con cabecera `Retry-After`. |
| HP-AC-06 | Con la aplicación sin configurar (sin `MONGODB_URI` ni `REDIS_URL`), la sonda devuelve `503` (falla cerrada) y no `500`. |
| HP-AC-07 | `docker compose config` es válido y el healthcheck de Nginx apunta a `/health`; el de la API sigue siendo TCP al puerto 8080. |
| HP-AC-08 | Con el stack local levantado (Mongo y Redis del perfil `local`), `GET /health` a través de Nginx devuelve `200`; al detener Mongo, devuelve `503`. |
| HP-AC-09 | El script de CD-1 registra el código HTTP de `GET /health` en la evidencia y no falla el despliegue por una dependencia caída. |
| HP-AC-10 | Build Release sin errores, `dotnet format` sin diferencias y suites unitaria, de integración y de seguridad en verde. |
| HP-AC-11 | El pipeline de CD-1 registra el estado de `/health` en la evidencia **sin** usar variables del runner dentro de la sesión SSH (`GITHUB_OUTPUT` no existe en la instancia) y sin abortar el paso si la sonda no responde. | `actionlint` sin errores; `bash -n` del script remoto; simulación del bloque del runner con `GITHUB_OUTPUT` local (valor `503` registrado y `000` cuando SSH falla) |

## 6. Riesgos

| Riesgo | Mitigación |
|---|---|
| La sonda pública permite enumerar el estado de las dependencias | Solo expone nombres de dependencia y estado; el detalle sensible queda en logs; rate limit distribuido y política de caché deshabilitada |
| Un ataque de fuerza contra la sonda satura Mongo/Redis | Límite distribuido por IP (política `health`), tiempos de espera acotados y sin efectos secundarios |
| `UseHttpsRedirection()` sin puerto HTTPS dentro del contenedor | La sonda no redirige: en el contenedor no hay puerto HTTPS configurado y el aviso ya conocido del middleware lo documenta |
| Nginx `unhealthy` por una caída de Atlas | Es el comportamiento correcto (readiness real); el liveness de la API y el reinicio de réplicas no dependen de Atlas |
| El rate limit usa la IP de la conexión, no `X-Forwarded-For` | La política existente lo asume así; se documenta que, detrás del balanceador, la partición es la IP del proxy |
| Deriva entre el healthcheck de Compose y la sonda | Ambos se derivan de esta spec y se verifican en el mismo ciclo de despliegue |

## 7. Aprobación

- Sonda operativa `GET /health`: aprobada por el propietario de producto el 2026-11-07 (petición explícita de implementación).
- Healthcheck del balanceador contra ruta real: aprobado por el propietario de producto el 2026-11-07.
- Implementación: AGENT-BE-01 con apoyo de AGENT-BE-05 (pruebas) y AGENT-BE-04 (revisión de seguridad).
