# Fotografías de productos — tareas propuestas

> Estado: **implementación funcional autorizada por el equipo el 2026-11-19**. Las tareas se cierran solo con evidencia de código/pruebas; ruta extra de confirmación, moderador real y despliegue requieren autorizaciones separadas.

| ID | Entregable verificable | Estado / dependencia |
|---|---|---|
| IMG-T01 | Conciliar DEC-019.f/h y API-012/013/014/017/018: rutas, DTO, propiedad, `Approved`, PATCH/null, HTTP y fallos parciales. | Aprobación funcional comunicada 2026-11-19; `/media/confirm` implementada como compatibilidad con API-018; registrar ficha formal adicional pendiente. |
| IMG-T02 | Media: intención propia Mongo + presigned PUT sin fallback mock; prueba de metadatos, firma, fallo cerrado. | Implementación preliminar; pruebas de firmado S3 local emulado pendientes. |
| IMG-T03 | Media: HEAD, moderación y CAS idempotente `Approved`/`Rejected`, borrado inseguro y GET temporal. | CAS/HEAD y pruebas unitarias implementados; proveedor Azure Content Safety sin adapter/configuración: falla cerrado. E2E pendiente. |
| IMG-T04 | Catalog: contrato Media dueño, POST con referencia propia aprobada; sin foto intacto. | Implementación y pruebas unitarias; falta integración Mongo+Redis de imagen. |
| IMG-T05 | Catalog: PATCH añadir/reemplazar/quitar con propietario+versión; GET/listado/POST/PATCH `imageUrl` temporal o null. | Implementación y pruebas unitarias; falta cobertura completa de integración. |
| IMG-T06 | OpenAPI y pruebas unitarias/integración/seguridad/E2E emulada; regresión de las tres suites y formato. | OpenAPI generado con ambas rutas; unitarias 345/345, integración completa 278/278 (repetida tras ampliar POST con imagen), seguridad completa 148/148. E2E LocalStack/Mongo/Redis y OpenAPI focalizadas 2/2. Formato/build verdes. La privacidad real requiere bucket AWS privado y revisión de IAM fuera de este trabajo; LocalStack no aplica la denegación anónima esperada. |
