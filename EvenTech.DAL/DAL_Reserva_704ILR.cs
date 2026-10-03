using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    public static class DAL_Reserva_704ILR
    {
        // SELECT base con JOIN a salon y cliente para proyectar sus nombres.
        private const string SelectBase_704ILR =
            "SELECT r.Id, r.ClienteId, LTRIM(ISNULL(c.Nombre,'') + ISNULL(' ' + c.Apellido,'')) AS ClienteNombre, " +
            "r.SalonId, s.Nombre, r.FechaEvento, r.Estado, r.Monto, r.CantidadInvitados, " +
            "r.CreatedAt, r.Dvh, r.VenceEl, r.EstadoCoordinacion " +
            "FROM dbo.Reservas r " +
            "INNER JOIN dbo.Salones s ON s.Id = r.SalonId " +
            "LEFT JOIN dbo.Clientes c ON c.Id = r.ClienteId ";

        public static List<BE_Reserva_704ILR> GetAll_704ILR()
        {
            var list_704ILR = new List<BE_Reserva_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                // El desempate por Id hace determinista el orden: varias reservas
                // pueden compartir la misma fecha de evento (RN-03 admite cotizaciones
                // y pendientes conviviendo) y sin criterio adicional el motor no
                // garantiza que se listen siempre igual.
                using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "ORDER BY r.FechaEvento DESC, r.Id DESC", cn_704ILR.OpenConnection_704ILR()))
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
                }
            }
            return list_704ILR;
        }

        public static BE_Reserva_704ILR GetById_704ILR(int id_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE r.Id = @id", cn_704ILR.OpenConnection_704ILR()))
                {
                    cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                    using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    {
                        return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
                    }
                }
            }
        }

        // Sobrecarga transaccional: lee la cabecera sobre la conexion y la transaccion
        // que le pasan, tomando un bloqueo de actualizacion sobre la fila (UPDLOCK,
        // HOLDLOCK). Quien la use serializa a cualquier otra operacion que lea la
        // misma reserva por esta via hasta que su transaccion termine: asi lo que se
        // valida (estado, monto, total cobrado) es lo mismo que se escribe.
        public static BE_Reserva_704ILR GetById_704ILR(int id_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(SelectBaseBloqueado_704ILR + "WHERE r.Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
                }
            }
        }

        // El mismo SELECT base, con el hint de bloqueo sobre la tabla de reservas.
        private static readonly string SelectBaseBloqueado_704ILR =
            SelectBase_704ILR.Replace("FROM dbo.Reservas r ", "FROM dbo.Reservas r WITH (UPDLOCK, HOLDLOCK) ");

        // Anti-solapamiento: hay otra reserva CONFIRMADA para ese salon y fecha
        // (excluyendo la propia reserva en edicion)? Las cotizaciones y reservas
        // pendientes no comprometen el salon: solo una reserva firme lo bloquea.
        public static bool SalonOcupado_704ILR(int salonId_704ILR, DateTime fecha_704ILR, int excluirId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Reservas " +
                "WHERE SalonId = @s AND CAST(FechaEvento AS DATE) = @f AND Estado = @estado AND Id <> @ex",
                cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@s", SqlDbType.Int).Value = salonId_704ILR;
                cmd_704ILR.Parameters.Add("@f", SqlDbType.Date).Value = fecha_704ILR.Date;
                // El estado viaja como parametro desde el enumerado, igual que al
                // persistir la reserva: el estado que compromete el salon se define en
                // un solo lugar y no queda escrito a mano dentro de la consulta.
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = EstadoReserva_704ILR.CONFIRMADA.ToString();
                cmd_704ILR.Parameters.Add("@ex", SqlDbType.Int).Value = excluirId_704ILR;
                return (int)cmd_704ILR.ExecuteScalar() > 0;
            }
        }

        // Fechas comprometidas por salon en un rango: dias con una reserva
        // CONFIRMADA. Una sola query para toda la consulta de disponibilidad
        // (evita ir a la base salon por salon y dia por dia).
        public static Dictionary<int, HashSet<DateTime>> FechasConfirmadasPorSalon_704ILR(DateTime desde_704ILR, DateTime hasta_704ILR)
        {
            var map_704ILR = new Dictionary<int, HashSet<DateTime>>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT SalonId, CAST(FechaEvento AS DATE) FROM dbo.Reservas " +
                "WHERE Estado = @estado AND CAST(FechaEvento AS DATE) BETWEEN @d AND @h",
                cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = EstadoReserva_704ILR.CONFIRMADA.ToString();
                cmd_704ILR.Parameters.Add("@d", SqlDbType.Date).Value = desde_704ILR.Date;
                cmd_704ILR.Parameters.Add("@h", SqlDbType.Date).Value = hasta_704ILR.Date;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    while (r_704ILR.Read())
                    {
                        int salonId_704ILR = r_704ILR.GetInt32(0);
                        if (!map_704ILR.TryGetValue(salonId_704ILR, out var fechas_704ILR))
                            map_704ILR[salonId_704ILR] = fechas_704ILR = new HashSet<DateTime>();
                        fechas_704ILR.Add(r_704ILR.GetDateTime(1).Date);
                    }
                }
            }
            return map_704ILR;
        }

        public static int Insert_704ILR(BE_Reserva_704ILR reserva_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return Insert_704ILR(reserva_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional: escribe sobre la conexion y la transaccion que
        // le pasan, para que la reserva y sus servicios contratados entren o no
        // entren juntos. La usa BLL_Reserva cuando orquesta la operacion completa.
        public static int Insert_704ILR(BE_Reserva_704ILR reserva_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "INSERT INTO dbo.Reservas (ClienteId, SalonId, FechaEvento, Estado, Monto, CantidadInvitados, Dvh, VenceEl) " +
                "OUTPUT INSERTED.Id " +
                "VALUES (@cliente, @salon, @fecha, @estado, @monto, @invitados, @dvh, @vence)",
                conn_704ILR, tx_704ILR))
            {
                BindEditable_704ILR(cmd_704ILR, reserva_704ILR);
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        public static void Update_704ILR(BE_Reserva_704ILR reserva_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                Update_704ILR(reserva_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional (ver Insert_704ILR).
        public static void Update_704ILR(BE_Reserva_704ILR reserva_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "UPDATE dbo.Reservas SET ClienteId = @cliente, SalonId = @salon, " +
                "FechaEvento = @fecha, Estado = @estado, Monto = @monto, " +
                "CantidadInvitados = @invitados, Dvh = @dvh, " +
                "VenceEl = @vence WHERE Id = @id",
                conn_704ILR, tx_704ILR))
            {
                BindEditable_704ILR(cmd_704ILR, reserva_704ILR);
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = reserva_704ILR.Id_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Actualiza solo el DV horizontal (usado al recalcular la linea base).
        public static void UpdateDvh_704ILR(int id_704ILR, string dvh_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand("UPDATE dbo.Reservas SET Dvh = @dvh WHERE Id = @id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@dvh", SqlDbType.NVarChar, 64).Value = (object)dvh_704ILR ?? DBNull.Value;
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        private static void BindEditable_704ILR(SqlCommand cmd_704ILR, BE_Reserva_704ILR reserva_704ILR)
        {
            cmd_704ILR.Parameters.Add("@cliente", SqlDbType.Int).Value = reserva_704ILR.ClienteId_704ILR;
            cmd_704ILR.Parameters.Add("@salon", SqlDbType.Int).Value = reserva_704ILR.SalonId_704ILR;
            cmd_704ILR.Parameters.Add("@fecha", SqlDbType.DateTime).Value = reserva_704ILR.FechaEvento_704ILR;
            cmd_704ILR.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = reserva_704ILR.Estado_704ILR.ToString();
            cmd_704ILR.Parameters.Add("@monto", SqlDbType.Decimal).Value = reserva_704ILR.Monto_704ILR;
            cmd_704ILR.Parameters.Add("@invitados", SqlDbType.Int).Value = reserva_704ILR.CantidadInvitados_704ILR;
            cmd_704ILR.Parameters.Add("@dvh", SqlDbType.NVarChar, 64).Value = (object)reserva_704ILR.Dvh_704ILR ?? DBNull.Value;
            cmd_704ILR.Parameters.Add("@vence", SqlDbType.DateTime).Value =
                reserva_704ILR.VenceEl_704ILR.HasValue ? (object)reserva_704ILR.VenceEl_704ILR.Value : DBNull.Value;
        }

        // Reservas cuyo Estado almacenado no es exactamente uno de los nombres del ciclo
        // de vida: otra capitalizacion ('confirmada') o un texto ajeno. La comparacion
        // es binaria a proposito, porque la intercalacion de la columna no distingue
        // mayusculas. Los nombres viajan como parametros desde el enumerado, igual que
        // en el resto de las consultas por estado. La usa la verificacion de integridad
        // para informar la reserva alterada en vez de abortar.
        public static List<int> IdsConEstadoFueraDeDominio_704ILR()
        {
            var ids_704ILR = new List<int>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(string.Empty, cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.CommandText = "SELECT Id FROM dbo.Reservas WHERE " +
                    FiltroEstadoFueraDeDominio_704ILR(cmd_704ILR) + " ORDER BY Id";
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) ids_704ILR.Add(r_704ILR.GetInt32(0));
            }
            return ids_704ILR;
        }

        // El mismo criterio para UNA reserva, sobre la conexion y la transaccion que le
        // pasan: devuelve el Estado tal cual esta almacenado si no es exactamente uno de
        // los nombres del ciclo de vida, o null si lo es (o si la reserva no existe). La
        // capa de negocio la consulta con la cabecera ya bloqueada y antes de reescribirla:
        // la escritura persiste el nombre exacto del estado y el valor alterado se pierde,
        // sin que el DV horizontal lo advierta porque se calcula con ese mismo nombre.
        public static string EstadoFueraDeDominio_704ILR(int id_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(string.Empty, conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.CommandText = "SELECT Estado FROM dbo.Reservas WHERE Id = @id AND " +
                    FiltroEstadoFueraDeDominio_704ILR(cmd_704ILR);
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                object valor_704ILR = cmd_704ILR.ExecuteScalar();
                return valor_704ILR == null || valor_704ILR == DBNull.Value ? null : (string)valor_704ILR;
            }
        }

        // Condicion "Estado fuera del dominio exacto" que comparten las dos consultas de
        // arriba y la de las versiones (DAL_ReservaMemento_704ILR.EstadoFueraDeDominio_704ILR),
        // para que la verificacion de integridad, las escrituras y los asientos de rechazo
        // juzguen igual. Agrega al comando un parametro por cada nombre del enumerado.
        internal static string FiltroEstadoFueraDeDominio_704ILR(SqlCommand cmd_704ILR)
        {
            string[] nombres_704ILR = Enum.GetNames(typeof(EstadoReserva_704ILR));
            var marcadores_704ILR = new List<string>();
            for (int i_704ILR = 0; i_704ILR < nombres_704ILR.Length; i_704ILR++)
            {
                string marcador_704ILR = "@e" + i_704ILR;
                marcadores_704ILR.Add(marcador_704ILR);
                cmd_704ILR.Parameters.Add(marcador_704ILR, SqlDbType.NVarChar, 20).Value = nombres_704ILR[i_704ILR];
            }
            return "Estado COLLATE Latin1_General_BIN2 NOT IN (" + string.Join(", ", marcadores_704ILR) + ")";
        }

        // Lectura tolerante del Estado almacenado. La base compara esa columna sin
        // distinguir mayusculas: para el motor (anti-solapamiento, indice unico de las
        // confirmadas) 'confirmada' ES 'CONFIRMADA'. La lectura aplica el mismo criterio
        // en lugar de abortar el listado completo por una sola fila escrita por fuera
        // del sistema; la verificacion de integridad informa esas filas aparte
        // (IdsConEstadoFueraDeDominio_704ILR). Como en la base, solo los espacios finales
        // no cuentan. Un texto que no corresponde a ningun estado se devuelve como un
        // valor fuera del enum, que la capa de negocio no deja operar (RN-05).
        internal static EstadoReserva_704ILR EstadoDesdeTexto_704ILR(string texto_704ILR)
        {
            string limpio_704ILR = (texto_704ILR ?? string.Empty).TrimEnd();
            foreach (EstadoReserva_704ILR estado_704ILR in Enum.GetValues(typeof(EstadoReserva_704ILR)))
                if (string.Equals(estado_704ILR.ToString(), limpio_704ILR, StringComparison.OrdinalIgnoreCase))
                    return estado_704ILR;
            return (EstadoReserva_704ILR)(-1);
        }

        private static BE_Reserva_704ILR Map_704ILR(SqlDataReader r_704ILR) => new BE_Reserva_704ILR
        {
            Id_704ILR = r_704ILR.GetInt32(0),
            ClienteId_704ILR = r_704ILR.IsDBNull(1) ? 0 : r_704ILR.GetInt32(1),
            ClienteNombre_704ILR = r_704ILR.IsDBNull(2) ? string.Empty : r_704ILR.GetString(2),
            SalonId_704ILR = r_704ILR.GetInt32(3),
            SalonNombre_704ILR = r_704ILR.GetString(4),
            FechaEvento_704ILR = r_704ILR.GetDateTime(5),
            Estado_704ILR = EstadoDesdeTexto_704ILR(r_704ILR.GetString(6)),
            Monto_704ILR = r_704ILR.GetDecimal(7),
            CantidadInvitados_704ILR = r_704ILR.IsDBNull(8) ? 0 : r_704ILR.GetInt32(8),
            CreatedAt_704ILR = r_704ILR.GetDateTime(9),
            Dvh_704ILR = r_704ILR.IsDBNull(10) ? null : r_704ILR.GetString(10),
            VenceEl_704ILR = r_704ILR.IsDBNull(11) ? (DateTime?)null : r_704ILR.GetDateTime(11),
            EstadoCoordinacion_704ILR = DAL_Coordinacion_704ILR.EstadoDesdeTexto_704ILR(r_704ILR.IsDBNull(12) ? null : r_704ILR.GetString(12))
        };
    }
}
