# task.md — Conciliación y tareas propuestas: productos por vendedor · Backend UTTT-GO
> Fase histórica `phase-03-catalog-offers` · Versión 2.0 · **Borrador pendiente de aprobación integral** · 2026-11-03.
> Deriva de `spec.md` v2.0 → `plan.md` v2.0; no autoriza implementación sin ficha y spec aprobados. Ninguna tarea o prueba está completada.

## Gates previos (documentales)

- [ ] **TASK-CATALOG-001 — Aprobar spec de producto por vendedor.** RF-01..14. Hecho cuando: equipo aprueba íntegramente `spec.md` v2.0, sin modelo canónico/ofertas compartidas.
- [ ] **TASK-CATALOG-002 — Conciliar fichas de lectura y escritura.** RF-01..09. Hecho cuando: API-011..015 tienen payloads/queries/actores revisados con el Excel sin cambiar método/ruta unilateralmente; ingredientes, precio/stock y permiso de propietario quedan comprobables.
- [ ] **TASK-CATALOG-003 — Resolver API-016 formalmente.** RF-07/08. Hecho cuando: equipo aprueba su retiro/sustitución funcional con búsqueda/filtros de API-011 o una alternativa explícita; hasta entonces mantener la ficha original como pendiente, no implementar comparativa compartida.
- [ ] **TASK-CATALOG-004 — Definir ficha de eliminación de producto.** RF-06. Hecho cuando: método/ruta, autorización, efectos en pedidos/media y pruebas están aprobados; ninguna ruta `DELETE /products/*` se expone antes.
- [ ] **TASK-CATALOG-005 — Aprobar plan y dependencias.** RF-01..14. Hecho cuando: este `plan.md` v2.0, contrato de Orders API-023, ratings de Reviews y borrado coordinado con Identity tienen responsable y aprobación; duplicados del mismo vendedor e ingredientes definidos.

## Implementación futura (bloqueada por gates)

- [ ] **TASK-CATALOG-010 — Modelo `products` propio del vendedor.** RF-01..04. Depende de: 001/002/005. Hecho cuando: POCO/índices y prueba BSON documentan dueño, ingredientes, precio decimal, existencias, estado y media aprobada; permiten homónimos de distintos vendedores.
- [ ] **TASK-CATALOG-011 — Categorías activas y gestión Admin.** RF-12. Depende de: 001/005. Hecho cuando: API-019..022 respetan rutas, autorización y ocultamiento de productos sin borrarlos, con pruebas.
- [ ] **TASK-CATALOG-012 — Búsqueda y filtros entre vendedores.** RF-07/08. Depende de: 002/005. Hecho cuando: API-011/012 muestran publicaciones de distintos vendedores, ingredientes/precios y ratings reales mediante contrato Reviews, paginadas con validadores y pruebas.
- [ ] **TASK-CATALOG-013 — Publicar y editar producto propio.** RF-01/02/04/11. Depende de: 002/010. Hecho cuando: API-013/014 validan `sellerId` local, precio/stock y media aprobada; edición de ajeno devuelve `403`; ambas publicaciones homónimas de vendedores distintos permanecen.
- [ ] **TASK-CATALOG-014 — Pausar/reactivar.** RF-05/09. Depende de: 002/010. Hecho cuando: API-015 distingue pausa y agotamiento, valida dueño y no altera pedidos existentes; tests de visibilidad y autorización pasan.
- [ ] **TASK-CATALOG-015 — Eliminar publicación por dueño.** RF-06/14. Depende de: 004/010. Hecho cuando: contrato aprobado evita compra y acceso a publicación eliminada, trata pedidos compartidos conforme DEC-003 y prueba `403` para ajeno; sin ficha se mantiene bloqueada.
- [ ] **TASK-CATALOG-016 — Compra y stock.** RF-09/10. Depende de: spec de Orders y API-023 conciliada. Hecho cuando: contrato interno sobre `productId`, precio server-side, reserva concurrente, cancelación idempotente y carrera con pausa/categoría se verifican sin afirmar atomicidad no probada.
- [ ] **TASK-CATALOG-017 — Caché y media.** RF-11/13. Depende de: 010..014 y Media. Hecho cuando: cambios de ingredientes/precio/stock/estado/categoría invalidan respuestas afectadas, Redis cache fail-open sin relajar seguridad y media no aprobada bloquea publicación.
- [ ] **TASK-CATALOG-018 — Baja de vendedor con otros módulos.** RF-14. Depende de: política técnica DEC-003 y Identity/Media/Orders. Hecho cuando: publicaciones/contenido propios son eliminados de forma coordinada y no se destruyen arbitrariamente datos pertenecientes al otro usuario; tests de reintento y privacidad pasan.
- [ ] **TASK-CATALOG-019 — Auditoría de rutas y trazabilidad.** RF-01..14. Depende de: 002..018 aplicables. Hecho cuando: no se publican API-016 ni rutas extras sin ficha aprobada, matriz y `memoria.md` reflejan contratos reales y suites Release/formato/unitarias/integración/seguridad pasan.

## Sustituciones y alcance

TASK-CATALOG-001..059 y 060..064 de v1.x son **históricos**, no un segundo backlog implementable: sus tareas de `offers`, producto canónico, índice único global, primera oferta y comparación `/products/{productId}/offers` deben retirarse del alcance al aprobar este borrador. Los IDs aquí son los de **v2.0 propuesta**, no implican que se ejecutaron tareas anteriores con los mismos números. API-016 y eliminación pública de producto permanecen bloqueadas hasta revisión/aprobación formal. T-CAT-01..08 en `spec.md` v2.0 son escenarios previstos, nunca pruebas ejecutadas.
