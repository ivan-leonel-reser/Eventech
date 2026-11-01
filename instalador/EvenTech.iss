; =====================================================================================
;  EvenTech - asistente de instalacion y reinstalacion (segunda version)
;
;  Instalacion inicial, en un equipo que todavia no tiene el sistema:
;    1. copia la aplicacion, que lleva incluido el entorno de ejecucion de .NET 8;
;    2. crea la base de datos del sistema, con sus datos de demostracion, en la
;       instancia de SQL Server que elige quien instala;
;    3. deja configurada la conexion de la aplicacion a esa instancia.
;
;  Reinstalacion, cuando el equipo ya tiene una version anterior (la reconoce por el
;  identificador del producto, AppId) o la instancia elegida ya tiene la base:
;    - muestra lo que esta (version instalada, version y contenido de la base) y lo
;      que viene (la version nueva y sus novedades);
;    - por defecto conserva los datos: respalda la base y le aplica el esquema nuevo
;      (db\schema.sql, idempotente); si algo falla, la base vuelve al respaldo;
;    - opcionalmente, la reemplaza por la de demostracion, tambien con respaldo previo;
;    - conserva la conexion guardada, la clave de cifrado y las preferencias.
;
;  Si el equipo no tiene ninguna instancia de SQL Server y el paquete trae los medios
;  de SQL Server Express (carpeta SQLServerExpress junto al instalador), ofrece
;  instalar el motor en modo silencioso, previa aceptacion de su licencia.
;
;  Quien instala es el usuario final: no se le pide ninguna herramienta de linea de
;  comandos ni escribir una cadena de conexion.
;
;  Se compila con Inno Setup 6 por medio de construir.ps1, que antes publica la
;  aplicacion en la carpeta "publicado". Este archivo esta guardado como UTF-8 con
;  marca de orden de bytes (los textos llevan tildes): conservar esa codificacion.
; =====================================================================================

#define Nombre_704ILR      "EvenTech"
#define Ejecutable_704ILR  "EvenTech.UI.exe"
#define Autor_704ILR       "Ivan Leonel Reser"
#define Publicado_704ILR   SourcePath + "publicado"
; Mutex que la aplicacion mantiene mientras esta abierta (Program_704ILR.NombreMutex_704ILR).
#define Mutex_704ILR       "EvenTech_704ILR_Aplicacion"

#if !FileExists(Publicado_704ILR + "\" + Ejecutable_704ILR)
  #error No se encuentra la aplicacion publicada. Ejecutar construir.ps1, que la publica antes de compilar.
#endif

; La version se lee del ejecutable publicado (propiedad Version de Directory.Build.props).
#define Mayor_704ILR
#define Menor_704ILR
#define Revision_704ILR
#define Compilacion_704ILR
#expr GetVersionComponents(Publicado_704ILR + "\" + Ejecutable_704ILR, Mayor_704ILR, Menor_704ILR, Revision_704ILR, Compilacion_704ILR)
#define Version_704ILR Str(Mayor_704ILR) + "." + Str(Menor_704ILR)

[Setup]
; Identificador del producto: es el mismo en todas las versiones. Por el reconoce la
; reinstalacion que el sistema ya esta instalado y con que version.
AppId={{1818AFF2-E854-4740-9D17-41F03F240A62}
AppName={#Nombre_704ILR}
AppVersion={#Version_704ILR}
AppVerName={#Nombre_704ILR} {#Version_704ILR}
AppPublisher={#Autor_704ILR}
; Con el sistema abierto (en esta u otra sesion del equipo) no se actualiza nada.
AppMutex={#Mutex_704ILR},Global\{#Mutex_704ILR}
VersionInfoVersion={#Mayor_704ILR}.{#Menor_704ILR}.{#Revision_704ILR}.{#Compilacion_704ILR}
VersionInfoProductName={#Nombre_704ILR}
VersionInfoDescription=Instalador de {#Nombre_704ILR}
DefaultDirName={autopf}\{#Nombre_704ILR}
DefaultGroupName={#Nombre_704ILR}
UninstallDisplayName={#Nombre_704ILR} {#Version_704ILR}
UninstallDisplayIcon={app}\{#Ejecutable_704ILR}
; Pasos del asistente: bienvenida, carpeta de destino (solo en la instalacion inicial:
; la reinstalacion usa la carpeta de la version anterior), instancia de SQL Server,
; base de datos existente (solo si la hay), accesos directos, resumen, instalacion y
; finalizacion.
DisableWelcomePage=no
DisableDirPage=auto
DisableProgramGroupPage=yes
ShowLanguageDialog=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\EvenTech.UI\Assets\eventech.ico
WizardImageFile=recursos\asistente_lateral.bmp,recursos\asistente_lateral_150.bmp,recursos\asistente_lateral_200.bmp
WizardSmallImageFile=recursos\asistente_esquina.bmp,recursos\asistente_esquina_150.bmp,recursos\asistente_esquina_200.bmp
Compression=lzma2/max
SolidCompression=yes
SetupLogging=yes
OutputDir=salida
OutputBaseFilename=EvenTech_Instalador_v{#Version_704ILR}

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
es.WelcomeLabel2=Este asistente instalará [name/ver] en este equipo: copia la aplicación, crea o actualiza la base de datos del sistema en la instancia de SQL Server que se elija y deja configurada la conexión.%n%nSe recomienda cerrar las demás aplicaciones antes de continuar.
es.FinishedLabel=La instalación de [name] finalizó: la aplicación quedó instalada, su base de datos creada y la conexión configurada.%n%nPara el primer ingreso se utiliza el usuario admin con la contraseña admin123.
es.FinishedLabelNoIcons=La instalación de [name] finalizó: la aplicación quedó instalada, su base de datos creada y la conexión configurada.%n%nPara el primer ingreso se utiliza el usuario admin con la contraseña admin123.
es.ConfirmUninstall=¿Desea quitar %1 de este equipo?%n%nLa base de datos EvenTechDB no se elimina: sus datos se conservan en SQL Server. Tampoco se eliminan la clave de cifrado ni el registro de excepciones del equipo.
es.SetupAppRunningError=El asistente detectó que %1 está abierto en este equipo.%n%nCierre todas las ventanas del sistema y presione Aceptar para continuar, o Cancelar para salir.

[CustomMessages]
es.InstanciaTitulo=Instancia de SQL Server
es.InstanciaDescripcion=¿En qué instancia está o se crea la base de datos del sistema?
es.InstanciaIntro=El asistente detectó las instancias de SQL Server instaladas en este equipo. Seleccione de la lista aquella en la que se creará la base de datos %1 (o la que ya la tiene), o escriba su nombre.
es.InstanciaEtiqueta=&Instancia de SQL Server:
es.InstanciaNota=Si la instancia ya tiene la base %1, el paso siguiente permite conservar sus datos y actualizarla. La conexión utiliza la autenticación integrada de Windows: no se solicita usuario ni contraseña.
es.InstanciaNinguna=No se detectó ninguna instancia de SQL Server en este equipo. %1 requiere SQL Server Express 2019 o superior: se lo puede instalar y volver a ejecutar este asistente, o escribir el nombre de la instancia si ya está instalada.
es.InstanciaEnlace=Descargar SQL Server Express desde el sitio de Microsoft
es.InstanciaVacia=Debe seleccionar o escribir una instancia de SQL Server.
es.InstanciaInvalida=El nombre de la instancia no puede contener comillas.
es.MotorIncluido=Instalar ahora SQL Server Express (incluido en este paquete) en la instancia «%1»
es.MotorLicencia=Acepto los términos de licencia de Microsoft SQL Server Express
es.MotorLicenciaEnlace=Leer los términos de licencia de SQL Server
es.MotorFaltaLicencia=Para instalar SQL Server Express hay que aceptar los términos de licencia de Microsoft.
es.MotorProgresoTitulo=Instalación de SQL Server Express
es.MotorProgresoDescripcion=El asistente está instalando el motor de base de datos.
es.MotorProgresoTexto=Instalando SQL Server Express en la instancia %1...
es.MotorProgresoNota=Puede demorar varios minutos. No apague el equipo.
es.MotorFallo=La instalación de SQL Server Express terminó con el código %1 y la instancia no quedó disponible.%n%nEl detalle quedó en el registro de la instalación de SQL Server (%2).
es.MotorReinicio=SQL Server Express quedó instalado, pero Windows pide reiniciar el equipo antes de usarlo. Reinicie el equipo y vuelva a ejecutar este asistente.
es.MotorNoDetectado=El instalador de SQL Server Express terminó sin error, pero el asistente no detecta la instancia «%1». Verifique la instalación de SQL Server y vuelva a ejecutar este asistente.
es.ErrSinConexion=No se pudo establecer la conexión con la instancia «%1».%n%nVerifique que el nombre esté bien escrito y que el servicio de SQL Server esté iniciado.
es.ErrInstanciaRemota=La instancia «%1» pertenece a otro equipo (%2).%n%nEste asistente trabaja con una instancia de SQL Server instalada en este mismo equipo.
es.ErrVersionAnterior=La instancia «%1» es %2.%n%nEl sistema requiere SQL Server 2019 o superior.
es.ErrSinPermiso=El usuario de Windows %2 no tiene permiso para crear ni restaurar bases de datos en la instancia «%1».%n%nEjecute el asistente con un usuario que sea administrador de esa instancia de SQL Server.
es.ErrBaseExistente=La instancia «%1» ya tiene una base de datos %2, creada entre la comprobación y la creación. Vuelva atrás para elegir qué hacer con ella.
es.ErrBaseAjena=La instancia «%1» tiene una base de datos %2 que no es del sistema.%n%nElija otra instancia: el asistente no modifica esa base.
es.ErrBaseMasNueva=La base de datos %2 de la instancia «%1» es de la versión %3, posterior a la que instala este asistente (%4).%n%nUse el instalador de esa versión o de una posterior.
es.ErrSinAccesoBase=No se pudo abrir la base de datos %2 de la instancia «%1».
es.ErrBaseEnUso=La base de datos %1 tiene %2 conexión(es) abierta(s): el sistema está abierto en otro equipo o hay otra herramienta conectada a la base.%n%nCierre esas conexiones y vuelva a intentar.
es.ErrNoPrevisto=No se pudo comprobar la instancia «%1».
es.ErrRestauracion=No se pudo crear la base de datos %1 en la instancia «%2». No se copió ningún archivo en el equipo.%n%nSe puede volver atrás para elegir otra instancia, o cancelar la instalación.
es.ErrActualizacion=No se pudo actualizar la base de datos %1 en la instancia «%2». Los archivos instalados no se modificaron.
es.ErrReemplazo=No se pudo reemplazar la base de datos %1 en la instancia «%2». Los archivos instalados no se modificaron.
es.BaseRestaurada=La base volvió al respaldo previo: quedó exactamente como estaba.
es.BaseNoRestaurada=ATENCIÓN: la base no pudo volver automáticamente al respaldo previo. El respaldo completo quedó en %1.
es.ErrDegradacion=Este equipo tiene instalado %1 %2, posterior a la versión de este asistente (%3).%n%nNo se instala una versión anterior sobre una posterior.
es.DetalleTecnico=Detalle informado por SQL Server:
es.RegistroEn=Registro de la instalación:
es.ProgresoTitulo=Base de datos
es.ProgresoDescripcion=El asistente está preparando la base de datos del sistema.
es.ProgresoTexto=Creando la base de datos %1 en la instancia %2...
es.ProgresoActualizar=Respaldando la base de datos %1 y actualizándola a la versión %2...
es.ProgresoReemplazar=Respaldando la base de datos %1 y reemplazándola por la de demostración...
es.ProgresoNota=Esta operación puede demorar unos segundos.
es.BienvenidaActualizacion=Este equipo tiene instalado %1 %2. El asistente lo actualizará a la versión %3: reemplaza la aplicación y actualiza la base de datos conservando sus datos (antes guarda un respaldo).%n%nCierre el sistema en todos los equipos antes de continuar.
es.BaseTitulo=Actualización de la base de datos
es.BaseDescripcion=Lo que está instalado y lo que viene con la versión nueva.
es.BaseInstalado=Instalado en este equipo: %1 %2, en %3.
es.BaseSinPrevia=No hay una versión anterior del sistema registrada en este equipo.
es.BaseExistente=Base de datos %1 en la instancia «%2»: versión %3, con %4 cuenta(s), %5 cliente(s) y %6 reserva(s).
es.BaseNueva=Versión que se instala: %1 %2. Novedades: informe de gestión exportable a PDF, registro de excepciones, ayuda en línea con la tecla F1 y material de apoyo para el usuario.
es.OpcionConservar=&Conservar los datos y actualizar la base a la versión %1 (recomendado)
es.OpcionConservarNota=Antes de actualizar se guarda un respaldo completo de la base en la carpeta de respaldos de SQL Server y, si algo falla, la base vuelve a ese respaldo. Se conservan las cuentas, los datos, la conexión configurada y la clave de cifrado de los contactos.
es.OpcionReemplazar=&Reemplazar la base por la de demostración
es.OpcionReemplazarNota=Antes de reemplazarla se guarda un respaldo completo de la base actual: sus datos dejan de estar en uso y quedan solo en ese respaldo.
es.ConfirmarReemplazo=La base de datos %1 se va a reemplazar por la de demostración. Los datos actuales quedarán solo en el respaldo que se guarda antes.%n%n¿Continuar?
es.MemoBase=Base de datos:
es.MemoInstancia=Instancia: %1 - %2
es.MemoNombre=Base: %1 (se crea con los datos de demostración)
es.MemoConservar=Base: %1, se conserva y se actualiza de la versión %2 a la %3 (con respaldo previo)
es.MemoReemplazar=Base: %1, se reemplaza por la de demostración (con respaldo previo de la actual)
es.MemoActualizacion=Actualización:
es.MemoVersiones=%1 %2 → %1 %3
es.FinActualizado=La actualización de [name] a la versión %1 finalizó: la aplicación quedó reemplazada y la base de datos actualizada, con todos sus datos.%n%nRespaldo de la base anterior: %2
es.FinReemplazado=La reinstalación de [name] %1 finalizó: la base de datos quedó con los datos de demostración.%n%nRespaldo de la base anterior: %2%n%nPara el primer ingreso se utiliza el usuario admin con la contraseña admin123.
es.AvisoConexion=La aplicación quedó instalada y la base de datos lista, pero no se pudo guardar la configuración de la conexión.%n%nAl iniciar %1 se abre la pantalla de configuración de la conexión: allí se elige la instancia «%2».
es.AvisoAcceso=La base de datos %1 está lista, pero el usuario de Windows que inició la instalación no pudo abrirla.%n%nEl administrador de SQL Server tiene que darle acceso a esa base para que pueda usar el sistema.

[Tasks]
Name: "escritorio"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Los archivos que usa el propio asistente van primero y en su propio bloque de
; compresion: con la compresion solida, extraer uno obligaria a descomprimir antes
; todo lo que lo precede.
Source: "scripts\BaseDeDatos.ps1"; Flags: dontcopy
Source: "scripts\Conexion.ps1"; Flags: dontcopy
Source: "..\db\schema.sql"; Flags: dontcopy
Source: "..\db\EvenTechDB.bak"; Flags: dontcopy
Source: "publicado\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs solidbreak

[Icons]
Name: "{group}\{#Nombre_704ILR}"; Filename: "{app}\{#Ejecutable_704ILR}"
Name: "{group}\Ayuda de {#Nombre_704ILR}"; Filename: "{app}\Ayuda\EvenTech.chm"
Name: "{group}\{cm:UninstallProgram,{#Nombre_704ILR}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#Nombre_704ILR}"; Filename: "{app}\{#Ejecutable_704ILR}"; Tasks: escritorio

[Run]
Filename: "{app}\{#Ejecutable_704ILR}"; Description: "{cm:LaunchProgram,{#Nombre_704ILR}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  BaseDeDatos_704ILR = 'EvenTechDB';
  // Clave del registro donde SQL Server anota las instancias instaladas en el equipo.
  ClaveInstancias_704ILR = 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL';
  // Clave de desinstalacion del sistema (AppId + "_is1"): ahi esta la version instalada.
  ClaveDesinstalacion_704ILR = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{1818AFF2-E854-4740-9D17-41F03F240A62}_is1';
  // La instancia a la que apunta de fabrica la aplicacion: si esta, se la propone.
  InstanciaDeFabrica_704ILR = 'localhost\SQLEXPRESS';
  DescargaDelMotor_704ILR = 'https://www.microsoft.com/es-es/sql-server/sql-server-downloads';
  LicenciaDelMotor_704ILR = 'https://aka.ms/useterms';
  GuionBase_704ILR = 'BaseDeDatos.ps1';
  GuionConexion_704ILR = 'Conexion.ps1';
  // Operacion sobre la base que decidio el asistente.
  OpCrear_704ILR = 'CREAR';
  OpActualizar_704ILR = 'ACTUALIZAR';
  OpReemplazar_704ILR = 'REEMPLAZAR';

var
  PaginaInstancia_704ILR: TWizardPage;
  ComboInstancia_704ILR: TNewComboBox;
  ChkMotor_704ILR, ChkLicencia_704ILR: TNewCheckBox;
  PaginaBase_704ILR: TWizardPage;
  LblResumenBase_704ILR: TNewStaticText;
  RadioConservar_704ILR, RadioReemplazar_704ILR: TNewRadioButton;
  PaginaProgreso_704ILR: TOutputProgressWizardPage;
  InstanciaElegida_704ILR: String;
  MotorDetectado_704ILR: String;
  // Instalacion previa del sistema en este equipo (vacio: no hay).
  VersionPrevia_704ILR, CarpetaPrevia_704ILR: String;
  // La instancia elegida ya tiene la base: su version y su contenido.
  BaseExiste_704ILR: Boolean;
  VersionBase_704ILR, CuentasBase_704ILR, ClientesBase_704ILR, ReservasBase_704ILR: String;
  Operacion_704ILR: String;
  // Fecha de creacion de la base que creo ESTA ejecucion. Vacia: no se creo ninguna.
  MarcaDeLaBase_704ILR: String;
  InstanciaDeLaBase_704ILR: String;
  // Respaldo que tomo ESTA ejecucion antes de actualizar o reemplazar la base.
  RespaldoPrevio_704ILR: String;
  BaseModificada_704ILR: Boolean;
  InstalacionCompleta_704ILR: Boolean;
  // Medios de SQL Server Express que trae el paquete (vacio: no trae).
  MediosDelMotor_704ILR: String;

// ------------------------------------------------------------------- utilidades

// "3.0" o "2.0.1" -> numero comparable (mayor * 1000 + menor). Lo que no se puede leer, 0.
function NumeroDeVersion_704ILR(Texto_704ILR: String): Integer;
var
  Punto_704ILR, Mayor_704ILR, Menor_704ILR: Integer;
  Resto_704ILR: String;
begin
  Texto_704ILR := Trim(Texto_704ILR);
  Punto_704ILR := Pos('.', Texto_704ILR);
  if Punto_704ILR = 0 then
  begin
    Mayor_704ILR := StrToIntDef(Texto_704ILR, 0);
    Menor_704ILR := 0;
  end
  else
  begin
    Mayor_704ILR := StrToIntDef(Copy(Texto_704ILR, 1, Punto_704ILR - 1), 0);
    Resto_704ILR := Copy(Texto_704ILR, Punto_704ILR + 1, Length(Texto_704ILR));
    if Pos('.', Resto_704ILR) > 0 then Resto_704ILR := Copy(Resto_704ILR, 1, Pos('.', Resto_704ILR) - 1);
    Menor_704ILR := StrToIntDef(Resto_704ILR, 0);
  end;
  Result := Mayor_704ILR * 1000 + Menor_704ILR;
end;

// Version y carpeta de la instalacion previa, leidas de la clave de desinstalacion.
procedure LeerInstalacionPrevia_704ILR;
begin
  VersionPrevia_704ILR := '';
  CarpetaPrevia_704ILR := '';
  if IsWin64 and RegQueryStringValue(HKLM64, ClaveDesinstalacion_704ILR, 'DisplayVersion', VersionPrevia_704ILR) then
    RegQueryStringValue(HKLM64, ClaveDesinstalacion_704ILR, 'InstallLocation', CarpetaPrevia_704ILR)
  else if RegQueryStringValue(HKLM32, ClaveDesinstalacion_704ILR, 'DisplayVersion', VersionPrevia_704ILR) then
    RegQueryStringValue(HKLM32, ClaveDesinstalacion_704ILR, 'InstallLocation', CarpetaPrevia_704ILR);
  CarpetaPrevia_704ILR := RemoveBackslashUnlessRoot(CarpetaPrevia_704ILR);
end;

function EsActualizacion_704ILR: Boolean;
begin
  Result := VersionPrevia_704ILR <> '';
end;

// ------------------------------------------------------------- deteccion de instancias

procedure AgregarInstanciasDe_704ILR(Raiz_704ILR: Integer; Lista_704ILR: TStrings);
var
  Nombres_704ILR: TArrayOfString;
  i_704ILR: Integer;
  Instancia_704ILR: String;
begin
  if not RegGetValueNames(Raiz_704ILR, ClaveInstancias_704ILR, Nombres_704ILR) then Exit;
  for i_704ILR := 0 to GetArrayLength(Nombres_704ILR) - 1 do
  begin
    // La instancia predeterminada no lleva nombre; las demas son equipo\nombre.
    if CompareText(Nombres_704ILR[i_704ILR], 'MSSQLSERVER') = 0 then
      Instancia_704ILR := 'localhost'
    else
      Instancia_704ILR := 'localhost\' + Nombres_704ILR[i_704ILR];
    if Lista_704ILR.IndexOf(Instancia_704ILR) < 0 then Lista_704ILR.Add(Instancia_704ILR);
  end;
end;

procedure DetectarInstancias_704ILR(Lista_704ILR: TStrings);
begin
  Lista_704ILR.Clear;
  if IsWin64 then AgregarInstanciasDe_704ILR(HKLM64, Lista_704ILR);
  AgregarInstanciasDe_704ILR(HKLM32, Lista_704ILR);
end;

// Medios de SQL Server Express junto al instalador: la carpeta extraida (SETUP.EXE) o
// el paquete autoextraible de Microsoft (SQLEXPR*.EXE).
function BuscarMediosDelMotor_704ILR: String;
var
  Carpeta_704ILR: String;
  Hallado_704ILR: TFindRec;
begin
  Result := '';
  Carpeta_704ILR := ExpandConstant('{src}\SQLServerExpress');
  if FileExists(Carpeta_704ILR + '\SETUP.EXE') then
    Result := Carpeta_704ILR + '\SETUP.EXE'
  else if FindFirst(Carpeta_704ILR + '\SQLEXPR*.EXE', Hallado_704ILR) then
  begin
    try
      Result := Carpeta_704ILR + '\' + Hallado_704ILR.Name;
    finally
      FindClose(Hallado_704ILR);
    end;
  end;
end;

// Nombre de la instancia que se crea: SQLEXPRESS (la de fabrica de la aplicacion) o,
// si ya existe una con ese nombre, EVENTECH.
function InstanciaNueva_704ILR: String;
var
  Instancias_704ILR: TStringList;
begin
  Instancias_704ILR := TStringList.Create;
  try
    DetectarInstancias_704ILR(Instancias_704ILR);
    if Instancias_704ILR.IndexOf(InstanciaDeFabrica_704ILR) < 0 then Result := 'SQLEXPRESS' else Result := 'EVENTECH';
  finally
    Instancias_704ILR.Free;
  end;
end;

// --------------------------------------------------------------- ejecucion de guiones

function Valor_704ILR(Resultado_704ILR: TStrings; Clave_704ILR: String): String;
var
  i_704ILR: Integer;
  Linea_704ILR: String;
begin
  Result := '';
  for i_704ILR := 0 to Resultado_704ILR.Count - 1 do
  begin
    Linea_704ILR := Resultado_704ILR[i_704ILR];
    // La primera linea puede conservar la marca de orden de bytes del archivo.
    if (Length(Linea_704ILR) > 0) and (Ord(Linea_704ILR[1]) = $FEFF) then Delete(Linea_704ILR, 1, 1);
    if Pos(Clave_704ILR + '=', Linea_704ILR) = 1 then
    begin
      Result := Copy(Linea_704ILR, Length(Clave_704ILR) + 2, Length(Linea_704ILR));
      Exit;
    end;
  end;
end;

// Ejecuta un guion de PowerShell y carga en Resultado las lineas CLAVE=valor que deja.
// Devuelve el codigo de salida del guion (0 correcto) o -1 si no se lo pudo ejecutar.
// Carpeta: donde esta el guion y donde deja su resultado. ComoUsuarioOriginal: con la
// cuenta de quien inicio la instalacion en lugar de la elevada.
function EjecutarGuion_704ILR(Carpeta_704ILR, Guion_704ILR, Argumentos_704ILR: String;
  ComoUsuarioOriginal_704ILR: Boolean; Resultado_704ILR: TStrings): Integer;
var
  PowerShell_704ILR, Salida_704ILR, Parametros_704ILR: String;
  Codigo_704ILR: Integer;
  Ejecuto_704ILR: Boolean;
begin
  Resultado_704ILR.Clear;
  PowerShell_704ILR := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Salida_704ILR := AddBackslash(Carpeta_704ILR) + 'resultado_' + ChangeFileExt(Guion_704ILR, '') + '.txt';
  DeleteFile(Salida_704ILR);
  Parametros_704ILR := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    AddBackslash(Carpeta_704ILR) + Guion_704ILR + '" ' + Argumentos_704ILR +
    ' -Salida_704ILR "' + Salida_704ILR + '"';

  if ComoUsuarioOriginal_704ILR then
    Ejecuto_704ILR := ExecAsOriginalUser(PowerShell_704ILR, Parametros_704ILR, '', SW_HIDE, ewWaitUntilTerminated, Codigo_704ILR)
  else
    Ejecuto_704ILR := Exec(PowerShell_704ILR, Parametros_704ILR, '', SW_HIDE, ewWaitUntilTerminated, Codigo_704ILR);

  if not Ejecuto_704ILR then
  begin
    Log('No se pudo ejecutar ' + Guion_704ILR + ': ' + SysErrorMessage(Codigo_704ILR));
    Result := -1;
    Exit;
  end;

  if FileExists(Salida_704ILR) then
  begin
    try
      Resultado_704ILR.LoadFromFile(Salida_704ILR);
    except
      Log('No se pudo leer el resultado de ' + Guion_704ILR);
    end;
    DeleteFile(Salida_704ILR);
  end;
  Log(Guion_704ILR + ' ' + Argumentos_704ILR + ' -> ' + IntToStr(Codigo_704ILR) + ' | ' + Resultado_704ILR.CommaText);
  Result := Codigo_704ILR;
end;

function ArgumentosBase_704ILR(Accion_704ILR, Instancia_704ILR: String): String;
begin
  Result := '-Accion_704ILR ' + Accion_704ILR + ' -Instancia_704ILR "' + Instancia_704ILR + '" -VersionNueva_704ILR "{#Version_704ILR}"';
end;

// Mensaje para quien instala a partir de lo que informo el diagnostico de la instancia.
function MensajeDeDiagnostico_704ILR(Instancia_704ILR: String; Resultado_704ILR: TStrings): String;
var
  Codigo_704ILR, Detalle_704ILR: String;
begin
  Codigo_704ILR := Valor_704ILR(Resultado_704ILR, 'CODIGO');
  Detalle_704ILR := Valor_704ILR(Resultado_704ILR, 'DETALLE');

  if Codigo_704ILR = 'INSTANCIA_REMOTA' then
    Result := FmtMessage(CustomMessage('ErrInstanciaRemota'), [Instancia_704ILR, Detalle_704ILR])
  else if Codigo_704ILR = 'VERSION_ANTERIOR' then
    Result := FmtMessage(CustomMessage('ErrVersionAnterior'), [Instancia_704ILR, Detalle_704ILR])
  else if Codigo_704ILR = 'SIN_PERMISO' then
    Result := FmtMessage(CustomMessage('ErrSinPermiso'), [Instancia_704ILR, Detalle_704ILR])
  else if Codigo_704ILR = 'BASE_AJENA' then
    Result := FmtMessage(CustomMessage('ErrBaseAjena'), [Instancia_704ILR, BaseDeDatos_704ILR])
  else if Codigo_704ILR = 'BASE_MAS_NUEVA' then
    Result := FmtMessage(CustomMessage('ErrBaseMasNueva'), [Instancia_704ILR, BaseDeDatos_704ILR, Detalle_704ILR, '{#Version_704ILR}'])
  else
  begin
    if Codigo_704ILR = 'SIN_CONEXION' then
      Result := FmtMessage(CustomMessage('ErrSinConexion'), [Instancia_704ILR])
    else if Codigo_704ILR = 'SIN_ACCESO_A_LA_BASE' then
      Result := FmtMessage(CustomMessage('ErrSinAccesoBase'), [Instancia_704ILR, BaseDeDatos_704ILR])
    else
      Result := FmtMessage(CustomMessage('ErrNoPrevisto'), [Instancia_704ILR]);
    if Detalle_704ILR <> '' then
      Result := Result + #13#10#13#10 + CustomMessage('DetalleTecnico') + #13#10 + Detalle_704ILR;
  end;
end;

function DiagnosticarInstancia_704ILR(Instancia_704ILR: String; var Mensaje_704ILR: String): Boolean;
var
  Resultado_704ILR: TStringList;
begin
  Mensaje_704ILR := '';
  Resultado_704ILR := TStringList.Create;
  try
    Result := EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
      ArgumentosBase_704ILR('Diagnosticar', Instancia_704ILR), False, Resultado_704ILR) = 0;
    if Result then
    begin
      MotorDetectado_704ILR := Valor_704ILR(Resultado_704ILR, 'MOTOR');
      BaseExiste_704ILR := Valor_704ILR(Resultado_704ILR, 'BASE_EXISTE') = '1';
      VersionBase_704ILR := Valor_704ILR(Resultado_704ILR, 'VERSION_BASE');
      CuentasBase_704ILR := Valor_704ILR(Resultado_704ILR, 'CUENTAS');
      ClientesBase_704ILR := Valor_704ILR(Resultado_704ILR, 'CLIENTES');
      ReservasBase_704ILR := Valor_704ILR(Resultado_704ILR, 'RESERVAS');
      // Con la base abierta en otro lado no se la puede respaldar ni actualizar.
      if BaseExiste_704ILR and (StrToIntDef(Valor_704ILR(Resultado_704ILR, 'SESIONES'), 0) > 0) then
      begin
        Mensaje_704ILR := FmtMessage(CustomMessage('ErrBaseEnUso'), [BaseDeDatos_704ILR, Valor_704ILR(Resultado_704ILR, 'SESIONES')]);
        Result := False;
      end;
    end
    else
      Mensaje_704ILR := MensajeDeDiagnostico_704ILR(Instancia_704ILR, Resultado_704ILR);
  finally
    Resultado_704ILR.Free;
  end;
end;

// Instala SQL Server Express con los medios que trae el paquete, en modo silencioso y
// con los parametros documentados por Microsoft para una instalacion desde la linea de
// comandos. Devuelve '' si la instancia quedo disponible, o el mensaje para quien instala.
function InstalarMotor_704ILR(Instancia_704ILR: String): String;
var
  Parametros_704ILR: String;
  Codigo_704ILR: Integer;
  Instancias_704ILR: TStringList;
begin
  Result := '';
  Parametros_704ILR := '/Q /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=' + Instancia_704ILR +
    ' /SQLSVCACCOUNT="NT Service\MSSQL$' + Instancia_704ILR + '"' +
    ' /SQLSYSADMINACCOUNTS="BUILTIN\Administrators" /ADDCURRENTUSERASSQLADMIN=True' +
    ' /TCPENABLED=0 /UPDATEENABLED=False /IACCEPTSQLSERVERLICENSETERMS /SUPPRESSPRIVACYSTATEMENTNOTICE';
  PaginaProgreso_704ILR.SetText(FmtMessage(CustomMessage('MotorProgresoTexto'), [Instancia_704ILR]), CustomMessage('MotorProgresoNota'));
  PaginaProgreso_704ILR.SetProgress(0, 100);
  PaginaProgreso_704ILR.ProgressBar.Style := npbstMarquee;
  PaginaProgreso_704ILR.Show;
  try
    Log('Instalacion de SQL Server Express: ' + MediosDelMotor_704ILR + ' ' + Parametros_704ILR);
    if not Exec(MediosDelMotor_704ILR, Parametros_704ILR, ExtractFileDir(MediosDelMotor_704ILR), SW_HIDE, ewWaitUntilTerminated, Codigo_704ILR) then
      Codigo_704ILR := -1;
    Log('SQL Server Express termino con el codigo ' + IntToStr(Codigo_704ILR));
  finally
    PaginaProgreso_704ILR.Hide;
  end;
  // 3010: instalado, pide reiniciar el equipo.
  if Codigo_704ILR = 3010 then
  begin
    Result := CustomMessage('MotorReinicio');
    Exit;
  end;
  Instancias_704ILR := TStringList.Create;
  try
    DetectarInstancias_704ILR(Instancias_704ILR);
    if Instancias_704ILR.IndexOf('localhost\' + Instancia_704ILR) < 0 then
    begin
      if Codigo_704ILR <> 0 then
        Result := FmtMessage(CustomMessage('MotorFallo'), [IntToStr(Codigo_704ILR),
          ExpandConstant('{commonpf}\Microsoft SQL Server\<versión>\Setup Bootstrap\Log')])
      else
        Result := FmtMessage(CustomMessage('MotorNoDetectado'), ['localhost\' + Instancia_704ILR]);
    end
    else
    begin
      ComboInstancia_704ILR.Items.Assign(Instancias_704ILR);
      ComboInstancia_704ILR.Text := 'localhost\' + Instancia_704ILR;
    end;
  finally
    Instancias_704ILR.Free;
  end;
end;

// ---------------------------------------------------------------- paginas del asistente

procedure AbrirEnlace_704ILR(Url_704ILR: String);
var
  Codigo_704ILR: Integer;
begin
  ShellExecAsOriginalUser('open', Url_704ILR, '', '', SW_SHOWNORMAL, ewNoWait, Codigo_704ILR);
end;

procedure AbrirDescargaDelMotor_704ILR(Sender: TObject);
begin
  AbrirEnlace_704ILR(DescargaDelMotor_704ILR);
end;

procedure AbrirLicenciaDelMotor_704ILR(Sender: TObject);
begin
  AbrirEnlace_704ILR(LicenciaDelMotor_704ILR);
end;

procedure MotorCambiado_704ILR(Sender: TObject);
begin
  ChkLicencia_704ILR.Enabled := ChkMotor_704ILR.Checked;
  ComboInstancia_704ILR.Enabled := not ChkMotor_704ILR.Checked;
end;

function NuevoEnlace_704ILR(Pagina_704ILR: TWizardPage; Arriba_704ILR: Integer; Texto_704ILR: String; AlHacerClic_704ILR: TNotifyEvent): TNewStaticText;
begin
  Result := TNewStaticText.Create(Pagina_704ILR);
  Result.Parent := Pagina_704ILR.Surface;
  Result.Left := 0;
  Result.Top := Arriba_704ILR;
  Result.Caption := Texto_704ILR;
  Result.Cursor := crHand;
  Result.Font.Color := clBlue;
  Result.Font.Style := [fsUnderline];
  Result.OnClick := AlHacerClic_704ILR;
end;

procedure CrearPaginaInstancia_704ILR;
var
  Intro_704ILR, Etiqueta_704ILR, Nota_704ILR, Enlace_704ILR: TNewStaticText;
  Instancias_704ILR: TStringList;
  Pedida_704ILR, Previa_704ILR: String;
begin
  PaginaInstancia_704ILR := CreateCustomPage(wpSelectDir,
    CustomMessage('InstanciaTitulo'), CustomMessage('InstanciaDescripcion'));

  Instancias_704ILR := TStringList.Create;
  try
    DetectarInstancias_704ILR(Instancias_704ILR);

    Intro_704ILR := TNewStaticText.Create(PaginaInstancia_704ILR);
    Intro_704ILR.Parent := PaginaInstancia_704ILR.Surface;
    Intro_704ILR.AutoSize := False;
    Intro_704ILR.WordWrap := True;
    Intro_704ILR.SetBounds(0, 0, PaginaInstancia_704ILR.SurfaceWidth, ScaleY(40));
    Intro_704ILR.Anchors := [akLeft, akTop, akRight];
    if Instancias_704ILR.Count > 0 then
      Intro_704ILR.Caption := FmtMessage(CustomMessage('InstanciaIntro'), [BaseDeDatos_704ILR])
    else
      Intro_704ILR.Caption := FmtMessage(CustomMessage('InstanciaNinguna'), ['{#Nombre_704ILR}']);
    Intro_704ILR.AdjustHeight;

    Etiqueta_704ILR := TNewStaticText.Create(PaginaInstancia_704ILR);
    Etiqueta_704ILR.Parent := PaginaInstancia_704ILR.Surface;
    Etiqueta_704ILR.Left := 0;
    Etiqueta_704ILR.Top := Intro_704ILR.Top + Intro_704ILR.Height + ScaleY(14);
    Etiqueta_704ILR.Caption := CustomMessage('InstanciaEtiqueta');

    ComboInstancia_704ILR := TNewComboBox.Create(PaginaInstancia_704ILR);
    ComboInstancia_704ILR.Parent := PaginaInstancia_704ILR.Surface;
    ComboInstancia_704ILR.Left := 0;
    ComboInstancia_704ILR.Top := Etiqueta_704ILR.Top + Etiqueta_704ILR.Height + ScaleY(6);
    ComboInstancia_704ILR.Width := PaginaInstancia_704ILR.SurfaceWidth;
    ComboInstancia_704ILR.Anchors := [akLeft, akTop, akRight];
    // Lista de las instancias detectadas, con la posibilidad de escribir otra.
    ComboInstancia_704ILR.Style := csDropDown;
    ComboInstancia_704ILR.Items.Assign(Instancias_704ILR);
    Etiqueta_704ILR.FocusControl := ComboInstancia_704ILR;

    // Instancia propuesta: la indicada por linea de comandos (/INSTANCIA=, para una
    // instalacion desatendida), la de la instalacion previa (reinstalacion), la de
    // fabrica de la aplicacion o la primera detectada.
    Pedida_704ILR := ExpandConstant('{param:INSTANCIA|}');
    Previa_704ILR := GetPreviousData('Instancia', '');
    if Pedida_704ILR <> '' then
      ComboInstancia_704ILR.Text := Pedida_704ILR
    else if Previa_704ILR <> '' then
      ComboInstancia_704ILR.Text := Previa_704ILR
    else if Instancias_704ILR.IndexOf(InstanciaDeFabrica_704ILR) >= 0 then
      ComboInstancia_704ILR.ItemIndex := Instancias_704ILR.IndexOf(InstanciaDeFabrica_704ILR)
    else if Instancias_704ILR.Count > 0 then
      ComboInstancia_704ILR.ItemIndex := 0;

    Nota_704ILR := TNewStaticText.Create(PaginaInstancia_704ILR);
    Nota_704ILR.Parent := PaginaInstancia_704ILR.Surface;
    Nota_704ILR.AutoSize := False;
    Nota_704ILR.WordWrap := True;
    Nota_704ILR.SetBounds(0, ComboInstancia_704ILR.Top + ComboInstancia_704ILR.Height + ScaleY(14),
      PaginaInstancia_704ILR.SurfaceWidth, ScaleY(40));
    Nota_704ILR.Anchors := [akLeft, akTop, akRight];
    Nota_704ILR.Caption := FmtMessage(CustomMessage('InstanciaNota'), [BaseDeDatos_704ILR]);
    Nota_704ILR.AdjustHeight;

    // Motor de base de datos: con los medios en el paquete, se ofrece instalarlo cuando
    // el equipo no tiene ninguna instancia (o cuando se lo pide con /MOTOR=1).
    MediosDelMotor_704ILR := BuscarMediosDelMotor_704ILR;
    if (MediosDelMotor_704ILR <> '') and ((Instancias_704ILR.Count = 0) or (ExpandConstant('{param:MOTOR|0}') = '1')) then
    begin
      ChkMotor_704ILR := TNewCheckBox.Create(PaginaInstancia_704ILR);
      ChkMotor_704ILR.Parent := PaginaInstancia_704ILR.Surface;
      ChkMotor_704ILR.SetBounds(0, Nota_704ILR.Top + Nota_704ILR.Height + ScaleY(12), PaginaInstancia_704ILR.SurfaceWidth, ScaleY(17));
      ChkMotor_704ILR.Caption := FmtMessage(CustomMessage('MotorIncluido'), ['localhost\' + InstanciaNueva_704ILR]);
      ChkMotor_704ILR.Checked := (Instancias_704ILR.Count = 0) or (ExpandConstant('{param:MOTOR|0}') = '1');
      ChkMotor_704ILR.OnClick := @MotorCambiado_704ILR;
      ChkLicencia_704ILR := TNewCheckBox.Create(PaginaInstancia_704ILR);
      ChkLicencia_704ILR.Parent := PaginaInstancia_704ILR.Surface;
      ChkLicencia_704ILR.SetBounds(ScaleX(16), ChkMotor_704ILR.Top + ChkMotor_704ILR.Height + ScaleY(6), PaginaInstancia_704ILR.SurfaceWidth - ScaleX(16), ScaleY(17));
      ChkLicencia_704ILR.Caption := CustomMessage('MotorLicencia');
      ChkLicencia_704ILR.Checked := ExpandConstant('{param:ACEPTARLICENCIASQL|0}') = '1';
      Enlace_704ILR := NuevoEnlace_704ILR(PaginaInstancia_704ILR, ChkLicencia_704ILR.Top + ChkLicencia_704ILR.Height + ScaleY(4),
        CustomMessage('MotorLicenciaEnlace'), @AbrirLicenciaDelMotor_704ILR);
      Enlace_704ILR.Left := ScaleX(16);
      MotorCambiado_704ILR(nil);
    end
    else if Instancias_704ILR.Count = 0 then
    begin
      Enlace_704ILR := NuevoEnlace_704ILR(PaginaInstancia_704ILR, Nota_704ILR.Top + Nota_704ILR.Height + ScaleY(18),
        CustomMessage('InstanciaEnlace'), @AbrirDescargaDelMotor_704ILR);
    end;
  finally
    Instancias_704ILR.Free;
  end;
end;

procedure CrearPaginaBase_704ILR;
var
  NotaConservar_704ILR, NotaReemplazar_704ILR: TNewStaticText;
  Ancho_704ILR: Integer;
begin
  PaginaBase_704ILR := CreateCustomPage(PaginaInstancia_704ILR.ID,
    CustomMessage('BaseTitulo'), CustomMessage('BaseDescripcion'));
  Ancho_704ILR := PaginaBase_704ILR.SurfaceWidth;

  LblResumenBase_704ILR := TNewStaticText.Create(PaginaBase_704ILR);
  LblResumenBase_704ILR.Parent := PaginaBase_704ILR.Surface;
  LblResumenBase_704ILR.AutoSize := False;
  LblResumenBase_704ILR.WordWrap := True;
  LblResumenBase_704ILR.SetBounds(0, 0, Ancho_704ILR, ScaleY(100));

  RadioConservar_704ILR := TNewRadioButton.Create(PaginaBase_704ILR);
  RadioConservar_704ILR.Parent := PaginaBase_704ILR.Surface;
  RadioConservar_704ILR.SetBounds(0, ScaleY(108), Ancho_704ILR, ScaleY(17));
  RadioConservar_704ILR.Caption := FmtMessage(CustomMessage('OpcionConservar'), ['{#Version_704ILR}']);
  RadioConservar_704ILR.Font.Style := [fsBold];

  NotaConservar_704ILR := TNewStaticText.Create(PaginaBase_704ILR);
  NotaConservar_704ILR.Parent := PaginaBase_704ILR.Surface;
  NotaConservar_704ILR.AutoSize := False;
  NotaConservar_704ILR.WordWrap := True;
  NotaConservar_704ILR.SetBounds(ScaleX(18), ScaleY(128), Ancho_704ILR - ScaleX(18), ScaleY(44));
  NotaConservar_704ILR.Caption := CustomMessage('OpcionConservarNota');

  RadioReemplazar_704ILR := TNewRadioButton.Create(PaginaBase_704ILR);
  RadioReemplazar_704ILR.Parent := PaginaBase_704ILR.Surface;
  RadioReemplazar_704ILR.SetBounds(0, ScaleY(180), Ancho_704ILR, ScaleY(17));
  RadioReemplazar_704ILR.Caption := CustomMessage('OpcionReemplazar');

  NotaReemplazar_704ILR := TNewStaticText.Create(PaginaBase_704ILR);
  NotaReemplazar_704ILR.Parent := PaginaBase_704ILR.Surface;
  NotaReemplazar_704ILR.AutoSize := False;
  NotaReemplazar_704ILR.WordWrap := True;
  NotaReemplazar_704ILR.SetBounds(ScaleX(18), ScaleY(200), Ancho_704ILR - ScaleX(18), ScaleY(30));
  NotaReemplazar_704ILR.Caption := CustomMessage('OpcionReemplazarNota');

  // Por defecto se conservan los datos. Una instalacion desatendida elige con
  // /BASE=CONSERVAR (o sin el parametro) o /BASE=REEMPLAZAR.
  RadioReemplazar_704ILR.Checked := CompareText(ExpandConstant('{param:BASE|CONSERVAR}'), 'REEMPLAZAR') = 0;
  RadioConservar_704ILR.Checked := not RadioReemplazar_704ILR.Checked;
end;

procedure ActualizarResumenBase_704ILR;
var
  Texto_704ILR: String;
begin
  if EsActualizacion_704ILR then
    Texto_704ILR := FmtMessage(CustomMessage('BaseInstalado'), ['{#Nombre_704ILR}', VersionPrevia_704ILR, CarpetaPrevia_704ILR])
  else
    Texto_704ILR := CustomMessage('BaseSinPrevia');
  Texto_704ILR := Texto_704ILR + #13#10#13#10 +
    FmtMessage(CustomMessage('BaseExistente'), [BaseDeDatos_704ILR, InstanciaElegida_704ILR, VersionBase_704ILR,
      CuentasBase_704ILR, ClientesBase_704ILR, ReservasBase_704ILR]) + #13#10#13#10 +
    FmtMessage(CustomMessage('BaseNueva'), ['{#Nombre_704ILR}', '{#Version_704ILR}']);
  LblResumenBase_704ILR.Caption := Texto_704ILR;
end;

// No se instala una version anterior sobre una posterior.
function InitializeSetup: Boolean;
begin
  Result := True;
  LeerInstalacionPrevia_704ILR;
  if EsActualizacion_704ILR and (NumeroDeVersion_704ILR(VersionPrevia_704ILR) > NumeroDeVersion_704ILR('{#Version_704ILR}')) then
  begin
    SuppressibleMsgBox(FmtMessage(CustomMessage('ErrDegradacion'), ['{#Nombre_704ILR}', VersionPrevia_704ILR, '{#Version_704ILR}']),
      mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  // Los guiones se extraen una sola vez a la carpeta temporal del asistente, que
  // solo pueden leer los administradores: de ahi los ejecuta la cuenta elevada.
  ExtractTemporaryFile(GuionBase_704ILR);
  ExtractTemporaryFile(GuionConexion_704ILR);

  if EsActualizacion_704ILR then
    WizardForm.WelcomeLabel2.Caption := FmtMessage(CustomMessage('BienvenidaActualizacion'), ['{#Nombre_704ILR}',
      VersionPrevia_704ILR, '{#Version_704ILR}']);

  CrearPaginaInstancia_704ILR;
  CrearPaginaBase_704ILR;
  PaginaProgreso_704ILR := CreateOutputProgressPage(
    CustomMessage('ProgresoTitulo'), CustomMessage('ProgresoDescripcion'));
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = PaginaBase_704ILR.ID) and not BaseExiste_704ILR;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = PaginaBase_704ILR.ID then
    ActualizarResumenBase_704ILR
  else if CurPageID = wpFinished then
  begin
    if Operacion_704ILR = OpActualizar_704ILR then
      WizardForm.FinishedLabel.Caption := FmtMessage(CustomMessage('FinActualizado'), ['{#Version_704ILR}', RespaldoPrevio_704ILR])
    else if Operacion_704ILR = OpReemplazar_704ILR then
      WizardForm.FinishedLabel.Caption := FmtMessage(CustomMessage('FinReemplazado'), ['{#Version_704ILR}', RespaldoPrevio_704ILR]);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Instancia_704ILR, Mensaje_704ILR: String;
begin
  Result := True;
  if CurPageID = PaginaBase_704ILR.ID then
  begin
    if RadioReemplazar_704ILR.Checked then
    begin
      Operacion_704ILR := OpReemplazar_704ILR;
      Result := SuppressibleMsgBox(FmtMessage(CustomMessage('ConfirmarReemplazo'), [BaseDeDatos_704ILR]),
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDYES) = IDYES;
    end
    else
      Operacion_704ILR := OpActualizar_704ILR;
    Exit;
  end;
  if CurPageID <> PaginaInstancia_704ILR.ID then Exit;

  Mensaje_704ILR := '';
  if (ChkMotor_704ILR <> nil) and ChkMotor_704ILR.Checked then
  begin
    // Primero el motor: sin licencia aceptada no se instala.
    if not ChkLicencia_704ILR.Checked then
      Mensaje_704ILR := CustomMessage('MotorFaltaLicencia')
    else
    begin
      WizardForm.NextButton.Enabled := False;
      WizardForm.BackButton.Enabled := False;
      try
        Mensaje_704ILR := InstalarMotor_704ILR(InstanciaNueva_704ILR);
      finally
        WizardForm.NextButton.Enabled := True;
        WizardForm.BackButton.Enabled := True;
      end;
      if Mensaje_704ILR = '' then
      begin
        ChkMotor_704ILR.Checked := False;
        MotorCambiado_704ILR(nil);
      end;
    end;
  end;

  Instancia_704ILR := Trim(ComboInstancia_704ILR.Text);
  // Si el motor no se pudo instalar, ese es el mensaje; si no, se valida la instancia.
  if Mensaje_704ILR = '' then
  begin
    if Instancia_704ILR = '' then
      Mensaje_704ILR := CustomMessage('InstanciaVacia')
    else if Pos('"', Instancia_704ILR) > 0 then
      Mensaje_704ILR := CustomMessage('InstanciaInvalida');
  end;
  if (Mensaje_704ILR = '') then
  begin
    // La comprobacion consulta al motor: mientras tanto el asistente no admite otro clic.
    WizardForm.NextButton.Enabled := False;
    WizardForm.BackButton.Enabled := False;
    try
      DiagnosticarInstancia_704ILR(Instancia_704ILR, Mensaje_704ILR);
    finally
      WizardForm.NextButton.Enabled := True;
      WizardForm.BackButton.Enabled := True;
    end;
  end;

  if Mensaje_704ILR = '' then
  begin
    InstanciaElegida_704ILR := Instancia_704ILR;
    if BaseExiste_704ILR then
    begin
      if RadioReemplazar_704ILR.Checked then Operacion_704ILR := OpReemplazar_704ILR else Operacion_704ILR := OpActualizar_704ILR;
    end
    else
      Operacion_704ILR := OpCrear_704ILR;
  end
  else
  begin
    Log('Instancia rechazada: ' + Mensaje_704ILR);
    SuppressibleMsgBox(Mensaje_704ILR, mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := '';
  if EsActualizacion_704ILR then
    Result := Result + CustomMessage('MemoActualizacion') + NewLine + Space +
      FmtMessage(CustomMessage('MemoVersiones'), ['{#Nombre_704ILR}', VersionPrevia_704ILR, '{#Version_704ILR}']) + NewLine + NewLine;
  if MemoDirInfo <> '' then Result := Result + MemoDirInfo + NewLine + NewLine;
  Result := Result + CustomMessage('MemoBase') + NewLine +
    Space + FmtMessage(CustomMessage('MemoInstancia'), [InstanciaElegida_704ILR, MotorDetectado_704ILR]) + NewLine + Space;
  if Operacion_704ILR = OpActualizar_704ILR then
    Result := Result + FmtMessage(CustomMessage('MemoConservar'), [BaseDeDatos_704ILR, VersionBase_704ILR, '{#Version_704ILR}'])
  else if Operacion_704ILR = OpReemplazar_704ILR then
    Result := Result + FmtMessage(CustomMessage('MemoReemplazar'), [BaseDeDatos_704ILR])
  else
    Result := Result + FmtMessage(CustomMessage('MemoNombre'), [BaseDeDatos_704ILR]);
  if MemoTasksInfo <> '' then Result := Result + NewLine + NewLine + MemoTasksInfo;
end;

// ------------------------------------------------------------------------ instalacion

// Mensaje de una operacion sobre la base que no prospero.
function MensajeDeFalla_704ILR(Titulo_704ILR: String; Resultado_704ILR: TStrings): String;
var
  Detalle_704ILR: String;
begin
  Result := Titulo_704ILR;
  if Valor_704ILR(Resultado_704ILR, 'RESTAURADA') = '1' then
    Result := Result + #13#10#13#10 + CustomMessage('BaseRestaurada')
  else if Valor_704ILR(Resultado_704ILR, 'RESPALDO') <> '' then
    Result := Result + #13#10#13#10 + FmtMessage(CustomMessage('BaseNoRestaurada'), [Valor_704ILR(Resultado_704ILR, 'RESPALDO')]);
  Detalle_704ILR := Valor_704ILR(Resultado_704ILR, 'DETALLE');
  if Detalle_704ILR <> '' then
    Result := Result + #13#10#13#10 + CustomMessage('DetalleTecnico') + #13#10 + Detalle_704ILR;
  Result := Result + #13#10#13#10 + CustomMessage('RegistroEn') + #13#10 + ExpandConstant('{log}');
end;

// La base se prepara ANTES de copiar la aplicacion: es el paso que depende del entorno
// y, si falla, los archivos instalados no se tocan y se puede volver atras.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Resultado_704ILR: TStringList;
  Respaldo_704ILR, Esquema_704ILR, Codigo_704ILR: String;
begin
  Result := '';
  // Si un intento anterior de esta misma ejecucion ya preparo la base ahi, no se repite.
  if (InstanciaDeLaBase_704ILR = InstanciaElegida_704ILR) and ((MarcaDeLaBase_704ILR <> '') or BaseModificada_704ILR) then Exit;

  if Operacion_704ILR = OpActualizar_704ILR then
    PaginaProgreso_704ILR.SetText(FmtMessage(CustomMessage('ProgresoActualizar'), [BaseDeDatos_704ILR, '{#Version_704ILR}']), CustomMessage('ProgresoNota'))
  else if Operacion_704ILR = OpReemplazar_704ILR then
    PaginaProgreso_704ILR.SetText(FmtMessage(CustomMessage('ProgresoReemplazar'), [BaseDeDatos_704ILR]), CustomMessage('ProgresoNota'))
  else
    PaginaProgreso_704ILR.SetText(FmtMessage(CustomMessage('ProgresoTexto'), [BaseDeDatos_704ILR, InstanciaElegida_704ILR]), CustomMessage('ProgresoNota'));
  PaginaProgreso_704ILR.SetProgress(0, 100);
  PaginaProgreso_704ILR.ProgressBar.Style := npbstMarquee;
  PaginaProgreso_704ILR.Show;
  Resultado_704ILR := TStringList.Create;
  try
    // El esquema de esta version se aplica en las tres operaciones: lleva la base
    // existente (o la recien restaurada) a la version 3.0, sin pisar datos.
    ExtractTemporaryFile('schema.sql');
    Esquema_704ILR := ExpandConstant('{tmp}\schema.sql');
    if Operacion_704ILR = OpActualizar_704ILR then
    begin
      if EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
           ArgumentosBase_704ILR('Actualizar', InstanciaElegida_704ILR) + ' -Esquema_704ILR "' + Esquema_704ILR + '"',
           False, Resultado_704ILR) = 0 then
      begin
        RespaldoPrevio_704ILR := Valor_704ILR(Resultado_704ILR, 'RESPALDO');
        BaseModificada_704ILR := True;
        InstanciaDeLaBase_704ILR := InstanciaElegida_704ILR;
      end
      else
      begin
        Codigo_704ILR := Valor_704ILR(Resultado_704ILR, 'CODIGO');
        if Codigo_704ILR = 'BASE_EN_USO' then
          Result := FmtMessage(CustomMessage('ErrBaseEnUso'), [BaseDeDatos_704ILR, Valor_704ILR(Resultado_704ILR, 'DETALLE')])
        else
          Result := MensajeDeFalla_704ILR(FmtMessage(CustomMessage('ErrActualizacion'), [BaseDeDatos_704ILR, InstanciaElegida_704ILR]), Resultado_704ILR);
      end;
    end
    else
    begin
      ExtractTemporaryFile(BaseDeDatos_704ILR + '.bak');
      Respaldo_704ILR := ExpandConstant('{tmp}\') + BaseDeDatos_704ILR + '.bak';
      if Operacion_704ILR = OpReemplazar_704ILR then
      begin
        if EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
             ArgumentosBase_704ILR('Reemplazar', InstanciaElegida_704ILR) + ' -Respaldo_704ILR "' + Respaldo_704ILR + '"' +
             ' -Esquema_704ILR "' + Esquema_704ILR + '"',
             False, Resultado_704ILR) = 0 then
        begin
          RespaldoPrevio_704ILR := Valor_704ILR(Resultado_704ILR, 'RESPALDO');
          BaseModificada_704ILR := True;
          InstanciaDeLaBase_704ILR := InstanciaElegida_704ILR;
        end
        else if Valor_704ILR(Resultado_704ILR, 'CODIGO') = 'BASE_EN_USO' then
          Result := FmtMessage(CustomMessage('ErrBaseEnUso'), [BaseDeDatos_704ILR, Valor_704ILR(Resultado_704ILR, 'DETALLE')])
        else
          Result := MensajeDeFalla_704ILR(FmtMessage(CustomMessage('ErrReemplazo'), [BaseDeDatos_704ILR, InstanciaElegida_704ILR]), Resultado_704ILR);
      end
      else
      begin
        if EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
             ArgumentosBase_704ILR('Restaurar', InstanciaElegida_704ILR) + ' -Respaldo_704ILR "' + Respaldo_704ILR + '"' +
             ' -Esquema_704ILR "' + Esquema_704ILR + '"',
             False, Resultado_704ILR) = 0 then
        begin
          MarcaDeLaBase_704ILR := Valor_704ILR(Resultado_704ILR, 'MARCA');
          InstanciaDeLaBase_704ILR := InstanciaElegida_704ILR;
        end
        else if Valor_704ILR(Resultado_704ILR, 'CODIGO') = 'BASE_EXISTENTE' then
          Result := FmtMessage(CustomMessage('ErrBaseExistente'), [InstanciaElegida_704ILR, BaseDeDatos_704ILR])
        else
          Result := MensajeDeFalla_704ILR(FmtMessage(CustomMessage('ErrRestauracion'), [BaseDeDatos_704ILR, InstanciaElegida_704ILR]), Resultado_704ILR);
      end;
      DeleteFile(Respaldo_704ILR);
    end;
    DeleteFile(Esquema_704ILR);
  finally
    Resultado_704ILR.Free;
    PaginaProgreso_704ILR.Hide;
  end;
end;

// Con la aplicacion ya copiada: se guarda la conexion en el perfil de quien inicio la
// instalacion y se comprueba, con esa misma cuenta, que pueda abrir la base. Las dos
// cosas corren sin elevacion, y la carpeta temporal del asistente solo la leen los
// administradores: los guiones se copian a una carpeta de apoyo que se borra al terminar.
// En una actualizacion la conexion que ya apunta a la misma instancia se conserva.
procedure ConfigurarConexion_704ILR;
var
  Resultado_704ILR: TStringList;
  Apoyo_704ILR, Detalle_704ILR, Aviso_704ILR, Conservar_704ILR: String;
begin
  Apoyo_704ILR := ExpandConstant('{commonappdata}\{#Nombre_704ILR}\instalacion');
  ForceDirectories(Apoyo_704ILR);
  Resultado_704ILR := TStringList.Create;
  try
    FileCopy(ExpandConstant('{tmp}\') + GuionConexion_704ILR, AddBackslash(Apoyo_704ILR) + GuionConexion_704ILR, False);
    FileCopy(ExpandConstant('{tmp}\') + GuionBase_704ILR, AddBackslash(Apoyo_704ILR) + GuionBase_704ILR, False);

    Conservar_704ILR := '';
    if Operacion_704ILR = OpActualizar_704ILR then Conservar_704ILR := ' -Conservar_704ILR';
    Aviso_704ILR := '';
    if EjecutarGuion_704ILR(Apoyo_704ILR, GuionConexion_704ILR,
         '-Instancia_704ILR "' + InstanciaElegida_704ILR + '"' + Conservar_704ILR, True, Resultado_704ILR) <> 0 then
      Aviso_704ILR := FmtMessage(CustomMessage('AvisoConexion'), ['{#Nombre_704ILR}', InstanciaElegida_704ILR])
    else if EjecutarGuion_704ILR(Apoyo_704ILR, GuionBase_704ILR,
         '-Accion_704ILR Verificar -Instancia_704ILR "' + InstanciaElegida_704ILR + '"', True, Resultado_704ILR) <> 0 then
      Aviso_704ILR := FmtMessage(CustomMessage('AvisoAcceso'), [BaseDeDatos_704ILR]);

    if Aviso_704ILR <> '' then
    begin
      Detalle_704ILR := Valor_704ILR(Resultado_704ILR, 'DETALLE');
      if Detalle_704ILR <> '' then
        Aviso_704ILR := Aviso_704ILR + #13#10#13#10 + CustomMessage('DetalleTecnico') + #13#10 + Detalle_704ILR;
      Log('Aviso al terminar: ' + Aviso_704ILR);
      SuppressibleMsgBox(Aviso_704ILR, mbInformation, MB_OK, IDOK);
    end;
  finally
    Resultado_704ILR.Free;
    DelTree(Apoyo_704ILR, True, True, True);
    // La carpeta superior se quita solo si quedo vacia (la aplicacion guarda ahi su
    // clave de cifrado y el registro de excepciones).
    RemoveDir(ExtractFileDir(Apoyo_704ILR));
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Desde aca la aplicacion ya esta copiada: la base queda con la instalacion.
    InstalacionCompleta_704ILR := True;
    ConfigurarConexion_704ILR;
  end;
end;

// La instancia elegida queda registrada junto con la instalacion: es el dato con el
// que la reinstalacion sabe donde esta la base del sistema.
procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'Instancia', InstanciaElegida_704ILR);
end;

// Instalacion cancelada o fallida despues de preparar la base: se deshace lo que hizo
// ESTA ejecucion, para que el equipo quede como estaba.
//  - Instalacion inicial: se quita la base creada (solo la que tiene la fecha de
//    creacion de esta ejecucion).
//  - Actualizacion o reemplazo: la base vuelve al respaldo que se tomo antes.
procedure DeinitializeSetup;
var
  Resultado_704ILR: TStringList;
begin
  if InstalacionCompleta_704ILR then Exit;
  if (MarcaDeLaBase_704ILR = '') and not (BaseModificada_704ILR and (RespaldoPrevio_704ILR <> '')) then Exit;
  Resultado_704ILR := TStringList.Create;
  try
    if MarcaDeLaBase_704ILR <> '' then
      EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
        '-Accion_704ILR Quitar -Instancia_704ILR "' + InstanciaDeLaBase_704ILR + '"' +
        ' -Marca_704ILR "' + MarcaDeLaBase_704ILR + '"', False, Resultado_704ILR)
    else
      EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
        '-Accion_704ILR Revertir -Instancia_704ILR "' + InstanciaDeLaBase_704ILR + '"' +
        ' -Respaldo_704ILR "' + RespaldoPrevio_704ILR + '"', False, Resultado_704ILR);
  finally
    Resultado_704ILR.Free;
  end;
end;
