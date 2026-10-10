using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum PagoResult_704ILR
    {
        Success_704ILR,
        MontoInvalido_704ILR,
        MetodoInvalido_704ILR,
        ExcedeSaldo_704ILR,
        ReservaInvalida_704ILR,       // la reserva no existe o su estado almacenado esta fuera del ciclo de vida (RN-05)
        ReservaCancelada_704ILR,      // estado terminal: no admite movimientos de cobro
        PagoInvalido_704ILR,          // el pago a anular no existe o es de otra reserva
        ConfirmadaSinAdelanto_704ILR  // RN-07: la anulacion dejaria una CONFIRMADA sin nada cobrado
    }

    // Reglas de negocio de pagos (Proceso 1, paso 5): cobro de adelanto/saldo de
    // una reserva. El total de la reserva (Monto = suma de servicios) actua como
    // tope: la suma de pagos nunca puede superarlo.
    //
    // Los pagos se protegen con digito verificador, como las reservas: cada cobro
    // guarda el horizontal de su fila dentro de la misma transaccion y, con el
    // movimiento ya confirmado (cobro o anulacion), se recalcula el vertical de la
    // tabla. La verificacion del arranque (BLL_Integridad) detecta asi un pago
    // alterado, agregado o quitado por fuera del sistema.
    public static class BLL_Pago_704ILR
    {
        // Los importes del detalle de la bitacora se escriben siempre con el mismo
        // formato ("1500,50", el de los asientos que ya trae la base de demostracion),
        // sin depender de la configuracion regional de la estacion que opero: antes el
        // mismo cobro quedaba asentado como "1500,50" o como "1500.50" segun el equipo.
        private static readonly CultureInfo CulturaBitacora_704ILR = CultureInfo.GetCultureInfo("es-AR");

        public static List<BE_MetodoPago_704ILR> GetMetodos_704ILR() => DAL_MetodoPago_704ILR.GetAll_704ILR();

        public static List<BE_Pago_704ILR> GetByReserva_704ILR(int reservaId_704ILR) => DAL_Pago_704ILR.GetByReserva_704ILR(reservaId_704ILR);

        public static decimal TotalPagado_704ILR(int reservaId_704ILR) => DAL_Pago_704ILR.TotalPagado_704ILR(reservaId_704ILR);

        public static decimal MontoReserva_704ILR(int reservaId_704ILR)
        {
            var r_704ILR = BLL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR);
            return r_704ILR == null ? 0m : r_704ILR.Monto_704ILR;
        }

        // Saldo pendiente = total de la reserva - lo ya pagado.
        public static decimal Saldo_704ILR(int reservaId_704ILR) => MontoReserva_704ILR(reservaId_704ILR) - TotalPagado_704ILR(reservaId_704ILR);

        public static PagoResult_704ILR Registrar_704ILR(BE_Pago_704ILR p_704ILR, out int nuevoId_704ILR)
        {
            nuevoId_704ILR = 0;
            if (p_704ILR == null || p_704ILR.ReservaId_704ILR <= 0) return PagoResult_704ILR.ReservaInvalida_704ILR;
            if (p_704ILR.MetodoPagoId_704ILR <= 0) return PagoResult_704ILR.MetodoInvalido_704ILR;
            // Pagos.Monto es DECIMAL(12,2) y la pantalla muestra dos decimales: las reglas
            // se aplican sobre el importe que se guarda y que el usuario ve. Antes 0,004
            // pasaba como positivo y se guardaba un pago de 0,00, y 1000,004 superaba un
            // saldo de 1000,00 que en pantalla alcanzaba justo. El redondeo es el
            // comercial (mitad hacia arriba), el mismo que aplica la base al guardar, y
            // queda en el pago recibido: el asiento informa el importe realmente cobrado.
            p_704ILR.Monto_704ILR = decimal.Round(p_704ILR.Monto_704ILR, 2, MidpointRounding.AwayFromZero);
            // Tope superior: lo que admite la columna, el mismo que el total de una reserva
            // (MontoMaximo_704ILR). Un importe mayor es un monto invalido, con el mismo
            // aviso que da la pantalla, y no llega al motor.
            if (p_704ILR.Monto_704ILR <= 0 || p_704ILR.Monto_704ILR > BLL_Reserva_704ILR.MontoMaximo_704ILR)
                return PagoResult_704ILR.MontoInvalido_704ILR;

            // Validacion y alta en UNA transaccion: la cabecera de la reserva se lee
            // con bloqueo de actualizacion y el total cobrado se relee adentro, asi
            // dos cobros simultaneos sobre la misma reserva se serializan y el
            // segundo valida contra lo que el primero ya dejo escrito. Antes cada
            // paso abria su propia conexion y los dos podian pasar el tope RN-04.
            PagoResult_704ILR resultado_704ILR;
            decimal montoReserva_704ILR = 0m, pagado_704ILR = 0m;
            bool estadoAjeno_704ILR = false, dvhAlterado_704ILR = false;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    var reserva_704ILR = DAL_Reserva_704ILR.GetById_704ILR(p_704ILR.ReservaId_704ILR, conn_704ILR, tx_704ILR);
                    if (reserva_704ILR == null)
                        resultado_704ILR = PagoResult_704ILR.ReservaInvalida_704ILR;
                    // Una reserva cancelada es estado terminal: tampoco admite cobros.
                    // La regla vive aca (y no solo en la UI) porque los pagos persisten en
                    // el acto, sin pasar por la validacion de BLL_Reserva.Actualizar.
                    else if (!BLL_Reserva_704ILR.PuedeModificar_704ILR(reserva_704ILR))
                        resultado_704ILR = PagoResult_704ILR.ReservaCancelada_704ILR;
                    // RN-05: un estado almacenado que no figura en el ciclo de vida (escrito
                    // por fuera del sistema) tampoco admite movimientos de cobro, como no
                    // admite edicion, cancelacion ni restauracion.
                    else if (!EstadoDefinido_704ILR(reserva_704ILR))
                    {
                        estadoAjeno_704ILR = true;
                        resultado_704ILR = PagoResult_704ILR.ReservaInvalida_704ILR;
                    }
                    else
                    {
                        // Tope: no se puede pagar mas que el total de la reserva (RN-04).
                        montoReserva_704ILR = reserva_704ILR.Monto_704ILR;
                        pagado_704ILR = DAL_Pago_704ILR.TotalPagado_704ILR(p_704ILR.ReservaId_704ILR, conn_704ILR, tx_704ILR);
                        if (pagado_704ILR + p_704ILR.Monto_704ILR > montoReserva_704ILR)
                            resultado_704ILR = PagoResult_704ILR.ExcedeSaldo_704ILR;
                        else
                        {
                            // Politica de dato alterado de BLL_Reserva: si el DV horizontal de
                            // la cabecera no coincide con sus datos, el cobro se valida contra
                            // un total que pudo alterarse por fuera del sistema. Procede, pero
                            // queda asentado aparte (los pagos no tocan el DV de la reserva).
                            dvhAlterado_704ILR = !DvhCoincide_704ILR(reserva_704ILR);
                            nuevoId_704ILR = DAL_Pago_704ILR.Insert_704ILR(p_704ILR, conn_704ILR, tx_704ILR);
                            // Digito verificador horizontal del pago: se calcula sobre la
                            // fila tal como quedo guardada (la fecha la pone el servidor) y
                            // se graba en esta misma transaccion: el pago queda registrado
                            // con su digito o no queda.
                            var guardado_704ILR = DAL_Pago_704ILR.GetById_704ILR(nuevoId_704ILR, conn_704ILR, tx_704ILR);
                            DAL_Pago_704ILR.UpdateDvh_704ILR(nuevoId_704ILR,
                                ValidadorDeIntegridad_704ILR.CalcularDVH_704ILR(guardado_704ILR), conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                            resultado_704ILR = PagoResult_704ILR.Success_704ILR;
                        }
                    }
                    // Si no hubo Commit, cerrar la transaccion la deshace y libera el bloqueo.
                }
            }

            // Los asientos van despues de cerrar la transaccion, como en el resto de
            // las operaciones: la bitacora no participa del bloqueo. El rechazo por
            // tope se asienta —es una regla de negocio, no un error de tipeo— con el
            // mismo formato de Advertencia que usan los demas rechazos de cobro.
            switch (resultado_704ILR)
            {
                case PagoResult_704ILR.ReservaInvalida_704ILR:
                    if (estadoAjeno_704ILR) AsentarEstadoAjeno_704ILR("Pago rechazado", p_704ILR.ReservaId_704ILR);
                    break;
                case PagoResult_704ILR.ReservaCancelada_704ILR:
                    BLL_Bitacora_704ILR.Registrar_704ILR("Pagos", "Pago rechazado", CriticidadBitacora_704ILR.Advertencia,
                        $"Reserva #{p_704ILR.ReservaId_704ILR} cancelada: no admite movimientos de cobro.");
                    break;
                case PagoResult_704ILR.ExcedeSaldo_704ILR:
                    BLL_Bitacora_704ILR.Registrar_704ILR("Pagos", "Pago rechazado", CriticidadBitacora_704ILR.Advertencia,
                        $"Reserva #{p_704ILR.ReservaId_704ILR}: un cobro de {Importe_704ILR(p_704ILR.Monto_704ILR)} " +
                        $"supera el saldo pendiente ({Importe_704ILR(montoReserva_704ILR - pagado_704ILR)}) (RN-04).");
                    break;
                case PagoResult_704ILR.Success_704ILR:
                    if (dvhAlterado_704ILR)
                        AsentarDvhNoCoincidente_704ILR(p_704ILR.ReservaId_704ILR, "el cobro de " + Importe_704ILR(p_704ILR.Monto_704ILR));
                    BLL_Bitacora_704ILR.Registrar_704ILR("Pagos", "Registro de pago", CriticidadBitacora_704ILR.Info,
                        $"Pago de {Importe_704ILR(p_704ILR.Monto_704ILR)} en reserva #{p_704ILR.ReservaId_704ILR} (metodo #{p_704ILR.MetodoPagoId_704ILR})");
                    RecalcularDVVertical_704ILR($"al cobro #{nuevoId_704ILR} de la reserva #{p_704ILR.ReservaId_704ILR}");
                    break;
            }
            return resultado_704ILR;
        }

        // Anular un pago es un movimiento de cobranza mas y pasa por las mismas reglas
        // que registrarlo: el pago tiene que existir, pertenecer a la reserva que la
        // pantalla dice, y la reserva tiene que admitir movimientos (una CANCELADA es
        // estado terminal, RN-04). Antes esto borraba la fila sin mirar nada y el
        // numero de reserva solo se usaba para armar el texto del asiento. Ademas, una
        // reserva CONFIRMADA no puede quedar sin nada cobrado (RN-07).
        public static PagoResult_704ILR Eliminar_704ILR(int pagoId_704ILR, int reservaId_704ILR)
        {
            if (pagoId_704ILR <= 0 || reservaId_704ILR <= 0) return PagoResult_704ILR.ReservaInvalida_704ILR;

            // Misma transaccion que el cobro (ver Registrar_704ILR). La cabecera se
            // bloquea ANTES de leer el pago: dos anulaciones del mismo pago se serializan
            // en esa lectura y la segunda lee el pago cuando la primera ya lo borro, asi
            // responde que el pago no existe. Antes el pago se leia sin bloqueo y la
            // segunda validaba la RN-07 contra un pago ya anulado (aviso falso) o
            // informaba y asentaba por segunda vez la misma anulacion.
            PagoResult_704ILR resultado_704ILR;
            decimal montoPago_704ILR = 0m;
            bool estadoAjeno_704ILR = false, dvhAlterado_704ILR = false, dvhDelPagoAlterado_704ILR = false;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    var reserva_704ILR = DAL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                    var pago_704ILR = DAL_Pago_704ILR.GetById_704ILR(pagoId_704ILR, conn_704ILR, tx_704ILR);
                    if (pago_704ILR == null || pago_704ILR.ReservaId_704ILR != reservaId_704ILR)
                        resultado_704ILR = PagoResult_704ILR.PagoInvalido_704ILR;
                    else
                    {
                        montoPago_704ILR = pago_704ILR.Monto_704ILR;
                        if (reserva_704ILR == null)
                            resultado_704ILR = PagoResult_704ILR.ReservaInvalida_704ILR;
                        else if (!BLL_Reserva_704ILR.PuedeModificar_704ILR(reserva_704ILR))
                            resultado_704ILR = PagoResult_704ILR.ReservaCancelada_704ILR;
                        // RN-05: estado almacenado fuera del ciclo de vida (ver Registrar_704ILR).
                        else if (!EstadoDefinido_704ILR(reserva_704ILR))
                        {
                            estadoAjeno_704ILR = true;
                            resultado_704ILR = PagoResult_704ILR.ReservaInvalida_704ILR;
                        }
                        // RN-07: una CONFIRMADA quedo firme porque se cobro el adelanto (tabla
                        // de estados de G02) y no puede volver a PENDIENTE (RN-05). Si la
                        // anulacion la dejaria sin nada cobrado, se rechaza: seguiria
                        // comprometiendo el salon (RN-03) sin respaldo. Es el mismo criterio de
                        // TieneAdelanto (lo cobrado mayor que cero) y lo cobrado se relee en esta
                        // transaccion, con la cabecera ya bloqueada: dos anulaciones simultaneas
                        // de sus dos ultimos pagos no pueden pasar las dos.
                        else if (reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA &&
                                 DAL_Pago_704ILR.TotalPagado_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR) - montoPago_704ILR <= 0m)
                            resultado_704ILR = PagoResult_704ILR.ConfirmadaSinAdelanto_704ILR;
                        else
                        {
                            // Politica de dato alterado (ver Registrar_704ILR). Vale tambien
                            // para el pago que se anula: si su digito verificador no coincide
                            // con sus datos, la anulacion procede y queda asentada aparte, porque
                            // con la baja de la fila desaparece lo que la verificacion detectaba.
                            dvhAlterado_704ILR = !DvhCoincide_704ILR(reserva_704ILR);
                            dvhDelPagoAlterado_704ILR = DvhAlterado_704ILR(pago_704ILR);
                            // Defensa: si el pago ya no estaba (lo borro una escritura que no
                            // paso por el bloqueo de la cabecera), no se informa ni se asienta
                            // una anulacion que no ocurrio.
                            if (DAL_Pago_704ILR.Delete_704ILR(pagoId_704ILR, conn_704ILR, tx_704ILR) == 0)
                                resultado_704ILR = PagoResult_704ILR.PagoInvalido_704ILR;
                            else
                            {
                                tx_704ILR.Commit();
                                resultado_704ILR = PagoResult_704ILR.Success_704ILR;
                            }
                        }
                    }
                }
            }

            switch (resultado_704ILR)
            {
                case PagoResult_704ILR.PagoInvalido_704ILR:
                    BLL_Bitacora_704ILR.Registrar_704ILR("Pagos", "Anulacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                        $"Pago #{pagoId_704ILR} inexistente o ajeno a la reserva #{reservaId_704ILR}.");
                    break;
                case PagoResult_704ILR.ReservaInvalida_704ILR:
                    if (estadoAjeno_704ILR) AsentarEstadoAjeno_704ILR("Anulacion rechazada", reservaId_704ILR);
                    break;
                case PagoResult_704ILR.ReservaCancelada_704ILR:
                    BLL_Bitacora_704ILR.Registrar_704ILR("Pagos", "Anulacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                        $"Reserva #{reservaId_704ILR} cancelada: no admite movimientos de cobro (RN-04).");
                    break;
                case PagoResult_704ILR.ConfirmadaSinAdelanto_704ILR:
                    BLL_Bitacora_704ILR.Registrar_704ILR("Pagos", "Anulacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                        $"Reserva #{reservaId_704ILR} confirmada: anular el pago #{pagoId_704ILR} de {Importe_704ILR(montoPago_704ILR)} la dejaria sin adelanto (RN-07).");
                    break;
                case PagoResult_704ILR.Success_704ILR:
                    if (dvhAlterado_704ILR)
                        AsentarDvhNoCoincidente_704ILR(reservaId_704ILR, "la anulacion del pago #" + pagoId_704ILR);
                    if (dvhDelPagoAlterado_704ILR)
                        BLL_Bitacora_704ILR.Registrar_704ILR("Integridad", "Operacion sobre dato alterado", CriticidadBitacora_704ILR.Error,
                            $"Pago #{pagoId_704ILR} de la reserva #{reservaId_704ILR}: su DV horizontal no coincidia con los datos " +
                            $"almacenados (posible alteracion externa). Se anulo con el importe almacenado, {Importe_704ILR(montoPago_704ILR)}.");
                    BLL_Bitacora_704ILR.Registrar_704ILR("Pagos", "Anulacion de pago", CriticidadBitacora_704ILR.Advertencia,
                        $"Pago #{pagoId_704ILR} de {Importe_704ILR(montoPago_704ILR)} en la reserva #{reservaId_704ILR} anulado");
                    RecalcularDVVertical_704ILR($"a la anulacion del pago #{pagoId_704ILR} de la reserva #{reservaId_704ILR}");
                    break;
            }
            return resultado_704ILR;
        }

        // Importe para el detalle de la bitacora (ver CulturaBitacora_704ILR).
        private static string Importe_704ILR(decimal monto_704ILR) => monto_704ILR.ToString("0.00", CulturaBitacora_704ILR);

        // RN-05: la lectura de la cabecera devuelve un valor fuera del enum cuando el
        // texto almacenado no es ningun estado. Esa reserva no admite movimientos.
        private static bool EstadoDefinido_704ILR(BE_Reserva_704ILR reserva_704ILR)
            => Enum.IsDefined(typeof(EstadoReserva_704ILR), reserva_704ILR.Estado_704ILR);

        private static void AsentarEstadoAjeno_704ILR(string accion_704ILR, int reservaId_704ILR)
            => BLL_Bitacora_704ILR.Registrar_704ILR("Pagos", accion_704ILR, CriticidadBitacora_704ILR.Advertencia,
                $"Reserva #{reservaId_704ILR}: estado almacenado fuera del ciclo de vida de la reserva, " +
                "no admite movimientos de cobro (RN-05).");

        // Mismo criterio que BLL_Reserva: el DV horizontal almacenado tiene que
        // coincidir con los datos de la fila leida; si no, fue alterada por fuera.
        private static bool DvhCoincide_704ILR(BE_Reserva_704ILR persistida_704ILR)
            => persistida_704ILR.Dvh_704ILR != null &&
               persistida_704ILR.Dvh_704ILR == ValidadorDeIntegridad_704ILR.CalcularDVH_704ILR(persistida_704ILR);

        // Para la fila de un pago: true si tiene digito y no coincide con sus datos. Un
        // pago SIN digito no se toma aca como alterado: es de una base anterior a esta
        // proteccion que todavia no establecio su linea base, o una fila agregada por
        // fuera, y eso lo informa la verificacion del arranque (anularlo no tiene que
        // dejar asentada una alteracion que nadie comprobo).
        private static bool DvhAlterado_704ILR(BE_Pago_704ILR persistido_704ILR)
            => persistido_704ILR.Dvh_704ILR != null &&
               persistido_704ILR.Dvh_704ILR != ValidadorDeIntegridad_704ILR.CalcularDVH_704ILR(persistido_704ILR);

        // El cobro o la anulacion ya quedaron confirmados en la base: el digito
        // verificador vertical de Pagos es evidencia derivada, como el de Reservas en
        // BLL_Reserva. Un fallo aca no se informa como error del movimiento —el
        // reintento lo duplicaria—: queda asentado como excepcion y la verificacion del
        // proximo arranque detecta y alerta un digito vertical desactualizado.
        // 'operacion' completa la frase del asiento: "al cobro #N..." / "a la anulacion...".
        private static void RecalcularDVVertical_704ILR(string operacion_704ILR)
        {
            try
            {
                BLL_Integridad_704ILR.RecalcularDVVerticalPagos_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Pagos",
                    $"evidencia posterior {operacion_704ILR} (DV vertical)");
            }
        }

        private static void AsentarDvhNoCoincidente_704ILR(int reservaId_704ILR, string operacion_704ILR)
            => BLL_Bitacora_704ILR.Registrar_704ILR("Integridad", "Operacion sobre dato alterado", CriticidadBitacora_704ILR.Error,
                $"Reserva #{reservaId_704ILR}: su DV horizontal no coincide con los datos almacenados (posible " +
                $"alteracion externa). Se registro {operacion_704ILR} sobre esos datos; el DV no se recalcula, " +
                "para que la verificacion de integridad lo siga detectando.");
    }
}
