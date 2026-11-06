; =====================================================================================
;  EvenTech - asistente de instalacion (primera version: instalacion inicial)
;
;  Despliega el sistema en un equipo que todavia no lo tiene:
;    1. si el equipo no tiene SQL Server, instala SQL Server Express (viene incluido en
;       el instalador), previa aceptacion de su licencia;
;    2. crea la base de datos del sistema en la instancia de SQL Server que elige quien
;       instala: restaura el respaldo con los datos de demostracion o, si el motor no
;       puede leerlo, la crea con el guion del esquema (db\schema.sql);
;    3. copia la aplicacion, que lleva incluido el entorno de ejecucion de .NET 8;
;    4. deja configurada la conexion de la aplicacion a esa instancia.
;
;  Quien instala es el usuario final, y puede no ser administrador del equipo (las PC
;  compartidas de una sala, por ejemplo): el asistente NO pide permisos de administrador.
;  La aplicacion se instala en la carpeta de programas del usuario
;  (%LOCALAPPDATA%\Programs\EvenTech) y todo lo que genera va a carpetas de datos con
;  permiso de escritura. El unico paso que necesita a un administrador es instalar SQL
;  Server Express: solo para ese paso Windows pide la autorizacion (UAC).
;
;  No se le pide ninguna herramienta de linea de comandos ni escribir una cadena de
;  conexion: las instancias de SQL Server del equipo se detectan y se ofrecen en una lista.
;
;  Esta version no modifica una base de datos con contenido: si la instancia elegida ya
;  tiene la base del sistema, lo informa y pide elegir otra (una base VACIA si la usa: es
;  la que prepara el administrador de SQL Server para quien no puede crear bases). La
;  reinstalacion y la actualizacion de una instalacion previa son de la version siguiente.
;
;  Se compila con Inno Setup 6 por medio de construir.ps1, que antes publica la
;  aplicacion en la carpeta "publicado". Este archivo esta guardado como UTF-8 con
;  marca de orden de bytes (los textos llevan tildes): conservar esa codificacion.
; =====================================================================================

#define Nombre_704ILR      "EvenTech"
#define Ejecutable_704ILR  "EvenTech.UI.exe"
#define Autor_704ILR       "Ivan Leonel Reser"
#define Publicado_704ILR   SourcePath + "publicado"

#if !FileExists(Publicado_704ILR + "\" + Ejecutable_704ILR)
  #error No se encuentra la aplicacion publicada. Ejecutar construir.ps1, que la publica antes de compilar.
#endif

; Medios de SQL Server Express que el instalador lleva adentro: carpeta con SETUP.EXE (la
; extraccion de los medios de Microsoft, sin modificar). construir.ps1 la pasa con
; /DMediosMotor_704ILR=<carpeta>; sin ella, el instalador solo busca los medios junto a el.
#ifdef MediosMotor_704ILR
  #if !FileExists(MediosMotor_704ILR + "\SETUP.EXE")
    #error La carpeta de los medios de SQL Server Express no tiene SETUP.EXE.
  #endif
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
VersionInfoVersion={#Mayor_704ILR}.{#Menor_704ILR}.{#Revision_704ILR}.{#Compilacion_704ILR}
VersionInfoProductName={#Nombre_704ILR}
VersionInfoDescription=Instalador de {#Nombre_704ILR}
; Sin permisos de administrador, {autopf} es la carpeta de programas del usuario
; (%LOCALAPPDATA%\Programs), donde siempre puede escribir.
DefaultDirName={autopf}\{#Nombre_704ILR}
DefaultGroupName={#Nombre_704ILR}
UninstallDisplayName={#Nombre_704ILR} {#Version_704ILR}
UninstallDisplayIcon={app}\{#Ejecutable_704ILR}
; Pasos del asistente: bienvenida, carpeta de destino, instancia de SQL Server (con la
; instalacion de SQL Server Express si hace falta), accesos directos, resumen,
; instalacion y finalizacion.
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
ShowLanguageDialog=no
; El asistente corre con los permisos del usuario: no pide ser administrador.
PrivilegesRequired=lowest
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
es.WelcomeLabel2=Este asistente instalará [name/ver] para el usuario de Windows que lo ejecuta, sin pedir permisos de administrador: copia la aplicación, crea la base de datos del sistema en la instancia de SQL Server que se elija y deja configurada la conexión.%n%nSe recomienda cerrar las demás aplicaciones antes de continuar.
es.FinishedLabel=La instalación de [name] finalizó: la aplicación quedó instalada, su base de datos creada y la conexión configurada.%n%nPara el primer ingreso se utiliza el usuario admin con la contraseña admin123.
es.FinishedLabelNoIcons=La instalación de [name] finalizó: la aplicación quedó instalada, su base de datos creada y la conexión configurada.%n%nPara el primer ingreso se utiliza el usuario admin con la contraseña admin123.
es.ConfirmUninstall=¿Desea quitar %1 de este equipo?%n%nLa base de datos EvenTechDB no se elimina: sus datos se conservan en SQL Server. Tampoco se elimina SQL Server.

[CustomMessages]
es.BienvenidaConMotor=Este asistente instalará %1 %2 para el usuario de Windows que lo ejecuta, con todo lo que necesita para funcionar: si el equipo no tiene SQL Server, instala SQL Server Express (incluido en este instalador); después crea la base de datos del sistema, copia la aplicación y deja configurada la conexión.%n%nNo hace falta ser administrador del equipo: Windows pide esa autorización solo si hay que instalar SQL Server.%n%nSe recomienda cerrar las demás aplicaciones antes de continuar.
es.CarpetaSinPermiso=El usuario de Windows no tiene permiso para escribir en la carpeta:%n%n%1%n%nElija una carpeta propia del usuario (la propuesta por el asistente siempre lo es).
es.InstanciaTitulo=Instancia de SQL Server
es.InstanciaDescripcion=¿En qué instancia se crea la base de datos del sistema?
es.InstanciaIntro=El asistente detectó las instancias de SQL Server instaladas en este equipo. Seleccione de la lista aquella en la que se creará la base de datos %1, o escriba su nombre.
es.InstanciaEtiqueta=&Instancia de SQL Server:
es.InstanciaNota=La base se crea con los datos de demostración del sistema. La conexión utiliza la autenticación integrada de Windows: no se solicita usuario ni contraseña.
es.InstanciaNinguna=No se detectó ninguna instancia de SQL Server en este equipo. %1 requiere SQL Server (Express, por ejemplo): se lo puede instalar y volver a ejecutar este asistente, o escribir el nombre de la instancia si ya está instalada.
es.InstanciaNingunaConMotor=No se detectó ninguna instancia de SQL Server en este equipo. %1 guarda sus datos en SQL Server Express, el motor de base de datos gratuito de Microsoft, que viene incluido en este instalador: para instalarlo basta con aceptar su licencia y presionar Siguiente.
es.InstanciaEnlace=Descargar SQL Server Express desde el sitio de Microsoft
es.InstanciaVacia=Debe seleccionar o escribir una instancia de SQL Server.
es.InstanciaInvalida=El nombre de la instancia no puede contener comillas.
es.MotorIncluido=Instalar SQL Server Express en la instancia «%1»
es.MotorIncluidoNueva=Instalar SQL Server Express en una instancia nueva, «%1»
es.MotorNota=La instalación del motor demora varios minutos (habitualmente entre 5 y 15) y necesita al menos 6 GB libres en el disco del sistema. Es el único paso que necesita a un administrador del equipo: Windows le pide la autorización. La conexión utiliza la autenticación integrada de Windows: no se solicita usuario ni contraseña.
es.MotorLicencia=Acepto los términos de licencia de Microsoft SQL Server Express
es.MotorLicenciaEnlace=Leer los términos de licencia de SQL Server
es.MotorFaltaLicencia=Para instalar SQL Server Express hay que aceptar los términos de licencia de Microsoft.
es.MotorProgresoTitulo=Instalación de SQL Server Express
es.MotorProgresoDescripcion=El asistente está instalando el motor de base de datos del sistema.
es.MotorExtrayendo=Preparando los archivos de SQL Server Express...
es.MotorProgresoTexto=Instalando SQL Server Express en la instancia %1...
es.MotorProgresoNota=Puede demorar varios minutos (habitualmente entre 5 y 15). No apague el equipo.
es.MotorSinNetFramework=Para instalar SQL Server Express, Windows necesita .NET Framework 4.7.2 o posterior, que viene incluido en Windows 10 versión 1803 y posteriores.%n%nActualice Windows con Windows Update y vuelva a ejecutar este asistente.
es.MotorSinEspacio=Para instalar SQL Server Express hacen falta al menos 6 GB libres en el disco del sistema (%1), y quedan %2 GB.%n%nLibere espacio y vuelva a intentar.
es.MotorSinAutorizacion=No se instaló SQL Server Express: instalarlo necesita la autorización de un administrador del equipo, y Windows no la recibió.%n%nSi el equipo ya tiene SQL Server, desmarque la opción y elija su instancia; si no, un administrador tiene que aceptar la autorización (o instalar SQL Server Express) y después se vuelve a ejecutar este asistente.
es.MotorFallo=La instalación de SQL Server Express terminó con el código %1 y la instancia no quedó disponible.%n%nEl detalle quedó en el registro de la instalación de SQL Server (%2).
es.MotorReinicioPrevio=No se pudo instalar SQL Server Express porque Windows tiene un reinicio pendiente (por ejemplo, de una actualización). Reinicie el equipo y vuelva a ejecutar este asistente.%n%nEl instalador de SQL Server terminó con el código %1; el detalle quedó en %2.
es.MotorReinicio=SQL Server Express quedó instalado, pero Windows pide reiniciar el equipo antes de usarlo. Reinicie el equipo y vuelva a ejecutar este asistente.
es.MotorNoDetectado=El instalador de SQL Server Express terminó sin error, pero el asistente no detecta la instancia «%1». Verifique la instalación de SQL Server y vuelva a ejecutar este asistente.
es.SugerenciaMotor=Si esa instancia no sirve, se puede marcar «Instalar SQL Server Express» para que el asistente cree una instancia nueva en este equipo.
es.ErrSinConexion=No se pudo establecer la conexión con la instancia «%1».%n%nVerifique que el nombre esté bien escrito y que el servicio de SQL Server esté iniciado.
es.ErrInstanciaRemota=La instancia «%1» pertenece a otro equipo (%2).%n%nEste asistente crea la base de datos en una instancia de SQL Server instalada en este mismo equipo.
es.ErrVersionAnterior=La instancia «%1» es %2.%n%nEl sistema requiere SQL Server 2012 o superior.
es.ErrSinPermiso=El usuario de Windows %2 no tiene permiso para crear bases de datos en la instancia «%1».%n%nHay dos salidas: ejecutar el asistente con un usuario que sea administrador de esa instancia de SQL Server, o pedirle a ese administrador que cree una base vacía llamada EvenTechDB y le dé a este usuario el rol de propietario (db_owner): el asistente le carga las tablas.
es.ErrBaseExistente=La instancia «%1» ya tiene una base de datos %2 con contenido.%n%nEsta versión del asistente realiza la primera instalación del sistema y no modifica una base existente (solo usa una base vacía). Elija otra instancia para continuar.
es.ErrNoPrevisto=No se pudo comprobar la instancia «%1».
es.ErrRestauracion=No se pudo crear la base de datos %1 en la instancia «%2». No se copió ningún archivo en el equipo.%n%nSe puede volver atrás para elegir otra instancia, o cancelar la instalación.
es.DetalleTecnico=Detalle informado por SQL Server:
es.RegistroEn=Registro de la instalación:
es.ProgresoTitulo=Creación de la base de datos
es.ProgresoDescripcion=El asistente está creando la base de datos del sistema.
es.ProgresoTexto=Creando la base de datos %1 en la instancia %2...
es.ProgresoNota=Esta operación puede demorar unos segundos.
es.MemoMotor=Motor de base de datos:
es.MemoMotorInstalado=SQL Server Express, instalado por este asistente en la instancia %1
es.MemoBase=Base de datos:
es.MemoInstancia=Instancia: %1 - %2
es.MemoNombre=Base: %1 (se crea con los datos de demostración)
es.MemoEsquema=Base: %1 (se crea con el guion del esquema, sin los datos de demostración: la versión de SQL Server no puede leer el respaldo)
es.MemoVacia=Base: %1 (ya existe y está vacía: se le cargan las tablas y los datos iniciales, sin los datos de demostración)
es.AvisoConexion=La aplicación quedó instalada y la base de datos creada, pero no se pudo guardar la configuración de la conexión.%n%nAl iniciar %1 se abre la pantalla de configuración de la conexión: allí se elige la instancia «%2».
es.AvisoAcceso=La base de datos %1 se creó, pero el usuario de Windows que inició la instalación no pudo abrirla.%n%nEl administrador de SQL Server tiene que darle acceso a esa base para que pueda usar el sistema.

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
#ifdef MediosMotor_704ILR
; SQL Server Express: solo se extrae si hay que instalarlo. Va en su propio bloque y sin
; comprimir (sus paquetes ya vienen comprimidos), asi que no demora el resto.
Source: "{#MediosMotor_704ILR}\*"; DestDir: "{tmp}\SQLServerExpress"; Flags: dontcopy recursesubdirs nocompression solidbreak
#endif
Source: "publicado\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs solidbreak

[Icons]
Name: "{group}\{#Nombre_704ILR}"; Filename: "{app}\{#Ejecutable_704ILR}"
Name: "{group}\{cm:UninstallProgram,{#Nombre_704ILR}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#Nombre_704ILR}"; Filename: "{app}\{#Ejecutable_704ILR}"; Tasks: escritorio

[Run]
Filename: "{app}\{#Ejecutable_704ILR}"; Description: "{cm:LaunchProgram,{#Nombre_704ILR}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  BaseDeDatos_704ILR = 'EvenTechDB';
  // Clave del registro donde SQL Server anota las instancias instaladas en el equipo.
  ClaveInstancias_704ILR = 'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL';
  // La instancia a la que apunta de fabrica la aplicacion: si esta, se la propone.
  InstanciaDeFabrica_704ILR = 'localhost\SQLEXPRESS';
  DescargaDelMotor_704ILR = 'https://www.microsoft.com/es-es/sql-server/sql-server-downloads';
  LicenciaDelMotor_704ILR = 'https://aka.ms/useterms';
  GuionBase_704ILR = 'BaseDeDatos.ps1';
  GuionConexion_704ILR = 'Conexion.ps1';
  GuionEsquema_704ILR = 'schema.sql';
  // Como se crea la base (lo decide el diagnostico de la instancia; ver BaseDeDatos.ps1).
  ModoRespaldo_704ILR = 'RESPALDO';
  ModoEsquema_704ILR = 'ESQUEMA';
  ModoVacia_704ILR = 'VACIA';
  // SQL Server Express adentro del instalador (ver [Files]).
#ifdef MediosMotor_704ILR
  MotorEmbebido_704ILR = True;
#else
  MotorEmbebido_704ILR = False;
#endif
  MediosEmbebidos_704ILR = '(incluidos en el instalador)';
  CarpetaMotorEmbebido_704ILR = '{tmp}\SQLServerExpress';
  // Requisitos de SQL Server 2022 Express que el asistente comprueba antes de instalarlo:
  // espacio libre en el disco del sistema y .NET Framework 4.7.2 (version 461808).
  EspacioDelMotorMB_704ILR = 6144;
  NetFrameworkMinimo_704ILR = 461808;
  // Windows: la autorizacion de administrador (UAC) se cancelo.
  ErrorCancelado_704ILR = 1223;

var
  PaginaInstancia_704ILR: TWizardPage;
  ComboInstancia_704ILR: TNewComboBox;
  ChkMotor_704ILR, ChkLicencia_704ILR: TNewCheckBox;
  PaginaProgreso_704ILR, PaginaMotor_704ILR: TOutputProgressWizardPage;
  InstanciaElegida_704ILR: String;
  MotorDetectado_704ILR: String;
  ModoBase_704ILR: String;
  // Marca de lo que creo ESTA ejecucion en la base (vacia: nada). Ver Quitar.
  MarcaDeLaBase_704ILR: String;
  InstanciaDeLaBase_704ILR: String;
  InstalacionCompleta_704ILR: Boolean;
  // Medios de SQL Server Express: el SETUP.EXE junto al instalador, MediosEmbebidos_704ILR
  // o vacio (no hay). OfrecerMotor: la pagina de la instancia ofrece instalarlo.
  MediosDelMotor_704ILR: String;
  OfrecerMotor_704ILR: Boolean;
  // Instancia que creo ESTA ejecucion al instalar SQL Server Express (vacia: ninguna) y
  // si el instalador de SQL Server pidio reiniciar el equipo al terminar.
  InstanciaCreada_704ILR: String;
  ReinicioDelMotor_704ILR: Boolean;

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
#ifdef PruebaSinInstancias_704ILR
  // Solo en la variante de prueba con que se capturan las pantallas de un equipo sin SQL
  // Server (construir.ps1 nunca lo define): no se lee el registro.
  Exit;
#endif
  if IsWin64 then AgregarInstanciasDe_704ILR(HKLM64, Lista_704ILR);
  AgregarInstanciasDe_704ILR(HKLM32, Lista_704ILR);
end;

// Medios de SQL Server Express: junto al instalador, la carpeta extraida (SETUP.EXE) o
// el paquete autoextraible de Microsoft (SQLEXPR*.EXE); si no, los incluidos.
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
  if (Result = '') and MotorEmbebido_704ILR then Result := MediosEmbebidos_704ILR;
end;

// Nombre de la instancia que se crea: SQLEXPRESS (la de fabrica de la aplicacion) o,
// si ya existe una con ese nombre, EVENTECH (EVENTECH2 a EVENTECH9 si tambien existe).
function InstanciaNueva_704ILR: String;
var
  Instancias_704ILR: TStringList;
  n_704ILR: Integer;
begin
  Instancias_704ILR := TStringList.Create;
  try
    DetectarInstancias_704ILR(Instancias_704ILR);
    if Instancias_704ILR.IndexOf(InstanciaDeFabrica_704ILR) < 0 then
      Result := 'SQLEXPRESS'
    else
    begin
      Result := 'EVENTECH';
      n_704ILR := 2;
      while (Instancias_704ILR.IndexOf('localhost\' + Result) >= 0) and (n_704ILR <= 9) do
      begin
        Result := 'EVENTECH' + IntToStr(n_704ILR);
        n_704ILR := n_704ILR + 1;
      end;
    end;
  finally
    Instancias_704ILR.Free;
  end;
end;

// Windows tiene un reinicio pendiente (de una actualizacion o de un programa recien
// instalado): el instalador de SQL Server no sigue en ese estado.
function ReinicioPendiente_704ILR: Boolean;
var
  Pendientes_704ILR: String;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') or
    RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') or
    (RegQueryMultiStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager', 'PendingFileRenameOperations', Pendientes_704ILR) and
     (Trim(Pendientes_704ILR) <> ''));
end;

// Carpeta donde el instalador de SQL Server deja su registro: depende de la version de
// los medios (16.x -> 160).
function CarpetaDeRegistrosDelMotor_704ILR(Programa_704ILR: String): String;
var
  Version_704ILR: String;
begin
  Result := ExpandConstant('{commonpf}\Microsoft SQL Server\');
  if GetVersionNumbersString(Programa_704ILR, Version_704ILR) and (Pos('.', Version_704ILR) > 1) then
    Result := Result + Copy(Version_704ILR, 1, Pos('.', Version_704ILR) - 1) + '0\Setup Bootstrap\Log'
  else
    Result := Result + '...\Setup Bootstrap\Log';
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
// cuenta de quien inicio la instalacion (es la misma, salvo que se haya ejecutado el
// asistente "como administrador" con otra cuenta).
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

// Carpeta de apoyo en los datos de programas del equipo (%ProgramData%\EvenTech), donde
// cualquier usuario puede escribir: ahi se deja lo que tiene que leer otra cuenta (el
// respaldo, que lee el servicio de SQL Server; los guiones que corren con la cuenta de
// quien inicio la instalacion). Lleva la hora en el nombre: si quedara una de otra
// ejecucion u otro usuario, no se la toca. Vacia si no se la pudo crear.
function CrearCarpetaDeApoyo_704ILR: String;
begin
  Result := ExpandConstant('{commonappdata}\{#Nombre_704ILR}\instalacion_') +
    GetDateTimeString('yyyymmddhhnnss', #0, #0);
  if not ForceDirectories(Result) then
  begin
    Log('No se pudo crear la carpeta de apoyo ' + Result);
    Result := '';
  end;
end;

procedure QuitarCarpetaDeApoyo_704ILR(Apoyo_704ILR: String);
begin
  if Apoyo_704ILR = '' then Exit;
  DelTree(Apoyo_704ILR, True, True, True);
  // La carpeta superior se quita solo si quedo vacia (la aplicacion guarda ahi su clave).
  RemoveDir(ExtractFileDir(Apoyo_704ILR));
end;

// Cuentas de Windows que administran la instancia que se crea, entre comillas y separadas
// por un espacio, como las pide el instalador de SQL Server: el grupo de administradores
// del equipo (con su nombre en el idioma de Windows) y quien inicio la instalacion, que
// es quien va a usar el sistema (si un administrador autoriza la instalacion con su
// propia cuenta, esa la agrega el parametro /ADDCURRENTUSERASSQLADMIN). Vacio si no se
// las pudo averiguar.
function CuentasAdministradoras_704ILR(Instancia_704ILR: String): String;
var
  Administradores_704ILR, Usuario_704ILR: String;
  Resultado_704ILR: TStringList;
begin
  Result := '';
  Resultado_704ILR := TStringList.Create;
  try
    if EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
         '-Accion_704ILR Cuentas -Instancia_704ILR "' + Instancia_704ILR + '"', False, Resultado_704ILR) = 0 then
    begin
      Administradores_704ILR := Valor_704ILR(Resultado_704ILR, 'ADMINISTRADORES');
      Usuario_704ILR := Valor_704ILR(Resultado_704ILR, 'USUARIO');
      if Administradores_704ILR <> '' then Result := '"' + Administradores_704ILR + '"';
      if (Usuario_704ILR <> '') and (CompareText(Usuario_704ILR, Administradores_704ILR) <> 0) then
        Result := Trim(Result + ' "' + Usuario_704ILR + '"');
    end;
  finally
    Resultado_704ILR.Free;
  end;
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
  else if Codigo_704ILR = 'BASE_EXISTENTE' then
    Result := FmtMessage(CustomMessage('ErrBaseExistente'), [Instancia_704ILR, BaseDeDatos_704ILR])
  else
  begin
    if Codigo_704ILR = 'SIN_CONEXION' then
      Result := FmtMessage(CustomMessage('ErrSinConexion'), [Instancia_704ILR])
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
      '-Accion_704ILR Diagnosticar -Instancia_704ILR "' + Instancia_704ILR + '"', False, Resultado_704ILR) = 0;
    if Result then
    begin
      MotorDetectado_704ILR := Valor_704ILR(Resultado_704ILR, 'MOTOR');
      ModoBase_704ILR := Valor_704ILR(Resultado_704ILR, 'MODO');
    end
    else
      Mensaje_704ILR := MensajeDeDiagnostico_704ILR(Instancia_704ILR, Resultado_704ILR);
  finally
    Resultado_704ILR.Free;
  end;
end;

// Instala SQL Server Express en modo silencioso, con los parametros documentados por
// Microsoft para una instalacion desde la linea de comandos (los mismos que usa su propio
// instalador basico: solo el motor, sin conexiones de red, sin buscar actualizaciones).
// El asistente corre sin permisos de administrador: el instalador de SQL Server se lanza
// pidiendo la elevacion a Windows (UAC), que es el unico paso que la necesita. Antes
// comprueba los requisitos que se pueden ver desde aca. Devuelve '' si la instancia
// quedo disponible, o el mensaje para quien instala.
function InstalarMotor_704ILR(Instancia_704ILR: String): String;
var
  Programa_704ILR, Parametros_704ILR, Cuentas_704ILR, Mensaje_704ILR: String;
  Codigo_704ILR: Integer;
  NetFramework_704ILR: Cardinal;
  Libre_704ILR, Total_704ILR: Int64;
  Instancias_704ILR: TStringList;
  Registrada_704ILR, Lanzado_704ILR: Boolean;
begin
  Result := '';
  if not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', NetFramework_704ILR) or
     (NetFramework_704ILR < NetFrameworkMinimo_704ILR) then
  begin
    Result := CustomMessage('MotorSinNetFramework');
    Exit;
  end;
  if GetSpaceOnDisk64(ExpandConstant('{sd}\'), Libre_704ILR, Total_704ILR) and
     (Libre_704ILR div 1048576 < EspacioDelMotorMB_704ILR) then
  begin
    Result := FmtMessage(CustomMessage('MotorSinEspacio'), [ExpandConstant('{sd}'), IntToStr(Libre_704ILR div 1073741824)]);
    Exit;
  end;

  PaginaMotor_704ILR.SetText(FmtMessage(CustomMessage('MotorProgresoTexto'), [Instancia_704ILR]), CustomMessage('MotorProgresoNota'));
  PaginaMotor_704ILR.SetProgress(0, 100);
  PaginaMotor_704ILR.ProgressBar.Style := npbstMarquee;
  PaginaMotor_704ILR.Show;
  Lanzado_704ILR := False;
  try
    Cuentas_704ILR := CuentasAdministradoras_704ILR('localhost\' + Instancia_704ILR);
    Programa_704ILR := MediosDelMotor_704ILR;
    if Programa_704ILR = MediosEmbebidos_704ILR then
    begin
      // Los medios incluidos se extraen recien ahora, y una sola vez por ejecucion.
      Programa_704ILR := ExpandConstant(CarpetaMotorEmbebido_704ILR + '\SETUP.EXE');
      if not FileExists(Programa_704ILR) then
      begin
        PaginaMotor_704ILR.SetText(CustomMessage('MotorExtrayendo'), CustomMessage('MotorProgresoNota'));
        try
          ExtractTemporaryFiles(CarpetaMotorEmbebido_704ILR + '\*');
        except
          Log('No se pudieron extraer los medios de SQL Server Express: ' + GetExceptionMessage);
        end;
        PaginaMotor_704ILR.SetText(FmtMessage(CustomMessage('MotorProgresoTexto'), [Instancia_704ILR]), CustomMessage('MotorProgresoNota'));
      end;
    end;

    Parametros_704ILR := '/Q /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=' + Instancia_704ILR +
      ' /ADDCURRENTUSERASSQLADMIN=True';
    if Cuentas_704ILR <> '' then
      Parametros_704ILR := Parametros_704ILR + ' /SQLSYSADMINACCOUNTS=' + Cuentas_704ILR;
    Parametros_704ILR := Parametros_704ILR + ' /SQLSVCSTARTUPTYPE=Automatic /TCPENABLED=0 /NPENABLED=0' +
      ' /UPDATEENABLED=False /IACCEPTSQLSERVERLICENSETERMS /SUPPRESSPRIVACYSTATEMENTNOTICE';
    Log('Instalacion de SQL Server Express: ' + Programa_704ILR + ' ' + Parametros_704ILR);
    Codigo_704ILR := -1;
    if FileExists(Programa_704ILR) then
    begin
#ifdef ForzarElevacion_704ILR
      // Solo en la variante de prueba (construir.ps1 nunca lo define): aunque la consola de
      // las pruebas ya este elevada, se recorre el camino que pide la elevacion a Windows.
      if False then
#else
      if IsAdmin then
#endif
        // El asistente ya corre elevado (se lo ejecuto "como administrador"): no hay que pedir nada.
        Lanzado_704ILR := Exec(Programa_704ILR, Parametros_704ILR, ExtractFileDir(Programa_704ILR), SW_HIDE, ewWaitUntilTerminated, Codigo_704ILR)
      else
        // Windows pide la autorizacion de un administrador solo para este programa.
        Lanzado_704ILR := ShellExec('runas', Programa_704ILR, Parametros_704ILR, ExtractFileDir(Programa_704ILR), SW_HIDE, ewWaitUntilTerminated, Codigo_704ILR);
      if not Lanzado_704ILR then
      begin
        Log('No se pudo lanzar el instalador de SQL Server Express: ' + IntToStr(Codigo_704ILR) + ' ' + SysErrorMessage(Codigo_704ILR));
        if Codigo_704ILR = ErrorCancelado_704ILR then
        begin
          Result := CustomMessage('MotorSinAutorizacion');
          Exit;
        end;
        Codigo_704ILR := -1;
      end;
    end;
    Log('SQL Server Express termino con el codigo ' + IntToStr(Codigo_704ILR));
  finally
    PaginaMotor_704ILR.Hide;
  end;

  Instancias_704ILR := TStringList.Create;
  try
    DetectarInstancias_704ILR(Instancias_704ILR);
    Registrada_704ILR := Instancias_704ILR.IndexOf('localhost\' + Instancia_704ILR) >= 0;
    if Registrada_704ILR then
    begin
      ComboInstancia_704ILR.Items.Assign(Instancias_704ILR);
      ComboInstancia_704ILR.Text := 'localhost\' + Instancia_704ILR;
    end;
  finally
    Instancias_704ILR.Free;
  end;

  if Codigo_704ILR = 3010 then
  begin
    // Instalado, pero Windows pide reiniciar: si la instancia ya responde, la instalacion
    // sigue y el asistente ofrece reiniciar al terminar; si no, hay que reiniciar antes.
    if Registrada_704ILR and DiagnosticarInstancia_704ILR('localhost\' + Instancia_704ILR, Mensaje_704ILR) then
      ReinicioDelMotor_704ILR := True
    else
      Result := CustomMessage('MotorReinicio');
  end
  else if not Registrada_704ILR then
  begin
    if Codigo_704ILR = 0 then
      Result := FmtMessage(CustomMessage('MotorNoDetectado'), ['localhost\' + Instancia_704ILR])
    else if ReinicioPendiente_704ILR then
      Result := FmtMessage(CustomMessage('MotorReinicioPrevio'), [IntToStr(Codigo_704ILR), CarpetaDeRegistrosDelMotor_704ILR(Programa_704ILR)])
    else
      Result := FmtMessage(CustomMessage('MotorFallo'), [IntToStr(Codigo_704ILR), CarpetaDeRegistrosDelMotor_704ILR(Programa_704ILR)]);
  end;
  if Result = '' then InstanciaCreada_704ILR := 'localhost\' + Instancia_704ILR;
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
  Pedida_704ILR: String;
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
    // Motor de base de datos: con los medios a mano, se ofrece instalarlo (marcado si el
    // equipo no tiene ninguna instancia; si tiene, se usa la que esta).
    OfrecerMotor_704ILR := MediosDelMotor_704ILR <> '';
    if Instancias_704ILR.Count > 0 then
      Intro_704ILR.Caption := FmtMessage(CustomMessage('InstanciaIntro'), [BaseDeDatos_704ILR])
    else if OfrecerMotor_704ILR then
      Intro_704ILR.Caption := FmtMessage(CustomMessage('InstanciaNingunaConMotor'), ['{#Nombre_704ILR}'])
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
    // instalacion desatendida), la de fabrica de la aplicacion o la primera detectada.
    Pedida_704ILR := ExpandConstant('{param:INSTANCIA|}');
    if Pedida_704ILR <> '' then
      ComboInstancia_704ILR.Text := Pedida_704ILR
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
    // Sin instancias, la nota sobre la base de demostracion deja lugar a la del motor.
    if (Instancias_704ILR.Count = 0) and OfrecerMotor_704ILR then
      Nota_704ILR.Caption := CustomMessage('MotorNota')
    else
      Nota_704ILR.Caption := CustomMessage('InstanciaNota');
    Nota_704ILR.AdjustHeight;

    if OfrecerMotor_704ILR then
    begin
      ChkMotor_704ILR := TNewCheckBox.Create(PaginaInstancia_704ILR);
      ChkMotor_704ILR.Parent := PaginaInstancia_704ILR.Surface;
      ChkMotor_704ILR.SetBounds(0, Nota_704ILR.Top + Nota_704ILR.Height + ScaleY(12), PaginaInstancia_704ILR.SurfaceWidth, ScaleY(17));
      if Instancias_704ILR.Count = 0 then
        ChkMotor_704ILR.Caption := FmtMessage(CustomMessage('MotorIncluido'), ['localhost\' + InstanciaNueva_704ILR])
      else
        ChkMotor_704ILR.Caption := FmtMessage(CustomMessage('MotorIncluidoNueva'), ['localhost\' + InstanciaNueva_704ILR]);
      ChkMotor_704ILR.Checked := (Instancias_704ILR.Count = 0) or (ExpandConstant('{param:MOTOR|0}') = '1');
      ChkMotor_704ILR.OnClick := @MotorCambiado_704ILR;
      ChkLicencia_704ILR := TNewCheckBox.Create(PaginaInstancia_704ILR);
      ChkLicencia_704ILR.Parent := PaginaInstancia_704ILR.Surface;
      ChkLicencia_704ILR.SetBounds(ScaleX(16), ChkMotor_704ILR.Top + ChkMotor_704ILR.Height + ScaleY(6), PaginaInstancia_704ILR.SurfaceWidth - ScaleX(16), ScaleY(17));
      ChkLicencia_704ILR.Caption := CustomMessage('MotorLicencia');
      // Una instalacion desatendida nunca acepta la licencia por su cuenta.
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

procedure InitializeWizard;
begin
  // Los guiones se extraen una sola vez a la carpeta temporal del asistente.
  ExtractTemporaryFile(GuionBase_704ILR);
  ExtractTemporaryFile(GuionConexion_704ILR);

  MediosDelMotor_704ILR := BuscarMediosDelMotor_704ILR;
  if MediosDelMotor_704ILR <> '' then
    WizardForm.WelcomeLabel2.Caption := FmtMessage(CustomMessage('BienvenidaConMotor'), ['{#Nombre_704ILR}', '{#Version_704ILR}']);

  CrearPaginaInstancia_704ILR;
  PaginaProgreso_704ILR := CreateOutputProgressPage(
    CustomMessage('ProgresoTitulo'), CustomMessage('ProgresoDescripcion'));
  PaginaMotor_704ILR := CreateOutputProgressPage(
    CustomMessage('MotorProgresoTitulo'), CustomMessage('MotorProgresoDescripcion'));
end;

// La carpeta de destino tiene que admitir escritura con los permisos del usuario (una
// carpeta de Archivos de programa, por ejemplo, no la admite sin ser administrador). Se
// prueba escribiendo un archivo; lo que se creo para la prueba se quita.
function CarpetaEscribible_704ILR(Carpeta_704ILR: String): Boolean;
var
  Existia_704ILR: Boolean;
  Prueba_704ILR: String;
begin
  Existia_704ILR := DirExists(Carpeta_704ILR);
  Result := ForceDirectories(Carpeta_704ILR);
  if not Result then Exit;
  Prueba_704ILR := AddBackslash(Carpeta_704ILR) + 'eventech_prueba_de_escritura.tmp';
  Result := SaveStringToFile(Prueba_704ILR, 'EvenTech', False);
  DeleteFile(Prueba_704ILR);
  if not Existia_704ILR then RemoveDir(Carpeta_704ILR);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Instancia_704ILR, Mensaje_704ILR: String;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if not CarpetaEscribible_704ILR(WizardDirValue) then
    begin
      SuppressibleMsgBox(FmtMessage(CustomMessage('CarpetaSinPermiso'), [WizardDirValue]), mbError, MB_OK, IDOK);
      Result := False;
    end;
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
  if Mensaje_704ILR = '' then
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
    InstanciaElegida_704ILR := Instancia_704ILR
  else
  begin
    // Con una instancia que no sirve, se recuerda que el asistente puede crear una.
    if (ChkMotor_704ILR <> nil) and not ChkMotor_704ILR.Checked and (InstanciaCreada_704ILR = '') then
      Mensaje_704ILR := Mensaje_704ILR + #13#10#13#10 + CustomMessage('SugerenciaMotor');
    Log('Instancia rechazada: ' + Mensaje_704ILR);
    SuppressibleMsgBox(Mensaje_704ILR, mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := '';
  if MemoDirInfo <> '' then Result := Result + MemoDirInfo + NewLine + NewLine;
  if InstanciaCreada_704ILR <> '' then
    Result := Result + CustomMessage('MemoMotor') + NewLine + Space +
      FmtMessage(CustomMessage('MemoMotorInstalado'), [InstanciaCreada_704ILR]) + NewLine + NewLine;
  Result := Result + CustomMessage('MemoBase') + NewLine +
    Space + FmtMessage(CustomMessage('MemoInstancia'), [InstanciaElegida_704ILR, MotorDetectado_704ILR]) + NewLine + Space;
  if ModoBase_704ILR = ModoEsquema_704ILR then
    Result := Result + FmtMessage(CustomMessage('MemoEsquema'), [BaseDeDatos_704ILR])
  else if ModoBase_704ILR = ModoVacia_704ILR then
    Result := Result + FmtMessage(CustomMessage('MemoVacia'), [BaseDeDatos_704ILR])
  else
    Result := Result + FmtMessage(CustomMessage('MemoNombre'), [BaseDeDatos_704ILR]);
  if MemoTasksInfo <> '' then Result := Result + NewLine + NewLine + MemoTasksInfo;
end;

// ------------------------------------------------------------------------ instalacion

// La base se crea ANTES de copiar la aplicacion: es el paso que puede fallar y, si
// falla, el equipo queda como estaba y se puede volver atras a elegir otra instancia.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Resultado_704ILR: TStringList;
  Apoyo_704ILR, Respaldo_704ILR, Argumentos_704ILR, Detalle_704ILR: String;
begin
  Result := '';
  // Si un intento anterior de esta misma ejecucion ya la creo ahi, no se repite.
  if (MarcaDeLaBase_704ILR <> '') and (InstanciaDeLaBase_704ILR = InstanciaElegida_704ILR) then Exit;

  PaginaProgreso_704ILR.SetText(
    FmtMessage(CustomMessage('ProgresoTexto'), [BaseDeDatos_704ILR, InstanciaElegida_704ILR]),
    CustomMessage('ProgresoNota'));
  PaginaProgreso_704ILR.SetProgress(0, 100);
  PaginaProgreso_704ILR.ProgressBar.Style := npbstMarquee;
  PaginaProgreso_704ILR.Show;
  Resultado_704ILR := TStringList.Create;
  Apoyo_704ILR := '';
  Respaldo_704ILR := '';
  try
    if (ModoBase_704ILR = ModoEsquema_704ILR) or (ModoBase_704ILR = ModoVacia_704ILR) then
    begin
      // Sin el respaldo: la base se crea (o se carga, si ya existe vacia) con el esquema.
      ExtractTemporaryFile(GuionEsquema_704ILR);
      if ModoBase_704ILR = ModoEsquema_704ILR then
        Argumentos_704ILR := '-Accion_704ILR Crear'
      else
        Argumentos_704ILR := '-Accion_704ILR Cargar';
      Argumentos_704ILR := Argumentos_704ILR + ' -Instancia_704ILR "' + InstanciaElegida_704ILR + '"' +
        ' -Esquema_704ILR "' + ExpandConstant('{tmp}\') + GuionEsquema_704ILR + '"';
    end
    else
    begin
      // El respaldo lo lee la cuenta del servicio de SQL Server, que no puede entrar a la
      // carpeta temporal del usuario: se lo deja en la carpeta de apoyo.
      ExtractTemporaryFile(BaseDeDatos_704ILR + '.bak');
      Respaldo_704ILR := ExpandConstant('{tmp}\') + BaseDeDatos_704ILR + '.bak';
      Apoyo_704ILR := CrearCarpetaDeApoyo_704ILR;
      if (Apoyo_704ILR <> '') and FileCopy(Respaldo_704ILR, AddBackslash(Apoyo_704ILR) + BaseDeDatos_704ILR + '.bak', False) then
      begin
        DeleteFile(Respaldo_704ILR);
        Respaldo_704ILR := AddBackslash(Apoyo_704ILR) + BaseDeDatos_704ILR + '.bak';
      end;
      Argumentos_704ILR := '-Accion_704ILR Restaurar -Instancia_704ILR "' + InstanciaElegida_704ILR + '"' +
        ' -Respaldo_704ILR "' + Respaldo_704ILR + '"';
    end;

    if EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR, Argumentos_704ILR, False, Resultado_704ILR) = 0 then
    begin
      MarcaDeLaBase_704ILR := Valor_704ILR(Resultado_704ILR, 'MARCA');
      InstanciaDeLaBase_704ILR := InstanciaElegida_704ILR;
    end
    else
    begin
      if Valor_704ILR(Resultado_704ILR, 'CODIGO') = 'BASE_EXISTENTE' then
        Result := FmtMessage(CustomMessage('ErrBaseExistente'), [InstanciaElegida_704ILR, BaseDeDatos_704ILR])
      else
      begin
        Result := FmtMessage(CustomMessage('ErrRestauracion'), [BaseDeDatos_704ILR, InstanciaElegida_704ILR]);
        Detalle_704ILR := Valor_704ILR(Resultado_704ILR, 'DETALLE');
        if Detalle_704ILR <> '' then
          Result := Result + #13#10#13#10 + CustomMessage('DetalleTecnico') + #13#10 + Detalle_704ILR;
      end;
      Result := Result + #13#10#13#10 + CustomMessage('RegistroEn') + #13#10 + ExpandConstant('{log}');
    end;
    if Respaldo_704ILR <> '' then DeleteFile(Respaldo_704ILR);
  finally
    Resultado_704ILR.Free;
    QuitarCarpetaDeApoyo_704ILR(Apoyo_704ILR);
    PaginaProgreso_704ILR.Hide;
  end;
end;

// Con la aplicacion ya copiada: se guarda la conexion en el perfil de quien inicio la
// instalacion y se comprueba, con esa misma cuenta, que pueda abrir la base. Si el
// asistente se ejecuto "como administrador" con otra cuenta, esa cuenta no puede leer
// la carpeta temporal del asistente: los guiones se copian a la carpeta de apoyo.
procedure ConfigurarConexion_704ILR;
var
  Resultado_704ILR: TStringList;
  Apoyo_704ILR, Carpeta_704ILR, Detalle_704ILR, Aviso_704ILR: String;
begin
  Apoyo_704ILR := CrearCarpetaDeApoyo_704ILR;
  Carpeta_704ILR := ExpandConstant('{tmp}');
  if (Apoyo_704ILR <> '') and
     FileCopy(ExpandConstant('{tmp}\') + GuionConexion_704ILR, AddBackslash(Apoyo_704ILR) + GuionConexion_704ILR, False) and
     FileCopy(ExpandConstant('{tmp}\') + GuionBase_704ILR, AddBackslash(Apoyo_704ILR) + GuionBase_704ILR, False) then
    Carpeta_704ILR := Apoyo_704ILR;
  Resultado_704ILR := TStringList.Create;
  try
    Aviso_704ILR := '';
    if EjecutarGuion_704ILR(Carpeta_704ILR, GuionConexion_704ILR,
         '-Instancia_704ILR "' + InstanciaElegida_704ILR + '"', True, Resultado_704ILR) <> 0 then
      Aviso_704ILR := FmtMessage(CustomMessage('AvisoConexion'), ['{#Nombre_704ILR}', InstanciaElegida_704ILR])
    else if EjecutarGuion_704ILR(Carpeta_704ILR, GuionBase_704ILR,
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
    QuitarCarpetaDeApoyo_704ILR(Apoyo_704ILR);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Desde aca la aplicacion ya esta copiada: la base creada queda con la instalacion.
    InstalacionCompleta_704ILR := True;
    ConfigurarConexion_704ILR;
  end;
end;

// El instalador de SQL Server pidio reiniciar y la instancia ya respondia: la instalacion
// siguio y la pagina final ofrece reiniciar el equipo.
function NeedRestart: Boolean;
begin
  Result := ReinicioDelMotor_704ILR;
end;

// La instancia elegida queda registrada junto con la instalacion: es el dato con el
// que la reinstalacion sabe donde esta la base del sistema.
procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'Instancia', InstanciaElegida_704ILR);
end;

// Instalacion cancelada o fallida despues de crear la base: se la quita (o, si era una
// base vacia existente, se la deja vacia), para que el equipo quede como estaba y un
// nuevo intento no la encuentre "existente". El guion solo deshace lo que tiene la marca
// de esta ejecucion. SQL Server Express, si se lo instalo, queda instalado.
procedure DeinitializeSetup;
var
  Resultado_704ILR: TStringList;
begin
  if (MarcaDeLaBase_704ILR = '') or InstalacionCompleta_704ILR then Exit;
  Resultado_704ILR := TStringList.Create;
  try
    EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
      '-Accion_704ILR Quitar -Instancia_704ILR "' + InstanciaDeLaBase_704ILR + '"' +
      ' -Marca_704ILR "' + MarcaDeLaBase_704ILR + '"', False, Resultado_704ILR);
  finally
    Resultado_704ILR.Free;
  end;
end;
