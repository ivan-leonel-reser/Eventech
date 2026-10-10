; =====================================================================================
;  EvenTech - asistente de instalacion (primera version: instalacion inicial)
;
;  Despliega el sistema en un equipo que todavia no lo tiene:
;    1. copia la aplicacion, que lleva incluido el entorno de ejecucion de .NET 8;
;    2. crea la base de datos del sistema, con sus datos de demostracion, en la
;       instancia de SQL Server que elige quien instala;
;    3. deja configurada la conexion de la aplicacion a esa instancia.
;
;  Quien instala es el usuario final: no se le pide ninguna herramienta de linea de
;  comandos ni escribir una cadena de conexion. Las instancias de SQL Server del
;  equipo se detectan y se ofrecen en una lista.
;
;  Esta version no modifica una base de datos existente: si la instancia elegida ya
;  tiene la base del sistema, lo informa y pide elegir otra. La reinstalacion y la
;  actualizacion de una instalacion previa corresponden a la version siguiente.
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

; La version se lee del ejecutable publicado (propiedad Version de EvenTech.UI.csproj).
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
DefaultDirName={autopf}\{#Nombre_704ILR}
DefaultGroupName={#Nombre_704ILR}
UninstallDisplayName={#Nombre_704ILR} {#Version_704ILR}
UninstallDisplayIcon={app}\{#Ejecutable_704ILR}
; Pasos del asistente: bienvenida, carpeta de destino, instancia de SQL Server,
; accesos directos, resumen, instalacion y finalizacion.
DisableWelcomePage=no
DisableDirPage=no
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
es.WelcomeLabel2=Este asistente instalará [name/ver] en este equipo: copia la aplicación, crea la base de datos del sistema en la instancia de SQL Server que se elija y deja configurada la conexión.%n%nSe recomienda cerrar las demás aplicaciones antes de continuar.
es.FinishedLabel=La instalación de [name] finalizó: la aplicación quedó instalada, su base de datos creada y la conexión configurada.%n%nPara el primer ingreso se utiliza el usuario admin con la contraseña admin123.
es.FinishedLabelNoIcons=La instalación de [name] finalizó: la aplicación quedó instalada, su base de datos creada y la conexión configurada.%n%nPara el primer ingreso se utiliza el usuario admin con la contraseña admin123.
es.ConfirmUninstall=¿Desea quitar %1 de este equipo?%n%nLa base de datos EvenTechDB no se elimina: sus datos se conservan en SQL Server.

[CustomMessages]
es.InstanciaTitulo=Instancia de SQL Server
es.InstanciaDescripcion=¿En qué instancia se crea la base de datos del sistema?
es.InstanciaIntro=El asistente detectó las instancias de SQL Server instaladas en este equipo. Seleccione de la lista aquella en la que se creará la base de datos %1, o escriba su nombre.
es.InstanciaEtiqueta=&Instancia de SQL Server:
es.InstanciaNota=La base se crea con los datos de demostración del sistema. La conexión utiliza la autenticación integrada de Windows: no se solicita usuario ni contraseña.
es.InstanciaNinguna=No se detectó ninguna instancia de SQL Server en este equipo. %1 requiere SQL Server Express 2019 o superior: se lo puede instalar y volver a ejecutar este asistente, o escribir el nombre de la instancia si ya está instalada.
es.InstanciaEnlace=Descargar SQL Server Express desde el sitio de Microsoft
es.InstanciaVacia=Debe seleccionar o escribir una instancia de SQL Server.
es.InstanciaInvalida=El nombre de la instancia no puede contener comillas.
es.ErrSinConexion=No se pudo establecer la conexión con la instancia «%1».%n%nVerifique que el nombre esté bien escrito y que el servicio de SQL Server esté iniciado.
es.ErrInstanciaRemota=La instancia «%1» pertenece a otro equipo (%2).%n%nEste asistente crea la base de datos en una instancia de SQL Server instalada en este mismo equipo.
es.ErrVersionAnterior=La instancia «%1» es %2.%n%nEl sistema requiere SQL Server 2019 o superior.
es.ErrSinPermiso=El usuario de Windows %2 no tiene permiso para crear bases de datos en la instancia «%1».%n%nEjecute el asistente con un usuario que sea administrador de esa instancia de SQL Server.
es.ErrBaseExistente=La instancia «%1» ya tiene una base de datos %2.%n%nEsta versión del asistente realiza la primera instalación del sistema y no modifica una base existente. Elija otra instancia para continuar.
es.ErrNoPrevisto=No se pudo comprobar la instancia «%1».
es.ErrRestauracion=No se pudo crear la base de datos %1 en la instancia «%2». No se copió ningún archivo en el equipo.%n%nSe puede volver atrás para elegir otra instancia, o cancelar la instalación.
es.DetalleTecnico=Detalle informado por SQL Server:
es.RegistroEn=Registro de la instalación:
es.ProgresoTitulo=Creación de la base de datos
es.ProgresoDescripcion=El asistente está creando la base de datos del sistema.
es.ProgresoTexto=Creando la base de datos %1 en la instancia %2...
es.ProgresoNota=Esta operación puede demorar unos segundos.
es.MemoBase=Base de datos:
es.MemoInstancia=Instancia: %1 - %2
es.MemoNombre=Base: %1 (se crea con los datos de demostración)
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
Source: "..\db\EvenTechDB.bak"; Flags: dontcopy
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
  GuionBase_704ILR = 'BaseDeDatos.ps1';
  GuionConexion_704ILR = 'Conexion.ps1';

var
  PaginaInstancia_704ILR: TWizardPage;
  ComboInstancia_704ILR: TNewComboBox;
  PaginaProgreso_704ILR: TOutputProgressWizardPage;
  InstanciaElegida_704ILR: String;
  MotorDetectado_704ILR: String;
  // Fecha de creacion de la base que creo ESTA ejecucion. Vacia: no se creo ninguna.
  MarcaDeLaBase_704ILR: String;
  InstanciaDeLaBase_704ILR: String;
  InstalacionCompleta_704ILR: Boolean;

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
      MotorDetectado_704ILR := Valor_704ILR(Resultado_704ILR, 'MOTOR')
    else
      Mensaje_704ILR := MensajeDeDiagnostico_704ILR(Instancia_704ILR, Resultado_704ILR);
  finally
    Resultado_704ILR.Free;
  end;
end;

// ---------------------------------------------------------------- paginas del asistente

procedure AbrirDescargaDelMotor_704ILR(Sender: TObject);
var
  Codigo_704ILR: Integer;
begin
  ShellExecAsOriginalUser('open', DescargaDelMotor_704ILR, '', '', SW_SHOWNORMAL, ewNoWait, Codigo_704ILR);
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
    if Instancias_704ILR.Count > 0 then
      Intro_704ILR.Caption := FmtMessage(CustomMessage('InstanciaIntro'), [BaseDeDatos_704ILR])
    else
      Intro_704ILR.Caption := FmtMessage(CustomMessage('InstanciaNinguna'), ['{#Nombre_704ILR}']);
    Intro_704ILR.AdjustHeight;

    Etiqueta_704ILR := TNewStaticText.Create(PaginaInstancia_704ILR);
    Etiqueta_704ILR.Parent := PaginaInstancia_704ILR.Surface;
    Etiqueta_704ILR.Left := 0;
    Etiqueta_704ILR.Top := Intro_704ILR.Top + Intro_704ILR.Height + ScaleY(18);
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
    Nota_704ILR.SetBounds(0, ComboInstancia_704ILR.Top + ComboInstancia_704ILR.Height + ScaleY(18),
      PaginaInstancia_704ILR.SurfaceWidth, ScaleY(40));
    Nota_704ILR.Anchors := [akLeft, akTop, akRight];
    Nota_704ILR.Caption := CustomMessage('InstanciaNota');
    Nota_704ILR.AdjustHeight;

    if Instancias_704ILR.Count = 0 then
    begin
      Enlace_704ILR := TNewStaticText.Create(PaginaInstancia_704ILR);
      Enlace_704ILR.Parent := PaginaInstancia_704ILR.Surface;
      Enlace_704ILR.Left := 0;
      Enlace_704ILR.Top := Nota_704ILR.Top + Nota_704ILR.Height + ScaleY(18);
      Enlace_704ILR.Caption := CustomMessage('InstanciaEnlace');
      Enlace_704ILR.Cursor := crHand;
      Enlace_704ILR.Font.Color := clBlue;
      Enlace_704ILR.Font.Style := [fsUnderline];
      Enlace_704ILR.OnClick := @AbrirDescargaDelMotor_704ILR;
    end;
  finally
    Instancias_704ILR.Free;
  end;
end;

procedure InitializeWizard;
begin
  // Los guiones se extraen una sola vez a la carpeta temporal del asistente, que
  // solo pueden leer los administradores: de ahi los ejecuta la cuenta elevada.
  ExtractTemporaryFile(GuionBase_704ILR);
  ExtractTemporaryFile(GuionConexion_704ILR);

  CrearPaginaInstancia_704ILR;
  PaginaProgreso_704ILR := CreateOutputProgressPage(
    CustomMessage('ProgresoTitulo'), CustomMessage('ProgresoDescripcion'));
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Instancia_704ILR, Mensaje_704ILR: String;
begin
  Result := True;
  if CurPageID <> PaginaInstancia_704ILR.ID then Exit;

  Instancia_704ILR := Trim(ComboInstancia_704ILR.Text);
  if Instancia_704ILR = '' then
    Mensaje_704ILR := CustomMessage('InstanciaVacia')
  else if Pos('"', Instancia_704ILR) > 0 then
    Mensaje_704ILR := CustomMessage('InstanciaInvalida')
  else
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
  Result := Result + CustomMessage('MemoBase') + NewLine +
    Space + FmtMessage(CustomMessage('MemoInstancia'), [InstanciaElegida_704ILR, MotorDetectado_704ILR]) + NewLine +
    Space + FmtMessage(CustomMessage('MemoNombre'), [BaseDeDatos_704ILR]);
  if MemoTasksInfo <> '' then Result := Result + NewLine + NewLine + MemoTasksInfo;
end;

// ------------------------------------------------------------------------ instalacion

// La base se crea ANTES de copiar la aplicacion: es el paso que puede fallar y, si
// falla, el equipo queda como estaba y se puede volver atras a elegir otra instancia.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Resultado_704ILR: TStringList;
  Respaldo_704ILR, Detalle_704ILR: String;
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
  try
    ExtractTemporaryFile(BaseDeDatos_704ILR + '.bak');
    Respaldo_704ILR := ExpandConstant('{tmp}\') + BaseDeDatos_704ILR + '.bak';

    if EjecutarGuion_704ILR(ExpandConstant('{tmp}'), GuionBase_704ILR,
         '-Accion_704ILR Restaurar -Instancia_704ILR "' + InstanciaElegida_704ILR + '"' +
         ' -Respaldo_704ILR "' + Respaldo_704ILR + '"', False, Resultado_704ILR) = 0 then
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
    DeleteFile(Respaldo_704ILR);
  finally
    Resultado_704ILR.Free;
    PaginaProgreso_704ILR.Hide;
  end;
end;

// Con la aplicacion ya copiada: se guarda la conexion en el perfil de quien inicio la
// instalacion y se comprueba, con esa misma cuenta, que pueda abrir la base. Las dos
// cosas corren sin elevacion, y la carpeta temporal del asistente solo la leen los
// administradores: los guiones se copian a una carpeta de apoyo que se borra al terminar.
procedure ConfigurarConexion_704ILR;
var
  Resultado_704ILR: TStringList;
  Apoyo_704ILR, Detalle_704ILR, Aviso_704ILR: String;
begin
  Apoyo_704ILR := ExpandConstant('{commonappdata}\{#Nombre_704ILR}\instalacion');
  ForceDirectories(Apoyo_704ILR);
  Resultado_704ILR := TStringList.Create;
  try
    FileCopy(ExpandConstant('{tmp}\') + GuionConexion_704ILR, AddBackslash(Apoyo_704ILR) + GuionConexion_704ILR, False);
    FileCopy(ExpandConstant('{tmp}\') + GuionBase_704ILR, AddBackslash(Apoyo_704ILR) + GuionBase_704ILR, False);

    Aviso_704ILR := '';
    if EjecutarGuion_704ILR(Apoyo_704ILR, GuionConexion_704ILR,
         '-Instancia_704ILR "' + InstanciaElegida_704ILR + '"', True, Resultado_704ILR) <> 0 then
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
    // La carpeta superior se quita solo si quedo vacia (la aplicacion guarda ahi su clave).
    RemoveDir(ExtractFileDir(Apoyo_704ILR));
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

// La instancia elegida queda registrada junto con la instalacion: es el dato con el
// que la reinstalacion sabe donde esta la base del sistema.
procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'Instancia', InstanciaElegida_704ILR);
end;

// Instalacion cancelada o fallida despues de crear la base: se la quita, para que el
// equipo quede como estaba y un nuevo intento no la encuentre como "existente". El
// guion solo elimina la base cuya fecha de creacion es la de esta ejecucion.
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
