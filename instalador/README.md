# Instalador de EvenTech

Asistente de instalación del sistema para el usuario final. En un equipo que todavía
no tiene EvenTech:

1. copia la aplicación, que lleva incluido el entorno de ejecución de .NET 8;
2. crea la base de datos `EvenTechDB`, con sus datos de demostración, en la
   instancia de SQL Server que elige quien instala;
3. deja configurada la conexión de la aplicación a esa instancia.

Quien instala no necesita abrir una consola, tener `sqlcmd` ni escribir una cadena
de conexión: el asistente detecta las instancias de SQL Server del equipo y las
ofrece en una lista.

## Alcance de esta versión

Es la **primera versión** del instalador y realiza la **instalación inicial**. No
modifica una base de datos existente: si la instancia elegida ya tiene una base
`EvenTechDB`, lo informa y pide elegir otra instancia. La reinstalación —detectar la
versión instalada, mostrar la actualización entre lo instalado y lo nuevo y conservar
los datos— corresponde a la versión siguiente. Para eso esta versión ya deja
registrados, junto con la instalación, la versión instalada y la instancia elegida.

## Requisitos del equipo donde se instala

| Componente | Requisito |
|---|---|
| Sistema operativo | Windows 10 u 11 de 64 bits |
| Motor de base de datos | SQL Server Express 2019 o superior, instalado en el mismo equipo y con su servicio iniciado |
| Usuario de Windows | Administrador del equipo y de la instancia de SQL Server (quien instaló SQL Server lo es) |
| Espacio en disco | 170 MB para la aplicación y unos 80 MB para la base, en la carpeta de datos de SQL Server |
| .NET | No se requiere: viaja incluido en el instalador |

## Pasos del asistente

| Paso | Qué hace |
|---|---|
| Bienvenida | Presenta lo que se va a instalar |
| Carpeta de destino | Por defecto, `C:\Program Files\EvenTech` |
| Instancia de SQL Server | Lista las instancias detectadas; al avanzar comprueba la elegida |
| Tareas adicionales | Acceso directo en el escritorio |
| Listo para instalar | Resumen: carpeta, instancia (con su versión) y base |
| Creación de la base de datos | Restaura el respaldo `db/EvenTechDB.bak` en la instancia |
| Instalando | Copia la aplicación y crea los accesos directos |
| Finalización | Guarda la conexión, comprueba el acceso a la base y ofrece abrir el sistema |

Al terminar quedan la aplicación en la carpeta elegida, los accesos directos del
menú Inicio y del escritorio, la entrada *EvenTech 2.0* en las aplicaciones
instaladas de Windows, la base `EvenTechDB` en la instancia y el archivo
`%APPDATA%\EvenTech\connection.cfg` con la conexión cifrada. El primer ingreso se
hace con `admin` / `admin123`; las demás cuentas de demostración figuran en
[`db/README.md`](../db/README.md).

La desinstalación quita la aplicación, los accesos directos y la entrada de Windows.
**La base de datos no se elimina**: sus datos quedan en SQL Server.

### Instalación desatendida

```bat
EvenTech_Instalador_v2.0.exe /VERYSILENT /SUPPRESSMSGBOXES /INSTANCIA=localhost\SQLEXPRESS /LOG=instalacion.log
```

Termina con código 0 si instaló y con otro código si la instancia no sirve; el motivo
queda en el registro.

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
lugar, la propiedad `Version` de `EvenTech.UI/EvenTech.UI.csproj`.

El respaldo `db/EvenTechDB.bak` viaja **dentro** del instalador. Como sus
cotizaciones y reservas pendientes vencen por fecha (regla RN-01), el instalador se
construye después de regenerar el respaldo.

| Ruta | Contenido |
|---|---|
| `EvenTech.iss` | Guion de Inno Setup: páginas, textos y lógica del asistente (UTF-8 con BOM) |
| `scripts/BaseDeDatos.ps1` | Diagnóstico de la instancia, restauración, verificación y deshacer |
| `scripts/Conexion.ps1` | Guarda la conexión cifrada en el perfil del usuario |
| `recursos/` | Imágenes del asistente, con la marca del sistema, en tres escalas de pantalla |
| `construir.ps1` | Publica la aplicación y compila el instalador |

## Decisiones de diseño

- **La base se crea antes de copiar la aplicación.** Es el único paso que depende
  del entorno. Si falla, el equipo queda como estaba y se puede volver atrás para
  elegir otra instancia.
- **Cancelar deshace todo.** Si la instalación se cancela mientras se copian los
  archivos, el asistente revierte la copia y quita la base que acababa de crear. Solo
  elimina la base cuya fecha de creación es la de esa ejecución.
- **Una base existente no se toca.** La comprobación de que la base no existe se
  repite en el mismo lote que la restauración: restaurar la misma base sobre sí misma
  no exige confirmación del motor cuando está en modo de recuperación simple.
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
| `BASE_EXISTENTE` | Ya hay una base `EvenTechDB` | Que esta versión no modifica una base existente |
| `RESTAURACION_FALLIDA` | El motor no pudo restaurar | El detalle que informó SQL Server y la ruta del registro |
