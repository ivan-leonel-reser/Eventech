using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Incidencias registradas durante la ejecucion del evento (Proceso 2: CUN011).
    public static class DAL_Incidencia_704ILR
    {
        private const string SelectBase_704ILR =
            "SELECT i.Id, i.ReservaId, i.FechaHora, i.Tipo, i.Descripcion, i.EmpleadoReportaId, " +
            "CASE WHEN e.Id IS NULL THEN NULL ELSE e.Apellido + ', ' + e.Nombre END, i.Estado, i.Resolucion, i.FechaResolucion " +
            "FROM dbo.Incidencias i LEFT JOIN dbo.Empleados e ON e.Id = i.EmpleadoReportaId ";

        public static List<BE_Incidencia_704ILR> GetByReserva_704ILR(int reservaId_704ILR)
        {
            var list_704ILR = new List<BE_Incidencia_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE i.ReservaId = @r ORDER BY i.FechaHora, i.Id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            }
            return list_704ILR;
        }

        public static BE_Incidencia_704ILR GetById_704ILR(int id_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return GetById_704ILR(id_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional (ver DAL_AsignacionPersonal_704ILR.GetByReserva_704ILR).
        public static BE_Incidencia_704ILR GetById_704ILR(int id_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE i.Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        public static int Insert_704ILR(BE_Incidencia_704ILR i_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "INSERT INTO dbo.Incidencias (ReservaId, FechaHora, Tipo, Descripcion, EmpleadoReportaId, Estado) " +
                "OUTPUT INSERTED.Id VALUES (@r, GETDATE(), @t, @d, @e, @estado)", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = i_704ILR.ReservaId_704ILR;
                cmd_704ILR.Parameters.Add("@t", SqlDbType.NVarChar, 20).Value = i_704ILR.Tipo_704ILR.ToString();
                cmd_704ILR.Parameters.Add("@d", SqlDbType.NVarChar, 500).Value = i_704ILR.Descripcion_704ILR ?? string.Empty;
                cmd_704ILR.Parameters.Add("@e", SqlDbType.Int).Value = i_704ILR.EmpleadoReportaId_704ILR.HasValue ? (object)i_704ILR.EmpleadoReportaId_704ILR.Value : DBNull.Value;
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 10).Value = EstadoIncidencia_704ILR.ABIERTA.ToString();
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        public static void Resolver_704ILR(int id_704ILR, string resolucion_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "UPDATE dbo.Incidencias SET Estado = @estado, Resolucion = @res, FechaResolucion = GETDATE() WHERE Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 10).Value = EstadoIncidencia_704ILR.RESUELTA.ToString();
                cmd_704ILR.Parameters.Add("@res", SqlDbType.NVarChar, 250).Value = resolucion_704ILR ?? string.Empty;
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        public static int Abiertas_704ILR(int reservaId_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand("SELECT COUNT(*) FROM dbo.Incidencias WHERE ReservaId = @r AND Estado = @estado", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 10).Value = EstadoIncidencia_704ILR.ABIERTA.ToString();
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        // Lectura de los valores almacenados. Las dos columnas tienen dominio cerrado
        // (CHECK); un texto ajeno se lee como OTRO y como ABIERTA respectivamente.
        private static TipoIncidencia_704ILR TipoDesdeTexto_704ILR(string texto_704ILR)
            => Enum.TryParse((texto_704ILR ?? string.Empty).Trim(), true, out TipoIncidencia_704ILR tipo_704ILR)
               && Enum.IsDefined(typeof(TipoIncidencia_704ILR), tipo_704ILR)
                ? tipo_704ILR
                : TipoIncidencia_704ILR.OTRO;

        private static EstadoIncidencia_704ILR EstadoDesdeTexto_704ILR(string texto_704ILR)
            => Enum.TryParse((texto_704ILR ?? string.Empty).Trim(), true, out EstadoIncidencia_704ILR estado_704ILR)
               && Enum.IsDefined(typeof(EstadoIncidencia_704ILR), estado_704ILR)
                ? estado_704ILR
                : EstadoIncidencia_704ILR.ABIERTA;

        private static BE_Incidencia_704ILR Map_704ILR(SqlDataReader r_704ILR) => new BE_Incidencia_704ILR
        {
            Id_704ILR = r_704ILR.GetInt32(0),
            ReservaId_704ILR = r_704ILR.GetInt32(1),
            FechaHora_704ILR = r_704ILR.GetDateTime(2),
            Tipo_704ILR = TipoDesdeTexto_704ILR(r_704ILR.GetString(3)),
            Descripcion_704ILR = r_704ILR.GetString(4),
            EmpleadoReportaId_704ILR = r_704ILR.IsDBNull(5) ? (int?)null : r_704ILR.GetInt32(5),
            EmpleadoReportaNombre_704ILR = r_704ILR.IsDBNull(6) ? null : r_704ILR.GetString(6),
            Estado_704ILR = EstadoDesdeTexto_704ILR(r_704ILR.GetString(7)),
            Resolucion_704ILR = r_704ILR.IsDBNull(8) ? null : r_704ILR.GetString(8),
            FechaResolucion_704ILR = r_704ILR.IsDBNull(9) ? (DateTime?)null : r_704ILR.GetDateTime(9)
        };
    }
}
