# memoria.md - Backend UTTT-GO

Registro vivo de decisiones, contexto y evidencia. No contiene secretos ni
credenciales.

## Decisiones vigentes

| ID | Decisión | Estado |
|---|---|---|
| DEC-001 | Compra directa individual; no existe carrito. | Resuelta |
| DEC-002 | El vendedor se autoactiva al completar su perfil de negocio. | Resuelta |
| DEC-003 | La eliminación de cuenta elimina la información propia. | Diseño técnico pendiente |
| DEC-005 | El chat se conserva durante el pedido y se purga dentro de tres días. | Diseño técnico pendiente |
| DEC-008 | El único pago es efectivo presencial. | Resuelta |
| DEC-009 | No existen menús ni horarios. | Resuelta |
| DEC-010 | La moderación previa de imágenes usa Amazon Rekognition. Las etiquetas con confianza igual o superior a `MODERATION_MIN_CONFIDENCE` rechazan; errores producen `ProcessingFailed`. | Resuelta |
| DEC-016 | Cada vendedor administra sus propios productos/publicaciones. | Resuelta funcionalmente |
| DEC-018 | La infraestructura objetivo es AWS `us-east-2`: una EC2 t3.micro, dos réplicas Compose detrás de Nginx, S3 privado, Redis local y Terraform. | Resuelta; costo final pendiente |
| DEC-019 | API-016 no se implementa; se aprueba `GET /api/v1/sellers/me`; Media pertenece a fase 05. | Resuelta |
| DEC-020 | El puerto 22 permanece cerrado; el despliegue usa SSM y el presupuesto usa variable externa. | Resuelta |
| DEC-021 | Los logs de la EC2 se publican con `awslogs` en CloudWatch. | Resuelta |

## Estado de fase 05

- `mediaAssets` vive en MongoDB; S3 solo almacena objetos privados.
- El cliente recibe únicamente URLs presignadas generadas por el backend.
- La intención valida MIME, extensión y máximo `4_000_000` bytes.
- La confirmación verifica HEAD, obtiene los bytes desde staging y llama a
  Rekognition con la identidad AWS del entorno.
- Solo `Approved` puede asociarse a un producto; no existe fallback aprobador.
- `Rejected` elimina el objeto; fallos del proveedor dejan `ProcessingFailed`.
- El E2E local usa Mongo, Redis, LocalStack y un doble de Rekognition. No
  acredita permisos reales de AWS, privacidad del bucket ni llamadas externas.
- La rama actual es `feature/media-rekognition` en el worktree
  `C:\UTTT-Go\media-rekognition`; todavía no hay commit de esta migración.

## Evidencia previa

- `origin/main` está en `2049fbe` al iniciar esta migración.
- CD #175/run `44968803181` quedó verde antes de abrir esta rama.
- La sonda productiva verificada previamente fue `GET /health` HTTP 200 con
  MongoDB y Redis saludables.
- Las pruebas locales anteriores no son evidencia de IAM, S3 privado ni
  servicio de moderación real.

## Pendientes

- Rama `feature/auth-logout` aprobada el 2026-11-21: implementa API-003 con `204`, `401` para token ausente/inválido/expirado/revocado, `503` si Redis falla, `jti` obligatorio, `SET NX` y TTL hasta `exp`; no implementa refresh token.

- Ejecutar build Release y las suites unitarias, integración y seguridad con
  Docker encendido antes de cualquier commit.
- Actualizar el snapshot OpenAPI y los ejemplos móviles del PATCH de producto.
- Preparar la rama independiente `infra/rekognition-lifecycle` desde `main`;
  solo ejecutar `fmt`, `validate` y `plan` sin aplicar infraestructura.
- Preparar `scripts/test-media-flow.ps1` sin ejecutarlo hasta autorización
  explícita.
