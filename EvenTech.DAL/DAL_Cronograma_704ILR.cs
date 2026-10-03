using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Cronograma del evento y sus actividades (Proceso 2: CUN008).
    public static class DAL_Cronograma_704ILR
    {
        public static BE_Cronograma_704ILR GetByReserva_704ILR(int reservaId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return GetByReserva_704ILR(reservaId_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional: el cronograma de la reserva con sus actividades
        // en el orden que les dio el coordinador, o null si todavia no se genero.
        public static BE_Cronograma_704ILR GetByReserva_704ILR(int reservaId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            BE_Cronograma_704ILR cronograma_704ILR = null;
            using (var cmd_704ILR = new SqlCommand("SELECT Id, ReservaId, CreatedAt FROM dbo.Cronogramas WHERE ReservaId = @r", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    if (r_704ILR.Read())
                        cronograma_704ILR = new BE_Cronograma_704ILR
                        {
                            Id_704ILR = r_704ILR.GetInt32(0),
                            ReservaId_704ILR = r_704ILR.GetInt32(1),
                            CreatedAt_704ILR = r_704ILR.GetDateTime(2)
                        };
                }
            }
            if (cronograma_704ILR == null) return null;

            using (var cmd_704ILR = new SqlCommand(
                "SELECT ca.Id, ca.CronogramaId, ca.Orden, ca.Hora, ca.Descripcion, ca.ResponsableId, e.Apellido + ', ' + e.Nombre, ca.DuracionMinutos " +
                "FROM dbo.CronogramaActividades ca INNER JOIN dbo.Empleados e ON e.Id = ca.ResponsableId " +
                "WHERE ca.CronogramaId = @c ORDER BY ca.Orden, ca.Id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@c", SqlDbType.Int).Value = cronograma_704ILR.Id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read())
                        cronograma_704ILR.Actividades_704ILR.Add(new BE_CronogramaActividad_704ILR
                        {
                            Id_704ILR = r_704ILR.GetInt32(0),
                            CronogramaId_704ILR = r_704ILR.GetInt32(1),
                            Orden_704ILR = r_704ILR.GetInt32(2),
                            Hora_704ILR = r_704ILR.GetTimeSpan(3),
                            Descripcion_704ILR = r_704ILR.GetString(4),
                            ResponsableId_704ILR = r_704ILR.GetInt32(5),
                            ResponsableNombre_704ILR = r_704ILR.GetString(6),
                            DuracionMinutos_704ILR = r_704ILR.GetInt32(7)
                        });
            }
            return cronograma_704ILR;
        }

        public static int Insert_704ILR(int reservaId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand("INSERT INTO dbo.Cronogramas (ReservaId) OUTPUT INSERTED.Id VALUES (@r)", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        // Reemplaza las actividades del cronograma por la lista recibida, numerandolas
        // en el orden en que llegan. Las tareas cuelgan del cronograma y no de una
        // actividad, asi que rehacer las actividades no las afecta.
        public static void ReplaceActividades_704ILR(int cronogramaId_704ILR, IList<BE_CronogramaActividad_704ILR> actividades_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var del_704ILR = new SqlCommand("DELETE FROM dbo.CronogramaActividades WHERE CronogramaId = @c", conn_704ILR, tx_704ILR))
            {
                del_704ILR.Parameters.Add("@c", SqlDbType.Int).Value = cronogramaId_704ILR;
                del_704ILR.ExecuteNonQuery();
            }
            for (int i_704ILR = 0; i_704ILR < actividades_704ILR.Count; i_704ILR++)
            {
                var a_704ILR = actividades_704ILR[i_704ILR];
                using (var ins_704ILR = new SqlCommand(
                    "INSERT INTO dbo.CronogramaActividades (CronogramaId, Orden, Hora, Descripcion, ResponsableId, DuracionMinutos) " +
                    "VALUES (@c, @o, @h, @d, @e, @m)", conn_704ILR, tx_704ILR))
                {
                    ins_704ILR.Parameters.Add("@c", SqlDbType.Int).Value = cronogramaId_704ILR;
                    ins_704ILR.Parameters.Add("@o", SqlDbType.Int).Value = i_704ILR + 1;
                    ins_704ILR.Parameters.Add("@h", SqlDbType.Time).Value = a_704ILR.Hora_704ILR;
                    ins_704ILR.Parameters.Add("@d", SqlDbType.NVarChar, 150).Value = a_704ILR.Descripcion_704ILR ?? string.Empty;
                    ins_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = a_704ILR.ResponsableId_704ILR;
                    ins_704ILR.Parameters.Add("@m", SqlDbType.Int).Value = a_704ILR.DuracionMinutos_704ILR;
                    ins_704ILR.ExecuteNonQuery();
                }
            }
        }

        // Elimina el cronograma con sus actividades. Quien la llama ya verifico que
        // no tenga tareas asignadas (la clave foranea de Tareas lo impediria).
        public static void Delete_704ILR(int cronogramaId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "DELETE FROM dbo.CronogramaActividades WHERE CronogramaId = @c; DELETE FROM dbo.Cronogramas WHERE Id = @c;", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@c", SqlDbType.Int).Value = cronogramaId_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Actividades del cronograma de la reserva que tienen a ese empleado como
        // responsable (para no quitar del equipo a quien tiene un tramo a cargo).
        public static int ActividadesACargo_704ILR(int reservaId_704ILR, int empleadoId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.CronogramaActividades ca INNER JOIN dbo.Cronogramas cr ON cr.Id = ca.CronogramaId " +
                "WHERE cr.ReservaId = @r AND ca.ResponsableId = @e", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = empleadoId_704ILR;
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }
    }
}
