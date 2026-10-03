using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Tareas especificas del equipo dentro del cronograma (Proceso 2: CUN009 y CUN010).
    public static class DAL_Tarea_704ILR
    {
        // SELECT base con JOIN al empleado y al cronograma para proyectar el nombre
        // y la reserva a la que pertenece la tarea.
        private const string SelectBase_704ILR =
            "SELECT t.Id, t.CronogramaId, cr.ReservaId, t.EmpleadoId, e.Apellido + ', ' + e.Nombre, t.Descripcion, " +
            "t.HoraInicio, t.HoraFin, t.Prioridad, t.Recursos, t.CreatedAt " +
            "FROM dbo.Tareas t " +
            "INNER JOIN dbo.Empleados e ON e.Id = t.EmpleadoId " +
            "INNER JOIN dbo.Cronogramas cr ON cr.Id = t.CronogramaId ";

        public static List<BE_Tarea_704ILR> GetByReserva_704ILR(int reservaId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return GetByReserva_704ILR(reservaId_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional (ver DAL_AsignacionPersonal_704ILR.GetByReserva_704ILR).
        public static List<BE_Tarea_704ILR> GetByReserva_704ILR(int reservaId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            var list_704ILR = new List<BE_Tarea_704ILR>();
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE cr.ReservaId = @r ORDER BY t.Id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            }
            return list_704ILR;
        }

        public static BE_Tarea_704ILR GetById_704ILR(int id_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return GetById_704ILR(id_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        public static BE_Tarea_704ILR GetById_704ILR(int id_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE t.Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        public static int Insert_704ILR(BE_Tarea_704ILR t_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "INSERT INTO dbo.Tareas (CronogramaId, EmpleadoId, Descripcion, HoraInicio, HoraFin, Prioridad, Recursos) " +
                "OUTPUT INSERTED.Id VALUES (@c, @e, @d, @hi, @hf, @p, @rec)", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@c", SqlDbType.Int).Value = t_704ILR.CronogramaId_704ILR;
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = t_704ILR.EmpleadoId_704ILR;
                cmd_704ILR.Parameters.Add("@d", SqlDbType.NVarChar, 200).Value = t_704ILR.Descripcion_704ILR ?? string.Empty;
                cmd_704ILR.Parameters.Add("@hi", SqlDbType.Time).Value = t_704ILR.HoraInicio_704ILR;
                cmd_704ILR.Parameters.Add("@hf", SqlDbType.Time).Value = t_704ILR.HoraFin_704ILR;
                cmd_704ILR.Parameters.Add("@p", SqlDbType.NVarChar, 10).Value = t_704ILR.Prioridad_704ILR.ToString();
                cmd_704ILR.Parameters.Add("@rec", SqlDbType.NVarChar, 200).Value = string.IsNullOrWhiteSpace(t_704ILR.Recursos_704ILR) ? (object)DBNull.Value : t_704ILR.Recursos_704ILR.Trim();
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        // Devuelve las filas borradas: 0 si la tarea ya no estaba.
        public static int Delete_704ILR(int id_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand("DELETE FROM dbo.Tareas WHERE Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                return cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Tareas del cronograma de la reserva; con empleadoId mayor que cero, solo las
        // de ese empleado.
        public static int Contar_704ILR(int reservaId_704ILR, int empleadoId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.Tareas t INNER JOIN dbo.Cronogramas cr ON cr.Id = t.CronogramaId " +
                "WHERE cr.ReservaId = @r AND (@e = 0 OR t.EmpleadoId = @e)", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = empleadoId_704ILR;
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        // Lectura de la prioridad almacenada. La columna tiene dominio cerrado
        // (CHECK); un texto ajeno se lee como MEDIA, el valor por defecto.
        private static PrioridadTarea_704ILR PrioridadDesdeTexto_704ILR(string texto_704ILR)
            => Enum.TryParse((texto_704ILR ?? string.Empty).Trim(), true, out PrioridadTarea_704ILR prioridad_704ILR)
               && Enum.IsDefined(typeof(PrioridadTarea_704ILR), prioridad_704ILR)
                ? prioridad_704ILR
                : PrioridadTarea_704ILR.MEDIA;

        private static BE_Tarea_704ILR Map_704ILR(SqlDataReader r_704ILR) => new BE_Tarea_704ILR
        {
            Id_704ILR = r_704ILR.GetInt32(0),
            CronogramaId_704ILR = r_704ILR.GetInt32(1),
            ReservaId_704ILR = r_704ILR.GetInt32(2),
            EmpleadoId_704ILR = r_704ILR.GetInt32(3),
            EmpleadoNombre_704ILR = r_704ILR.GetString(4),
            Descripcion_704ILR = r_704ILR.GetString(5),
            HoraInicio_704ILR = r_704ILR.GetTimeSpan(6),
            HoraFin_704ILR = r_704ILR.GetTimeSpan(7),
            Prioridad_704ILR = PrioridadDesdeTexto_704ILR(r_704ILR.GetString(8)),
            Recursos_704ILR = r_704ILR.IsDBNull(9) ? null : r_704ILR.GetString(9),
            CreatedAt_704ILR = r_704ILR.GetDateTime(10)
        };
    }
}
