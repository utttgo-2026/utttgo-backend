# {{API_ID}} — {{NOMBRE}} · task

> PLANTILLA, NO APROBADA. Sustituir `{{...}}`; los IDs DEV no reemplazan TASK históricos.

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Si la tarea afecta otra API, consignar dependencia, **no** implementarla aquí.

## Tareas ejecutables sin cambiar contrato

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| {{DEV-ID-01}} · {{TIPO_OBJETIVO}} | {{RF_ESC_ARCHIVOS}} | {{DEP_ACCION_RESULTADO_COMANDO_EVIDENCIA}} | READY |

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| {{DEV-ID-02}} · {{TIPO_OBJETIVO}} | {{RF_ESC_ARCHIVOS}} | {{DECISION_EXACTA_DEP_ACCION_RESULTADO_COMANDO_EVIDENCIA}} | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas y [guía](../../../docs/sdd-guia-api-atomica.md). Comprobar Git, elegir solo tarea READY con dependencias y autorización cumplidas; consultar código vigente. Ejecutar comandos definidos en la tarea en entorno no productivo, registrar fecha/resultado/evidencia sanitizada; no cambiar otras APIs ni cerrar gates por inferencia.

### Control de ejecución

Una fila **por ID detallado**; `Cambio técnico=SI` incluye editar tests funcionales e integración nueva. `Autorización` para READY técnico debe identificar **acta aprobada de ficha y gates cerrados**, no «pendiente», enlace a código ni decisión funcional parcial. DONE requiere evidencia específica distinta de `PENDIENTE`.

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| {{DEV-ID-01}} | {{TIPO}} | READY | NO | N/A: lectura | - | PENDIENTE |
| {{DEV-ID-02}} | {{TIPO}} | BLOCKED | SI | Pendiente: {{DECISION_ORIGINAL_Y_GATE}} | {{DEV-ID-01}} | PENDIENTE |
