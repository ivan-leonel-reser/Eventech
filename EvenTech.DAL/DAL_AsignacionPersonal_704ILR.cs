using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Asignaciones de personal a los eventos (Proceso 2: CUN006 y CUN007).
    public static class DAL_AsignacionPersonal_704ILR
    {
        // SELECT base con JOIN al empleado, a su especialidad y a la reserva para
        // proyectar sus nombres y la fecha del evento (la franja se interpreta sobre
        // esa fecha).
        private const string SelectBase_704ILR =
            "SELECT a.Id, a.ReservaId, a.EmpleadoId, e.Apellido + ', ' + e.Nombre, es.Nombre, a.RolAsignado, " +
            "a.HoraInicio, a.HoraFin, a.Estado, a.FechaConfirmacion, a.MotivoRechazo, a.CreatedAt, " +
            "r.FechaEvento, s.Nombre, LTRIM(ISNULL(c.Nombre,'') + ISNULL(' ' + c.Apellido,'')), r.EstadoCoordinacion " +
            "FROM dbo.AsignacionesPersonal a " +
            "INNER JOIN dbo.Empleados e ON e.Id = a.EmpleadoId " +
            "INNER JOIN dbo.Especialidades es ON es.Id = e.EspecialidadId " +
            "INNER JOIN dbo.Reservas r ON r.Id = a.ReservaId " +
            "INNER JOIN dbo.Salones s ON s.Id = r.SalonId " +
            "LEFT JOIN dbo.Clientes c ON c.Id = r.ClienteId ";

        public static List<BE_AsignacionPersonal_704ILR> GetByReserva_704ILR(int reservaId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return GetByReserva_704ILR(reservaId_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional: lee sobre la conexion y la transaccion que le
        // pasan, para que lo que se valida sea lo mismo que despues se escribe.
        public static List<BE_AsignacionPersonal_704ILR> GetByReserva_704ILR(int reservaId_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            var list_704ILR = new List<BE_AsignacionPersonal_704ILR>();
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE a.ReservaId = @r ORDER BY e.Apellido, e.Nombre, a.Id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            }
            return list_704ILR;
        }

        public static BE_AsignacionPersonal_704ILR GetById_704ILR(int id_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return GetById_704ILR(id_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        public static BE_AsignacionPersonal_704ILR GetById_704ILR(int id_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE a.Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        // La asignacion de un empleado en una reserva (hay a lo sumo una), o null.
        public static BE_AsignacionPersonal_704ILR GetDeEmpleadoEnReserva_704ILR(int reservaId_704ILR, int empleadoId_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE a.ReservaId = @r AND a.EmpleadoId = @e", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = empleadoId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        // Agenda de un empleado: sus asignaciones en eventos confirmados, por fecha del
        // evento; los turnos de eventos ya cerrados van al final. Una reserva que dejo
        // de estar confirmada no compromete a nadie y no se lista.
        public static List<BE_AsignacionPersonal_704ILR> GetDeEmpleado_704ILR(int empleadoId_704ILR)
        {
            var list_704ILR = new List<BE_AsignacionPersonal_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR +
                "WHERE a.EmpleadoId = @e AND r.Estado = @confirmada " +
                "ORDER BY CASE WHEN r.EstadoCoordinacion = @cerrado THEN 1 ELSE 0 END, r.FechaEvento, a.HoraInicio, a.Id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = empleadoId_704ILR;
                cmd_704ILR.Parameters.Add("@confirmada", SqlDbType.NVarChar, 20).Value = EstadoReserva_704ILR.CONFIRMADA.ToString();
                cmd_704ILR.Parameters.Add("@cerrado", SqlDbType.NVarChar, 20).Value = EstadoCoordinacion_704ILR.CERRADO.ToString();
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            }
            return list_704ILR;
        }

        // Asignaciones del empleado que pueden chocar con una franja: las de OTROS
        // eventos confirmados cuya fecha cae entre 'desde' y 'hasta' (una franja que
        // cruza la medianoche choca con la del dia vecino). Con soloConfirmadas se
        // traen las que el empleado ya acepto; sin ella, tambien las pendientes de
        // respuesta. Las rechazadas no comprometen al empleado.
        public static List<BE_AsignacionPersonal_704ILR> GetComprometidas_704ILR(int empleadoId_704ILR,
            DateTime desde_704ILR, DateTime hasta_704ILR, int excluirReservaId_704ILR, bool soloConfirmadas_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            var list_704ILR = new List<BE_AsignacionPersonal_704ILR>();
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR +
                "WHERE a.EmpleadoId = @e AND a.ReservaId <> @ex AND r.Estado = @confirmada " +
                "AND CAST(r.FechaEvento AS DATE) BETWEEN @d AND @h " +
                "AND (a.Estado = @aConfirmada OR (@solo = 0 AND a.Estado = @aPendiente)) " +
                "ORDER BY r.FechaEvento, a.HoraInicio, a.Id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = empleadoId_704ILR;
                cmd_704ILR.Parameters.Add("@ex", SqlDbType.Int).Value = excluirReservaId_704ILR;
                cmd_704ILR.Parameters.Add("@confirmada", SqlDbType.NVarChar, 20).Value = EstadoReserva_704ILR.CONFIRMADA.ToString();
                cmd_704ILR.Parameters.Add("@d", SqlDbType.Date).Value = desde_704ILR.Date;
                cmd_704ILR.Parameters.Add("@h", SqlDbType.Date).Value = hasta_704ILR.Date;
                cmd_704ILR.Parameters.Add("@aConfirmada", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.CONFIRMADA.ToString();
                cmd_704ILR.Parameters.Add("@aPendiente", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.PENDIENTE.ToString();
                cmd_704ILR.Parameters.Add("@solo", SqlDbType.Bit).Value = soloConfirmadas_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            }
            return list_704ILR;
        }

        public static int Insert_704ILR(BE_AsignacionPersonal_704ILR a_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "INSERT INTO dbo.AsignacionesPersonal (ReservaId, EmpleadoId, RolAsignado, HoraInicio, HoraFin, Estado) " +
                "OUTPUT INSERTED.Id VALUES (@r, @e, @rol, @hi, @hf, @estado)", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = a_704ILR.ReservaId_704ILR;
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = a_704ILR.EmpleadoId_704ILR;
                BindFranja_704ILR(cmd_704ILR, a_704ILR.RolAsignado_704ILR, a_704ILR.HoraInicio_704ILR, a_704ILR.HoraFin_704ILR);
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.PENDIENTE.ToString();
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        // Vuelve a ofrecerle el turno a un empleado que lo habia rechazado: la misma
        // fila queda PENDIENTE con el rol y la franja nuevos, sin respuesta ni motivo.
        public static void Reactivar_704ILR(int id_704ILR, string rol_704ILR, TimeSpan horaInicio_704ILR, TimeSpan horaFin_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "UPDATE dbo.AsignacionesPersonal SET RolAsignado = @rol, HoraInicio = @hi, HoraFin = @hf, Estado = @estado, " +
                "FechaConfirmacion = NULL, MotivoRechazo = NULL WHERE Id = @id", conn_704ILR, tx_704ILR))
            {
                BindFranja_704ILR(cmd_704ILR, rol_704ILR, horaInicio_704ILR, horaFin_704ILR);
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.PENDIENTE.ToString();
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Respuesta del empleado: acepta o rechaza (con motivo). Queda la fecha en
        // que respondio.
        public static void Responder_704ILR(int id_704ILR, EstadoAsignacion_704ILR estado_704ILR, string motivo_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "UPDATE dbo.AsignacionesPersonal SET Estado = @estado, FechaConfirmacion = GETDATE(), MotivoRechazo = @m WHERE Id = @id",
                conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = estado_704ILR.ToString();
                cmd_704ILR.Parameters.Add("@m", SqlDbType.NVarChar, 250).Value = string.IsNullOrWhiteSpace(motivo_704ILR) ? (object)DBNull.Value : motivo_704ILR.Trim();
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Devuelve las filas borradas: 0 si la asignacion ya no estaba.
        public static int Delete_704ILR(int id_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand("DELETE FROM dbo.AsignacionesPersonal WHERE Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                return cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Las confirmaciones dejan de valer (el evento cambio de fecha o la reserva
        // dejo de estar firme): las asignaciones confirmadas vuelven a PENDIENTE y el
        // personal tiene que responder de nuevo. Devuelve cuantas se reiniciaron.
        public static int ReiniciarConfirmaciones_704ILR(int reservaId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "UPDATE dbo.AsignacionesPersonal SET Estado = @pendiente, FechaConfirmacion = NULL " +
                "WHERE ReservaId = @r AND Estado = @confirmada", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@pendiente", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.PENDIENTE.ToString();
                cmd_704ILR.Parameters.Add("@confirmada", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.CONFIRMADA.ToString();
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                return cmd_704ILR.ExecuteNonQuery();
            }
        }

        private static void BindFranja_704ILR(SqlCommand cmd_704ILR, string rol_704ILR, TimeSpan horaInicio_704ILR, TimeSpan horaFin_704ILR)
        {
            cmd_704ILR.Parameters.Add("@rol", SqlDbType.NVarChar, 60).Value = rol_704ILR ?? string.Empty;
            cmd_704ILR.Parameters.Add("@hi", SqlDbType.Time).Value = horaInicio_704ILR;
            cmd_704ILR.Parameters.Add("@hf", SqlDbType.Time).Value = horaFin_704ILR;
        }

        // Lectura del estado almacenado. La columna tiene dominio cerrado (CHECK); un
        // texto ajeno se lee como PENDIENTE, que es el estado que no habilita nada.
        private static EstadoAsignacion_704ILR EstadoDesdeTexto_704ILR(string texto_704ILR)
            => Enum.TryParse((texto_704ILR ?? string.Empty).Trim(), true, out EstadoAsignacion_704ILR estado_704ILR)
               && Enum.IsDefined(typeof(EstadoAsignacion_704ILR), estado_704ILR)
                ? estado_704ILR
                : EstadoAsignacion_704ILR.PENDIENTE;

        private static BE_AsignacionPersonal_704ILR Map_704ILR(SqlDataReader r_704ILR) => new BE_AsignacionPersonal_704ILR
        {
            Id_704ILR = r_704ILR.GetInt32(0),
            ReservaId_704ILR = r_704ILR.GetInt32(1),
            EmpleadoId_704ILR = r_704ILR.GetInt32(2),
            EmpleadoNombre_704ILR = r_704ILR.GetString(3),
            EspecialidadNombre_704ILR = r_704ILR.GetString(4),
            RolAsignado_704ILR = r_704ILR.GetString(5),
            HoraInicio_704ILR = r_704ILR.GetTimeSpan(6),
            HoraFin_704ILR = r_704ILR.GetTimeSpan(7),
            Estado_704ILR = EstadoDesdeTexto_704ILR(r_704ILR.GetString(8)),
            FechaConfirmacion_704ILR = r_704ILR.IsDBNull(9) ? (DateTime?)null : r_704ILR.GetDateTime(9),
            MotivoRechazo_704ILR = r_704ILR.IsDBNull(10) ? null : r_704ILR.GetString(10),
            CreatedAt_704ILR = r_704ILR.GetDateTime(11),
            FechaEvento_704ILR = r_704ILR.GetDateTime(12),
            SalonNombre_704ILR = r_704ILR.GetString(13),
            ClienteNombre_704ILR = r_704ILR.IsDBNull(14) ? string.Empty : r_704ILR.GetString(14),
            EstadoCoordinacion_704ILR = DAL_Coordinacion_704ILR.EstadoDesdeTexto_704ILR(r_704ILR.IsDBNull(15) ? null : r_704ILR.GetString(15))
        };
    }
}
