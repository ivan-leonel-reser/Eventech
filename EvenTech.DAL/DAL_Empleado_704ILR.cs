using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Personal de la organizacion y su catalogo de especialidades (Proceso 2).
    public static class DAL_Empleado_704ILR
    {
        // SELECT base con JOIN a la especialidad y a la cuenta vinculada para
        // proyectar sus nombres.
        private const string SelectBase_704ILR =
            "SELECT e.Id, e.Nombre, e.Apellido, e.Dni, e.EspecialidadId, es.Nombre, e.UserId, u.Username, e.Activo, e.CreatedAt " +
            "FROM dbo.Empleados e " +
            "INNER JOIN dbo.Especialidades es ON es.Id = e.EspecialidadId " +
            "LEFT JOIN dbo.Users u ON u.Id = e.UserId ";

        public static List<BE_Especialidad_704ILR> GetEspecialidades_704ILR()
        {
            var list_704ILR = new List<BE_Especialidad_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand("SELECT Id, Nombre FROM dbo.Especialidades ORDER BY Nombre", cn_704ILR.OpenConnection_704ILR()))
            using (var r_704ILR = cmd_704ILR.ExecuteReader())
                while (r_704ILR.Read())
                    list_704ILR.Add(new BE_Especialidad_704ILR { Id_704ILR = r_704ILR.GetInt32(0), Nombre_704ILR = r_704ILR.GetString(1) });
            return list_704ILR;
        }

        public static bool ExisteEspecialidad_704ILR(int id_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand("SELECT COUNT(1) FROM dbo.Especialidades WHERE Id = @id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                return (int)cmd_704ILR.ExecuteScalar() > 0;
            }
        }

        public static List<BE_Empleado_704ILR> GetAll_704ILR()
        {
            var list_704ILR = new List<BE_Empleado_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "ORDER BY e.Apellido, e.Nombre, e.Id", cn_704ILR.OpenConnection_704ILR()))
            using (var r_704ILR = cmd_704ILR.ExecuteReader())
                while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            return list_704ILR;
        }

        // Solo los activos (para ofrecerlos al asignar personal a un evento).
        public static List<BE_Empleado_704ILR> GetActivos_704ILR()
        {
            var list_704ILR = new List<BE_Empleado_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE e.Activo = 1 ORDER BY e.Apellido, e.Nombre, e.Id", cn_704ILR.OpenConnection_704ILR()))
            using (var r_704ILR = cmd_704ILR.ExecuteReader())
                while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            return list_704ILR;
        }

        public static BE_Empleado_704ILR GetById_704ILR(int id_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE e.Id = @id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        // Sobrecarga transaccional: lee la ficha con bloqueo de actualizacion sobre la
        // fila (UPDLOCK, HOLDLOCK). Quien la use serializa a las demas operaciones que
        // lean al mismo empleado por esta via: dos asignaciones simultaneas del mismo
        // empleado a eventos distintos validan la superposicion una detras de la otra.
        public static BE_Empleado_704ILR GetById_704ILR(int id_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(SelectBaseBloqueado_704ILR + "WHERE e.Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        private static readonly string SelectBaseBloqueado_704ILR =
            SelectBase_704ILR.Replace("FROM dbo.Empleados e ", "FROM dbo.Empleados e WITH (UPDLOCK, HOLDLOCK) ");

        // Empleado vinculado a una cuenta (null si la cuenta no representa a nadie).
        public static BE_Empleado_704ILR GetByUserId_704ILR(int userId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE e.UserId = @u", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@u", SqlDbType.Int).Value = userId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        public static bool ExistsDni_704ILR(string dni_704ILR, int excluirId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand("SELECT COUNT(1) FROM dbo.Empleados WHERE Dni = @d AND Id <> @id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@d", SqlDbType.NVarChar, 20).Value = dni_704ILR ?? string.Empty;
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = excluirId_704ILR;
                return (int)cmd_704ILR.ExecuteScalar() > 0;
            }
        }

        // True si la cuenta ya representa a otro empleado.
        public static bool CuentaVinculada_704ILR(int userId_704ILR, int excluirId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand("SELECT COUNT(1) FROM dbo.Empleados WHERE UserId = @u AND Id <> @id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@u", SqlDbType.Int).Value = userId_704ILR;
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = excluirId_704ILR;
                return (int)cmd_704ILR.ExecuteScalar() > 0;
            }
        }

        // Asignaciones del empleado que todavia lo comprometen: pendientes o
        // confirmadas, en eventos confirmados que no se cerraron y que estan en
        // ejecucion o cuya fecha no paso. Se cuenta desde ayer: un turno puede cruzar
        // la medianoche y seguir en curso al dia siguiente de la fecha del evento.
        // Mientras tenga alguna no se lo puede dar de baja. Se lee sobre la
        // transaccion de la baja, con la ficha del empleado bloqueada.
        public static int AsignacionesVigentes_704ILR(int empleadoId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.AsignacionesPersonal a INNER JOIN dbo.Reservas r ON r.Id = a.ReservaId " +
                "WHERE a.EmpleadoId = @e AND a.Estado <> @rechazada AND r.Estado = @confirmada " +
                "AND r.EstadoCoordinacion <> @cerrado " +
                "AND (r.EstadoCoordinacion = @enEjecucion OR CAST(r.FechaEvento AS DATE) >= @desde)", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = empleadoId_704ILR;
                cmd_704ILR.Parameters.Add("@rechazada", SqlDbType.NVarChar, 20).Value = EstadoAsignacion_704ILR.RECHAZADA.ToString();
                cmd_704ILR.Parameters.Add("@confirmada", SqlDbType.NVarChar, 20).Value = EstadoReserva_704ILR.CONFIRMADA.ToString();
                cmd_704ILR.Parameters.Add("@cerrado", SqlDbType.NVarChar, 20).Value = EstadoCoordinacion_704ILR.CERRADO.ToString();
                cmd_704ILR.Parameters.Add("@enEjecucion", SqlDbType.NVarChar, 20).Value = EstadoCoordinacion_704ILR.EN_EJECUCION.ToString();
                cmd_704ILR.Parameters.Add("@desde", SqlDbType.Date).Value = DateTime.Today.AddDays(-1);
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        public static int Insert_704ILR(BE_Empleado_704ILR e_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "INSERT INTO dbo.Empleados (Nombre, Apellido, Dni, EspecialidadId, UserId, Activo) " +
                "OUTPUT INSERTED.Id VALUES (@n, @a, @d, @es, @u, @ac)", cn_704ILR.OpenConnection_704ILR()))
            {
                Bind_704ILR(cmd_704ILR, e_704ILR);
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        public static void Update_704ILR(BE_Empleado_704ILR e_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "UPDATE dbo.Empleados SET Nombre=@n, Apellido=@a, Dni=@d, EspecialidadId=@es, UserId=@u, Activo=@ac WHERE Id=@id",
                conn_704ILR, tx_704ILR))
            {
                Bind_704ILR(cmd_704ILR, e_704ILR);
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = e_704ILR.Id_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        private static void Bind_704ILR(SqlCommand cmd_704ILR, BE_Empleado_704ILR e_704ILR)
        {
            cmd_704ILR.Parameters.Add("@n", SqlDbType.NVarChar, 60).Value = e_704ILR.Nombre_704ILR ?? string.Empty;
            cmd_704ILR.Parameters.Add("@a", SqlDbType.NVarChar, 60).Value = e_704ILR.Apellido_704ILR ?? string.Empty;
            cmd_704ILR.Parameters.Add("@d", SqlDbType.NVarChar, 20).Value = e_704ILR.Dni_704ILR ?? string.Empty;
            cmd_704ILR.Parameters.Add("@es", SqlDbType.Int).Value = e_704ILR.EspecialidadId_704ILR;
            cmd_704ILR.Parameters.Add("@u", SqlDbType.Int).Value = e_704ILR.UserId_704ILR.HasValue ? (object)e_704ILR.UserId_704ILR.Value : DBNull.Value;
            cmd_704ILR.Parameters.Add("@ac", SqlDbType.Bit).Value = e_704ILR.Activo_704ILR;
        }

        private static BE_Empleado_704ILR Map_704ILR(SqlDataReader r_704ILR) => new BE_Empleado_704ILR
        {
            Id_704ILR = r_704ILR.GetInt32(0),
            Nombre_704ILR = r_704ILR.GetString(1),
            Apellido_704ILR = r_704ILR.GetString(2),
            Dni_704ILR = r_704ILR.GetString(3),
            EspecialidadId_704ILR = r_704ILR.GetInt32(4),
            EspecialidadNombre_704ILR = r_704ILR.GetString(5),
            UserId_704ILR = r_704ILR.IsDBNull(6) ? (int?)null : r_704ILR.GetInt32(6),
            Username_704ILR = r_704ILR.IsDBNull(7) ? null : r_704ILR.GetString(7),
            Activo_704ILR = r_704ILR.GetBoolean(8),
            CreatedAt_704ILR = r_704ILR.GetDateTime(9)
        };
    }
}
