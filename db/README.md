# Base de datos — EvenTech

La cadena de fábrica apunta a `localhost\SQLEXPRESS`, base **`EvenTechDB`**, con
seguridad integrada; se arma en
`EvenTech.Services/ConfiguracionConexion_704ILR.cs`, que también guarda la cadena
elegida por el usuario (cifrada con DPAPI). `EvenTech.DAL/DAL_DB_Connection_704ILR.cs`
es el consumidor de esa cadena: al arrancar comprueba que el servidor responda, que
la base exista y que tenga el esquema completo. Si algo falla, la aplicación abre la
pantalla de configuración con el motivo y ofrece en un combo las instancias
detectadas en la máquina (`sqlcmd -L`) más las instalaciones típicas —entre ellas
`localhost\SQLEXPRESS`— para elegir o tipear una; no las prueba por su cuenta.

Hay dos formas de tener la base:

## Opción A — Recrear desde el script (recomendada, portable)

`schema.sql` es **idempotente**: crea las tablas que falten, aplica las migraciones
de columnas y de valores por defecto, y siembra los datos base (idiomas y
traducciones, permisos y perfiles, el usuario `admin` y los catálogos de ejemplo:
salones, servicios, métodos de pago, especialidades del personal y dos clientes).
Se puede correr varias veces: agrega lo que falte, regulariza el
vencimiento (`VenceEl`) de las operaciones anteriores desde su emisión (o desde el
último pase a PENDIENTE) y corrige los textos de fábrica solo mientras conserven su
valor original; las traducciones editadas por el usuario se conservan, y un idioma
agregado desde la aplicación recibe —copiadas del español, para traducirlas— las
claves nuevas que todavía no tenga.

El script corre **sobre la base que indica `-d`**: no crea la base ni hace `USE`,
y aborta con error si `-d` falta (para no ejecutarse sobre `master`). Por eso la
base se crea aparte, en el primer comando. Los comandos se corren **desde esta
carpeta** (`db`), que es donde está el script:

```bat
sqlcmd -S localhost\SQLEXPRESS -d master -E -C -Q "IF DB_ID('EvenTechDB') IS NULL CREATE DATABASE EvenTechDB;"
sqlcmd -S localhost\SQLEXPRESS -d EvenTechDB -E -C -b -i schema.sql
```

- `-b` corta la ejecución en el primer error en lugar de seguir y dejar la base a
  medias; sin `-b`, el script igual desactiva el resto (`NOEXEC`) si detecta que
  corre sobre una base del sistema.
- `-I` (QUOTED_IDENTIFIER ON) es opcional: el script ya lo fija al inicio, que es
  lo que exigen sus índices filtrados (`UX_Clientes_Dni`,
  `UX_Reservas_SalonFecha_Confirmada`, `UX_Empleados_UserId`). Sirve como
  alternativa si se ejecuta con una herramienta que no respete el `SET` inicial.
- Si la base se llama distinto de `EvenTechDB`, se usa el mismo nombre en los dos
  comandos y después en la pantalla de conexión de la aplicación.
- `schema.sql` está guardado como **UTF-8 con BOM**: así `sqlcmd` lee bien las
  tildes y la eñe de los textos sembrados. Si se lo edita, conservar esa
  codificación.
- El script activa en la base las **lecturas por versión**
  (`READ_COMMITTED_SNAPSHOT`): con más de un puesto, las consultas de las pantallas
  leen el último dato confirmado sin esperar a quien escribe, y las operaciones que
  validan y escriben se siguen ejecutando una detrás de la otra. Es una opción de la
  base, así que también viaja con el `.bak`. Para activarla, el script deshace las
  transacciones que otra sesión tenga abiertas sobre esa base: conviene correrlo con
  la aplicación cerrada.
- `sqlcmd` no viene con el motor: se instala con SSMS, con *Microsoft Command Line
  Utilities for SQL Server* o como `go-sqlcmd`. El script está verificado con el
  `sqlcmd` ODBC y con `go-sqlcmd`; también se puede ejecutar desde SSMS sobre la
  base ya creada. Si en SSMS se lo ejecuta por error con `master` seleccionada, la
  guarda corta con un error y el resto del script no se ejecuta; el último lote
  vuelve a habilitar la ejecución de la ventana (`SET NOEXEC OFF`), así que alcanza
  con elegir la base creada en el combo y volver a ejecutar el script.

> Un `CREATE TABLE` dentro de un `IF OBJECT_ID(...) IS NULL` solo se ejecuta la
> primera vez: editarlo **no** cambia una base ya creada. Toda corrección de una
> columna o de un `DEFAULT` tiene que llevar además su `ALTER` idempotente en la
> zona de migraciones del script.

Usuario inicial: **admin / admin123** (perfil Administrador, acceso total). Es la
única cuenta que siembra el script. Los perfiles sí los siembra: son **seis**
—Administrador, Vendedor, Supervisor (incluye a Vendedor), Gerencial (incluye a
Supervisor), Coordinador y Empleado—, con sus permisos y las dos inclusiones del
Composite. El script crea **27 tablas**: las veinte de seguridad, reservas,
auditoría e idiomas, y las siete de la coordinación de eventos (`Especialidades`,
`Empleados`, `AsignacionesPersonal`, `Cronogramas`, `CronogramaActividades`,
`Tareas` e `Incidencias`), más la columna `Reservas.EstadoCoordinacion`.

**Base de una revisión anterior.** La aplicación rechaza al conectar una base que
tenga `Users` pero no todas las tablas o columnas de la versión actual, e indica
qué falta. Se resuelve corriendo `schema.sql` sobre esa base (agrega solo lo que
falte) y volviendo a probar la conexión.

**Cuenta bloqueada.** Tres contraseñas erróneas seguidas bloquean la cuenta y el
bloqueo no expira. Se levanta desde *Perfiles* (botón *Desbloquear*, exige otro
usuario con `PERFILES_GESTION`, que solo tiene el Administrador). Si la bloqueada es
la única cuenta de Administrador, se desbloquea por SQL:

```bat
sqlcmd -S localhost\SQLEXPRESS -d EvenTechDB -E -C -Q "UPDATE dbo.Users SET Blocked = 0, FailedAttempts = 0 WHERE Username = 'admin';"
```

El cambio de contraseña desde la aplicación no forma parte de esta versión; el
procedimiento para reemplazar una contraseña por SQL está en el `README.md` de la
raíz.

## Datos cifrados (Email/Telefono de Clientes)

Email y Telefono de `Clientes` se guardan cifrados con AES-256 (prefijo `ENC:`,
ver `EvenTech.Services/CryptoService_704ILR.cs`). La clave se genera sola en el primer
uso y queda en `%ProgramData%\EvenTech\crypto.key`, protegida con DPAPI de la
**máquina**, y por eso **no viaja con el `.bak`**: un valor `ENC:` restaurado en
otra PC no se puede descifrar ahí y se mostraría tal cual.

Por eso **el snapshot que se entrega lleva los datos de contacto en texto plano**.
Al primer guardado de cada cliente, la aplicación los cifra con la clave local de
esa máquina: el cifrado se ejercita igual y los datos se leen desde el arranque.
La aplicación tampoco rechaza un valor que siga cifrado (no lo confunde con un
email mal escrito), de modo que la ficha del cliente nunca queda trabada.

## Opción B — Restaurar el snapshot completo (con datos)

`EvenTechDB.bak` es un backup full, **generado el 07/10/2026 a las 00:01**, con los
datos de demostración: 12 clientes (contactos en texto plano), 24 reservas
repartidas en los tres salones y los cuatro estados (11 CONFIRMADA, 6 COTIZACIÓN,
5 PENDIENTE y 2 CANCELADA), 95 líneas de servicios contratados, 11 pagos (toda
CONFIRMADA tiene su adelanto, RN-07), 15 versiones con 17 asientos de historial
de cambios y 155 asientos de bitácora de esas operaciones. La reserva #4 acumula
dos modificaciones, para recorrer su historial y sus versiones.

De la coordinación de eventos trae las 9 especialidades, 12 empleados (11 activos,
cada uno con su cuenta, y 1 dado de baja), 19 asignaciones de personal repartidas
en 4 eventos, 2 cronogramas con 12 actividades, 9 tareas y 2 incidencias (una
resuelta y una abierta). Los once eventos confirmados muestran cuatro de los cinco
estados de coordinación: 1 EN_EJECUCION (la reserva #1, con el plan congelado y una
incidencia abierta), 1 LISTO (la #2, para iniciar su ejecución), 2 EN_COORDINACION
(la #3, con un turno pendiente y uno rechazado, y la #6, con el equipo confirmado
y sin cronograma) y 7 SIN_ASIGNAR. Ninguno está CERRADO: el cierre se recorre
resolviendo la incidencia abierta de la reserva #1 y cerrando ese evento desde
*Operaciones → Supervisión*.

Trae los seis perfiles —Administrador, Vendedor, Supervisor (incluye a Vendedor),
Gerencial (incluye a Supervisor), Coordinador y Empleado— y 16 cuentas:

| Usuario | Contraseña | Perfil |
|---|---|---|
| `admin` | `admin123` | Administrador |
| `dsosa` | `demo123` | Vendedor |
| `mojeda` | `demo123` | Supervisor (supervisa la ejecución de los eventos) |
| `mgutierrez` | `demo123` | Gerencial |
| `lbenitez` | `demo123` | Coordinador (personal, cronograma y tareas) |
| `nferreyra`, `cluna`, `epaz`, `frojas`, `gmedina`, `jcastro`, `mherrera`, `rvega`, `smolina`, `pibarra`, `bsuarez` | `demo123` | Empleado (cada cuenta está vinculada a la ficha de su empleado) |

Para recorrer la respuesta de un turno, `jcastro` tiene uno pendiente en la
reserva #3; `rvega` es quien rechazó el suyo en ese mismo evento.

Los datos se cargaron a través de la capa de negocio, así que respetan las reglas
RN-01 a RN-13 y llevan sus dígitos verificadores calculados; la verificación de
integridad del arranque da `Ok`.

**Vigencia de las operaciones del snapshot (RN-01).** Las cotizaciones y las
reservas PENDIENTE tienen el plazo que fija la RN-01: 15 días y 72 horas desde esa
fecha de generación. Restaurado días después, figuran vencidas en la columna
*Vence*. No es un defecto: al intentar avanzarlas el sistema lo informa y ofrece
renovar la vigencia en el acto (CUN005, flujo 6.2). El orden para confirmar una
PENDIENTE es siempre cobrar el adelanto (*Pagos*) y después confirmar (RN-07).

Antes de restaurar, dos pasos que evitan los dos errores más comunes:

1. **Dejar el `.bak` donde el motor pueda leerlo.** Quien abre el archivo no es el
   usuario que corre el comando sino la cuenta de servicio de SQL Server, que
   normalmente no tiene acceso a las carpetas del perfil del usuario (Descargas,
   Documentos, Escritorio). Restaurando desde ahí el motor devuelve
   `Msg 3201 ... Operating system error 5 (Acceso denegado)`, que **no** significa
   que el backup esté dañado. La salida más simple es copiar `EvenTechDB.bak` a la
   carpeta de backups de la instancia —`SELECT SERVERPROPERTY('InstanceDefaultBackupPath')`—
   y restaurar desde ahí; la alternativa es dar permiso de lectura sobre la carpeta
   del repositorio a `NT Service\MSSQL$SQLEXPRESS`.
2. **Confirmar los nombres lógicos** antes de armar los `MOVE`:

```sql
RESTORE FILELISTONLY FROM DISK = N'C:\ruta\al\.bak\EvenTechDB.bak';
-- Devuelve los nombres lógicos a usar: EvenTechDB y EvenTechDB_log.
```

Restaurar:

```sql
RESTORE DATABASE EvenTechDB
FROM DISK = N'C:\ruta\al\repo\db\EvenTechDB.bak'
WITH MOVE 'EvenTechDB'     TO N'C:\...\MSSQL\DATA\EvenTechDB.mdf',
     MOVE 'EvenTechDB_log' TO N'C:\...\MSSQL\DATA\EvenTechDB_log.ldf',
     REPLACE;
```

(Ajustar las rutas `MOVE` a la carpeta DATA de la instancia local; ver
`SELECT SERVERPROPERTY('InstanceDefaultDataPath')`.) Requiere SQL Server de
igual o mayor versión que el de origen (SQL Server Express 2019 / MSSQL15).

Si el `.bak` restaurado fuera anterior al `schema.sql` vigente, correr el script
sobre la base restaurada (Opción A, segundo comando): agrega solo lo que falte. La
aplicación lo exige al conectar solo cuando faltan tablas o columnas; las
traducciones y restricciones nuevas no se controlan, así que el script se corre igual.
