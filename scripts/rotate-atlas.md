# Rotacion segura de credenciales MongoDB Atlas

Procedimiento operativo para cambiar la credencial usada por `MONGODB_URI` en
la EC2 sin guardar secretos en Git, Terraform, SSM Document, logs ni comandos
del shell.

## Precondiciones

- Tener aprobacion operativa para la ventana de mantenimiento.
- Tener acceso de administrador a MongoDB Atlas.
- Tener acceso interactivo a la EC2 mediante AWS Systems Manager Session
  Manager. No usar SSH.
- Confirmar que la instancia tiene dos replicas sanas y que `/health` devuelve
  `200` antes de iniciar.
- Preparar una URI nueva fuera del repositorio y de los logs. Codificar los
  caracteres reservados de la contraseña antes de incorporarla a la URI.

## 1. Crear el usuario nuevo en Atlas

1. Crear un usuario temporal nuevo con un nombre distinto al actual.
2. Asignar exactamente los mismos roles mínimos del usuario actual.
3. Mantener la misma lista de red permitida y exigir TLS.
4. Generar una contraseña nueva fuera del repositorio.
5. Verificar desde una estación administrativa que el usuario nuevo puede
   autenticar y leer/escribir las colecciones requeridas.
6. No eliminar todavía el usuario anterior.

## 2. Preparar el cambio en la EC2

1. Abrir una sesión interactiva con Session Manager sobre la instancia de
   producción.
2. Desactivar el historial de comandos de esa sesión y usar `umask 077`.
3. Crear una copia temporal protegida de `/opt/utttgo/.env` sin mostrarla ni
   enviarla a stdout.
4. Sustituir únicamente la línea `MONGODB_URI` en un archivo temporal.
5. Validar que el archivo contiene una sola asignación `MONGODB_URI`, que no
   está vacía y que conserva `MONGODB_DATABASE` y las demás variables.
6. Reemplazar `/opt/utttgo/.env` con `mv` atómico y conservar permisos
   restrictivos.
7. No pasar la URI como argumento de `aws ssm send-command`; los parámetros de
   SSM y sus logs no deben contener la credencial.

## 3. Recrear las réplicas una por una

1. Tomar el nombre del primer contenedor API y retirarlo del balanceo sin
   detener la otra réplica.
2. Recrear esa réplica usando el `.env` actualizado y la imagen ya presente en
   la instancia. No compilar ni descargar una imagen nueva durante la ventana.
3. Esperar su estado `healthy` y verificar que `/health` devuelve `200` con
   MongoDB y Redis saludables.
4. Ejecutar una comprobación autenticada de una operación MongoDB no
   destructiva.
5. Repetir el procedimiento con la segunda réplica.
6. Confirmar que las dos réplicas están sanas detrás de Nginx.

El Compose actual no ofrece un rolling update declarativo; si la operación
disponible solo puede recrear ambas réplicas juntas, no afirmar disponibilidad
continua. En ese caso, detener la operación y solicitar una ventana aprobada.

## 4. Validación posterior

- Consultar `/health` varias veces durante al menos una ventana de salud.
- Confirmar una lectura y una escritura funcional controlada, sin registrar
  datos personales ni credenciales.
- Revisar que no aparezcan URI, contraseñas o excepciones de autenticación en
  CloudWatch Logs.
- Confirmar el SHA de la imagen y el número de réplicas, sin copiar secretos.

## 5. Revocar el usuario anterior

1. Mantener el usuario anterior durante el periodo de observación aprobado.
2. Si no hay errores de autenticación ni reconexión, eliminar o deshabilitar
   el usuario anterior en Atlas.
3. Verificar nuevamente `/health` y una operación MongoDB no destructiva.
4. Registrar únicamente fecha, entorno, resultado y responsable; nunca la URI,
   contraseña, tokens o salida cruda del proveedor.

## Rollback

1. Si una réplica no alcanza `healthy`, detener el avance.
2. Restaurar el archivo `.env` protegido anterior mediante la misma sesión
   interactiva de Session Manager.
3. Recrear solo la réplica afectada y esperar `healthy`.
4. Si ambas fueron recreadas, restaurarlas una por una y verificar `/health`.
5. Mantener el usuario nuevo habilitado hasta determinar la causa.
6. No revocar el usuario anterior hasta completar la recuperación.
7. Si el cambio falló por Atlas, restaurar la conectividad primero y después
   investigar la causa sin exponer la credencial en logs.

## Criterio de cierre

La rotación solo se considera cerrada cuando ambas réplicas están sanas, las
operaciones MongoDB funcionan, el usuario anterior fue revocado y la revisión
de logs no muestra secretos.
