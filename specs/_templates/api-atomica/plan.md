# {{API_ID}} — {{NOMBRE}} · plan

> PLANTILLA, NO APROBADA. Sustituir `{{...}}` antes de usar.

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Alcance `{{FRONTERA}}`; no aprobar contratos por código existente.

## Arquitectura existente

`{{MODULO_DUENO_Y_ESTADO_IMPLEMENTADO_O_NO}}`; no crear segunda colección/repo de otro módulo.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| {{DTO_CONTROLADOR_SERVICIO_REPOSITORIO_ADAPTADOR}} | {{RUTA_O_PENDIENTE}} | {{ROL}} |

## Secuencia técnica

`{{ORDEN_DE_VALIDACION_GATE_PROVEEDOR_PERSISTENCIA_RESPUESTA}}`; señalar pasos candidatos frente a aprobados y fallos parciales.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar solo los pertinentes, sin duplicar reglas.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| {{ESC-01}} | {{ARCHIVO_O_NINGUNO}} | {{CAMBIO_SOLO_TRAS_AUTORIZACION}} |

## Orden de implementación

`{{ORDEN_DEV_CON_DEPENDENCIAS}}`: verificar si ya existe antes de crear tarea de código; no reimplementar lo probado al nivel requerido.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| {{ID}} | {{DEV_ID}} | {{ACTA_Y_CONDICION}} |

## Estrategia de pruebas

`{{UNITARIAS_INTEGRACION_SEGURIDAD_Y_ENTORNO}}`; dobles ≠ integración externa; no usar producción. Indicar comandos/recuento/fecha, fallos de dependencia y casos negativos.

## Seguridad, errores y observabilidad

`{{JWT_ROL_PROPIEDAD_LIMITES_FAIL_CLOSED_ERROR_CORRELACION_LOGS}}` según [contratos compartidos](../../bloque-1-arquitectura/contratos-transversales/spec.md); no cambiar middleware sin autorización.

## Criterios de cierre técnico

`{{RESULTADO_MEDIBLE_POR_ESCENARIO}}`: build Release, format, suites pertinentes y evidencia del entorno requerido, sin secretos; aprobación documental y cierre TASK histórico separados.
