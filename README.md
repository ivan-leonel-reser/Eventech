# EvenTech

Sistema de gestión operativa de salones de fiestas y eventos: reservas y
cotizaciones, clientes, servicios contratados, cobros y comprobantes, y la
coordinación operativa de cada evento (personal, confirmación de disponibilidad,
cronograma, tareas, ejecución e incidencias), con seguridad, auditoría e
integridad de datos de forma transversal.

Aplicación de escritorio **WinForms sobre .NET 8** con **SQL Server Express**,
organizada en cinco capas y sin frameworks de persistencia de terceros.

> **Trabajo de Diploma** — Reser, Iván Leonel (DNI 38.823.704, Legajo
> A0900013691-T1). Comisión 3-B-N, Sede Centro, 2026.
> Clases, métodos, propiedades, campos, variables y miembros de enumerados de
> resultado llevan el sufijo de autoría `_704ILR`. Quedan sin sufijo, a propósito,
> las tablas y columnas de la base, los valores que se persisten como dato (estados
> de la reserva, de coordinación y de asignación, prioridades de tarea, tipos y
> estados de incidencia, criticidades, acciones de la auditoría de acceso, claves
> de permisos y de traducciones) y lo que el framework no permite renombrar.

---

## Instalación

El sistema se instala con el asistente `EvenTech_Instalador_v2.0.exe`, pensado para
el usuario final: copia la aplicación —con .NET 8 incluido—, crea la base de datos
con los datos de demostración en la instancia de SQL Server que se elija y deja
configurada la conexión. El equipo solo necesita Windows 10 u 11 de 64 bits y SQL
Server Express 2019 o superior; no hacen falta `sqlcmd` ni el SDK de .NET.

El instalador se genera con `instalador\construir.ps1`. Su alcance, sus pasos y sus
decisiones de diseño están en **[instalador/README.md](instalador/README.md)**; el
paso a paso con capturas, en el manual de instalación que acompaña la entrega.

Las secciones siguientes describen la puesta en marcha **desde el código fuente**.

## Requisitos

| Componente | Versión mínima |
|---|---|
| Sistema operativo | Windows 10 (x64) |
| Runtime | .NET 8 Desktop Runtime (el instalador lo lleva incluido) |
| Motor de base de datos | SQL Server Express 2019 (MSSQL15) |
| Herramienta de línea de comandos | `sqlcmd` (ver nota) |
| Para compilar | .NET 8 SDK |

`sqlcmd` **no viene con el motor**: se instala con SQL Server Management Studio
(SSMS), con el paquete *Microsoft Command Line Utilities for SQL Server* (el
`sqlcmd` ODBC) o como `go-sqlcmd` (`winget install sqlcmd`). Cualquiera de los
tres acepta los parámetros que se usan abajo (`-E -C -b -i`); `-C` (confiar en el
certificado del servidor) exige una versión reciente. Si no se quiere instalar la
herramienta, `db\schema.sql` también se puede abrir y ejecutar desde SSMS sobre la
base ya creada.

Hardware sugerido: procesador Intel Core i5 o equivalente, 8 GB de RAM,
500 MB de disco y una resolución efectiva de 1366x768 o superior (la de la
pantalla dividida por la escala configurada en Windows).

## Puesta en marcha

### 1. Crear la base de datos

`db/schema.sql` es **idempotente**: crea las tablas que falten, aplica las
migraciones de columnas y de valores por defecto y siembra los datos base
(idiomas y traducciones, permisos y los seis perfiles, el usuario inicial y los
catálogos de ejemplo: salones, servicios, métodos de pago, especialidades del
personal y dos clientes). Se puede ejecutar varias veces: agrega lo que falte,
regulariza el vencimiento (`VenceEl`) de las operaciones anteriores y corrige los
textos de fábrica solo mientras conserven su valor original; las traducciones
editadas por el usuario se conservan, y un idioma agregado desde la aplicación
recibe —copiadas del español— las claves nuevas que todavía no tenga.

El script corre **sobre la base que indica `-d`**: no la crea ni cambia de
contexto, y aborta con error si `-d` falta (para no ejecutarse sobre `master`).
Por eso la base se crea aparte, en el primer comando:

```bat
sqlcmd -S localhost\SQLEXPRESS -d master -E -C -Q "IF DB_ID('EvenTechDB') IS NULL CREATE DATABASE EvenTechDB;"
sqlcmd -S localhost\SQLEXPRESS -d EvenTechDB -E -C -b -i db\schema.sql
```

`-b` corta la ejecución en el primer error, en lugar de seguir y dejar una base a
medias. `-I` (QUOTED_IDENTIFIER ON) es opcional: el script ya lo fija al inicio,
que es lo que exigen sus índices filtrados. El nombre `EvenTechDB` es el de la
cadena de fábrica; si se usa otro, se indica el mismo nombre en los dos comandos y
después en la pantalla de conexión de la aplicación. Conviene correrlo con la
aplicación cerrada: el script activa en la base las lecturas por versión
(`READ_COMMITTED_SNAPSHOT`) y, para hacerlo, deshace las transacciones que otra
sesión tenga abiertas.

Alternativamente se puede restaurar el snapshot con datos de prueba
`db/EvenTechDB.bak`. El procedimiento completo, con las dos opciones y sus
advertencias, está en **[db/README.md](db/README.md)**.

### 2. Compilar y ejecutar

```bat
dotnet build EvenTech.sln
```

o `_build.bat`, que deja el resultado en `_build_log.txt`. El ejecutable queda
en `EvenTech.UI\bin\Debug\net8.0-windows\EvenTech.UI.exe`.

### 3. Credencial inicial

| Usuario | Contraseña | Perfil |
|---|---|---|
| `admin` | `admin123` | Administrador (acceso total) |

Es la única cuenta que siembra `schema.sql`. El snapshot `db/EvenTechDB.bak`
suma además quince cuentas de demostración, todas con contraseña `demo123`, para
mostrar el menú según el perfil y recorrer el proceso completo de un evento:

| Usuario | Perfil |
|---|---|
| `dsosa` | Vendedor |
| `mojeda` | Supervisor (incluye a Vendedor; supervisa la ejecución de los eventos) |
| `mgutierrez` | Gerencial (incluye a Supervisor) |
| `lbenitez` | Coordinador (personal, cronograma y tareas de cada evento) |
| `nferreyra`, `cluna`, `epaz`, `frojas`, `gmedina`, `jcastro`, `mherrera`, `rvega`, `smolina`, `pibarra`, `bsuarez` | Empleado (cada cuenta está vinculada a la ficha de su empleado: confirma sus turnos y consulta su agenda) |

La versión actual **no incluye el cambio de contraseña desde la aplicación**
(previsto para la iteración de gestión de usuarios, junto con la administración de
la cuenta propia). Si hace falta reemplazar una contraseña, se actualiza
`Users.PasswordHash` con el SHA-256 hexadecimal en minúsculas (64 caracteres) de
la contraseña nueva, que es el mismo formato que calcula la pantalla de acceso:

```bat
sqlcmd -S localhost\SQLEXPRESS -d EvenTechDB -E -C -Q "UPDATE dbo.Users SET PasswordHash = '<sha256 hex>' WHERE Username = 'admin';"
```

**Cuenta bloqueada.** Tres contraseñas erróneas seguidas bloquean la cuenta y el
bloqueo no expira. Se levanta desde *Perfiles* (botón *Desbloquear*, requiere otro
usuario con el permiso `PERFILES_GESTION`, que solo tiene el Administrador). Si la
cuenta bloqueada es la única de Administrador, el desbloqueo es por SQL:

```bat
sqlcmd -S localhost\SQLEXPRESS -d EvenTechDB -E -C -Q "UPDATE dbo.Users SET Blocked = 0, FailedAttempts = 0 WHERE Username = 'admin';"
```

### Conexión a la base

La cadena de fábrica apunta a `localhost\SQLEXPRESS`, base `EvenTechDB`, con
seguridad integrada. Al arrancar, la aplicación prueba la cadena guardada (o la
de fábrica si no hay ninguna): que el servidor responda, que la base exista y que
tenga el esquema completo. Si algo falla, abre la pantalla de configuración antes
del login con el motivo, y ofrece en un combo las instancias detectadas en la
máquina (`sqlcmd -L`) más las instalaciones típicas —entre ellas
`localhost\SQLEXPRESS`— para elegir o tipear una; no las prueba por su cuenta.
Ahí se indican la instancia y el nombre de la base, se prueba la conexión y la
cadena resultante se guarda cifrada con DPAPI en `%APPDATA%\EvenTech\connection.cfg`.

Una base de una revisión anterior (con `Users` pero sin alguna tabla o columna
que la versión actual necesita) se rechaza con el detalle de lo que falta: se
completa corriendo `db\schema.sql` sobre esa base y se vuelve a probar.

## Arquitectura

```
EvenTech.sln
├── EvenTech.BE          entidades de negocio (BE_*)
├── EvenTech.DAL         acceso a datos, SQL parametrizado a mano (DAL_*)
├── EvenTech.BLL         reglas de negocio y validaciones (BLL_*)
├── EvenTech.Services    transversales: sesión, cifrado, idiomas, integridad
├── EvenTech.UI          WinForms: frmLogin, frmMain y UserControls por sección
└── EvenTech.SmokeTest   validación programática end-to-end contra la base real
```

Dependencias entre capas, tal como las declaran los `ProjectReference`:
`UI -> BLL, BE, Services` · `BLL -> DAL, BE, Services` · `DAL -> BE, Services` ·
`Services -> BE` · `BE` no referencia a ninguna otra.

Único paquete NuGet: `Microsoft.Data.SqlClient`, el proveedor de SQL Server que usa la
DAL (.NET 8 no trae ninguno en el framework). No contiene lógica del sistema: todo el SQL
está escrito a mano. DPAPI (`ProtectedData`) se toma del runtime de escritorio de .NET 8.

**Patrones aplicados:** Singleton (gestión de sesión: instancia única con
constructor privado y acceso sincronizado), Composite (árbol de perfiles y
permisos), Observer (cambio de idioma en caliente) y Memento (versiones de una
reserva, con restauración auditada). El hash de contraseñas y la comparación en
tiempo constante viven en clases estáticas de servicio, sin estado propio.

## Secciones y permisos

El menú lateral se arma con los permisos efectivos del perfil de la sesión: cada
sección aparece si el perfil tiene alguno de los permisos de las acciones que
viven en ella, y cada acción vuelve a exigir el suyo al ejecutarse.

| Sección | Qué se hace ahí | Permisos |
|---|---|---|
| Inicio | Portada de la sesión | (sin restricción) |
| Reservas | Cotizaciones y reservas, disponibilidad, servicios, cobros, comprobante, historial y versiones | `RESERVA_CREAR`, `RESERVA_EDITAR`, `RESERVA_HISTORIAL`, `RESERVA_RESTAURAR`, `DISPONIBILIDAD_CONSULTAR`, `PAGOS_REGISTRAR`, `PAGOS_ANULAR` |
| Clientes | Padrón de clientes | `CLIENTES_GESTION` |
| Servicios | Catálogo de servicios y precios | `SERVICIOS_GESTION` |
| Operaciones | Eventos confirmados: personal, cronograma, tareas y supervisión de la ejecución (un diálogo por cada uno) | `PERSONAL_ASIGNAR`, `CRONOGRAMA_GESTION`, `TAREAS_ASIGNAR`, `EJECUCION_SUPERVISAR` |
| Mi agenda | Turnos del empleado de la sesión: confirmar o rechazar, y consultar sus tareas y el cronograma | `DISPONIBILIDAD_CONFIRMAR`, `AGENDA_CONSULTAR` |
| Empleados | Ficha del personal, su especialidad y la cuenta con la que responde | `EMPLEADOS_GESTION` |
| Perfiles | Perfiles, permisos y asignación a usuarios | `PERFILES_GESTION` |
| Auditoría | Bitácora, auditoría de acceso y línea base de integridad | `BITACORA_VER`, `AUDIT_LOGIN_VER`, `INTEGRIDAD_RECALC` |

La gestión de idiomas (`IDIOMAS_GESTION`) se abre desde el selector de idioma del
pie de la ventana. Perfiles de fábrica: **Administrador** (todos los permisos),
**Vendedor** (disponibilidad, clientes, alta y edición de reservas, historial y
cobros), **Supervisor** (incluye a Vendedor y suma la bitácora, la auditoría de
acceso, la anulación de pagos y la supervisión de la ejecución), **Gerencial**
(incluye a Supervisor y suma el recálculo de la línea base), **Coordinador**
(empleados, personal, cronograma y tareas) y **Empleado** (confirmar
disponibilidad y consultar la agenda).

## Reglas de negocio implementadas

| Regla | Enunciado |
|---|---|
| RN-01 | Vigencia: una cotización vale 15 días corridos; una reserva PENDIENTE, 72 horas. Vencido el plazo, la operación no pasa a otro estado —salvo darse de baja— hasta que se renueve su vigencia. |
| RN-02 | Cancelación: con 30 días o más de antelación se reintegra el 100 %; con menos se retiene el 50 %. El sistema calcula, informa y asienta ambos importes. |
| RN-03 | Solo una reserva CONFIRMADA compromete el salón para la fecha del evento. |
| RN-04 | La suma de los pagos nunca supera el importe total, y una reserva cancelada no admite cobros. |
| RN-05 | Transiciones de estado: COTIZACION avanza a cualquier estado, PENDIENTE solo confirma o cancela, CONFIRMADA solo cancela (salvo con el evento en ejecución o cerrado, RN-13) y CANCELADA es terminal. |
| RN-06 | Al confirmar, el salón elegido tiene que poder alojar a la cantidad de invitados estimada. |
| RN-07 | Una reserva queda CONFIRMADA con el adelanto ya cobrado: el orden es guardar la operación, cobrar y recién entonces confirmar. |
| RN-08 | Solo se coordina el evento de una reserva CONFIRMADA. Si la reserva deja de estar confirmada o cambia de fecha, las confirmaciones de su personal vuelven a pendiente; si dejó de estar confirmada —por una cancelación o por la restauración de una versión no confirmada—, el equipo queda liberado. |
| RN-09 | La franja de un empleado no puede superponerse con otro turno suyo, pendiente o confirmado, en otro evento confirmado; tampoco cuando el turno cruza la medianoche. Un turno rechazado no lo compromete, y el control se repite al confirmar. |
| RN-10 | La disponibilidad la responde el propio empleado, desde la cuenta vinculada a su ficha; el rechazo lleva motivo. |
| RN-11 | El cronograma se genera con el equipo confirmado (al menos un confirmado y ninguna respuesta pendiente) y con responsables confirmados; hay uno solo por reserva. Uno ya generado se puede modificar con respuestas pendientes, siempre con responsables confirmados. |
| RN-12 | Las tareas se asignan sobre el cronograma del evento: sin cronograma no se asignan y un cronograma con tareas no se elimina. La tarea se asigna a personal confirmado, cae dentro de su turno y no se superpone con otra tarea del mismo empleado. |
| RN-13 | La ejecución empieza con el evento LISTO. Desde ese momento el plan y la reserva quedan congelados (los movimientos de cobro siguen admitidos), lo que se sale del plan se registra como incidencia y el evento se cierra con todas resueltas. |

El evento de una reserva confirmada tiene además un **estado de coordinación**,
independiente del estado comercial: `SIN_ASIGNAR` (sin personal), `EN_COORDINACION`
(falta una respuesta, hay un rechazo sin resolver o falta el cronograma), `LISTO`
(todo el equipo confirmado y el cronograma generado), `EN_EJECUCION` y `CERRADO`.

## Seguridad e integridad

- Contraseñas con hash SHA-256 aplicado en el cliente antes de salir de la interfaz.
- Email y teléfono de los clientes cifrados con AES-256 reversible; la clave se
  protege con DPAPI de máquina en `%ProgramData%\EvenTech\crypto.key`, así que un
  contacto cifrado se lee en el equipo que lo guardó.
- Cadena de conexión cifrada con DPAPI en el perfil del usuario.
- Dígitos verificadores sobre las reservas y sobre los pagos: el horizontal de
  cada fila y el vertical de cada tabla. Cada alta o modificación de una reserva y
  cada cobro guardan el horizontal en la misma transacción y recalculan el
  vertical; se verifican al arrancar, antes del login, y delatan una fila
  alterada, agregada o quitada por fuera del sistema.
- Permisos por perfil con **denegar por defecto** y doble control: la sección se
  oculta y la acción se vuelve a exigir al ejecutarse. Los permisos efectivos se
  resuelven al iniciar sesión y rigen durante toda ella: un cambio de perfil, de
  composición o de asignación se aplica desde el siguiente inicio de sesión del
  usuario afectado.
- Bitácora de toda operación relevante y de los rechazos por regla de negocio,
  y control de cambios campo por campo con la versión previa de cada reserva.
- Uso desde más de un puesto: las operaciones que validan y escriben sobre una
  misma reserva leen su cabecera con bloqueo y se ejecutan una detrás de la otra;
  las consultas leen el último dato confirmado sin esperar a quien escribe
  (`READ_COMMITTED_SNAPSHOT`).

## Pruebas

`EvenTech.SmokeTest` recorre el sistema end-to-end contra la base configurada:
login y auditoría, alta y modificación de reservas, control de cambios, árbol de
permisos, idiomas, integridad, memento, cifrado, configuración de conexión, el
flujo completo del RF1 y del RF2 (personal, asignación con control de
superposición, confirmación de disponibilidad, cronograma, tareas, ejecución con
incidencias, reprogramación y cancelación), las trece reglas de negocio, cobros
simultáneos, guardados simultáneos (el dígito verificador vertical se recalcula de
a un puesto por vez), el dígito verificador de los pagos (un pago alterado,
duplicado o quitado por fuera se detecta) y restauración de versiones. Son 53
casos numerados `[1]` a `[53]`, más un bloque `[limpieza]` y un `[cierre]` que
vuelve a verificar la integridad de toda la base al terminar: 55 casos y 880
verificaciones en total.

```bat
dotnet run --project EvenTech.SmokeTest
```

Cada verificación compara el valor obtenido con el esperado y marca la diferencia
con `<-- DIFIERE`; un caso que no puede correr por faltar un dato (catálogo vacío,
base de prueba que no se pudo crear) se declara **omitido**, no aprobado. El
resumen final informa verificaciones, fallos y casos ejecutados, y el proceso
devuelve un código de salida que sirve para automatizar:

| Código | Significado |
|---|---|
| `0` | todo aprobado |
| `1` | al menos una verificación falló o un caso terminó por excepción (o la base no respondió al empezar) |
| `2` | sin fallos, pero con casos omitidos (cobertura incompleta) |

**La corrida deja rastro.** La suite elimina al final su usuario, sus perfiles,
su cliente, su idioma, sus empleados y la coordinación de sus eventos
(asignaciones, cronogramas, tareas e incidencias), pero las reservas que crea quedan CANCELADAS con
sus líneas, pagos, versiones e historial (la aplicación no borra reservas), y se
suman los asientos de bitácora y de auditoría de acceso; el bloque `[limpieza]`
imprime el detalle. Por eso conviene correrla sobre una copia de la base o
restaurar `db\EvenTechDB.bak` después, y nunca sobre la base desde la que se va a
generar un snapshot. Supone el uso exclusivo de la base mientras corre, los
textos de fábrica sin editar y la credencial inicial `admin / admin123` (sin una
ficha de empleado vinculada). El caso `[51]` crea, ejecuta y cierra un evento con
la fecha del día, sobre un salón que esté libre hoy.

## Estructura del repositorio

| Ruta | Contenido |
|---|---|
| `db/schema.sql` | Esquema idempotente con migraciones y datos base (UTF-8 con BOM) |
| `db/EvenTechDB.bak` | Snapshot completo con datos de demostración |
| `db/README.md` | Procedimiento detallado de creación y restauración |
| `instalador/EvenTech.iss` | Guion del asistente de instalación (Inno Setup 6) |
| `instalador/scripts/` | Guiones del asistente: instancia, base de datos y conexión |
| `instalador/construir.ps1` | Publica la aplicación y compila el instalador |
| `instalador/README.md` | Alcance, pasos y construcción del instalador |
| `_build.bat` | Compilación con log en `_build_log.txt` |
