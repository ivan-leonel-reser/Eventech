using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Caretaker (patron Memento): guarda y devuelve las versiones (mementos) de
    // una reserva sin interpretar su contenido. La decision de que entra en la
    // foto y como se repone es exclusiva del Originator (BE_Reserva).
    public static class CaretakerReserva_704ILR
    {
        // Toma la foto del estado actual de la reserva (incluye sus servicios
        // contratados, porque el monto se deriva de ellos) y la persiste sobre la
        // conexion y la transaccion de la operacion que la motiva (modificacion,
        // restauracion o cancelacion): si esa operacion no llega a confirmarse, la
        // version tampoco queda. Se invoca con la cabecera de la reserva ya bloqueada
        // y ANTES de escribir sus lineas: los servicios se leen por otra conexion y en
        // ese punto nadie mas puede estar reescribiendolos.
        public static void GuardarVersion_704ILR(BE_Reserva_704ILR reserva_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            List<BE_ReservaServicio_704ILR> servicios_704ILR = DAL_ReservaServicio_704ILR.GetByReserva_704ILR(reserva_704ILR.Id_704ILR);
            BE_ReservaMemento_704ILR memento_704ILR = reserva_704ILR.CrearMemento_704ILR(UsuarioActual_704ILR(), servicios_704ILR);
            DAL_ReservaMemento_704ILR.Insert_704ILR(memento_704ILR, conn_704ILR, tx_704ILR);
        }

        public static List<BE_ReservaMemento_704ILR> GetVersiones_704ILR(int reservaId_704ILR) =>
            DAL_ReservaMemento_704ILR.GetByReserva_704ILR(reservaId_704ILR);

        public static BE_ReservaMemento_704ILR GetVersion_704ILR(int mementoId_704ILR) =>
            DAL_ReservaMemento_704ILR.GetById_704ILR(mementoId_704ILR);

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
    }
}
