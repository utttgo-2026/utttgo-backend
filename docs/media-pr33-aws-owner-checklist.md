# PR #33: verificación pendiente del responsable AWS

**No ejecutada aquí.** No efectuar en producción ni cambiar recursos a mano;
coordinar con el propietario un entorno controlado, la revisión de seguridad
y cambios solo mediante `infra/terraform`. No pegar buckets reales, URLs
presignadas, claves ni respuestas con datos sensibles en el PR. El E2E con
LocalStack **no acredita** control de acceso anónimo (dio `200` a GET sin
firma); los pasos siguientes requieren evidencia AWS independiente.

## Antes de permitir fotos aprobadas

- [ ] Registrar decisión formal del umbral `MODERATION_MIN_CONFIDENCE` de
  Amazon Rekognition, tratamiento de fallos, reintentos, compensación y coste.
  El valor por defecto es `75`; valores fuera de `0..100` fallan cerrado y un
  fallo del proveedor termina en `ProcessingFailed`, nunca en `Approved`.
- [ ] Revisar/autorizar cambios Terraform de IAM: `infra/terraform/main.tf`
  actualmente crea bucket, Block Public Access y CORS, pero el Instance
  Profile EC2 **no tiene política S3 para este bucket**. Añadir mediante
  Terraform, tras aprobación, `s3:PutObject`, `s3:GetObject`,
  `s3:DeleteObject` en `arn:aws:s3:::<bucket>/staging/*` y acceso mínimo
  necesario a HEAD, sin `s3:*`, ACL pública ni claves estáticas en la app;
  verificar de forma explícita que rol de despliegue/cliente no hereda acceso
  innecesario. Plan/revisión antes de apply del propietario.
- [ ] En AWS, con permisos de lectura del responsable, comprobar
  `aws s3api get-public-access-block --bucket <bucket>`: cuatro banderas
  `true`; `aws s3api get-bucket-ownership-controls --bucket <bucket>`:
  `BucketOwnerEnforced`; `aws s3api get-bucket-acl --bucket <bucket>`:
  sin grants de `AllUsers`/`AuthenticatedUsers`; examinar
  `get-bucket-policy`/`get-bucket-policy-status` (si hay política) y Access
  Analyzer: sin acceso público; `get-bucket-encryption`: SSE-S3 activa.
  Verificar CORS contra orígenes aprobados, nunca `*` en entorno real.
- [ ] En un objeto **de prueba** y entorno controlado, con red ajena a la
  sesión AWS: GET HTTPS sin firma y HEAD sin firma deben devolver `403`
  (o `404` que no divulgue existencia), sin bytes. Guardar solo estado HTTP y
  fecha, nunca URL completa. Si es `200`, bloquear publicación y corregir
  política/ACL antes de cualquier despliegue.
- [ ] Verificar **en AWS S3 controlado**, no en LocalStack, que PUT firmado con
  `Content-Type` diferente, clave alterada y URL caducada son rechazados por
  S3; confirmar que PUT del mismo objeto no puede reemplazar un asset ya
  aprobado dentro del TTL de firma (riesgo a revisar). LocalStack ha aceptado
  cambios en MIME firmado, por lo que las pruebas locales solo acreditan
  rechazo posterior por HEAD y firma binaria, no enforcement de SigV4 ni
  privacidad/expiración reales de AWS.
- [ ] Con identidad de backend **no productivo**, solicitar intención y confirmar objeto:
  PUT presignado de **una clave staging** con `Content-Type` exacto,
  `X-Amz-Expires ≤ 300`, sin ACL pública; HEAD por backend debe ver MIME y
  tamaño reales. Una URL PUT no debe firmar otra clave, ni cambiar
  Content-Type, ni funcionar tras expiración. Intento de >4 000 000 bytes debe fallar
  en backend/confirmación; comprobar que la presigned PUT por sí sola
  **no limita bytes** (se necesita revisión de política ante consumo indebido).
- [ ] Comprobar que GET presignado se devuelve **solo** tras `Approved`,
  vence a ≤ 5 minutos, y deja de funcionar después; para pendiente, rechazado
  o usuario distinto no se expone URL. Comprobar borrado de objeto inseguro.
  No confundir éxito LocalStack, ni `aws s3api` autenticado del propietario,
  con prueba de anonimato.
- [ ] Registrar evidencia mínima (fecha, entorno, ID de prueba, comando sin
  credenciales y código HTTP) y revisión de seguridad/IAM; conciliar ficha
  formal de `/api/v1/media/confirm` antes del cierre de PR/despliegue.
