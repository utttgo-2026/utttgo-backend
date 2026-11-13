# Guía de una pasada para fichas SDD atómicas

> Procedimiento documental; **no aprueba contratos ni habilita programación**. Fuente superior: [constitución v2.6](constitution.md); conservar [decisiones históricas](decisiones-pendientes-fase-02.md), [trazabilidad](traceability.md) y [migración](migracion-sdd.md). Plantillas: [`spec`](../specs/_templates/api-atomica/spec.md), [`plan`](../specs/_templates/api-atomica/plan.md), [`task`](../specs/_templates/api-atomica/task.md). Las fichas API-001/002 ilustran el formato heredado normalizado; **sus contratos no son plantillas para otras APIs**.

## Flujo único para una nueva ficha

1. **Git y autoridad.** En worktree y rama correctos registrar `git status --short --branch` y `git rev-parse HEAD`; leer `AGENTS.md`, constitución vigente, Bloque 1 y decisiones pertinentes. No alterar trabajo ajeno ni asumir que copiar una ficha traslada aprobación.
2. **Identidad.** En este worktree **no existe** `material/UTTT_GO_Punto_4_Especificacion_API_v1.xlsx`; no declarar el Excel consultado. Localizar ID, método y ruta en [trazabilidad](traceability.md), [migración](migracion-sdd.md) y la fuente histórica aplicable. Registrar «cotejo directo con Excel pendiente de fuente» hasta disponer de la fuente original autorizada; no copiar archivos del worktree original con cambios sin confirmar. API-004, 006 y 007 son tres rutas/responsabilidades separadas; no crear ninguna de ellas por esta guía. No inventar rutas ni adoptar ejemplos contradictorios sin decisión.
3. **Fuentes y código en una sola inspección acotada.** Leer referencias de inventario registradas en trazabilidad, RF/RNF/AC/T-AUTH/TASK del spec histórico correspondiente, decisiones aplicables, código (controlador, DTO, validador, servicio, adaptadores), pruebas y QA exactos. Para API-004/006/007 consultar fase 02; no repetir auditoría completa de APIs ajenas. Registrar «pendiente de fuente» si falta evidencia primaria o el Excel; contrastar el Excel directamente solo cuando se incorpore por vía autorizada.
4. **Frontera.** Escribir una responsabilidad y las dependencias con sus dueños; OTP RF-15, API-003 logout y RF-06 recuperación no se implementan en API-001/002/004/006/007 por estar mencionados allí. Usar contratos del Bloque 1 para errores, Redis, Mongo y módulos; no duplicar repositorios de otros módulos.
5. **Tres niveles.** Aprobado = regla y autoridad expresa; candidato = comportamiento de código sin contrato ratificado; faltante = funcionalidad requerida no cubierta. Identificar método/ruta, entrada (obligatorios y opcionales), auth/propiedad, validaciones, límites, salidas y errores, persistencia, seguridad y degradación. Una aprobación individual parcial no convierte JSON/HTTP íntegro en aprobado.
6. **Brechas y dependencias.** Para cada escenario determinar: implementado y probado al nivel requerido, implementado sin evidencia suficiente, discrepancia **ratificada**, no implementado, otra ficha o bloqueado. No generar tarea de reimplementación por mera falta de pruebas; crear tarea de prueba específica. Si la discrepancia aún no fue ratificada, crear tarea de decisión bloqueante, no una corrección READY.
7. **Crear trío.** Copiar las tres plantillas a `specs/bloque-2-funcionalidades/<id-nombre>/`; reemplazar todos los `{{MARCADORES}}`, borrar filas de ejemplo y secciones no pertinentes con «Ninguno; fuente y motivo» cuando sea necesario. El formato canónico de plantillas es obligatorio para fichas nuevas; API-001/002 conservan sus encabezados heredados, validados mediante compatibilidad explícita. Asignar IDs de escenario propios y DEV locales sin renumerar AC/TASK originales.
8. **Tareas.** Una fila detallada y una fila de «Control de ejecución» por DEV. Objetivo, RF/escenario, tipo, archivos permitidos, acción, resultado, prueba/comando y evidencia identificable. Usar `READY` solo si ejecutable sin decisión pendiente; `BLOCKED` con ID de decisión/gate y evidencia para desbloquear; `DONE` solo con resultado fechado; `NOT_APPLICABLE` con justificación. `Cambio técnico=SI` incluye editar tests de aplicación o validar integraciones nuevas; `READY` técnico necesita acta identificada de contrato **y** gates cerrados. No confundir READY de lectura con permiso de implementación.
9. **Validar.** En raíz: `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/<id-nombre>`; para todas las fichas actuales del Bloque 2: `python scripts/validate-sdd-atomic.py`; para plantillas: `python scripts/validate-sdd-atomic.py --templates`; negativos locales: `python scripts/validate-sdd-atomic.py --self-test`. Corregir hallazgos reales, cotejar manualmente que las fuentes **dicen** lo atribuido (el script no verifica semántica, Excel ni contenido de actas). No usar producción para tests.
10. **Reporte.** Entregar matriz escenario ↔ brecha ↔ DEV, resultados del validador, tarea técnica READY autorizada o bloqueo, evidencia actual/histórica separada y [estado operativo](#estados-operativos); indicar quién debe decidir y qué prueba falta. No declarar VERIFIED global por un curl o dobles.
11. **Commit documental, solo si corresponde.** Verificar `git diff --check` y `git status --short`, revisar scope/secretos, enlaces, aprobaciones y PR/CI; preparar commit sin hacerlo por inferencia de esta guía. Esta intervención no autoriza commit, amend, push ni comenzar API-004.

## Estados operativos

Son etiquetas **documentales/operativas, no aprobaciones**. Registrar `Estado de aprobación de la ficha: PENDIENTE|PARCIAL|APROBADA` de forma idéntica en spec/plan/task y `Estado operativo:` en spec, declarando el alcance técnico evaluado. En escenarios, etiquetar el nivel de evidencia independientemente del estado de la ficha.

| Estado | Condición verificable |
|---|---|
| DOCUMENTADA | Contrato/fuentes/brechas/decisiones registrados; no afirma código ni permiso técnico. |
| READY_PARTIAL | Al menos una tarea **técnica** READY autorizada para alcance delimitado; otras permanecen bloqueadas. |
| READY_FOR_IMPLEMENTATION | Todas las tareas técnicas necesarias para el alcance declarado están READY, con contrato aprobado, precondiciones y autorización de gates documentada. Solo tareas de lectura/QA/documentación READY **no** bastan. |
| BLOCKED | El alcance técnico solicitado requiere decisión/gate/dependencia; puede haber código candidato y tareas documentales READY. |
| IMPLEMENTED_PENDING_VERIFICATION | Código presente; no se ha acreditado el nivel de prueba requerido, sin declarar listo un cambio técnico nuevo. Si además hay gates que impiden el alcance solicitado, usar BLOCKED y describir código presente. |
| VERIFIED | **Solo los escenarios enumerados** tienen prueba pertinente, artefacto, fecha, entorno y límite; no es aprobación integral automática. |

## Casos de inventario y evidencia

| Situación | Tratamiento en una sola pasada |
|---|---|
| Implementada con prueba pertinente | Mapear código ↔ escenario ↔ evidencia fechada; no tarea de reimplementación. Verificar si falta ratificación del contrato. |
| Parcialmente implementada | Separar código probado, código sin prueba, faltante y discrepancia aprobada; tareas por brecha sin fijar HTTP/DTO pendiente. |
| No implementada | Preparar contrato/escenarios/plan y tareas BLOCKED hasta aprobación; READY técnica solo con gates satisfechos. |
| Retirada por decisión | API-016 **no** recibe ficha nueva ni redirección: [DEC-019](constitution.md) mantiene Excel intacto y API-011 cubre su función por búsqueda/filtros; documentar retirada en trazabilidad, no reabrir implementación. |
| Fuera de las 42, aprobada expresamente | Conservar ID de decisión: `GET /api/v1/sellers/me` es DEC-019.d, **no** API-043; crear ficha propia con acta, sin suponer que toda ampliación está aprobada. `/health` es sonda operativa. |
| Evidencia histórica vs actual | QA 2026-11-04 con dobles, curl local comunicado y AWS atribuido se rotulan con fuente, fecha, nivel y límites; repetir comandos hoy solo si autorizado y registrar salida. Un despliegue, OpenAPI o `/health` no prueban todas las ramas ni OTP/blacklist. |

## Dependencias transversales Identity

Fuente canónica de **estado y alternativas**: [matriz DF02 y orden de bloqueos](decisiones-pendientes-fase-02.md#orden-y-bloqueos-expresos). Referenciar el mismo ID en cada ficha afectada, no crear «nueva decisión API-001» y «nueva decisión API-002». Decisiones particulares: DF02-02/07 para contrato de registro, DF02-03/07 para login, DF02-05 para perfil/avatar API-006/007; no trasladarlas automáticamente a otras rutas.

| ID histórico | Afecta | Estado vigente | Autoridad y evidencia para desbloquear | Tareas bloqueadas (ejemplos) |
|---|---|---|---|---|
| DF02-01, TASK-AUTH-002 | API-001/002, acceso privado de API-004 y rutas posteriores | OTP funcional aprobado, técnica pendiente | Equipo Identity/móvil + seguridad: acta de configuración/SDK/plantilla OTP y pruebas error/vencimiento/reenvío; enlace de guía móvil no es OTP. | DEV-API001-07/11, DEV-API002-11 y guard privado en su ficha. |
| DF02-10, TASK-AUTH-002/011 | API-001/002 y flujo de verificación | SMTP institucional/costo cero pendiente | Operaciones/Identity, presupuesto y profesor si procede: proveedor permitido y entrega real `@uttt.edu.mx` en entorno no productivo, cuotas y costo, sin secretos en evidencia. | DEV-API001-07/11, DEV-API002-11. |
| DF02-09, TASK-AUTH-011/012 | API-001/002 y guard de API-004/006/007 | Diseño técnico parcial, no ratificado | Identity + Data Modeler + seguridad: fuente/frecuencia de confirmación, CAS/reconciliación/reparación y prueba de fallo parcial con Mongo/Supabase autorizados. | DEV-API001-10/08, DEV-API002-07 y guard de sesiones/perfil. |
| DF02-04, TASK-AUTH-003/025 | API-002/003 y sesiones de API-004 | Revocación `revoked:{jti}` constitucional; refresh/logout sin resolver | Identity + móvil + seguridad: acta de renovación, almacenamiento, revocación y reingreso; estrategia sin `jti` conforme constitución (fallback `session_id` no ratificado). | DEV-API002-10/08; escritor logout solo en API-003. |
| DF02-08, TASK-AUTH-007 → 008 → 010 | **Toda programación fase 02** (001/002/004/006/007 incluidas) | Aprobación individual parcial de versiones; gate colectivo pendiente | Equipo/delegación: acta TASK-AUTH-007, aprobación SDD 008 tras 002..007, y contrato por ficha 010; aprobación operativa por operación aparte. | DEV-API001-06/08/09, DEV-API002-06/07/08; cualquier tarea técnica posterior con el mismo gate. |

El orden histórico TASK-AUTH-008 depende de 002..007 y TASK-AUTH-010 de 007/008; solicitar un acta **no requiere fingir que ese mismo gate ya cerró**. Consultar [contratos Bloque 1](../specs/bloque-1-arquitectura/contratos-transversales/spec.md) y la constitución antes de duplicar política compartida.
