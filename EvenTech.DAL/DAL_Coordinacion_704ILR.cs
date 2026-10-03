using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Estado de coordinacion de los eventos (Proceso 2) y consulta de los eventos
    // a coordinar. El estado se guarda en Reservas.EstadoCoordinacion, pero ninguna
    // escritura de la reserva lo toca: se lee y se escribe solo por aca.
    public static class DAL_Coordinacion_704ILR
    {
        // Una sola consulta para toda la pantalla de operaciones: la reserva, su
        // estado de coordinacion y los conteos que resumen cuanto se avanzo. Los
        // estados viajan como parametros desde los enumerados, igual que en el resto
        // de las consultas por estado.
        private const string SelectEventos_704ILR =
            "SELECT r.Id, LTRIM(ISNULL(c.Nombre,'') + ISNULL(' ' + c.Apellido,'')), s.Nombre, r.FechaEvento, " +
            "r.CantidadInvitados, r.EstadoCoordinacion, " +
            "(SELECT COUNT(*) FROM dbo.AsignacionesPersonal a WHERE a.ReservaId = r.Id), " +
            "(SELECT COUNT(*) FROM dbo.AsignacionesPersonal a WHERE a.ReservaId = r.Id AND a.Estado = @aConfirmada), " +
            "(SELECT COUNT(*) FROM dbo.AsignacionesPersonal a WHERE a.ReservaId = r.Id AND a.Estado = @aPendiente), " +
            "(SELECT COUNT(*) FROM dbo.AsignacionesPersonal a WHERE a.ReservaId = r.Id AND a.Estado = @aRechazada), " +
            "(SELECT COUNT(*) FROM dbo.CronogramaActividades ca INNER JOIN dbo.Cronogramas cr ON cr.Id = ca.CronogramaId WHERE cr.ReservaId = r.Id), " +
            "(SELECT COUNT(*) FROM dbo.Cronogramas cr WHERE cr.ReservaId = r.Id), " +
            "(SELECT COUNT(*) FROM dbo.Tareas t INNER JOIN dbo.Cronogramas cr ON cr.Id = t.CronogramaId WHERE cr.ReservaId = r.Id), " +
            "(SELECT COUNT(*) FROM dbo.Incidencias i WHERE i.ReservaId = r.Id AND i.Estado = @iAbierta), " +
            "(SELECT COUNT(*) FROM dbo.Incidencias i WHERE i.ReservaId = r.Id), r.Estado " +
            "FROM dbo.Reservas r " +
            "INNER JOIN dbo.Salones s ON s.Id = r.SalonId " +
            "LEFT JOIN dbo.Clientes c ON c.Id = r.ClienteId ";

        // Eventos a coordinar: las reservas CONFIRMADA (RN-08), por fecha del evento.
        // Los ya cerrados van al final: no tienen nada pendiente y, por fecha, quedarian
        // arriba de los que todavia hay que coordinar. El desempate por Id hace
        // determinista el orden.
        public static List<BE_EventoCoordinacion_704ILR> GetEventos_704ILR()
        {
            var list_704ILR = new List<BE_EventoCoordinacion_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectEventos_704ILR +
                "WHERE r.Estado = @estado ORDER BY CASE WHEN r.EstadoCoordinacion = @cerrado THEN 1 ELSE 0 END, r.FechaEvento, r.Id", cn_704ILR.OpenConnection_704ILR()))
            {
                BindEstados_704ILR(cmd_704ILR);
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = EstadoReserva_704ILR.CONFIRMADA.ToString();
                cmd_704ILR.Parameters.Add("@cerrado", SqlDbType.NVarChar, 20).Value = EstadoCoordinacion_704ILR.CERRADO.ToString();
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            }
            return list_704ILR;
        }

        // Un evento por reserva, cualquiera sea su estado comercial (null si no existe).
        public static BE_EventoCoordinacion_704ILR GetEvento_704ILR(int reservaId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectEventos_704ILR + "WHERE r.Id = @id", cn_704ILR.OpenConnection_704ILR()))
            {
                BindEstados_704ILR(cmd_704ILR);
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = reservaId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        // Lo que decide el estado de coordinacion mientras el evento no empezo: cuantas
        // asignaciones hay, cuantas estan confirmadas y si el cronograma existe. Se lee
        // sobre la transaccion de la operacion, con la cabecera de la reserva bloqueada.
        public static void Conteos_704ILR(int reservaId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR,
            out int asignaciones_704ILR, out int confirmadas_704ILR, out bool tieneCronograma_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "SELECT (SELECT COUNT(*) FROM dbo.AsignacionesPersonal WHERE ReservaId = @r), " +
                "(SELECT COUNT(*) FROM dbo.AsignacionesPersonal WHERE ReservaId = @r AND Estado = @aConfirmada), " +
                "(SELECT COUNT(*) FROM dbo.Cronogramas WHERE ReservaId = @r)", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                cmd_704ILR.Parameters.Add("@aConfirmada", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.CONFIRMADA.ToString();
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    r_704ILR.Read();
                    asignaciones_704ILR = r_704ILR.GetInt32(0);
                    confirmadas_704ILR = r_704ILR.GetInt32(1);
                    tieneCronograma_704ILR = r_704ILR.GetInt32(2) > 0;
                }
            }
        }

        public static void SetEstado_704ILR(int reservaId_704ILR, EstadoCoordinacion_704ILR estado_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand("UPDATE dbo.Reservas SET EstadoCoordinacion = @e WHERE Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@e", SqlDbType.NVarChar, 20).Value = estado_704ILR.ToString();
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = reservaId_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Lectura del estado almacenado. La columna tiene dominio cerrado (CHECK); un
        // texto que no corresponda a ningun estado se lee como SIN_ASIGNAR, el estado
        // del que parte toda reserva.
        internal static EstadoCoordinacion_704ILR EstadoDesdeTexto_704ILR(string texto_704ILR)
            => Enum.TryParse((texto_704ILR ?? string.Empty).Trim(), true, out EstadoCoordinacion_704ILR estado_704ILR)
               && Enum.IsDefined(typeof(EstadoCoordinacion_704ILR), estado_704ILR)
                ? estado_704ILR
                : EstadoCoordinacion_704ILR.SIN_ASIGNAR;

        private static void BindEstados_704ILR(SqlCommand cmd_704ILR)
        {
            cmd_704ILR.Parameters.Add("@aConfirmada", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.CONFIRMADA.ToString();
            cmd_704ILR.Parameters.Add("@aPendiente", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.PENDIENTE.ToString();
            cmd_704ILR.Parameters.Add("@aRechazada", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.RECHAZADA.ToString();
            cmd_704ILR.Parameters.Add("@iAbierta", SqlDbType.NVarChar, 20).Value = EstadoIncidencia_704ILR.ABIERTA.ToString();
        }

        private static BE_EventoCoordinacion_704ILR Map_704ILR(SqlDataReader r_704ILR) => new BE_EventoCoordinacion_704ILR
        {
            ReservaId_704ILR = r_704ILR.GetInt32(0),
            ClienteNombre_704ILR = r_704ILR.IsDBNull(1) ? string.Empty : r_704ILR.GetString(1),
            SalonNombre_704ILR = r_704ILR.GetString(2),
            FechaEvento_704ILR = r_704ILR.GetDateTime(3),
            CantidadInvitados_704ILR = r_704ILR.IsDBNull(4) ? 0 : r_704ILR.GetInt32(4),
            EstadoCoordinacion_704ILR = EstadoDesdeTexto_704ILR(r_704ILR.IsDBNull(5) ? null : r_704ILR.GetString(5)),
            Asignados_704ILR = r_704ILR.GetInt32(6),
            Confirmados_704ILR = r_704ILR.GetInt32(7),
            Pendientes_704ILR = r_704ILR.GetInt32(8),
            Rechazados_704ILR = r_704ILR.GetInt32(9),
            Actividades_704ILR = r_704ILR.GetInt32(10),
            TieneCronograma_704ILR = r_704ILR.GetInt32(11) > 0,
            Tareas_704ILR = r_704ILR.GetInt32(12),
            IncidenciasAbiertas_704ILR = r_704ILR.GetInt32(13),
            Incidencias_704ILR = r_704ILR.GetInt32(14),
            Estado_704ILR = DAL_Reserva_704ILR.EstadoDesdeTexto_704ILR(r_704ILR.GetString(15))
        };
    }
}
