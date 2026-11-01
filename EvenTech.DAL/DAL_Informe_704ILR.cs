using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Lecturas del informe de gestion (A02). Solo trae las filas: totales, porcentajes
    // y agrupaciones los calcula la capa de negocio.
    public static class DAL_Informe_704ILR
    {
        // Reservas cuyo evento cae entre las dos fechas (inclusive), con el cliente, el
        // salon y lo cobrado (la anulacion de un pago lo elimina, asi que la suma de los
        // pagos que quedan es lo cobrado).
        public static List<BE_InformeFila_704ILR> ReservasDelPeriodo_704ILR(DateTime desde_704ILR, DateTime hasta_704ILR)
        {
            var lista_704ILR = new List<BE_InformeFila_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT r.Id, r.FechaEvento, r.Estado, r.EstadoCoordinacion, r.Monto, r.VenceEl, r.CantidadInvitados, " +
                "       LTRIM(RTRIM(c.Nombre + ISNULL(N' ' + NULLIF(c.Apellido, N''), N''))), s.Id, s.Nombre, " +
                "       ISNULL((SELECT SUM(p.Monto) FROM dbo.Pagos p WHERE p.ReservaId = r.Id), 0) " +
                "FROM dbo.Reservas r " +
                "JOIN dbo.Clientes c ON c.Id = r.ClienteId " +
                "JOIN dbo.Salones s ON s.Id = r.SalonId " +
                "WHERE r.FechaEvento >= @desde AND r.FechaEvento < @hasta " +
                "ORDER BY r.FechaEvento, r.Id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@desde", SqlDbType.DateTime).Value = desde_704ILR.Date;
                cmd_704ILR.Parameters.Add("@hasta", SqlDbType.DateTime).Value = hasta_704ILR.Date.AddDays(1);
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    while (r_704ILR.Read())
                    {
                        lista_704ILR.Add(new BE_InformeFila_704ILR
                        {
                            ReservaId_704ILR = r_704ILR.GetInt32(0),
                            FechaEvento_704ILR = r_704ILR.GetDateTime(1),
                            Estado_704ILR = Enum.Parse<EstadoReserva_704ILR>(r_704ILR.GetString(2)),
                            EstadoCoordinacion_704ILR = Enum.Parse<EstadoCoordinacion_704ILR>(r_704ILR.GetString(3)),
                            Monto_704ILR = r_704ILR.GetDecimal(4),
                            VenceEl_704ILR = r_704ILR.IsDBNull(5) ? (DateTime?)null : r_704ILR.GetDateTime(5),
                            Invitados_704ILR = r_704ILR.GetInt32(6),
                            ClienteNombre_704ILR = r_704ILR.GetString(7),
                            SalonId_704ILR = r_704ILR.GetInt32(8),
                            SalonNombre_704ILR = r_704ILR.GetString(9),
                            Cobrado_704ILR = r_704ILR.GetDecimal(10)
                        });
                    }
                }
            }
            return lista_704ILR;
        }
    }
}
