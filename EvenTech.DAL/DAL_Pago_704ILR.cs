using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    public static class DAL_Pago_704ILR
    {
        // Proyeccion comun de las lecturas: la fila del pago, el nombre de su medio de
        // pago y su digito verificador horizontal.
        private const string Seleccion_704ILR =
            "SELECT p.Id, p.ReservaId, p.MetodoPagoId, m.Nombre, p.Monto, p.Fecha, p.Observacion, p.Dvh " +
            "FROM dbo.Pagos p INNER JOIN dbo.MetodosPago m ON m.Id = p.MetodoPagoId ";

        public static List<BE_Pago_704ILR> GetByReserva_704ILR(int reservaId_704ILR)
        {
            var list_704ILR = new List<BE_Pago_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                Seleccion_704ILR + "WHERE p.ReservaId = @r ORDER BY p.Fecha, p.Id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read())
                        list_704ILR.Add(Map_704ILR(r_704ILR));
            }
            return list_704ILR;
        }

        // Todos los pagos, por Id ascendente: es el orden estable sobre el que se
        // calcula y se verifica el digito verificador vertical de la tabla.
        public static List<BE_Pago_704ILR> GetAll_704ILR()
        {
            var list_704ILR = new List<BE_Pago_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                Seleccion_704ILR + "ORDER BY p.Id", cn_704ILR.OpenConnection_704ILR()))
            using (var r_704ILR = cmd_704ILR.ExecuteReader())
                while (r_704ILR.Read())
                    list_704ILR.Add(Map_704ILR(r_704ILR));
            return list_704ILR;
        }

        // Un pago puntual, sobre la conexion y la transaccion que le pasan: la
        // validacion, el borrado y el calculo del digito verificador ven el mismo
        // estado. La usa BLL_Pago cuando orquesta el movimiento completo.
        public static BE_Pago_704ILR GetById_704ILR(int id_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                Seleccion_704ILR + "WHERE p.Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
                }
            }
        }

        public static decimal TotalPagado_704ILR(int reservaId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return TotalPagado_704ILR(reservaId_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional (ver GetById_704ILR): el total se relee dentro de
        // la misma transaccion que despues inserta o borra, con la cabecera de la
        // reserva ya bloqueada, asi el tope RN-04 se valida contra lo que hay de verdad.
        public static decimal TotalPagado_704ILR(int reservaId_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand("SELECT ISNULL(SUM(Monto), 0) FROM dbo.Pagos WHERE ReservaId = @r", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = reservaId_704ILR;
                return (decimal)cmd_704ILR.ExecuteScalar();
            }
        }

        // El alta y la baja de un pago se hacen solo dentro de la transaccion del
        // movimiento (BLL_Pago): el pago queda guardado junto con su digito verificador
        // horizontal o no queda.
        public static int Insert_704ILR(BE_Pago_704ILR p_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "INSERT INTO dbo.Pagos (ReservaId, MetodoPagoId, Monto, Fecha, Observacion) " +
                "OUTPUT INSERTED.Id VALUES (@r, @m, @mo, GETDATE(), @o)", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@r", SqlDbType.Int).Value = p_704ILR.ReservaId_704ILR;
                cmd_704ILR.Parameters.Add("@m", SqlDbType.Int).Value = p_704ILR.MetodoPagoId_704ILR;
                cmd_704ILR.Parameters.Add("@mo", SqlDbType.Decimal).Value = p_704ILR.Monto_704ILR;
                cmd_704ILR.Parameters.Add("@o", SqlDbType.NVarChar, 200).Value = (object)p_704ILR.Observacion_704ILR ?? System.DBNull.Value;
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        // Graba el digito verificador horizontal del pago. Dentro de la transaccion
        // del cobro va con su conexion y su transaccion; el recalculo de la linea base
        // (herramienta de integridad) lo hace por fuera, con la sobrecarga de abajo.
        public static void UpdateDvh_704ILR(int id_704ILR, string dvh_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand("UPDATE dbo.Pagos SET Dvh = @dvh WHERE Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@dvh", SqlDbType.NVarChar, 64).Value = (object)dvh_704ILR ?? System.DBNull.Value;
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        public static void UpdateDvh_704ILR(int id_704ILR, string dvh_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                UpdateDvh_704ILR(id_704ILR, dvh_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Devuelve las filas borradas: 0 si el pago ya no estaba, para que quien
        // orquesta la anulacion no la informe ni la asiente como hecha.
        public static int Delete_704ILR(int id_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand("DELETE FROM dbo.Pagos WHERE Id = @id", conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                return cmd_704ILR.ExecuteNonQuery();
            }
        }

        private static BE_Pago_704ILR Map_704ILR(SqlDataReader r_704ILR) => new BE_Pago_704ILR
        {
            Id_704ILR = r_704ILR.GetInt32(0),
            ReservaId_704ILR = r_704ILR.GetInt32(1),
            MetodoPagoId_704ILR = r_704ILR.GetInt32(2),
            MetodoNombre_704ILR = r_704ILR.GetString(3),
            Monto_704ILR = r_704ILR.GetDecimal(4),
            Fecha_704ILR = r_704ILR.GetDateTime(5),
            Observacion_704ILR = r_704ILR.IsDBNull(6) ? null : r_704ILR.GetString(6),
            Dvh_704ILR = r_704ILR.IsDBNull(7) ? null : r_704ILR.GetString(7)
        };
    }
}
