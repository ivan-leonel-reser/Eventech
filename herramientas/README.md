# Herramientas

Guiones de apoyo que no forman parte de la aplicación ni del instalador.

## Prueba de los dígitos verificadores

`PruebaIntegridad.ps1` (o doble clic en `Prueba de integridad.cmd`) altera la base del
sistema **por fuera de la aplicación**, para comprobar que la verificación de los dígitos
verificadores lo detecta y que se puede resolver desde el sistema.

1. Toma un respaldo completo y verificado de la base en la carpeta de respaldos del
   sistema (`%ProgramData%\EvenTech\respaldos`), con el motivo *Antes de alterar los
   datos*. Con `-SinRespaldo_704ILR` no lo toma.
2. Altera lo que se elija en el menú (o con `-Alteracion_704ILR`):
   - `Monto`: suma $ 1000 al monto de una reserva → lo delata el **DV horizontal** de
     esa reserva;
   - `Pago`: borra el último pago registrado → lo delata el **DV vertical** de los pagos;
   - `Ambos`.
3. Explica qué hacer en el sistema.

Al abrir EvenTech aparece la alerta *Se detectaron problemas de integridad* con lo que se
alteró. Hasta resolverlo, solo ingresa una cuenta con permiso para restaurar un respaldo
(`RESPALDO_RESTAURAR`) o recalcular los dígitos verificadores (`INTEGRIDAD_RECALC`); el
perfil Administrador (`admin` / `admin123`) tiene los dos. Al ingresar, el sistema ofrece:

- **Restaurar un respaldo**: se elige el respaldo *Antes de alterar los datos* (o
  cualquier otro de la lista, o un `.bak` de otra carpeta con *Buscar otro archivo...*).
  Antes de restaurar se guarda un respaldo de la base actual; si el elegido no sirve, la
  base vuelve a como estaba. Al terminar, el sistema se reinicia sin la alerta.
- **Recalcular los dígitos verificadores**: los datos actuales, alterados incluidos, pasan
  a ser la referencia de integridad. Un estado de reserva que no existe en la tabla de
  estados no se legitima: para eso queda restaurar.

Todo queda en la bitácora (módulos *Integridad*, *Respaldos* y *Seguridad*).

La base es la misma con la que trabaja la aplicación en ese equipo y con ese usuario de
Windows (la conexión que guardó el instalador); sin conexión guardada, la de fábrica
(`localhost\SQLEXPRESS`, `EvenTechDB`). Otra base se elige con `-Instancia_704ILR` y
`-Base_704ILR`. No hace falta ser administrador del equipo ni tener `sqlcmd`.

```
powershell -ExecutionPolicy Bypass -File PruebaIntegridad.ps1
powershell -ExecutionPolicy Bypass -File PruebaIntegridad.ps1 -Alteracion_704ILR Ambos -SinConfirmar_704ILR
```
