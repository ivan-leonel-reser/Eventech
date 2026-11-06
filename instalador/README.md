# Instalador de EvenTech

Asistente de instalación del sistema para el usuario final. En un equipo que todavía
no tiene EvenTech:

1. si el equipo no tiene SQL Server, instala **SQL Server Express** (viene incluido en
   el instalador), previa aceptación de su licencia;
2. crea la base de datos `EvenTechDB` en la instancia de SQL Server que elige quien
   instala: restaura el respaldo con los datos de demostración o, si el motor no puede
   leerlo, la crea con el guion del esquema (`db/schema.sql`);
3. copia la aplicación, que lleva incluido el entorno de ejecución de .NET 8;
4. deja configurada la conexión de la aplicación a esa instancia.

Quien instala no necesita abrir una consola, tener `sqlcmd` ni escribir una cadena
de conexión: el asistente detecta las instancias de SQL Server del equipo y las
ofrece en una lista. **Tampoco necesita ser administrador del equipo** (las PC
compartidas de una sala, por ejemplo): el asistente corre con los permisos del
usuario. El único paso que necesita a un administrador es instalar SQL Server
Express, y solo para ese paso Windows pide la autorización (UAC).

## Alcance de esta versión

Es la **primera versión** del instalador y realiza la **instalación inicial**. No
modifica una base de datos con contenido: si la instancia elegida ya tiene una base
`EvenTechDB` con objetos, lo informa y pide elegir otra instancia. La reinstalación
—detectar la versión instalada, mostrar la actualización entre lo instalado y lo
nuevo y conservar los datos— corresponde a la versión siguiente. Para eso esta versión
ya deja registrados, junto con la instalación, la versión instalada y la instancia
elegida.

La versión 2.1 suma a la 2.0 la instalación sin permisos de administrador, SQL Server
Express incluido y la creación de la base con el guion del esquema cuando no se puede
usar el respaldo.

## Requisitos del equipo donde se instala

| Componente | Requisito |
|---|---|
| Sistema operativo | Windows 10 u 11 de 64 bits |
| Motor de base de datos | Ninguno: si el equipo no tiene SQL Server, el asistente instala SQL Server Express. Si tiene, sirve SQL Server 2012 o superior en el mismo equipo (con 2019 o superior la base trae los datos de demostración) |
| Usuario de Windows | Cualquiera. Para instalar SQL Server Express, Windows pide la autorización de un administrador |
| Usuario en SQL Server | Que pueda crear bases (`dbcreator` o `sysadmin`), o que sea propietario (`db_owner`) de una base `EvenTechDB` vacía que le haya creado el administrador de SQL Server. Si el asistente instala SQL Server Express, quien instala queda como administrador de la instancia |
| Espacio en disco | 500 MB para la aplicación, unos 80 MB para la base y, si se instala SQL Server Express, 6 GB libres en el disco del sistema |
| .NET | No se requiere: viaja incluido en el instalador |

## Pasos del asistente

| Paso | Qué hace |
|---|---|
| Bienvenida | Presenta lo que se va a instalar |
| Carpeta de destino | Por defecto, `%LOCALAPPDATA%\Programs\EvenTech` (la carpeta de programas del usuario). Al avanzar comprueba que el usuario pueda escribir en la carpeta elegida |
| Instancia de SQL Server | Lista las instancias detectadas. Sin instancias, propone instalar SQL Server Express (marcado; falta aceptar la licencia); con instancias, lo ofrece para una instancia nueva. Al avanzar comprueba la instancia y decide cómo se crea la base |
| Tareas adicionales | Acceso directo en el escritorio |
| Listo para instalar | Resumen: carpeta, motor instalado (si lo hubo), instancia (con su versión) y base |
| Creación de la base de datos | Restaura el respaldo `db/EvenTechDB.bak`, o crea la base con `db/schema.sql` |
| Instalando | Copia la aplicación y crea los accesos directos |
| Finalización | Guarda la conexión, comprueba el acceso a la base y ofrece abrir el sistema |

Al terminar quedan la aplicación en la carpeta elegida, los accesos directos del
menú Inicio y del escritorio del usuario, la entrada *EvenTech 2.1* en las
aplicaciones instaladas del usuario, la base `EvenTechDB` en la instancia y el archivo
`%APPDATA%\EvenTech\connection.cfg` con la conexión cifrada. El primer ingreso se
hace con `admin` / `admin123`; las demás cuentas de demostración figuran en
[`db/README.md`](../db/README.md).

La desinstalación quita la aplicación, los accesos directos y la entrada de Windows.
**La base de datos no se elimina**: sus datos quedan en SQL Server. Tampoco se quita
SQL Server Express, si lo instaló el asistente.

### Cómo se crea la base

| Situación de la instancia | Cómo se crea |
|---|---|
| Sin la base, SQL Server 2019 o superior | Se restaura el respaldo con los datos de demostración |
| Sin la base, SQL Server 2012 a 2017 (no pueden leer el respaldo) | Se crea vacía y se le aplica `db/schema.sql`: queda con la cuenta `admin` y los catálogos, sin los datos de demostración |
| Con una base `EvenTechDB` vacía (la preparó el administrador de SQL Server) | Se le aplica `db/schema.sql`, igual que en el caso anterior |
| Con una base `EvenTechDB` con contenido | No se toca: se pide elegir otra instancia |

### Instalación desatendida

```bat
EvenTech_Instalador_v2.1.exe /VERYSILENT /SUPPRESSMSGBOXES /INSTANCIA=localhost\SQLEXPRESS /LOG=instalacion.log
```

Termina con código 0 si instaló y con otro código si la instancia no sirve; el motivo
queda en el registro. Para instalar también SQL Server Express hay que pedirlo
(`/MOTOR=1`) y aceptar su licencia de forma explícita (`/ACEPTARLICENCIASQL=1`): una
instalación desatendida nunca la acepta por su cuenta.

## Cómo se construye

```bat
powershell -ExecutionPolicy Bypass -File instalador\construir.ps1
```

Requiere el SDK de .NET (8 o superior), Inno Setup 6
(`winget install JRSoftware.InnoSetup`) y los medios de SQL Server Express en la
carpeta `instalador\motor\SQLServerExpress`: la extracción, sin modificar, de los
medios "Express Core" que baja el instalador de SQL Server 2022 Express de Microsoft
("Descargar medios"). El guion comprueba que estén completos y firmados por Microsoft;
sin ellos se detiene (salvo con `-SinMotor_704ILR`, solo para pruebas). Publica la
aplicación para Windows de 64 bits con .NET 8 incluido, compila `EvenTech.iss` y deja
`salida\EvenTech_Instalador_v<versión>.exe`, del que informa el tamaño y el SHA-256.
Las carpetas `motor`, `publicado` y `salida` no se versionan.

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
| `scripts/BaseDeDatos.ps1` | Diagnóstico de la instancia, creación de la base (respaldo o esquema), verificación y deshacer |
| `scripts/Conexion.ps1` | Guarda la conexión cifrada en el perfil del usuario |
| `recursos/` | Imágenes del asistente, con la marca del sistema, en tres escalas de pantalla |
| `construir.ps1` | Publica la aplicación y compila el instalador |

## Decisiones de diseño

- **Sin permisos de administrador.** El asistente se compila con
  `PrivilegesRequired=lowest`: instala en la carpeta de programas del usuario y
  registra la instalación para ese usuario. Todo lo que el sistema escribe va a
  carpetas con permiso de escritura (`%APPDATA%`, `%ProgramData%`, `%LOCALAPPDATA%`);
  nada al lado del ejecutable. Si se elige una carpeta donde el usuario no puede
  escribir (Archivos de programa, por ejemplo), el asistente lo dice antes de seguir.
- **SQL Server Express incluido, con elevación solo para ese paso.** El instalador de
  SQL Server se lanza pidiendo la autorización a Windows (UAC). Quedan como
  administradores de la instancia el grupo de administradores del equipo (buscado por
  su identificador, porque su nombre cambia con el idioma de Windows) y quien inició
  la instalación, que es quien va a usar el sistema. Antes se comprueban .NET Framework
  4.7.2 y 6 GB libres; un reinicio pendiente de Windows se informa.
- **La base se crea antes de copiar la aplicación.** Es el paso que depende del
  entorno. Si falla, el equipo queda como estaba y se puede volver atrás para elegir
  otra instancia.
- **Cancelar deshace todo.** Si la instalación se cancela mientras se copian los
  archivos, el asistente revierte la copia y quita la base que acababa de crear (o,
  si era una base vacía existente, la deja vacía). Solo deshace lo que tiene la marca
  de esa ejecución. SQL Server Express, si se lo instaló, queda instalado.
- **Una base con contenido no se toca.** La comprobación se repite en el mismo lote
  que la creación: restaurar la misma base sobre sí misma no exige confirmación del
  motor cuando está en modo de recuperación simple.
- **El respaldo, donde el motor lo pueda leer.** Quien lee el respaldo es la cuenta
  del servicio de SQL Server, que no puede entrar a la carpeta temporal del usuario.
  El asistente lo deja en `%ProgramData%\EvenTech\instalacion_<fecha>`, con permiso de
  lectura para todos, y lo borra al terminar. Si quien instala puede escribir en la
  carpeta de respaldos de la instancia, se usa esa.
- **Sin herramientas adicionales.** Los guiones usan el proveedor de SQL Server de
  .NET Framework, que viene con Windows. `db/schema.sql` se ejecuta lote por lote,
  como lo haría `sqlcmd`.
- **La conexión se guarda con la cuenta de quien instala.** El archivo se cifra con
  DPAPI en el ámbito del usuario, igual que lo hace la aplicación. Con esa misma cuenta
  se comprueba que la base se pueda abrir. Si la conexión deja de servir (otra
  instancia, otro equipo), la aplicación abre su pantalla de configuración de la
  conexión: se ajusta sin recompilar.
- **.NET 8 incluido.** La aplicación se publica con su entorno de ejecución: no se
  agrega ningún paquete al código y el usuario final no instala .NET. Inno Setup es
  la herramienta con la que se arma el instalador, no una dependencia del sistema.

### Situaciones que informa el asistente

| Código | Situación | Qué ve quien instala |
|---|---|---|
| — | La carpeta de destino no admite escritura | Que elija una carpeta propia del usuario |
| — | No se aceptó la licencia de SQL Server Express | Que hay que aceptarla para instalarlo |
| — | Windows no recibió la autorización para instalar SQL Server Express | Que un administrador la acepte, o que elija una instancia existente |
| — | Falta .NET Framework 4.7.2, faltan 6 GB, hay un reinicio pendiente o el instalador de SQL Server falló | La causa y, si falló, el código y la carpeta de su registro |
| `SIN_CONEXION` | La instancia no responde | Que revise el nombre y que el servicio esté iniciado |
| `INSTANCIA_REMOTA` | La instancia es de otro equipo | Que la base se crea en una instancia de este equipo |
| `VERSION_ANTERIOR` | Motor anterior a SQL Server 2012 | La versión detectada y la mínima requerida |
| `SIN_PERMISO` | El usuario no puede crear bases | Que instale con un administrador de la instancia, o que ese administrador le cree una base `EvenTechDB` vacía y lo haga propietario |
| `BASE_EXISTENTE` | Ya hay una base `EvenTechDB` con contenido | Que esta versión no modifica una base existente |
| `RESTAURACION_FALLIDA`, `CREACION_FALLIDA` | El motor no pudo crear la base | El detalle que informó SQL Server y la ruta del registro |
| `ERROR_NO_PREVISTO` | La comprobación de la instancia falló por una causa no prevista | Que no se pudo comprobar la instancia, con el detalle |
| `CONEXION_NO_GUARDADA` | Al terminar, no se pudo guardar `connection.cfg` | Que al abrir el sistema elija la misma instancia en la pantalla de conexión |
| `SIN_ACCESO_A_LA_BASE` | Al terminar, la cuenta que inició la instalación no puede abrir la base | Que el administrador de SQL Server le dé acceso a esa base |
