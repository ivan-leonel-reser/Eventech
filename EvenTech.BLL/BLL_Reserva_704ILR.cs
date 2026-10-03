using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum ReservaResult_704ILR
    {
        Success_704ILR,
        InvalidCliente_704ILR,
        InvalidSalon_704ILR,
        InvalidFecha_704ILR,
        InvalidMonto_704ILR,
        SalonOcupado_704ILR,        // ya hay otra reserva activa para ese salon y fecha
        NoModificable_704ILR,       // la reserva esta cancelada: es un estado terminal
        Vencida_704ILR,             // se quiso cambiar de estado una cotizacion/pendiente cuyo plazo expiro (RN-01)
        TransicionInvalida_704ILR,  // el cambio de estado no figura en la tabla de transiciones (RN-05)
        InvalidInvitados_704ILR,    // cantidad de invitados negativa o mayor que InvitadosMaximo_704ILR
        CapacidadInsuficiente_704ILR, // el salon no aloja a los invitados de la reserva (RN-06)
        MontoInferiorPagado_704ILR,   // RN-04: el total quedaria por debajo de lo ya cobrado
        SinAdelanto_704ILR,           // RN-07: se quiso confirmar sin ningun pago registrado
        SinPlazo_704ILR,              // RN-01: la operacion no tiene plazo de vigencia que renovar
        EventoIniciado_704ILR,        // RN-13: el evento esta en ejecucion o cerrado, la reserva queda congelada
        NotFound_704ILR
    }

    // Reglas de negocio de reservas: validacion de datos y estados antes de
    // delegar al DAL. Las validaciones viven aca (no en la UI ni en el DAL)
    // para mantener cohesion y permitir reuso desde otros frentes.
    //
    // Serializacion de las escrituras de una reserva. Cobrar, anular un pago,
    // modificar, restaurar una version, cancelar y renovar validan reglas sobre el
    // estado PERSISTIDO de la reserva (RN-01, RN-02, RN-04, RN-05, RN-07) y despues
    // lo reescriben. Todas leen la cabecera con DAL_Reserva_704ILR.GetById_704ILR(id,
    // conn, tx) —bloqueo de actualizacion sobre la fila— y validan y escriben dentro
    // de esa misma transaccion: una segunda operacion sobre la misma reserva espera a
    // que la primera termine y juzga lo que la primera dejo escrito. Antes solo los
    // cobros lo hacian, y una edicion, restauracion o cancelacion simultanea validaba
    // contra un estado que ya no era el vigente (saldo negativo, baja revertida,
    // liquidacion de la RN-02 sin el cobro que entro en el medio). Mientras la fila
    // esta bloqueada, las tablas de la reserva solo se LEEN por otras conexiones
    // (validaciones, lineas de servicios) y siempre antes de la primera escritura
    // propia. La bitacora no comparte tablas con la operacion y se escribe por su lado:
    // los asientos de rechazo y la constancia de un estado alterado, que va antes de
    // confirmar (ver AsentarEstadoFueraDeDominio_704ILR).
    public static class BLL_Reserva_704ILR
    {
        public static List<BE_Reserva_704ILR> GetAll_704ILR() => DAL_Reserva_704ILR.GetAll_704ILR();

        public static BE_Reserva_704ILR GetById_704ILR(int id_704ILR) => DAL_Reserva_704ILR.GetById_704ILR(id_704ILR);

        // ---------------------------------------------------------------
        // RN-01 — Vigencia de la operacion.
        // Una COTIZACION vale DiasValidezCotizacion dias desde su emision; una
        // reserva PENDIENTE vale HorasValidezPendiente horas desde que quedo en
        // ese estado. Vencido el plazo la operacion no puede AVANZAR DE ESTADO: hay
        // que renovarla. CONFIRMADA y CANCELADA no tienen plazo (VenceEl queda null).
        public const int DiasValidezCotizacion_704ILR = 15;
        public const int HorasValidezPendiente_704ILR = 72;

        // RN-02 — Politica de cancelacion.
        // Cancelando con DiasCancelacionSinPenalidad dias o mas de antelacion a la
        // fecha del evento se reintegra el 100% de lo cobrado; con menos, se retiene
        // el PorcentajeRetencion. El sistema calcula y deja asentado el importe: el
        // movimiento fisico del dinero es una gestion administrativa externa.
        public const int DiasCancelacionSinPenalidad_704ILR = 30;
        public const int PorcentajeRetencion_704ILR = 50;

        // Importe maximo que admite la base para el total de una operacion y para el
        // precio de cada linea (Reservas.Monto, ReservaMemento.Monto y PrecioUnitario
        // son DECIMAL(12,2)). Un total mayor se rechaza como monto invalido antes de
        // escribir nada, en lugar de llegar al motor y volver como error tecnico.
        public const decimal MontoMaximo_704ILR = 9999999999.99m;

        // Cantidad maxima de invitados estimados de una operacion: la que admite el campo
        // Invitados de la ficha de la reserva y de la consulta de disponibilidad. Una cantidad
        // mayor solo llega escribiendo por fuera de la pantalla y se rechaza como invitados
        // invalidos, igual que una negativa. La base no la restringe (sin CHECK sobre
        // Reservas.CantidadInvitados): una base existente podria tener filas que la violen.
        public const int InvitadosMaximo_704ILR = 100000;

        // Ultimo dia admitido para el evento: el ultimo que muestra el selector de fecha de la
        // ficha. Una reserva con una fecha posterior no se podria mostrar ni corregir desde la
        // pantalla, que la declara fuera del calendario admitido.
        public static readonly DateTime FechaEventoMaxima_704ILR = new DateTime(9998, 12, 31);

        // ---------------------------------------------------------------
        // RN-05 — Transiciones de estado admitidas.
        // El ciclo de vida de la operacion no es libre: COTIZACION puede avanzar a
        // cualquier estado; PENDIENTE solo confirma o cancela; CONFIRMADA solo
        // cancela (no se "desconfirma": el salon ya quedo comprometido y hay
        // cobros asociados); CANCELADA es terminal. Mantener el mismo estado
        // siempre es valido (guardar una reserva sin tocar su estado).
        // La tabla vive aca, en una unica funcion, para que el documento y el
        // codigo compartan una sola fuente de verdad.
        public static bool TransicionValida_704ILR(EstadoReserva_704ILR desde_704ILR, EstadoReserva_704ILR hacia_704ILR)
        {
            if (desde_704ILR == hacia_704ILR)
                return desde_704ILR != EstadoReserva_704ILR.CANCELADA;

            switch (desde_704ILR)
            {
                case EstadoReserva_704ILR.COTIZACION:
                    return hacia_704ILR == EstadoReserva_704ILR.PENDIENTE
                        || hacia_704ILR == EstadoReserva_704ILR.CONFIRMADA
                        || hacia_704ILR == EstadoReserva_704ILR.CANCELADA;

                case EstadoReserva_704ILR.PENDIENTE:
                    return hacia_704ILR == EstadoReserva_704ILR.CONFIRMADA
                        || hacia_704ILR == EstadoReserva_704ILR.CANCELADA;

                case EstadoReserva_704ILR.CONFIRMADA:
                    return hacia_704ILR == EstadoReserva_704ILR.CANCELADA;

                default:                       // CANCELADA: estado terminal
                    return false;
            }
        }

        // RN-06 — Capacidad del salon.
        // Al comprometer el salon (CONFIRMADA) tiene que poder alojar a los invitados
        // estimados de la reserva. En COTIZACION y PENDIENTE no se exige: el vendedor
        // todavia esta armando la propuesta y puede cambiar de salon o de cantidad.
        // La funcion responde para cualquier estado; quien decide CUANDO exigirla (y
        // que el dato no falte al confirmar) es Validar_704ILR.
        // Un salon sin capacidad cargada (0) NO alcanza: es el mismo criterio con el
        // que responde la consulta de disponibilidad, y comprometer el salon sin saber
        // a cuanta gente aloja es justo lo que la regla evita.
        public static bool CapacidadSuficiente_704ILR(int salonId_704ILR, int cantidadInvitados_704ILR)
        {
            if (cantidadInvitados_704ILR <= 0) return true;   // sin dato no hay nada que comparar
            int capacidad_704ILR = DAL_Salon_704ILR.Capacidad_704ILR(salonId_704ILR);
            return capacidad_704ILR > 0 && cantidadInvitados_704ILR <= capacidad_704ILR;
        }

        // RN-07 — Adelanto para confirmar.
        // Lo que distingue a una reserva CONFIRMADA de una PENDIENTE es que el cliente
        // ya puso dinero: recien ahi la operacion queda firme y compromete el salon.
        // Como el cobro necesita una reserva ya registrada, el orden es siempre
        // guardar -> cobrar -> confirmar, que es el que describe el proceso de negocio.
        public static bool TieneAdelanto_704ILR(int reservaId_704ILR)
            => reservaId_704ILR > 0 && DAL_Pago_704ILR.TotalPagado_704ILR(reservaId_704ILR) > 0m;

        // Vencimiento que le corresponde a un estado, contado desde 'desde'.
        public static DateTime? CalcularVencimiento_704ILR(EstadoReserva_704ILR estado_704ILR, DateTime desde_704ILR)
        {
            if (estado_704ILR == EstadoReserva_704ILR.COTIZACION)
                return desde_704ILR.AddDays(DiasValidezCotizacion_704ILR);
            if (estado_704ILR == EstadoReserva_704ILR.PENDIENTE)
                return desde_704ILR.AddHours(HorasValidezPendiente_704ILR);
            return null;   // CONFIRMADA / CANCELADA no vencen
        }

        // Renueva el plazo de una cotizacion o pendiente (RN-01). Solo actua sobre una
        // operacion que TIENE plazo: CONFIRMADA no vence y una cotizacion sin
        // vencimiento cargado no tiene nada que renovar. En esos casos no se escribe
        // ni se asienta nada (antes quedaba un asiento con la fecha vacia).
        // Lee y reescribe la cabecera bajo el mismo bloqueo que el resto de las
        // escrituras (ver el encabezado de la clase): una renovacion que habia leido la
        // reserva antes de una baja simultanea la reescribia entera y la reabria.
        public static ReservaResult_704ILR Renovar_704ILR(int reservaId_704ILR)
        {
            BE_Reserva_704ILR r_704ILR;
            bool dvhAlterado_704ILR;
            string estadoAlterado_704ILR;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    r_704ILR = DAL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                    if (r_704ILR == null) return ReservaResult_704ILR.NotFound_704ILR;
                    if (!PuedeModificar_704ILR(r_704ILR)) return ReservaResult_704ILR.NoModificable_704ILR;

                    DateTime? nuevoPlazo_704ILR = CalcularVencimiento_704ILR(r_704ILR.Estado_704ILR, DateTime.Now);
                    if (!r_704ILR.VenceEl_704ILR.HasValue || !nuevoPlazo_704ILR.HasValue)
                        return ReservaResult_704ILR.SinPlazo_704ILR;

                    // La renovacion tambien reescribe la fila: la evidencia de una alteracion
                    // externa se toma antes, con el mismo criterio que Cancelar, Actualizar y
                    // RestaurarVersion. El DV horizontal almacenado se conserva tal cual (el
                    // vencimiento no forma parte de el).
                    dvhAlterado_704ILR = !DvhCoincide_704ILR(r_704ILR);
                    estadoAlterado_704ILR = DAL_Reserva_704ILR.EstadoFueraDeDominio_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);

                    r_704ILR.VenceEl_704ILR = nuevoPlazo_704ILR;
                    DAL_Reserva_704ILR.Update_704ILR(r_704ILR, conn_704ILR, tx_704ILR);
                    // La constancia del estado alterado va antes de confirmar: sin ella la
                    // renovacion no se aplica (ver AsentarEstadoFueraDeDominio_704ILR).
                    if (estadoAlterado_704ILR != null)
                        AsentarEstadoFueraDeDominio_704ILR(reservaId_704ILR, estadoAlterado_704ILR, r_704ILR.Estado_704ILR, "la renovacion");
                    tx_704ILR.Commit();
                }
            }
            if (dvhAlterado_704ILR) AsentarDvhNoCoincidente_704ILR(reservaId_704ILR, "la renovacion");
            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Renovacion de vigencia", CriticidadBitacora_704ILR.Info,
                $"Reserva #{reservaId_704ILR} renovada hasta {FechaBitacora_704ILR(r_704ILR.VenceEl_704ILR)}");
            return ReservaResult_704ILR.Success_704ILR;
        }

        // RN-02: importe que se retiene y que se reintegra si se cancela hoy.
        public static void CalcularCancelacion_704ILR(BE_Reserva_704ILR reserva_704ILR,
            out decimal retenido_704ILR, out decimal reembolsable_704ILR)
        {
            retenido_704ILR = 0m;
            reembolsable_704ILR = 0m;
            if (reserva_704ILR == null) return;

            LiquidarCancelacion_704ILR(reserva_704ILR, DAL_Pago_704ILR.TotalPagado_704ILR(reserva_704ILR.Id_704ILR),
                out retenido_704ILR, out reembolsable_704ILR);
        }

        // Cancela la reserva aplicando la RN-02 y dejando todo asentado. No pasa por
        // Actualizar porque una reserva con fecha ya pasada tambien se puede cancelar.
        // La lectura, la liquidacion, la version previa y la baja van en UNA
        // transaccion con la cabecera bloqueada (ver el encabezado de la clase): una
        // segunda baja simultanea ve la reserva ya cancelada, y un cobro simultaneo
        // entra antes (y queda liquidado) o despues (y se rechaza por estado terminal).
        public static ReservaResult_704ILR Cancelar_704ILR(int reservaId_704ILR,
            out decimal retenido_704ILR, out decimal reembolsable_704ILR)
        {
            retenido_704ILR = 0m;
            reembolsable_704ILR = 0m;

            BE_Reserva_704ILR antes_704ILR, cancelada_704ILR;
            bool dvhAlterado_704ILR;
            string estadoAlterado_704ILR;
            int confirmacionesReiniciadas_704ILR = 0;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    antes_704ILR = DAL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                    if (antes_704ILR == null) return ReservaResult_704ILR.NotFound_704ILR;
                    if (!PuedeModificar_704ILR(antes_704ILR)) return ReservaResult_704ILR.NoModificable_704ILR;
                    if (EventoIniciado_704ILR(antes_704ILR, "Cancelacion rechazada")) return ReservaResult_704ILR.EventoIniciado_704ILR;

                    // RN-05: un estado almacenado que no figura en el ciclo de vida no
                    // tiene transicion a CANCELADA ni una foto valida que versionar.
                    if (!EstadoAlmacenadoDefinido_704ILR(antes_704ILR.Estado_704ILR, reservaId_704ILR, 0, "Cancelacion rechazada",
                            () => DAL_Reserva_704ILR.EstadoFueraDeDominio_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR)))
                        return ReservaResult_704ILR.TransicionInvalida_704ILR;

                    // RN-02 sobre lo cobrado releido con la cabecera ya bloqueada.
                    LiquidarCancelacion_704ILR(antes_704ILR,
                        DAL_Pago_704ILR.TotalPagado_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR),
                        out retenido_704ILR, out reembolsable_704ILR);

                    cancelada_704ILR = DAL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                    cancelada_704ILR.Estado_704ILR = EstadoReserva_704ILR.CANCELADA;
                    cancelada_704ILR.VenceEl_704ILR = null;
                    dvhAlterado_704ILR = !DvhCoincide_704ILR(antes_704ILR);
                    cancelada_704ILR.Dvh_704ILR = DvhParaPersistir_704ILR(antes_704ILR, cancelada_704ILR);
                    estadoAlterado_704ILR = DAL_Reserva_704ILR.EstadoFueraDeDominio_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);

                    // La version previa entra en la misma transaccion que la baja.
                    CaretakerReserva_704ILR.GuardarVersion_704ILR(antes_704ILR, conn_704ILR, tx_704ILR);
                    DAL_Reserva_704ILR.Update_704ILR(cancelada_704ILR, conn_704ILR, tx_704ILR);
                    // Un evento cancelado ya no compromete a nadie: el personal que habia
                    // confirmado queda liberado en la misma transaccion que la baja.
                    if (ConfirmacionesCaducan_704ILR(antes_704ILR, cancelada_704ILR))
                        confirmacionesReiniciadas_704ILR = BLL_Coordinacion_704ILR.ReiniciarConfirmaciones_704ILR(antes_704ILR, conn_704ILR, tx_704ILR);
                    // La constancia del estado alterado va antes de confirmar: sin ella la
                    // baja no se aplica (ver AsentarEstadoFueraDeDominio_704ILR).
                    if (estadoAlterado_704ILR != null)
                        AsentarEstadoFueraDeDominio_704ILR(reservaId_704ILR, estadoAlterado_704ILR, cancelada_704ILR.Estado_704ILR, "la cancelacion");
                    tx_704ILR.Commit();
                }
            }

            // La baja ya esta confirmada en la base: lo que sigue es evidencia (ver
            // Crear_704ILR). La liquidacion de la RN-02 se asienta primero, porque es el
            // registro que la politica exige; el historial de cambios y el DV vertical van
            // despues, cada uno por su lado para que la falla de uno no se lleve al otro, y
            // un fallo ahi se asienta sin informar como fallida una baja aplicada.
            if (dvhAlterado_704ILR) AsentarDvhNoCoincidente_704ILR(reservaId_704ILR, "la cancelacion");
            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Cancelacion de reserva",
                CriticidadBitacora_704ILR.Advertencia,
                $"Reserva #{reservaId_704ILR} cancelada. Retenido {ImporteBitacora_704ILR(retenido_704ILR)}, " +
                $"reintegro {ImporteBitacora_704ILR(reembolsable_704ILR)} (RN-02).");
            BLL_Coordinacion_704ILR.AsentarConfirmacionesReiniciadas_704ILR(reservaId_704ILR, confirmacionesReiniciadas_704ILR,
                "la cancelacion de la reserva", personalLiberado_704ILR: true);
            try
            {
                RegistradorDeCambios_704ILR.RegistrarCambios_704ILR("Reserva", reservaId_704ILR,
                    antes_704ILR, cancelada_704ILR, CamposAuditados_704ILR);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas",
                    $"evidencia posterior a la cancelacion de la reserva #{reservaId_704ILR} (historial de cambios)");
            }
            try
            {
                BLL_Integridad_704ILR.RecalcularDVVerticalReservas_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas",
                    $"evidencia posterior a la cancelacion de la reserva #{reservaId_704ILR} (DV vertical)");
            }
            return ReservaResult_704ILR.Success_704ILR;
        }

        // Campos auditados por el control de cambios (T06b). Se persisten con el
        // nombre logico; RegistradorDeCambios resuelve por reflexion la propiedad
        // sufijada correspondiente.
        private static readonly string[] CamposAuditados_704ILR =
            { "ClienteId", "SalonId", "FechaEvento", "Estado", "Monto", "CantidadInvitados" };

        public static ReservaResult_704ILR Crear_704ILR(BE_Reserva_704ILR reserva_704ILR, out int nuevoId_704ILR)
            => Crear_704ILR(reserva_704ILR, null, out nuevoId_704ILR);

        // Alta de la operacion COMPLETA: la reserva y los servicios contratados que
        // componen su monto se escriben bajo una unica transaccion. Si algo falla en
        // el medio no queda una reserva con un total que sus lineas no explican.
        // Con 'servicios' en null se guarda solo la cabecera (alta sin servicios).
        public static ReservaResult_704ILR Crear_704ILR(BE_Reserva_704ILR reserva_704ILR,
            IList<BE_ReservaServicio_704ILR> servicios_704ILR, out int nuevoId_704ILR)
        {
            nuevoId_704ILR = 0;
            if (reserva_704ILR == null) return ReservaResult_704ILR.InvalidCliente_704ILR;

            // RN-05: el estado tiene que ser uno de los cuatro del ciclo de vida. Un
            // valor fuera del enum pasaria las guardas por estado y se persistiria
            // como un texto sin significado para la tabla de transiciones.
            if (!EstadoDefinido_704ILR(reserva_704ILR.Estado_704ILR, 0, "Alta rechazada"))
                return ReservaResult_704ILR.TransicionInvalida_704ILR;

            // RN-05: CANCELADA es un estado al que se LLEGA dando de baja una operacion
            // existente, no uno con el que se nace. Admitirlo en el alta dejaria una
            // reserva en estado terminal sin liquidacion de la RN-02, sin version previa
            // y sin asiento de cancelacion, y ya no se podria editar ni dar de baja.
            if (reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.CANCELADA)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Alta rechazada",
                    CriticidadBitacora_704ILR.Advertencia,
                    "No se admite dar de alta una reserva directamente en estado CANCELADA (RN-05).");
                return ReservaResult_704ILR.TransicionInvalida_704ILR;
            }

            // RN-07: una reserva nace en COTIZACION o PENDIENTE. No puede nacer
            // CONFIRMADA porque para confirmar hace falta el adelanto cobrado, y un
            // cobro necesita una reserva ya registrada a la que imputarse: primero se
            // guarda la operacion, despues se cobra y recien entonces se confirma.
            if (reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Alta rechazada",
                    CriticidadBitacora_704ILR.Advertencia,
                    "No se admite dar de alta una reserva directamente CONFIRMADA: " +
                    "primero se registra la operacion y se cobra el adelanto (RN-07).");
                return ReservaResult_704ILR.SinAdelanto_704ILR;
            }

            // El monto de la operacion ES la suma de sus lineas: cuando viajan los
            // servicios, la capa de negocio valida cada linea y fija el total desde
            // ellas en vez de confiar en el que armo la pantalla.
            if (!AplicarServicios_704ILR(reserva_704ILR, servicios_704ILR))
                return ReservaResult_704ILR.InvalidMonto_704ILR;

            var validacion_704ILR = Validar_704ILR(reserva_704ILR);
            if (validacion_704ILR != ReservaResult_704ILR.Success_704ILR)
            {
                AsentarRechazoDeRegla_704ILR(reserva_704ILR.Id_704ILR, validacion_704ILR, "Alta rechazada");
                return validacion_704ILR;
            }

            // RN-01: la vigencia se fija al dar de alta, segun el estado inicial.
            reserva_704ILR.VenceEl_704ILR =
                CalcularVencimiento_704ILR(reserva_704ILR.Estado_704ILR, DateTime.Now);

            // DV horizontal: se calcula sobre los campos de negocio antes de persistir.
            reserva_704ILR.Dvh_704ILR = ValidadorDeIntegridad_704ILR.CalcularDVH_704ILR(reserva_704ILR);

            try
            {
                using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                {
                    SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                    using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                    {
                        nuevoId_704ILR = DAL_Reserva_704ILR.Insert_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR);
                        if (servicios_704ILR != null)
                            DAL_ReservaServicio_704ILR.ReplaceForReserva_704ILR(nuevoId_704ILR, servicios_704ILR, conn_704ILR, tx_704ILR);
                        tx_704ILR.Commit();
                    }
                }
            }
            catch (SqlException ex_704ILR) when (EsChoqueDeUnicidad_704ILR(ex_704ILR))
            {
                nuevoId_704ILR = 0;
                AsentarRechazoDelMotor_704ILR(0, "Alta rechazada");
                return ReservaResult_704ILR.SalonOcupado_704ILR;
            }

            // La operacion ya quedo confirmada en la base: lo que sigue es evidencia
            // derivada (DV vertical y asiento). Un fallo aca no se informa como error,
            // porque el reintento duplicaria el alta; queda asentado como excepcion y
            // la verificacion del proximo arranque detecta y alerta un DV vertical
            // desactualizado (no lo repara sola: lo recalcula el administrador desde
            // la herramienta de integridad).
            try
            {
                BLL_Integridad_704ILR.RecalcularDVVerticalReservas_704ILR();

                // El asiento nombra el documento que se emitio: una cotizacion y una
                // reserva pendiente son operaciones distintas para el negocio, y el estado
                // inicial es lo unico que las separa en el alta (RN-07).
                BLL_Bitacora_704ILR.Registrar_704ILR("Reservas",
                    reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.COTIZACION
                        ? "Cotizacion generada"
                        : "Reserva generada",
                    CriticidadBitacora_704ILR.Info,
                    $"Reserva #{nuevoId_704ILR} - cliente #{reserva_704ILR.ClienteId_704ILR}, " +
                    $"estado {reserva_704ILR.Estado_704ILR}, monto {ImporteBitacora_704ILR(reserva_704ILR.Monto_704ILR)}");
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas",
                    $"evidencia posterior al alta de la reserva #{nuevoId_704ILR}");
            }
            return ReservaResult_704ILR.Success_704ILR;
        }

        // Una reserva cancelada es un estado terminal: no admite mas ediciones.
        // (La fecha pasada no se contempla aca a proposito: Validar ya rechaza
        // guardar con fecha anterior a hoy, y bloquear por la fecha VIEJA
        // impediria reprogramar un evento vencido, que si es una operacion valida.)
        public static bool PuedeModificar_704ILR(BE_Reserva_704ILR reserva_704ILR)
            => reserva_704ILR != null && reserva_704ILR.Estado_704ILR != EstadoReserva_704ILR.CANCELADA;

        public static ReservaResult_704ILR Actualizar_704ILR(BE_Reserva_704ILR reserva_704ILR)
            => Actualizar_704ILR(reserva_704ILR, null);

        // Modificacion de la operacion COMPLETA (ver Crear_704ILR): la cabecera, sus
        // servicios contratados y la version previa viajan en la misma transaccion.
        // Todas las reglas que dependen de lo persistido se evaluan sobre la cabecera
        // leida con bloqueo dentro de esa transaccion (ver el encabezado de la clase).
        public static ReservaResult_704ILR Actualizar_704ILR(BE_Reserva_704ILR reserva_704ILR,
            IList<BE_ReservaServicio_704ILR> servicios_704ILR)
        {
            if (reserva_704ILR.Id_704ILR <= 0) return ReservaResult_704ILR.NotFound_704ILR;

            BE_Reserva_704ILR antes_704ILR = null;
            bool lineasCambiaron_704ILR = false;
            bool dvhAlterado_704ILR = false;
            string estadoAlterado_704ILR = null;
            int confirmacionesReiniciadas_704ILR = 0;
            try
            {
                using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                {
                    SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                    using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                    {
                        antes_704ILR = DAL_Reserva_704ILR.GetById_704ILR(reserva_704ILR.Id_704ILR, conn_704ILR, tx_704ILR);
                        if (antes_704ILR == null) return ReservaResult_704ILR.NotFound_704ILR;

                        // Se evalua sobre el estado PERSISTIDO: lo que el usuario mando en el
                        // formulario no puede habilitar la edicion de una reserva ya cancelada.
                        if (!PuedeModificar_704ILR(antes_704ILR))
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Modificacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reserva_704ILR.Id_704ILR} cancelada: no admite modificaciones.");
                            return ReservaResult_704ILR.NoModificable_704ILR;
                        }

                        // RN-13: con el evento en ejecucion o cerrado la reserva queda congelada.
                        if (EventoIniciado_704ILR(antes_704ILR, "Modificacion rechazada"))
                            return ReservaResult_704ILR.EventoIniciado_704ILR;

                        // RN-05: el estado almacenado y el pedido tienen que ser dos de los cuatro
                        // del ciclo de vida (ver Crear_704ILR). Se controlan antes que la tabla de
                        // transiciones, que no puede opinar sobre un valor que no figura en ella:
                        // con un estado almacenado ajeno el rechazo quedaba asentado como una
                        // transicion "de -1" a otro estado, sin el valor que habia en la base.
                        if (!EstadoAlmacenadoDefinido_704ILR(antes_704ILR.Estado_704ILR, reserva_704ILR.Id_704ILR, 0, "Modificacion rechazada",
                                () => DAL_Reserva_704ILR.EstadoFueraDeDominio_704ILR(reserva_704ILR.Id_704ILR, conn_704ILR, tx_704ILR)))
                            return ReservaResult_704ILR.TransicionInvalida_704ILR;
                        if (!EstadoDefinido_704ILR(reserva_704ILR.Estado_704ILR, reserva_704ILR.Id_704ILR, "Modificacion rechazada"))
                            return ReservaResult_704ILR.TransicionInvalida_704ILR;

                        // RN-05: el cambio de estado tiene que figurar en la tabla de transiciones.
                        // Se evalua sobre el estado PERSISTIDO contra el pedido, antes que nada:
                        // una CONFIRMADA no puede volver a COTIZACION ni a PENDIENTE.
                        if (!TransicionValida_704ILR(antes_704ILR.Estado_704ILR, reserva_704ILR.Estado_704ILR))
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Transicion rechazada",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reserva_704ILR.Id_704ILR}: no se admite pasar de " +
                                $"{antes_704ILR.Estado_704ILR} a {reserva_704ILR.Estado_704ILR} (RN-05).");
                            return ReservaResult_704ILR.TransicionInvalida_704ILR;
                        }

                        // Dar de baja no es una edicion mas: entrar a CANCELADA tiene que pasar
                        // por Cancelar, que es quien liquida la RN-02 y deja asentado el
                        // reintegro. Si se admitiera por aca se podria cancelar sin liquidacion.
                        if (reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.CANCELADA)
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Cancelacion rechazada por via incorrecta",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reserva_704ILR.Id_704ILR}: la baja se registra por la via de " +
                                "cancelacion, que aplica la politica de reintegro (RN-02).");
                            return ReservaResult_704ILR.TransicionInvalida_704ILR;
                        }

                        // RN-01: una operacion vencida no AVANZA de estado hasta que se renueve su
                        // vigencia. El control cubre cualquier cambio de estado y no solo el paso a
                        // CONFIRMADA: si solo mirara la confirmacion, el vencimiento se eludiria en
                        // dos pasos (una cotizacion vencida pasa a PENDIENTE, lo que le da plazo
                        // nuevo, y desde ahi se confirma sin renovar y sin dejar asiento).
                        // Se evalua sobre lo PERSISTIDO: el formulario no puede saltear el
                        // vencimiento. Conservar el mismo estado sigue permitido (se puede seguir
                        // editando una cotizacion vencida); la baja va por Cancelar, que se rechaza
                        // antes y no llega hasta aca. El criterio es el mismo que aplica la
                        // restauracion de versiones (AvanceConVigenciaVencida_704ILR).
                        if (AvanceConVigenciaVencida_704ILR(antes_704ILR, reserva_704ILR.Estado_704ILR))
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Cambio de estado rechazado",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reserva_704ILR.Id_704ILR} vencida el " +
                                $"{FechaBitacora_704ILR(antes_704ILR.VenceEl_704ILR)}: no puede pasar a " +
                                $"{reserva_704ILR.Estado_704ILR} hasta renovarla (RN-01).");
                            return ReservaResult_704ILR.Vencida_704ILR;
                        }

                        // Monto = suma de las lineas cuando viajan los servicios (ver Crear_704ILR).
                        // Va antes de Validar y de la RN-04 para que las dos juzguen el total real.
                        if (!AplicarServicios_704ILR(reserva_704ILR, servicios_704ILR))
                            return ReservaResult_704ILR.InvalidMonto_704ILR;

                        var validacion_704ILR = Validar_704ILR(reserva_704ILR);
                        if (validacion_704ILR != ReservaResult_704ILR.Success_704ILR)
                        {
                            AsentarRechazoDeRegla_704ILR(reserva_704ILR.Id_704ILR, validacion_704ILR, "Modificacion rechazada");
                            return validacion_704ILR;
                        }

                        // Lo cobrado se relee con la cabecera bloqueada: un cobro o una anulacion
                        // simultaneos esperan a que esta transaccion termine (BLL_Pago_704ILR).
                        decimal pagado_704ILR = DAL_Pago_704ILR.TotalPagado_704ILR(reserva_704ILR.Id_704ILR, conn_704ILR, tx_704ILR);

                        // RN-04: el total de la reserva es el tope de la cobranza. Una edicion que
                        // lo achique por debajo de lo ya cobrado (por ejemplo, quitando servicios)
                        // romperia el invariante sin registrar ningun pago: se rechaza aca, que es
                        // el unico camino por el que el total puede bajar.
                        if (reserva_704ILR.Monto_704ILR < pagado_704ILR)
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Modificacion rechazada",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reserva_704ILR.Id_704ILR}: el total quedaria por debajo de lo ya cobrado (RN-04).");
                            return ReservaResult_704ILR.MontoInferiorPagado_704ILR;
                        }

                        // RN-07: la reserva queda firme cuando el cliente puso el adelanto. Pasar a
                        // CONFIRMADA sin ningun pago registrado comprometeria el salon sobre una
                        // decision que el cliente todavia no respaldo con dinero.
                        if (reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA &&
                            antes_704ILR.Estado_704ILR != EstadoReserva_704ILR.CONFIRMADA &&
                            pagado_704ILR <= 0m)
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Confirmacion rechazada",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reserva_704ILR.Id_704ILR}: no se registro el adelanto, " +
                                "no puede confirmarse (RN-07).");
                            return ReservaResult_704ILR.SinAdelanto_704ILR;
                        }

                        // RN-01: si cambia el estado se recalcula la vigencia; si no, se conserva.
                        reserva_704ILR.VenceEl_704ILR =
                            reserva_704ILR.Estado_704ILR != antes_704ILR.Estado_704ILR
                                ? CalcularVencimiento_704ILR(reserva_704ILR.Estado_704ILR, DateTime.Now)
                                : antes_704ILR.VenceEl_704ILR;

                        // Composicion de servicios: se compara contra lo persistido por (servicio,
                        // cantidad, precio) y no por Id de linea, porque ReplaceForReserva recrea
                        // las filas y un Id nuevo no significa un cambio.
                        lineasCambiaron_704ILR = servicios_704ILR != null &&
                            !BLL_ReservaServicio_704ILR.MismasLineas_704ILR(
                                DAL_ReservaServicio_704ILR.GetByReserva_704ILR(reserva_704ILR.Id_704ILR), servicios_704ILR);

                        // Guardar sin cambiar nada no es una modificacion: no se versiona ni se
                        // asienta, para que el historial de versiones y la bitacora no acumulen
                        // entradas vacias. Las reglas de arriba ya se evaluaron igual.
                        if (!lineasCambiaron_704ILR && !CabeceraCambio_704ILR(antes_704ILR, reserva_704ILR))
                            return ReservaResult_704ILR.Success_704ILR;

                        // DV horizontal con los nuevos valores, salvo que el almacenado ya no
                        // coincida con la fila (alteracion externa): ese no se legitima.
                        dvhAlterado_704ILR = !DvhCoincide_704ILR(antes_704ILR);
                        reserva_704ILR.Dvh_704ILR = DvhParaPersistir_704ILR(antes_704ILR, reserva_704ILR);

                        // Estado almacenado con otra capitalizacion: la escritura lo normaliza,
                        // asi que el valor original se toma aca y se asienta antes de confirmar.
                        estadoAlterado_704ILR = DAL_Reserva_704ILR.EstadoFueraDeDominio_704ILR(reserva_704ILR.Id_704ILR, conn_704ILR, tx_704ILR);

                        // Memento: antes de pisar el estado actual se guarda una version
                        // completa (reserva + servicios) para poder volver atras. Entra en la
                        // misma transaccion: si la escritura falla, la version tampoco queda.
                        CaretakerReserva_704ILR.GuardarVersion_704ILR(antes_704ILR, conn_704ILR, tx_704ILR);

                        DAL_Reserva_704ILR.Update_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR);
                        if (servicios_704ILR != null)
                            DAL_ReservaServicio_704ILR.ReplaceForReserva_704ILR(reserva_704ILR.Id_704ILR, servicios_704ILR, conn_704ILR, tx_704ILR);
                        // Reprogramar un evento ya coordinado deja sin efecto las confirmaciones
                        // del personal, que valian para la fecha anterior (Proceso 2).
                        if (ConfirmacionesCaducan_704ILR(antes_704ILR, reserva_704ILR))
                            confirmacionesReiniciadas_704ILR = BLL_Coordinacion_704ILR.ReiniciarConfirmaciones_704ILR(antes_704ILR, conn_704ILR, tx_704ILR);
                        // Sin la constancia del estado alterado la modificacion no se aplica
                        // (ver AsentarEstadoFueraDeDominio_704ILR).
                        if (estadoAlterado_704ILR != null)
                            AsentarEstadoFueraDeDominio_704ILR(reserva_704ILR.Id_704ILR, estadoAlterado_704ILR, reserva_704ILR.Estado_704ILR, "la modificacion");
                        tx_704ILR.Commit();
                    }
                }
            }
            catch (SqlException ex_704ILR) when (EsChoqueDeUnicidad_704ILR(ex_704ILR))
            {
                AsentarRechazoDelMotor_704ILR(reserva_704ILR.Id_704ILR, "Modificacion rechazada");
                return ReservaResult_704ILR.SalonOcupado_704ILR;
            }

            // Evidencia posterior al commit (ver Crear_704ILR): la modificacion ya
            // esta guardada, un fallo aca se asienta y no se informa como error.
            if (dvhAlterado_704ILR) AsentarDvhNoCoincidente_704ILR(reserva_704ILR.Id_704ILR, "la modificacion");
            BLL_Coordinacion_704ILR.AsentarConfirmacionesReiniciadas_704ILR(reserva_704ILR.Id_704ILR, confirmacionesReiniciadas_704ILR, "la reprogramacion del evento");
            try
            {
                BLL_Integridad_704ILR.RecalcularDVVerticalReservas_704ILR();

                // Control de cambios: registra campo por campo lo que cambio en la
                // cabecera; el cambio de composicion se nombra en el asiento para que
                // "0 campo(s)" no se lea como "no cambio nada".
                int cambios_704ILR = RegistradorDeCambios_704ILR.RegistrarCambios_704ILR("Reserva", reserva_704ILR.Id_704ILR, antes_704ILR, reserva_704ILR, CamposAuditados_704ILR);
                string detalle_704ILR = $"Reserva #{reserva_704ILR.Id_704ILR} - {cambios_704ILR} campo(s) modificado(s)";
                if (lineasCambiaron_704ILR)
                    detalle_704ILR += $"; servicios modificados: {servicios_704ILR.Count} linea(s)";
                BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Modificacion de reserva", CriticidadBitacora_704ILR.Info, detalle_704ILR);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas",
                    $"evidencia posterior a la modificacion de la reserva #{reserva_704ILR.Id_704ILR}");
            }
            return ReservaResult_704ILR.Success_704ILR;
        }

        // Restaura la reserva al estado de una version previa (patron Memento).
        // El estado vigente se versiona antes de pisarlo, de modo que la propia
        // restauracion tambien se puede deshacer.
        //
        // Relacion con la RN-05: restaurar es una correccion ADMINISTRATIVA, no un
        // avance del ciclo comercial, y por eso no se le exige la tabla de
        // transiciones (deshacer una confirmacion erronea es su caso de uso tipico
        // y queda enteramente auditado: versiona, registra campo por campo y asienta
        // en bitacora). Los dos limites terminales de la RN-05 si se respetan: no se
        // restaura una reserva CANCELADA ni se restaura HACIA una version cancelada.
        //
        // Como la edicion, evalua todo sobre la cabecera leida con bloqueo y escribe
        // version, cabecera y lineas en una sola transaccion (ver el encabezado de la clase).
        public static ReservaResult_704ILR RestaurarVersion_704ILR(int reservaId_704ILR, int mementoId_704ILR)
        {
            BE_Reserva_704ILR actual_704ILR = null, restaurada_704ILR = null;
            BE_ReservaMemento_704ILR memento_704ILR = null;
            bool lineasCambian_704ILR = false;
            bool dvhAlterado_704ILR = false;
            string estadoAlterado_704ILR = null;
            int confirmacionesReiniciadas_704ILR = 0;
            try
            {
                using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                {
                    SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                    using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                    {
                        actual_704ILR = DAL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                        if (actual_704ILR == null) return ReservaResult_704ILR.NotFound_704ILR;

                        // CANCELADA es terminal (RN-05): restaurar una version previa tambien es
                        // una modificacion y reabriria la operacion por la puerta de atras. La
                        // regla se aplica aca y no solo en la UI porque la restauracion persiste
                        // en el acto, sin pasar por Actualizar.
                        if (!PuedeModificar_704ILR(actual_704ILR))
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Restauracion rechazada",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reservaId_704ILR} cancelada: no admite restaurar versiones (RN-05).");
                            return ReservaResult_704ILR.NoModificable_704ILR;
                        }

                        // RN-13: con el evento en ejecucion o cerrado la reserva queda congelada.
                        if (EventoIniciado_704ILR(actual_704ILR, "Restauracion rechazada"))
                            return ReservaResult_704ILR.EventoIniciado_704ILR;

                        memento_704ILR = CaretakerReserva_704ILR.GetVersion_704ILR(mementoId_704ILR);
                        if (memento_704ILR == null || memento_704ILR.ReservaId_704ILR != reservaId_704ILR) return ReservaResult_704ILR.NotFound_704ILR;

                        // Tampoco se puede ENTRAR a CANCELADA restaurando: dar de baja una
                        // operacion exige pasar por Cancelar, que es quien aplica la RN-02 y
                        // deja asentado el reintegro. Restaurar salteando esa via dejaria una
                        // reserva cancelada sin liquidacion.
                        if (memento_704ILR.Estado_704ILR == EstadoReserva_704ILR.CANCELADA)
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Restauracion rechazada",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reservaId_704ILR}: la version #{mementoId_704ILR} esta CANCELADA; " +
                                "la baja se registra por la via de cancelacion (RN-02/RN-05).");
                            return ReservaResult_704ILR.TransicionInvalida_704ILR;
                        }

                        // RN-05: un estado almacenado (vigente o de la version) que no figura en
                        // el ciclo de vida no se restaura ni se pisa. Se evaluan los dos para que
                        // el asiento ubique cada fila alterada (la reserva o la version) con el
                        // valor que tiene en la base.
                        bool actualDefinido_704ILR = EstadoAlmacenadoDefinido_704ILR(actual_704ILR.Estado_704ILR, reservaId_704ILR, 0,
                            "Restauracion rechazada", () => DAL_Reserva_704ILR.EstadoFueraDeDominio_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR));
                        bool versionDefinida_704ILR = EstadoAlmacenadoDefinido_704ILR(memento_704ILR.Estado_704ILR, reservaId_704ILR, mementoId_704ILR,
                            "Restauracion rechazada", () => DAL_ReservaMemento_704ILR.EstadoFueraDeDominio_704ILR(mementoId_704ILR, conn_704ILR, tx_704ILR));
                        if (!actualDefinido_704ILR || !versionDefinida_704ILR)
                            return ReservaResult_704ILR.TransicionInvalida_704ILR;

                        // RN-01: la restauracion esta exceptuada de la TABLA de transiciones, no del
                        // plazo de vigencia. Si la version que se repone cambia el estado de una
                        // operacion ya vencida (a CONFIRMADA, a PENDIENTE o de vuelta a COTIZACION),
                        // restaurar le daria plazo nuevo sin renovar y sin asiento, que es justo lo
                        // que Actualizar rechaza; se aplica el mismo criterio. Restaurar una version
                        // del mismo estado sigue permitido porque conserva el vencimiento vigente.
                        if (AvanceConVigenciaVencida_704ILR(actual_704ILR, memento_704ILR.Estado_704ILR))
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Restauracion rechazada",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reservaId_704ILR} vencida el " +
                                $"{FechaBitacora_704ILR(actual_704ILR.VenceEl_704ILR)}: hay que renovarla antes de " +
                                $"restaurar una version con otro estado ({memento_704ILR.Estado_704ILR}) (RN-01).");
                            return ReservaResult_704ILR.Vencida_704ILR;
                        }

                        // Lo cobrado, releido con la cabecera bloqueada (RN-04 y RN-07).
                        decimal pagado_704ILR = DAL_Pago_704ILR.TotalPagado_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);

                        // RN-07: tampoco se llega a CONFIRMADA restaurando si no hay adelanto.
                        if (memento_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA &&
                            actual_704ILR.Estado_704ILR != EstadoReserva_704ILR.CONFIRMADA &&
                            pagado_704ILR <= 0m)
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Restauracion rechazada",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reservaId_704ILR}: la version #{mementoId_704ILR} esta CONFIRMADA " +
                                "y no hay adelanto registrado (RN-07).");
                            return ReservaResult_704ILR.SinAdelanto_704ILR;
                        }

                        restaurada_704ILR = DAL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                        restaurada_704ILR.RestaurarDesde_704ILR(memento_704ILR);

                        // RN-01: el memento no guarda el vencimiento (es un dato administrativo, no
                        // parte del estado de negocio versionado), asi que hay que recalcularlo con
                        // el mismo criterio que usa la edicion: si el estado cambio, el plazo se
                        // cuenta de nuevo; si no cambio, se conserva el que tenia. Sin esto una
                        // cotizacion restaurada quedaba sin plazo y una confirmada, con plazo.
                        restaurada_704ILR.VenceEl_704ILR =
                            restaurada_704ILR.Estado_704ILR != actual_704ILR.Estado_704ILR
                                ? CalcularVencimiento_704ILR(restaurada_704ILR.Estado_704ILR, DateTime.Now)
                                : actual_704ILR.VenceEl_704ILR;

                        // La fecha del evento de esa version puede haber quedado en el pasado:
                        // se admite (es un estado historico valido), pero el resto de las
                        // reglas (cliente/salon existentes, anti-solapamiento) sigue vigente. Los
                        // rechazos de regla (RN-03, RN-06) se asientan con el mismo criterio que
                        // en el alta y la edicion.
                        var validacion_704ILR = Validar_704ILR(restaurada_704ILR, permitirFechaPasada_704ILR: true);
                        if (validacion_704ILR != ReservaResult_704ILR.Success_704ILR)
                        {
                            AsentarRechazoDeRegla_704ILR(reservaId_704ILR, validacion_704ILR, "Restauracion rechazada");
                            return validacion_704ILR;
                        }

                        // RN-04: una version previa mas barata que lo ya cobrado tampoco se repone.
                        if (restaurada_704ILR.Monto_704ILR < pagado_704ILR)
                        {
                            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Restauracion rechazada",
                                CriticidadBitacora_704ILR.Advertencia,
                                $"Reserva #{reservaId_704ILR}: la version #{mementoId_704ILR} dejaria el total " +
                                "por debajo de lo ya cobrado (RN-04).");
                            return ReservaResult_704ILR.MontoInferiorPagado_704ILR;
                        }

                        // Una version identica al estado vigente (cabecera y composicion) no
                        // repone nada: con el mismo criterio que "guardar sin cambios" no se
                        // versiona, no se escribe ni se asienta una restauracion vacia.
                        lineasCambian_704ILR = !BLL_ReservaServicio_704ILR.MismasLineas_704ILR(
                            DAL_ReservaServicio_704ILR.GetByReserva_704ILR(reservaId_704ILR), memento_704ILR.Servicios_704ILR);
                        if (!lineasCambian_704ILR && !CabeceraCambio_704ILR(actual_704ILR, restaurada_704ILR))
                            return ReservaResult_704ILR.Success_704ILR;

                        dvhAlterado_704ILR = !DvhCoincide_704ILR(actual_704ILR);
                        restaurada_704ILR.Dvh_704ILR = DvhParaPersistir_704ILR(actual_704ILR, restaurada_704ILR);
                        estadoAlterado_704ILR = DAL_Reserva_704ILR.EstadoFueraDeDominio_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);

                        // La version del estado que se pisa, la cabecera repuesta y los servicios
                        // de esa version entran juntos, igual que en el alta y en la edicion: el
                        // monto restaurado y las lineas que lo componen no pueden quedar desfasados.
                        CaretakerReserva_704ILR.GuardarVersion_704ILR(actual_704ILR, conn_704ILR, tx_704ILR);
                        DAL_Reserva_704ILR.Update_704ILR(restaurada_704ILR, conn_704ILR, tx_704ILR);
                        DAL_ReservaServicio_704ILR.ReplaceForReserva_704ILR(
                            reservaId_704ILR, memento_704ILR.Servicios_704ILR, conn_704ILR, tx_704ILR);
                        // La version repuesta cambia la fecha o deja a la reserva sin confirmar:
                        // las confirmaciones del personal dejan de valer (Proceso 2).
                        if (ConfirmacionesCaducan_704ILR(actual_704ILR, restaurada_704ILR))
                            confirmacionesReiniciadas_704ILR = BLL_Coordinacion_704ILR.ReiniciarConfirmaciones_704ILR(actual_704ILR, conn_704ILR, tx_704ILR);
                        // Sin la constancia del estado alterado la restauracion no se aplica
                        // (ver AsentarEstadoFueraDeDominio_704ILR).
                        if (estadoAlterado_704ILR != null)
                            AsentarEstadoFueraDeDominio_704ILR(reservaId_704ILR, estadoAlterado_704ILR, restaurada_704ILR.Estado_704ILR, "la restauracion");
                        tx_704ILR.Commit();
                    }
                }
            }
            catch (SqlException ex_704ILR) when (EsChoqueDeUnicidad_704ILR(ex_704ILR))
            {
                AsentarRechazoDelMotor_704ILR(reservaId_704ILR, "Restauracion rechazada");
                return ReservaResult_704ILR.SalonOcupado_704ILR;
            }

            // Evidencia posterior al commit (ver Crear_704ILR): la version ya esta
            // repuesta, un fallo aca se asienta y no se informa como error.
            if (dvhAlterado_704ILR) AsentarDvhNoCoincidente_704ILR(reservaId_704ILR, "la restauracion");
            // Si la version repuesta deja a la reserva sin confirmar, su evento ya no se
            // coordina: el personal queda liberado, igual que en una cancelacion.
            BLL_Coordinacion_704ILR.AsentarConfirmacionesReiniciadas_704ILR(reservaId_704ILR, confirmacionesReiniciadas_704ILR, "la restauracion de una version",
                personalLiberado_704ILR: restaurada_704ILR.Estado_704ILR != EstadoReserva_704ILR.CONFIRMADA);
            try
            {
                BLL_Integridad_704ILR.RecalcularDVVerticalReservas_704ILR();

                // El control de cambios registra la restauracion como una modificacion
                // mas, campo por campo (queda trazado en el historial de la reserva); la
                // composicion de servicios repuesta se nombra en el asiento, como en la
                // edicion, para que "0 campo(s)" no se lea como "no repuso nada".
                int cambios_704ILR = RegistradorDeCambios_704ILR.RegistrarCambios_704ILR("Reserva", reservaId_704ILR, actual_704ILR, restaurada_704ILR, CamposAuditados_704ILR);
                string detalle_704ILR = $"Reserva #{reservaId_704ILR} restaurada a la version #{mementoId_704ILR} ({cambios_704ILR} campo(s) repuestos)";
                if (lineasCambian_704ILR)
                    detalle_704ILR += $"; servicios repuestos: {memento_704ILR.Servicios_704ILR.Count} linea(s)";
                BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Restauracion de version", CriticidadBitacora_704ILR.Info, detalle_704ILR);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas",
                    $"evidencia posterior a la restauracion de la reserva #{reservaId_704ILR}");
            }
            return ReservaResult_704ILR.Success_704ILR;
        }

        // ---------------------------------------------------------------
        // Helpers privados de las reglas.

        // RN-13: una reserva cuyo evento esta en ejecucion o cerrado queda congelada: no
        // se modifica, no se cancela y no se le restaura una version (los cobros del
        // saldo siguen admitidos, van por BLL_Pago). Se evalua sobre la cabecera leida
        // con bloqueo y el rechazo se asienta: es una regla de negocio, no un error de tipeo.
        private static bool EventoIniciado_704ILR(BE_Reserva_704ILR persistida_704ILR, string accion_704ILR)
        {
            if (!BLL_Coordinacion_704ILR.PlanCongelado_704ILR(persistida_704ILR.EstadoCoordinacion_704ILR)) return false;
            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", accion_704ILR, CriticidadBitacora_704ILR.Advertencia,
                $"Reserva #{persistida_704ILR.Id_704ILR}: el evento esta {persistida_704ILR.EstadoCoordinacion_704ILR}, " +
                "la reserva queda congelada (RN-13).");
            return true;
        }

        // Las confirmaciones del personal valen para una reserva CONFIRMADA y para su
        // fecha: caducan si la reserva estaba confirmada y cambia de dia o deja de estarlo.
        private static bool ConfirmacionesCaducan_704ILR(BE_Reserva_704ILR persistida_704ILR, BE_Reserva_704ILR nueva_704ILR)
            => persistida_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA
               && (nueva_704ILR.Estado_704ILR != EstadoReserva_704ILR.CONFIRMADA
                   || nueva_704ILR.FechaEvento_704ILR.Date != persistida_704ILR.FechaEvento_704ILR.Date);

        // RN-01: true si la operacion persistida ya vencio y se la quiere llevar a
        // OTRO estado. Es el unico criterio de vigencia y lo comparten la edicion y
        // la restauracion de versiones: conservar el estado sigue permitido,
        // cualquier cambio exige renovar antes.
        private static bool AvanceConVigenciaVencida_704ILR(BE_Reserva_704ILR persistida_704ILR, EstadoReserva_704ILR destino_704ILR)
            => persistida_704ILR.Estado_704ILR != destino_704ILR && persistida_704ILR.EstaVencida_704ILR;

        // RN-02: liquidacion sobre un total cobrado dado. La cancelacion la invoca con
        // lo cobrado releido dentro de su transaccion; CalcularCancelacion_704ILR, con
        // lo cobrado al momento de la consulta (anticipo que muestra la ficha).
        private static void LiquidarCancelacion_704ILR(BE_Reserva_704ILR reserva_704ILR, decimal pagado_704ILR,
            out decimal retenido_704ILR, out decimal reembolsable_704ILR)
        {
            retenido_704ILR = 0m;
            reembolsable_704ILR = 0m;
            if (reserva_704ILR == null || pagado_704ILR <= 0) return;

            int diasAntelacion_704ILR = (reserva_704ILR.FechaEvento_704ILR.Date - DateTime.Today).Days;
            if (diasAntelacion_704ILR >= DiasCancelacionSinPenalidad_704ILR)
            {
                reembolsable_704ILR = pagado_704ILR;
            }
            else
            {
                retenido_704ILR = decimal.Round(pagado_704ILR * PorcentajeRetencion_704ILR / 100m, 2,
                    MidpointRounding.AwayFromZero);   // redondeo comercial sobre importes
                reembolsable_704ILR = pagado_704ILR - retenido_704ILR;
            }
        }

        // RN-05: un estado PEDIDO por quien llama (alta o modificacion) que no figura en el
        // enum (posible por casteo desde un entero) no figura en la tabla de transiciones y
        // no puede persistirse. Deja asiento porque no es un error de tipeo: no hay pantalla
        // que lo produzca. El detalle identifica la operacion ("Reserva nueva" en el alta)
        // y el valor pedido, escrito con la cultura invariante.
        private static bool EstadoDefinido_704ILR(EstadoReserva_704ILR estado_704ILR, int reservaId_704ILR, string accion_704ILR)
        {
            if (Enum.IsDefined(typeof(EstadoReserva_704ILR), estado_704ILR)) return true;
            string referencia_704ILR = reservaId_704ILR > 0 ? "Reserva #" + reservaId_704ILR : "Reserva nueva";
            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", accion_704ILR, CriticidadBitacora_704ILR.Advertencia,
                $"{referencia_704ILR}: el estado pedido (valor {((int)estado_704ILR).ToString(CultureInfo.InvariantCulture)}) " +
                "esta fuera del ciclo de vida de la reserva (RN-05).");
            return false;
        }

        // RN-05: un estado ALMACENADO (en la reserva o en una de sus versiones) que no es
        // ninguno del ciclo de vida: fue escrito por fuera del sistema. La lectura tolerante
        // lo entrega como un valor fuera del enum y descarta el texto, asi que el texto real
        // se relee de la base (sobre la conexion y la transaccion de la operacion) solo para
        // el asiento: con el valor interno ("Estado '-1'") y sin la reserva ni la version, el
        // administrador no podia ubicar la alteracion. mementoId en 0 senala la reserva;
        // mayor que 0, esa version.
        private static bool EstadoAlmacenadoDefinido_704ILR(EstadoReserva_704ILR estadoLeido_704ILR, int reservaId_704ILR,
            int mementoId_704ILR, string accion_704ILR, Func<string> leerAlmacenado_704ILR)
        {
            if (Enum.IsDefined(typeof(EstadoReserva_704ILR), estadoLeido_704ILR)) return true;
            string almacenado_704ILR = leerAlmacenado_704ILR();
            string valor_704ILR = almacenado_704ILR == null ? string.Empty : " ('" + almacenado_704ILR + "')";
            string detalle_704ILR = mementoId_704ILR > 0
                ? $"Reserva #{reservaId_704ILR}: la version #{mementoId_704ILR} tiene un estado almacenado{valor_704ILR} " +
                  "fuera del ciclo de vida de la reserva (RN-05)."
                : $"Reserva #{reservaId_704ILR}: su estado almacenado{valor_704ILR} esta fuera del ciclo de vida de la reserva (RN-05).";
            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", accion_704ILR, CriticidadBitacora_704ILR.Advertencia, detalle_704ILR);
            return false;
        }

        // Cuando viajan los servicios, valida cada linea y fija el monto de la
        // cabecera como su suma. Con 'servicios' en null no hay lineas que juzgar y
        // el monto queda como lo informo el llamador (cabecera sola).
        // Devuelve false si alguna linea es invalida, incluido un precio unitario que
        // la base no puede almacenar (MontoMaximo_704ILR); el tope del total lo
        // controla Validar_704ILR.
        private static bool AplicarServicios_704ILR(BE_Reserva_704ILR reserva_704ILR,
            IList<BE_ReservaServicio_704ILR> servicios_704ILR)
        {
            if (servicios_704ILR == null) return true;
            if (!BLL_ReservaServicio_704ILR.ValidarLineas_704ILR(servicios_704ILR)) return false;
            foreach (var linea_704ILR in servicios_704ILR)
                if (linea_704ILR.PrecioUnitario_704ILR > MontoMaximo_704ILR) return false;
            reserva_704ILR.Monto_704ILR = BLL_ReservaServicio_704ILR.Total_704ILR(servicios_704ILR);
            return true;
        }

        // True si algun campo auditado de la cabecera difiere entre lo persistido y
        // lo pedido (mismos campos que CamposAuditados_704ILR).
        private static bool CabeceraCambio_704ILR(BE_Reserva_704ILR antes_704ILR, BE_Reserva_704ILR despues_704ILR)
            => antes_704ILR.ClienteId_704ILR != despues_704ILR.ClienteId_704ILR
            || antes_704ILR.SalonId_704ILR != despues_704ILR.SalonId_704ILR
            || antes_704ILR.FechaEvento_704ILR != despues_704ILR.FechaEvento_704ILR
            || antes_704ILR.Estado_704ILR != despues_704ILR.Estado_704ILR
            || antes_704ILR.Monto_704ILR != despues_704ILR.Monto_704ILR
            || antes_704ILR.CantidadInvitados_704ILR != despues_704ILR.CantidadInvitados_704ILR;

        // Digito verificador de una fila persistida: true si el almacenado coincide
        // con sus datos. Si no coincide, la fila fue alterada por fuera del sistema.
        private static bool DvhCoincide_704ILR(BE_Reserva_704ILR persistida_704ILR)
            => persistida_704ILR.Dvh_704ILR != null &&
               persistida_704ILR.Dvh_704ILR == ValidadorDeIntegridad_704ILR.CalcularDVH_704ILR(persistida_704ILR);

        // DV horizontal que se persiste al reescribir una reserva ya registrada. Sobre
        // una fila integra se calcula con los datos nuevos. Sobre una fila alterada por
        // fuera del sistema se conserva el almacenado: recalcularlo convertiria el dato
        // adulterado en linea base y la verificacion dejaria de detectarlo (lo mismo
        // que ya hace la renovacion, que no toca el DV). Queda asentado aparte.
        private static string DvhParaPersistir_704ILR(BE_Reserva_704ILR persistida_704ILR, BE_Reserva_704ILR nueva_704ILR)
            => DvhCoincide_704ILR(persistida_704ILR)
                ? ValidadorDeIntegridad_704ILR.CalcularDVH_704ILR(nueva_704ILR)
                : persistida_704ILR.Dvh_704ILR;

        private static void AsentarDvhNoCoincidente_704ILR(int reservaId_704ILR, string operacion_704ILR)
        {
            BLL_Bitacora_704ILR.Registrar_704ILR("Integridad", "Operacion sobre dato alterado", CriticidadBitacora_704ILR.Error,
                $"Reserva #{reservaId_704ILR}: su DV horizontal no coincide con los datos almacenados (posible " +
                $"alteracion externa). Se registro {operacion_704ILR} sin recalcularlo, para que la verificacion " +
                "de integridad lo siga detectando.");
        }

        // Estado almacenado fuera del dominio exacto de la tabla de estados (otra
        // capitalizacion, como 'confirmada'). La lectura tolerante lo toma como el estado
        // equivalente y toda escritura de la cabecera persiste el nombre exacto: este
        // asiento es la unica evidencia de la alteracion que queda, porque el DV horizontal
        // se calcula con ese nombre y no la detecta.
        // Por eso, a diferencia del resto de la bitacora (de mejor esfuerzo), es condicion de
        // la escritura: se detecta dentro de la transaccion, antes de reescribir la fila, y
        // se escribe ANTES de confirmarla por la bitacora directa, que informa la falla en
        // lugar de descartarla. Si no se puede escribir (la bitacora rechaza el asiento, esta
        // bloqueada hasta el tiempo de espera o la base cae) la excepcion deshace la
        // transaccion: la fila conserva el valor alterado y la verificacion de integridad la
        // sigue marcando. Antes se escribia despues del commit con el registro silencioso y,
        // si fallaba, la operacion normalizaba el estado y la alteracion desaparecia sin
        // rastro ni aviso. Si lo que fallara fuera la confirmacion posterior al asiento,
        // quedaria un asiento de mas y la fila todavia marcada: nunca una evidencia de menos.
        // Una operacion rechazada o sin cambios no escribe y no asienta: la fila sigue a la
        // vista de la verificacion. El DV horizontal conserva su politica: el almacenado no
        // se recalcula sobre una fila alterada y su asiento sigue despues del commit
        // (AsentarDvhNoCoincidente_704ILR), porque ahi la evidencia no se pierde.
        private static void AsentarEstadoFueraDeDominio_704ILR(int reservaId_704ILR, string estadoAlmacenado_704ILR,
            EstadoReserva_704ILR estadoEscrito_704ILR, string operacion_704ILR)
        {
            DAL_Bitacora_704ILR.Insert_704ILR(new BE_BitacoraEntry_704ILR
            {
                Fecha_704ILR = DateTime.Now,
                Usuario_704ILR = UsuarioActual_704ILR(),
                Modulo_704ILR = "Integridad",
                Accion_704ILR = "Operacion sobre dato alterado",
                Criticidad_704ILR = CriticidadBitacora_704ILR.Error,
                Detalle_704ILR = $"Reserva #{reservaId_704ILR}: su estado almacenado ('{estadoAlmacenado_704ILR}') esta fuera del dominio de " +
                    $"la tabla de estados (posible alteracion externa). Se registro {operacion_704ILR} y la fila quedo con el " +
                    $"estado {estadoEscrito_704ILR}: este asiento conserva el valor original."
            });
        }

        // Usuario de la sesion para el asiento que se escribe por la bitacora directa (el
        // mismo criterio que aplica BLL_Bitacora_704ILR al registrar).
        private static string UsuarioActual_704ILR()
        {
            try
            {
                return SessionManager_704ILR.IsSessionActive_704ILR
                    ? SessionManager_704ILR.GetInstance_704ILR.User_704ILR.Username_704ILR
                    : "Sistema";
            }
            catch { return "Sistema"; }
        }

        // Importes en el detalle de la bitacora: formato "0.00" fijo de es-AR (coma
        // decimal, sin separador de miles), el mismo que traen los asientos de la base de
        // demostracion, sin depender de la configuracion regional de la estacion que opera.
        private static readonly CultureInfo CulturaBitacora_704ILR = CultureInfo.GetCultureInfo("es-AR");

        private static string ImporteBitacora_704ILR(decimal importe_704ILR)
            => importe_704ILR.ToString("0.00", CulturaBitacora_704ILR);

        // Fechas en el detalle de la bitacora: calendario gregoriano y formato fijo
        // "yyyy-MM-dd HH:mm" con la cultura invariante, el mismo del historial de cambios
        // (RegistradorDeCambios_704ILR). Con la configuracion regional de la estacion el
        // mismo vencimiento quedaba asentado en otro calendario (2569 en th-TH, 1448 en
        // ar-SA, 1405 en fa-IR) o con otro separador de hora ("20.20" en fi-FI).
        private static string FechaBitacora_704ILR(DateTime? fecha_704ILR)
            => fecha_704ILR.HasValue
                ? fecha_704ILR.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : string.Empty;

        // RN-03 en el motor: el indice unico de (salon, fecha) sobre las confirmadas
        // es la red de seguridad cuando dos operaciones pasaron la validacion previa a
        // la vez. 2601 y 2627 son los errores de indice unico y de restriccion unica.
        private static bool EsChoqueDeUnicidad_704ILR(SqlException ex_704ILR)
            => ex_704ILR.Number == 2601 || ex_704ILR.Number == 2627;

        private static void AsentarRechazoDelMotor_704ILR(int reservaId_704ILR, string accion_704ILR)
        {
            string referencia_704ILR = reservaId_704ILR > 0 ? "Reserva #" + reservaId_704ILR : "Reserva nueva";
            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", accion_704ILR, CriticidadBitacora_704ILR.Advertencia,
                $"{referencia_704ILR}: el motor rechazo la escritura, el salon ya tiene una reserva " +
                "confirmada para esa fecha (RN-03).");
        }

        // Criterio de auditoria de los rechazos de Validar_704ILR: se asientan los que
        // son REGLA DE NEGOCIO (RN-03 salon comprometido, RN-06 capacidad), porque
        // describen un conflicto real de la operacion y son informacion de gestion.
        // Los errores de tipeo (cliente o salon inexistente, fecha pasada o fuera del
        // calendario, monto o invitados fuera de rango) se corrigen en pantalla y no dejan
        // asiento: serian ruido.
        private static void AsentarRechazoDeRegla_704ILR(int reservaId_704ILR,
            ReservaResult_704ILR motivo_704ILR, string accion_704ILR)
        {
            string regla_704ILR;
            if (motivo_704ILR == ReservaResult_704ILR.SalonOcupado_704ILR)
                regla_704ILR = "el salon ya tiene una reserva confirmada para esa fecha (RN-03)";
            else if (motivo_704ILR == ReservaResult_704ILR.CapacidadInsuficiente_704ILR)
                regla_704ILR = "el salon no aloja a los invitados estimados (RN-06)";
            else
                return;

            string referencia_704ILR = reservaId_704ILR > 0 ? "Reserva #" + reservaId_704ILR : "Reserva nueva";
            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", accion_704ILR,
                CriticidadBitacora_704ILR.Advertencia, $"{referencia_704ILR}: {regla_704ILR}.");
        }

        private static ReservaResult_704ILR Validar_704ILR(BE_Reserva_704ILR reserva_704ILR, bool permitirFechaPasada_704ILR = false)
        {
            if (reserva_704ILR == null || reserva_704ILR.ClienteId_704ILR <= 0 || !DAL_Cliente_704ILR.Exists_704ILR(reserva_704ILR.ClienteId_704ILR))
                return ReservaResult_704ILR.InvalidCliente_704ILR;

            if (reserva_704ILR.SalonId_704ILR <= 0 || !DAL_Salon_704ILR.Exists_704ILR(reserva_704ILR.SalonId_704ILR))
                return ReservaResult_704ILR.InvalidSalon_704ILR;

            // Una reserva nueva no puede agendarse en el pasado (al restaurar una
            // version historica esta regla se relaja). Ninguna operacion, tampoco la
            // restauracion, admite una fecha posterior al ultimo dia que muestra la ficha
            // (FechaEventoMaxima_704ILR, a las 00:00 como el selector, asi que se compara con la hora):
            // la reserva quedaria sin poder mostrarse ni corregirse.
            if (reserva_704ILR.FechaEvento_704ILR == default ||
                reserva_704ILR.FechaEvento_704ILR > FechaEventoMaxima_704ILR ||
                (!permitirFechaPasada_704ILR && reserva_704ILR.FechaEvento_704ILR.Date < DateTime.Today))
                return ReservaResult_704ILR.InvalidFecha_704ILR;

            // Monto entre cero y lo que la base puede almacenar (MontoMaximo_704ILR).
            if (reserva_704ILR.Monto_704ILR < 0 || reserva_704ILR.Monto_704ILR > MontoMaximo_704ILR)
                return ReservaResult_704ILR.InvalidMonto_704ILR;

            // Invitados entre cero y el maximo que admite el campo de la ficha (InvitadosMaximo_704ILR).
            if (reserva_704ILR.CantidadInvitados_704ILR < 0 || reserva_704ILR.CantidadInvitados_704ILR > InvitadosMaximo_704ILR)
                return ReservaResult_704ILR.InvalidInvitados_704ILR;

            // RN-06: al comprometer el salon hay que saber a cuanta gente hay que alojar,
            // y el salon tiene que poder hacerlo. En COTIZACION y PENDIENTE el dato puede
            // faltar: la propuesta todavia se esta componiendo.
            if (reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA)
            {
                if (reserva_704ILR.CantidadInvitados_704ILR <= 0)
                    return ReservaResult_704ILR.InvalidInvitados_704ILR;
                if (!CapacidadSuficiente_704ILR(reserva_704ILR.SalonId_704ILR, reserva_704ILR.CantidadInvitados_704ILR))
                    return ReservaResult_704ILR.CapacidadInsuficiente_704ILR;
            }

            // Anti-solapamiento: el salon se compromete solo al CONFIRMAR. Una
            // cotizacion o una reserva pendiente no bloquean (puede haber varias
            // para la misma fecha); recien al confirmar se verifica que no haya
            // otra reserva firme ese dia. Se excluye la propia reserva.
            if (reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA &&
                DAL_Reserva_704ILR.SalonOcupado_704ILR(reserva_704ILR.SalonId_704ILR, reserva_704ILR.FechaEvento_704ILR, reserva_704ILR.Id_704ILR))
                return ReservaResult_704ILR.SalonOcupado_704ILR;

            return ReservaResult_704ILR.Success_704ILR;
        }
    }
}
