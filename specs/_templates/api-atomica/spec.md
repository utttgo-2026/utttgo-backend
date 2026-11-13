# {{API_ID}} — {{NOMBRE}} · spec

> PLANTILLA, NO APROBADA. Sustituir cada `{{MARCADOR}}`, eliminar ejemplos y citar fuentes antes de validar.

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: DOCUMENTADA

## Identificación y autoridad

ID `{{API_ID}}`; método/ruta `{{METODO_RUTA}}` según inventario o decisión nueva `{{FUENTE_RUTA}}`. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Si fuente no localizada: «pendiente de fuente», no inventar ruta.

## Estado de aprobación

`{{ALCANCE_APROBADO_Y_AUTORIDAD}}`. Una aprobación parcial no aprueba DTO/HTTP íntegro; estado operativo independiente del estado de contrato.

## Responsabilidad única

`{{EFECTO_DE_ESTA_API}}`; dependencias de otras fichas `{{DEPENDENCIAS_EXTERNAS}}` sin implementar su funcionalidad aquí.

## Fuentes consultadas

| Fuente (enlace local) | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| {{FUENTE_ENLACE}} | {{RF_AC_O_DECISION}} | {{ALCANCE}} |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| {{REGLA_O_NINGUNA}} | {{FUENTE_O_PENDIENTE}} | {{PARCIALIDAD}} |

## Comportamiento implementado candidato

`{{CODIGO_REAL_O_NO_IMPLEMENTADO}}`. Código presente ≠ aprobación; distinguir endpoint, adaptadores y dobles.

## Funcionalidad requerida faltante

`{{REQUISITO_FALTANTE_O_NINGUNO}}` y ficha dueña; no adjudicar a esta ruta OTP, logout, recuperación u otra función.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta, auth, JSON/campos obligatorios y opcionales, HTTP/DTO/errores | {{APROBADO}} | {{CANDIDATO}} | {{PENDIENTE}} |

## Seguridad y validaciones

`{{REGLAS_Y_FUENTES}}`: normalización, propiedad, límites, efectos/persistencia, fallos y secretos. Enlazar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) cuando apliquen; no copiar su contenido.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado, etiquetar candidato si no aprobado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| {{ESC-01}} | {{PRECONDICION}} | {{ACCION}} | {{RESULTADO}} | {{RF_AC}} | {{PRUEBA}} | {{APROBADO_CANDIDATO_BLOQUEADO}} |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| {{RF_RNF_AC_T_AUTH_TASK}} | {{FUENTE}} | {{ESC-01}} | {{DEV-ID_O_EXTERNA}} |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| {{ESC-01}} | {{IMPLEMENTADO_PROBADO / SIN_EVIDENCIA / DISCREPANCIA_RATIFICADA / NO_IMPLEMENTADO / OTRA_FICHA / BLOQUEADO}} | {{QUE_FALTA_Y_NIVEL_DE_PRUEBA}} | {{DEV_ID_O_DECISION}} |

## Evidencia con nivel y fecha

| Fecha | Nivel (documental/dobles/local real/externa) | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| {{FECHA_O_PENDIENTE}} | {{NIVEL}} | {{FUENTE}} | {{LIMITACION}} |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas (si hay) | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| {{ID_O_NINGUNO}} | {{FUENTE}} | {{ALTERNATIVAS}} | {{IMPACTO}} | {{APROBADOR_Y_EVIDENCIA}} |

## Criterios de preparación para desarrollo

Declarar el alcance técnico `{{ALCANCE}}` y [estado operativo](../../../docs/sdd-guia-api-atomica.md#estados-operativos). `READY_FOR_IMPLEMENTATION` requiere tareas técnicas READY con contrato, precondiciones y autorización **identificable**; solo lectura/QA/documentación no basta. `VERIFIED` se asigna a escenarios con evidencia específica, no a la ficha por presencia de ruta. Faltantes: `{{GATES_Y_EVIDENCIA}}`.
