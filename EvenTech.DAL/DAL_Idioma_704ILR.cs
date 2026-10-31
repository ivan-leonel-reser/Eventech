using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    public static class DAL_Idioma_704ILR
    {
        public static List<BE_Idioma_704ILR> GetIdiomas_704ILR()
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return GetIdiomas_704ILR(cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional: el alta de un idioma relee los existentes con el
        // bloqueo del alta ya tomado (TomarBloqueoAlta_704ILR).
        public static List<BE_Idioma_704ILR> GetIdiomas_704ILR(SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            var list_704ILR = new List<BE_Idioma_704ILR>();
            using (var cmd_704ILR = new SqlCommand(
                "SELECT Id, Codigo, Nombre FROM dbo.Idiomas ORDER BY Nombre",
                conn_704ILR, tx_704ILR))
            using (var r_704ILR = cmd_704ILR.ExecuteReader())
            {
                while (r_704ILR.Read())
                {
                    list_704ILR.Add(new BE_Idioma_704ILR
                    {
                        Id_704ILR = r_704ILR.GetInt32(0),
                        Codigo_704ILR = r_704ILR.GetString(1),
                        Nombre_704ILR = r_704ILR.GetString(2)
                    });
                }
            }
            return list_704ILR;
        }

        // Recurso del bloqueo de aplicacion que serializa las altas de idioma. La
        // unicidad del NOMBRE (comparado como se ve, sin mayusculas y en compatibilidad)
        // no se puede expresar con un indice de la base: sin este bloqueo dos altas
        // simultaneas con el mismo nombre y codigos distintos pasaban las dos.
        private const string RecursoAltaIdioma_704ILR = "EvenTech_AltaIdioma";

        // Toma, dentro de la transaccion dada, el bloqueo exclusivo del alta de idiomas;
        // se libera con el COMMIT o el ROLLBACK. Si otra alta lo retiene mas de 20 s, el
        // servidor devuelve un error y no se graba nada.
        public static void TomarBloqueoAlta_704ILR(SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "DECLARE @r INT; " +
                "EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 20000; " +
                "IF @r < 0 RAISERROR(N'No se pudo obtener el bloqueo del alta de idiomas.', 16, 1);",
                conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@recurso", SqlDbType.NVarChar, 255).Value = RecursoAltaIdioma_704ILR;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        public static bool ExistsCodigo_704ILR(string codigo_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Idiomas WHERE Codigo = @c", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@c", SqlDbType.NVarChar, 5).Value = codigo_704ILR ?? string.Empty;
                return (int)cmd_704ILR.ExecuteScalar() > 0;
            }
        }

        public static int InsertIdioma_704ILR(string codigo_704ILR, string nombre_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                return InsertIdioma_704ILR(codigo_704ILR, nombre_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional: no abre ni cierra nada, escribe donde le digan.
        // Asi el idioma y la copia de sus leyendas quedan en una sola transaccion.
        public static int InsertIdioma_704ILR(string codigo_704ILR, string nombre_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "INSERT INTO dbo.Idiomas (Codigo, Nombre) OUTPUT INSERTED.Id VALUES (@c, @n)",
                conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@c", SqlDbType.NVarChar, 5).Value = codigo_704ILR;
                cmd_704ILR.Parameters.Add("@n", SqlDbType.NVarChar, 50).Value = nombre_704ILR;
                return (int)cmd_704ILR.ExecuteScalar();
            }
        }

        // Claves distintas existentes (para inicializar un idioma nuevo).
        public static List<string> GetClaves_704ILR()
        {
            var list_704ILR = new List<string>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT DISTINCT Clave FROM dbo.Traducciones ORDER BY Clave", cn_704ILR.OpenConnection_704ILR()))
            using (var r_704ILR = cmd_704ILR.ExecuteReader())
            {
                while (r_704ILR.Read()) list_704ILR.Add(r_704ILR.GetString(0));
            }
            return list_704ILR;
        }

        // Inserta o actualiza el texto de una clave para un idioma (upsert).
        public static void UpsertTraduccion_704ILR(int idiomaId_704ILR, string clave_704ILR, string texto_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                UpsertTraduccion_704ILR(idiomaId_704ILR, clave_704ILR, texto_704ILR, cn_704ILR.OpenConnection_704ILR(), null);
        }

        // Sobrecarga transaccional del upsert: un lote de traducciones se guarda
        // entero o no se guarda.
        public static void UpsertTraduccion_704ILR(int idiomaId_704ILR, string clave_704ILR, string texto_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            using (var cmd_704ILR = new SqlCommand(
                "IF EXISTS (SELECT 1 FROM dbo.Traducciones WHERE IdiomaId = @i AND Clave = @c) " +
                "  UPDATE dbo.Traducciones SET Texto = @t WHERE IdiomaId = @i AND Clave = @c; " +
                "ELSE " +
                "  INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto) VALUES (@i, @c, @t);",
                conn_704ILR, tx_704ILR))
            {
                cmd_704ILR.Parameters.Add("@i", SqlDbType.Int).Value = idiomaId_704ILR;
                cmd_704ILR.Parameters.Add("@c", SqlDbType.NVarChar, 60).Value = clave_704ILR;
                cmd_704ILR.Parameters.Add("@t", SqlDbType.NVarChar, 250).Value = texto_704ILR ?? string.Empty;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Traducciones de un idioma como diccionario clave -> texto.
        public static Dictionary<string, string> GetTraducciones_704ILR(int idiomaId_704ILR)
        {
            var dict_704ILR = new Dictionary<string, string>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT Clave, Texto FROM dbo.Traducciones WHERE IdiomaId = @id",
                cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = idiomaId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    while (r_704ILR.Read())
                        dict_704ILR[r_704ILR.GetString(0)] = r_704ILR.GetString(1);
                }
            }
            return dict_704ILR;
        }
    }
}
