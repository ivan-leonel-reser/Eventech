using EvenTech.BLL;
using EvenTech.Services;

Console.WriteLine("== EvenTech smoke test v2 ==");

// ---------------------------------------------------------------------------
// Contador de fallos: la prueba tiene que poder FALLAR SOLA.
// Antes cada caso imprimia el valor obtenido y, entre parentesis, el esperado,
// pero nadie los comparaba: la corrida terminaba igual de "verde" aunque un
// numero no coincidiera, y el proceso devolvia siempre 0. Ahora toda
// verificacion pasa por Esperar_704ILR, que compara, marca la diferencia y suma
// un fallo; al cierre el programa informa el total y devuelve un codigo de
// salida distinto de cero si algo no coincidio.
//
// Ademas se lleva el resultado POR CASO: cada caso se abre con Caso_704ILR y
// termina aprobado, fallido u omitido. Un caso que no pudo correr por faltar un
// dato de la base (catalogo vacio, base de prueba que no se pudo crear) ya no
// pasa como verde: se declara con Omitir_704ILR, se cuenta aparte y el proceso
// devuelve 2. El "en 33 casos" del resumen era un literal que nadie contaba.
// ---------------------------------------------------------------------------
int fallos_704ILR = 0;
int verificaciones_704ILR = 0;
int casos_704ILR = 0, aprobados_704ILR = 0, fallidos_704ILR = 0, omitidos_704ILR = 0;
int fallosAlAbrir_704ILR = 0;
bool casoAbierto_704ILR = false, casoOmitido_704ILR = false;

string Mostrar_704ILR(object valor_704ILR) =>
    valor_704ILR == null ? "null"
    : valor_704ILR is decimal dec_704ILR ? dec_704ILR.ToString("0.00")
    : valor_704ILR is DateTime f_704ILR ? f_704ILR.ToString("yyyy-MM-dd")
    : valor_704ILR.ToString();

void Esperar_704ILR(string etiqueta_704ILR, object real_704ILR, object esperado_704ILR)
{
    verificaciones_704ILR++;
    bool ok_704ILR = object.Equals(real_704ILR, esperado_704ILR);
    if (!ok_704ILR) fallos_704ILR++;
    Console.WriteLine($"  {etiqueta_704ILR}: {Mostrar_704ILR(real_704ILR)} " +
                      $"(esperado {Mostrar_704ILR(esperado_704ILR)})" + (ok_704ILR ? "" : "   <-- DIFIERE"));
}

// Un caso que aborta por excepcion tambien es un fallo: se anota y la corrida
// sigue con el resto (antes una excepcion en [18] se llevaba puestos [19]-[33]).
void Excepcion_704ILR(string caso_704ILR, Exception ex_704ILR)
{
    fallos_704ILR++;
    Console.WriteLine($"  EXCEPCION en {caso_704ILR}: {ex_704ILR.GetType().Name}: {ex_704ILR.Message}   <-- DIFIERE");
}

// Cierra el caso abierto y lo clasifica: omitido, fallido (sumo fallos desde
// que se abrio) o aprobado.
void CerrarCaso_704ILR()
{
    if (!casoAbierto_704ILR) return;
    casoAbierto_704ILR = false;
    if (casoOmitido_704ILR) omitidos_704ILR++;
    else if (fallos_704ILR > fallosAlAbrir_704ILR) fallidos_704ILR++;
    else aprobados_704ILR++;
}

// Abre un caso: imprime su encabezado y deja marcado desde donde contar fallos.
void Caso_704ILR(string encabezado_704ILR)
{
    CerrarCaso_704ILR();
    casos_704ILR++;
    casoAbierto_704ILR = true;
    casoOmitido_704ILR = false;
    fallosAlAbrir_704ILR = fallos_704ILR;
    Console.WriteLine(encabezado_704ILR);
}

// El caso abierto no pudo ejecutarse: se declara y se cuenta como omitido. No es
// un fallo del sistema, pero tampoco cobertura: la corrida termina con codigo 2.
void Omitir_704ILR(string caso_704ILR, string motivo_704ILR)
{
    casoOmitido_704ILR = true;
    Console.WriteLine($"  OMITIDO {caso_704ILR}: {motivo_704ILR}   <-- SIN COBERTURA");
}

// Varios casos consecutivos que dependen del mismo dato faltante.
void OmitirCasos_704ILR(string[] encabezados_704ILR, string motivo_704ILR)
{
    foreach (string e_704ILR in encabezados_704ILR)
    {
        Caso_704ILR(e_704ILR);
        Omitir_704ILR(e_704ILR.Substring(0, e_704ILR.IndexOf(']') + 1), motivo_704ILR);
    }
}

// Asientos de bitacora de un modulo (opcionalmente, de una accion). Las
// postcondiciones de los CUN prometen dejar traza de la operacion: se cuentan
// antes y despues. Se filtra por MODULO y no por el texto de la accion a
// proposito: ese texto es una leyenda para el usuario y cambia (el alta ya dice
// "Cotizacion generada" o "Reserva generada" segun el estado); atar la prueba a
// la leyenda la haria fallar por un cambio de redaccion, no por un defecto.
// Donde la postcondicion documentada nombra la accion (rechazos de cobro,
// asignacion de perfil, consulta de disponibilidad) si se filtra por ella.
List<EvenTech.BE.BE_BitacoraEntry_704ILR> Bitacora_704ILR(string modulo_704ILR, string accion_704ILR = null) =>
    EvenTech.BLL.BLL_Bitacora_704ILR.Buscar_704ILR(new EvenTech.BE.BitacoraFiltros_704ILR
    { Modulo_704ILR = modulo_704ILR, Accion_704ILR = accion_704ILR });

int Asientos_704ILR(string modulo_704ILR, string accion_704ILR = null) => Bitacora_704ILR(modulo_704ILR, accion_704ILR).Count;

// Sufijo unico de la corrida. Con HHmmss dos corridas de dias distintos a la
// misma hora chocaban (usuario duplicado, perfil duplicado, DNI duplicado);
// con la fecha adelante el choque exige repetir el segundo del mismo dia.
string suf_704ILR = DateTime.Now.ToString("yyMMddHHmmss");

// Desplazamiento propio de esta corrida. Las reservas de prueba se agendan a
// varios anios vista y con este desfasaje para no pisar fechas del negocio
// (RN-03), y todas las que quedan CONFIRMADAS se cancelan al cerrar su caso, de
// modo que no bloqueen el salon en la proxima corrida. Donde igual podria haber
// rastro (la ventana cercana de [26]) la fecha se elige con la propia consulta
// de disponibilidad, no a ciegas. Se prefirio esto antes que limpiar la base:
// una rutina que cancelara reservas CONFIRMADAS por salon+fecha podria dar de
// baja datos reales sin aviso y sin vuelta atras.
int desfasaje_704ILR = (int)DateTime.Now.TimeOfDay.TotalSeconds % 900;

// Rastro de la corrida. Cada caso limpia lo suyo al terminar, pero si aborta por
// excepcion la limpieza no corre y quedaba una CONFIRMADA ocupando el salon, un
// perfil o un cliente de prueba. Todo lo que se crea se anota aca y al final una
// pasada de limpieza cancela o borra lo que haya quedado vivo, con asercion.
var reservasDeLaCorrida_704ILR = new List<int>();
var clientesDeLaCorrida_704ILR = new List<int>();
var perfilesDeLaCorrida_704ILR = new List<int>();
int idiomaDeLaCorrida_704ILR = 0;

void Anotar_704ILR(int reservaId_704ILR)
{
    if (reservaId_704ILR > 0 && !reservasDeLaCorrida_704ILR.Contains(reservaId_704ILR))
        reservasDeLaCorrida_704ILR.Add(reservaId_704ILR);
}

// Valor escalar directo contra la base (conteos de residuo, ids de corte).
int Escalar_704ILR(string sql_704ILR, params (string nombre_704ILR, object valor_704ILR)[] parametros_704ILR)
{
    using var cn_704ILR = new EvenTech.DAL.DAL_DB_Connection_704ILR();
    using var cmd_704ILR = new Microsoft.Data.SqlClient.SqlCommand(sql_704ILR, cn_704ILR.OpenConnection_704ILR());
    foreach (var p_704ILR in parametros_704ILR) cmd_704ILR.Parameters.AddWithValue(p_704ILR.nombre_704ILR, p_704ILR.valor_704ILR ?? DBNull.Value);
    object r_704ILR = cmd_704ILR.ExecuteScalar();
    return r_704ILR == null || r_704ILR is DBNull ? 0 : Convert.ToInt32(r_704ILR);
}

void Ejecutar_704ILR(string sql_704ILR, params (string nombre_704ILR, object valor_704ILR)[] parametros_704ILR)
{
    using var cn_704ILR = new EvenTech.DAL.DAL_DB_Connection_704ILR();
    using var cmd_704ILR = new Microsoft.Data.SqlClient.SqlCommand(sql_704ILR, cn_704ILR.OpenConnection_704ILR());
    foreach (var p_704ILR in parametros_704ILR) cmd_704ILR.Parameters.AddWithValue(p_704ILR.nombre_704ILR, p_704ILR.valor_704ILR ?? DBNull.Value);
    cmd_704ILR.ExecuteNonQuery();
}

// Marcas de corte para informar al final cuanto residuo dejo la corrida en la
// bitacora y en la auditoria de acceso (esas filas no se borran: son evidencia).
// Es la primera lectura de la base: si no responde no hay nada que probar, y se
// informa con el codigo de fallo en lugar de abortar con una excepcion sin resumen.
int bitacoraInicio_704ILR, auditoriaInicio_704ILR;
try
{
    bitacoraInicio_704ILR = Escalar_704ILR("SELECT ISNULL(MAX(Id), 0) FROM dbo.Bitacora");
    auditoriaInicio_704ILR = Escalar_704ILR("SELECT ISNULL(MAX(Id), 0) FROM dbo.LoginAuditLog");
}
catch (Exception exInicio_704ILR)
{
    Console.WriteLine($"No se pudo leer la base configurada: {exInicio_704ILR.GetType().Name}: {exInicio_704ILR.Message}");
    Console.WriteLine("== Resultado: la corrida no pudo empezar (codigo 1). Revisar la conexion y el esquema: db/README.md ==");
    return 1;
}

// Primera fecha en la que el salon indicado admite una reserva firme, a partir
// de 'desde'. Se resuelve con la consulta de disponibilidad del propio sistema
// (la misma que usa el vendedor), de modo que el caso no de un falso rojo por
// chocar contra los datos de demostracion o contra el rastro de otra corrida.
// La consulta deja su asiento en bitacora (postcondicion del CUN001): no es un
// efecto de la prueba sino del sistema.
DateTime FechaLibre_704ILR(int salonId_704ILR, DateTime desde_704ILR)
{
    var d_704ILR = BLL_Disponibilidad_704ILR.Consultar_704ILR(desde_704ILR, 0)
        .FirstOrDefault(x_704ILR => x_704ILR.SalonId_704ILR == salonId_704ILR);
    if (d_704ILR == null || d_704ILR.Libre_704ILR) return desde_704ILR.Date;
    return d_704ILR.ProximaFechaLibre_704ILR ?? desde_704ILR.Date;
}

// Limpieza de los perfiles que crea [19]. La aplicacion no ofrece dar de baja un
// perfil (no es una operacion del alcance), asi que el rastro de la prueba se
// borra directamente contra la base, igual que [25] crea y elimina la suya.
void BorrarPerfilDePrueba_704ILR(int perfilId_704ILR)
{
    if (perfilId_704ILR <= 0) return;
    Ejecutar_704ILR(
        "UPDATE dbo.Users SET PerfilId = NULL WHERE PerfilId = @id; " +
        "DELETE FROM dbo.PerfilIncluido WHERE PerfilPadreId = @id OR PerfilHijoId = @id; " +
        "DELETE FROM dbo.PerfilPermiso WHERE PerfilId = @id; " +
        "DELETE FROM dbo.Perfiles WHERE Id = @id;", ("@id", perfilId_704ILR));
    perfilesDeLaCorrida_704ILR.Remove(perfilId_704ILR);
}

// Limpieza de un cliente de prueba: solo si ninguna reserva lo referencia.
void BorrarClienteDePrueba_704ILR(int clienteId_704ILR)
{
    if (clienteId_704ILR <= 0) return;
    Ejecutar_704ILR(
        "DELETE FROM dbo.Clientes WHERE Id = @id AND NOT EXISTS (SELECT 1 FROM dbo.Reservas WHERE ClienteId = @id)",
        ("@id", clienteId_704ILR));
    clientesDeLaCorrida_704ILR.Remove(clienteId_704ILR);
}

// Limpieza del idioma que crea [17]: la aplicacion no da de baja idiomas.
void BorrarIdiomaDePrueba_704ILR(int idiomaId_704ILR)
{
    if (idiomaId_704ILR <= 0) return;
    Ejecutar_704ILR("DELETE FROM dbo.Traducciones WHERE IdiomaId = @id; DELETE FROM dbo.Idiomas WHERE Id = @id;",
        ("@id", idiomaId_704ILR));
    EvenTech.BLL.BLL_Idioma_704ILR.Inicializar_704ILR();
    if (idiomaDeLaCorrida_704ILR == idiomaId_704ILR) idiomaDeLaCorrida_704ILR = 0;
}

// RN-07: una reserva se confirma con el adelanto ya cobrado. Este helper registra
// ese cobro para poder ejercitar las transiciones a CONFIRMADA, igual que hace el
// vendedor en la aplicacion: guardar la operacion, cobrar y recien ahi confirmar.
// Devuelve el id del pago (0 si no se registro).
int Adelanto_704ILR(int reservaId_704ILR, decimal monto_704ILR)
{
    var met_704ILR = BLL_Pago_704ILR.GetMetodos_704ILR();
    if (met_704ILR.Count == 0)
    {
        fallos_704ILR++;
        Console.WriteLine($"  adelanto en la reserva #{reservaId_704ILR}: sin metodos de pago sembrados   <-- DIFIERE");
        return 0;
    }
    var rAd_704ILR = BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
    {
        ReservaId_704ILR = reservaId_704ILR,
        MetodoPagoId_704ILR = met_704ILR[0].Id_704ILR,
        Monto_704ILR = monto_704ILR,
        Observacion_704ILR = "Adelanto"
    }, out int idPago_704ILR);

    // Si el adelanto no entra, la confirmacion que viene despues falla por RN-07
    // y el caso reportaria un motivo equivocado: se anota aca.
    if (rAd_704ILR != PagoResult_704ILR.Success_704ILR)
    {
        fallos_704ILR++;
        Console.WriteLine($"  adelanto de {monto_704ILR:0.00} en la reserva #{reservaId_704ILR}: " +
                          $"{rAd_704ILR} (esperado Success_704ILR)   <-- DIFIERE");
    }
    return idPago_704ILR;
}

// Reserva de prueba minima (cotizacion o pendiente, sin servicios) para los
// casos que solo necesitan una operacion viva sobre la que aplicar una regla.
EvenTech.BE.BE_Reserva_704ILR NuevaReserva_704ILR(int clienteId_704ILR, int salonId_704ILR, int diasVista_704ILR,
    EvenTech.BE.EstadoReserva_704ILR estado_704ILR, decimal monto_704ILR, int invitados_704ILR = 50) =>
    new EvenTech.BE.BE_Reserva_704ILR
    {
        ClienteId_704ILR = clienteId_704ILR,
        SalonId_704ILR = salonId_704ILR,
        FechaEvento_704ILR = DateTime.Today.AddDays(diasVista_704ILR + desfasaje_704ILR),
        Estado_704ILR = estado_704ILR,
        CantidadInvitados_704ILR = invitados_704ILR,
        Monto_704ILR = monto_704ILR
    };

// [1] Login OK
Caso_704ILR("[1] Login admin/admin123:");
try
{
    var r1_704ILR = BLL_Login_704ILR.Authenticate_704ILR("admin", Encrypt_704ILR.HashValue_704ILR("admin123"));
    Esperar_704ILR("result", r1_704ILR.Result_704ILR, LoginResult_704ILR.Success_704ILR);
    Esperar_704ILR("sesion activa", SessionManager_704ILR.IsSessionActive_704ILR, true);
    BLL_Login_704ILR.Logout_704ILR();
}
catch (Exception ex1_704ILR) { Excepcion_704ILR("[1]", ex1_704ILR); }

// [2] Crear usuario nuevo (con timestamp para que sea unico entre corridas)
string newUser_704ILR = "smoke_" + suf_704ILR;
Caso_704ILR($"[2] Crear usuario '{newUser_704ILR}' password 'pass1234':");
try
{
    var rc1_704ILR = BLL_User_704ILR.CreateUser_704ILR(newUser_704ILR, Encrypt_704ILR.HashValue_704ILR("pass1234"));
    Esperar_704ILR("result", rc1_704ILR, CreateUserResult_704ILR.Success_704ILR);
}
catch (Exception ex2_704ILR) { Excepcion_704ILR("[2]", ex2_704ILR); }

// [3] Crear duplicado
Caso_704ILR($"[3] Crear '{newUser_704ILR}' duplicado:");
try
{
    var rc2_704ILR = BLL_User_704ILR.CreateUser_704ILR(newUser_704ILR, Encrypt_704ILR.HashValue_704ILR("otra"));
    Esperar_704ILR("result", rc2_704ILR, CreateUserResult_704ILR.UsernameAlreadyExists_704ILR);
}
catch (Exception ex3_704ILR) { Excepcion_704ILR("[3]", ex3_704ILR); }

// [4] Username invalido
Caso_704ILR("[4] Crear con username '..' (invalido):");
try
{
    var rc3_704ILR = BLL_User_704ILR.CreateUser_704ILR("..", Encrypt_704ILR.HashValue_704ILR("xxxx"));
    Esperar_704ILR("result", rc3_704ILR, CreateUserResult_704ILR.InvalidUsername_704ILR);
}
catch (Exception ex4_704ILR) { Excepcion_704ILR("[4]", ex4_704ILR); }

// [5] Login con el usuario recien creado. Nace SIN perfil asignado: la sesion
// tiene que quedar marcada como tal y sin un solo permiso (denegar por defecto),
// que es la bandera con la que la ventana principal bloquea al usuario. Las tres
// verificaciones de la sesion corren siempre: si el ingreso falla, fallan con el.
Caso_704ILR($"[5] Login con '{newUser_704ILR}':");
try
{
    var r5_704ILR = BLL_Login_704ILR.Authenticate_704ILR(newUser_704ILR, Encrypt_704ILR.HashValue_704ILR("pass1234"));
    Esperar_704ILR("result", r5_704ILR.Result_704ILR, LoginResult_704ILR.Success_704ILR);
    bool sesion5_704ILR = SessionManager_704ILR.IsSessionActive_704ILR;
    Esperar_704ILR("sesion sin perfil asignado", sesion5_704ILR && SessionManager_704ILR.GetInstance_704ILR.SinPerfil_704ILR, true);
    Esperar_704ILR("permisos de la sesion", sesion5_704ILR ? SessionManager_704ILR.GetInstance_704ILR.Permisos_704ILR.Count : -1, 0);
    Esperar_704ILR("RESERVA_CREAR sin perfil", sesion5_704ILR && SessionManager_704ILR.GetInstance_704ILR.TienePermiso_704ILR("RESERVA_CREAR"), false);
    BLL_Login_704ILR.Logout_704ILR();
}
catch (Exception ex5_704ILR) { Excepcion_704ILR("[5]", ex5_704ILR); }

// [6] Auditoria de acceso: el ingreso y el cierre de sesion de [5] tienen que ser
// los dos ultimos movimientos registrados para ese usuario (antes el caso solo
// imprimia las ultimas cinco filas, sin verificar nada).
Caso_704ILR("[6] Ultimas 5 entradas de auditoria:");
try
{
    var ultimas_704ILR = BLL_LoginAudit_704ILR.GetAll_704ILR(5);
    foreach (var e_704ILR in ultimas_704ILR)
    {
        Console.WriteLine($"  #{e_704ILR.Id_704ILR} {e_704ILR.Timestamp_704ILR:HH:mm:ss} {e_704ILR.Username_704ILR,-20} {e_704ILR.Action_704ILR,-12} {e_704ILR.Details_704ILR}");
    }
    Esperar_704ILR("ultimo movimiento: cierre de sesion del usuario de prueba",
        ultimas_704ILR.Count > 0 && ultimas_704ILR[0].Username_704ILR == newUser_704ILR &&
        ultimas_704ILR[0].Action_704ILR == EvenTech.BE.LoginAuditAction_704ILR.LOGOUT, true);
    Esperar_704ILR("ingreso correcto del usuario de prueba registrado",
        ultimas_704ILR.Any(e_704ILR => e_704ILR.Username_704ILR == newUser_704ILR &&
            e_704ILR.Action_704ILR == EvenTech.BE.LoginAuditAction_704ILR.LOGIN_OK), true);
    Esperar_704ILR("movimientos del usuario de prueba entre los ultimos 5",
        ultimas_704ILR.Count(e_704ILR => e_704ILR.Username_704ILR == newUser_704ILR), 2);
}
catch (Exception ex6_704ILR) { Excepcion_704ILR("[6]", ex6_704ILR); }

// [7] Reservas: alta valida (la reserva referencia al cliente por Id). Si los
// catalogos no se pueden leer, las listas quedan vacias y los casos se omiten.
Caso_704ILR("[7] Crear reserva valida:");
var salones_704ILR = new List<EvenTech.BE.BE_Salon_704ILR>();
var clientes_704ILR = new List<EvenTech.BE.BE_Cliente_704ILR>();
try
{
    salones_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    clientes_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
}
catch (Exception ex7a_704ILR) { Excepcion_704ILR("[7] (lectura de catalogos)", ex7a_704ILR); }
if (salones_704ILR.Count == 0 || clientes_704ILR.Count == 0)
{
    Omitir_704ILR("[7]", "no hay salones/clientes seed; corre db/schema.sql");
    OmitirCasos_704ILR(new[] { "[8] Crear reserva con fecha pasada (invalida):", "[9] Total de reservas:",
        "[10] Modificar reserva (estado + monto):", "[11] Historial de cambios:", "[12] Ultimas 5 entradas de bitacora:" },
        "no hay salones/clientes seed; corre db/schema.sql");
}
else try
{
    // Fecha propia de la corrida (como el username del caso [2]) y ademas libre de
    // verdad para ese salon: el caso [10] confirma esta reserva y una fecha ya
    // comprometida daria SalonOcupado por un motivo ajeno a lo que se prueba.
    // Ventana 1000-1900 para no pisar la del [26].
    var nueva_704ILR = new EvenTech.BE.BE_Reserva_704ILR
    {
        ClienteId_704ILR = clientes_704ILR[0].Id_704ILR,
        SalonId_704ILR = salones_704ILR[0].Id_704ILR,
        FechaEvento_704ILR = FechaLibre_704ILR(salones_704ILR[0].Id_704ILR, DateTime.Today.AddDays(1000 + desfasaje_704ILR)),
        Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE,
        CantidadInvitados_704ILR = 60,   // RN-06: sin este dato no se puede confirmar
        Monto_704ILR = 150000m
    };

    // Postcondicion del CUN005: el alta deja su propio asiento en la bitacora.
    int altasAntes_704ILR = Asientos_704ILR("Reservas");
    int reservasAntes_704ILR = BLL_Reserva_704ILR.GetAll_704ILR().Count;
    var rr1_704ILR = BLL_Reserva_704ILR.Crear_704ILR(nueva_704ILR, out int nuevoId_704ILR);
    Anotar_704ILR(nuevoId_704ILR);
    Esperar_704ILR("result", rr1_704ILR, ReservaResult_704ILR.Success_704ILR);
    Esperar_704ILR("id asignado", nuevoId_704ILR > 0, true);
    Esperar_704ILR("asientos del modulo Reservas tras el alta", Asientos_704ILR("Reservas"), altasAntes_704ILR + 1);
    var asientoAlta_704ILR = Bitacora_704ILR("Reservas")[0];
    Esperar_704ILR("el asiento nombra la reserva creada",
        asientoAlta_704ILR.Detalle_704ILR.Contains($"#{nuevoId_704ILR}"), true);

    // [8] Reserva con fecha pasada (debe fallar)
    Caso_704ILR("[8] Crear reserva con fecha pasada (invalida):");
    var pasada_704ILR = new EvenTech.BE.BE_Reserva_704ILR
    {
        ClienteId_704ILR = clientes_704ILR[0].Id_704ILR,
        SalonId_704ILR = salones_704ILR[0].Id_704ILR,
        FechaEvento_704ILR = DateTime.Today.AddDays(-1),
        Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE,
        CantidadInvitados_704ILR = 60,   // RN-06: sin este dato no se puede confirmar
        Monto_704ILR = 1000m
    };
    var rr2_704ILR = BLL_Reserva_704ILR.Crear_704ILR(pasada_704ILR, out int idPasada_704ILR);
    Esperar_704ILR("result", rr2_704ILR, ReservaResult_704ILR.InvalidFecha_704ILR);
    Esperar_704ILR("no se asigno id", idPasada_704ILR, 0);

    // [9] Listado: el alta valida de [7] sumo exactamente una reserva y la
    // rechazada de [8] no sumo ninguna.
    Caso_704ILR("[9] Total de reservas:");
    int reservasAhora_704ILR = BLL_Reserva_704ILR.GetAll_704ILR().Count;
    Console.WriteLine($"  {reservasAhora_704ILR} reservas");
    Esperar_704ILR("reservas tras [7] y [8]", reservasAhora_704ILR, reservasAntes_704ILR + 1);

    // [10] Control de cambios: modificar la reserva recien creada
    if (rr1_704ILR == ReservaResult_704ILR.Success_704ILR)
    {
        Caso_704ILR($"[10] Modificar reserva #{nuevoId_704ILR} (estado + monto):");
        Adelanto_704ILR(nuevoId_704ILR, 1000m);   // RN-07: sin adelanto no se confirma
        var editada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(nuevoId_704ILR);
        editada_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        editada_704ILR.Monto_704ILR = 175000m;
        var ru_704ILR = BLL_Reserva_704ILR.Actualizar_704ILR(editada_704ILR);
        Esperar_704ILR("result", ru_704ILR, ReservaResult_704ILR.Success_704ILR);

        Caso_704ILR($"[11] Historial de cambios de la reserva #{nuevoId_704ILR}:");
        var hist_704ILR = EvenTech.BLL.RegistradorDeCambios_704ILR.GetHistorial_704ILR("Reserva", nuevoId_704ILR);
        foreach (var c_704ILR in hist_704ILR)
            Console.WriteLine($"  {c_704ILR.Fecha_704ILR:HH:mm:ss} {c_704ILR.NombreCampo_704ILR,-14} '{c_704ILR.ValorAnterior_704ILR}' -> '{c_704ILR.ValorNuevo_704ILR}'");
        // La edicion toco dos campos de negocio auditados: Estado y Monto.
        Esperar_704ILR("campos registrados por el control de cambios", hist_704ILR.Count, 2);

        // [12] Bitacora general: el ultimo asiento de la base tiene que ser el de la
        // modificacion de [10], sobre esta reserva (antes solo se imprimian filas).
        Caso_704ILR("[12] Ultimas 5 entradas de bitacora:");
        var ultimosAsientos_704ILR = EvenTech.BLL.BLL_Bitacora_704ILR.Buscar_704ILR(new EvenTech.BE.BitacoraFiltros_704ILR());
        int mostradas_704ILR = 0;
        foreach (var b_704ILR in ultimosAsientos_704ILR)
        {
            Console.WriteLine($"  #{b_704ILR.Id_704ILR} {b_704ILR.Fecha_704ILR:HH:mm:ss} {b_704ILR.Modulo_704ILR,-10} {b_704ILR.Accion_704ILR,-26} {b_704ILR.Criticidad_704ILR}");
            if (++mostradas_704ILR >= 5) break;
        }
        Esperar_704ILR("el ultimo asiento es del modulo Reservas",
            ultimosAsientos_704ILR.Count > 0 ? ultimosAsientos_704ILR[0].Modulo_704ILR : null, "Reservas");
        Esperar_704ILR("el ultimo asiento nombra la reserva modificada",
            ultimosAsientos_704ILR.Count > 0 && ultimosAsientos_704ILR[0].Detalle_704ILR.Contains($"#{nuevoId_704ILR}"), true);
        Esperar_704ILR("criticidad del asiento de modificacion",
            ultimosAsientos_704ILR.Count > 0 ? ultimosAsientos_704ILR[0].Criticidad_704ILR : default,
            EvenTech.BE.CriticidadBitacora_704ILR.Info);

        // Limpieza: [10] dejo la reserva CONFIRMADA y asi bloquearia ese salon y esa
        // fecha en la proxima corrida. Se da de baja por la via de cancelacion, que
        // es la unica admitida para entrar a CANCELADA (RN-05) y la que liquida la RN-02.
        Esperar_704ILR("limpieza (cancelar la reserva de [7]/[10])",
            BLL_Reserva_704ILR.Cancelar_704ILR(nuevoId_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
    else
    {
        OmitirCasos_704ILR(new[] { "[10] Modificar reserva (estado + monto):", "[11] Historial de cambios:",
            "[12] Ultimas 5 entradas de bitacora:" }, "la reserva de [7] no se creo");
    }
}
catch (Exception ex7_704ILR) { Excepcion_704ILR("[7]-[12]", ex7_704ILR); }

// [13] Composite de perfiles: recorrer arbol y permisos efectivos. El arbol tiene
// que tener grupos con hojas adentro, y toda hoja lleva su clave (sin clave no
// habilita nada).
Caso_704ILR("[13] Arbol de permisos (Composite):");
try
{
    var arbol_704ILR = BLL_Perfil_704ILR.GetArbolPermisos_704ILR();
    int grupos_704ILR = 0, hojas_704ILR = 0, hojasSinClave_704ILR = 0;
    void Imprimir_704ILR(EvenTech.BE.BE_IComponentePermiso_704ILR n_704ILR, int nivel_704ILR)
    {
        Console.WriteLine($"  {new string(' ', nivel_704ILR * 2)}{(n_704ILR.EsGrupo_704ILR ? "[G]" : "[P]")} {n_704ILR.Nombre_704ILR}");
        if (n_704ILR is EvenTech.BE.BE_GrupoPermisos_704ILR g_704ILR)
        {
            grupos_704ILR++;
            foreach (var h_704ILR in g_704ILR.Hijos_704ILR) Imprimir_704ILR(h_704ILR, nivel_704ILR + 1);
        }
        else if (n_704ILR is EvenTech.BE.BE_Permiso_704ILR p_704ILR)
        {
            hojas_704ILR++;
            if (string.IsNullOrWhiteSpace(p_704ILR.Clave_704ILR)) hojasSinClave_704ILR++;
        }
    }
    foreach (var raiz_704ILR in arbol_704ILR) Imprimir_704ILR(raiz_704ILR, 0);
    Esperar_704ILR("raices del arbol", arbol_704ILR.Count > 0, true);
    Esperar_704ILR("grupos en el arbol", grupos_704ILR > 0, true);
    Esperar_704ILR("hojas en el arbol", hojas_704ILR > 0, true);
    Esperar_704ILR("hojas sin clave", hojasSinClave_704ILR, 0);
    // Una raiz sin permisos efectivos pasaria el All() sin probar nada: se exige
    // que tenga hojas y que todo lo que resuelve lo sea.
    var efectivosRaiz_704ILR = arbol_704ILR.Count > 0 ? arbol_704ILR[0].ObtenerPermisosEfectivos_704ILR().ToList() : null;
    Esperar_704ILR("permisos efectivos de una raiz = sus hojas",
        efectivosRaiz_704ILR != null && efectivosRaiz_704ILR.Count > 0 && efectivosRaiz_704ILR.All(p_704ILR => p_704ILR.EsHoja_704ILR()), true);
}
catch (Exception ex13_704ILR) { Excepcion_704ILR("[13]", ex13_704ILR); }

Caso_704ILR("[14] Permisos efectivos de un perfil:");
try
{
    var perfiles_704ILR = BLL_Perfil_704ILR.GetPerfiles_704ILR();
    if (perfiles_704ILR.Count > 0)
    {
        // Se resuelve con el MISMO algoritmo que usa el login (Composite sobre
        // BE_Perfil): los permisos efectivos son las hojas que cubren los componentes
        // asignados, incluidas las que llegan por los perfiles incluidos.
        var asignados_704ILR = BLL_Perfil_704ILR.GetPermisosAsignados_704ILR(perfiles_704ILR[0].Id_704ILR);
        var efectivos_704ILR = BLL_Perfil_704ILR.GetPermisosEfectivosDePerfil_704ILR(perfiles_704ILR[0].Id_704ILR);
        Console.WriteLine($"  perfil '{perfiles_704ILR[0].Nombre_704ILR}': {asignados_704ILR.Count} componente(s) asignado(s) " +
                          $"-> {efectivos_704ILR.Count} permisos efectivos (hojas).");
        Esperar_704ILR("el perfil resuelve al menos un permiso", efectivos_704ILR.Count > 0, true);
    }
    else
    {
        Omitir_704ILR("[14]", "no hay perfiles sembrados; corre db/schema.sql");
    }
}
catch (Exception ex14_704ILR) { Excepcion_704ILR("[14]", ex14_704ILR); }

// [15] Idiomas (Observer): el gestor notifica a sus observadores cuando el idioma
// cambia en caliente. Se suscribe un observador de prueba —el mismo rol que cumple
// cada formulario de la aplicacion— y se cuenta cuantas veces lo llamo.
Caso_704ILR("[15] Idiomas (Observer):");
try
{
    EvenTech.BLL.BLL_Idioma_704ILR.Inicializar_704ILR();
    var gi_704ILR = EvenTech.Services.GestorDeIdioma_704ILR.GetInstance_704ILR;
    var obs_704ILR = new ObservadorPrueba_704ILR();
    gi_704ILR.Suscribir_704ILR(obs_704ILR);
    Esperar_704ILR("idioma inicial", gi_704ILR.IdiomaActual_704ILR, "ES");
    Esperar_704ILR("MENU_RESERVAS en ES", gi_704ILR.Traducir_704ILR("MENU_RESERVAS"), "Reservas");

    gi_704ILR.CambiarIdioma_704ILR("EN");
    Esperar_704ILR("idioma tras el cambio", gi_704ILR.IdiomaActual_704ILR, "EN");
    Esperar_704ILR("notificaciones al observador", obs_704ILR.Llamadas_704ILR, 1);
    Esperar_704ILR("MENU_RESERVAS en EN", gi_704ILR.Traducir_704ILR("MENU_RESERVAS"), "Reservations");

    gi_704ILR.CambiarIdioma_704ILR("ES");
    Esperar_704ILR("notificaciones tras volver a ES", obs_704ILR.Llamadas_704ILR, 2);

    // Desuscribir corta la notificacion: un formulario cerrado no debe seguir avisado.
    gi_704ILR.Desuscribir_704ILR(obs_704ILR);
    gi_704ILR.CambiarIdioma_704ILR("EN");
    Esperar_704ILR("notificaciones tras desuscribir", obs_704ILR.Llamadas_704ILR, 2);
    gi_704ILR.CambiarIdioma_704ILR("ES");
}
catch (Exception ex15_704ILR) { Excepcion_704ILR("[15]", ex15_704ILR); }

// [16] Digitos verificadores (T07/T08). La prueba DIAGNOSTICA: si la linea base
// esta inconsistente lo informa, lista las inconsistencias y la corrida falla.
// No la repara: antes llamaba al recalculo, que reescribe todos los DV de la base
// configurada y borra la evidencia de una alteracion externa, que es justo lo que
// los digitos verificadores existen para detectar. El recalculo es una accion
// administrativa (Auditoria > Recalcular linea base, permiso INTEGRIDAD_RECALC)
// que se ejecuta despues de revisar la causa, no desde una prueba.
Caso_704ILR("[16] Integridad (digitos verificadores):");
try
{
    var resInt_704ILR = EvenTech.BLL.BLL_Integridad_704ILR.Verificar_704ILR();
    Esperar_704ILR("Ok", resInt_704ILR.Ok_704ILR, true);
    Esperar_704ILR("inconsistencias", resInt_704ILR.Inconsistencias_704ILR.Count, 0);
    foreach (var i_704ILR in resInt_704ILR.Inconsistencias_704ILR) Console.WriteLine("   - " + i_704ILR);
    if (!resInt_704ILR.Ok_704ILR)
        Console.WriteLine("  ATENCION: la linea base esta inconsistente. La prueba no la repara: revisar la causa y " +
                          "recalcular desde Auditoria (accion administrativa).");
}
catch (Exception ex16_704ILR) { Excepcion_704ILR("[16]", ex16_704ILR); }

// [17] Alta de idioma desde la capa de negocio (admin agrega idioma). 'PT' ya
// viene sembrado por db/schema.sql y ejercita el rechazo del codigo duplicado;
// las dos validaciones de datos no dejan residuo. El alta VALIDA se prueba con un
// codigo propio de la corrida y se borra al terminar (la aplicacion no da de
// baja idiomas): tiene que nacer con las leyendas del idioma por defecto
// copiadas, quedar publicada en el gestor y asentada en bitacora.
Caso_704ILR("[17] Alta de idioma:");
try
{
    var rIdioma_704ILR = EvenTech.BLL.BLL_Idioma_704ILR.CrearIdioma_704ILR("PT", "Portugues", out _);
    Esperar_704ILR("codigo duplicado", rIdioma_704ILR, IdiomaResult_704ILR.CodigoDuplicado_704ILR);
    Esperar_704ILR("codigo vacio", EvenTech.BLL.BLL_Idioma_704ILR.CrearIdioma_704ILR("", "Sin codigo", out _),
        IdiomaResult_704ILR.CodigoInvalido_704ILR);
    Esperar_704ILR("nombre vacio", EvenTech.BLL.BLL_Idioma_704ILR.CrearIdioma_704ILR("XX", "", out _),
        IdiomaResult_704ILR.NombreInvalido_704ILR);

    var codigos_704ILR = new SortedSet<string>(
        EvenTech.Services.GestorDeIdioma_704ILR.GetInstance_704ILR.IdiomasDisponibles_704ILR
            .Select(i_704ILR => i_704ILR.Codigo_704ILR), StringComparer.OrdinalIgnoreCase);
    Console.WriteLine($"  idiomas disponibles: {codigos_704ILR.Count} ({string.Join(", ", codigos_704ILR)})");
    Esperar_704ILR("los tres idiomas del sistema presentes",
        codigos_704ILR.IsSupersetOf(new[] { "ES", "EN", "PT" }), true);

    // Alta valida: codigo de 5 caracteres propio de la corrida (mmss del sufijo).
    string codigoNuevo_704ILR = "Z" + suf_704ILR.Substring(8);
    var idiomasPrevios_704ILR = EvenTech.BLL.BLL_Idioma_704ILR.GetIdiomas_704ILR();
    var es_704ILR = idiomasPrevios_704ILR.FirstOrDefault(i_704ILR => i_704ILR.Codigo_704ILR == "ES");
    int leyendasES_704ILR = es_704ILR == null ? 0 : EvenTech.BLL.BLL_Idioma_704ILR.GetTraducciones_704ILR(es_704ILR.Id_704ILR).Count;
    int altasIdioma_704ILR = Asientos_704ILR("Idiomas");
    var rNuevo_704ILR = EvenTech.BLL.BLL_Idioma_704ILR.CrearIdioma_704ILR(codigoNuevo_704ILR, "Idioma smoke " + suf_704ILR, out int idIdioma_704ILR);
    idiomaDeLaCorrida_704ILR = idIdioma_704ILR;
    Esperar_704ILR($"alta de '{codigoNuevo_704ILR}'", rNuevo_704ILR, IdiomaResult_704ILR.Success_704ILR);
    Esperar_704ILR("id asignado", idIdioma_704ILR > 0, true);
    Esperar_704ILR("publicado en el gestor (Observer)",
        EvenTech.Services.GestorDeIdioma_704ILR.GetInstance_704ILR.IdiomasDisponibles_704ILR
            .Any(i_704ILR => i_704ILR.Codigo_704ILR == codigoNuevo_704ILR), true);
    Esperar_704ILR("leyendas copiadas del idioma por defecto",
        idIdioma_704ILR > 0 ? EvenTech.BLL.BLL_Idioma_704ILR.GetTraducciones_704ILR(idIdioma_704ILR).Count : -1, leyendasES_704ILR);
    Esperar_704ILR("asientos del modulo Idiomas tras el alta", Asientos_704ILR("Idiomas"), altasIdioma_704ILR + 1);
    Esperar_704ILR("el idioma actual no cambio", EvenTech.Services.GestorDeIdioma_704ILR.GetInstance_704ILR.IdiomaActual_704ILR, "ES");

    // Limpieza: el idioma de prueba se borra y el gestor se recarga sin el.
    BorrarIdiomaDePrueba_704ILR(idIdioma_704ILR);
    Esperar_704ILR("limpieza (idioma de prueba eliminado)",
        EvenTech.BLL.BLL_Idioma_704ILR.GetIdiomas_704ILR().Any(i_704ILR => i_704ILR.Codigo_704ILR == codigoNuevo_704ILR), false);
    Esperar_704ILR("limpieza (gestor sin el idioma de prueba)",
        EvenTech.Services.GestorDeIdioma_704ILR.GetInstance_704ILR.IdiomasDisponibles_704ILR
            .Any(i_704ILR => i_704ILR.Codigo_704ILR == codigoNuevo_704ILR), false);
}
catch (Exception ex17_704ILR) { Excepcion_704ILR("[17]", ex17_704ILR); }

// [18] Patron Memento: versionado y restauracion de reservas
Caso_704ILR("[18] Memento (versiones de reserva):");
// Los catalogos son los que leyo [7] (vacios si esa lectura fallo: el caso se omite).
var clientesM_704ILR = clientes_704ILR;
var salonesM_704ILR = salones_704ILR;
if (clientesM_704ILR.Count == 0 || salonesM_704ILR.Count == 0)
{
    Omitir_704ILR("[18]", "faltan clientes/salones seed; corre db/schema.sql");
}
else try
{
    var reservaM_704ILR = new EvenTech.BE.BE_Reserva_704ILR
    {
        ClienteId_704ILR = clientesM_704ILR[0].Id_704ILR,
        SalonId_704ILR = salonesM_704ILR[0].Id_704ILR,
        FechaEvento_704ILR = FechaLibre_704ILR(salonesM_704ILR[0].Id_704ILR, DateTime.Today.AddDays(1500 + desfasaje_704ILR)),
        Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE,
        CantidadInvitados_704ILR = 60,   // RN-06: sin este dato no se puede confirmar
        Monto_704ILR = 1000m
    };
    var rm_704ILR = BLL_Reserva_704ILR.Crear_704ILR(reservaM_704ILR, out int idM_704ILR);
    Anotar_704ILR(idM_704ILR);
    Esperar_704ILR("alta", rm_704ILR, ReservaResult_704ILR.Success_704ILR);

    // Sin la reserva de prueba no hay nada que versionar: se corta el caso en
    // lugar de seguir sobre un null y llevarse puestos los casos siguientes.
    if (rm_704ILR == ReservaResult_704ILR.Success_704ILR)
    {
        Adelanto_704ILR(idM_704ILR, 500m);   // RN-07
        var v1_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idM_704ILR);
        v1_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        v1_704ILR.Monto_704ILR = 2000m;
        Esperar_704ILR("modificar (PENDIENTE/1000 -> CONFIRMADA/2000)",
            BLL_Reserva_704ILR.Actualizar_704ILR(v1_704ILR), ReservaResult_704ILR.Success_704ILR);

        var versiones_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idM_704ILR);
        Esperar_704ILR("versiones guardadas", versiones_704ILR.Count, 1);

        if (versiones_704ILR.Count > 0)
        {
            var rr_704ILR = BLL_Reserva_704ILR.RestaurarVersion_704ILR(idM_704ILR, versiones_704ILR[0].Id_704ILR);
            var restaurada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idM_704ILR);
            Esperar_704ILR("restaurar", rr_704ILR, ReservaResult_704ILR.Success_704ILR);
            Esperar_704ILR("estado repuesto", restaurada_704ILR.Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.PENDIENTE);
            Esperar_704ILR("monto repuesto", restaurada_704ILR.Monto_704ILR, 1000m);
            // RN-01: al volver a PENDIENTE la operacion recupera su plazo.
            Esperar_704ILR("plazo repuesto al volver a PENDIENTE", restaurada_704ILR.VenceEl_704ILR.HasValue, true);
            // La restauracion versiona el estado que piso: quedan dos versiones.
            Esperar_704ILR("versiones tras restaurar", CaretakerReserva_704ILR.GetVersiones_704ILR(idM_704ILR).Count, 2);
        }

        // Limpieza: la reserva de prueba se da de baja para no dejar residuo.
        Esperar_704ILR("limpieza (cancelar la reserva de [18])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idM_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex18_704ILR) { Excepcion_704ILR("[18]", ex18_704ILR); }

// [19] Composite de perfiles: un perfil incluye a otro y hereda sus permisos
Caso_704ILR("[19] Composite de perfiles (perfil incluye perfil):");
try
{
    var arbolC_704ILR = BLL_Perfil_704ILR.GetArbolPermisos_704ILR();

    // Busca el id de una hoja por su clave, recorriendo el arbol Composite.
    int BuscarClave_704ILR(IEnumerable<EvenTech.BE.BE_IComponentePermiso_704ILR> nodos_704ILR, string clave_704ILR)
    {
        foreach (var n_704ILR in nodos_704ILR)
        {
            if (n_704ILR is EvenTech.BE.BE_Permiso_704ILR p_704ILR && p_704ILR.Clave_704ILR == clave_704ILR) return p_704ILR.Id_704ILR;
            if (n_704ILR is EvenTech.BE.BE_GrupoPermisos_704ILR g_704ILR)
            {
                int r_704ILR = BuscarClave_704ILR(g_704ILR.Hijos_704ILR, clave_704ILR);
                if (r_704ILR > 0) return r_704ILR;
            }
        }
        return 0;
    }

    int idCrear_704ILR = BuscarClave_704ILR(arbolC_704ILR, "RESERVA_CREAR");
    int idEditar_704ILR = BuscarClave_704ILR(arbolC_704ILR, "RESERVA_EDITAR");
    int idBitacora_704ILR = BuscarClave_704ILR(arbolC_704ILR, "BITACORA_VER");

    Esperar_704ILR("alta del perfil Vendedor",
        BLL_Perfil_704ILR.CrearPerfil_704ILR("Vendedor_" + suf_704ILR, "smoke", out int idVend_704ILR),
        PerfilResult_704ILR.Success_704ILR);
    if (idVend_704ILR > 0) perfilesDeLaCorrida_704ILR.Add(idVend_704ILR);
    Esperar_704ILR("alta del perfil Gerencial",
        BLL_Perfil_704ILR.CrearPerfil_704ILR("Gerencial_" + suf_704ILR, "smoke", out int idGer_704ILR),
        PerfilResult_704ILR.Success_704ILR);
    if (idGer_704ILR > 0) perfilesDeLaCorrida_704ILR.Add(idGer_704ILR);

    var rVend_704ILR = BLL_Perfil_704ILR.GuardarComposicion_704ILR(idVend_704ILR, new[] { idCrear_704ILR, idEditar_704ILR }, new int[0]);
    Esperar_704ILR("Vendedor (RESERVA_CREAR + RESERVA_EDITAR)", rVend_704ILR, PerfilResult_704ILR.Success_704ILR);

    var rGer_704ILR = BLL_Perfil_704ILR.GuardarComposicion_704ILR(idGer_704ILR, new[] { idBitacora_704ILR }, new[] { idVend_704ILR });
    Esperar_704ILR("Gerencial (BITACORA_VER + incluye Vendedor)", rGer_704ILR, PerfilResult_704ILR.Success_704ILR);

    // Las claves efectivas se comparan de verdad (antes solo se imprimian junto a
    // un "(esperado ...)" escrito a mano, que nadie contrastaba).
    var clavesGer_704ILR = new SortedSet<string>(
        BLL_Perfil_704ILR.GetPermisosEfectivosDePerfil_704ILR(idGer_704ILR).Select(p_704ILR => p_704ILR.Clave_704ILR),
        StringComparer.OrdinalIgnoreCase);
    Esperar_704ILR("permisos efectivos de Gerencial (BITACORA_VER propio + los dos heredados de Vendedor)",
        string.Join(", ", clavesGer_704ILR), "BITACORA_VER, RESERVA_CREAR, RESERVA_EDITAR");

    var rCiclo_704ILR = BLL_Perfil_704ILR.GuardarComposicion_704ILR(idVend_704ILR, new[] { idCrear_704ILR, idEditar_704ILR }, new[] { idGer_704ILR });
    Esperar_704ILR("incluir Gerencial dentro de Vendedor", rCiclo_704ILR, PerfilResult_704ILR.ReferenciaCircular_704ILR);

    var rSelf_704ILR = BLL_Perfil_704ILR.GuardarComposicion_704ILR(idVend_704ILR, new[] { idCrear_704ILR }, new[] { idVend_704ILR });
    Esperar_704ILR("incluir Vendedor dentro de si mismo", rSelf_704ILR, PerfilResult_704ILR.ReferenciaCircular_704ILR);

    // Denegar por defecto, sobre una sesion real y restringida: el usuario de
    // prueba recibe el perfil Vendedor y entra. Tiene que poder crear reservas y
    // NO poder anular pagos, que es un permiso que su perfil no contiene. Hasta
    // ahora la unica denegacion probada era una clave inexistente sobre admin.
    var uSmoke_704ILR = BLL_User_704ILR.GetAll_704ILR()
        .FirstOrDefault(u_704ILR => u_704ILR.Username_704ILR == newUser_704ILR);
    Esperar_704ILR("usuario de prueba disponible", uSmoke_704ILR != null, true);
    if (uSmoke_704ILR != null)
    {
        // Contrato de la capa de negocio: una llamada a AsignarPerfil deja
        // exactamente un asiento 'Asignacion de perfil' (la pantalla de perfiles
        // saltea las filas sin cambio, asi que nunca la llama de mas).
        int asignacionesAntes_704ILR = Asientos_704ILR("Perfiles", "Asignacion de perfil");
        BLL_User_704ILR.AsignarPerfil_704ILR(uSmoke_704ILR.Id_704ILR, idVend_704ILR);
        var asignaciones_704ILR = Bitacora_704ILR("Perfiles", "Asignacion de perfil");
        Esperar_704ILR("asientos 'Asignacion de perfil' tras una llamada", asignaciones_704ILR.Count, asignacionesAntes_704ILR + 1);
        Esperar_704ILR("el asiento nombra el perfil asignado",
            asignaciones_704ILR.Count > 0 && asignaciones_704ILR[0].Detalle_704ILR.Contains($"#{idVend_704ILR}"), true);

        var rLoginV_704ILR = BLL_Login_704ILR.Authenticate_704ILR(newUser_704ILR, Encrypt_704ILR.HashValue_704ILR("pass1234"));
        Esperar_704ILR("login del usuario con perfil Vendedor", rLoginV_704ILR.Result_704ILR, LoginResult_704ILR.Success_704ILR);

        if (SessionManager_704ILR.IsSessionActive_704ILR)
        {
            var sV_704ILR = SessionManager_704ILR.GetInstance_704ILR;
            Esperar_704ILR("sesion con perfil", sV_704ILR.SinPerfil_704ILR, false);
            Esperar_704ILR("permisos resueltos", sV_704ILR.PermisosNoDisponibles_704ILR, false);
            Esperar_704ILR("Vendedor tiene RESERVA_CREAR", sV_704ILR.TienePermiso_704ILR("RESERVA_CREAR"), true);
            Esperar_704ILR("Vendedor tiene RESERVA_EDITAR", sV_704ILR.TienePermiso_704ILR("RESERVA_EDITAR"), true);
            Esperar_704ILR("Vendedor NO tiene PAGOS_ANULAR", sV_704ILR.TienePermiso_704ILR("PAGOS_ANULAR"), false);
            Esperar_704ILR("Vendedor NO tiene PERFILES_GESTION", sV_704ILR.TienePermiso_704ILR("PERFILES_GESTION"), false);
            Esperar_704ILR("permisos efectivos de la sesion", sV_704ILR.Permisos_704ILR.Count, 2);
            BLL_Login_704ILR.Logout_704ILR();
        }

        BLL_User_704ILR.AsignarPerfil_704ILR(uSmoke_704ILR.Id_704ILR, null);
        Esperar_704ILR("asientos 'Asignacion de perfil' tras quitar el perfil",
            Asientos_704ILR("Perfiles", "Asignacion de perfil"), asignacionesAntes_704ILR + 2);
    }

    // Limpieza: los perfiles de prueba se dan de baja. Corridas anteriores
    // llegaron a dejar 76 perfiles 'Vendedor_'/'Gerencial_' en la base de demostracion.
    BorrarPerfilDePrueba_704ILR(idGer_704ILR);
    BorrarPerfilDePrueba_704ILR(idVend_704ILR);
    var nombresPerfiles_704ILR = BLL_Perfil_704ILR.GetPerfiles_704ILR().Select(p_704ILR => p_704ILR.Nombre_704ILR).ToList();
    Esperar_704ILR("perfiles de prueba eliminados",
        nombresPerfiles_704ILR.Any(n_704ILR => n_704ILR.EndsWith(suf_704ILR, StringComparison.Ordinal)), false);
}
catch (Exception ex19_704ILR) { Excepcion_704ILR("[19]", ex19_704ILR); }

// [20] Cifrado reversible (AES) de datos sensibles del cliente
Caso_704ILR("[20] Alta de cliente (CUN002) y cifrado reversible de Email/Telefono:");
try
{
    var cli_704ILR = new EvenTech.BE.BE_Cliente_704ILR
    {
        Nombre_704ILR = "SmokeCrypto",
        Apellido_704ILR = suf_704ILR,
        Dni_704ILR = "9" + suf_704ILR,
        Email_704ILR = $"crypto_{suf_704ILR}@test.com",
        Telefono_704ILR = "11-5555-" + suf_704ILR
    };
    int altasCli_704ILR = Asientos_704ILR("Clientes");
    var rCli_704ILR = BLL_Cliente_704ILR.Crear_704ILR(cli_704ILR, out int idCli_704ILR);
    if (idCli_704ILR > 0) clientesDeLaCorrida_704ILR.Add(idCli_704ILR);
    Esperar_704ILR("alta", rCli_704ILR, ClienteResult_704ILR.Success_704ILR);
    // Postcondicion del CUN002: el alta queda asentada en la bitacora.
    Esperar_704ILR("asientos del modulo Clientes tras el alta", Asientos_704ILR("Clientes"), altasCli_704ILR + 1);

    var leido_704ILR = BLL_Cliente_704ILR.GetById_704ILR(idCli_704ILR);
    Console.WriteLine($"  leido por la app: Email='{leido_704ILR.Email_704ILR}', Telefono='{leido_704ILR.Telefono_704ILR}'");
    Esperar_704ILR("roundtrip del email (cifrar -> descifrar)", leido_704ILR.Email_704ILR, cli_704ILR.Email_704ILR);
    Esperar_704ILR("roundtrip del telefono (cifrar -> descifrar)", leido_704ILR.Telefono_704ILR, cli_704ILR.Telefono_704ILR);
    Esperar_704ILR("DNI persistido", leido_704ILR.Dni_704ILR, cli_704ILR.Dni_704ILR);

    // Flujos alternativos del CUN002 (3.1 DNI ya registrado, 3.2 datos invalidos):
    // ninguno tenia una sola asercion, aunque los tres resultados estan implementados.
    var dup_704ILR = new EvenTech.BE.BE_Cliente_704ILR
    { Nombre_704ILR = "Otro", Apellido_704ILR = "Cliente", Dni_704ILR = "9" + suf_704ILR };
    Esperar_704ILR("alta con el mismo DNI", BLL_Cliente_704ILR.Crear_704ILR(dup_704ILR, out _),
        ClienteResult_704ILR.DniDuplicado_704ILR);

    var sinNombre_704ILR = new EvenTech.BE.BE_Cliente_704ILR { Nombre_704ILR = "  ", Apellido_704ILR = "SinNombre" };
    Esperar_704ILR("alta sin nombre", BLL_Cliente_704ILR.Crear_704ILR(sinNombre_704ILR, out _),
        ClienteResult_704ILR.NombreInvalido_704ILR);

    // Un nombre formado solo por caracteres que no se ven tampoco es un nombre.
    var invisible_704ILR = new EvenTech.BE.BE_Cliente_704ILR { Nombre_704ILR = "\u200B", Apellido_704ILR = "Invisible" };
    Esperar_704ILR("alta con nombre de caracteres invisibles", BLL_Cliente_704ILR.Crear_704ILR(invisible_704ILR, out _),
        ClienteResult_704ILR.NombreInvalido_704ILR);

    var mailMalo_704ILR = new EvenTech.BE.BE_Cliente_704ILR
    { Nombre_704ILR = "Mail", Apellido_704ILR = "Invalido", Email_704ILR = "sin-arroba" };
    Esperar_704ILR("alta con email invalido", BLL_Cliente_704ILR.Crear_704ILR(mailMalo_704ILR, out _),
        ClienteResult_704ILR.EmailInvalido_704ILR);

    // Lectura cruda, salteando la DAL: en la DB tiene que estar cifrado.
    using (var cn_704ILR = new EvenTech.DAL.DAL_DB_Connection_704ILR())
    using (var cmd_704ILR = new Microsoft.Data.SqlClient.SqlCommand(
        "SELECT Email, Telefono FROM dbo.Clientes WHERE Id = @id", cn_704ILR.OpenConnection_704ILR()))
    {
        cmd_704ILR.Parameters.AddWithValue("@id", idCli_704ILR);
        using var r_704ILR = cmd_704ILR.ExecuteReader();
        if (r_704ILR.Read())
        {
            string rawE_704ILR = r_704ILR.GetString(0), rawT_704ILR = r_704ILR.GetString(1);
            Console.WriteLine($"  crudo en DB: Email='{rawE_704ILR[..Math.Min(44, rawE_704ILR.Length)]}...'");
            Esperar_704ILR("email cifrado en la base", CryptoService_704ILR.EstaProtegido_704ILR(rawE_704ILR), true);
            Esperar_704ILR("telefono cifrado en la base", CryptoService_704ILR.EstaProtegido_704ILR(rawT_704ILR), true);
        }
    }

    // Limpieza: el cliente de prueba se borra para no engordar la base de
    // demostracion (no lo referencia ninguna reserva: se creo aca y solo aca).
    BorrarClienteDePrueba_704ILR(idCli_704ILR);
    Esperar_704ILR("limpieza (cliente de prueba eliminado)", BLL_Cliente_704ILR.GetById_704ILR(idCli_704ILR) == null, true);
}
catch (Exception ex20_704ILR) { Excepcion_704ILR("[20]", ex20_704ILR); }

// [21] Control de acceso: los permisos se conceden solo si estan en el perfil
// (denegar por defecto). Se valida sobre la sesion real de admin.
Caso_704ILR("[21] Permisos de la sesion (denegar por defecto):");
try
{
    BLL_Login_704ILR.Authenticate_704ILR("admin", Encrypt_704ILR.HashValue_704ILR("admin123"));
    var s_704ILR = SessionManager_704ILR.GetInstance_704ILR;
    Esperar_704ILR("permisos no disponibles", s_704ILR.PermisosNoDisponibles_704ILR, false);
    Esperar_704ILR("admin sin perfil", s_704ILR.SinPerfil_704ILR, false);
    Esperar_704ILR("admin tiene RESERVA_CREAR", s_704ILR.TienePermiso_704ILR("RESERVA_CREAR"), true);
    Esperar_704ILR("admin tiene PAGOS_ANULAR", s_704ILR.TienePermiso_704ILR("PAGOS_ANULAR"), true);
    Esperar_704ILR("clave inexistente NO_EXISTE", s_704ILR.TienePermiso_704ILR("NO_EXISTE"), false);
    Esperar_704ILR("clave nula", s_704ILR.TienePermiso_704ILR(null), false);
    BLL_Login_704ILR.Logout_704ILR();
}
catch (Exception ex21_704ILR) { Excepcion_704ILR("[21]", ex21_704ILR); }

// [22] Todas las claves que la UI exige tienen que existir en el arbol COMO HOJAS
// (EsGrupo = 0): si una falta, la seccion queda invisible para todos y el problema
// pasa inadvertido. Son exactamente las claves que la ventana principal y las
// pantallas exigen; las siete del Proceso 2 son las mas recientes.
Caso_704ILR("[22] Claves de permiso usadas por la UI presentes en el arbol:");
try
{
    string[] usadas_704ILR = { "RESERVA_CREAR", "RESERVA_EDITAR", "RESERVA_HISTORIAL", "RESERVA_RESTAURAR",
                        "DISPONIBILIDAD_CONSULTAR", "PAGOS_REGISTRAR", "PAGOS_ANULAR",
                        "CLIENTES_GESTION", "SERVICIOS_GESTION", "PERFILES_GESTION",
                        "BITACORA_VER", "AUDIT_LOGIN_VER", "INTEGRIDAD_RECALC", "IDIOMAS_GESTION",
                        // Proceso 2: operaciones de los eventos, agenda del empleado y personal.
                        "EMPLEADOS_GESTION", "PERSONAL_ASIGNAR", "CRONOGRAMA_GESTION", "TAREAS_ASIGNAR",
                        "DISPONIBILIDAD_CONFIRMAR", "AGENDA_CONSULTAR", "EJECUCION_SUPERVISAR" };
    // Solo se recogen las claves de las HOJAS (BE_Permiso): un grupo con el mismo
    // nombre no cuenta, porque no habilita nada por si mismo.
    var enArbol_704ILR = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void Recorrer_704ILR(IEnumerable<EvenTech.BE.BE_IComponentePermiso_704ILR> nodos_704ILR)
    {
        foreach (var n_704ILR in nodos_704ILR)
        {
            if (n_704ILR is EvenTech.BE.BE_Permiso_704ILR hoja_704ILR && !hoja_704ILR.EsGrupo_704ILR &&
                !string.IsNullOrEmpty(hoja_704ILR.Clave_704ILR)) enArbol_704ILR.Add(hoja_704ILR.Clave_704ILR);
            if (n_704ILR is EvenTech.BE.BE_GrupoPermisos_704ILR g_704ILR) Recorrer_704ILR(g_704ILR.Hijos_704ILR);
        }
    }
    Recorrer_704ILR(BLL_Perfil_704ILR.GetArbolPermisos_704ILR());
    var faltan_704ILR = usadas_704ILR.Where(c_704ILR => !enArbol_704ILR.Contains(c_704ILR)).ToList();
    // Y al reves: una hoja que ninguna pantalla exige es un permiso que se asigna
    // y no habilita nada.
    var sobran_704ILR = enArbol_704ILR.Where(c_704ILR => !usadas_704ILR.Contains(c_704ILR, StringComparer.OrdinalIgnoreCase)).ToList();
    Esperar_704ILR("hojas del arbol de permisos", enArbol_704ILR.Count, 21);
    Esperar_704ILR("claves de la UI que faltan como hoja del arbol",
        faltan_704ILR.Count == 0 ? "ninguna" : string.Join(", ", faltan_704ILR), "ninguna");
    Esperar_704ILR("hojas del arbol que ninguna pantalla exige",
        sobran_704ILR.Count == 0 ? "ninguna" : string.Join(", ", sobran_704ILR), "ninguna");
}
catch (Exception ex22_704ILR) { Excepcion_704ILR("[22]", ex22_704ILR); }

// [23] Una reserva cancelada es estado terminal: no admite modificaciones.
Caso_704ILR("[23] Reserva cancelada no modificable:");
try
{
    var sal_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    var cli_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    if (sal_704ILR.Count == 0 || cli_704ILR.Count == 0)
    {
        Omitir_704ILR("[23]", "no hay salones/clientes seed; corre db/schema.sql");
    }
    else
    {
        // RN-05: no se puede nacer cancelado. Se da de alta pendiente y se cancela
        // por la via correcta, que es la unica que liquida la RN-02.
        var altaDirecta_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cli_704ILR[0].Id_704ILR,
            SalonId_704ILR = sal_704ILR[0].Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(2500 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CANCELADA,
            Monto_704ILR = 1000m
        };
        Esperar_704ILR("alta directa en CANCELADA", BLL_Reserva_704ILR.Crear_704ILR(altaDirecta_704ILR, out _),
            ReservaResult_704ILR.TransicionInvalida_704ILR);

        var res_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cli_704ILR[0].Id_704ILR,
            SalonId_704ILR = sal_704ILR[0].Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(2500 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE,
            CantidadInvitados_704ILR = 60,   // RN-06: sin este dato no se puede confirmar
            Monto_704ILR = 1000m
        };
        var rAlta_704ILR = BLL_Reserva_704ILR.Crear_704ILR(res_704ILR, out int idCancel_704ILR);
        Anotar_704ILR(idCancel_704ILR);
        Esperar_704ILR("alta pendiente", rAlta_704ILR, ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("cancelar", BLL_Reserva_704ILR.Cancelar_704ILR(idCancel_704ILR, out _, out _),
            ReservaResult_704ILR.Success_704ILR);

        var guardada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCancel_704ILR);
        Esperar_704ILR("PuedeModificar", BLL_Reserva_704ILR.PuedeModificar_704ILR(guardada_704ILR), false);

        guardada_704ILR.Monto_704ILR = 2000m;
        var rMod_704ILR = BLL_Reserva_704ILR.Actualizar_704ILR(guardada_704ILR);
        Esperar_704ILR("intento de modificar", rMod_704ILR, ReservaResult_704ILR.NoModificable_704ILR);

        // Una reserva viva si se modifica.
        var viva_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCancel_704ILR);
        viva_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE;
        Esperar_704ILR("PuedeModificar sobre PENDIENTE", BLL_Reserva_704ILR.PuedeModificar_704ILR(viva_704ILR), true);

        // Los pagos persisten en el acto, sin pasar por BLL_Reserva.Actualizar:
        // la regla del estado terminal tiene que rechazarlos tambien, y el rechazo
        // queda asentado (flujo 4.2 del CUN004).
        var metodos_704ILR = BLL_Pago_704ILR.GetMetodos_704ILR();
        if (metodos_704ILR.Count > 0)
        {
            int rechazosAntes_704ILR = Asientos_704ILR("Pagos", "Pago rechazado");
            var pago_704ILR = new EvenTech.BE.BE_Pago_704ILR { ReservaId_704ILR = idCancel_704ILR, MetodoPagoId_704ILR = metodos_704ILR[0].Id_704ILR, Monto_704ILR = 10m };
            var rPago_704ILR = BLL_Pago_704ILR.Registrar_704ILR(pago_704ILR, out _);
            Esperar_704ILR("cobrar sobre cancelada", rPago_704ILR, PagoResult_704ILR.ReservaCancelada_704ILR);
            var rechazos_704ILR = Bitacora_704ILR("Pagos", "Pago rechazado");
            Esperar_704ILR("asiento 'Pago rechazado' tras el intento", rechazos_704ILR.Count, rechazosAntes_704ILR + 1);
            Esperar_704ILR("el asiento nombra la reserva cancelada",
                rechazos_704ILR.Count > 0 && rechazos_704ILR[0].Detalle_704ILR.Contains($"#{idCancel_704ILR}"), true);
        }
        else
        {
            Omitir_704ILR("[23]", "no hay metodos de pago sembrados: el cobro sobre cancelada no se probo");
        }
    }
}
catch (Exception ex23_704ILR) { Excepcion_704ILR("[23]", ex23_704ILR); }

// [24] Configuracion de conexion: la cadena sale del gestor (no hardcodeada) y
// el diagnostico distingue servidor caido de base inexistente.
Caso_704ILR("[24] Configuracion de conexion:");
try
{
    Console.WriteLine($"  configurada por el usuario: {BLL_Conexion_704ILR.EstaConfigurada_704ILR}");
    Console.WriteLine($"  servidor='{BLL_Conexion_704ILR.ServidorActual_704ILR}', base='{BLL_Conexion_704ILR.BaseDatosActual_704ILR}'");

    bool ok_704ILR = BLL_Conexion_704ILR.VerificarActual_704ILR(out string msgOk_704ILR);
    Esperar_704ILR("verificar actual", ok_704ILR, true);
    if (!ok_704ILR) Console.WriteLine($"    diagnostico: {msgOk_704ILR}");

    bool inexistente_704ILR = BLL_Conexion_704ILR.Probar_704ILR(EvenTech.Services.ConfiguracionConexion_704ILR.ServidorActual_704ILR,
                                           "BaseQueNoExiste_" + suf_704ILR, out string msgNo_704ILR);
    Esperar_704ILR("base inexistente aceptada", inexistente_704ILR, false);
    Console.WriteLine($"    diagnostico: {msgNo_704ILR}");

    Console.WriteLine($"  instancias detectadas: {BLL_Conexion_704ILR.GetInstancias_704ILR().Count}");

    // Roundtrip del archivo cifrado con DPAPI: si guardar/leer fallara, la app
    // quedaria sin poder conectar en el proximo arranque. Se prueba con la
    // configuracion que ya funciona y se deja el entorno como estaba.
    bool estabaConfigurada_704ILR = BLL_Conexion_704ILR.EstaConfigurada_704ILR;
    string servidorPrevio_704ILR = BLL_Conexion_704ILR.ServidorActual_704ILR, basePrevia_704ILR = BLL_Conexion_704ILR.BaseDatosActual_704ILR;

    bool guardo_704ILR = BLL_Conexion_704ILR.Guardar_704ILR(servidorPrevio_704ILR, basePrevia_704ILR, out string msgGuardar_704ILR);
    Esperar_704ILR("guardar cifrado (DPAPI)", guardo_704ILR, true);
    if (!guardo_704ILR) Console.WriteLine($"    diagnostico: {msgGuardar_704ILR}");
    Esperar_704ILR("persistida", BLL_Conexion_704ILR.EstaConfigurada_704ILR, true);

    bool releeOk_704ILR = BLL_Conexion_704ILR.VerificarActual_704ILR(out _);
    Esperar_704ILR("releida y conecta", releeOk_704ILR, true);
    Esperar_704ILR("servidor releido", BLL_Conexion_704ILR.ServidorActual_704ILR, servidorPrevio_704ILR);
    Esperar_704ILR("base releida", BLL_Conexion_704ILR.BaseDatosActual_704ILR, basePrevia_704ILR);

    // El entorno queda como estaba: sin archivo si no lo habia y con el suyo si lo
    // habia. La verificacion corre siempre, para que la cantidad de verificaciones
    // de la corrida no dependa de la maquina.
    if (!estabaConfigurada_704ILR) BLL_Conexion_704ILR.Restablecer_704ILR();
    Esperar_704ILR("entorno como estaba antes de la prueba", BLL_Conexion_704ILR.EstaConfigurada_704ILR, estabaConfigurada_704ILR);
}
catch (Exception ex24_704ILR) { Excepcion_704ILR("[24]", ex24_704ILR); }

// [25] Diagnostico de conexion: una base sin el esquema tiene que rechazarse, si
// no la app quedaria conectada a una base inservible sin volver a ofrecer
// configurar; y una base con el esquema a medias (solo Users, o de una revision
// anterior) tambien, con un diagnostico que lo diga.
// La base auxiliar lleva el sufijo de la corrida (nombre unico) y se borra SOLO
// si esta corrida la creo: antes el nombre era fijo y el final la eliminaba
// existiera de quien existiera, con ROLLBACK IMMEDIATE sobre quien la usara.
Caso_704ILR("[25] Base existente pero sin esquema o con esquema incompleto:");
{
    string tmpDb_704ILR = "EvenTechSmokeVacia_" + suf_704ILR;
    string csMaster_704ILR = EvenTech.Services.ConfiguracionConexion_704ILR.Construir_704ILR(
        EvenTech.Services.ConfiguracionConexion_704ILR.ServidorActual_704ILR, "master");
    string cs_704ILR = EvenTech.Services.ConfiguracionConexion_704ILR.Construir_704ILR(
        EvenTech.Services.ConfiguracionConexion_704ILR.ServidorActual_704ILR, tmpDb_704ILR);
    bool creada_704ILR = false;
    try
    {
        using (var cn_704ILR = new Microsoft.Data.SqlClient.SqlConnection(csMaster_704ILR))
        {
            cn_704ILR.Open();
            using var crear_704ILR = new Microsoft.Data.SqlClient.SqlCommand(
                $"IF DB_ID('{tmpDb_704ILR}') IS NULL BEGIN CREATE DATABASE [{tmpDb_704ILR}]; SELECT 1 END ELSE SELECT 0", cn_704ILR);
            creada_704ILR = Convert.ToInt32(crear_704ILR.ExecuteScalar()) == 1;
        }

        if (!creada_704ILR)
        {
            Omitir_704ILR("[25]", $"la base '{tmpDb_704ILR}' ya existia (no se toca)");
        }
        else
        {
            bool ok_704ILR = EvenTech.DAL.DAL_DB_Connection_704ILR.Probar_704ILR(cs_704ILR, out string msg_704ILR);
            Esperar_704ILR("base sin esquema aceptada", ok_704ILR, false);
            Console.WriteLine($"    diagnostico: {msg_704ILR}");

            // Esquema incompleto: con Users sola la base ya no es "vacia", pero le
            // faltan las otras 26 tablas y las columnas migradas.
            using (var cn_704ILR = new Microsoft.Data.SqlClient.SqlConnection(cs_704ILR))
            {
                cn_704ILR.Open();
                using var tabla_704ILR = new Microsoft.Data.SqlClient.SqlCommand(
                    "CREATE TABLE dbo.Users (Id INT IDENTITY PRIMARY KEY, Username NVARCHAR(50), PasswordHash NVARCHAR(64), CreatedAt DATETIME)", cn_704ILR);
                tabla_704ILR.ExecuteNonQuery();
            }
            bool okParcial_704ILR = EvenTech.DAL.DAL_DB_Connection_704ILR.Probar_704ILR(cs_704ILR, out string msgParcial_704ILR);
            Esperar_704ILR("esquema incompleto aceptado", okParcial_704ILR, false);
            Esperar_704ILR("mensaje indica esquema incompleto", (msgParcial_704ILR ?? "").Contains("incompleto"), true);
            Console.WriteLine($"    diagnostico: {msgParcial_704ILR}");

            // Control: la base configurada (esquema completo) si pasa el diagnostico.
            Esperar_704ILR("la base configurada pasa el diagnostico",
                EvenTech.DAL.DAL_DB_Connection_704ILR.Probar_704ILR(EvenTech.DAL.DAL_DB_Connection_704ILR.ConnectionString_704ILR, out _), true);
        }
    }
    catch (Exception ex_704ILR)
    {
        if (!creada_704ILR) Omitir_704ILR("[25]", $"no se pudo crear la base de prueba: {ex_704ILR.Message}");
        else Excepcion_704ILR("[25]", ex_704ILR);
    }
    finally
    {
        if (creada_704ILR)
        {
            try
            {
                using var cn_704ILR = new Microsoft.Data.SqlClient.SqlConnection(csMaster_704ILR);
                cn_704ILR.Open();
                using var borrar_704ILR = new Microsoft.Data.SqlClient.SqlCommand(
                    $"IF DB_ID('{tmpDb_704ILR}') IS NOT NULL BEGIN ALTER DATABASE [{tmpDb_704ILR}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{tmpDb_704ILR}]; END " +
                    $"SELECT CASE WHEN DB_ID('{tmpDb_704ILR}') IS NULL THEN 1 ELSE 0 END", cn_704ILR);
                Esperar_704ILR("limpieza (base de prueba eliminada)", Convert.ToInt32(borrar_704ILR.ExecuteScalar()), 1);
            }
            catch (Exception ex_704ILR) { Excepcion_704ILR("[25] limpieza", ex_704ILR); }
        }
    }
}

// [26] Flujo completo del Proceso 1 (RF1): cotizacion con servicios -> total =
// suma de subtotales -> confirmacion (anti-solapamiento) -> adelanto y saldo
// (tope = total). Es el happy path que la UI recorre pantalla por pantalla.
Caso_704ILR("[26] Flujo RF1 completo (servicios, confirmacion, pagos):");
try
{
    var sal_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    var cli_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var srv_704ILR = BLL_Servicio_704ILR.GetActivos_704ILR();
    if (sal_704ILR.Count == 0 || cli_704ILR.Count == 0 || srv_704ILR.Count < 2)
    {
        Omitir_704ILR("[26]", "faltan salones/clientes/servicios seed (hacen falta 2 servicios activos); corre db/schema.sql");
        Caso_704ILR("[27] Consulta de disponibilidad:");
        Omitir_704ILR("[27]", "depende de la reserva confirmada de [26]");
    }
    else
    {
        // Fecha propia de la corrida y ademas libre de verdad para ese salon. La
        // ventana cercana (+60 dias) se cruza con los datos de demostracion, asi
        // que la fecha se resuelve con la consulta de disponibilidad en lugar de
        // apostar a que el dia elegido este vacio: si no, la confirmacion del
        // happy path fallaba por SalonOcupado sin que hubiera nada roto.
        DateTime fechaEvento_704ILR = FechaLibre_704ILR(sal_704ILR[0].Id_704ILR, DateTime.Today.AddDays(60 + desfasaje_704ILR));
        Console.WriteLine($"  salon '{sal_704ILR[0].Nombre_704ILR}', fecha del evento {fechaEvento_704ILR:yyyy-MM-dd}");

        // Cotizacion: no compromete el salon. El monto es la suma de servicios.
        var servicios_704ILR = new List<EvenTech.BE.BE_ReservaServicio_704ILR>
        {
            new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = srv_704ILR[0].Id_704ILR, Cantidad_704ILR = 2, PrecioUnitario_704ILR = srv_704ILR[0].Precio_704ILR },
            new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = srv_704ILR[1].Id_704ILR, Cantidad_704ILR = 1, PrecioUnitario_704ILR = srv_704ILR[1].Precio_704ILR }
        };
        decimal total_704ILR = BLL_ReservaServicio_704ILR.Total_704ILR(servicios_704ILR);
        decimal esperado_704ILR = srv_704ILR[0].Precio_704ILR * 2 + srv_704ILR[1].Precio_704ILR;
        Esperar_704ILR("total de servicios", total_704ILR, esperado_704ILR);

        var cot_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cli_704ILR[0].Id_704ILR,
            SalonId_704ILR = sal_704ILR[0].Id_704ILR,
            FechaEvento_704ILR = fechaEvento_704ILR,
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = 50,   // RN-06: sin este dato no se puede confirmar
            Monto_704ILR = total_704ILR
        };
        // Cabecera y lineas en una sola transaccion, igual que la aplicacion.
        var rCot_704ILR = BLL_Reserva_704ILR.Crear_704ILR(cot_704ILR, servicios_704ILR, out int idFlujo_704ILR);
        Anotar_704ILR(idFlujo_704ILR);
        Esperar_704ILR("alta cotizacion", rCot_704ILR, ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("servicios persistidos", BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idFlujo_704ILR).Count, servicios_704ILR.Count);

        // RN-07: el adelanto se cobra ANTES de confirmar. Ese es el orden del proceso
        // de negocio: se registra la operacion, se cobra y recien ahi queda firme.
        var metodos_704ILR = BLL_Pago_704ILR.GetMetodos_704ILR();
        Esperar_704ILR("metodos de pago", metodos_704ILR.Count, 5);
        decimal adelanto_704ILR = Math.Round(total_704ILR / 2, 2);
        var rAde_704ILR = BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idFlujo_704ILR, MetodoPagoId_704ILR = metodos_704ILR[0].Id_704ILR, Monto_704ILR = adelanto_704ILR, Observacion_704ILR = "Adelanto" }, out _);
        Esperar_704ILR($"adelanto de {adelanto_704ILR:0.00}", rAde_704ILR, PagoResult_704ILR.Success_704ILR);
        Esperar_704ILR("saldo tras adelanto", BLL_Pago_704ILR.Saldo_704ILR(idFlujo_704ILR), total_704ILR - adelanto_704ILR);

        // Confirmar: recien aca se compromete el salon.
        var reserva_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idFlujo_704ILR);
        reserva_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        var rConf_704ILR = BLL_Reserva_704ILR.Actualizar_704ILR(reserva_704ILR);
        Esperar_704ILR("confirmar", rConf_704ILR, ReservaResult_704ILR.Success_704ILR);

        // RN-03: el salon queda comprometido SOLO para reservas firmes. Una
        // cotizacion y una reserva pendiente para el mismo salon y la misma fecha
        // tienen que poder convivir con la confirmada; lo que no puede es una
        // SEGUNDA confirmada. Antes solo se probaba el rechazo: si alguien
        // cambiaba el filtro a "distinto de CANCELADA" la corrida seguia verde.
        var choque_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cli_704ILR[0].Id_704ILR,
            SalonId_704ILR = sal_704ILR[0].Id_704ILR,
            FechaEvento_704ILR = fechaEvento_704ILR,
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE,
            CantidadInvitados_704ILR = 70,   // RN-06: sin este dato no se puede confirmar
            Monto_704ILR = 1000m
        };
        Esperar_704ILR("pendiente sobre salon/fecha ya confirmados",
            BLL_Reserva_704ILR.Crear_704ILR(choque_704ILR, out int idChoque_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idChoque_704ILR);

        var coex_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cli_704ILR[0].Id_704ILR,
            SalonId_704ILR = sal_704ILR[0].Id_704ILR,
            FechaEvento_704ILR = fechaEvento_704ILR,
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = 40,
            Monto_704ILR = 500m
        };
        Esperar_704ILR("cotizacion sobre salon/fecha ya confirmados",
            BLL_Reserva_704ILR.Crear_704ILR(coex_704ILR, out int idCoex_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idCoex_704ILR);

        // La pendiente nace con su adelanto cobrado (RN-07), de modo que lo unico
        // que puede rechazar su confirmacion es el salon ya comprometido.
        Adelanto_704ILR(idChoque_704ILR, 100m);
        var aChocar_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idChoque_704ILR);
        aChocar_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        var rChoque_704ILR = BLL_Reserva_704ILR.Actualizar_704ILR(aChocar_704ILR);
        Esperar_704ILR("segunda confirmada mismo salon/fecha", rChoque_704ILR, ReservaResult_704ILR.SalonOcupado_704ILR);
        Esperar_704ILR("limpieza (cancelar la pendiente de choque)",
            BLL_Reserva_704ILR.Cancelar_704ILR(idChoque_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);

        // RN-03 en el motor: el indice unico filtrado sobre (salon, fecha) de las
        // CONFIRMADA es la red de seguridad cuando dos operaciones pasan la
        // validacion previa a la vez. Desde la capa de negocio no se puede llegar
        // (Validar rechaza antes con SalonOcupado), asi que se escribe por la DAL
        // una segunda confirmada y se espera el rechazo del motor (2601 indice
        // unico / 2627 restriccion unica). No queda fila: el INSERT no se ejecuta.
        int numeroSql_704ILR = 0, idDuplicada_704ILR = 0;
        try
        {
            idDuplicada_704ILR = EvenTech.DAL.DAL_Reserva_704ILR.Insert_704ILR(new EvenTech.BE.BE_Reserva_704ILR
            {
                ClienteId_704ILR = cli_704ILR[0].Id_704ILR,
                SalonId_704ILR = sal_704ILR[0].Id_704ILR,
                FechaEvento_704ILR = fechaEvento_704ILR,
                Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA,
                CantidadInvitados_704ILR = 10,
                Monto_704ILR = 1m,
                Dvh_704ILR = "smoke"
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException exSql_704ILR) { numeroSql_704ILR = exSql_704ILR.Number; }
        Console.WriteLine($"  error del motor ante la segunda CONFIRMADA: {numeroSql_704ILR}");
        Esperar_704ILR("indice unico del motor rechaza una segunda CONFIRMADA (RN-03)",
            numeroSql_704ILR == 2601 || numeroSql_704ILR == 2627, true);
        if (idDuplicada_704ILR > 0)
        {
            // El motor no la rechazo (indice ausente): se borra la fila cruda para
            // no dejar una confirmada sin DV; el fallo ya quedo contado arriba.
            Ejecutar_704ILR("DELETE FROM dbo.Reservas WHERE Id = @id", ("@id", idDuplicada_704ILR));
        }

        // Memento sobre la operacion COMPLETA: la version guarda tambien los
        // servicios contratados, asi que restaurarla tiene que reponerlos. Se
        // quita una linea con solo el adelanto cobrado (queda la de mayor subtotal,
        // que cubre el adelanto: la RN-04 no admite un total por debajo de lo
        // pagado) y se vuelve a la version anterior. El monto de la cabecera lo
        // fija la capa de negocio como suma de las lineas que viajan: el que se
        // mande desde la pantalla no cuenta.
        var lineaMayor_704ILR = servicios_704ILR.OrderByDescending(l_704ILR => l_704ILR.Subtotal_704ILR).First();
        var unServicio_704ILR = new List<EvenTech.BE.BE_ReservaServicio_704ILR>
        {
            new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = lineaMayor_704ILR.ServicioId_704ILR, Cantidad_704ILR = lineaMayor_704ILR.Cantidad_704ILR, PrecioUnitario_704ILR = lineaMayor_704ILR.PrecioUnitario_704ILR }
        };
        var recortada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idFlujo_704ILR);
        Esperar_704ILR("quitar una linea de servicio (con solo el adelanto cobrado)",
            BLL_Reserva_704ILR.Actualizar_704ILR(recortada_704ILR, unServicio_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("servicios tras la edicion", BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idFlujo_704ILR).Count, 1);
        Esperar_704ILR("monto normalizado a la suma de las lineas",
            BLL_Reserva_704ILR.GetById_704ILR(idFlujo_704ILR).Monto_704ILR, BLL_ReservaServicio_704ILR.Total_704ILR(unServicio_704ILR));

        // Versiones: la de la confirmacion (COTIZACION, 2 lineas) y la del recorte
        // (CONFIRMADA, 2 lineas); la mas reciente va primera.
        var versionesFlujo_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idFlujo_704ILR);
        Esperar_704ILR("versiones de la reserva del flujo", versionesFlujo_704ILR.Count, 2);
        // El listado no carga las lineas (solo hacen falta al restaurar): la
        // composicion de la version se lee con la version puntual.
        var masReciente_704ILR = versionesFlujo_704ILR.Count > 0
            ? CaretakerReserva_704ILR.GetVersion_704ILR(versionesFlujo_704ILR[0].Id_704ILR) : null;
        Esperar_704ILR("la version mas reciente es la confirmada con dos lineas",
            masReciente_704ILR != null && masReciente_704ILR.Estado_704ILR == EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA
                && masReciente_704ILR.Servicios_704ILR.Count == 2, true);
        Esperar_704ILR("restaurar la version con las dos lineas",
            BLL_Reserva_704ILR.RestaurarVersion_704ILR(idFlujo_704ILR, versionesFlujo_704ILR[0].Id_704ILR),
            ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("servicios repuestos por la restauracion",
            BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idFlujo_704ILR).Count, 2);
        Esperar_704ILR("monto tras la restauracion", BLL_Reserva_704ILR.GetById_704ILR(idFlujo_704ILR).Monto_704ILR, total_704ILR);
        Esperar_704ILR("sigue confirmada tras la restauracion",
            BLL_Reserva_704ILR.GetById_704ILR(idFlujo_704ILR).Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA);

        var rExceso_704ILR = BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idFlujo_704ILR, MetodoPagoId_704ILR = metodos_704ILR[0].Id_704ILR, Monto_704ILR = total_704ILR }, out _);
        Esperar_704ILR("pago que excede el saldo", rExceso_704ILR, PagoResult_704ILR.ExcedeSaldo_704ILR);

        var rSaldo_704ILR = BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idFlujo_704ILR, MetodoPagoId_704ILR = metodos_704ILR[metodos_704ILR.Count - 1].Id_704ILR, Monto_704ILR = total_704ILR - adelanto_704ILR, Observacion_704ILR = "Saldo" }, out _);
        Esperar_704ILR("saldo restante", rSaldo_704ILR, PagoResult_704ILR.Success_704ILR);
        Esperar_704ILR("saldo final", BLL_Pago_704ILR.Saldo_704ILR(idFlujo_704ILR), 0m);

        // RN-04 como invariante: con el total ya cobrado, una edicion que achique la
        // reserva por debajo de lo pagado se rechaza, venga por la cabecera o por
        // las lineas (el monto se normaliza a la suma de las lineas antes de juzgar).
        var achicada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idFlujo_704ILR);
        achicada_704ILR.Monto_704ILR = achicada_704ILR.Monto_704ILR / 2;
        Esperar_704ILR("reducir el total por debajo de lo pagado",
            BLL_Reserva_704ILR.Actualizar_704ILR(achicada_704ILR), ReservaResult_704ILR.MontoInferiorPagado_704ILR);
        Esperar_704ILR("quitar una linea con todo cobrado",
            BLL_Reserva_704ILR.Actualizar_704ILR(BLL_Reserva_704ILR.GetById_704ILR(idFlujo_704ILR), unServicio_704ILR),
            ReservaResult_704ILR.MontoInferiorPagado_704ILR);
        Esperar_704ILR("las dos lineas siguen", BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idFlujo_704ILR).Count, 2);
        Esperar_704ILR("el total no bajo", BLL_Reserva_704ILR.GetById_704ILR(idFlujo_704ILR).Monto_704ILR, total_704ILR);

        // RN-04 tambien al restaurar: reponer una version mas barata que lo ya
        // cobrado dejaria el total por debajo del tope de cobranza. Se prueba en una
        // operacion propia, porque en la del flujo todas las versiones valen lo mismo.
        var barata_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cli_704ILR[0].Id_704ILR,
            SalonId_704ILR = sal_704ILR[0].Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(6000 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = 10,
            Monto_704ILR = 1000m
        };
        Esperar_704ILR("alta de la cotizacion barata",
            BLL_Reserva_704ILR.Crear_704ILR(barata_704ILR, out int idBarata_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idBarata_704ILR);
        var ampliada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idBarata_704ILR);
        ampliada_704ILR.Monto_704ILR = 3000m;
        Esperar_704ILR("ampliar el total a 3000 (versiona el de 1000)",
            BLL_Reserva_704ILR.Actualizar_704ILR(ampliada_704ILR), ReservaResult_704ILR.Success_704ILR);
        Adelanto_704ILR(idBarata_704ILR, 2000m);
        var versionesBarata_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idBarata_704ILR);
        Esperar_704ILR("restaurar una version por debajo de lo cobrado",
            BLL_Reserva_704ILR.RestaurarVersion_704ILR(idBarata_704ILR, versionesBarata_704ILR[0].Id_704ILR),
            ReservaResult_704ILR.MontoInferiorPagado_704ILR);
        Esperar_704ILR("el total no se movio", BLL_Reserva_704ILR.GetById_704ILR(idBarata_704ILR).Monto_704ILR, 3000m);
        Esperar_704ILR("limpieza (cancelar la cotizacion barata)",
            BLL_Reserva_704ILR.Cancelar_704ILR(idBarata_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);

        // [27] Consulta de disponibilidad (Proceso 1, paso 1): la fecha recien
        // confirmada tiene que figurar ocupada para ese salon, con una fecha
        // alternativa propuesta; una capacidad imposible marca insuficiente. La
        // consulta queda asentada en la bitacora desde la capa de negocio
        // (postcondicion del CUN001), con la fecha efectivamente consultada.
        Caso_704ILR("[27] Consulta de disponibilidad:");
        int consultasAntes_704ILR = Asientos_704ILR("Reservas", "Disponibilidad consultada");
        var disp_704ILR = BLL_Disponibilidad_704ILR.Consultar_704ILR(fechaEvento_704ILR, 0);
        Esperar_704ILR("salones evaluados", disp_704ILR.Count, sal_704ILR.Count);
        var delFlujo_704ILR = disp_704ILR.FirstOrDefault(d_704ILR => d_704ILR.SalonId_704ILR == sal_704ILR[0].Id_704ILR);
        Esperar_704ILR("salon confirmado libre", delFlujo_704ILR?.Libre_704ILR, false);
        Esperar_704ILR("propone una fecha alternativa", delFlujo_704ILR?.ProximaFechaLibre_704ILR.HasValue, true);
        if (delFlujo_704ILR?.ProximaFechaLibre_704ILR != null)
            Console.WriteLine($"    propuesta: {delFlujo_704ILR.ProximaFechaLibre_704ILR.Value:yyyy-MM-dd}");

        var consultas_704ILR = Bitacora_704ILR("Reservas", "Disponibilidad consultada");
        Esperar_704ILR("asientos 'Disponibilidad consultada' tras la consulta", consultas_704ILR.Count, consultasAntes_704ILR + 1);
        string detalleConsulta_704ILR = consultas_704ILR.Count > 0 ? consultas_704ILR[0].Detalle_704ILR ?? "" : "";
        Console.WriteLine($"    asiento: {detalleConsulta_704ILR}");
        Esperar_704ILR("el asiento lleva fecha e invitados consultados",
            detalleConsulta_704ILR.StartsWith("Fecha " + fechaEvento_704ILR.ToString("yyyy-MM-dd") + " | Invitados 0 | Disponibles: ", StringComparison.Ordinal), true);
        Esperar_704ILR("el asiento cierra con el total de salones",
            detalleConsulta_704ILR.EndsWith("/" + sal_704ILR.Count, StringComparison.Ordinal), true);
        Esperar_704ILR("criticidad del asiento",
            consultas_704ILR.Count > 0 ? consultas_704ILR[0].Criticidad_704ILR : default, EvenTech.BE.CriticidadBitacora_704ILR.Info);

        var dispCap_704ILR = BLL_Disponibilidad_704ILR.Consultar_704ILR(fechaEvento_704ILR, 99999);
        Esperar_704ILR("capacidad imposible -> disponibles", dispCap_704ILR.Count(d_704ILR => d_704ILR.Disponible_704ILR), 0);
        Esperar_704ILR("capacidad imposible -> suficientes", dispCap_704ILR.Count(d_704ILR => d_704ILR.CapacidadSuficiente_704ILR), 0);
        // Sin capacidad suficiente no se calcula propuesta alternativa: ofrecer otra
        // fecha de un salon que igual no entra no le sirve a nadie (paso 4 del CUN001).
        Esperar_704ILR("capacidad imposible -> sin propuesta alternativa",
            dispCap_704ILR.All(d_704ILR => !d_704ILR.ProximaFechaLibre_704ILR.HasValue), true);

        // Flujo alternativo 2.1 del CUN001: una fecha anterior a hoy se ajusta al dia
        // de hoy en lugar de rechazarse (el vendedor consulta "a partir de"), y el
        // asiento lleva la fecha ajustada, no la pedida.
        var dispPasado_704ILR = BLL_Disponibilidad_704ILR.Consultar_704ILR(DateTime.Today.AddDays(-5), 0);
        Esperar_704ILR("fecha pasada ajustada a hoy",
            dispPasado_704ILR.All(d_704ILR => d_704ILR.FechaConsultada_704ILR == DateTime.Today), true);
        var asientoPasado_704ILR = Bitacora_704ILR("Reservas", "Disponibilidad consultada");
        Esperar_704ILR("el asiento del flujo 2.1 lleva la fecha de hoy",
            asientoPasado_704ILR.Count > 0 && (asientoPasado_704ILR[0].Detalle_704ILR ?? "")
                .StartsWith("Fecha " + DateTime.Today.ToString("yyyy-MM-dd"), StringComparison.Ordinal), true);

        // Un dia sin reservas confirmadas: todos los salones libres.
        var dispLibre_704ILR = BLL_Disponibilidad_704ILR.Consultar_704ILR(fechaEvento_704ILR.AddDays(2000), 0);
        Esperar_704ILR("fecha lejana -> disponibles",
            dispLibre_704ILR.Count(d_704ILR => d_704ILR.Disponible_704ILR), dispLibre_704ILR.Count);

        // Limpieza: se cancela la reserva del flujo para liberar el salon
        // (la corrida queda repetible aunque la fecha se repitiera). Se usa la via
        // de cancelacion, que es la unica admitida para entrar a CANCELADA (RN-05)
        // y la que liquida la politica de reintegro (RN-02).
        var rFin_704ILR = BLL_Reserva_704ILR.Cancelar_704ILR(idFlujo_704ILR, out _, out _);
        Esperar_704ILR("limpieza (cancelar reserva del flujo)", rFin_704ILR, ReservaResult_704ILR.Success_704ILR);

        // RN-03, otra vez: una reserva CANCELADA deja de comprometer el salon. Con
        // la del flujo dada de baja, ese mismo salon y esa misma fecha vuelven a
        // admitir una reserva firme.
        Esperar_704ILR("salon liberado tras la cancelacion",
            BLL_Disponibilidad_704ILR.Consultar_704ILR(fechaEvento_704ILR, 0)
                .First(d_704ILR => d_704ILR.SalonId_704ILR == sal_704ILR[0].Id_704ILR).Libre_704ILR, true);

        var reocupa_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCoex_704ILR);
        reocupa_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Adelanto_704ILR(idCoex_704ILR, 100m);   // RN-07
        Esperar_704ILR("confirmar sobre la fecha liberada",
            BLL_Reserva_704ILR.Actualizar_704ILR(reocupa_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("limpieza (cancelar la reserva de coexistencia)",
            BLL_Reserva_704ILR.Cancelar_704ILR(idCoex_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex26_704ILR) { Excepcion_704ILR("[26]-[27]", ex26_704ILR); }

// [28] RN-01 Vigencia de la operacion: una COTIZACION nace con fecha de
// vencimiento, una CONFIRMADA no; una operacion vencida no puede AVANZAR de
// estado hasta renovarla. Los plazos se contrastan con los literales que
// documenta la Carpeta (15 dias, 72 horas), no con las constantes de la capa
// de negocio: si alguien cambiara la constante, el documento quedaria
// desbalanceado y la prueba tiene que decirlo.
Caso_704ILR("[28] RN-01 vigencia de la operacion:");
try
{
    var cliRnLista_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salRnLista_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (cliRnLista_704ILR.Count == 0 || salRnLista_704ILR.Count == 0)
    {
        Omitir_704ILR("[28]", "faltan clientes/salones seed; corre db/schema.sql");
        Caso_704ILR("[29] RN-02 politica de cancelacion:");
        Omitir_704ILR("[29]", "depende de la reserva confirmada de [28]");
        throw new OperationCanceledException("omitido");
    }
    int cliRn_704ILR = cliRnLista_704ILR[0].Id_704ILR;
    int salRn_704ILR = salRnLista_704ILR[0].Id_704ILR;
    DateTime fechaRn_704ILR = DateTime.Today.AddDays(3000 + desfasaje_704ILR);

    var cot_704ILR = new EvenTech.BE.BE_Reserva_704ILR
    {
        ClienteId_704ILR = cliRn_704ILR,
        SalonId_704ILR = salRn_704ILR,
        FechaEvento_704ILR = fechaRn_704ILR,
        Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
        CantidadInvitados_704ILR = 50,   // RN-06: sin este dato no se puede confirmar
        Monto_704ILR = 1000m
    };
    var rCot_704ILR = BLL_Reserva_704ILR.Crear_704ILR(cot_704ILR, out int idCot_704ILR);
    Anotar_704ILR(idCot_704ILR);
    var leida_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot_704ILR);
    Esperar_704ILR("alta cotizacion", rCot_704ILR, ReservaResult_704ILR.Success_704ILR);
    Esperar_704ILR("nace con fecha de vencimiento", leida_704ILR.VenceEl_704ILR.HasValue, true);
    Esperar_704ILR("vencida hoy", leida_704ILR.EstaVencida_704ILR, false);

    int diasReales_704ILR = leida_704ILR.VenceEl_704ILR.HasValue
        ? (int)Math.Round((leida_704ILR.VenceEl_704ILR.Value - DateTime.Now).TotalDays) : -1;
    Esperar_704ILR("plazo de la cotizacion en dias (RN-01: 15 dias)", diasReales_704ILR, 15);

    // Al confirmar, la operacion deja de tener plazo.
    Adelanto_704ILR(idCot_704ILR, 100m);   // RN-07
    leida_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
    var rConf_704ILR = BLL_Reserva_704ILR.Actualizar_704ILR(leida_704ILR);
    var confirmada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot_704ILR);
    Esperar_704ILR("confirmar", rConf_704ILR, ReservaResult_704ILR.Success_704ILR);
    Esperar_704ILR("vence tras confirmar", confirmada_704ILR.VenceEl_704ILR.HasValue, false);

    // El vencimiento se prueba sobre una SEGUNDA cotizacion: una vez confirmada,
    // la RN-05 ya no admite volver a COTIZACION (no se puede "desconfirmar").
    var cot2_704ILR = new EvenTech.BE.BE_Reserva_704ILR
    {
        ClienteId_704ILR = cliRn_704ILR,
        SalonId_704ILR = salRn_704ILR,
        FechaEvento_704ILR = fechaRn_704ILR.AddDays(1),
        Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
        CantidadInvitados_704ILR = 50,   // RN-06: sin este dato no se puede confirmar
        Monto_704ILR = 500m
    };
    Esperar_704ILR("alta de la segunda cotizacion",
        BLL_Reserva_704ILR.Crear_704ILR(cot2_704ILR, out int idCot2_704ILR), ReservaResult_704ILR.Success_704ILR);
    Anotar_704ILR(idCot2_704ILR);

    var paraVencer_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot2_704ILR);
    paraVencer_704ILR.VenceEl_704ILR = DateTime.Now.AddDays(-1);
    EvenTech.DAL.DAL_Reserva_704ILR.Update_704ILR(paraVencer_704ILR);

    var vencida_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot2_704ILR);
    Esperar_704ILR("vencida forzada", vencida_704ILR.EstaVencida_704ILR, true);
    vencida_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
    var rVenc_704ILR = BLL_Reserva_704ILR.Actualizar_704ILR(vencida_704ILR);
    Esperar_704ILR("confirmar vencida", rVenc_704ILR, ReservaResult_704ILR.Vencida_704ILR);

    // El rodeo en dos pasos tampoco: una cotizacion vencida no pasa a PENDIENTE
    // (que le daria plazo nuevo sin renovar) y desde ahi a CONFIRMADA. El control
    // de vigencia cubre cualquier cambio de estado, no solo la confirmacion.
    var rodeo_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot2_704ILR);
    rodeo_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE;
    Esperar_704ILR("COTIZACION vencida -> PENDIENTE (rodeo en dos pasos)",
        BLL_Reserva_704ILR.Actualizar_704ILR(rodeo_704ILR), ReservaResult_704ILR.Vencida_704ILR);
    Esperar_704ILR("sigue en COTIZACION", BLL_Reserva_704ILR.GetById_704ILR(idCot2_704ILR).Estado_704ILR,
        EvenTech.BE.EstadoReserva_704ILR.COTIZACION);
    // Seguir editando la cotizacion vencida sin cambiar de estado si se admite.
    var editaVencida_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot2_704ILR);
    editaVencida_704ILR.Monto_704ILR = 550m;
    Esperar_704ILR("editar la cotizacion vencida sin cambiar de estado",
        BLL_Reserva_704ILR.Actualizar_704ILR(editaVencida_704ILR), ReservaResult_704ILR.Success_704ILR);

    var rRen_704ILR = BLL_Reserva_704ILR.Renovar_704ILR(idCot2_704ILR);
    Esperar_704ILR("renovar", rRen_704ILR, ReservaResult_704ILR.Success_704ILR);
    var renovada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot2_704ILR);
    Esperar_704ILR("vencida tras renovar", renovada_704ILR.EstaVencida_704ILR, false);
    // Renovada, el pase a PENDIENTE procede y la operacion toma el plazo de ese estado.
    renovada_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE;
    Esperar_704ILR("COTIZACION -> PENDIENTE tras renovar", BLL_Reserva_704ILR.Actualizar_704ILR(renovada_704ILR),
        ReservaResult_704ILR.Success_704ILR);
    Adelanto_704ILR(idCot2_704ILR, 100m);   // RN-07
    var aConfirmar2_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot2_704ILR);
    aConfirmar2_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
    Esperar_704ILR("confirmar tras renovar", BLL_Reserva_704ILR.Actualizar_704ILR(aConfirmar2_704ILR),
        ReservaResult_704ILR.Success_704ILR);
    Esperar_704ILR("limpieza (cancelar la segunda cotizacion)",
        BLL_Reserva_704ILR.Cancelar_704ILR(idCot2_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);

    // Rama PENDIENTE de la RN-01: 72 horas de vigencia desde que entro al estado.
    var pen_704ILR = new EvenTech.BE.BE_Reserva_704ILR
    {
        ClienteId_704ILR = cliRn_704ILR,
        SalonId_704ILR = salRn_704ILR,
        FechaEvento_704ILR = fechaRn_704ILR.AddDays(2),
        Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE,
        CantidadInvitados_704ILR = 50,
        Monto_704ILR = 700m
    };
    Esperar_704ILR("alta pendiente", BLL_Reserva_704ILR.Crear_704ILR(pen_704ILR, out int idPen_704ILR), ReservaResult_704ILR.Success_704ILR);
    Anotar_704ILR(idPen_704ILR);
    var leidaPen_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idPen_704ILR);
    int horasReales_704ILR = leidaPen_704ILR.VenceEl_704ILR.HasValue
        ? (int)Math.Round((leidaPen_704ILR.VenceEl_704ILR.Value - DateTime.Now).TotalHours) : -1;
    Esperar_704ILR("pendiente vence en horas (RN-01: 72 horas)", horasReales_704ILR, 72);

    leidaPen_704ILR.VenceEl_704ILR = DateTime.Now.AddHours(-1);
    EvenTech.DAL.DAL_Reserva_704ILR.Update_704ILR(leidaPen_704ILR);
    var penVencida_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idPen_704ILR);
    penVencida_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
    Esperar_704ILR("confirmar pendiente vencida", BLL_Reserva_704ILR.Actualizar_704ILR(penVencida_704ILR),
        ReservaResult_704ILR.Vencida_704ILR);
    Esperar_704ILR("renovar pendiente", BLL_Reserva_704ILR.Renovar_704ILR(idPen_704ILR), ReservaResult_704ILR.Success_704ILR);
    Adelanto_704ILR(idPen_704ILR, 100m);   // RN-07
    var penRenovada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idPen_704ILR);
    penRenovada_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
    Esperar_704ILR("confirmar tras renovar", BLL_Reserva_704ILR.Actualizar_704ILR(penRenovada_704ILR),
        ReservaResult_704ILR.Success_704ILR);
    Esperar_704ILR("limpieza (cancelar la pendiente)",
        BLL_Reserva_704ILR.Cancelar_704ILR(idPen_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);

    // [29] RN-02 Politica de cancelacion: con 30 dias o mas de antelacion se
    // reintegra todo; con menos se retiene el 50 %. Los literales son los de la
    // Carpeta; la frontera se prueba a 29, 30 y 31 dias. El calculo queda en bitacora.
    Caso_704ILR("[29] RN-02 politica de cancelacion:");
    var conAntelacion_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot_704ILR);
    var metodo_704ILR = BLL_Pago_704ILR.GetMetodos_704ILR().First();
    Esperar_704ILR("cobro adicional sobre la confirmada", BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
    {
        ReservaId_704ILR = idCot_704ILR,
        MetodoPagoId_704ILR = metodo_704ILR.Id_704ILR,
        Monto_704ILR = 400m
    }, out _), PagoResult_704ILR.Success_704ILR);

    // Lo esperado se deriva de lo REALMENTE cobrado sobre la reserva, no de un
    // literal: la operacion pudo recibir otros pagos antes (por ejemplo el adelanto
    // que exige la RN-07 para confirmarla) y un numero fijo daria un falso rojo.
    decimal pagadoRn2_704ILR = BLL_Pago_704ILR.TotalPagado_704ILR(idCot_704ILR);
    BLL_Reserva_704ILR.CalcularCancelacion_704ILR(conAntelacion_704ILR,
        out decimal retLejos_704ILR, out decimal reemLejos_704ILR);
    Console.WriteLine($"  total cobrado: {pagadoRn2_704ILR:0.00}");
    Esperar_704ILR("evento lejano -> retenido", retLejos_704ILR, 0m);
    Esperar_704ILR("evento lejano -> reintegro", reemLejos_704ILR, pagadoRn2_704ILR);

    // Mismo calculo con el evento dentro de la ventana de penalidad (50 %).
    decimal retencionEsperada_704ILR = decimal.Round(pagadoRn2_704ILR * 50m / 100m, 2);
    var cerca_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot_704ILR);
    cerca_704ILR.FechaEvento_704ILR = DateTime.Today.AddDays(5);
    BLL_Reserva_704ILR.CalcularCancelacion_704ILR(cerca_704ILR,
        out decimal retCerca_704ILR, out decimal reemCerca_704ILR);
    Esperar_704ILR("evento cercano -> retenido (50 %)", retCerca_704ILR, retencionEsperada_704ILR);
    Esperar_704ILR("evento cercano -> reintegro", reemCerca_704ILR, pagadoRn2_704ILR - retencionEsperada_704ILR);

    // Frontera de los 30 dias: a 29 se retiene, a 30 y a 31 no.
    foreach (int dias_704ILR in new[] { 29, 30, 31 })
    {
        var enFrontera_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot_704ILR);
        enFrontera_704ILR.FechaEvento_704ILR = DateTime.Today.AddDays(dias_704ILR);
        BLL_Reserva_704ILR.CalcularCancelacion_704ILR(enFrontera_704ILR,
            out decimal retFrontera_704ILR, out decimal reemFrontera_704ILR);
        decimal retEsperada_704ILR = dias_704ILR < 30 ? retencionEsperada_704ILR : 0m;
        Esperar_704ILR($"evento a {dias_704ILR} dias -> retenido", retFrontera_704ILR, retEsperada_704ILR);
        Esperar_704ILR($"evento a {dias_704ILR} dias -> reintegro", reemFrontera_704ILR, pagadoRn2_704ILR - retEsperada_704ILR);
    }

    // Postcondicion de la RN-02: el calculo queda asentado en la bitacora, no solo
    // devuelto por el metodo. Se cuenta el asiento y se lee el importe que dejo.
    int cancelacionesAntes_704ILR = Asientos_704ILR("Reservas");
    var rCan_704ILR = BLL_Reserva_704ILR.Cancelar_704ILR(idCot_704ILR,
        out decimal retFinal_704ILR, out decimal reemFinal_704ILR);
    var cancelada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idCot_704ILR);
    Esperar_704ILR("cancelar", rCan_704ILR, ReservaResult_704ILR.Success_704ILR);
    // El evento seguia lejos en la base (la fecha cercana era solo del calculo).
    Esperar_704ILR("retenido al cancelar", retFinal_704ILR, 0m);
    Esperar_704ILR("reintegro al cancelar", reemFinal_704ILR, pagadoRn2_704ILR);
    Esperar_704ILR("estado final", cancelada_704ILR.Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.CANCELADA);
    Esperar_704ILR("vence tras cancelar", cancelada_704ILR.VenceEl_704ILR.HasValue, false);

    var asientosCan_704ILR = Bitacora_704ILR("Reservas");
    Esperar_704ILR("asientos del modulo Reservas tras la cancelacion", asientosCan_704ILR.Count, cancelacionesAntes_704ILR + 1);
    Esperar_704ILR("el asiento deja el reintegro calculado",
        asientosCan_704ILR.Count > 0 && asientosCan_704ILR[0].Detalle_704ILR.Contains($"reintegro {reemFinal_704ILR:0.00}"), true);
    Esperar_704ILR("criticidad del asiento",
        asientosCan_704ILR.Count > 0 ? asientosCan_704ILR[0].Criticidad_704ILR : default,
        EvenTech.BE.CriticidadBitacora_704ILR.Advertencia);

    Esperar_704ILR("recancelar", BLL_Reserva_704ILR.Cancelar_704ILR(idCot_704ILR, out _, out _),
        ReservaResult_704ILR.NoModificable_704ILR);
}
catch (OperationCanceledException) { /* caso omitido por falta de datos: ya declarado */ }
catch (Exception ex28_704ILR) { Excepcion_704ILR("[28]-[29]", ex28_704ILR); }

// [30] RN-05 Transiciones de estado: el ciclo de vida no es libre. COTIZACION
// avanza a cualquier estado, PENDIENTE solo confirma o cancela, CONFIRMADA solo
// cancela y CANCELADA es terminal. Ademas, entrar a CANCELADA exige pasar por la
// via de cancelacion (la unica que liquida la RN-02).
Caso_704ILR("[30] RN-05 transiciones de estado admitidas:");
try
{
    void Chequear_704ILR(EvenTech.BE.EstadoReserva_704ILR d_704ILR, EvenTech.BE.EstadoReserva_704ILR h_704ILR, bool esperado_704ILR)
        => Esperar_704ILR($"{d_704ILR} -> {h_704ILR}", BLL_Reserva_704ILR.TransicionValida_704ILR(d_704ILR, h_704ILR), esperado_704ILR);

    var COT_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION;
    var PEN_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE;
    var CON_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
    var CAN_704ILR = EvenTech.BE.EstadoReserva_704ILR.CANCELADA;

    Chequear_704ILR(COT_704ILR, PEN_704ILR, true);
    Chequear_704ILR(COT_704ILR, CON_704ILR, true);
    Chequear_704ILR(COT_704ILR, CAN_704ILR, true);
    Chequear_704ILR(PEN_704ILR, CON_704ILR, true);
    Chequear_704ILR(PEN_704ILR, CAN_704ILR, true);
    Chequear_704ILR(PEN_704ILR, COT_704ILR, false);
    Chequear_704ILR(CON_704ILR, CAN_704ILR, true);
    Chequear_704ILR(CON_704ILR, COT_704ILR, false);
    Chequear_704ILR(CON_704ILR, PEN_704ILR, false);
    Chequear_704ILR(CAN_704ILR, COT_704ILR, false);
    Chequear_704ILR(CAN_704ILR, PEN_704ILR, false);
    Chequear_704ILR(CAN_704ILR, CON_704ILR, false);
    Chequear_704ILR(CAN_704ILR, CAN_704ILR, false);
    // Conservar el estado no es una transicion: siempre se admite, salvo en CANCELADA.
    Chequear_704ILR(COT_704ILR, COT_704ILR, true);
    Chequear_704ILR(PEN_704ILR, PEN_704ILR, true);
    Chequear_704ILR(CON_704ILR, CON_704ILR, true);

    // Verificacion end-to-end contra la base: una CONFIRMADA no vuelve atras.
    var cliT_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salT_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (cliT_704ILR.Count > 0 && salT_704ILR.Count > 0)
    {
        var rT_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cliT_704ILR[0].Id_704ILR,
            SalonId_704ILR = salT_704ILR[0].Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(4000 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = 10,
            Monto_704ILR = 800m
        };
        Esperar_704ILR("alta cotizacion", BLL_Reserva_704ILR.Crear_704ILR(rT_704ILR, out int idT_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idT_704ILR);

        Adelanto_704ILR(idT_704ILR, 100m);   // RN-07
        var aConfirmar_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idT_704ILR);
        aConfirmar_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar cotizacion", BLL_Reserva_704ILR.Actualizar_704ILR(aConfirmar_704ILR),
            ReservaResult_704ILR.Success_704ILR);

        var aRetroceder_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idT_704ILR);
        aRetroceder_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE;
        Esperar_704ILR("CONFIRMADA -> PENDIENTE via Actualizar", BLL_Reserva_704ILR.Actualizar_704ILR(aRetroceder_704ILR),
            ReservaResult_704ILR.TransicionInvalida_704ILR);

        var aCancelarMal_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idT_704ILR);
        aCancelarMal_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CANCELADA;
        Esperar_704ILR("cancelar via Actualizar (saltea RN-02)", BLL_Reserva_704ILR.Actualizar_704ILR(aCancelarMal_704ILR),
            ReservaResult_704ILR.TransicionInvalida_704ILR);
        Esperar_704ILR("cancelar por la via correcta", BLL_Reserva_704ILR.Cancelar_704ILR(idT_704ILR, out _, out _),
            ReservaResult_704ILR.Success_704ILR);

        var estadoFinal_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idT_704ILR);
        Esperar_704ILR("estado final", estadoFinal_704ILR.Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.CANCELADA);

        // Restaurar una version tampoco reabre una reserva cancelada (RN-05).
        var versionesT_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idT_704ILR);
        Esperar_704ILR("versiones de la reserva", versionesT_704ILR.Count > 0, true);
        if (versionesT_704ILR.Count > 0)
            Esperar_704ILR("restaurar version sobre cancelada",
                BLL_Reserva_704ILR.RestaurarVersion_704ILR(idT_704ILR, versionesT_704ILR[0].Id_704ILR),
                ReservaResult_704ILR.NoModificable_704ILR);
    }
    else
    {
        Omitir_704ILR("[30]", "faltan clientes/salones seed: la verificacion contra la base no corrio");
    }
}
catch (Exception ex30_704ILR) { Excepcion_704ILR("[30]", ex30_704ILR); }

// [31] RN-06 Capacidad del salon: al confirmar, el salon tiene que poder alojar
// a los invitados estimados. En COTIZACION no se exige (la propuesta se esta armando).
Caso_704ILR("[31] RN-06 capacidad del salon al confirmar:");
try
{
    var cliC_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salC_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (cliC_704ILR.Count == 0 || salC_704ILR.Count == 0)
    {
        Omitir_704ILR("[31]", "faltan clientes/salones seed; corre db/schema.sql");
    }
    else
    {
        // El salon mas chico, para que el exceso sea inequivoco.
        var chico_704ILR = salC_704ILR.OrderBy(s_704ILR => s_704ILR.Capacidad_704ILR).First();
        int exceso_704ILR = chico_704ILR.Capacidad_704ILR + 10;
        Console.WriteLine($"  salon '{chico_704ILR.Nombre_704ILR}' capacidad {chico_704ILR.Capacidad_704ILR}; se piden {exceso_704ILR} invitados");

        var rC_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cliC_704ILR[0].Id_704ILR,
            SalonId_704ILR = chico_704ILR.Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(5000 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = exceso_704ILR,
            Monto_704ILR = 1500m
        };
        var rAltaC_704ILR = BLL_Reserva_704ILR.Crear_704ILR(rC_704ILR, out int idC_704ILR);
        Anotar_704ILR(idC_704ILR);
        Esperar_704ILR("cotizar con exceso de invitados (en COTIZACION no se exige)",
            rAltaC_704ILR, ReservaResult_704ILR.Success_704ILR);

        var persistidaC_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idC_704ILR);
        Esperar_704ILR("invitados persistidos", persistidaC_704ILR.CantidadInvitados_704ILR, exceso_704ILR);

        persistidaC_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar con exceso", BLL_Reserva_704ILR.Actualizar_704ILR(persistidaC_704ILR),
            ReservaResult_704ILR.CapacidadInsuficiente_704ILR);

        // Confirmar sin saber cuanta gente viene tampoco se admite.
        var sinDato_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idC_704ILR);
        sinDato_704ILR.CantidadInvitados_704ILR = 0;
        sinDato_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar sin invitados", BLL_Reserva_704ILR.Actualizar_704ILR(sinDato_704ILR),
            ReservaResult_704ILR.InvalidInvitados_704ILR);

        // Topes de la ficha: la BLL no registra lo que la ficha despues no puede mostrar ni corregir
        // (mas invitados que el maximo del campo, o una fecha posterior al ultimo dia del selector).
        var sobreTope_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cliC_704ILR[0].Id_704ILR,
            SalonId_704ILR = chico_704ILR.Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(5000 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = BLL_Reserva_704ILR.InvitadosMaximo_704ILR + 1,
            Monto_704ILR = 1500m
        };
        Esperar_704ILR("cotizar con mas invitados que el maximo del campo",
            BLL_Reserva_704ILR.Crear_704ILR(sobreTope_704ILR, out _), ReservaResult_704ILR.InvalidInvitados_704ILR);
        sobreTope_704ILR.CantidadInvitados_704ILR = exceso_704ILR;
        sobreTope_704ILR.FechaEvento_704ILR = new DateTime(9998, 12, 31, 12, 0, 0);
        Esperar_704ILR("cotizar con fecha posterior al ultimo dia del selector",
            BLL_Reserva_704ILR.Crear_704ILR(sobreTope_704ILR, out _), ReservaResult_704ILR.InvalidFecha_704ILR);

        Adelanto_704ILR(idC_704ILR, 100m);   // RN-07
        var ajustada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idC_704ILR);
        ajustada_704ILR.CantidadInvitados_704ILR = chico_704ILR.Capacidad_704ILR;
        ajustada_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar ajustando a la capacidad", BLL_Reserva_704ILR.Actualizar_704ILR(ajustada_704ILR),
            ReservaResult_704ILR.Success_704ILR);

        // El control de cambios registra la correccion de invitados campo por campo.
        var histC_704ILR = RegistradorDeCambios_704ILR.GetHistorial_704ILR("Reserva", idC_704ILR);
        int cambiosInv_704ILR = histC_704ILR.Count(h_704ILR => h_704ILR.NombreCampo_704ILR == "CantidadInvitados");
        Esperar_704ILR("cambios de CantidadInvitados en el historial", cambiosInv_704ILR, 1);

        // El memento conserva la cantidad de invitados de cada version.
        var versionesC_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idC_704ILR);
        Esperar_704ILR("versiones guardadas", versionesC_704ILR.Count > 0, true);
        if (versionesC_704ILR.Count > 0)
        {
            var vC_704ILR = CaretakerReserva_704ILR.GetVersion_704ILR(versionesC_704ILR[versionesC_704ILR.Count - 1].Id_704ILR);
            Esperar_704ILR("invitados en la version mas antigua", vC_704ILR.CantidadInvitados_704ILR, exceso_704ILR);
        }

        // Limpieza: la reserva confirmada del caso se cancela para no bloquear el
        // salon en la proxima corrida.
        Esperar_704ILR("limpieza (cancelar la reserva de [31])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idC_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex31_704ILR) { Excepcion_704ILR("[31]", ex31_704ILR); }

// [32] RN-07 Adelanto para confirmar. Lo que distingue a una reserva CONFIRMADA de
// una PENDIENTE es que el cliente ya puso dinero: sin adelanto registrado no se
// confirma, y como el cobro necesita la reserva ya guardada, tampoco se puede nacer
// CONFIRMADA. El orden del proceso es siempre guardar -> cobrar -> confirmar.
Caso_704ILR("[32] RN-07 Adelanto para confirmar:");
try
{
    var cliA_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salA_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (cliA_704ILR.Count == 0 || salA_704ILR.Count == 0)
    {
        Omitir_704ILR("[32]", "faltan clientes/salones seed; corre db/schema.sql");
    }
    else
    {
        var salA0_704ILR = salA_704ILR[0];

        // Alta directa en CONFIRMADA: rechazada, no hay reserva a la que imputar el cobro.
        var altaDirecta_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cliA_704ILR[0].Id_704ILR,
            SalonId_704ILR = salA0_704ILR.Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(5500 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA,
            CantidadInvitados_704ILR = 20,
            Monto_704ILR = 900m
        };
        Esperar_704ILR("alta directa CONFIRMADA", BLL_Reserva_704ILR.Crear_704ILR(altaDirecta_704ILR, out _),
            ReservaResult_704ILR.SinAdelanto_704ILR);

        // Alta normal en COTIZACION y confirmacion sin ningun pago: rechazada.
        var cotA_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cliA_704ILR[0].Id_704ILR,
            SalonId_704ILR = salA0_704ILR.Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(5500 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = 20,
            Monto_704ILR = 900m
        };
        var rAltaA_704ILR = BLL_Reserva_704ILR.Crear_704ILR(cotA_704ILR, out int idA_704ILR);
        Anotar_704ILR(idA_704ILR);
        Esperar_704ILR("alta cotizacion", rAltaA_704ILR, ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("adelanto registrado", BLL_Reserva_704ILR.TieneAdelanto_704ILR(idA_704ILR), false);

        var sinPago_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idA_704ILR);
        sinPago_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar sin adelanto", BLL_Reserva_704ILR.Actualizar_704ILR(sinPago_704ILR),
            ReservaResult_704ILR.SinAdelanto_704ILR);
        Esperar_704ILR("estado tras el rechazo", BLL_Reserva_704ILR.GetById_704ILR(idA_704ILR).Estado_704ILR,
            EvenTech.BE.EstadoReserva_704ILR.COTIZACION);

        // Con el adelanto cobrado, la misma confirmacion procede.
        Adelanto_704ILR(idA_704ILR, 300m);
        Esperar_704ILR("adelanto registrado", BLL_Reserva_704ILR.TieneAdelanto_704ILR(idA_704ILR), true);
        var conPago_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idA_704ILR);
        conPago_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar con adelanto", BLL_Reserva_704ILR.Actualizar_704ILR(conPago_704ILR),
            ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("estado final", BLL_Reserva_704ILR.GetById_704ILR(idA_704ILR).Estado_704ILR,
            EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA);

        // Una PENDIENTE sin cobros tampoco confirma.
        var penA_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cliA_704ILR[0].Id_704ILR,
            SalonId_704ILR = salA0_704ILR.Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(5600 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE,
            CantidadInvitados_704ILR = 20,
            Monto_704ILR = 900m
        };
        Esperar_704ILR("alta pendiente", BLL_Reserva_704ILR.Crear_704ILR(penA_704ILR, out int idPenA_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idPenA_704ILR);
        var penSinPago_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idPenA_704ILR);
        penSinPago_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar pendiente sin adelanto", BLL_Reserva_704ILR.Actualizar_704ILR(penSinPago_704ILR),
            ReservaResult_704ILR.SinAdelanto_704ILR);

        // Limpieza: se liberan los dos salones tomados por el caso.
        Esperar_704ILR("limpieza (cancelar la confirmada de [32])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idA_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("limpieza (cancelar la pendiente de [32])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idPenA_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex32_704ILR) { Excepcion_704ILR("[32]", ex32_704ILR); }

// [33] Anulacion de pago: pasa por las mismas reglas que el registro. Antes esto
// borraba la fila sin mirar si el pago existia, si era de esa reserva o si la
// reserva admitia movimientos.
Caso_704ILR("[33] Cobro y anulacion de pago con reglas (CUN004):");
try
{
    var cliP_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salP_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    var metP_704ILR = BLL_Pago_704ILR.GetMetodos_704ILR();
    if (cliP_704ILR.Count == 0 || salP_704ILR.Count == 0 || metP_704ILR.Count == 0)
    {
        Omitir_704ILR("[33]", "faltan clientes/salones/metodos de pago seed; corre db/schema.sql");
    }
    else
    {
        var rP_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = cliP_704ILR[0].Id_704ILR,
            SalonId_704ILR = salP_704ILR[0].Id_704ILR,
            FechaEvento_704ILR = DateTime.Today.AddDays(5700 + desfasaje_704ILR),
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = 15,
            Monto_704ILR = 600m
        };
        Esperar_704ILR("alta cotizacion", BLL_Reserva_704ILR.Crear_704ILR(rP_704ILR, out int idP_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idP_704ILR);

        Esperar_704ILR("cobro valido", BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idP_704ILR, MetodoPagoId_704ILR = metP_704ILR[0].Id_704ILR, Monto_704ILR = 200m }, out int idPago_704ILR),
            PagoResult_704ILR.Success_704ILR);
        Esperar_704ILR("pagado", BLL_Pago_704ILR.TotalPagado_704ILR(idP_704ILR), 200m);

        // Paso 4 del CUN004: el importe tiene que ser positivo y el metodo, valido.
        Esperar_704ILR("cobro por cero", BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idP_704ILR, MetodoPagoId_704ILR = metP_704ILR[0].Id_704ILR, Monto_704ILR = 0m }, out _),
            PagoResult_704ILR.MontoInvalido_704ILR);
        Esperar_704ILR("cobro por importe negativo", BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idP_704ILR, MetodoPagoId_704ILR = metP_704ILR[0].Id_704ILR, Monto_704ILR = -50m }, out _),
            PagoResult_704ILR.MontoInvalido_704ILR);
        Esperar_704ILR("cobro sin metodo de pago", BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idP_704ILR, MetodoPagoId_704ILR = 0, Monto_704ILR = 100m }, out _),
            PagoResult_704ILR.MetodoInvalido_704ILR);

        // Flujo 5.1 del CUN004: la anulacion de un pago propio de una reserva viva
        // procede y queda asentada con criticidad Advertencia. Antes el caso solo
        // ejercitaba los rechazos: la anulacion exitosa nunca llegaba a correr.
        int anulAntes_704ILR = Asientos_704ILR("Pagos");
        Esperar_704ILR("anular el pago registrado", BLL_Pago_704ILR.Eliminar_704ILR(idPago_704ILR, idP_704ILR),
            PagoResult_704ILR.Success_704ILR);
        Esperar_704ILR("total tras la anulacion", BLL_Pago_704ILR.TotalPagado_704ILR(idP_704ILR), 0m);
        var asientosAnul_704ILR = Bitacora_704ILR("Pagos");
        Esperar_704ILR("asientos del modulo Pagos tras la anulacion", asientosAnul_704ILR.Count, anulAntes_704ILR + 1);
        Esperar_704ILR("criticidad del asiento",
            asientosAnul_704ILR.Count > 0 ? asientosAnul_704ILR[0].Criticidad_704ILR : default,
            EvenTech.BE.CriticidadBitacora_704ILR.Advertencia);

        // Se vuelve a cobrar para probar los rechazos sobre un pago que existe.
        Esperar_704ILR("recobro tras la anulacion", BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idP_704ILR, MetodoPagoId_704ILR = metP_704ILR[0].Id_704ILR, Monto_704ILR = 200m }, out int idPago2_704ILR),
            PagoResult_704ILR.Success_704ILR);

        // Un pago inexistente no se anula.
        Esperar_704ILR("anular pago inexistente", BLL_Pago_704ILR.Eliminar_704ILR(999999, idP_704ILR),
            PagoResult_704ILR.PagoInvalido_704ILR);

        // Un pago que existe pero es de otra reserva, tampoco.
        Esperar_704ILR("anular pago ajeno a la reserva", BLL_Pago_704ILR.Eliminar_704ILR(idPago2_704ILR, idP_704ILR + 100000),
            PagoResult_704ILR.PagoInvalido_704ILR);

        // Sobre una reserva cancelada no se admiten movimientos de cobro (RN-04) y
        // cada rechazo queda asentado (flujo 4.2 del CUN004).
        Esperar_704ILR("cancelar la reserva", BLL_Reserva_704ILR.Cancelar_704ILR(idP_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
        int anulRechazadasAntes_704ILR = Asientos_704ILR("Pagos", "Anulacion rechazada");
        Esperar_704ILR("anular sobre reserva cancelada", BLL_Pago_704ILR.Eliminar_704ILR(idPago2_704ILR, idP_704ILR),
            PagoResult_704ILR.ReservaCancelada_704ILR);
        Esperar_704ILR("asiento 'Anulacion rechazada' tras el intento",
            Asientos_704ILR("Pagos", "Anulacion rechazada"), anulRechazadasAntes_704ILR + 1);
        int pagosRechazadosAntes_704ILR = Asientos_704ILR("Pagos", "Pago rechazado");
        Esperar_704ILR("cobrar sobre reserva cancelada", BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
        { ReservaId_704ILR = idP_704ILR, MetodoPagoId_704ILR = metP_704ILR[0].Id_704ILR, Monto_704ILR = 50m }, out _),
            PagoResult_704ILR.ReservaCancelada_704ILR);
        var pagosRechazados_704ILR = Bitacora_704ILR("Pagos", "Pago rechazado");
        Esperar_704ILR("asiento 'Pago rechazado' tras el intento", pagosRechazados_704ILR.Count, pagosRechazadosAntes_704ILR + 1);
        Esperar_704ILR("el asiento nombra la reserva cancelada",
            pagosRechazados_704ILR.Count > 0 && pagosRechazados_704ILR[0].Detalle_704ILR.Contains($"#{idP_704ILR}"), true);
        Esperar_704ILR("el pago sigue registrado", BLL_Pago_704ILR.TotalPagado_704ILR(idP_704ILR), 200m);
    }
}
catch (Exception ex33_704ILR) { Excepcion_704ILR("[33]", ex33_704ILR); }

// [34] RN-04 bajo concurrencia: dos cobros simultaneos sobre la misma reserva
// no pueden superar el total entre los dos. El registro valida y escribe en una
// sola transaccion con la cabecera bloqueada, asi el segundo cobro juzga contra
// lo que el primero ya dejo. Se lanzan dos cobros de 700 sobre un total de 1000
// arrancando a la vez: exactamente uno entra y el otro se rechaza por tope.
Caso_704ILR("[34] Cobros simultaneos sobre la misma reserva (RN-04):");
try
{
    var cliK_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salK_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    var metK_704ILR = BLL_Pago_704ILR.GetMetodos_704ILR();
    if (cliK_704ILR.Count == 0 || salK_704ILR.Count == 0 || metK_704ILR.Count == 0)
    {
        Omitir_704ILR("[34]", "faltan clientes/salones/metodos de pago seed; corre db/schema.sql");
    }
    else
    {
        int metodoK_704ILR = metK_704ILR[0].Id_704ILR;
        Esperar_704ILR("alta pendiente de 1000",
            BLL_Reserva_704ILR.Crear_704ILR(NuevaReserva_704ILR(cliK_704ILR[0].Id_704ILR, salK_704ILR[0].Id_704ILR, 6100,
                EvenTech.BE.EstadoReserva_704ILR.PENDIENTE, 1000m), out int idK_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idK_704ILR);

        // Los dos hilos esperan en la barrera y salen juntos, para que el solape
        // sea real y no dependa de la planificacion.
        var resultados_704ILR = new PagoResult_704ILR[2];
        var idsPago_704ILR = new int[2];
        Exception errorHilo_704ILR = null;
        int rechazosAntes_704ILR = Asientos_704ILR("Pagos", "Pago rechazado");
        using (var barrera_704ILR = new Barrier(2))
        {
            var tareas_704ILR = Enumerable.Range(0, 2).Select(i_704ILR => Task.Run(() =>
            {
                try
                {
                    barrera_704ILR.SignalAndWait();
                    resultados_704ILR[i_704ILR] = BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
                    { ReservaId_704ILR = idK_704ILR, MetodoPagoId_704ILR = metodoK_704ILR, Monto_704ILR = 700m, Observacion_704ILR = "Cobro " + i_704ILR },
                        out idsPago_704ILR[i_704ILR]);
                }
                catch (Exception ex_704ILR) { errorHilo_704ILR = ex_704ILR; }
            })).ToArray();
            Task.WaitAll(tareas_704ILR);
        }
        Console.WriteLine($"  resultados: {resultados_704ILR[0]} / {resultados_704ILR[1]}");
        Esperar_704ILR("los dos cobros terminaron sin excepcion", errorHilo_704ILR == null ? "sin excepcion" : errorHilo_704ILR.GetType().Name + ": " + errorHilo_704ILR.Message, "sin excepcion");
        Esperar_704ILR("cobros aceptados", resultados_704ILR.Count(r_704ILR => r_704ILR == PagoResult_704ILR.Success_704ILR), 1);
        Esperar_704ILR("cobros rechazados por tope", resultados_704ILR.Count(r_704ILR => r_704ILR == PagoResult_704ILR.ExcedeSaldo_704ILR), 1);
        Esperar_704ILR("total cobrado", BLL_Pago_704ILR.TotalPagado_704ILR(idK_704ILR), 700m);
        Esperar_704ILR("pagos registrados", BLL_Pago_704ILR.GetByReserva_704ILR(idK_704ILR).Count, 1);
        Esperar_704ILR("el rechazo por tope quedo asentado", Asientos_704ILR("Pagos", "Pago rechazado"), rechazosAntes_704ILR + 1);

        // Anulacion y cobro a la vez: la anulacion siempre procede y el cobro
        // entra o se rechaza segun quien tome primero la cabecera; lo que no puede
        // pasar es que el total quede en 1400 (los dos vivos) ni que se pierda una
        // fila. Se admite cualquiera de los dos ordenes y se verifica el invariante.
        int idPrimero_704ILR = idsPago_704ILR.First(id_704ILR => id_704ILR > 0);
        PagoResult_704ILR rAnula_704ILR = default, rCobra_704ILR = default;
        int idSegundo_704ILR = 0;
        errorHilo_704ILR = null;
        using (var barrera2_704ILR = new Barrier(2))
        {
            var tAnula_704ILR = Task.Run(() =>
            {
                try { barrera2_704ILR.SignalAndWait(); rAnula_704ILR = BLL_Pago_704ILR.Eliminar_704ILR(idPrimero_704ILR, idK_704ILR); }
                catch (Exception ex_704ILR) { errorHilo_704ILR = ex_704ILR; }
            });
            var tCobra_704ILR = Task.Run(() =>
            {
                try
                {
                    barrera2_704ILR.SignalAndWait();
                    rCobra_704ILR = BLL_Pago_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Pago_704ILR
                    { ReservaId_704ILR = idK_704ILR, MetodoPagoId_704ILR = metodoK_704ILR, Monto_704ILR = 700m, Observacion_704ILR = "Cobro simultaneo" },
                        out idSegundo_704ILR);
                }
                catch (Exception ex_704ILR) { errorHilo_704ILR = ex_704ILR; }
            });
            Task.WaitAll(tAnula_704ILR, tCobra_704ILR);
        }
        Console.WriteLine($"  anulacion: {rAnula_704ILR} / cobro simultaneo: {rCobra_704ILR}");
        Esperar_704ILR("anulacion y cobro terminaron sin excepcion", errorHilo_704ILR == null ? "sin excepcion" : errorHilo_704ILR.GetType().Name + ": " + errorHilo_704ILR.Message, "sin excepcion");
        Esperar_704ILR("la anulacion procede", rAnula_704ILR, PagoResult_704ILR.Success_704ILR);
        Esperar_704ILR("el cobro simultaneo entra o se rechaza por tope",
            rCobra_704ILR == PagoResult_704ILR.Success_704ILR || rCobra_704ILR == PagoResult_704ILR.ExcedeSaldo_704ILR, true);
        decimal totalEsperado_704ILR = rCobra_704ILR == PagoResult_704ILR.Success_704ILR ? 700m : 0m;
        Esperar_704ILR("total cobrado coherente con el orden en que entraron", BLL_Pago_704ILR.TotalPagado_704ILR(idK_704ILR), totalEsperado_704ILR);
        Esperar_704ILR("pagos vivos", BLL_Pago_704ILR.GetByReserva_704ILR(idK_704ILR).Count, totalEsperado_704ILR == 0m ? 0 : 1);
        Esperar_704ILR("el total nunca supera la reserva", BLL_Pago_704ILR.TotalPagado_704ILR(idK_704ILR) <= 1000m, true);

        // Limpieza: se anula lo que haya quedado y se cancela la reserva. La
        // verificacion corre siempre (la cantidad de verificaciones de la corrida no
        // depende de quien gano la carrera).
        PagoResult_704ILR rLimpia_704ILR = idSegundo_704ILR > 0
            ? BLL_Pago_704ILR.Eliminar_704ILR(idSegundo_704ILR, idK_704ILR)
            : PagoResult_704ILR.Success_704ILR;
        Esperar_704ILR("limpieza (anular el cobro simultaneo, si entro)", rLimpia_704ILR, PagoResult_704ILR.Success_704ILR);
        Esperar_704ILR("limpieza (cancelar la reserva de [34])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idK_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex34_704ILR) { Excepcion_704ILR("[34]", ex34_704ILR); }

// [35] RN-01 y RN-07 al restaurar una version. Restaurar esta exceptuada de la
// tabla de transiciones pero no del plazo ni del adelanto: reponer una version de
// OTRO estado sobre una operacion vencida se rechaza hasta renovarla (si no, la
// restauracion daria plazo nuevo sin asiento), reponer la MISMA version conserva
// el vencimiento, y llegar a CONFIRMADA restaurando exige el adelanto cobrado.
Caso_704ILR("[35] Vigencia y adelanto al restaurar una version (RN-01/RN-07):");
try
{
    var cliV_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salV_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (cliV_704ILR.Count == 0 || salV_704ILR.Count == 0)
    {
        Omitir_704ILR("[35]", "faltan clientes/salones seed; corre db/schema.sql");
    }
    else
    {
        // Cotizacion -> pendiente (queda versionada la COTIZACION) -> se fuerza el
        // vencimiento por la DAL, como en [28].
        Esperar_704ILR("alta cotizacion",
            BLL_Reserva_704ILR.Crear_704ILR(NuevaReserva_704ILR(cliV_704ILR[0].Id_704ILR, salV_704ILR[0].Id_704ILR, 6200,
                EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 500m), out int idV_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idV_704ILR);
        var aPendiente_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idV_704ILR);
        aPendiente_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.PENDIENTE;
        Esperar_704ILR("COTIZACION -> PENDIENTE", BLL_Reserva_704ILR.Actualizar_704ILR(aPendiente_704ILR), ReservaResult_704ILR.Success_704ILR);
        var versionesV_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idV_704ILR);
        Esperar_704ILR("version COTIZACION guardada",
            versionesV_704ILR.Count == 1 && versionesV_704ILR[0].Estado_704ILR == EvenTech.BE.EstadoReserva_704ILR.COTIZACION, true);
        int mementoCot_704ILR = versionesV_704ILR.Count > 0 ? versionesV_704ILR[0].Id_704ILR : 0;

        var vencer_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idV_704ILR);
        vencer_704ILR.VenceEl_704ILR = DateTime.Now.AddHours(-1);
        EvenTech.DAL.DAL_Reserva_704ILR.Update_704ILR(vencer_704ILR);
        Esperar_704ILR("pendiente vencida (forzada)", BLL_Reserva_704ILR.GetById_704ILR(idV_704ILR).EstaVencida_704ILR, true);

        Esperar_704ILR("restaurar la version COTIZACION sobre la pendiente vencida",
            BLL_Reserva_704ILR.RestaurarVersion_704ILR(idV_704ILR, mementoCot_704ILR), ReservaResult_704ILR.Vencida_704ILR);
        Esperar_704ILR("sigue PENDIENTE", BLL_Reserva_704ILR.GetById_704ILR(idV_704ILR).Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.PENDIENTE);
        Esperar_704ILR("versiones sin cambio", CaretakerReserva_704ILR.GetVersiones_704ILR(idV_704ILR).Count, 1);

        Esperar_704ILR("renovar la pendiente", BLL_Reserva_704ILR.Renovar_704ILR(idV_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("restaurar la version COTIZACION tras renovar",
            BLL_Reserva_704ILR.RestaurarVersion_704ILR(idV_704ILR, mementoCot_704ILR), ReservaResult_704ILR.Success_704ILR);
        var repuesta_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idV_704ILR);
        Esperar_704ILR("estado repuesto", repuesta_704ILR.Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.COTIZACION);
        Esperar_704ILR("la cotizacion repuesta tiene plazo", repuesta_704ILR.VenceEl_704ILR.HasValue, true);
        Esperar_704ILR("la cotizacion repuesta no esta vencida", repuesta_704ILR.EstaVencida_704ILR, false);
        Esperar_704ILR("versiones tras restaurar", CaretakerReserva_704ILR.GetVersiones_704ILR(idV_704ILR).Count, 2);

        // Restaurar la MISMA version/estado sobre una vencida si procede: no cambia
        // de estado, asi que conserva el vencimiento que tenia (sigue vencida).
        var vencer2_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idV_704ILR);
        vencer2_704ILR.VenceEl_704ILR = DateTime.Now.AddHours(-1);
        EvenTech.DAL.DAL_Reserva_704ILR.Update_704ILR(vencer2_704ILR);
        Esperar_704ILR("restaurar la version COTIZACION sobre la cotizacion vencida (mismo estado)",
            BLL_Reserva_704ILR.RestaurarVersion_704ILR(idV_704ILR, mementoCot_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("conserva el vencimiento (sigue vencida)", BLL_Reserva_704ILR.GetById_704ILR(idV_704ILR).EstaVencida_704ILR, true);
        Esperar_704ILR("limpieza (cancelar la reserva de vigencia)",
            BLL_Reserva_704ILR.Cancelar_704ILR(idV_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);

        // RN-07 al restaurar: se confirma con adelanto (queda versionada la
        // COTIZACION), se vuelve a la cotizacion (queda versionada la CONFIRMADA),
        // se anula el adelanto y la version CONFIRMADA ya no se puede reponer.
        Esperar_704ILR("alta de la segunda cotizacion",
            BLL_Reserva_704ILR.Crear_704ILR(NuevaReserva_704ILR(cliV_704ILR[0].Id_704ILR, salV_704ILR[0].Id_704ILR, 6300,
                EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 500m), out int idW_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idW_704ILR);
        int idAdelantoW_704ILR = Adelanto_704ILR(idW_704ILR, 100m);
        var aConfirmarW_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idW_704ILR);
        aConfirmarW_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar con adelanto", BLL_Reserva_704ILR.Actualizar_704ILR(aConfirmarW_704ILR), ReservaResult_704ILR.Success_704ILR);
        var versionesW_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idW_704ILR);
        int mementoCotW_704ILR = versionesW_704ILR.Count > 0 ? versionesW_704ILR[0].Id_704ILR : 0;
        Esperar_704ILR("volver a la version COTIZACION",
            BLL_Reserva_704ILR.RestaurarVersion_704ILR(idW_704ILR, mementoCotW_704ILR), ReservaResult_704ILR.Success_704ILR);
        var versionesW2_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idW_704ILR);
        Esperar_704ILR("la version CONFIRMADA quedo guardada",
            versionesW2_704ILR.Count == 2 && versionesW2_704ILR[0].Estado_704ILR == EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA, true);
        int mementoConfW_704ILR = versionesW2_704ILR.Count > 0 ? versionesW2_704ILR[0].Id_704ILR : 0;

        Esperar_704ILR("anular el adelanto", BLL_Pago_704ILR.Eliminar_704ILR(idAdelantoW_704ILR, idW_704ILR), PagoResult_704ILR.Success_704ILR);
        Esperar_704ILR("sin adelanto", BLL_Reserva_704ILR.TieneAdelanto_704ILR(idW_704ILR), false);
        Esperar_704ILR("restaurar la version CONFIRMADA sin adelanto",
            BLL_Reserva_704ILR.RestaurarVersion_704ILR(idW_704ILR, mementoConfW_704ILR), ReservaResult_704ILR.SinAdelanto_704ILR);
        Esperar_704ILR("sigue en COTIZACION", BLL_Reserva_704ILR.GetById_704ILR(idW_704ILR).Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.COTIZACION);
        Esperar_704ILR("limpieza (cancelar la reserva de adelanto)",
            BLL_Reserva_704ILR.Cancelar_704ILR(idW_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex35_704ILR) { Excepcion_704ILR("[35]", ex35_704ILR); }

// [36] El monto de la operacion ES la suma de sus lineas: cuando viajan los
// servicios, la capa de negocio valida cada linea y fija el total desde ellas,
// sin confiar en el que armo la pantalla. Una linea con cantidad cero o precio
// negativo no describe un servicio contratado y se rechaza entera, sin insertar.
Caso_704ILR("[36] Monto normalizado y lineas de servicio invalidas:");
try
{
    var cliN_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salN_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    var srvN_704ILR = BLL_Servicio_704ILR.GetActivos_704ILR();
    if (cliN_704ILR.Count == 0 || salN_704ILR.Count == 0 || srvN_704ILR.Count == 0)
    {
        Omitir_704ILR("[36]", "faltan clientes/salones/servicios seed; corre db/schema.sql");
    }
    else
    {
        EvenTech.BE.BE_ReservaServicio_704ILR Linea_704ILR(int cantidad_704ILR, decimal precio_704ILR) =>
            new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = srvN_704ILR[0].Id_704ILR, Cantidad_704ILR = cantidad_704ILR, PrecioUnitario_704ILR = precio_704ILR };
        decimal precio_704ILR = srvN_704ILR[0].Precio_704ILR;

        int reservasAntes_704ILR = BLL_Reserva_704ILR.GetAll_704ILR().Count;
        var conLinea_704ILR = NuevaReserva_704ILR(cliN_704ILR[0].Id_704ILR, salN_704ILR[0].Id_704ILR, 6400,
            EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 999m);
        Esperar_704ILR("alta con una linea y monto 999 desde la pantalla",
            BLL_Reserva_704ILR.Crear_704ILR(conLinea_704ILR, new List<EvenTech.BE.BE_ReservaServicio_704ILR> { Linea_704ILR(1, precio_704ILR) }, out int idN_704ILR),
            ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idN_704ILR);
        Esperar_704ILR("monto persistido = precio de la linea", BLL_Reserva_704ILR.GetById_704ILR(idN_704ILR).Monto_704ILR, precio_704ILR);

        var cantidadCero_704ILR = NuevaReserva_704ILR(cliN_704ILR[0].Id_704ILR, salN_704ILR[0].Id_704ILR, 6401,
            EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 100m);
        Esperar_704ILR("alta con una linea de cantidad 0",
            BLL_Reserva_704ILR.Crear_704ILR(cantidadCero_704ILR, new List<EvenTech.BE.BE_ReservaServicio_704ILR> { Linea_704ILR(0, precio_704ILR) }, out int idCero_704ILR),
            ReservaResult_704ILR.InvalidMonto_704ILR);
        Esperar_704ILR("no se asigno id", idCero_704ILR, 0);

        var precioNegativo_704ILR = NuevaReserva_704ILR(cliN_704ILR[0].Id_704ILR, salN_704ILR[0].Id_704ILR, 6402,
            EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 100m);
        Esperar_704ILR("alta con una linea de precio -1",
            BLL_Reserva_704ILR.Crear_704ILR(precioNegativo_704ILR, new List<EvenTech.BE.BE_ReservaServicio_704ILR> { Linea_704ILR(1, -1m) }, out int idNeg_704ILR),
            ReservaResult_704ILR.InvalidMonto_704ILR);
        Esperar_704ILR("no se asigno id", idNeg_704ILR, 0);
        Esperar_704ILR("reservas tras los rechazos", BLL_Reserva_704ILR.GetAll_704ILR().Count, reservasAntes_704ILR + 1);

        Esperar_704ILR("modificar con una linea de cantidad 0",
            BLL_Reserva_704ILR.Actualizar_704ILR(BLL_Reserva_704ILR.GetById_704ILR(idN_704ILR), new List<EvenTech.BE.BE_ReservaServicio_704ILR> { Linea_704ILR(0, precio_704ILR) }),
            ReservaResult_704ILR.InvalidMonto_704ILR);
        Esperar_704ILR("la linea original sigue", BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idN_704ILR).Count, 1);

        string excepcion_704ILR = "ninguna";
        try { BLL_ReservaServicio_704ILR.Guardar_704ILR(idN_704ILR, new List<EvenTech.BE.BE_ReservaServicio_704ILR> { Linea_704ILR(0, precio_704ILR) }); }
        catch (ArgumentException) { excepcion_704ILR = "ArgumentException"; }
        Esperar_704ILR("guardar lineas invalidas por la capa de servicios", excepcion_704ILR, "ArgumentException");
        Esperar_704ILR("la linea original sigue", BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idN_704ILR).Count, 1);

        Esperar_704ILR("limpieza (cancelar la reserva de [36])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idN_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex36_704ILR) { Excepcion_704ILR("[36]", ex36_704ILR); }

// [37] RN-05 con un estado fuera del ciclo de vida: un valor que no figura en el
// enum (posible por casteo desde un entero) no pasa las guardas por estado ni la
// tabla de transiciones, y no se persiste. Deja asiento: no hay pantalla que lo
// produzca, asi que no es un error de tipeo.
Caso_704ILR("[37] Estado fuera del ciclo de vida (RN-05):");
try
{
    var cliE_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salE_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (cliE_704ILR.Count == 0 || salE_704ILR.Count == 0)
    {
        Omitir_704ILR("[37]", "faltan clientes/salones seed; corre db/schema.sql");
    }
    else
    {
        var estadoRaro_704ILR = (EvenTech.BE.EstadoReserva_704ILR)99;
        int altasRechazadasAntes_704ILR = Asientos_704ILR("Reservas", "Alta rechazada");
        Esperar_704ILR("alta con estado 99",
            BLL_Reserva_704ILR.Crear_704ILR(NuevaReserva_704ILR(cliE_704ILR[0].Id_704ILR, salE_704ILR[0].Id_704ILR, 6500, estadoRaro_704ILR, 100m), out int idRaro_704ILR),
            ReservaResult_704ILR.TransicionInvalida_704ILR);
        Esperar_704ILR("no se asigno id", idRaro_704ILR, 0);
        var altasRechazadas_704ILR = Bitacora_704ILR("Reservas", "Alta rechazada");
        Esperar_704ILR("asiento 'Alta rechazada'", altasRechazadas_704ILR.Count, altasRechazadasAntes_704ILR + 1);
        Esperar_704ILR("el asiento explica el motivo",
            altasRechazadas_704ILR.Count > 0 && altasRechazadas_704ILR[0].Detalle_704ILR.Contains("fuera del ciclo de vida"), true);

        Esperar_704ILR("alta cotizacion",
            BLL_Reserva_704ILR.Crear_704ILR(NuevaReserva_704ILR(cliE_704ILR[0].Id_704ILR, salE_704ILR[0].Id_704ILR, 6500,
                EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 100m), out int idE_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idE_704ILR);
        int modRechazadasAntes_704ILR = Asientos_704ILR("Reservas", "Modificacion rechazada");
        var aRaro_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idE_704ILR);
        aRaro_704ILR.Estado_704ILR = estadoRaro_704ILR;
        Esperar_704ILR("modificar hacia el estado 99", BLL_Reserva_704ILR.Actualizar_704ILR(aRaro_704ILR), ReservaResult_704ILR.TransicionInvalida_704ILR);
        Esperar_704ILR("el estado persistido sigue COTIZACION",
            BLL_Reserva_704ILR.GetById_704ILR(idE_704ILR).Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.COTIZACION);
        var modRechazadas_704ILR = Bitacora_704ILR("Reservas", "Modificacion rechazada");
        Esperar_704ILR("asiento 'Modificacion rechazada'", modRechazadas_704ILR.Count, modRechazadasAntes_704ILR + 1);
        Esperar_704ILR("el asiento explica el motivo",
            modRechazadas_704ILR.Count > 0 && modRechazadas_704ILR[0].Detalle_704ILR.Contains("fuera del ciclo de vida"), true);
        Esperar_704ILR("limpieza (cancelar la reserva de [37])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idE_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex37_704ILR) { Excepcion_704ILR("[37]", ex37_704ILR); }

// [38] Un dato "ENC:" que no es un paquete AES valido (prefijo suelto, base64
// invalido, largo que no es multiplo del bloque) no puede voltear la lectura de
// un listado: se devuelve tal cual quedo almacenado, sin lanzar. El roundtrip
// normal sigue funcionando. Sin base.
Caso_704ILR("[38] Dato cifrado malformado no rompe la lectura:");
try
{
    string[] malformados_704ILR =
    {
        "ENC:",
        "ENC:AA==",
        "ENC:" + Convert.ToBase64String(new byte[15]),
        "ENC:" + Convert.ToBase64String(new byte[16]),
        "ENC:" + Convert.ToBase64String(new byte[20]),
        "ENC:noesbase64!!"
    };
    foreach (string s_704ILR in malformados_704ILR)
    {
        string devuelto_704ILR;
        try { devuelto_704ILR = CryptoService_704ILR.Desproteger_704ILR(s_704ILR); }
        catch (Exception ex_704ILR) { devuelto_704ILR = "EXCEPCION " + ex_704ILR.GetType().Name; }
        Esperar_704ILR($"'{s_704ILR}' devuelve lo almacenado", devuelto_704ILR, s_704ILR);
    }
    Esperar_704ILR("roundtrip (proteger -> desproteger)",
        CryptoService_704ILR.Desproteger_704ILR(CryptoService_704ILR.Proteger_704ILR("juan@mail.com")), "juan@mail.com");
    Esperar_704ILR("un texto plano se devuelve tal cual", CryptoService_704ILR.Desproteger_704ILR("sin prefijo"), "sin prefijo");
}
catch (Exception ex38_704ILR) { Excepcion_704ILR("[38]", ex38_704ILR); }

// [39] Un texto editado solo puede usar los marcadores {n} del texto de fabrica
// de la misma clave y sus llaves tienen que cerrar: una plantilla rota lanzaria
// en la pantalla que la usa, no en el editor. Un lote con una plantilla invalida
// se rechaza entero y no guarda nada.
Caso_704ILR("[39] Plantillas de traduccion validadas al guardar:");
try
{
    string fabrica_704ILR = "Intento {0} de {1}.";
    Esperar_704ILR("marcador que la clave no admite", EvenTech.BLL.BLL_Idioma_704ILR.PlantillaValida_704ILR(fabrica_704ILR, "Intento {2}"), false);
    Esperar_704ILR("llave sin cerrar", EvenTech.BLL.BLL_Idioma_704ILR.PlantillaValida_704ILR(fabrica_704ILR, "Intento {0 de {1}"), false);
    Esperar_704ILR("llave de cierre suelta", EvenTech.BLL.BLL_Idioma_704ILR.PlantillaValida_704ILR(fabrica_704ILR, "Intento {0} de {1} }"), false);
    Esperar_704ILR("llaves escapadas", EvenTech.BLL.BLL_Idioma_704ILR.PlantillaValida_704ILR(fabrica_704ILR, "Intento {{0}} de {1}"), true);
    Esperar_704ILR("marcadores en otro orden", EvenTech.BLL.BLL_Idioma_704ILR.PlantillaValida_704ILR(fabrica_704ILR, "Tentativa {1} de {0}."), true);
    Esperar_704ILR("marcador sobre una clave sin marcadores", EvenTech.BLL.BLL_Idioma_704ILR.PlantillaValida_704ILR("Hola", "Hola {0}"), false);

    // Todo texto sembrado respeta el catalogo de marcadores del codigo. Una clave que
    // se formatea con argumentos y no figura en ese catalogo queda imposible de editar:
    // el editor rechaza cualquier texto suyo que conserve el marcador y lo acepta sin
    // el, con lo que la pantalla pierde el dato. Se controla en los tres idiomas.
    var idiomasSembrados_704ILR = EvenTech.BLL.BLL_Idioma_704ILR.GetIdiomas_704ILR();
    foreach (string codigoSembrado_704ILR in new[] { "ES", "EN", "PT" })
    {
        var idiomaSembrado_704ILR = idiomasSembrados_704ILR.FirstOrDefault(i_704ILR => i_704ILR.Codigo_704ILR == codigoSembrado_704ILR);
        string rechazada_704ILR = idiomaSembrado_704ILR == null
            ? "(idioma sin sembrar)"
            : EvenTech.BLL.BLL_Idioma_704ILR.PrimeraPlantillaInvalida_704ILR(
                  EvenTech.BLL.BLL_Idioma_704ILR.GetTraducciones_704ILR(idiomaSembrado_704ILR.Id_704ILR)) ?? "ninguna";
        Esperar_704ILR($"textos {codigoSembrado_704ILR} que el editor rechazaria por sus marcadores", rechazada_704ILR, "ninguna");
    }

    var en_704ILR = EvenTech.BLL.BLL_Idioma_704ILR.GetIdiomas_704ILR().FirstOrDefault(i_704ILR => i_704ILR.Codigo_704ILR == "EN");
    if (en_704ILR == null)
    {
        Omitir_704ILR("[39]", "el idioma EN no esta sembrado: el rechazo del lote no se probo");
    }
    else
    {
        EvenTech.BLL.BLL_Idioma_704ILR.GetTraducciones_704ILR(en_704ILR.Id_704ILR).TryGetValue("LOGIN_INTENTOS", out string textoAntes_704ILR);
        Esperar_704ILR("texto EN de fabrica", textoAntes_704ILR, "Attempt {0} of {1}.");
        int edicionesAntes_704ILR = Asientos_704ILR("Idiomas", "Edicion de traducciones");
        string rechazo_704ILR = "ninguna";
        try
        {
            EvenTech.BLL.BLL_Idioma_704ILR.GuardarTraducciones_704ILR(en_704ILR.Id_704ILR,
                new Dictionary<string, string> { { "LOGIN_INTENTOS", "Intento {2}" } });
        }
        catch (ArgumentException) { rechazo_704ILR = "ArgumentException"; }
        Esperar_704ILR("lote con plantilla invalida rechazado", rechazo_704ILR, "ArgumentException");
        EvenTech.BLL.BLL_Idioma_704ILR.GetTraducciones_704ILR(en_704ILR.Id_704ILR).TryGetValue("LOGIN_INTENTOS", out string textoDespues_704ILR);
        Esperar_704ILR("el texto EN no cambio", textoDespues_704ILR, textoAntes_704ILR);
        Esperar_704ILR("sin asiento de edicion", Asientos_704ILR("Idiomas", "Edicion de traducciones"), edicionesAntes_704ILR);
    }
}
catch (Exception ex39_704ILR) { Excepcion_704ILR("[39]", ex39_704ILR); }

// [40] La pantalla de acceso no revela si un usuario existe: un nombre
// inexistente recibe la misma respuesta y el mismo conteo de intentos que una
// contrasena incorrecta, y al tercer intento queda "bloqueado" igual. El conteo
// para nombres inexistentes vive en memoria (no hay fila donde guardarlo), por
// eso el nombre es unico por corrida. La distincion real queda en la auditoria.
Caso_704ILR("[40] El login no revela la existencia del usuario:");
try
{
    string inexistente_704ILR = "smoke_inexistente_" + DateTime.Now.Ticks;
    string hash_704ILR = Encrypt_704ILR.HashValue_704ILR("loquesea");
    var i1_704ILR = BLL_Login_704ILR.Authenticate_704ILR(inexistente_704ILR, hash_704ILR);
    Esperar_704ILR("primer intento", i1_704ILR.Result_704ILR, LoginResult_704ILR.IncorrectPassword_704ILR);
    Esperar_704ILR("intentos contados", i1_704ILR.FailedAttempts_704ILR, 1);
    var i2_704ILR = BLL_Login_704ILR.Authenticate_704ILR(inexistente_704ILR, hash_704ILR);
    Esperar_704ILR("segundo intento", i2_704ILR.Result_704ILR, LoginResult_704ILR.IncorrectPassword_704ILR);
    Esperar_704ILR("intentos contados", i2_704ILR.FailedAttempts_704ILR, 2);
    var i3_704ILR = BLL_Login_704ILR.Authenticate_704ILR(inexistente_704ILR, hash_704ILR);
    Esperar_704ILR("tercer intento", i3_704ILR.Result_704ILR, LoginResult_704ILR.UserBlocked_704ILR);
    Esperar_704ILR("intentos contados", i3_704ILR.FailedAttempts_704ILR, 3);
    Esperar_704ILR("sesion abierta", SessionManager_704ILR.IsSessionActive_704ILR, false);
    Esperar_704ILR("no se creo ninguna cuenta",
        BLL_User_704ILR.GetAll_704ILR().Any(u_704ILR => u_704ILR.Username_704ILR == inexistente_704ILR), false);
    Esperar_704ILR("fallos registrados en la auditoria de acceso",
        BLL_LoginAudit_704ILR.GetAll_704ILR(20).Count(e_704ILR => e_704ILR.Username_704ILR == inexistente_704ILR &&
            e_704ILR.Action_704ILR == EvenTech.BE.LoginAuditAction_704ILR.LOGIN_FAIL), 3);
}
catch (Exception ex40_704ILR) { Excepcion_704ILR("[40]", ex40_704ILR); }

// [41] Guardar sin cambiar nada no es una modificacion: no se versiona ni se
// asienta, para que el historial de versiones y la bitacora no acumulen entradas
// vacias. La composicion se compara por (servicio, cantidad, precio) y no por Id
// de linea ni por orden. Y una edicion que SOLO cambia las lineas si es una
// modificacion, nombrada como tal en el asiento.
Caso_704ILR("[41] Guardar sin cambios y edicion que solo cambia lineas:");
try
{
    var cliG_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salG_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    var srvG_704ILR = BLL_Servicio_704ILR.GetActivos_704ILR();
    if (cliG_704ILR.Count == 0 || salG_704ILR.Count == 0 || srvG_704ILR.Count < 2)
    {
        Omitir_704ILR("[41]", "faltan clientes/salones/servicios seed (hacen falta 2 servicios activos); corre db/schema.sql");
    }
    else
    {
        var lineasA_704ILR = new List<EvenTech.BE.BE_ReservaServicio_704ILR>
        {
            new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = srvG_704ILR[0].Id_704ILR, Cantidad_704ILR = 2, PrecioUnitario_704ILR = srvG_704ILR[0].Precio_704ILR },
            new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = srvG_704ILR[1].Id_704ILR, Cantidad_704ILR = 1, PrecioUnitario_704ILR = srvG_704ILR[1].Precio_704ILR }
        };
        var reservaG_704ILR = NuevaReserva_704ILR(cliG_704ILR[0].Id_704ILR, salG_704ILR[0].Id_704ILR, 6600,
            EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 0m);
        Esperar_704ILR("alta con dos lineas", BLL_Reserva_704ILR.Crear_704ILR(reservaG_704ILR, lineasA_704ILR, out int idG_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idG_704ILR);

        int versionesAntes_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(idG_704ILR).Count;
        int modificacionesAntes_704ILR = Asientos_704ILR("Reservas", "Modificacion de reserva");
        int historialAntes_704ILR = RegistradorDeCambios_704ILR.GetHistorial_704ILR("Reserva", idG_704ILR).Count;

        Esperar_704ILR("guardar la cabecera y las mismas lineas",
            BLL_Reserva_704ILR.Actualizar_704ILR(BLL_Reserva_704ILR.GetById_704ILR(idG_704ILR), BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idG_704ILR)),
            ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("guardar solo la cabecera sin tocarla",
            BLL_Reserva_704ILR.Actualizar_704ILR(BLL_Reserva_704ILR.GetById_704ILR(idG_704ILR)), ReservaResult_704ILR.Success_704ILR);
        // Las mismas lineas en otro orden y con Id de linea en cero (como las arma
        // la pantalla) tampoco cuentan como cambio.
        var mismasLineas_704ILR = BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idG_704ILR)
            .OrderByDescending(l_704ILR => l_704ILR.ServicioId_704ILR)
            .Select(l_704ILR => new EvenTech.BE.BE_ReservaServicio_704ILR
            { ServicioId_704ILR = l_704ILR.ServicioId_704ILR, Cantidad_704ILR = l_704ILR.Cantidad_704ILR, PrecioUnitario_704ILR = l_704ILR.PrecioUnitario_704ILR })
            .ToList();
        Esperar_704ILR("guardar las mismas lineas en otro orden y sin Id",
            BLL_Reserva_704ILR.Actualizar_704ILR(BLL_Reserva_704ILR.GetById_704ILR(idG_704ILR), mismasLineas_704ILR), ReservaResult_704ILR.Success_704ILR);

        Esperar_704ILR("versiones tras guardar sin cambios", CaretakerReserva_704ILR.GetVersiones_704ILR(idG_704ILR).Count, versionesAntes_704ILR);
        Esperar_704ILR("asientos 'Modificacion de reserva' tras guardar sin cambios",
            Asientos_704ILR("Reservas", "Modificacion de reserva"), modificacionesAntes_704ILR);
        Esperar_704ILR("historial de cambios tras guardar sin cambios",
            RegistradorDeCambios_704ILR.GetHistorial_704ILR("Reserva", idG_704ILR).Count, historialAntes_704ILR);

        // Edicion que solo cambia las lineas, con el MISMO total que A, para aislar
        // el cambio de composicion del cambio de monto: primero se busca otro
        // servicio del catalogo cuyo precio por una cantidad chica sume igual; si
        // los precios sembrados no lo permiten, se parte la linea "2 x srv0" en dos
        // lineas de 1 (misma suma, composicion distinta). El asiento tiene que
        // decir "0 campo(s) modificado(s)" y nombrar las lineas.
        decimal totalA_704ILR = BLL_ReservaServicio_704ILR.Total_704ILR(lineasA_704ILR);
        List<EvenTech.BE.BE_ReservaServicio_704ILR> lineasB_704ILR = null;
        foreach (var candidato_704ILR in srvG_704ILR)
        {
            if (candidato_704ILR.Precio_704ILR <= 0m) continue;
            for (int cant_704ILR = 1; cant_704ILR <= 6 && lineasB_704ILR == null; cant_704ILR++)
                if (candidato_704ILR.Precio_704ILR * cant_704ILR == totalA_704ILR)
                    lineasB_704ILR = new List<EvenTech.BE.BE_ReservaServicio_704ILR>
                    { new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = candidato_704ILR.Id_704ILR, Cantidad_704ILR = cant_704ILR, PrecioUnitario_704ILR = candidato_704ILR.Precio_704ILR } };
            if (lineasB_704ILR != null) break;
        }
        if (lineasB_704ILR == null)
            lineasB_704ILR = new List<EvenTech.BE.BE_ReservaServicio_704ILR>
            {
                new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = srvG_704ILR[0].Id_704ILR, Cantidad_704ILR = 1, PrecioUnitario_704ILR = srvG_704ILR[0].Precio_704ILR },
                new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = srvG_704ILR[0].Id_704ILR, Cantidad_704ILR = 1, PrecioUnitario_704ILR = srvG_704ILR[0].Precio_704ILR },
                new EvenTech.BE.BE_ReservaServicio_704ILR { ServicioId_704ILR = srvG_704ILR[1].Id_704ILR, Cantidad_704ILR = 1, PrecioUnitario_704ILR = srvG_704ILR[1].Precio_704ILR }
            };
        decimal totalB_704ILR = BLL_ReservaServicio_704ILR.Total_704ILR(lineasB_704ILR);
        Esperar_704ILR("la composicion B suma lo mismo que A", totalB_704ILR, totalA_704ILR);
        Esperar_704ILR("la composicion B es distinta de A", BLL_ReservaServicio_704ILR.MismasLineas_704ILR(lineasA_704ILR, lineasB_704ILR), false);
        int camposEsperados_704ILR = 0;
        Console.WriteLine($"  composicion B: {lineasB_704ILR.Count} linea(s), total {totalB_704ILR:0.00} (A: {totalA_704ILR:0.00})");

        Esperar_704ILR("cambiar solo la composicion",
            BLL_Reserva_704ILR.Actualizar_704ILR(BLL_Reserva_704ILR.GetById_704ILR(idG_704ILR), lineasB_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("versiones tras cambiar la composicion", CaretakerReserva_704ILR.GetVersiones_704ILR(idG_704ILR).Count, versionesAntes_704ILR + 1);
        Esperar_704ILR("lineas persistidas", BLL_ReservaServicio_704ILR.GetByReserva_704ILR(idG_704ILR).Count, lineasB_704ILR.Count);
        Esperar_704ILR("monto sin cambio (misma suma)", BLL_Reserva_704ILR.GetById_704ILR(idG_704ILR).Monto_704ILR, totalA_704ILR);
        var modificaciones_704ILR = Bitacora_704ILR("Reservas", "Modificacion de reserva");
        Esperar_704ILR("asientos 'Modificacion de reserva'", modificaciones_704ILR.Count, modificacionesAntes_704ILR + 1);
        string detalleMod_704ILR = modificaciones_704ILR.Count > 0 ? modificaciones_704ILR[0].Detalle_704ILR ?? "" : "";
        Console.WriteLine($"    asiento: {detalleMod_704ILR}");
        Esperar_704ILR("el asiento nombra la reserva", detalleMod_704ILR.Contains($"#{idG_704ILR}"), true);
        Esperar_704ILR("el asiento nombra el cambio de composicion",
            detalleMod_704ILR.Contains($"{camposEsperados_704ILR} campo(s) modificado(s); servicios modificados: {lineasB_704ILR.Count} linea(s)"), true);

        Esperar_704ILR("limpieza (cancelar la reserva de [41])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idG_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
}
catch (Exception ex41_704ILR) { Excepcion_704ILR("[41]", ex41_704ILR); }

// [42] Renovar solo actua sobre una operacion que TIENE plazo (RN-01): una
// CONFIRMADA no vence y no se "renueva" (antes quedaba un asiento con la fecha
// vacia), una CANCELADA no se toca, una cotizacion sin vencimiento cargado no
// tiene nada que renovar, y una cotizacion vigente si se renueva (reinicia el
// plazo: comportamiento conservado y documentado).
Caso_704ILR("[42] Renovar sin plazo (RN-01):");
try
{
    var cliR_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salR_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (cliR_704ILR.Count == 0 || salR_704ILR.Count == 0)
    {
        Omitir_704ILR("[42]", "faltan clientes/salones seed; corre db/schema.sql");
    }
    else
    {
        Esperar_704ILR("alta cotizacion",
            BLL_Reserva_704ILR.Crear_704ILR(NuevaReserva_704ILR(cliR_704ILR[0].Id_704ILR, salR_704ILR[0].Id_704ILR, 6700,
                EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 300m), out int idR_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idR_704ILR);

        // Vigente: se renueva y el plazo vuelve a contarse desde ahora.
        int renovacionesAntes_704ILR = Asientos_704ILR("Reservas", "Renovacion de vigencia");
        DateTime? venciaAntes_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idR_704ILR).VenceEl_704ILR;
        Esperar_704ILR("renovar una cotizacion vigente", BLL_Reserva_704ILR.Renovar_704ILR(idR_704ILR), ReservaResult_704ILR.Success_704ILR);
        DateTime? venciaDespues_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idR_704ILR).VenceEl_704ILR;
        Esperar_704ILR("el plazo no se acorto", venciaDespues_704ILR.HasValue && venciaAntes_704ILR.HasValue && venciaDespues_704ILR >= venciaAntes_704ILR, true);
        Esperar_704ILR("asiento 'Renovacion de vigencia'", Asientos_704ILR("Reservas", "Renovacion de vigencia"), renovacionesAntes_704ILR + 1);

        // Sin vencimiento cargado: nada que renovar, y sin asiento.
        var sinPlazo_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idR_704ILR);
        sinPlazo_704ILR.VenceEl_704ILR = null;
        EvenTech.DAL.DAL_Reserva_704ILR.Update_704ILR(sinPlazo_704ILR);
        Esperar_704ILR("renovar una cotizacion sin vencimiento cargado", BLL_Reserva_704ILR.Renovar_704ILR(idR_704ILR), ReservaResult_704ILR.SinPlazo_704ILR);
        Esperar_704ILR("sigue sin vencimiento", BLL_Reserva_704ILR.GetById_704ILR(idR_704ILR).VenceEl_704ILR.HasValue, false);
        Esperar_704ILR("sin asiento nuevo", Asientos_704ILR("Reservas", "Renovacion de vigencia"), renovacionesAntes_704ILR + 1);

        // CONFIRMADA: no tiene plazo.
        Adelanto_704ILR(idR_704ILR, 100m);   // RN-07
        var aConfirmarR_704ILR = BLL_Reserva_704ILR.GetById_704ILR(idR_704ILR);
        aConfirmarR_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar", BLL_Reserva_704ILR.Actualizar_704ILR(aConfirmarR_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("renovar una confirmada", BLL_Reserva_704ILR.Renovar_704ILR(idR_704ILR), ReservaResult_704ILR.SinPlazo_704ILR);
        Esperar_704ILR("la confirmada sigue sin vencimiento", BLL_Reserva_704ILR.GetById_704ILR(idR_704ILR).VenceEl_704ILR.HasValue, false);
        Esperar_704ILR("sin asiento nuevo", Asientos_704ILR("Reservas", "Renovacion de vigencia"), renovacionesAntes_704ILR + 1);

        // CANCELADA: estado terminal.
        Esperar_704ILR("limpieza (cancelar la reserva de [42])",
            BLL_Reserva_704ILR.Cancelar_704ILR(idR_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("renovar una cancelada", BLL_Reserva_704ILR.Renovar_704ILR(idR_704ILR), ReservaResult_704ILR.NoModificable_704ILR);
        Esperar_704ILR("renovar una reserva inexistente", BLL_Reserva_704ILR.Renovar_704ILR(999999), ReservaResult_704ILR.NotFound_704ILR);
    }
}
catch (Exception ex42_704ILR) { Excepcion_704ILR("[42]", ex42_704ILR); }

// ===========================================================================
// Proceso 2 (RFN2): Asignacion de Personal y Cronograma de Eventos.
// Los casos [43] a [49] recorren el proceso completo sobre eventos propios de la
// corrida: personal, asignacion con control de superposicion, respuesta del
// empleado, cronograma, tareas, ejecucion con incidencias y reprogramacion. Los
// empleados, los eventos y las asignaciones se comparten entre casos (cada uno
// continua lo que dejo el anterior); un caso que no encuentra lo que necesita se
// declara omitido. Todo lo que crean se elimina en la limpieza final.
// ===========================================================================
var empleadosDeLaCorrida_704ILR = new List<int>();
int empA_704ILR = 0, empB_704ILR = 0, empC_704ILR = 0;
int evR1_704ILR = 0, evR2_704ILR = 0, evR3_704ILR = 0;
int asigA1_704ILR = 0, asigA2_704ILR = 0, asigA3_704ILR = 0;

TimeSpan H_704ILR(int hora_704ILR, int minuto_704ILR = 0) => new TimeSpan(hora_704ILR, minuto_704ILR, 0);

EvenTech.BE.EstadoCoordinacion_704ILR EstadoCoord_704ILR(int reservaId_704ILR) =>
    BLL_Coordinacion_704ILR.GetEvento_704ILR(reservaId_704ILR).EstadoCoordinacion_704ILR;

EvenTech.BE.BE_AsignacionPersonal_704ILR Asignacion_704ILR(int reservaId_704ILR, int empleadoId_704ILR) =>
    BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(reservaId_704ILR).FirstOrDefault(a_704ILR => a_704ILR.EmpleadoId_704ILR == empleadoId_704ILR);

// Evento de prueba: una reserva que recorre el camino real hasta CONFIRMADA
// (alta en cotizacion, adelanto y confirmacion). Devuelve 0 si no lo logra.
int EventoConfirmado_704ILR(int clienteId_704ILR, int salonId_704ILR, int diasVista_704ILR)
{
    var rAlta_704ILR = BLL_Reserva_704ILR.Crear_704ILR(
        NuevaReserva_704ILR(clienteId_704ILR, salonId_704ILR, diasVista_704ILR, EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 900m, 20), out int id_704ILR);
    Anotar_704ILR(id_704ILR);
    if (rAlta_704ILR != ReservaResult_704ILR.Success_704ILR) return 0;
    Adelanto_704ILR(id_704ILR, 300m);
    var confirmar_704ILR = BLL_Reserva_704ILR.GetById_704ILR(id_704ILR);
    confirmar_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
    return BLL_Reserva_704ILR.Actualizar_704ILR(confirmar_704ILR) == ReservaResult_704ILR.Success_704ILR ? id_704ILR : 0;
}

// Sesion del usuario de prueba de [2], que [43] vincula con el empleado A: es la
// identidad con la que ese empleado responde sus turnos y consulta sus tareas.
bool SesionDelEmpleado_704ILR()
{
    if (SessionManager_704ILR.IsSessionActive_704ILR) BLL_Login_704ILR.Logout_704ILR();
    return BLL_Login_704ILR.Authenticate_704ILR(newUser_704ILR, Encrypt_704ILR.HashValue_704ILR("pass1234")).Result_704ILR == LoginResult_704ILR.Success_704ILR;
}

void CerrarSesion_704ILR()
{
    if (SessionManager_704ILR.IsSessionActive_704ILR) BLL_Login_704ILR.Logout_704ILR();
}

// La cuenta de prueba pasa de representar a un empleado a representar a otro. Una
// cuenta responde por una sola persona, asi que primero se la desvincula del primero.
bool VincularCuenta_704ILR(int desde_704ILR, int hacia_704ILR)
{
    var origen_704ILR = BLL_Empleado_704ILR.GetById_704ILR(desde_704ILR);
    int? cuenta_704ILR = origen_704ILR?.UserId_704ILR;
    if (cuenta_704ILR == null) return false;
    origen_704ILR.UserId_704ILR = null;
    if (BLL_Empleado_704ILR.Actualizar_704ILR(origen_704ILR) != EmpleadoResult_704ILR.Success_704ILR) return false;
    var destino_704ILR = BLL_Empleado_704ILR.GetById_704ILR(hacia_704ILR);
    destino_704ILR.UserId_704ILR = cuenta_704ILR;
    return BLL_Empleado_704ILR.Actualizar_704ILR(destino_704ILR) == EmpleadoResult_704ILR.Success_704ILR;
}

// Limpieza de lo que crea el Proceso 2 sobre las reservas de la corrida. Un
// evento en ejecucion o cerrado congela su reserva (RN-13) y la aplicacion no da de
// baja la coordinacion de un evento, asi que el rastro se borra directamente contra
// la base (igual que los perfiles de [19]) antes de cancelar las reservas.
void BorrarCoordinacionDePrueba_704ILR()
{
    if (reservasDeLaCorrida_704ILR.Count > 0)
    {
        string ids_704ILR = string.Join(",", reservasDeLaCorrida_704ILR);
        Ejecutar_704ILR(
            "DELETE FROM dbo.Incidencias WHERE ReservaId IN (" + ids_704ILR + "); " +
            "DELETE t FROM dbo.Tareas t INNER JOIN dbo.Cronogramas c ON c.Id = t.CronogramaId WHERE c.ReservaId IN (" + ids_704ILR + "); " +
            "DELETE a FROM dbo.CronogramaActividades a INNER JOIN dbo.Cronogramas c ON c.Id = a.CronogramaId WHERE c.ReservaId IN (" + ids_704ILR + "); " +
            "DELETE FROM dbo.Cronogramas WHERE ReservaId IN (" + ids_704ILR + "); " +
            "DELETE FROM dbo.AsignacionesPersonal WHERE ReservaId IN (" + ids_704ILR + "); " +
            "UPDATE dbo.Reservas SET EstadoCoordinacion = 'SIN_ASIGNAR' WHERE Id IN (" + ids_704ILR + ");");
    }
    foreach (int idEmpleado_704ILR in empleadosDeLaCorrida_704ILR.ToList())
    {
        Ejecutar_704ILR(
            "DELETE FROM dbo.Empleados WHERE Id = @id AND NOT EXISTS (SELECT 1 FROM dbo.AsignacionesPersonal WHERE EmpleadoId = @id)",
            ("@id", idEmpleado_704ILR));
        empleadosDeLaCorrida_704ILR.Remove(idEmpleado_704ILR);
    }
}

// [43] Personal: alta con sus validaciones, DNI unico y vinculo con la cuenta con
// la que el empleado ingresa (una cuenta representa a un solo empleado).
Caso_704ILR("[43] Empleados: alta, validaciones y vinculo con la cuenta:");
try
{
    var especialidades_704ILR = BLL_Empleado_704ILR.GetEspecialidades_704ILR();
    var cuenta_704ILR = BLL_User_704ILR.GetAll_704ILR().FirstOrDefault(u_704ILR => u_704ILR.Username_704ILR == newUser_704ILR);
    if (especialidades_704ILR.Count == 0 || cuenta_704ILR == null)
    {
        Omitir_704ILR("[43]", "faltan las especialidades seed (corre db/schema.sql) o el usuario de prueba de [2]");
    }
    else
    {
        int esp_704ILR = especialidades_704ILR[0].Id_704ILR;
        int asientosAlta_704ILR = Asientos_704ILR("Empleados", "Alta de empleado");
        EvenTech.BE.BE_Empleado_704ILR Nuevo_704ILR(string nombre_704ILR, string dni_704ILR) => new EvenTech.BE.BE_Empleado_704ILR
        { Nombre_704ILR = nombre_704ILR, Apellido_704ILR = "Smoke" + suf_704ILR, Dni_704ILR = dni_704ILR, EspecialidadId_704ILR = esp_704ILR };

        Esperar_704ILR("alta sin nombre", BLL_Empleado_704ILR.Crear_704ILR(Nuevo_704ILR(" ", "91" + suf_704ILR), out _), EmpleadoResult_704ILR.NombreInvalido_704ILR);
        Esperar_704ILR("alta con DNI no numerico", BLL_Empleado_704ILR.Crear_704ILR(Nuevo_704ILR("Ana", "ABC123"), out _), EmpleadoResult_704ILR.DniInvalido_704ILR);
        Esperar_704ILR("alta sin DNI", BLL_Empleado_704ILR.Crear_704ILR(Nuevo_704ILR("Ana", ""), out _), EmpleadoResult_704ILR.DniInvalido_704ILR);
        var sinEspecialidad_704ILR = Nuevo_704ILR("Ana", "91" + suf_704ILR);
        sinEspecialidad_704ILR.EspecialidadId_704ILR = 0;
        Esperar_704ILR("alta sin especialidad", BLL_Empleado_704ILR.Crear_704ILR(sinEspecialidad_704ILR, out _), EmpleadoResult_704ILR.EspecialidadInvalida_704ILR);
        Esperar_704ILR("alta con un nombre que no entra en su columna", BLL_Empleado_704ILR.Crear_704ILR(Nuevo_704ILR(new string('x', 61), "94" + suf_704ILR), out _), EmpleadoResult_704ILR.LongitudExcedida_704ILR);

        Esperar_704ILR("alta del empleado A", BLL_Empleado_704ILR.Crear_704ILR(Nuevo_704ILR("Ana", "91" + suf_704ILR), out empA_704ILR), EmpleadoResult_704ILR.Success_704ILR);
        if (empA_704ILR > 0) empleadosDeLaCorrida_704ILR.Add(empA_704ILR);
        Esperar_704ILR("alta del empleado B", BLL_Empleado_704ILR.Crear_704ILR(Nuevo_704ILR("Bruno", "92" + suf_704ILR), out empB_704ILR), EmpleadoResult_704ILR.Success_704ILR);
        if (empB_704ILR > 0) empleadosDeLaCorrida_704ILR.Add(empB_704ILR);
        Esperar_704ILR("alta del empleado C", BLL_Empleado_704ILR.Crear_704ILR(Nuevo_704ILR("Carla", "93" + suf_704ILR), out empC_704ILR), EmpleadoResult_704ILR.Success_704ILR);
        if (empC_704ILR > 0) empleadosDeLaCorrida_704ILR.Add(empC_704ILR);
        Esperar_704ILR("asientos de alta de empleado", Asientos_704ILR("Empleados", "Alta de empleado") - asientosAlta_704ILR, 3);
        Esperar_704ILR("los tres figuran entre los activos",
            BLL_Empleado_704ILR.GetActivos_704ILR().Count(e_704ILR => e_704ILR.Apellido_704ILR == "Smoke" + suf_704ILR), 3);

        // El mismo documento escrito con puntos es el mismo DNI.
        string dniConPuntos_704ILR = "91." + suf_704ILR.Substring(0, 6) + "." + suf_704ILR.Substring(6);
        Esperar_704ILR("alta con el DNI de A (con puntos)", BLL_Empleado_704ILR.Crear_704ILR(Nuevo_704ILR("Otra", dniConPuntos_704ILR), out _), EmpleadoResult_704ILR.DniDuplicado_704ILR);
        Esperar_704ILR("DNI guardado solo con digitos", BLL_Empleado_704ILR.GetById_704ILR(empA_704ILR)?.Dni_704ILR, "91" + suf_704ILR);

        // Vinculo con la cuenta: A queda vinculado al usuario de prueba.
        var a_704ILR = BLL_Empleado_704ILR.GetById_704ILR(empA_704ILR);
        a_704ILR.UserId_704ILR = cuenta_704ILR.Id_704ILR;
        Esperar_704ILR("vincular A con la cuenta", BLL_Empleado_704ILR.Actualizar_704ILR(a_704ILR), EmpleadoResult_704ILR.Success_704ILR);
        Esperar_704ILR("cuenta de A", BLL_Empleado_704ILR.GetById_704ILR(empA_704ILR)?.Username_704ILR, newUser_704ILR);

        var b_704ILR = BLL_Empleado_704ILR.GetById_704ILR(empB_704ILR);
        b_704ILR.UserId_704ILR = cuenta_704ILR.Id_704ILR;
        Esperar_704ILR("vincular B con la misma cuenta", BLL_Empleado_704ILR.Actualizar_704ILR(b_704ILR), EmpleadoResult_704ILR.CuentaYaVinculada_704ILR);
        b_704ILR.UserId_704ILR = int.MaxValue;
        Esperar_704ILR("vincular B con una cuenta inexistente", BLL_Empleado_704ILR.Actualizar_704ILR(b_704ILR), EmpleadoResult_704ILR.CuentaInvalida_704ILR);

        // Guardar sin cambiar nada no es una modificacion: no deja asiento.
        int asientosMod_704ILR = Asientos_704ILR("Empleados", "Modificacion de empleado");
        Esperar_704ILR("guardar A sin cambios", BLL_Empleado_704ILR.Actualizar_704ILR(BLL_Empleado_704ILR.GetById_704ILR(empA_704ILR)), EmpleadoResult_704ILR.Success_704ILR);
        Esperar_704ILR("asientos por guardar sin cambios", Asientos_704ILR("Empleados", "Modificacion de empleado") - asientosMod_704ILR, 0);
        Esperar_704ILR("modificar un empleado inexistente",
            BLL_Empleado_704ILR.Actualizar_704ILR(new EvenTech.BE.BE_Empleado_704ILR { Id_704ILR = int.MaxValue, Nombre_704ILR = "X", Apellido_704ILR = "Y", Dni_704ILR = "12345678", EspecialidadId_704ILR = esp_704ILR }),
            EmpleadoResult_704ILR.NotFound_704ILR);

        // El empleado de la sesion sale de la cuenta vinculada.
        Esperar_704ILR("sin sesion no hay empleado", BLL_Empleado_704ILR.GetDeLaSesion_704ILR() == null, true);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("empleado de la sesion", BLL_Empleado_704ILR.GetDeLaSesion_704ILR()?.Id_704ILR, (int?)empA_704ILR);
        CerrarSesion_704ILR();
    }
}
catch (Exception ex43_704ILR) { Excepcion_704ILR("[43]", ex43_704ILR); CerrarSesion_704ILR(); }

// [44] CUN006 Asignar personal. RN-08: solo se coordina una reserva CONFIRMADA.
// RN-09: la franja no puede pisarse con otro turno del empleado en otro evento,
// tampoco cuando el turno cruza la medianoche y choca con el dia siguiente.
Caso_704ILR("[44] CUN006 Asignar personal (RN-08 reserva confirmada, RN-09 superposicion):");
try
{
    var cliOp_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
    var salOp_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (empA_704ILR == 0 || empB_704ILR == 0 || empC_704ILR == 0 || cliOp_704ILR.Count == 0 || salOp_704ILR.Count < 2
        || BLL_Pago_704ILR.GetMetodos_704ILR().Count == 0)
    {
        Omitir_704ILR("[44]", "faltan los empleados de [43] o hacen falta un cliente, dos salones y un metodo de pago seed; corre db/schema.sql");
    }
    else
    {
        int cli_704ILR = cliOp_704ILR[0].Id_704ILR;

        // RN-08: una cotizacion todavia no se coordina.
        Esperar_704ILR("alta cotizacion", BLL_Reserva_704ILR.Crear_704ILR(
            NuevaReserva_704ILR(cli_704ILR, salOp_704ILR[0].Id_704ILR, 8020, EvenTech.BE.EstadoReserva_704ILR.COTIZACION, 900m, 20), out int idCot_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(idCot_704ILR);
        int asientosRn08_704ILR = Asientos_704ILR("Coordinacion", "Coordinacion rechazada");
        Esperar_704ILR("asignar en una cotizacion (RN-08)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(idCot_704ILR, empA_704ILR, "Mozo", H_704ILR(20), H_704ILR(23), out _, out _), CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR);
        Esperar_704ILR("asiento del rechazo RN-08", Asientos_704ILR("Coordinacion", "Coordinacion rechazada") - asientosRn08_704ILR, 1);
        Esperar_704ILR("asignar en una reserva inexistente",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(int.MaxValue, empA_704ILR, "Mozo", H_704ILR(20), H_704ILR(23), out _, out _), CoordinacionResult_704ILR.ReservaInvalida_704ILR);
        // RN-08: una reserva cancelada tampoco se coordina.
        Esperar_704ILR("cancelar la cotizacion", BLL_Reserva_704ILR.Cancelar_704ILR(idCot_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("asignar en una reserva cancelada (RN-08)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(idCot_704ILR, empA_704ILR, "Mozo", H_704ILR(20), H_704ILR(23), out _, out _), CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR);
        Esperar_704ILR("asientos de los dos rechazos RN-08", Asientos_704ILR("Coordinacion", "Coordinacion rechazada") - asientosRn08_704ILR, 2);

        // Tres eventos: R1 y R2 el mismo dia en salones distintos, R3 al dia siguiente.
        evR1_704ILR = EventoConfirmado_704ILR(cli_704ILR, salOp_704ILR[0].Id_704ILR, 8000);
        evR2_704ILR = EventoConfirmado_704ILR(cli_704ILR, salOp_704ILR[1].Id_704ILR, 8000);
        evR3_704ILR = EventoConfirmado_704ILR(cli_704ILR, salOp_704ILR[0].Id_704ILR, 8001);
        Esperar_704ILR("eventos confirmados de la corrida", evR1_704ILR > 0 && evR2_704ILR > 0 && evR3_704ILR > 0, true);
        Esperar_704ILR("estado inicial de coordinacion", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.SIN_ASIGNAR);
        Esperar_704ILR("el evento figura entre los eventos a coordinar",
            BLL_Coordinacion_704ILR.GetEventos_704ILR().Any(e_704ILR => e_704ILR.ReservaId_704ILR == evR1_704ILR), true);

        int asientosRechazo_704ILR = Asientos_704ILR("Coordinacion", "Asignacion rechazada");
        int asientosAsig_704ILR = Asientos_704ILR("Coordinacion", "Asignacion de personal");

        // Errores de lo tipeado.
        Esperar_704ILR("rol vacio", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empA_704ILR, "  ", H_704ILR(20), H_704ILR(2), out _, out _), CoordinacionResult_704ILR.RolInvalido_704ILR);
        Esperar_704ILR("rol que no entra en su columna", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empA_704ILR, new string('r', 61), H_704ILR(20), H_704ILR(2), out _, out _), CoordinacionResult_704ILR.RolInvalido_704ILR);
        Esperar_704ILR("franja de duracion cero", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empA_704ILR, "Mozo", H_704ILR(20), H_704ILR(20), out _, out _), CoordinacionResult_704ILR.FranjaInvalida_704ILR);
        Esperar_704ILR("empleado inexistente", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, int.MaxValue, "Mozo", H_704ILR(20), H_704ILR(2), out _, out _), CoordinacionResult_704ILR.EmpleadoInvalido_704ILR);

        // A queda asignado a R1 de 20:00 a 02:00 (el turno cruza la medianoche).
        Esperar_704ILR("asignar A a R1 20:00-02:00", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empA_704ILR, "Mozo de salon", H_704ILR(20), H_704ILR(2), out asigA1_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("la asignacion nace pendiente", Asignacion_704ILR(evR1_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        Esperar_704ILR("estado de coordinacion de R1", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("A otra vez en R1", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empA_704ILR, "Mozo", H_704ILR(21), H_704ILR(23), out _, out _), CoordinacionResult_704ILR.YaAsignado_704ILR);

        // RN-09 el mismo dia, en el otro evento.
        Esperar_704ILR("A en R2 22:00-23:00 (se pisa con R1)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR2_704ILR, empA_704ILR, "Mozo", H_704ILR(22), H_704ILR(23), out _, out var conflicto_704ILR), CoordinacionResult_704ILR.Superposicion_704ILR);
        Esperar_704ILR("turno con el que se pisa", conflicto_704ILR?.ReservaId_704ILR, (int?)evR1_704ILR);
        Esperar_704ILR("R2 sigue sin personal", EstadoCoord_704ILR(evR2_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.SIN_ASIGNAR);

        // Quitar la unica asignacion devuelve el evento a SIN_ASIGNAR.
        int asientosBajaAsig_704ILR = Asientos_704ILR("Coordinacion", "Baja de asignacion");
        Esperar_704ILR("asignar B a R2", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR2_704ILR, empB_704ILR, "Barra", H_704ILR(10), H_704ILR(12), out int asigB2_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("R2 en coordinacion", EstadoCoord_704ILR(evR2_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("quitar a B de R2", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigB2_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("R2 vuelve a quedar sin personal", EstadoCoord_704ILR(evR2_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.SIN_ASIGNAR);
        Esperar_704ILR("asiento de la baja de asignacion", Asientos_704ILR("Coordinacion", "Baja de asignacion") - asientosBajaAsig_704ILR, 1);
        Esperar_704ILR("A en R2 10:00-18:00 (no se pisa)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR2_704ILR, empA_704ILR, "Armado", H_704ILR(10), H_704ILR(18), out asigA2_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);

        // RN-09 cruzando la medianoche: el turno de R1 termina a las 02:00 del dia de R3.
        Esperar_704ILR("A en R3 01:00-03:00 (se pisa con el final de R1)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR3_704ILR, empA_704ILR, "Mozo", H_704ILR(1), H_704ILR(3), out _, out var conflictoNoche_704ILR), CoordinacionResult_704ILR.Superposicion_704ILR);
        Esperar_704ILR("turno con el que se pisa (medianoche)", conflictoNoche_704ILR?.ReservaId_704ILR, (int?)evR1_704ILR);
        Esperar_704ILR("A en R3 02:00-06:00 (empieza donde termina R1)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR3_704ILR, empA_704ILR, "Desarme", H_704ILR(2), H_704ILR(6), out asigA3_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);

        // RN-09 en el otro sentido: el turno PEDIDO es el que cruza la medianoche y se
        // pisa con un turno del empleado en el evento del dia siguiente.
        Esperar_704ILR("asignar B a R3 02:00-06:00", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR3_704ILR, empB_704ILR, "Desarme", H_704ILR(2), H_704ILR(6), out int asigB3_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("B en R2 22:00-03:00 (cruza la medianoche y se pisa con su turno del dia siguiente)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR2_704ILR, empB_704ILR, "Barra", H_704ILR(22), H_704ILR(3), out _, out var conflictoManana_704ILR), CoordinacionResult_704ILR.Superposicion_704ILR);
        Esperar_704ILR("turno con el que se pisa (dia siguiente)", conflictoManana_704ILR?.ReservaId_704ILR, (int?)evR3_704ILR);
        Esperar_704ILR("B en R2 22:00-02:00 (termina donde empieza su turno de R3)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR2_704ILR, empB_704ILR, "Barra", H_704ILR(22), H_704ILR(2), out int asigB2b_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("quitar a B de R2 y de R3",
            BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigB2b_704ILR) + "," + BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigB3_704ILR),
            CoordinacionResult_704ILR.Success_704ILR + "," + CoordinacionResult_704ILR.Success_704ILR);

        Esperar_704ILR("asientos de asignacion", Asientos_704ILR("Coordinacion", "Asignacion de personal") - asientosAsig_704ILR, 6);
        Esperar_704ILR("asientos de rechazo por superposicion (RN-09)", Asientos_704ILR("Coordinacion", "Asignacion rechazada") - asientosRechazo_704ILR, 3);

        // Un empleado dado de baja no se asigna; uno con turnos vigentes no se da de baja.
        var c_704ILR = BLL_Empleado_704ILR.GetById_704ILR(empC_704ILR);
        c_704ILR.Activo_704ILR = false;
        int asientosModEmp_704ILR = Asientos_704ILR("Empleados", "Modificacion de empleado");
        int asientosBajaEmp_704ILR = Asientos_704ILR("Empleados", "Baja rechazada");
        Esperar_704ILR("baja del empleado C (sin turnos)", BLL_Empleado_704ILR.Actualizar_704ILR(c_704ILR), EmpleadoResult_704ILR.Success_704ILR);
        Esperar_704ILR("asiento de la baja", Asientos_704ILR("Empleados", "Modificacion de empleado") - asientosModEmp_704ILR, 1);
        Esperar_704ILR("C ya no figura entre los activos", BLL_Empleado_704ILR.GetActivos_704ILR().Any(e_704ILR => e_704ILR.Id_704ILR == empC_704ILR), false);
        Esperar_704ILR("asignar al empleado dado de baja",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empC_704ILR, "Mozo", H_704ILR(20), H_704ILR(23), out _, out _), CoordinacionResult_704ILR.EmpleadoInvalido_704ILR);
        var aBaja_704ILR = BLL_Empleado_704ILR.GetById_704ILR(empA_704ILR);
        aBaja_704ILR.Activo_704ILR = false;
        Esperar_704ILR("baja de A con turnos vigentes", BLL_Empleado_704ILR.Actualizar_704ILR(aBaja_704ILR), EmpleadoResult_704ILR.ConAsignacionesVigentes_704ILR);
        Esperar_704ILR("A sigue activo", BLL_Empleado_704ILR.GetById_704ILR(empA_704ILR)?.Activo_704ILR, (bool?)true);
        Esperar_704ILR("asiento de la baja rechazada", Asientos_704ILR("Empleados", "Baja rechazada") - asientosBajaEmp_704ILR, 1);
    }
}
catch (Exception ex44_704ILR) { Excepcion_704ILR("[44]", ex44_704ILR); }

// [45] CUN007 Confirmar disponibilidad. RN-10: responde el propio empleado (la
// cuenta de la sesion tiene que ser la suya) y el rechazo lleva motivo. Un turno
// rechazado se le puede volver a ofrecer.
Caso_704ILR("[45] CUN007 Confirmar disponibilidad (RN-10):");
try
{
    if (asigA1_704ILR == 0 || asigA2_704ILR == 0 || asigA3_704ILR == 0)
    {
        Omitir_704ILR("[45]", "faltan las asignaciones de [44]");
    }
    else
    {
        // B queda pendiente en R1: es el turno ajeno que A no puede responder.
        Esperar_704ILR("asignar B a R1", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empB_704ILR, "Barra", H_704ILR(20), H_704ILR(2), out int asigB1_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);

        CerrarSesion_704ILR();
        int asientosSinFicha_704ILR = Asientos_704ILR("Coordinacion", "Respuesta rechazada");
        Esperar_704ILR("confirmar sin sesion", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA1_704ILR, out _), CoordinacionResult_704ILR.SinEmpleadoVinculado_704ILR);
        // La sesion tiene que existir: sin ella las dos verificaciones siguientes darian
        // lo mismo y no distinguirian "cuenta sin empleado" de "sin sesion".
        Esperar_704ILR("sesion de una cuenta sin empleado (admin)",
            BLL_Login_704ILR.Authenticate_704ILR("admin", Encrypt_704ILR.HashValue_704ILR("admin123")).Result_704ILR, LoginResult_704ILR.Success_704ILR);
        Esperar_704ILR("confirmar con una cuenta que no es de un empleado", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA1_704ILR, out _), CoordinacionResult_704ILR.SinEmpleadoVinculado_704ILR);
        Esperar_704ILR("agenda de una cuenta sin empleado", BLL_AsignacionPersonal_704ILR.GetMisAsignaciones_704ILR().Count, 0);
        // RN-10: responder sin ser el empleado del turno queda asentado en los dos intentos.
        Esperar_704ILR("asientos de las respuestas de una cuenta sin ficha (RN-10)", Asientos_704ILR("Coordinacion", "Respuesta rechazada") - asientosSinFicha_704ILR, 2);
        Esperar_704ILR("el turno sigue pendiente tras esos intentos", Asignacion_704ILR(evR1_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        CerrarSesion_704ILR();

        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("agenda de A (turnos en eventos confirmados)", BLL_AsignacionPersonal_704ILR.GetMisAsignaciones_704ILR().Count, 3);

        int asientosAjena_704ILR = Asientos_704ILR("Coordinacion", "Respuesta rechazada");
        Esperar_704ILR("A responde el turno de B", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigB1_704ILR, out _), CoordinacionResult_704ILR.NoEsElEmpleado_704ILR);
        Esperar_704ILR("asiento del intento de responder por otro", Asientos_704ILR("Coordinacion", "Respuesta rechazada") - asientosAjena_704ILR, 1);

        // Una ficha dada de baja no responde turnos. La aplicacion no deja dar de baja
        // a quien tiene turnos vigentes: la baja se fuerza en la base solo para probar
        // la guarda, y se repone enseguida.
        Ejecutar_704ILR("UPDATE dbo.Empleados SET Activo = 0 WHERE Id = @id", ("@id", empA_704ILR));
        Esperar_704ILR("responder con la ficha dada de baja", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA1_704ILR, out _), CoordinacionResult_704ILR.EmpleadoDeBaja_704ILR);
        Ejecutar_704ILR("UPDATE dbo.Empleados SET Activo = 1 WHERE Id = @id", ("@id", empA_704ILR));
        Esperar_704ILR("el turno sigue pendiente", Asignacion_704ILR(evR1_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        Esperar_704ILR("asiento de la respuesta de una ficha dada de baja", Asientos_704ILR("Coordinacion", "Respuesta rechazada") - asientosAjena_704ILR, 2);
        int asientosConfirma_704ILR = Asientos_704ILR("Coordinacion", "Disponibilidad confirmada");
        int asientosRechaza_704ILR = Asientos_704ILR("Coordinacion", "Turno rechazado");
        Esperar_704ILR("el turno de B sigue pendiente", Asignacion_704ILR(evR1_704ILR, empB_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        Esperar_704ILR("confirmar una asignacion inexistente", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(int.MaxValue, out _), CoordinacionResult_704ILR.AsignacionInvalida_704ILR);

        Esperar_704ILR("A confirma su turno de R1", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA1_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        var confirmada_704ILR = Asignacion_704ILR(evR1_704ILR, empA_704ILR);
        Esperar_704ILR("estado del turno", confirmada_704ILR?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.CONFIRMADA);
        Esperar_704ILR("queda la fecha de la respuesta", confirmada_704ILR?.FechaConfirmacion_704ILR.HasValue, (bool?)true);
        Esperar_704ILR("confirmar dos veces", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA1_704ILR, out _), CoordinacionResult_704ILR.AsignacionYaRespondida_704ILR);

        Esperar_704ILR("rechazar sin motivo", BLL_AsignacionPersonal_704ILR.Rechazar_704ILR(asigA2_704ILR, "   "), CoordinacionResult_704ILR.MotivoObligatorio_704ILR);
        Esperar_704ILR("A rechaza su turno de R2", BLL_AsignacionPersonal_704ILR.Rechazar_704ILR(asigA2_704ILR, "Tengo otro compromiso ese dia"), CoordinacionResult_704ILR.Success_704ILR);
        var rechazada_704ILR = Asignacion_704ILR(evR2_704ILR, empA_704ILR);
        Esperar_704ILR("estado del turno rechazado", rechazada_704ILR?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.RECHAZADA);
        Esperar_704ILR("motivo guardado", rechazada_704ILR?.MotivoRechazo_704ILR, "Tengo otro compromiso ese dia");
        // Con el unico turno rechazado, R2 sigue en coordinacion: hay un rechazo sin resolver.
        Esperar_704ILR("estado de coordinacion de R2", EstadoCoord_704ILR(evR2_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("asiento de la confirmacion", Asientos_704ILR("Coordinacion", "Disponibilidad confirmada") - asientosConfirma_704ILR, 1);
        Esperar_704ILR("asiento del turno rechazado", Asientos_704ILR("Coordinacion", "Turno rechazado") - asientosRechaza_704ILR, 1);
        CerrarSesion_704ILR();

        // El coordinador le vuelve a ofrecer el turno con otra franja: es la misma asignacion.
        Esperar_704ILR("reasignar a A en R2 con otra franja",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR2_704ILR, empA_704ILR, "Armado", H_704ILR(11), H_704ILR(17), out int asigReactivada_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("es la misma asignacion", asigReactivada_704ILR, asigA2_704ILR);
        var reactivada_704ILR = Asignacion_704ILR(evR2_704ILR, empA_704ILR);
        Esperar_704ILR("vuelve a pendiente", reactivada_704ILR?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        Esperar_704ILR("sin motivo ni respuesta", reactivada_704ILR?.MotivoRechazo_704ILR == null && reactivada_704ILR?.FechaConfirmacion_704ILR == null, true);
        Esperar_704ILR("franja nueva", reactivada_704ILR?.HoraInicio_704ILR, (TimeSpan?)H_704ILR(11));
    }
}
catch (Exception ex45_704ILR) { Excepcion_704ILR("[45]", ex45_704ILR); CerrarSesion_704ILR(); }

// [46] CUN008 Generar cronograma. RN-11: con el equipo confirmado (personal
// confirmado y ninguna respuesta pendiente) y con responsables confirmados; hay un
// solo cronograma por reserva y el orden de las actividades es el del coordinador.
Caso_704ILR("[46] CUN008 Generar cronograma (RN-11):");
try
{
    var turnoA_704ILR = evR1_704ILR == 0 ? null : Asignacion_704ILR(evR1_704ILR, empA_704ILR);
    var turnoB_704ILR = evR1_704ILR == 0 ? null : Asignacion_704ILR(evR1_704ILR, empB_704ILR);
    if (turnoA_704ILR == null || turnoB_704ILR == null || turnoA_704ILR.Estado_704ILR != EvenTech.BE.EstadoAsignacion_704ILR.CONFIRMADA)
    {
        Omitir_704ILR("[46]", "depende de los turnos de [45] (A confirmado y B pendiente en R1)");
    }
    else
    {
        EvenTech.BE.BE_CronogramaActividad_704ILR Act_704ILR(int hora_704ILR, int minuto_704ILR, string descripcion_704ILR, int responsable_704ILR, int duracion_704ILR) =>
            new EvenTech.BE.BE_CronogramaActividad_704ILR
            { Hora_704ILR = H_704ILR(hora_704ILR, minuto_704ILR), Descripcion_704ILR = descripcion_704ILR, ResponsableId_704ILR = responsable_704ILR, DuracionMinutos_704ILR = duracion_704ILR };
        var jornada_704ILR = new List<EvenTech.BE.BE_CronogramaActividad_704ILR>
        {
            Act_704ILR(20, 0, "Recepcion de invitados", empA_704ILR, 30),
            Act_704ILR(21, 0, "Cena", empA_704ILR, 90),
            Act_704ILR(0, 30, "Brindis", empA_704ILR, 15)
        };

        int asientosRn11_704ILR = Asientos_704ILR("Coordinacion", "Cronograma rechazado");
        Esperar_704ILR("generar con una respuesta pendiente (RN-11)", BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, jornada_704ILR), CoordinacionResult_704ILR.PersonalSinConfirmar_704ILR);
        Esperar_704ILR("asiento del rechazo RN-11", Asientos_704ILR("Coordinacion", "Cronograma rechazado") - asientosRn11_704ILR, 1);

        // El coordinador quita a B, que no respondio: el equipo queda confirmado.
        Esperar_704ILR("quitar a B de R1", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(turnoB_704ILR.Id_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("quitar una asignacion que ya no existe", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(turnoB_704ILR.Id_704ILR), CoordinacionResult_704ILR.AsignacionInvalida_704ILR);
        // Todos confirmados pero sin cronograma: todavia en coordinacion.
        Esperar_704ILR("estado con el equipo confirmado y sin cronograma", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);

        Esperar_704ILR("cronograma sin actividades", BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR>()), CoordinacionResult_704ILR.SinActividades_704ILR);
        Esperar_704ILR("actividad sin descripcion",
            BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR> { Act_704ILR(20, 0, " ", empA_704ILR, 30) }), CoordinacionResult_704ILR.ActividadInvalida_704ILR);
        Esperar_704ILR("actividad de duracion cero",
            BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR> { Act_704ILR(20, 0, "Recepcion", empA_704ILR, 0) }), CoordinacionResult_704ILR.ActividadInvalida_704ILR);
        Esperar_704ILR("actividad con una descripcion que no entra en su columna",
            BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR> { Act_704ILR(20, 0, new string('d', 151), empA_704ILR, 30) }), CoordinacionResult_704ILR.ActividadInvalida_704ILR);
        Esperar_704ILR("actividad de mas de un dia",
            BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR> { Act_704ILR(20, 0, "Recepcion", empA_704ILR, 1441) }), CoordinacionResult_704ILR.ActividadInvalida_704ILR);
        Esperar_704ILR("responsable que no es del equipo (RN-11)",
            BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR> { Act_704ILR(20, 0, "Recepcion", empB_704ILR, 30) }), CoordinacionResult_704ILR.ResponsableInvalido_704ILR);
        Esperar_704ILR("asiento del rechazo por el responsable (RN-11)", Asientos_704ILR("Coordinacion", "Cronograma rechazado") - asientosRn11_704ILR, 2);
        Esperar_704ILR("nada de eso genero un cronograma", BLL_Cronograma_704ILR.GetByReserva_704ILR(evR1_704ILR) == null, true);

        int asientosGen_704ILR = Asientos_704ILR("Coordinacion", "Generacion de cronograma");
        int asientosMod_704ILR = Asientos_704ILR("Coordinacion", "Modificacion de cronograma");
        Esperar_704ILR("generar el cronograma", BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, jornada_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        var generado_704ILR = BLL_Cronograma_704ILR.GetByReserva_704ILR(evR1_704ILR);
        Esperar_704ILR("actividades guardadas", generado_704ILR?.Actividades_704ILR.Count, (int?)3);
        // El orden es el del coordinador: la actividad de las 00:30 va al final.
        Esperar_704ILR("orden conservado (la de las 00:30 es la ultima)", generado_704ILR?.Actividades_704ILR.LastOrDefault()?.Hora_704ILR, (TimeSpan?)H_704ILR(0, 30));
        Esperar_704ILR("estado de coordinacion de R1", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);

        // Eliminar el cronograma devuelve el evento a coordinacion; se lo vuelve a generar.
        int asientosElim_704ILR = Asientos_704ILR("Coordinacion", "Eliminacion de cronograma");
        Esperar_704ILR("eliminar el cronograma", BLL_Cronograma_704ILR.Eliminar_704ILR(evR1_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("el evento queda sin cronograma", BLL_Cronograma_704ILR.GetByReserva_704ILR(evR1_704ILR) == null, true);
        Esperar_704ILR("R1 vuelve a coordinacion", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("asiento de la eliminacion", Asientos_704ILR("Coordinacion", "Eliminacion de cronograma") - asientosElim_704ILR, 1);
        Esperar_704ILR("generar el cronograma de nuevo", BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, jornada_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("R1 listo otra vez", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);

        // Guardar lo mismo no es una modificacion; cambiar la lista si.
        Esperar_704ILR("guardar sin cambios", BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, jornada_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        jornada_704ILR.RemoveAt(1);
        Esperar_704ILR("guardar con una actividad menos", BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, jornada_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("actividades tras la modificacion", BLL_Cronograma_704ILR.GetByReserva_704ILR(evR1_704ILR)?.Actividades_704ILR.Count, (int?)2);
        Esperar_704ILR("asientos de generacion (el primero y el que siguio a la eliminacion)", Asientos_704ILR("Coordinacion", "Generacion de cronograma") - asientosGen_704ILR, 2);
        Esperar_704ILR("asientos de modificacion", Asientos_704ILR("Coordinacion", "Modificacion de cronograma") - asientosMod_704ILR, 1);
        Esperar_704ILR("un solo cronograma por reserva", Escalar_704ILR("SELECT COUNT(*) FROM dbo.Cronogramas WHERE ReservaId = @r", ("@r", evR1_704ILR)), 1);

        // Quien tiene un tramo a cargo no se quita del equipo.
        Esperar_704ILR("quitar a A, responsable de actividades", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(turnoA_704ILR.Id_704ILR), CoordinacionResult_704ILR.TieneCarga_704ILR);
        Esperar_704ILR("eliminar el cronograma de un evento que no lo tiene", BLL_Cronograma_704ILR.Eliminar_704ILR(evR2_704ILR), CoordinacionResult_704ILR.SinCronograma_704ILR);
    }
}
catch (Exception ex46_704ILR) { Excepcion_704ILR("[46]", ex46_704ILR); }

// [47] CUN009 Asignar tareas y CUN010 consultarlas. RN-12: la tarea va a personal
// confirmado, cae dentro de su turno (que puede cruzar la medianoche) y no se pisa
// con otra tarea del mismo empleado.
Caso_704ILR("[47] CUN009 Asignar tareas y CUN010 consultarlas (RN-12):");
try
{
    if (evR1_704ILR == 0 || BLL_Cronograma_704ILR.GetByReserva_704ILR(evR1_704ILR) == null)
    {
        Omitir_704ILR("[47]", "depende del cronograma de [46]");
    }
    else
    {
        EvenTech.BE.BE_Tarea_704ILR Tarea_704ILR(int reserva_704ILR, int empleado_704ILR, string descripcion_704ILR, TimeSpan desde_704ILR, TimeSpan hasta_704ILR) =>
            new EvenTech.BE.BE_Tarea_704ILR
            { ReservaId_704ILR = reserva_704ILR, EmpleadoId_704ILR = empleado_704ILR, Descripcion_704ILR = descripcion_704ILR, HoraInicio_704ILR = desde_704ILR, HoraFin_704ILR = hasta_704ILR, Prioridad_704ILR = EvenTech.BE.PrioridadTarea_704ILR.ALTA };

        // Las tareas se asignan sobre el cronograma generado: sin el, el rechazo queda
        // asentado (la pantalla no ofrece el alta, asi que solo llega por una carrera).
        int asientosSinCrono_704ILR = Asientos_704ILR("Coordinacion", "Tarea rechazada");
        Esperar_704ILR("tarea en un evento sin cronograma", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR3_704ILR, empA_704ILR, "Desarmar", H_704ILR(2), H_704ILR(3)), out _), CoordinacionResult_704ILR.SinCronograma_704ILR);
        Esperar_704ILR("asiento del rechazo por falta de cronograma", Asientos_704ILR("Coordinacion", "Tarea rechazada") - asientosSinCrono_704ILR, 1);
        Esperar_704ILR("tarea para quien no es del equipo", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empB_704ILR, "Armar mesas", H_704ILR(20), H_704ILR(21)), out _), CoordinacionResult_704ILR.ResponsableInvalido_704ILR);
        Esperar_704ILR("tarea sin descripcion", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, " ", H_704ILR(20), H_704ILR(21)), out _), CoordinacionResult_704ILR.DescripcionInvalida_704ILR);
        Esperar_704ILR("tarea con una descripcion que no entra en su columna", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, new string('t', 201), H_704ILR(20), H_704ILR(21)), out _), CoordinacionResult_704ILR.DescripcionInvalida_704ILR);
        Esperar_704ILR("tarea de duracion cero", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Armar mesas", H_704ILR(20), H_704ILR(20)), out _), CoordinacionResult_704ILR.FranjaInvalida_704ILR);

        // El turno de A en R1 es de 20:00 a 02:00.
        int asientosRn12_704ILR = Asientos_704ILR("Coordinacion", "Tarea rechazada");
        int asientosAltaTarea_704ILR = Asientos_704ILR("Coordinacion", "Asignacion de tarea");
        int asientosBajaTarea_704ILR = Asientos_704ILR("Coordinacion", "Baja de tarea");
        Esperar_704ILR("tarea antes del turno (19:00-20:00)", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Armar mesas", H_704ILR(19), H_704ILR(20)), out _), CoordinacionResult_704ILR.FueraDeFranja_704ILR);
        Esperar_704ILR("tarea que termina despues del turno (01:30-02:30)", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Cierre", H_704ILR(1, 30), H_704ILR(2, 30)), out _), CoordinacionResult_704ILR.FueraDeFranja_704ILR);
        Esperar_704ILR("tarea 20:00-21:00", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Armar mesas", H_704ILR(20), H_704ILR(21)), out int tarea1_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("tarea 20:30-21:30 (se pisa)", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Servir entrada", H_704ILR(20, 30), H_704ILR(21, 30)), out _), CoordinacionResult_704ILR.TareaSuperpuesta_704ILR);
        // La ultima tarea de la jornada (00:30-02:00) se carga ANTES que la que la precede
        // (23:30-00:30): asi el orden de alta no coincide con el orden dentro del turno y la
        // verificacion del orden, mas abajo, falla si la lista saliera por orden de alta o por
        // la hora a secas.
        Esperar_704ILR("tarea 00:30-02:00 (despues de la medianoche, hasta el fin del turno)", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Desarmar", H_704ILR(0, 30), H_704ILR(2)), out int tarea3_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("tarea 23:30-00:30 (cruza la medianoche, pegada a la siguiente)", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Servir el brindis", H_704ILR(23, 30), H_704ILR(0, 30)), out int tarea2_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("tarea 00:00-01:00 (se pisa con las dos que rodean la medianoche)", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Retirar copas", H_704ILR(0), H_704ILR(1)), out _), CoordinacionResult_704ILR.TareaSuperpuesta_704ILR);
        Esperar_704ILR("asientos de rechazo RN-12", Asientos_704ILR("Coordinacion", "Tarea rechazada") - asientosRn12_704ILR, 4);

        var tareas_704ILR = BLL_Tarea_704ILR.GetByReserva_704ILR(evR1_704ILR);
        Esperar_704ILR("tareas del evento", tareas_704ILR.Count, 3);
        // Ordenadas como ocurren dentro del turno: 20:00, 23:30 y 00:30, aunque la de las
        // 00:30 se haya cargado antes que la de las 23:30.
        Esperar_704ILR("el orden de alta no es el orden dentro del turno", tarea3_704ILR < tarea2_704ILR, true);
        Esperar_704ILR("orden dentro del turno", string.Join(",", tareas_704ILR.Select(t_704ILR => t_704ILR.Id_704ILR)), tarea1_704ILR + "," + tarea2_704ILR + "," + tarea3_704ILR);
        Esperar_704ILR("prioridad guardada", tareas_704ILR[0].Prioridad_704ILR, EvenTech.BE.PrioridadTarea_704ILR.ALTA);

        // CUN010: el empleado consulta sus tareas; otra cuenta no ve ninguna.
        Esperar_704ILR("mis tareas sin sesion", BLL_Tarea_704ILR.GetMisTareas_704ILR(evR1_704ILR).Count, 0);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("mis tareas (empleado A)", BLL_Tarea_704ILR.GetMisTareas_704ILR(evR1_704ILR).Count, 3);
        CerrarSesion_704ILR();

        // B se suma al equipo: mientras no responde, el evento deja de estar listo y B no
        // recibe tareas. Para que B responda, la cuenta de prueba pasa a representarlo.
        Esperar_704ILR("asignar B a R1 (pendiente)", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empB_704ILR, "Barra", H_704ILR(20), H_704ILR(2), out int asigB1b_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("R1 deja de estar listo", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("tarea para quien todavia no confirmo (RN-12)", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empB_704ILR, "Atender la barra", H_704ILR(20), H_704ILR(21)), out _), CoordinacionResult_704ILR.ResponsableInvalido_704ILR);
        Esperar_704ILR("la cuenta de prueba pasa a representar a B", VincularCuenta_704ILR(empA_704ILR, empB_704ILR), true);
        Esperar_704ILR("sesion del empleado B", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("B confirma su turno de R1", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigB1b_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("R1 listo con B confirmado", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);
        Esperar_704ILR("tarea para B", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empB_704ILR, "Atender la barra", H_704ILR(20), H_704ILR(21)), out int tareaB_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        // Cada empleado ve solo sus tareas.
        Esperar_704ILR("mis tareas (empleado B)", BLL_Tarea_704ILR.GetMisTareas_704ILR(evR1_704ILR).Count, 1);
        CerrarSesion_704ILR();
        // Quien tiene tareas no se quita del equipo, aunque no tenga actividades a cargo.
        Esperar_704ILR("quitar a B, que tiene una tarea", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigB1b_704ILR), CoordinacionResult_704ILR.TieneCarga_704ILR);
        Esperar_704ILR("quitar la tarea de B", BLL_Tarea_704ILR.Quitar_704ILR(tareaB_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("quitar a B sin carga", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigB1b_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("la cuenta de prueba vuelve a representar a A", VincularCuenta_704ILR(empB_704ILR, empA_704ILR), true);

        // El cronograma con tareas no se elimina, y el rechazo queda asentado (la pantalla
        // ofrece la operacion y el plan la impide); una tarea se quita una sola vez.
        int asientosCroConTareas_704ILR = Asientos_704ILR("Coordinacion", "Cronograma rechazado");
        Esperar_704ILR("eliminar un cronograma con tareas", BLL_Cronograma_704ILR.Eliminar_704ILR(evR1_704ILR), CoordinacionResult_704ILR.CronogramaConTareas_704ILR);
        Esperar_704ILR("asiento del rechazo de la eliminacion", Asientos_704ILR("Coordinacion", "Cronograma rechazado") - asientosCroConTareas_704ILR, 1);
        Esperar_704ILR("el cronograma sigue estando", BLL_Cronograma_704ILR.GetByReserva_704ILR(evR1_704ILR) != null, true);
        Esperar_704ILR("quitar una tarea", BLL_Tarea_704ILR.Quitar_704ILR(tarea3_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("quitar la misma tarea otra vez", BLL_Tarea_704ILR.Quitar_704ILR(tarea3_704ILR), CoordinacionResult_704ILR.TareaInvalida_704ILR);
        Esperar_704ILR("tareas que quedan", BLL_Tarea_704ILR.GetByReserva_704ILR(evR1_704ILR).Count, 2);
        // Las tareas no cambian el estado de coordinacion.
        Esperar_704ILR("estado de coordinacion de R1", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);
        Esperar_704ILR("asientos de asignacion de tarea (tres de A y una de B)", Asientos_704ILR("Coordinacion", "Asignacion de tarea") - asientosAltaTarea_704ILR, 4);
        Esperar_704ILR("asientos de baja de tarea", Asientos_704ILR("Coordinacion", "Baja de tarea") - asientosBajaTarea_704ILR, 2);

        // La franja que la pantalla propone para la proxima tarea es la primera hora libre
        // del turno (BLL_Tarea_704ILR.PrimeraFranjaLibre_704ILR): proponer siempre el
        // inicio del turno ofrecia una franja que la asignacion rechazaba. A tiene el turno
        // de 20:00 a 02:00 con tareas de 20:00 a 21:00 y de 23:30 a 00:30; cada franja
        // propuesta se asigna, hasta completar el turno.
        string Franja_704ILR(TimeSpan desde_704ILR, TimeSpan hasta_704ILR) => desde_704ILR.ToString(@"hh\:mm") + "-" + hasta_704ILR.ToString(@"hh\:mm");
        string Propuesta_704ILR(out TimeSpan desde_704ILR, out TimeSpan hasta_704ILR)
        {
            var turno_704ILR = BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(evR1_704ILR).First(a_704ILR => a_704ILR.EmpleadoId_704ILR == empA_704ILR);
            BLL_Tarea_704ILR.PrimeraFranjaLibre_704ILR(turno_704ILR, BLL_Tarea_704ILR.GetByReserva_704ILR(evR1_704ILR), out desde_704ILR, out hasta_704ILR);
            return Franja_704ILR(desde_704ILR, hasta_704ILR);
        }
        var propuestas_704ILR = new List<int>();
        foreach (string esperada_704ILR in new[] { "21:00-22:00", "22:00-23:00", "23:00-23:30", "00:30-01:30", "01:30-02:00" })
        {
            Esperar_704ILR("franja propuesta para la proxima tarea de A", Propuesta_704ILR(out TimeSpan pDesde_704ILR, out TimeSpan pHasta_704ILR), esperada_704ILR);
            Esperar_704ILR("la franja propuesta " + esperada_704ILR + " se asigna", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Tarea en la franja propuesta", pDesde_704ILR, pHasta_704ILR), out int tareaPropuesta_704ILR), CoordinacionResult_704ILR.Success_704ILR);
            propuestas_704ILR.Add(tareaPropuesta_704ILR);
        }
        // Con el turno completo se propone su primera hora y la asignacion dice por que no.
        Esperar_704ILR("franja propuesta con el turno completo", Propuesta_704ILR(out TimeSpan llenoDesde_704ILR, out TimeSpan llenoHasta_704ILR), "20:00-21:00");
        Esperar_704ILR("asignar con el turno completo", BLL_Tarea_704ILR.Asignar_704ILR(Tarea_704ILR(evR1_704ILR, empA_704ILR, "Sin lugar", llenoDesde_704ILR, llenoHasta_704ILR), out _), CoordinacionResult_704ILR.TareaSuperpuesta_704ILR);
        foreach (int tareaPropuesta_704ILR in propuestas_704ILR)
            Esperar_704ILR("quitar la tarea #" + tareaPropuesta_704ILR + " de la franja propuesta", BLL_Tarea_704ILR.Quitar_704ILR(tareaPropuesta_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("tareas del evento tras probar las franjas propuestas", BLL_Tarea_704ILR.GetByReserva_704ILR(evR1_704ILR).Count, 2);

        // Sin tocar la base: turno sin tareas, turno mas corto que una hora, tareas de otro
        // empleado y un turno que empieza despues de la medianoche.
        EvenTech.BE.BE_AsignacionPersonal_704ILR TurnoDe_704ILR(int empleado_704ILR, TimeSpan desde_704ILR, TimeSpan hasta_704ILR) =>
            new EvenTech.BE.BE_AsignacionPersonal_704ILR { ReservaId_704ILR = evR1_704ILR, EmpleadoId_704ILR = empleado_704ILR, HoraInicio_704ILR = desde_704ILR, HoraFin_704ILR = hasta_704ILR };
        string PropuestaDe_704ILR(EvenTech.BE.BE_AsignacionPersonal_704ILR turno_704ILR, params EvenTech.BE.BE_Tarea_704ILR[] tareasDadas_704ILR)
        {
            BLL_Tarea_704ILR.PrimeraFranjaLibre_704ILR(turno_704ILR, tareasDadas_704ILR, out TimeSpan desde_704ILR, out TimeSpan hasta_704ILR);
            return Franja_704ILR(desde_704ILR, hasta_704ILR);
        }
        Esperar_704ILR("propuesta sin tareas", PropuestaDe_704ILR(TurnoDe_704ILR(empA_704ILR, H_704ILR(20), H_704ILR(2))), "20:00-21:00");
        Esperar_704ILR("propuesta sin lista de tareas", PropuestaSinLista_704ILR(), "20:00-21:00");
        Esperar_704ILR("propuesta en un turno de 40 minutos", PropuestaDe_704ILR(TurnoDe_704ILR(empA_704ILR, H_704ILR(12), H_704ILR(12, 40))), "12:00-12:40");
        Esperar_704ILR("las tareas de otro empleado no cuentan", PropuestaDe_704ILR(TurnoDe_704ILR(empA_704ILR, H_704ILR(20), H_704ILR(2)),
            Tarea_704ILR(evR1_704ILR, empB_704ILR, "De otro", H_704ILR(20), H_704ILR(22))), "20:00-21:00");
        Esperar_704ILR("propuesta que cruza la medianoche", PropuestaDe_704ILR(TurnoDe_704ILR(empA_704ILR, H_704ILR(22), H_704ILR(4)),
            Tarea_704ILR(evR1_704ILR, empA_704ILR, "Primera", H_704ILR(22), H_704ILR(23, 30))), "23:30-00:30");
        Esperar_704ILR("hueco antes de una tarea que no arranca con el turno", PropuestaDe_704ILR(TurnoDe_704ILR(empA_704ILR, H_704ILR(20), H_704ILR(2)),
            Tarea_704ILR(evR1_704ILR, empA_704ILR, "Mas tarde", H_704ILR(20, 20), H_704ILR(21))), "20:00-20:20");

        string PropuestaSinLista_704ILR()
        {
            BLL_Tarea_704ILR.PrimeraFranjaLibre_704ILR(TurnoDe_704ILR(empA_704ILR, H_704ILR(20), H_704ILR(2)), null, out TimeSpan desde_704ILR, out TimeSpan hasta_704ILR);
            return Franja_704ILR(desde_704ILR, hasta_704ILR);
        }
    }
}
catch (Exception ex47_704ILR) { Excepcion_704ILR("[47]", ex47_704ILR); CerrarSesion_704ILR(); }

// [48] CUN011 Supervisar la ejecucion. RN-13: la ejecucion empieza con el evento
// LISTO; desde ahi el plan y la reserva quedan congelados, lo que se sale del plan
// se registra como incidencia y el evento se cierra con todas resueltas.
Caso_704ILR("[48] CUN011 Ejecucion, incidencias y cierre (RN-13):");
try
{
    if (evR1_704ILR == 0 || evR2_704ILR == 0 || EstadoCoord_704ILR(evR1_704ILR) != EvenTech.BE.EstadoCoordinacion_704ILR.LISTO)
    {
        Omitir_704ILR("[48]", "depende del evento listo de [46]");
    }
    else
    {
        EvenTech.BE.BE_Incidencia_704ILR Incidencia_704ILR(string descripcion_704ILR, int? reporta_704ILR) => new EvenTech.BE.BE_Incidencia_704ILR
        { ReservaId_704ILR = evR1_704ILR, Tipo_704ILR = EvenTech.BE.TipoIncidencia_704ILR.EQUIPAMIENTO, Descripcion_704ILR = descripcion_704ILR, EmpleadoReportaId_704ILR = reporta_704ILR };

        int asientosNoListo_704ILR = Asientos_704ILR("Coordinacion", "Ejecucion rechazada");
        int asientosCierreRech_704ILR = Asientos_704ILR("Coordinacion", "Cierre rechazado");
        int asientosRegla_704ILR = Asientos_704ILR("Coordinacion", "Coordinacion rechazada");
        int asientosInicio_704ILR = Asientos_704ILR("Coordinacion", "Inicio de ejecucion");
        int asientosCierre_704ILR = Asientos_704ILR("Coordinacion", "Cierre de evento");
        int asientosResol_704ILR = Asientos_704ILR("Coordinacion", "Resolucion de incidencia");
        int asientosModRech_704ILR = Asientos_704ILR("Reservas", "Modificacion rechazada");
        int asientosCanRech_704ILR = Asientos_704ILR("Reservas", "Cancelacion rechazada");
        int asientosResRech_704ILR = Asientos_704ILR("Reservas", "Restauracion rechazada");
        Esperar_704ILR("iniciar un evento que no esta listo", BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(evR2_704ILR, true), CoordinacionResult_704ILR.NoListo_704ILR);
        Esperar_704ILR("incidencia antes de iniciar", BLL_Incidencia_704ILR.Registrar_704ILR(Incidencia_704ILR("Falla de sonido", null), out _), CoordinacionResult_704ILR.NoEnEjecucion_704ILR);
        Esperar_704ILR("cerrar antes de iniciar", BLL_Coordinacion_704ILR.CerrarEvento_704ILR(evR1_704ILR), CoordinacionResult_704ILR.NoEnEjecucion_704ILR);

        // El evento esta agendado a anios vista: iniciar hoy exige confirmarlo.
        Esperar_704ILR("iniciar fuera de fecha sin confirmar", BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(evR1_704ILR, false), CoordinacionResult_704ILR.FueraDeFecha_704ILR);
        Esperar_704ILR("el evento sigue listo", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);
        Esperar_704ILR("iniciar confirmando", BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(evR1_704ILR, true), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("estado de coordinacion", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_EJECUCION);
        // Iniciar fuera de la fecha del evento es un desvio: se asienta como advertencia
        // (el inicio en fecha, como informacion: caso [51]).
        Esperar_704ILR("iniciar fuera de fecha se asienta como advertencia",
            Bitacora_704ILR("Coordinacion", "Inicio de ejecucion").FirstOrDefault()?.Criticidad_704ILR,
            (EvenTech.BE.CriticidadBitacora_704ILR?)EvenTech.BE.CriticidadBitacora_704ILR.Advertencia);
        Esperar_704ILR("iniciar dos veces", BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(evR1_704ILR, true), CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);

        // El plan queda congelado.
        Esperar_704ILR("asignar personal con el evento en ejecucion",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empB_704ILR, "Barra", H_704ILR(20), H_704ILR(23), out _, out _), CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
        Esperar_704ILR("quitar personal con el evento en ejecucion", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigA1_704ILR), CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
        Esperar_704ILR("modificar el cronograma con el evento en ejecucion",
            BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR>
            { new EvenTech.BE.BE_CronogramaActividad_704ILR { Hora_704ILR = H_704ILR(22), Descripcion_704ILR = "Baile", ResponsableId_704ILR = empA_704ILR, DuracionMinutos_704ILR = 60 } }),
            CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
        Esperar_704ILR("asignar una tarea con el evento en ejecucion",
            BLL_Tarea_704ILR.Asignar_704ILR(new EvenTech.BE.BE_Tarea_704ILR { ReservaId_704ILR = evR1_704ILR, EmpleadoId_704ILR = empA_704ILR, Descripcion_704ILR = "Extra", HoraInicio_704ILR = H_704ILR(22), HoraFin_704ILR = H_704ILR(23) }, out _),
            CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("responder un turno con el evento en ejecucion", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA1_704ILR, out _), CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
        CerrarSesion_704ILR();

        // La reserva tambien: no se modifica ni se cancela; el saldo se sigue cobrando.
        var congelada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evR1_704ILR);
        congelada_704ILR.CantidadInvitados_704ILR = 25;
        Esperar_704ILR("modificar la reserva de un evento en ejecucion", BLL_Reserva_704ILR.Actualizar_704ILR(congelada_704ILR), ReservaResult_704ILR.EventoIniciado_704ILR);
        Esperar_704ILR("cancelar la reserva de un evento en ejecucion", BLL_Reserva_704ILR.Cancelar_704ILR(evR1_704ILR, out _, out _), ReservaResult_704ILR.EventoIniciado_704ILR);
        var versionPrevia_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(evR1_704ILR).FirstOrDefault();
        Esperar_704ILR("R1 tiene una version anterior", versionPrevia_704ILR != null, true);
        if (versionPrevia_704ILR != null)
            Esperar_704ILR("restaurar una version con el evento en ejecucion", BLL_Reserva_704ILR.RestaurarVersion_704ILR(evR1_704ILR, versionPrevia_704ILR.Id_704ILR), ReservaResult_704ILR.EventoIniciado_704ILR);
        Esperar_704ILR("la reserva sigue confirmada", BLL_Reserva_704ILR.GetById_704ILR(evR1_704ILR).Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA);
        Esperar_704ILR("cobrar saldo con el evento en ejecucion", Adelanto_704ILR(evR1_704ILR, 100m) > 0, true);
        Esperar_704ILR("asientos de la reserva congelada (modificacion, cancelacion y restauracion)",
            (Asientos_704ILR("Reservas", "Modificacion rechazada") - asientosModRech_704ILR) + "," +
            (Asientos_704ILR("Reservas", "Cancelacion rechazada") - asientosCanRech_704ILR) + "," +
            (Asientos_704ILR("Reservas", "Restauracion rechazada") - asientosResRech_704ILR), "1,1," + (versionPrevia_704ILR != null ? 1 : 0));
        Esperar_704ILR("asiento del inicio de la ejecucion", Asientos_704ILR("Coordinacion", "Inicio de ejecucion") - asientosInicio_704ILR, 1);

        // Incidencias.
        int asientosInc_704ILR = Asientos_704ILR("Coordinacion", "Registro de incidencia");
        Esperar_704ILR("incidencia sin descripcion", BLL_Incidencia_704ILR.Registrar_704ILR(Incidencia_704ILR("  ", null), out _), CoordinacionResult_704ILR.DescripcionInvalida_704ILR);
        Esperar_704ILR("incidencia con una descripcion que no entra en su columna", BLL_Incidencia_704ILR.Registrar_704ILR(Incidencia_704ILR(new string('i', 501), null), out _), CoordinacionResult_704ILR.DescripcionInvalida_704ILR);
        Esperar_704ILR("incidencia informada por quien no es del equipo", BLL_Incidencia_704ILR.Registrar_704ILR(Incidencia_704ILR("Falla de sonido", empB_704ILR), out _), CoordinacionResult_704ILR.EmpleadoInvalido_704ILR);
        Esperar_704ILR("incidencia informada por A", BLL_Incidencia_704ILR.Registrar_704ILR(Incidencia_704ILR("Falla de sonido en la pista", empA_704ILR), out int inc1_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("incidencia observada por el supervisor", BLL_Incidencia_704ILR.Registrar_704ILR(Incidencia_704ILR("Demora en el servicio de la cena", null), out int inc2_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("asientos de incidencia", Asientos_704ILR("Coordinacion", "Registro de incidencia") - asientosInc_704ILR, 2);
        var incidencias_704ILR = BLL_Incidencia_704ILR.GetByReserva_704ILR(evR1_704ILR);
        Esperar_704ILR("incidencias del evento", incidencias_704ILR.Count, 2);
        Esperar_704ILR("nacen abiertas", incidencias_704ILR.All(i_704ILR => i_704ILR.Estado_704ILR == EvenTech.BE.EstadoIncidencia_704ILR.ABIERTA), true);
        Esperar_704ILR("tipo guardado", incidencias_704ILR[0].Tipo_704ILR, EvenTech.BE.TipoIncidencia_704ILR.EQUIPAMIENTO);

        // Cierre: exige todas las incidencias resueltas.
        Esperar_704ILR("cerrar con incidencias abiertas", BLL_Coordinacion_704ILR.CerrarEvento_704ILR(evR1_704ILR), CoordinacionResult_704ILR.IncidenciasAbiertas_704ILR);
        Esperar_704ILR("resolver sin decir como", BLL_Incidencia_704ILR.Resolver_704ILR(inc1_704ILR, " "), CoordinacionResult_704ILR.ResolucionObligatoria_704ILR);
        Esperar_704ILR("resolver una incidencia inexistente", BLL_Incidencia_704ILR.Resolver_704ILR(int.MaxValue, "x"), CoordinacionResult_704ILR.IncidenciaInvalida_704ILR);
        Esperar_704ILR("resolver la primera", BLL_Incidencia_704ILR.Resolver_704ILR(inc1_704ILR, "Se reemplazo el cable"), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("resolver la primera otra vez", BLL_Incidencia_704ILR.Resolver_704ILR(inc1_704ILR, "Otra vez"), CoordinacionResult_704ILR.IncidenciaYaResuelta_704ILR);
        Esperar_704ILR("cerrar con una abierta", BLL_Coordinacion_704ILR.CerrarEvento_704ILR(evR1_704ILR), CoordinacionResult_704ILR.IncidenciasAbiertas_704ILR);
        Esperar_704ILR("resolver la segunda", BLL_Incidencia_704ILR.Resolver_704ILR(inc2_704ILR, "Se sumo un mozo al servicio"), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("resolucion guardada", BLL_Incidencia_704ILR.GetByReserva_704ILR(evR1_704ILR).First(i_704ILR => i_704ILR.Id_704ILR == inc1_704ILR).Resolucion_704ILR, "Se reemplazo el cable");
        Esperar_704ILR("cerrar el evento", BLL_Coordinacion_704ILR.CerrarEvento_704ILR(evR1_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("estado de coordinacion", EstadoCoord_704ILR(evR1_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.CERRADO);

        // CERRADO es terminal.
        Esperar_704ILR("incidencia en un evento cerrado", BLL_Incidencia_704ILR.Registrar_704ILR(Incidencia_704ILR("Tarde", null), out _), CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("iniciar un evento cerrado", BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(evR1_704ILR, true), CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("cerrar dos veces", BLL_Coordinacion_704ILR.CerrarEvento_704ILR(evR1_704ILR), CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("cancelar la reserva de un evento cerrado", BLL_Reserva_704ILR.Cancelar_704ILR(evR1_704ILR, out _, out _), ReservaResult_704ILR.EventoIniciado_704ILR);
        var cerrada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evR1_704ILR);
        cerrada_704ILR.CantidadInvitados_704ILR = 26;
        Esperar_704ILR("modificar la reserva de un evento cerrado", BLL_Reserva_704ILR.Actualizar_704ILR(cerrada_704ILR), ReservaResult_704ILR.EventoIniciado_704ILR);
        // El plan de un evento cerrado tampoco se toca.
        Esperar_704ILR("asignar personal a un evento cerrado",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR1_704ILR, empB_704ILR, "Barra", H_704ILR(20), H_704ILR(23), out _, out _), CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("quitar personal de un evento cerrado", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigA1_704ILR), CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("modificar el cronograma de un evento cerrado",
            BLL_Cronograma_704ILR.Guardar_704ILR(evR1_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR>
            { new EvenTech.BE.BE_CronogramaActividad_704ILR { Hora_704ILR = H_704ILR(22), Descripcion_704ILR = "Baile", ResponsableId_704ILR = empA_704ILR, DuracionMinutos_704ILR = 60 } }),
            CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("eliminar el cronograma de un evento cerrado", BLL_Cronograma_704ILR.Eliminar_704ILR(evR1_704ILR), CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("asignar una tarea en un evento cerrado",
            BLL_Tarea_704ILR.Asignar_704ILR(new EvenTech.BE.BE_Tarea_704ILR { ReservaId_704ILR = evR1_704ILR, EmpleadoId_704ILR = empA_704ILR, Descripcion_704ILR = "Extra", HoraInicio_704ILR = H_704ILR(22), HoraFin_704ILR = H_704ILR(23) }, out _),
            CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("el plan del evento cerrado quedo como estaba (asignaciones, actividades y tareas)",
            BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(evR1_704ILR).Count + "," + BLL_Cronograma_704ILR.GetByReserva_704ILR(evR1_704ILR)?.Actividades_704ILR.Count + "," + BLL_Tarea_704ILR.GetByReserva_704ILR(evR1_704ILR).Count, "1,2,2");

        Esperar_704ILR("asientos de rechazo de ejecucion (el evento que no estaba listo)", Asientos_704ILR("Coordinacion", "Ejecucion rechazada") - asientosNoListo_704ILR, 1);
        Esperar_704ILR("asientos de cierre rechazado (dos intentos con incidencias abiertas)", Asientos_704ILR("Coordinacion", "Cierre rechazado") - asientosCierreRech_704ILR, 2);
        Esperar_704ILR("asientos de resolucion de incidencia", Asientos_704ILR("Coordinacion", "Resolucion de incidencia") - asientosResol_704ILR, 2);
        Esperar_704ILR("asiento del cierre", Asientos_704ILR("Coordinacion", "Cierre de evento") - asientosCierre_704ILR, 1);
        // RN-13 asentada en cada rechazo: 2 antes de iniciar (incidencia y cierre), 6 con
        // el evento en ejecucion (iniciar otra vez, asignar, quitar, cronograma, tarea y
        // responder) y 8 con el evento cerrado (incidencia, iniciar, cerrar, asignar,
        // quitar, cronograma, eliminar cronograma y tarea).
        Esperar_704ILR("asientos de las operaciones rechazadas por RN-13", Asientos_704ILR("Coordinacion", "Coordinacion rechazada") - asientosRegla_704ILR, 16);
    }
}
catch (Exception ex48_704ILR) { Excepcion_704ILR("[48]", ex48_704ILR); }

// [49] Reprogramacion. Las confirmaciones del personal valen para una fecha y
// para una reserva firme: si la reserva cambia de dia o deja de estar confirmada,
// vuelven a PENDIENTE; y al confirmar de nuevo se vuelve a controlar la RN-09.
Caso_704ILR("[49] Reprogramacion del evento y vigencia de las confirmaciones:");
try
{
    var salRep_704ILR = BLL_Salon_704ILR.GetAll_704ILR();
    if (evR3_704ILR == 0 || asigA3_704ILR == 0 || salRep_704ILR.Count < 3 || BLL_Cliente_704ILR.GetAll_704ILR().Count == 0)
    {
        Omitir_704ILR("[49]", "depende de los eventos de [44] y hacen falta tres salones seed; corre db/schema.sql");
    }
    else
    {
        int cli_704ILR = BLL_Cliente_704ILR.GetAll_704ILR()[0].Id_704ILR;
        var unaActividad_704ILR = new List<EvenTech.BE.BE_CronogramaActividad_704ILR>
        { new EvenTech.BE.BE_CronogramaActividad_704ILR { Hora_704ILR = H_704ILR(2), Descripcion_704ILR = "Desarme del salon", ResponsableId_704ILR = empA_704ILR, DuracionMinutos_704ILR = 120 } };

        // R3 queda LISTO: A confirma y se genera el cronograma.
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A confirma su turno de R3", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA3_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();
        Esperar_704ILR("cronograma de R3", BLL_Cronograma_704ILR.Guardar_704ILR(evR3_704ILR, unaActividad_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("R3 listo", EstadoCoord_704ILR(evR3_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);
        Esperar_704ILR("tarea de A en R3 (02:00-03:00)",
            BLL_Tarea_704ILR.Asignar_704ILR(new EvenTech.BE.BE_Tarea_704ILR { ReservaId_704ILR = evR3_704ILR, EmpleadoId_704ILR = empA_704ILR, Descripcion_704ILR = "Desarmar la pista", HoraInicio_704ILR = H_704ILR(2), HoraFin_704ILR = H_704ILR(3) }, out _),
            CoordinacionResult_704ILR.Success_704ILR);

        // Un cambio que no toca la fecha conserva las confirmaciones.
        int asientosReinicio_704ILR = Asientos_704ILR("Coordinacion", "Confirmaciones reiniciadas");
        var mismoDia_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evR3_704ILR);
        mismoDia_704ILR.CantidadInvitados_704ILR = 30;
        Esperar_704ILR("cambiar los invitados de R3", BLL_Reserva_704ILR.Actualizar_704ILR(mismoDia_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("el turno sigue confirmado", Asignacion_704ILR(evR3_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.CONFIRMADA);
        Esperar_704ILR("R3 sigue listo", EstadoCoord_704ILR(evR3_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);

        // Cambiar el dia del evento reinicia las confirmaciones.
        var otroDia_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evR3_704ILR);
        otroDia_704ILR.FechaEvento_704ILR = otroDia_704ILR.FechaEvento_704ILR.AddDays(9);
        Esperar_704ILR("reprogramar R3", BLL_Reserva_704ILR.Actualizar_704ILR(otroDia_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("el turno vuelve a pendiente", Asignacion_704ILR(evR3_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        Esperar_704ILR("sin fecha de respuesta", Asignacion_704ILR(evR3_704ILR, empA_704ILR)?.FechaConfirmacion_704ILR == null, true);
        Esperar_704ILR("R3 vuelve a coordinacion", EstadoCoord_704ILR(evR3_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("el cronograma se conserva", BLL_Cronograma_704ILR.GetByReserva_704ILR(evR3_704ILR)?.Actividades_704ILR.Count, (int?)1);
        Esperar_704ILR("asiento de confirmaciones reiniciadas", Asientos_704ILR("Coordinacion", "Confirmaciones reiniciadas") - asientosReinicio_704ILR, 1);
        Esperar_704ILR("iniciar un evento que dejo de estar listo", BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(evR3_704ILR, true), CoordinacionResult_704ILR.NoListo_704ILR);

        // A rechaza el turno reprogramado. Volver a ofrecerselo con otra franja no puede
        // dejar afuera la tarea que ya tiene en el evento (RN-12).
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A rechaza el turno reprogramado de R3", BLL_AsignacionPersonal_704ILR.Rechazar_704ILR(asigA3_704ILR, "Ese dia no puedo"), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();
        int asientosReoferta_704ILR = Asientos_704ILR("Coordinacion", "Asignacion rechazada");
        Esperar_704ILR("volver a ofrecer el turno con una franja que deja afuera su tarea",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR3_704ILR, empA_704ILR, "Desarme", H_704ILR(4), H_704ILR(6), out _, out _), CoordinacionResult_704ILR.TieneCarga_704ILR);
        Esperar_704ILR("el turno sigue rechazado", Asignacion_704ILR(evR3_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.RECHAZADA);
        Esperar_704ILR("asiento del rechazo de la reoferta (RN-12)", Asientos_704ILR("Coordinacion", "Asignacion rechazada") - asientosReoferta_704ILR, 1);
        Esperar_704ILR("el asiento nombra la regla",
            Bitacora_704ILR("Coordinacion", "Asignacion rechazada").FirstOrDefault()?.Detalle_704ILR.Contains("RN-12"), (bool?)true);
        Esperar_704ILR("volver a ofrecerlo con la franja que contiene la tarea",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR3_704ILR, empA_704ILR, "Desarme", H_704ILR(2), H_704ILR(6), out _, out _), CoordinacionResult_704ILR.Success_704ILR);

        // RN-09 al confirmar. R4 nace otro dia, con A confirmado de 12:00 a 16:00, y se
        // reprograma al dia de R2, donde A tiene pendiente un turno de 11:00 a 17:00: los
        // dos turnos quedan pendientes y pisados, y solo uno se puede confirmar.
        int evR4_704ILR = EventoConfirmado_704ILR(cli_704ILR, salRep_704ILR[2].Id_704ILR, 8004);
        Esperar_704ILR("evento R4 confirmado", evR4_704ILR > 0, true);
        Esperar_704ILR("asignar A a R4 12:00-16:00", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR4_704ILR, empA_704ILR, "Armado", H_704ILR(12), H_704ILR(16), out int asigA4_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A confirma su turno de R4", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA4_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();

        var alDiaDeR2_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evR4_704ILR);
        alDiaDeR2_704ILR.FechaEvento_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evR2_704ILR).FechaEvento_704ILR;
        Esperar_704ILR("reprogramar R4 al dia de R2", BLL_Reserva_704ILR.Actualizar_704ILR(alDiaDeR2_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("el turno de R4 vuelve a pendiente", Asignacion_704ILR(evR4_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);

        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A confirma su turno de R2 (11:00-17:00)", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA2_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("A confirma su turno de R4 (se pisa con el de R2)",
            BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA4_704ILR, out var conflictoConf_704ILR), CoordinacionResult_704ILR.Superposicion_704ILR);
        Esperar_704ILR("turno con el que se pisa", conflictoConf_704ILR?.ReservaId_704ILR, (int?)evR2_704ILR);
        Esperar_704ILR("A rechaza el turno de R4", BLL_AsignacionPersonal_704ILR.Rechazar_704ILR(asigA4_704ILR, "Ese dia ya tengo otro turno"), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();

        // Una reserva que deja de estar confirmada (se le repone la version en
        // cotizacion) tambien reinicia las confirmaciones, y deja de coordinarse (RN-08).
        var versionCot_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(evR2_704ILR)
            .FirstOrDefault(v_704ILR => v_704ILR.Estado_704ILR == EvenTech.BE.EstadoReserva_704ILR.COTIZACION);
        Esperar_704ILR("R2 tiene una version en cotizacion", versionCot_704ILR != null, true);
        if (versionCot_704ILR != null)
        {
            Esperar_704ILR("restaurar R2 a la version en cotizacion", BLL_Reserva_704ILR.RestaurarVersion_704ILR(evR2_704ILR, versionCot_704ILR.Id_704ILR), ReservaResult_704ILR.Success_704ILR);
            Esperar_704ILR("R2 ya no esta confirmada", BLL_Reserva_704ILR.GetById_704ILR(evR2_704ILR).Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.COTIZACION);
            Esperar_704ILR("el turno de R2 vuelve a pendiente", Asignacion_704ILR(evR2_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
            // Una reserva que deja de estar confirmada ya no se coordina: el asiento dice
            // que el personal queda liberado, no que tiene que responder de nuevo.
            Esperar_704ILR("el asiento informa que el personal queda liberado",
                Bitacora_704ILR("Coordinacion", "Confirmaciones reiniciadas").FirstOrDefault()?.Detalle_704ILR.Contains("queda liberado"), (bool?)true);
            Esperar_704ILR("asignar en R2, que ya no esta confirmada (RN-08)",
                BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR2_704ILR, empB_704ILR, "Barra", H_704ILR(11), H_704ILR(17), out _, out _), CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR);
            Esperar_704ILR("R2 ya no figura entre los eventos a coordinar",
                BLL_Coordinacion_704ILR.GetEventos_704ILR().Any(e_704ILR => e_704ILR.ReservaId_704ILR == evR2_704ILR), false);
            Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
            Esperar_704ILR("responder un turno de una reserva que ya no esta confirmada (RN-08)",
                BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA2_704ILR, out _), CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR);
            Esperar_704ILR("la agenda no trae turnos de reservas sin confirmar",
                BLL_AsignacionPersonal_704ILR.GetMisAsignaciones_704ILR().Any(a_704ILR => a_704ILR.ReservaId_704ILR == evR2_704ILR), false);
            CerrarSesion_704ILR();
            // El turno de una reserva que no esta confirmada no compromete al empleado:
            // ahora el de R4 (rechazado) se le puede volver a ofrecer en la misma franja.
            Esperar_704ILR("reasignar a A en R4 con R2 fuera de coordinacion",
                BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR4_704ILR, empA_704ILR, "Armado", H_704ILR(12), H_704ILR(16), out _, out _), CoordinacionResult_704ILR.Success_704ILR);
        }

        // Un turno rechazado no compromete al empleado (RN-09), y cancelar una reserva
        // libera a su equipo. R5 y R6 son dos eventos del mismo dia en salones distintos.
        int evR5_704ILR = EventoConfirmado_704ILR(cli_704ILR, salRep_704ILR[0].Id_704ILR, 8030);
        int evR6_704ILR = EventoConfirmado_704ILR(cli_704ILR, salRep_704ILR[1].Id_704ILR, 8030);
        Esperar_704ILR("eventos R5 y R6 confirmados", evR5_704ILR > 0 && evR6_704ILR > 0, true);
        Esperar_704ILR("asignar A a R5 09:00-13:00", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR5_704ILR, empA_704ILR, "Armado", H_704ILR(9), H_704ILR(13), out int asigA5_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("A en R6 10:00-12:00 con el turno de R5 pendiente (se pisa)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR6_704ILR, empA_704ILR, "Armado", H_704ILR(10), H_704ILR(12), out _, out _), CoordinacionResult_704ILR.Superposicion_704ILR);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A rechaza el turno de R5", BLL_AsignacionPersonal_704ILR.Rechazar_704ILR(asigA5_704ILR, "No llego a tiempo"), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();
        Esperar_704ILR("A en R6 10:00-12:00 con el turno de R5 rechazado (RN-09 no lo cuenta)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR6_704ILR, empA_704ILR, "Armado", H_704ILR(10), H_704ILR(12), out int asigA6_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A confirma su turno de R6", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA6_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();
        Esperar_704ILR("cronograma de R6", BLL_Cronograma_704ILR.Guardar_704ILR(evR6_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR>
            { new EvenTech.BE.BE_CronogramaActividad_704ILR { Hora_704ILR = H_704ILR(10), Descripcion_704ILR = "Armado del salon", ResponsableId_704ILR = empA_704ILR, DuracionMinutos_704ILR = 60 } }),
            CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("R6 listo", EstadoCoord_704ILR(evR6_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);

        int asientosLiberado_704ILR = Asientos_704ILR("Coordinacion", "Confirmaciones reiniciadas");
        Esperar_704ILR("cancelar R6 con el equipo confirmado", BLL_Reserva_704ILR.Cancelar_704ILR(evR6_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("el turno de la reserva cancelada vuelve a pendiente", Asignacion_704ILR(evR6_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        Esperar_704ILR("la reserva cancelada deja de figurar lista", EstadoCoord_704ILR(evR6_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("asiento del personal liberado por la cancelacion", Asientos_704ILR("Coordinacion", "Confirmaciones reiniciadas") - asientosLiberado_704ILR, 1);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("la agenda no trae el turno de la reserva cancelada",
            BLL_AsignacionPersonal_704ILR.GetMisAsignaciones_704ILR().Any(a_704ILR => a_704ILR.ReservaId_704ILR == evR6_704ILR), false);
        CerrarSesion_704ILR();
        Esperar_704ILR("volver a ofrecer a A el turno de R5 (el de R6 ya no lo compromete)",
            BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR5_704ILR, empA_704ILR, "Armado", H_704ILR(9), H_704ILR(13), out _, out _), CoordinacionResult_704ILR.Success_704ILR);

        // RN-08 al restaurar: reponer una version CONFIRMADA con OTRA fecha tambien cambia
        // el dia del evento, asi que las confirmaciones se reinician y el personal tiene
        // que responder de nuevo (la reserva sigue confirmada: no queda liberado).
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A confirma su turno de R3 reprogramado", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA3_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();
        DateTime fechaR3_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evR3_704ILR).FechaEvento_704ILR.Date;
        var versionOtraFecha_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(evR3_704ILR)
            .FirstOrDefault(v_704ILR => v_704ILR.Estado_704ILR == EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA && v_704ILR.FechaEvento_704ILR.Date != fechaR3_704ILR);
        Esperar_704ILR("R3 tiene una version confirmada con otra fecha", versionOtraFecha_704ILR != null, true);
        int asientosRestaura_704ILR = Asientos_704ILR("Coordinacion", "Confirmaciones reiniciadas");
        Esperar_704ILR("restaurar R3 a la version confirmada con la fecha anterior",
            versionOtraFecha_704ILR == null ? (ReservaResult_704ILR?)null : BLL_Reserva_704ILR.RestaurarVersion_704ILR(evR3_704ILR, versionOtraFecha_704ILR.Id_704ILR),
            (ReservaResult_704ILR?)ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("R3 sigue confirmada", BLL_Reserva_704ILR.GetById_704ILR(evR3_704ILR).Estado_704ILR, EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA);
        Esperar_704ILR("el turno de R3 vuelve a pendiente", Asignacion_704ILR(evR3_704ILR, empA_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        Esperar_704ILR("asiento de las confirmaciones reiniciadas por la restauracion", Asientos_704ILR("Coordinacion", "Confirmaciones reiniciadas") - asientosRestaura_704ILR, 1);
        Esperar_704ILR("el asiento pide una nueva respuesta",
            Bitacora_704ILR("Coordinacion", "Confirmaciones reiniciadas").FirstOrDefault()?.Detalle_704ILR.Contains("responder de nuevo"), (bool?)true);
    }
}
catch (Exception ex49_704ILR) { Excepcion_704ILR("[49]", ex49_704ILR); CerrarSesion_704ILR(); }

// [50] Cronograma de un evento reprogramado. RN-11 exige el equipo sin respuestas
// pendientes para GENERAR el cronograma; uno ya generado se puede seguir modificando,
// siempre con responsables confirmados. Es lo que destraba un evento reprogramado
// cuando un integrante con actividades a cargo no vuelve a responder: sus actividades
// pasan a otro integrante confirmado y recien entonces se lo quita del equipo.
Caso_704ILR("[50] Cronograma de un evento reprogramado (RN-08, RN-11):");
try
{
    if (empA_704ILR == 0 || empB_704ILR == 0 || salones_704ILR.Count == 0 || clientes_704ILR.Count == 0)
    {
        Omitir_704ILR("[50]", "faltan los empleados de [43] o los catalogos seed; corre db/schema.sql");
    }
    else
    {
        int cli_704ILR = clientes_704ILR[0].Id_704ILR;
        List<EvenTech.BE.BE_CronogramaActividad_704ILR> Jornada_704ILR(int primero_704ILR, int segundo_704ILR) => new List<EvenTech.BE.BE_CronogramaActividad_704ILR>
        {
            new EvenTech.BE.BE_CronogramaActividad_704ILR { Hora_704ILR = H_704ILR(20), Descripcion_704ILR = "Recepcion", ResponsableId_704ILR = primero_704ILR, DuracionMinutos_704ILR = 60 },
            new EvenTech.BE.BE_CronogramaActividad_704ILR { Hora_704ILR = H_704ILR(21), Descripcion_704ILR = "Cena", ResponsableId_704ILR = segundo_704ILR, DuracionMinutos_704ILR = 60 }
        };

        // RN-11: sin ningun confirmado no hay cronograma, aunque no quede ninguna respuesta
        // pendiente (el unico integrante rechazo el turno).
        int evR7_704ILR = EventoConfirmado_704ILR(cli_704ILR, salones_704ILR[0].Id_704ILR, 8050);
        Esperar_704ILR("evento R7 confirmado", evR7_704ILR > 0, true);
        Esperar_704ILR("asignar A a R7", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR7_704ILR, empA_704ILR, "Mozo", H_704ILR(20), H_704ILR(23), out int asigA7_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A rechaza el turno de R7", BLL_AsignacionPersonal_704ILR.Rechazar_704ILR(asigA7_704ILR, "Ese dia no puedo"), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();
        int asientosSinEquipo_704ILR = Asientos_704ILR("Coordinacion", "Cronograma rechazado");
        Esperar_704ILR("generar sin ningun confirmado y sin pendientes (RN-11)", BLL_Cronograma_704ILR.Guardar_704ILR(evR7_704ILR, Jornada_704ILR(empA_704ILR, empA_704ILR)), CoordinacionResult_704ILR.PersonalSinConfirmar_704ILR);
        Esperar_704ILR("asiento del rechazo RN-11", Asientos_704ILR("Coordinacion", "Cronograma rechazado") - asientosSinEquipo_704ILR, 1);

        // Un rechazo sin resolver no impide generar el cronograma con los confirmados,
        // pero el evento no queda LISTO hasta que el coordinador lo resuelve.
        Esperar_704ILR("asignar B a R7", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR7_704ILR, empB_704ILR, "Barra", H_704ILR(20), H_704ILR(23), out int asigB7_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("la cuenta de prueba pasa a representar a B", VincularCuenta_704ILR(empA_704ILR, empB_704ILR), true);
        Esperar_704ILR("sesion del empleado B", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("B confirma su turno de R7", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigB7_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();
        Esperar_704ILR("generar con B confirmado y A rechazado", BLL_Cronograma_704ILR.Guardar_704ILR(evR7_704ILR, Jornada_704ILR(empB_704ILR, empB_704ILR)), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("con un rechazo sin resolver el evento sigue en coordinacion", EstadoCoord_704ILR(evR7_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("quitar al integrante que rechazo", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigA7_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("resuelto el rechazo, el evento queda listo", EstadoCoord_704ILR(evR7_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);
        Esperar_704ILR("la cuenta de prueba vuelve a representar a A", VincularCuenta_704ILR(empB_704ILR, empA_704ILR), true);

        // El evento se reprograma: la confirmacion de B se reinicia y B no vuelve a
        // responder. A se suma al equipo y confirma.
        var reprogramada_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evR7_704ILR);
        reprogramada_704ILR.FechaEvento_704ILR = reprogramada_704ILR.FechaEvento_704ILR.AddDays(3);
        Esperar_704ILR("reprogramar R7", BLL_Reserva_704ILR.Actualizar_704ILR(reprogramada_704ILR), ReservaResult_704ILR.Success_704ILR);
        Esperar_704ILR("el turno de B vuelve a pendiente", Asignacion_704ILR(evR7_704ILR, empB_704ILR)?.Estado_704ILR, (EvenTech.BE.EstadoAsignacion_704ILR?)EvenTech.BE.EstadoAsignacion_704ILR.PENDIENTE);
        Esperar_704ILR("asignar A a R7 reprogramado", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR7_704ILR, empA_704ILR, "Mozo", H_704ILR(20), H_704ILR(23), out int asigA7b_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A confirma su turno de R7", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigA7b_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();

        // B sigue pendiente y tiene las dos actividades a cargo: no se lo puede quitar...
        Esperar_704ILR("quitar a B, pendiente y con actividades a cargo", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigB7_704ILR), CoordinacionResult_704ILR.TieneCarga_704ILR);
        // ...pero el cronograma ya generado se puede modificar con esa respuesta pendiente,
        // siempre que cada responsable sea personal confirmado.
        int asientosModCro_704ILR = Asientos_704ILR("Coordinacion", "Modificacion de cronograma");
        Esperar_704ILR("modificar dejando una actividad a cargo del pendiente (RN-11)", BLL_Cronograma_704ILR.Guardar_704ILR(evR7_704ILR, Jornada_704ILR(empA_704ILR, empB_704ILR)), CoordinacionResult_704ILR.ResponsableInvalido_704ILR);
        Esperar_704ILR("pasar las actividades al integrante confirmado", BLL_Cronograma_704ILR.Guardar_704ILR(evR7_704ILR, Jornada_704ILR(empA_704ILR, empA_704ILR)), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("asiento de la modificacion del cronograma", Asientos_704ILR("Coordinacion", "Modificacion de cronograma") - asientosModCro_704ILR, 1);
        Esperar_704ILR("con B pendiente el evento sigue en coordinacion", EstadoCoord_704ILR(evR7_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_COORDINACION);
        Esperar_704ILR("quitar a B, ya sin carga", BLL_AsignacionPersonal_704ILR.Quitar_704ILR(asigB7_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("el evento reprogramado vuelve a quedar listo", EstadoCoord_704ILR(evR7_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);

        // Generar (a diferencia de modificar) sigue exigiendo que no haya respuestas pendientes.
        Esperar_704ILR("eliminar el cronograma de R7", BLL_Cronograma_704ILR.Eliminar_704ILR(evR7_704ILR), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("asignar B a R7 otra vez (pendiente)", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evR7_704ILR, empB_704ILR, "Barra", H_704ILR(20), H_704ILR(23), out _, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("generar de nuevo con una respuesta pendiente (RN-11)", BLL_Cronograma_704ILR.Guardar_704ILR(evR7_704ILR, Jornada_704ILR(empA_704ILR, empA_704ILR)), CoordinacionResult_704ILR.PersonalSinConfirmar_704ILR);
    }
}
catch (Exception ex50_704ILR) { Excepcion_704ILR("[50]", ex50_704ILR); CerrarSesion_704ILR(); }

// [51] RN-13 el dia del evento. Iniciar la ejecucion en la fecha del evento no exige
// confirmacion y se asienta como informacion (fuera de fecha, como advertencia: [48]).
// Con el evento en ejecucion o cerrado tampoco se quitan tareas, no se elimina el
// cronograma, no se rechaza un turno, no se resuelve una incidencia ni se restaura una
// version; los movimientos de cobro (cobrar y anular) siguen admitidos. De paso, RN-07:
// el unico pago de una reserva confirmada no se anula.
Caso_704ILR("[51] RN-13 el dia del evento: inicio sin confirmacion y plan congelado:");
try
{
    var libresHoy_704ILR = clientes_704ILR.Count == 0 ? null
        : BLL_Disponibilidad_704ILR.Consultar_704ILR(DateTime.Today, 0).Where(d_704ILR => d_704ILR.Libre_704ILR).ToList();
    if (empA_704ILR == 0 || libresHoy_704ILR == null || libresHoy_704ILR.Count == 0)
    {
        Omitir_704ILR("[51]", "hacen falta el empleado de [43], un cliente seed y un salon libre en la fecha de hoy");
    }
    else
    {
        var reservaHoy_704ILR = new EvenTech.BE.BE_Reserva_704ILR
        {
            ClienteId_704ILR = clientes_704ILR[0].Id_704ILR,
            SalonId_704ILR = libresHoy_704ILR[0].SalonId_704ILR,
            FechaEvento_704ILR = DateTime.Today,
            Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.COTIZACION,
            CantidadInvitados_704ILR = 20,
            Monto_704ILR = 900m
        };
        Esperar_704ILR("alta de una reserva para hoy", BLL_Reserva_704ILR.Crear_704ILR(reservaHoy_704ILR, out int evHoy_704ILR), ReservaResult_704ILR.Success_704ILR);
        Anotar_704ILR(evHoy_704ILR);
        int adelantoHoy_704ILR = Adelanto_704ILR(evHoy_704ILR, 300m);
        var confirmarHoy_704ILR = BLL_Reserva_704ILR.GetById_704ILR(evHoy_704ILR);
        confirmarHoy_704ILR.Estado_704ILR = EvenTech.BE.EstadoReserva_704ILR.CONFIRMADA;
        Esperar_704ILR("confirmar la reserva de hoy", BLL_Reserva_704ILR.Actualizar_704ILR(confirmarHoy_704ILR), ReservaResult_704ILR.Success_704ILR);
        // RN-07: una reserva confirmada no puede quedar sin adelanto.
        Esperar_704ILR("anular el unico pago de una reserva confirmada (RN-07)",
            BLL_Pago_704ILR.Eliminar_704ILR(adelantoHoy_704ILR, evHoy_704ILR), PagoResult_704ILR.ConfirmadaSinAdelanto_704ILR);

        Esperar_704ILR("asignar A al evento de hoy", BLL_AsignacionPersonal_704ILR.Asignar_704ILR(evHoy_704ILR, empA_704ILR, "Mozo", H_704ILR(20), H_704ILR(23), out int asigHoy_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("A confirma su turno de hoy", BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(asigHoy_704ILR, out _), CoordinacionResult_704ILR.Success_704ILR);
        CerrarSesion_704ILR();
        Esperar_704ILR("cronograma del evento de hoy", BLL_Cronograma_704ILR.Guardar_704ILR(evHoy_704ILR, new List<EvenTech.BE.BE_CronogramaActividad_704ILR>
            { new EvenTech.BE.BE_CronogramaActividad_704ILR { Hora_704ILR = H_704ILR(20), Descripcion_704ILR = "Recepcion", ResponsableId_704ILR = empA_704ILR, DuracionMinutos_704ILR = 60 } }),
            CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("tarea del evento de hoy",
            BLL_Tarea_704ILR.Asignar_704ILR(new EvenTech.BE.BE_Tarea_704ILR { ReservaId_704ILR = evHoy_704ILR, EmpleadoId_704ILR = empA_704ILR, Descripcion_704ILR = "Recibir a los invitados", HoraInicio_704ILR = H_704ILR(20), HoraFin_704ILR = H_704ILR(21) }, out int tareaHoy_704ILR),
            CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("evento de hoy listo", EstadoCoord_704ILR(evHoy_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.LISTO);

        int asientosInicioHoy_704ILR = Asientos_704ILR("Coordinacion", "Inicio de ejecucion");
        Esperar_704ILR("iniciar el dia del evento, sin confirmacion", BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(evHoy_704ILR, false), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("estado de coordinacion", EstadoCoord_704ILR(evHoy_704ILR), EvenTech.BE.EstadoCoordinacion_704ILR.EN_EJECUCION);
        Esperar_704ILR("asiento del inicio", Asientos_704ILR("Coordinacion", "Inicio de ejecucion") - asientosInicioHoy_704ILR, 1);
        Esperar_704ILR("iniciar en fecha se asienta como informacion",
            Bitacora_704ILR("Coordinacion", "Inicio de ejecucion").FirstOrDefault()?.Criticidad_704ILR,
            (EvenTech.BE.CriticidadBitacora_704ILR?)EvenTech.BE.CriticidadBitacora_704ILR.Info);

        // Plan congelado: las operaciones que [48] no recorre.
        int asientosRn13_704ILR = Asientos_704ILR("Coordinacion", "Coordinacion rechazada");
        Esperar_704ILR("quitar una tarea con el evento en ejecucion", BLL_Tarea_704ILR.Quitar_704ILR(tareaHoy_704ILR), CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
        Esperar_704ILR("eliminar el cronograma con el evento en ejecucion", BLL_Cronograma_704ILR.Eliminar_704ILR(evHoy_704ILR), CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
        Esperar_704ILR("sesion del empleado A", SesionDelEmpleado_704ILR(), true);
        Esperar_704ILR("rechazar un turno con el evento en ejecucion", BLL_AsignacionPersonal_704ILR.Rechazar_704ILR(asigHoy_704ILR, "Llego tarde"), CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
        CerrarSesion_704ILR();

        // Los movimientos de cobro siguen admitidos: se cobra y se anula.
        int cobroEnEjecucion_704ILR = Adelanto_704ILR(evHoy_704ILR, 100m);
        Esperar_704ILR("cobrar con el evento en ejecucion", cobroEnEjecucion_704ILR > 0, true);
        Esperar_704ILR("anular ese cobro con el evento en ejecucion", BLL_Pago_704ILR.Eliminar_704ILR(cobroEnEjecucion_704ILR, evHoy_704ILR), PagoResult_704ILR.Success_704ILR);

        Esperar_704ILR("incidencia del evento de hoy",
            BLL_Incidencia_704ILR.Registrar_704ILR(new EvenTech.BE.BE_Incidencia_704ILR { ReservaId_704ILR = evHoy_704ILR, Tipo_704ILR = EvenTech.BE.TipoIncidencia_704ILR.OTRO, Descripcion_704ILR = "Un invitado llego antes de hora" }, out int incHoy_704ILR),
            CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("resolver la incidencia", BLL_Incidencia_704ILR.Resolver_704ILR(incHoy_704ILR, "Se lo ubico en la recepcion"), CoordinacionResult_704ILR.Success_704ILR);
        Esperar_704ILR("cerrar el evento de hoy", BLL_Coordinacion_704ILR.CerrarEvento_704ILR(evHoy_704ILR), CoordinacionResult_704ILR.Success_704ILR);

        Esperar_704ILR("quitar una tarea de un evento cerrado", BLL_Tarea_704ILR.Quitar_704ILR(tareaHoy_704ILR), CoordinacionResult_704ILR.EventoCerrado_704ILR);
        Esperar_704ILR("resolver una incidencia de un evento cerrado", BLL_Incidencia_704ILR.Resolver_704ILR(incHoy_704ILR, "Otra vez"), CoordinacionResult_704ILR.EventoCerrado_704ILR);
        var versionHoy_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(evHoy_704ILR).FirstOrDefault();
        Esperar_704ILR("restaurar una version con el evento cerrado",
            versionHoy_704ILR == null ? (ReservaResult_704ILR?)null : BLL_Reserva_704ILR.RestaurarVersion_704ILR(evHoy_704ILR, versionHoy_704ILR.Id_704ILR),
            (ReservaResult_704ILR?)ReservaResult_704ILR.EventoIniciado_704ILR);
        int cobroCerrado_704ILR = Adelanto_704ILR(evHoy_704ILR, 50m);
        Esperar_704ILR("cobrar con el evento cerrado", cobroCerrado_704ILR > 0, true);
        Esperar_704ILR("anular ese cobro con el evento cerrado", BLL_Pago_704ILR.Eliminar_704ILR(cobroCerrado_704ILR, evHoy_704ILR), PagoResult_704ILR.Success_704ILR);
        // 3 con el evento en ejecucion (tarea, cronograma y turno) y 2 con el evento cerrado
        // (tarea e incidencia).
        Esperar_704ILR("asientos de las operaciones rechazadas por RN-13", Asientos_704ILR("Coordinacion", "Coordinacion rechazada") - asientosRn13_704ILR, 5);
        Esperar_704ILR("el plan del evento cerrado quedo como estaba (asignaciones, actividades y tareas)",
            BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(evHoy_704ILR).Count + "," + BLL_Cronograma_704ILR.GetByReserva_704ILR(evHoy_704ILR)?.Actividades_704ILR.Count + "," + BLL_Tarea_704ILR.GetByReserva_704ILR(evHoy_704ILR).Count, "1,1,1");
    }
}
catch (Exception ex51_704ILR) { Excepcion_704ILR("[51]", ex51_704ILR); CerrarSesion_704ILR(); }

// ---------------------------------------------------------------------------
// Limpieza final: lo que cada caso no alcanzo a limpiar (por una excepcion en el
// medio) se cancela o se borra aca, con asercion. Las reservas de prueba quedan
// CANCELADAS (la aplicacion no borra reservas: es el rastro que el negocio
// conserva); el usuario, los perfiles, el cliente y el idioma de prueba se
// eliminan. Al final se informa cuanto residuo dejo la corrida, para depurar la
// base de demostracion antes de regenerar el respaldo.
// ---------------------------------------------------------------------------
Caso_704ILR("[limpieza] Rastro de la corrida:");
try
{
    // Primero el Proceso 2: un evento en ejecucion o cerrado congela su reserva y
    // los empleados de prueba referencian al usuario de prueba.
    CerrarSesion_704ILR();
    // En su propio bloque: si falla, el resto de la limpieza (reservas, perfiles,
    // cliente, idioma y usuario) se ejecuta igual.
    try { BorrarCoordinacionDePrueba_704ILR(); }
    catch (Exception exCoordinacion_704ILR) { Excepcion_704ILR("[limpieza] coordinacion", exCoordinacion_704ILR); }
    Esperar_704ILR("empleados de prueba restantes",
        Escalar_704ILR("SELECT COUNT(*) FROM dbo.Empleados WHERE Apellido = @ap", ("@ap", "Smoke" + suf_704ILR)), 0);
    Esperar_704ILR("filas de coordinacion de la corrida restantes",
        reservasDeLaCorrida_704ILR.Count == 0 ? 0 : Escalar_704ILR(
            "SELECT (SELECT COUNT(*) FROM dbo.AsignacionesPersonal WHERE ReservaId IN (" + string.Join(",", reservasDeLaCorrida_704ILR) + ")) + " +
            "(SELECT COUNT(*) FROM dbo.Cronogramas WHERE ReservaId IN (" + string.Join(",", reservasDeLaCorrida_704ILR) + ")) + " +
            "(SELECT COUNT(*) FROM dbo.Incidencias WHERE ReservaId IN (" + string.Join(",", reservasDeLaCorrida_704ILR) + ")) + " +
            "(SELECT COUNT(*) FROM dbo.Reservas WHERE EstadoCoordinacion <> 'SIN_ASIGNAR' AND Id IN (" + string.Join(",", reservasDeLaCorrida_704ILR) + "))"), 0);

    int vivas_704ILR = 0;
    foreach (int id_704ILR in reservasDeLaCorrida_704ILR)
    {
        var r_704ILR = BLL_Reserva_704ILR.GetById_704ILR(id_704ILR);
        if (r_704ILR == null || r_704ILR.Estado_704ILR == EvenTech.BE.EstadoReserva_704ILR.CANCELADA) continue;
        vivas_704ILR++;
        Esperar_704ILR($"cancelar la reserva #{id_704ILR} que quedo {r_704ILR.Estado_704ILR}",
            BLL_Reserva_704ILR.Cancelar_704ILR(id_704ILR, out _, out _), ReservaResult_704ILR.Success_704ILR);
    }
    Console.WriteLine($"  reservas creadas por la corrida: {reservasDeLaCorrida_704ILR.Count}; sin cancelar por su caso: {vivas_704ILR}");
    Esperar_704ILR("reservas de la corrida vivas tras la limpieza",
        reservasDeLaCorrida_704ILR.Count(id_704ILR =>
        {
            var r_704ILR = BLL_Reserva_704ILR.GetById_704ILR(id_704ILR);
            return r_704ILR != null && r_704ILR.Estado_704ILR != EvenTech.BE.EstadoReserva_704ILR.CANCELADA;
        }), 0);

    foreach (int idPerfil_704ILR in perfilesDeLaCorrida_704ILR.ToList()) BorrarPerfilDePrueba_704ILR(idPerfil_704ILR);
    Esperar_704ILR("perfiles de prueba restantes",
        BLL_Perfil_704ILR.GetPerfiles_704ILR().Count(p_704ILR => p_704ILR.Nombre_704ILR.EndsWith(suf_704ILR, StringComparison.Ordinal)), 0);

    foreach (int idCliente_704ILR in clientesDeLaCorrida_704ILR.ToList()) BorrarClienteDePrueba_704ILR(idCliente_704ILR);
    Esperar_704ILR("clientes de prueba restantes",
        Escalar_704ILR("SELECT COUNT(*) FROM dbo.Clientes WHERE Apellido = @suf", ("@suf", suf_704ILR)), 0);

    if (idiomaDeLaCorrida_704ILR > 0) BorrarIdiomaDePrueba_704ILR(idiomaDeLaCorrida_704ILR);
    Esperar_704ILR("idiomas de prueba restantes",
        Escalar_704ILR("SELECT COUNT(*) FROM dbo.Idiomas WHERE Nombre = @n", ("@n", "Idioma smoke " + suf_704ILR)), 0);

    // El usuario de prueba se elimina (la aplicacion no da de baja usuarios). Sus
    // movimientos quedan en la auditoria de acceso, que no referencia la cuenta.
    Ejecutar_704ILR("DELETE FROM dbo.Users WHERE Username = @u", ("@u", newUser_704ILR));
    Esperar_704ILR("usuario de prueba eliminado",
        BLL_User_704ILR.GetAll_704ILR().Any(u_704ILR => u_704ILR.Username_704ILR == newUser_704ILR), false);

    // Residuo que queda a proposito (la base de demostracion se depura antes del
    // respaldo): reservas canceladas con sus lineas, pagos, versiones e historial,
    // mas los asientos de bitacora y de auditoria de acceso de la corrida.
    string ids_704ILR = reservasDeLaCorrida_704ILR.Count == 0 ? "0" : string.Join(",", reservasDeLaCorrida_704ILR);
    Console.WriteLine("  residuo de negocio que deja la corrida (ids " + ids_704ILR + "):");
    Console.WriteLine($"    Reservas canceladas: {Escalar_704ILR("SELECT COUNT(*) FROM dbo.Reservas WHERE Id IN (" + ids_704ILR + ")")}");
    Console.WriteLine($"    ReservaServicio: {Escalar_704ILR("SELECT COUNT(*) FROM dbo.ReservaServicio WHERE ReservaId IN (" + ids_704ILR + ")")}");
    Console.WriteLine($"    Pagos: {Escalar_704ILR("SELECT COUNT(*) FROM dbo.Pagos WHERE ReservaId IN (" + ids_704ILR + ")")}");
    Console.WriteLine($"    ReservaMemento: {Escalar_704ILR("SELECT COUNT(*) FROM dbo.ReservaMemento WHERE ReservaId IN (" + ids_704ILR + ")")}");
    Console.WriteLine($"    HistorialCambios: {Escalar_704ILR("SELECT COUNT(*) FROM dbo.HistorialCambios WHERE Entidad = 'Reserva' AND EntidadId IN (" + ids_704ILR + ")")}");
    Console.WriteLine($"    Bitacora: {Escalar_704ILR("SELECT COUNT(*) FROM dbo.Bitacora WHERE Id > @id", ("@id", bitacoraInicio_704ILR))}");
    Console.WriteLine($"    LoginAuditLog: {Escalar_704ILR("SELECT COUNT(*) FROM dbo.LoginAuditLog WHERE Id > @id", ("@id", auditoriaInicio_704ILR))}");
}
catch (Exception exLimpieza_704ILR) { Excepcion_704ILR("[limpieza]", exLimpieza_704ILR); }

// ---------------------------------------------------------------------------
// Cierre: la linea base de integridad tiene que seguir sana DESPUES de todas las
// altas, ediciones, cancelaciones y restauraciones de los casos [7] a [49] — que
// son justamente las operaciones que recalculan los digitos verificadores. Hasta
// ahora [16] la verificaba una sola vez, antes de que ocurriera nada de eso.
// ---------------------------------------------------------------------------
Caso_704ILR("[cierre] Integridad tras la corrida completa:");
try
{
    var resFin_704ILR = EvenTech.BLL.BLL_Integridad_704ILR.Verificar_704ILR();
    Esperar_704ILR("integridad al cierre", resFin_704ILR.Ok_704ILR, true);
    foreach (var i_704ILR in resFin_704ILR.Inconsistencias_704ILR) Console.WriteLine("   - " + i_704ILR);
}
catch (Exception exFin_704ILR) { Excepcion_704ILR("[cierre]", exFin_704ILR); }
CerrarCaso_704ILR();

// Resumen y codigo de salida: 0 todo bien; 1 hubo fallos; 2 sin fallos pero con
// casos omitidos (cobertura incompleta: no es verde).
Console.WriteLine($"== fin: {verificaciones_704ILR} verificaciones, {fallos_704ILR} fallo(s) | " +
                  $"casos: {casos_704ILR - omitidos_704ILR} ejecutados ({aprobados_704ILR} aprobados, {fallidos_704ILR} fallidos), {omitidos_704ILR} omitidos, {casos_704ILR} en total ==");
return fallos_704ILR > 0 ? 1 : (omitidos_704ILR > 0 ? 2 : 0);

// Observador de prueba del patron Observer (idiomas). Cumple el mismo rol que un
// formulario de la aplicacion: se suscribe al gestor y cuenta cuantas veces le
// pidieron refrescar sus textos.
class ObservadorPrueba_704ILR : IObservadorIdioma_704ILR
{
    public int Llamadas_704ILR { get; private set; }

    public void ActualizarTextos_704ILR() => Llamadas_704ILR++;
}
