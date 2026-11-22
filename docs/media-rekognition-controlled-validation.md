# Prueba controlada de Amazon Rekognition - Media fase 05

> Preparacion, no ejecucion real. Este documento no contiene credenciales ni
> resultados observados. Las pruebas locales usan un doble de
> `IAmazonRekognition` y no acreditan acceso real al servicio AWS.

## Requisitos

1. Entorno AWS no productivo en `us-east-2`, bucket privado y rol de minimo
   privilegio con `rekognition:DetectModerationLabels`.
2. Credenciales resueltas por la cadena por defecto del SDK, preferentemente
   instance profile. No usar access keys en la aplicacion ni en scripts.
3. `MODERATION_MIN_CONFIDENCE` entre `0` y `100`; el valor operativo aprobado
   por defecto es `75`.

## Ejecucion manual autorizada

1. Aislar la API de cualquier entorno productivo y cargar solo variables no
   sensibles del entorno, incluyendo `AWS_REGION=us-east-2` y el bucket de
   pruebas.
2. Solicitar una intencion de subida, subir la imagen con el `Content-Type`
   firmado y confirmar el mismo `mediaAssetId`.
3. Registrar solo el identificador anonimizado, estado terminal y error
   tecnico. No guardar bytes, URLs presignadas, tokens ni respuestas completas.
4. Una respuesta con etiquetas de moderacion produce `Rejected` y elimina el
   objeto. Una excepcion, timeout o respuesta no disponible produce
   `ProcessingFailed`; nunca se aprueba por degradacion.
5. Repetir confirmacion terminal para comprobar idempotencia y verificar que
   un asset rechazado no se reabre.

## Evidencia

| ID anonimo | Estado esperado | Estado observado | Error tecnico |
|---|---|---|---|
| *por asignar* | *por registrar* | *por registrar* | *por registrar* |

No hay evidencia real en este repositorio hasta que el responsable autorice
la ejecucion y conserve los resultados fuera de Git.
