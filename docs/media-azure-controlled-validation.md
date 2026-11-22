# Prueba controlada Azure Content Safety — Media fase 05

> **Preparación, no ejecución real.** Umbrales 0/0/0/0 autorizados solo para
> validación aislada; producción sigue fail-closed. Esta matriz no contiene
> resultados observados. Nunca guardar imágenes, URL firmadas, respuestas
> completas de Azure, claves ni datos de personas en Git o en logs.

## Acceso que debe facilitar el responsable de Azure

1. Recurso **Azure AI Content Safety de pruebas** con endpoint HTTPS y clave de
   suscripción no productiva, con acceso habilitado para REST `2024-09-01` e
   `image:analyze`; cuota suficiente para un conjunto pequeño y presupuesto
   autorizado. Entregar endpoint/clave mediante gestor de secretos aprobado,
   nunca en PR ni `appsettings`.
2. Responsable AWS: bucket **no productivo** privado y credencial/rol de
   pruebas de mínimo privilegio para PUT, HEAD, GET y DELETE en staging;
   requerirá validación separada de IAM/CORS y aislamiento de producción.
   La ausencia de política S3 en el rol EC2 de Terraform sigue bloqueando una
   prueba en esa instancia; no modificar infraestructura como parte de este
   procedimiento.
3. MongoDB/Redis y cuenta **vendedora de prueba** aislados; aprobación/consentimiento
   de uso del corpus. Sin estos accesos **no hay ejecución real autorizada**.

## Ejecución manual autorizada (responsable de pruebas)

1. Aislar la API de cualquier entorno productivo. Cargar secretos localmente
   mediante el mecanismo autorizado; establecer
   `ASPNETCORE_ENVIRONMENT=ModerationValidation`,
   `CONTENT_SAFETY_TEST_ENABLED=true`, `CONTENT_SAFETY_ENDPOINT` y
   `CONTENT_SAFETY_KEY` **solo en ese proceso**, y fijar los cuatro
   `CONTENT_SAFETY_MAX_{HATE,SELF_HARM,SEXUAL,VIOLENCE}_SEVERITY=0`.
   Comprobar que en `Production`, aun si se exportan por error, no se aprueba.
2. Usar fotos con licencia/consentimiento de plato, bebida empaquetada, empaque
   legible, utensilio de cocina junto a comida, carne cruda y salsa roja (bordes
   de falso positivo), con al menos dos iluminaciones. No crear ni almacenar
   material sensible para forzar severidades: esos casos se ensayan con
   respuestas simuladas en unitarias.
3. Para cada foto de `<= 4 000 000` bytes, JPEG/PNG/WebP con extensión y bytes
   coincidentes: solicitar `POST /api/v1/media/upload-url` como vendedor de prueba;
   subir los mismos bytes con el `Content-Type` presignado; confirmar con
   `POST /api/v1/media/confirm`; si se observa `PendingModeration`, consultar
   `GET /api/v1/media/{mediaAssetId}` como propietario y registrar estado.
   Repetir confirmación terminal para comprobar idempotencia. Un 429/5xx de
   Azure hace máximo 3 intentos del **mismo claim** (1 s y 2 s entre intentos);
   no volver a subir el mismo asset para cambiar `Rejected` a `Approved`.
4. Registrar **solo** ID aleatorio de corpus, categoría de prueba, juicio humano
   esperado, severidades observadas (`Hate/SelfHarm/Sexual/Violence`), estado
   final, discrepancia y código/clase de error técnico si lo hubo. El backend
   emite solo las cuatro severidades a logs estructurados cuando la respuesta
   es válida; sin bytes, claves, URL, ID de objeto ni payload Azure. Correlacionar
   por ID de ejecución fuera de Git; conservar la matriz anonimizada según
   política institucional.
5. Terminar proceso y eliminar credenciales temporales conforme al responsable.
   Imagen rechazada -> objeto eliminado; `ProcessingFailed` o pendiente
   requiere revisión operativa de la prueba, no aprobación manual por API.
   No instalar los umbrales en producción.

## Matriz de evidencia (plantilla vacía; **cero imágenes observadas**)

| ID anónimo | Categoría de prueba | Juicio humano esperado | Azure H/S/Sx/V (0/2/4/6) | Estado final | FP / FN / no aplica | Error técnico (código/clase) |
|---|---|---|---|---|---|---|
| *por asignar* | plato/bebida/empaque/utensilio/carne/salsa | *por registrar* | *por registrar* | *por registrar* | *por registrar* | *por registrar* |

**Tasas:** calcular **solo** cuando haya resultados realmente observados,
revisión humana y denominador válido. Excluir fallos técnicos/no evaluados del
denominador de decisiones de moderación, pero reportar su cantidad aparte.
`FP = casos rechazados por Azure (severidad >0) considerados aptos por revisión humana
/ casos aptos revisados con evaluación válida`; `FN = casos aprobados por Azure
(cuatro ceros) considerados no aptos por revisión humana / casos no aptos revisados
con evaluación válida`. De momento **no hay tasas** (N observado = 0);
las respuestas simuladas de tests jamás cuentan como imágenes ni como tasa.

## Bloqueos y revisión manual

- 4 000 001 bytes, MIME/extensión no permitida, HEAD no coincidente o firma
  binaria discrepante: rechazar sin llamar a Azure. No se comprime ni transforma.
- Una evaluación válida con alguna severidad >0 se rechaza y borra; apelación
  de falso positivo requiere decisión humana y **flujo futuro con nueva revisión
  autorizada**. No hay ruta de override.
- `PendingModeration` concurrente no lanza otra evaluación. Tras 2 minutos de
  antigüedad, **la siguiente confirmación autenticada** puede marcar
  `ProcessingFailed` mediante CAS, sin aprobar. La expiración no es un job
  automático: un asset sin nueva llamada permanece pendiente y debe ser
  revisado operativamente. Un `ProcessingFailed` no se reintenta por llamada
  repetida; una nueva foto precisa una nueva intención y revisión. No resubir
  un `Rejected` para convertirlo en `Approved` sin decisión de apelación.
