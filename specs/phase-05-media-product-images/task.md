# Fotografías de productos — tareas propuestas

> Estado: **implementación funcional autorizada por el equipo el 2026-11-19**. Las tareas se cierran solo con evidencia de código/pruebas; ruta extra de confirmación, moderador real y despliegue requieren autorizaciones separadas.

| ID | Entregable verificable | Estado / dependencia |
|---|---|---|
| IMG-T01 | Conciliar DEC-019.f/h y API-012/013/014/017/018: rutas, DTO, propiedad, `Approved`, PATCH/null, HTTP y fallos parciales. | Aprobación funcional comunicada 2026-11-19; `/media/confirm` implementada como compatibilidad con API-018; registrar ficha formal adicional pendiente. |
| IMG-T02 | Media: intención propia Mongo + presigned PUT sin fallback mock; prueba de metadatos, firma, fallo cerrado. | Implementación preliminar; pruebas de firmado S3 local emulado pendientes. |
| IMG-T03 | Media: HEAD, moderación y CAS idempotente `Approved`/`Rejected`, borrado inseguro y GET temporal. | Adaptador Azure y lectura S3 implementados con dobles locales; sin umbrales aprobados/secretos del entorno, falla cerrado en `ProcessingFailed`. IAM EC2 S3 sin política, aceptación real pendiente. |
| IMG-T04 | Catalog: contrato Media dueño, POST con referencia propia aprobada; sin foto intacto. | Implementación y pruebas unitarias; falta integración Mongo+Redis de imagen. |
| IMG-T05 | Catalog: PATCH añadir/reemplazar/quitar con propietario+versión; GET/listado/POST/PATCH `imageUrl` temporal o null. | Implementación y pruebas unitarias; falta cobertura completa de integración. |
| IMG-T06 | OpenAPI y pruebas unitarias/integración/seguridad/E2E emulada; regresión de las tres suites y formato. | Snapshot OpenAPI y ejemplos cotejados por tests, E2E Mongo/Redis/LocalStack verde con política de prueba; build Release/formato verdes, unitarias 359/359, integración 280/280 y seguridad 149/149, sin omitidas (2026-11-20). Checklist AWS en `docs/media-pr33-aws-owner-checklist.md`. Ninguna prueba LocalStack acredita denegación anónima AWS. |
