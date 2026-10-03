using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Persistencia de los mementos de reserva (patron Memento). Cada version es
    // una cabecera (ReservaMemento) + sus lineas de servicios congeladas
    // (ReservaMementoServicio), guardadas en la misma transaccion.
    public static class DAL_ReservaMemento_704ILR
    {
        // Cabecera + nombres actuales de cliente/salon proyectados para mostrar.
        private const string SelectBase_704ILR =
            "SELECT m.Id, m.ReservaId, m.ClienteId, m.SalonId, m.FechaEvento, m.Estado, m.Monto, " +
            "m.CantidadInvitados, m.Usuario, m.Fecha, " +
            "LTRIM(ISNULL(c.Nombre,'') + ISNULL(' ' + c.Apellido,'')) AS ClienteNombre, s.Nombre AS SalonNombre " +
            "FROM dbo.ReservaMemento m " +
            "LEFT JOIN dbo.Clientes c ON c.Id = m.ClienteId " +
            "LEFT JOIN dbo.Salones s ON s.Id = m.SalonId ";

        // Escribe la version sobre la conexion y la transaccion de la operacion que
        // la motiva: es el unico camino de alta, para que la version previa y la
        // modificacion entren o no entren juntas. Si la escritura de la reserva
        // falla, tampoco queda una version de un cambio que nunca ocurrio.
        public static int Insert_704ILR(BE_ReservaMemento_704ILR m_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            int id_704ILR;
            using (var cmd_704ILR = new SqlCommand(
                "INSERT INTO dbo.ReservaMemento (ReservaId, ClienteId, SalonId, FechaEvento, Estado, Monto, " +
                "CantidadInvitados, Usuario, Fecha) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@reserva, @cliente, @salon, @fechaEvento, @estado, @monto, @invitados, @usuario, @fecha)",
                conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@reserva", SqlDbType.Int).Value = m_704ILR.ReservaId_704ILR;
                cmd_704ILR.Parameters.Add("@cliente", SqlDbType.Int).Value = m_704ILR.ClienteId_704ILR;
                cmd_704ILR.Parameters.Add("@salon", SqlDbType.Int).Value = m_704ILR.SalonId_704ILR;
                cmd_704ILR.Parameters.Add("@fechaEvento", SqlDbType.DateTime).Value = m_704ILR.FechaEvento_704ILR;
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = m_704ILR.Estado_704ILR.ToString();
                cmd_704ILR.Parameters.Add("@monto", SqlDbType.Decimal).Value = m_704ILR.Monto_704ILR;
                cmd_704ILR.Parameters.Add("@invitados", SqlDbType.Int).Value = m_704ILR.CantidadInvitados_704ILR;
                cmd_704ILR.Parameters.Add("@usuario", SqlDbType.NVarChar, 50).Value = m_704ILR.Usuario_704ILR ?? "Sistema";
                cmd_704ILR.Parameters.Add("@fecha", SqlDbType.DateTime).Value = m_704ILR.Fecha_704ILR;
                id_704ILR = (int)cmd_704ILR.ExecuteScalar();
            }

            foreach (var sv_704ILR in m_704ILR.Servicios_704ILR)
            {
                using (var ins_704ILR = new SqlCommand(
                    "INSERT INTO dbo.ReservaMementoServicio (MementoId, ServicioId, Cantidad, PrecioUnitario) " +
                    "VALUES (@m, @s, @c, @p)", conn_704ILR, tx_704ILR))
                {
                    ins_704ILR.Parameters.Add("@m", SqlDbType.Int).Value = id_704ILR;
                    ins_704ILR.Parameters.Add("@s", SqlDbType.Int).Value = sv_704ILR.ServicioId_704ILR;
                    ins_704ILR.Parameters.Add("@c", SqlDbType.Int).Value = sv_704ILR.Cantidad_704ILR;
                    ins_704ILR.Parameters.Add("@p", SqlDbType.Decimal).Value = sv_704ILR.PrecioUnitario_704ILR;
                    ins_704ILR.ExecuteNonQuery();
                }
            }
            return id_704ILR;
        }

        // Listado de versiones de una reserva, de la mas reciente a la mas vieja.
        // No carga las lineas de servicios (solo hacen falta al restaurar).
        public static List<BE_ReservaMemento_704ILR> GetByReserva_704ILR(int reservaId_704ILR)
        {
            var list_704ILR = new List<BE_ReservaMemento_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE m.ReservaId = @r ORDER BY m.Id DESC", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR, null));
            }
            return list_704ILR;
        }

        // Version puntual con sus lineas de servicios (lo que necesita la restauracion).
        public static BE_ReservaMemento_704ILR GetById_704ILR(int id_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                var conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                BE_ReservaMemento_704ILR header_704ILR = null;
                using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE m.Id = @id", conn_704ILR))
                {
                    cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                    using (var r_704ILR = cmd_704ILR.ExecuteReader())
                        if (r_704ILR.Read()) header_704ILR = Map_704ILR(r_704ILR, null);
                }
                if (header_704ILR == null) return null;

                var servicios_704ILR = new List<BE_ReservaServicio_704ILR>();
                using (var cmd_704ILR = new SqlCommand(
                    "SELECT ms.ServicioId, s.Nombre, ms.Cantidad, ms.PrecioUnitario " +
                    "FROM dbo.ReservaMementoServicio ms " +
                    "INNER JOIN dbo.Servicios s ON s.Id = ms.ServicioId " +
                    "WHERE ms.MementoId = @m ORDER BY s.Nombre", conn_704ILR))
                {
                    cmd_704ILR.Parameters.Add("@m", SqlDbType.Int).Value = id_704ILR;
                    using (var r_704ILR = cmd_704ILR.ExecuteReader())
                        while (r_704ILR.Read())
                            servicios_704ILR.Add(new BE_ReservaServicio_704ILR
                            {
                                ReservaId_704ILR = header_704ILR.ReservaId_704ILR,
                                ServicioId_704ILR = r_704ILR.GetInt32(0),
                                ServicioNombre_704ILR = r_704ILR.GetString(1),
                                Cantidad_704ILR = r_704ILR.GetInt32(2),
                                PrecioUnitario_704ILR = r_704ILR.GetDecimal(3)
                            });
                }

                // Se rearma el memento con las lineas (es inmutable: no hay setter).
                return new BE_ReservaMemento_704ILR(header_704ILR.Id_704ILR, header_704ILR.ReservaId_704ILR, header_704ILR.ClienteId_704ILR, header_704ILR.SalonId_704ILR,
                    header_704ILR.FechaEvento_704ILR, header_704ILR.Estado_704ILR, header_704ILR.Monto_704ILR,
                    header_704ILR.CantidadInvitados_704ILR, header_704ILR.Usuario_704ILR, header_704ILR.Fecha_704ILR,
                    header_704ILR.ClienteNombre_704ILR, header_704ILR.SalonNombre_704ILR, servicios_704ILR);
            }
        }

        // Estado de una version tal cual esta almacenado cuando no es exactamente uno de los
        // nombres del ciclo de vida, o null si lo es (o si la version no existe). Mismo
        // criterio que DAL_Reserva_704ILR.EstadoFueraDeDominio_704ILR para la reserva. La
        // lectura tolerante (Map_704ILR) entrega un texto que no es ningun estado como un
        // valor fuera del enum y descarta el texto: la capa de negocio lo relee con esta
        // consulta, sobre su conexion y su transaccion, para dejar asentado el valor real
        // cuando rechaza restaurar esa version.
        public static string EstadoFueraDeDominio_704ILR(int id_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(string.Empty, conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.CommandText = "SELECT Estado FROM dbo.ReservaMemento WHERE Id = @id AND " +
                    DAL_Reserva_704ILR.FiltroEstadoFueraDeDominio_704ILR(cmd_704ILR);
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                object valor_704ILR = cmd_704ILR.ExecuteScalar();
                return valor_704ILR == null || valor_704ILR == DBNull.Value ? null : (string)valor_704ILR;
            }
        }

        private static BE_ReservaMemento_704ILR Map_704ILR(SqlDataReader r_704ILR, List<BE_ReservaServicio_704ILR> servicios_704ILR) =>
            new BE_ReservaMemento_704ILR(
                r_704ILR.GetInt32(0),
                r_704ILR.GetInt32(1),
                r_704ILR.GetInt32(2),
                r_704ILR.GetInt32(3),
                r_704ILR.GetDateTime(4),
                // Lectura tolerante del estado (ver DAL_Reserva_704ILR.EstadoDesdeTexto_704ILR):
                // una version con el estado escrito con otra capitalizacion no deja sin
                // listado a toda la reserva.
                DAL_Reserva_704ILR.EstadoDesdeTexto_704ILR(r_704ILR.GetString(5)),
                r_704ILR.GetDecimal(6),
                r_704ILR.IsDBNull(7) ? 0 : r_704ILR.GetInt32(7),
                r_704ILR.GetString(8),
                r_704ILR.GetDateTime(9),
                r_704ILR.IsDBNull(10) ? string.Empty : r_704ILR.GetString(10),
                r_704ILR.IsDBNull(11) ? string.Empty : r_704ILR.GetString(11),
                servicios_704ILR);
    }
}
