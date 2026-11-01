# Instalador de EvenTech

Asistente de instalación y reinstalación del sistema para el usuario final. En un
equipo que todavía no tiene EvenTech:

1. copia la aplicación, que lleva incluido el entorno de ejecución de .NET 8;
2. crea la base de datos `EvenTechDB`, con sus datos de demostración, en la
   instancia de SQL Server que elige quien instala;
3. deja configurada la conexión de la aplicación a esa instancia.

Quien instala no necesita abrir una consola, tener `sqlcmd` ni escribir una cadena
de conexión: el asistente detecta las instancias de SQL Server del equipo y las
ofrece en una lista.

## Alcance de esta versión

Es la **segunda versión** del instalador (la de la versión 3.0 del sistema). Conserva
la instalación inicial de la primera y suma la **reinstalación**:

- Reconoce una instalación anterior por el identificador del producto (el mismo en
  todas las versiones) y lee su versión y la instancia que se eligió entonces.
- Si la instancia elegida ya tiene la base `EvenTechDB`, muestra lo que está (la
  versión instalada, la versión de la base y cuántas cuentas, clientes y reservas
  tiene) y lo que viene (la versión nueva y sus novedades), y ofrece:
  - **Conservar los datos y actualizar la base** (opción por defecto): guarda un
    respaldo completo y verificado de la base en la carpeta de respaldos de SQL Server,
    le aplica `db/schema.sql` —idempotente: agrega lo que falta sin volver a sembrar ni
    pisar datos— y, si algo falla, la devuelve a ese respaldo;
  - **Reemplazar la base por la de demostración**, también con respaldo previo.
- Conserva la conexión guardada del usuario, la clave de cifrado de los contactos
  (`%ProgramData%\EvenTech\crypto.key`), el registro de excepciones y las preferencias.
- No instala una versión anterior sobre una posterior (ni la aplicación ni la base), ni
  toca una base `EvenTechDB` que no sea del sistema.
- Con el sistema abierto no actualiza nada.
- Si el paquete trae los medios de SQL Server Express (carpeta `SQLServerExpress` junto
  al instalador) y el equipo no tiene ninguna instancia, ofrece instalar el motor en modo
  silencioso, previa aceptación de su licencia.

La versión que dejó el asistente queda grabada en la base (propiedad extendida
`EvenTech_Version`). Una base sin esa marca se reconoce por su esquema.

## Requisitos del equipo donde se instala

| Componente | Requisito |
|---|---|
| Sistema operativo | Windows 10 u 11 de 64 bits |
| Motor de base de datos | SQL Server Express 2019 o superior, instalado en el mismo equipo y con su servicio iniciado (o los medios de SQL Server Express en el paquete, para que el asistente lo instale) |
| Usuario de Windows | Administrador del equipo y de la instancia de SQL Server (quien instaló SQL Server lo es) |
| Espacio en disco | 170 MB para la aplicación y unos 80 MB para la base, en la carpeta de datos de SQL Server |
| .NET | No se requiere: viaja incluido en el instalador |

## Pasos del asistente

| Paso | Qué hace |
|---|---|
| Bienvenida | Presenta lo que se va a instalar |
| Carpeta de destino | Por defecto, `C:\Program Files\EvenTech` |
| Instancia de SQL Server | Lista las instancias detectadas (propone la de la instalación anterior); al avanzar comprueba la elegida. Sin instancias y con los medios en el paquete, ofrece instalar SQL Server Express |
| Actualización de la base de datos | Solo si la instancia ya tiene la base: lo que está, lo que viene y qué hacer con los datos |
| Tareas adicionales | Acceso directo en el escritorio |
| Listo para instalar | Resumen: carpeta, instancia (con su versión) y base |
| Base de datos | Crea la base restaurando `db/EvenTechDB.bak`, o la respalda y la actualiza (o la reemplaza); en los tres casos le aplica `db/schema.sql` |
| Instalando | Copia la aplicación y crea los accesos directos |
| Finalización | Guarda la conexión, comprueba el acceso a la base y ofrece abrir el sistema |

Al terminar quedan la aplicación en la carpeta elegida, los accesos directos del
menú Inicio (también el de la ayuda) y del escritorio, la entrada *EvenTech 3.0* en las aplicaciones
instaladas de Windows, la base `EvenTechDB` en la instancia y el archivo
`%APPDATA%\EvenTech\connection.cfg` con la conexión cifrada. El primer ingreso se
hace con `admin` / `admin123`; las demás cuentas de demostración figuran en
[`db/README.md`](../db/README.md).

La desinstalación quita la aplicación, los accesos directos y la entrada de Windows.
**La base de datos no se elimina**: sus datos quedan en SQL Server.

### Instalación desatendida

```bat
EvenTech_Instalador_v3.0.exe /VERYSILENT /SUPPRESSMSGBOXES /INSTANCIA=localhost\SQLEXPRESS /LOG=instalacion.log
EvenTech_Instalador_v3.0.exe /VERYSILENT /SUPPRESSMSGBOXES /INSTANCIA=localhost\SQLEXPRESS /BASE=REEMPLAZAR /LOG=instalacion.log
```

Termina con código 0 si instaló y con otro código si la instancia no sirve o la base no
se pudo preparar; el motivo queda en el registro. Sobre una base existente, `/BASE`
elige qué hacer con ella: `CONSERVAR` (por defecto) o `REEMPLAZAR`. `/MOTOR=1` muestra
la opción de instalar SQL Server Express aunque haya instancias y
`/ACEPTARLICENCIASQL=1` deja aceptada su licencia (para una instalación desatendida).

## Cómo se construye

```bat
powershell -ExecutionPolicy Bypass -File instalador\construir.ps1
```

Requiere el SDK de .NET (8 o superior) e Inno Setup 6
(`winget install JRSoftware.InnoSetup`). El guion publica la aplicación para Windows
de 64 bits con .NET 8 incluido, compila `EvenTech.iss` y deja
`salida\EvenTech_Instalador_v<versión>.exe`, del que informa el tamaño y el SHA-256.
Las carpetas `publicado` y `salida` se generan y no se versionan.

La versión del instalador se lee del ejecutable publicado: se cambia en un solo
lugar, la propiedad `Version` de `Directory.Build.props`. El guion informa además
el commit que lleva la aplicación publicada y avisa si se publicó con cambios sin
commitear: el instalador de una entrega se arma desde el commit entregado, con el
repositorio limpio (ver "Versión y commit" en el README del repositorio).

El respaldo `db/EvenTechDB.bak` viaja **dentro** del instalador. Como sus
cotizaciones y reservas pendientes vencen por fecha (regla RN-01), el instalador se
construye después de regenerar el respaldo.

| Ruta | Contenido |
|---|---|
| `EvenTech.iss` | Guion de Inno Setup: páginas, textos y lógica del asistente (UTF-8 con BOM) |
| `scripts/BaseDeDatos.ps1` | Diagnóstico de la instancia y de la base existente, restauración, actualización y reemplazo con respaldo previo, reversión, verificación y deshacer |
| `scripts/Conexion.ps1` | Guarda la conexión cifrada en el perfil del usuario |
| `recursos/` | Imágenes del asistente, con la marca del sistema, en tres escalas de pantalla |
| `construir.ps1` | Publica la aplicación y compila el instalador |

## Decisiones de diseño

- **La base se prepara antes de copiar la aplicación.** Es el único paso que depende
  del entorno. Si falla, los archivos instalados no se tocan (en una reinstalación, la
  versión anterior sigue funcionando con su base, que volvió al respaldo) y se puede
  volver atrás.
- **Cancelar deshace todo.** Si la instalación se cancela mientras se copian los
  archivos, el asistente quita la base que acababa de crear (solo la que tiene la fecha
  de creación de esa ejecución) o, en una reinstalación, devuelve la base al respaldo
  que tomó antes de modificarla.
- **Nunca se modifica una base sin respaldo.** Actualizar y reemplazar empiezan por un
  respaldo completo con suma de verificación (`RESTORE VERIFYONLY`), que queda en la
  carpeta de respaldos de la instancia como red de seguridad. Con otras conexiones
  abiertas sobre la base (el sistema abierto en otro equipo), no se respalda ni se
  migra. La instalación inicial sigue sin tocar una base existente: la comprobación va
  en el mismo lote que la restauración.
- **La migración es la del propio esquema.** `db/schema.sql` es idempotente: sobre una
  base 2.0 agrega los permisos, las traducciones y los objetos de la 3.0; sobre una base
  3.0 no cambia nada (se comprobó con la huella de cada tabla). El asistente lo ejecuta
  lote por lote en una sola sesión, con el proveedor de SQL Server de Windows.
- **Sin herramientas adicionales.** Los guiones usan el proveedor de SQL Server de
  .NET Framework, que viene con Windows. El respaldo se copia a la carpeta de
  respaldos de la instancia, que es la que el servicio de SQL Server siempre puede
  leer, y esa copia se borra al terminar.
- **La conexión se guarda con la cuenta de quien instala.** El archivo se cifra con
  DPAPI en el ámbito del usuario, igual que lo hace la aplicación, y por eso lo
  escribe la cuenta que inició la instalación y no la elevada. Con esa misma cuenta
  se comprueba que la base se pueda abrir.
- **.NET 8 incluido.** La aplicación se publica con su entorno de ejecución: no se
  agrega ningún paquete al código y el usuario final no instala .NET. Inno Setup es
  la herramienta con la que se arma el instalador, no una dependencia del sistema.

### Situaciones que informa el asistente

| Código | Situación | Qué ve quien instala |
|---|---|---|
| `SIN_CONEXION` | La instancia no responde | Que revise el nombre y que el servicio esté iniciado |
| `INSTANCIA_REMOTA` | La instancia es de otro equipo | Que la base se crea en una instancia de este equipo |
| `VERSION_ANTERIOR` | Motor anterior a SQL Server 2019 | La versión detectada y la mínima requerida |
| `SIN_PERMISO` | El usuario no puede crear bases | Que instale con un administrador de la instancia |
| `BASE_EXISTENTE` | La base apareció entre la comprobación y la creación | Que vuelva atrás para elegir qué hacer con ella |
| `BASE_AJENA` | La instancia tiene una base `EvenTechDB` que no es del sistema | Que elija otra instancia: esa base no se toca |
| `BASE_MAS_NUEVA` | La base es de una versión posterior a la del asistente | Que use el instalador de esa versión o de una posterior |
| `BASE_EN_USO` | Hay conexiones abiertas sobre la base | Que cierre el sistema en todos los equipos y vuelva a intentar |
| `ACTUALIZACION_FALLIDA` | La migración de la base falló | El detalle, y que la base volvió al respaldo (o dónde quedó el respaldo si no pudo volver) |
| `RESTAURACION_FALLIDA` | El motor no pudo restaurar | El detalle que informó SQL Server y la ruta del registro |
| `ERROR_NO_PREVISTO` | La comprobación de la instancia falló por una causa no prevista | Que no se pudo comprobar la instancia, con el detalle |
| `CONEXION_NO_GUARDADA` | Al terminar, no se pudo guardar `connection.cfg` | Que al abrir el sistema elija la misma instancia en la pantalla de conexión |
| `SIN_ACCESO_A_LA_BASE` | Al terminar, la cuenta que inició la instalación no puede abrir la base | Que el administrador de SQL Server le dé acceso a esa base |
