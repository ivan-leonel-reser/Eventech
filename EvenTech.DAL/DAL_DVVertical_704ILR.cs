using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace EvenTech.DAL
{
    // Persistencia del digito verificador vertical (uno por tabla protegida).
    public static class DAL_DVVertical_704ILR
    {
        // Ejecuta el recalculo del digito de una tabla de a un puesto por vez. El
        // recalculo lee todas las filas y despues guarda: sin serializarlo, dos puestos
        // que terminan una operacion a la vez podian leer en un orden y grabar en el
        // otro, y quedaba guardado un digito anterior al ultimo cambio (la verificacion
        // del arranque lo informaba como una alteracion que no existia). El bloqueo es
        // de aplicacion (un nombre de recurso del servidor, por tabla) y dura lo que la
        // transaccion que lo toma; el recalculo usa sus propias conexiones. Si otro
        // puesto lo retiene mas de 20 s, el servidor devuelve un error y no se graba.
        public static void Serializar_704ILR(string tabla_704ILR, Action recalculo_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    using (var cmd_704ILR = new SqlCommand(
                        "DECLARE @r INT; " +
                        "EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 20000; " +
                        "IF @r < 0 RAISERROR(N'No se pudo obtener el bloqueo del dígito verificador vertical.', 16, 1);",
                        conn_704ILR, tx_704ILR))
                    {
                        cmd_704ILR.Parameters.Add("@recurso", SqlDbType.NVarChar, 255).Value = "EvenTech_DVV_" + tabla_704ILR;
                        cmd_704ILR.ExecuteNonQuery();
                    }
                    recalculo_704ILR();
                    tx_704ILR.Commit();
                }
            }
        }

        public static string Get_704ILR(string tabla_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT Dvv FROM dbo.DVVertical WHERE Tabla = @t", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@t", SqlDbType.NVarChar, 50).Value = tabla_704ILR;
                var r_704ILR = cmd_704ILR.ExecuteScalar();
                return r_704ILR == null || r_704ILR == System.DBNull.Value ? null : (string)r_704ILR;
            }
        }

        // Inserta o actualiza el DVV de la tabla (upsert).
        public static void Upsert_704ILR(string tabla_704ILR, string dvv_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "IF EXISTS (SELECT 1 FROM dbo.DVVertical WHERE Tabla = @t) " +
                "  UPDATE dbo.DVVertical SET Dvv = @v, CalculadoEn = GETDATE() WHERE Tabla = @t; " +
                "ELSE " +
                "  INSERT INTO dbo.DVVertical (Tabla, Dvv, CalculadoEn) VALUES (@t, @v, GETDATE());",
                cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@t", SqlDbType.NVarChar, 50).Value = tabla_704ILR;
                cmd_704ILR.Parameters.Add("@v", SqlDbType.NVarChar, 64).Value = dvv_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }
    }
}
