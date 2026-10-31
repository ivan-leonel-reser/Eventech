using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    public static class DAL_User_704ILR
    {
        // Todos los usuarios (para la asignacion de perfiles).
        public static List<BE_User_704ILR> GetAll_704ILR()
        {
            var list_704ILR = new List<BE_User_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT Id, Username, PasswordHash, CreatedAt, PerfilId, Activo, Blocked, FailedAttempts FROM dbo.Users ORDER BY Username",
                cn_704ILR.OpenConnection_704ILR()))
            using (var r_704ILR = cmd_704ILR.ExecuteReader())
            {
                while (r_704ILR.Read())
                    list_704ILR.Add(new BE_User_704ILR
                    {
                        Id_704ILR = r_704ILR.GetInt32(0),
                        Username_704ILR = r_704ILR.GetString(1),
                        PasswordHash_704ILR = r_704ILR.GetString(2),
                        CreatedAt_704ILR = r_704ILR.GetDateTime(3),
                        PerfilId_704ILR = r_704ILR.IsDBNull(4) ? (int?)null : r_704ILR.GetInt32(4),
                        Activo_704ILR = r_704ILR.GetBoolean(5),
                        Blocked_704ILR = r_704ILR.GetBoolean(6),
                        FailedAttempts_704ILR = r_704ILR.GetInt32(7)
                    });
            }
            return list_704ILR;
        }

        // Asigna (o quita, con null) el perfil de un usuario (serializado con las demas
        // grabaciones de la gestion de perfiles).
        public static void SetPerfil_704ILR(int userId_704ILR, int? perfilId_704ILR) =>
            SetPerfiles_704ILR(new Dictionary<int, int?> { [userId_704ILR] = perfilId_704ILR });

        // Asigna los perfiles de varias cuentas en una sola transaccion: el lote que
        // valida la BLL (que no deje al sistema sin gestor de perfiles) se aplica
        // entero o no se aplica.
        public static void SetPerfiles_704ILR(IDictionary<int, int?> perfilPorUsuario_704ILR) =>
            SetPerfiles_704ILR(perfilPorUsuario_704ILR, null);

        // Igual que la anterior, pero la transaccion toma primero el bloqueo de la gestion
        // de perfiles y, con el tomado, consulta 'puedeGrabar' (la regla de la BLL) antes
        // de escribir: si devuelve false no escribe nada y devuelve false. Asi la regla se
        // evalua y se graba sin que otra grabacion de perfiles se meta en el medio.
        public static bool SetPerfiles_704ILR(IDictionary<int, int?> perfilPorUsuario_704ILR, Func<bool> puedeGrabar_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                var conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (var tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    DAL_Perfil_704ILR.TomarBloqueoGestion_704ILR(conn_704ILR, tx_704ILR);
                    if (puedeGrabar_704ILR != null && !puedeGrabar_704ILR())
                    {
                        tx_704ILR.Rollback();
                        return false;
                    }
                    foreach (var kv_704ILR in perfilPorUsuario_704ILR)
                    {
                        using (var cmd_704ILR = new SqlCommand("UPDATE dbo.Users SET PerfilId = @p WHERE Id = @id", conn_704ILR, tx_704ILR))
                        {
                            cmd_704ILR.Parameters.Add("@p", SqlDbType.Int).Value = (object)kv_704ILR.Value ?? DBNull.Value;
                            cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = kv_704ILR.Key;
                            cmd_704ILR.ExecuteNonQuery();
                        }
                    }
                    tx_704ILR.Commit();
                    return true;
                }
            }
        }

        // Una cuenta por Id (la bitacora de perfiles y desbloqueos la nombra).
        public static BE_User_704ILR GetById_704ILR(int userId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "SELECT Id, Username, PasswordHash, CreatedAt, PerfilId, Activo, Blocked, FailedAttempts FROM dbo.Users WHERE Id = @id",
                cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = userId_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                {
                    if (!r_704ILR.Read()) return null;
                    return new BE_User_704ILR
                    {
                        Id_704ILR = r_704ILR.GetInt32(0),
                        Username_704ILR = r_704ILR.GetString(1),
                        PasswordHash_704ILR = r_704ILR.GetString(2),
                        CreatedAt_704ILR = r_704ILR.GetDateTime(3),
                        PerfilId_704ILR = r_704ILR.IsDBNull(4) ? (int?)null : r_704ILR.GetInt32(4),
                        Activo_704ILR = r_704ILR.GetBoolean(5),
                        Blocked_704ILR = r_704ILR.GetBoolean(6),
                        FailedAttempts_704ILR = r_704ILR.GetInt32(7)
                    };
                }
            }
        }

        public static BE_User_704ILR GetByUsername_704ILR(string username_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                using (var cmd_704ILR = new SqlCommand(
                    "SELECT Id, Username, PasswordHash, CreatedAt, PerfilId, Activo, Blocked, FailedAttempts FROM dbo.Users WHERE Username = @username",
                    cn_704ILR.OpenConnection_704ILR()))
                {
                    cmd_704ILR.Parameters.Add("@username", SqlDbType.NVarChar, 50).Value = username_704ILR ?? string.Empty;

                    using (var reader_704ILR = cmd_704ILR.ExecuteReader())
                    {
                        if (!reader_704ILR.Read()) return null;

                        return new BE_User_704ILR
                        {
                            Id_704ILR = reader_704ILR.GetInt32(0),
                            Username_704ILR = reader_704ILR.GetString(1),
                            PasswordHash_704ILR = reader_704ILR.GetString(2),
                            CreatedAt_704ILR = reader_704ILR.GetDateTime(3),
                            PerfilId_704ILR = reader_704ILR.IsDBNull(4) ? (int?)null : reader_704ILR.GetInt32(4),
                            Activo_704ILR = reader_704ILR.GetBoolean(5),
                            Blocked_704ILR = reader_704ILR.GetBoolean(6),
                            FailedAttempts_704ILR = reader_704ILR.GetInt32(7)
                        };
                    }
                }
            }
        }

        public static bool ExistsUsername_704ILR(string username_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                using (var cmd_704ILR = new SqlCommand(
                    "SELECT COUNT(1) FROM dbo.Users WHERE Username = @username",
                    cn_704ILR.OpenConnection_704ILR()))
                {
                    cmd_704ILR.Parameters.Add("@username", SqlDbType.NVarChar, 50).Value = username_704ILR ?? string.Empty;
                    return (int)cmd_704ILR.ExecuteScalar() > 0;
                }
            }
        }

        public static void Insert_704ILR(string username_704ILR, string passwordHash_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                using (var cmd_704ILR = new SqlCommand(
                    "INSERT INTO dbo.Users (Username, PasswordHash) VALUES (@username, @passwordHash)",
                    cn_704ILR.OpenConnection_704ILR()))
                {
                    cmd_704ILR.Parameters.Add("@username", SqlDbType.NVarChar, 50).Value = username_704ILR;
                    cmd_704ILR.Parameters.Add("@passwordHash", SqlDbType.NVarChar, 64).Value = passwordHash_704ILR;
                    cmd_704ILR.ExecuteNonQuery();
                }
            }
        }

        // Suma 1 al contador de intentos fallidos y devuelve el nuevo total.
        public static int IncrementFailedAttempts_704ILR(string username_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(
                "UPDATE dbo.Users SET FailedAttempts = FailedAttempts + 1 WHERE Username = @u; " +
                "SELECT FailedAttempts FROM dbo.Users WHERE Username = @u;",
                cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@u", SqlDbType.NVarChar, 50).Value = username_704ILR ?? string.Empty;
                object o_704ILR = cmd_704ILR.ExecuteScalar();
                return (o_704ILR == null || o_704ILR == DBNull.Value) ? 0 : (int)o_704ILR;
            }
        }

        public static void ResetFailedAttempts_704ILR(string username_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand("UPDATE dbo.Users SET FailedAttempts = 0 WHERE Username = @u", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@u", SqlDbType.NVarChar, 50).Value = username_704ILR ?? string.Empty;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        public static void SetBlocked_704ILR(string username_704ILR, bool blocked_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand("UPDATE dbo.Users SET Blocked = @b WHERE Username = @u", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@b", SqlDbType.Bit).Value = blocked_704ILR;
                cmd_704ILR.Parameters.Add("@u", SqlDbType.NVarChar, 50).Value = username_704ILR ?? string.Empty;
                cmd_704ILR.ExecuteNonQuery();
            }
        }

        // Desbloqueo por admin: quita el bloqueo y resetea el contador de intentos.
        // Devuelve las filas afectadas: 0 si la cuenta no existe.
        public static int Desbloquear_704ILR(int userId_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand("UPDATE dbo.Users SET Blocked = 0, FailedAttempts = 0 WHERE Id = @id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = userId_704ILR;
                return cmd_704ILR.ExecuteNonQuery();
            }
        }
    }
}
