# Instalador de EvenTech

Asistente de instalación y reinstalación del sistema para el usuario final. Lleva
adentro todo lo que el sistema necesita para funcionar. En un equipo que todavía no
tiene EvenTech:

1. si el equipo no tiene SQL Server, instala **SQL Server Express**, que viaja dentro
   del instalador (previa aceptación de su licencia);
2. crea la base de datos `EvenTechDB` en la instancia de SQL Server que elige quien
   instala (o en la recién instalada): restaura el respaldo con los datos de
   demostración o, si el motor no puede leerlo, la crea con el guion del esquema
   (`db/schema.sql`);
3. copia la aplicación, que lleva incluido el entorno de ejecución de .NET 8;
4. deja configurada la conexión de la aplicación a esa instancia.

Quien instala no necesita tener nada instalado de antemano, abrir una consola, tener
`sqlcmd` ni escribir una cadena de conexión: el asistente detecta las instancias de
SQL Server del equipo y las ofrece en una lista, y si no hay ninguna instala una.
**Tampoco necesita ser administrador del equipo** (las PC compartidas de una sala, por
ejemplo): el asistente corre con los permisos del usuario. El único paso que necesita a
un administrador es instalar SQL Server Express, y solo para ese paso Windows pide la
autorización (UAC).

## Alcance de esta versión

Es la **segunda versión** del instalador (la de la versión 3.0 del sistema). Conserva
la instalación inicial de la primera —con lo que sumó la 2.1: sin permisos de
administrador, con SQL Server Express incluido y con el guion del esquema cuando no se
puede usar el respaldo— y suma la **reinstalación**:

- Reconoce una instalación anterior por el identificador del producto (el mismo en
  todas las versiones), registrada para el usuario como la deja la 2.1, y lee su
  versión, su carpeta y la instancia que se eligió entonces. **Avisa que ya hay una
  versión instalada y la actualiza en la misma carpeta**: no vuelve a pedir la carpeta
  ni la instancia (salvo que esa instancia ya no responda o no tenga la base) y pasa
  directo a lo que está y lo que viene.
- Si la instancia elegida ya tiene la base `EvenTechDB`, muestra lo que está (la
  versión instalada, la versión de la base y cuántas cuentas, clientes y reservas
  tiene) y lo que viene (la versión nueva y sus novedades), y ofrece:
  - **Conservar los datos y actualizar la base** (opción por defecto): guarda un
    respaldo completo y verificado de la base en la carpeta de respaldos de SQL Server,
    le aplica `db/schema.sql` —idempotente: agrega lo que falta sin volver a sembrar ni
    pisar datos— y, si algo falla, la devuelve a ese respaldo;
  - **Reemplazar la base por la de demostración**, también con respaldo previo (solo con
    SQL Server 2019 o superior, que puede leer el respaldo de demostración).
- Conserva la conexión guardada del usuario, la clave de cifrado de los contactos
  (`%ProgramData%\EvenTech\crypto.key`), los respaldos hechos desde la aplicación, el
  registro de excepciones y las preferencias.
- No instala una versión anterior sobre una posterior (ni la aplicación ni la base), ni
  toca una base `EvenTechDB` que no sea del sistema.
- Con el sistema abierto no actualiza nada.
- Una instalación de la 2.0, que se hacía para todos los usuarios y con permisos de
  administrador (`C:\Program Files\EvenTech`), no se puede reemplazar sin serlo: el
  asistente lo explica y pide desinstalarla primero. La desinstalación conserva la base
  y la conexión, así que al volver a ejecutarlo el asistente encuentra la base y la
  actualiza conservando los datos.
- **Instala el motor de base de datos.** El instalador lleva adentro SQL Server 2022
  Express (los medios de Microsoft, sin modificar). Si el equipo no tiene ninguna
  instancia de SQL Server, la opción de instalarlo aparece marcada: basta con aceptar su
  licencia. Si ya tiene alguna, la opción queda disponible para crear una instancia nueva.
  En una reinstalación no se ofrece, salvo que no quede ninguna instancia o se lo pida
  con `/MOTOR=1`.

La versión que dejó el asistente queda grabada en la base (propiedad extendida
`EvenTech_Version`). Una base sin esa marca se reconoce por su esquema.

## Requisitos del equipo donde se instala

| Componente | Requisito |
|---|---|
| Sistema operativo | Windows 10 (versión 1803 o posterior, que trae .NET Framework 4.7.2, requisito de SQL Server 2022) u 11, de 64 bits |
| Motor de base de datos | No se requiere: si el equipo no tiene SQL Server, el asistente instala SQL Server 2022 Express. También sirve una instancia de SQL Server 2012 o superior ya instalada en el mismo equipo (con 2019 o superior, la base trae los datos de demostración) |
| Usuario de Windows | Cualquiera. Para instalar SQL Server Express, Windows pide la autorización de un administrador |
| Usuario en SQL Server | Que pueda crear bases (`dbcreator` o `sysadmin`), o que sea propietario (`db_owner`) de una base `EvenTechDB` vacía que le haya creado el administrador de SQL Server. Para actualizar una base existente, que sea su propietario o administrador de la instancia. Si el asistente instala SQL Server Express, quien instala queda como administrador de la instancia |
| Espacio en disco | 500 MB para la aplicación y unos 80 MB para la base; si hay que instalar SQL Server Express, al menos 6 GB libres en el disco del sistema |
| .NET | No se requiere: viaja incluido en el instalador |

## Pasos del asistente

| Paso | Qué hace |
|---|---|
| Bienvenida | Presenta lo que se va a instalar |
| Carpeta de destino | Por defecto, `%LOCALAPPDATA%\Programs\EvenTech` (la carpeta de programas del usuario). Al avanzar comprueba que el usuario pueda escribir en la carpeta elegida. En una reinstalación no se muestra: se usa la de la instalación anterior |
| Instancia de SQL Server | Lista las instancias detectadas; al avanzar comprueba la elegida y decide cómo se crea la base. Sin instancias, propone instalar SQL Server Express en `localhost\SQLEXPRESS` (marcado; falta aceptar la licencia) y, al avanzar, lo instala (entre 5 y 15 minutos). En una reinstalación no se muestra si la instancia anterior responde y tiene la base |
| Actualización de la base de datos | Solo si la instancia ya tiene la base: lo que está, lo que viene y qué hacer con los datos (conservar y actualizar, marcado) |
| Tareas adicionales | Acceso directo en el escritorio |
| Listo para instalar | Resumen: actualización (versión instalada → nueva), carpeta, motor instalado (si lo hubo), instancia (con su versión) y base |
| Base de datos | Crea la base restaurando `db/EvenTechDB.bak` (o con `db/schema.sql` si el motor no puede leerlo), o la respalda y la actualiza (o la reemplaza); en todos los casos le aplica `db/schema.sql` |
| Instalando | Copia la aplicación y crea los accesos directos |
| Finalización | Guarda la conexión, comprueba el acceso a la base y ofrece abrir el sistema |

Al terminar quedan la aplicación en la carpeta elegida, los accesos directos del
menú Inicio (también el de la ayuda) y del escritorio del usuario, la entrada *EvenTech 3.0*
en las aplicaciones instaladas del usuario, la base `EvenTechDB` en la instancia y el archivo
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
EvenTech_Instalador_v3.0.exe /VERYSILENT /SUPPRESSMSGBOXES /INSTANCIA=localhost\SQLEXPRESS /LOG=instalacion.log
EvenTech_Instalador_v3.0.exe /VERYSILENT /SUPPRESSMSGBOXES /INSTANCIA=localhost\SQLEXPRESS /BASE=REEMPLAZAR /LOG=instalacion.log
```

Termina con código 0 si instaló y con otro código si la instancia no sirve o la base no
se pudo preparar; el motivo queda en el registro. Sobre una base existente, `/BASE`
elige qué hacer con ella: `CONSERVAR` (por defecto) o `REEMPLAZAR`. `/MOTOR=1` marca
la opción de instalar SQL Server Express aunque haya instancias y
`/ACEPTARLICENCIASQL=1` deja aceptada su licencia: sin ese parámetro una instalación
desatendida nunca instala el motor.

## Cómo se construye

```bat
powershell -ExecutionPolicy Bypass -File instalador\construir.ps1
```

Requiere el SDK de .NET (8 o superior), Inno Setup 6
(`winget install JRSoftware.InnoSetup`) y los medios de SQL Server Express en
`instalador\motor\SQLServerExpress` (o en la carpeta que indique
`-MediosSqlExpress_704ILR`). El guion publica la aplicación para Windows de 64 bits con
.NET 8 incluido, comprueba que los medios estén completos y firmados por Microsoft,
compila `EvenTech.iss` y deja `salida\EvenTech_Instalador_v<versión>.exe` (unos 380 MB:
SQL Server Express ocupa 326), del que informa el tamaño y el SHA-256. Las carpetas
`publicado`, `salida` y `motor` se generan o se copian y no se versionan.

**Los medios de SQL Server Express** son la carpeta que deja el instalador web de
Microsoft (`SQL2022-SSEI-Expr.exe`, opción *Descargar medios* > *Express Core*) al
extraer su paquete `SQLEXPR_x64_ENU.exe` (`SQLEXPR_x64_ENU.exe /x:<carpeta>`), sin
modificar: `SETUP.EXE`, `MEDIAINFO.XML` y sus subcarpetas. El guion se detiene si falta
alguno, si un ejecutable o paquete no tiene una firma válida de Microsoft o si la
carpeta trae `SqlServerInstallConfig.ini` (lo escribe una instalación local y lleva la
cuenta de ese equipo). La licencia de SQL Server Express permite distribuirlo dentro de
un programa propio (sección 2.b de sus términos): el programa le agrega funcionalidad
principal, el instalador exige aceptar la licencia de Microsoft antes de instalarlo, los
medios viajan sin modificar y el programa muestra su aviso de derechos de autor.
`-SinMotor_704ILR` arma un instalador sin el motor, solo para pruebas.

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
| `scripts/BaseDeDatos.ps1` | Diagnóstico de la instancia y de la base existente, creación de la base (respaldo, esquema o base vacía), actualización y reemplazo con respaldo previo, reversión, verificación, deshacer y las cuentas que administran la instancia que se crea |
| `scripts/Conexion.ps1` | Guarda la conexión cifrada en el perfil del usuario |
| `recursos/` | Imágenes del asistente, con la marca del sistema, en tres escalas de pantalla |
| `construir.ps1` | Publica la aplicación y compila el instalador |

## Decisiones de diseño

- **Sin permisos de administrador.** El asistente se compila con
  `PrivilegesRequired=lowest`: instala en la carpeta de programas del usuario y
  registra la instalación para ese usuario (`HKEY_CURRENT_USER`), igual que la 2.1;
  por ahí la reconoce la reinstalación. Todo lo que el sistema escribe va a carpetas con
  permiso de escritura (`%APPDATA%`, `%ProgramData%`, `%LOCALAPPDATA%`); nada al lado del
  ejecutable. Si se elige una carpeta donde el usuario no puede escribir (Archivos de
  programa, por ejemplo), el asistente lo dice antes de seguir.
- **SQL Server Express incluido, con elevación solo para ese paso.** El instalador de
  SQL Server se lanza pidiendo la autorización a Windows (UAC). Quedan como
  administradores de la instancia el grupo de administradores del equipo (buscado por
  su identificador, porque su nombre cambia con el idioma de Windows) y quien inició
  la instalación, que es quien va a usar el sistema. Antes se comprueban .NET Framework
  4.7.2 y 6 GB libres; un reinicio pendiente de Windows se informa.
- **La base se prepara antes de copiar la aplicación.** Es el único paso que depende
  del entorno. Si falla, los archivos instalados no se tocan (en una reinstalación, la
  versión anterior sigue funcionando con su base, que volvió al respaldo) y se puede
  volver atrás.
- **Cancelar deshace todo.** Si la instalación se cancela mientras se copian los
  archivos, el asistente quita la base que acababa de crear (solo la que tiene la fecha
  de creación de esa ejecución; si era una base vacía existente, la deja vacía) o, en
  una reinstalación, devuelve la base al respaldo que tomó antes de modificarla. SQL
  Server Express, si se lo instaló, queda instalado.
- **Nunca se modifica una base sin respaldo.** Actualizar y reemplazar empiezan por un
  respaldo completo con suma de verificación (`RESTORE VERIFYONLY`), que queda en la
  carpeta de respaldos de la instancia como red de seguridad (lo escribe la cuenta del
  servicio de SQL Server: no hace falta ser administrador). Con otras conexiones
  abiertas sobre la base (el sistema abierto en otro equipo), no se respalda ni se
  migra. La instalación inicial sigue sin tocar una base existente: la comprobación va
  en el mismo lote que la restauración.
- **El respaldo de demostración, donde el motor lo pueda leer.** Quien lo lee es la
  cuenta del servicio de SQL Server, que no puede entrar a la carpeta temporal del
  usuario. El asistente lo deja en `%ProgramData%\EvenTech\instalacion_<fecha>`, con
  permiso de lectura para todos, y lo borra al terminar. Si quien instala puede escribir
  en la carpeta de respaldos de la instancia, se usa esa.
- **La migración es la del propio esquema.** `db/schema.sql` es idempotente: sobre una
  base 2.x agrega los permisos, las traducciones y los objetos de la 3.0; sobre una base
  3.0 no cambia nada (se comprobó con la huella de cada tabla). El asistente lo ejecuta
  lote por lote en una sola sesión, con el proveedor de SQL Server de Windows.
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
- **SQL Server Express incluido.** Quien instala es el usuario final promedio, que no
  sabe qué es SQL Server: el instalador lo lleva adentro y lo instala sin que haga falta
  internet. Los medios se extraen recién cuando hay que instalarlo (van en su propio
  bloque, sin comprimir, y no demoran el resto). Se instala solo el motor, con los
  parámetros documentados de la instalación desde la línea de comandos: instancia
  `SQLEXPRESS` (la que la aplicación espera de fábrica; si ya existe, `EVENTECH`), sin
  conexiones de red (`/TCPENABLED=0 /NPENABLED=0`: el sistema corre en el mismo equipo),
  inicio automático del servicio y sin buscar actualizaciones durante la instalación.
  Administran la instancia el grupo de administradores del equipo —buscado por su
  identificador, porque en un Windows en español se llama `BUILTIN\Administradores`— y
  quien inició la instalación. Antes de empezar se comprueba lo que se puede ver desde el
  asistente (.NET Framework 4.7.2 y 6 GB libres); si el instalador de SQL Server falla con
  un reinicio pendiente de Windows, lo dice; si pide reiniciar al terminar y la instancia
  ya responde, la instalación sigue y la página final ofrece reiniciar. Si se cancela
  después, SQL Server Express queda instalado (se quita desde *Aplicaciones* de Windows),
  y la desinstalación del sistema tampoco lo quita.
- **Medios junto al instalador.** Una carpeta `SQLServerExpress` al lado del instalador
  tiene prioridad sobre los medios incluidos (sirve para las pruebas con un doble del
  instalador de SQL Server).

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
| `SIN_PERMISO_BASE` | El usuario no es propietario de la base existente ni administrador de la instancia | Que instale con un usuario que pueda respaldarla y actualizarla |
| `BASE_EXISTENTE` | La base apareció (o la base vacía se llenó) entre la comprobación y la creación | Que vuelva atrás para elegir qué hacer con ella |
| `BASE_AJENA` | La instancia tiene una base `EvenTechDB` que no es del sistema | Que elija otra instancia: esa base no se toca |
| `BASE_MAS_NUEVA` | La base es de una versión posterior a la del asistente | Que use el instalador de esa versión o de una posterior |
| `BASE_EN_USO` | Hay conexiones abiertas sobre la base | Que cierre el sistema en todos los equipos y vuelva a intentar |
| `ACTUALIZACION_FALLIDA` | La migración de la base falló | El detalle, y que la base volvió al respaldo (o dónde quedó el respaldo si no pudo volver) |
| `RESTAURACION_FALLIDA` | El motor no pudo crear o restaurar la base | El detalle que informó SQL Server y la ruta del registro |
| `ERROR_NO_PREVISTO` | La comprobación de la instancia falló por una causa no prevista | Que no se pudo comprobar la instancia, con el detalle |
| `CONEXION_NO_GUARDADA` | Al terminar, no se pudo guardar `connection.cfg` | Que al abrir el sistema elija la misma instancia en la pantalla de conexión |
| `SIN_ACCESO_A_LA_BASE` | Al terminar, la cuenta que inició la instalación no puede abrir la base | Que el administrador de SQL Server le dé acceso a esa base |

Al instalar SQL Server Express, además:

| Situación | Qué ve quien instala |
|---|---|
| No aceptó la licencia de Microsoft | Que hay que aceptarla para instalar el motor |
| Windows sin .NET Framework 4.7.2 | Que actualice Windows con Windows Update |
| Menos de 6 GB libres en el disco del sistema | Cuánto espacio queda y que libere espacio |
| El instalador de SQL Server falló con un reinicio pendiente de Windows | Que reinicie el equipo y vuelva a ejecutar el asistente, con el código y la carpeta del registro de SQL Server |
| El instalador de SQL Server falló | El código y la carpeta del registro de SQL Server |
| SQL Server pidió reiniciar y la instancia todavía no responde | Que reinicie el equipo y vuelva a ejecutar el asistente |
| SQL Server terminó sin error pero la instancia no aparece | Que verifique la instalación de SQL Server |
| Una instancia existente no sirve | El motivo, y que puede marcar la opción de instalar SQL Server Express |
