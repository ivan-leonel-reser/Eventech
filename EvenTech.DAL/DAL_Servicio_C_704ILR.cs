using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Acceso a la tabla copia del catalogo (dbo.Servicios_C, bitacora de cambios).
    // Las versiones las escriben los triggers de dbo.Servicios; aca solo se leen y se
    // activa una version guardada: se le escriben a dbo.Servicios los datos de esa
    // version y el trigger de modificacion le pasa Act = 1 (sin agregar otra version).
    public static class DAL_Servicio_C_704ILR
    {
        private const int AnchoNombre_704ILR = 80;

        private const string SelectBase_704ILR =
            "SELECT c.Id, c.ServicioId, c.Fecha, c.Hora, c.Nombre, c.Descripcion, c.Precio, c.Activo, c.Act, c.Operacion, c.Usuario " +
            "FROM dbo.Servicios_C c ";

        // Claves de sesion que leen los triggers de dbo.Servicios (ver db/schema.sql).
        internal const string ClaveUsuario_704ILR = "EvenTech.Usuario";
        internal const string ClaveActivarVersion_704ILR = "EvenTech.ActivarVersion";

        // Busqueda combinada (cada filtro es opcional), en orden cronologico: la
        // version mas vieja primero, como se lee una bitacora. Nombre se busca entero
        // (el elegido de la lista) o como texto literal contenido en el nombre.
        public static List<BE_Servicio_C_704ILR> Buscar_704ILR(FiltrosServicio_C_704ILR f_704ILR)
        {
            f_704ILR = f_704ILR ?? new FiltrosServicio_C_704ILR();
            var sb_704ILR = new StringBuilder(SelectBase_704ILR + "WHERE 1=1 ");
            var ps_704ILR = new List<SqlParameter>();
            if (f_704ILR.ServicioId_704ILR.HasValue)
            {
                sb_704ILR.Append("AND c.ServicioId = @sid ");
                ps_704ILR.Add(new SqlParameter("@sid", SqlDbType.Int) { Value = f_704ILR.ServicioId_704ILR.Value });
            }
            if (!string.IsNullOrWhiteSpace(f_704ILR.Nombre_704ILR) && f_704ILR.NombreExacto_704ILR)
            {
                // El nombre elegido de la lista de la pantalla: esa version y no las de un
                // nombre que lo contenga ("DJ" no trae "DJ y sonido"), comparado en binario
                // como arma la lista la pantalla ("dj y sonido" no trae "DJ y sonido").
                sb_704ILR.Append("AND c.Nombre COLLATE Latin1_General_BIN2 = @nombre ");
                ps_704ILR.Add(new SqlParameter("@nombre", SqlDbType.NVarChar, AnchoNombre_704ILR) { Value = f_704ILR.Nombre_704ILR });
            }
            else if (!string.IsNullOrWhiteSpace(f_704ILR.Nombre_704ILR))
            {
                sb_704ILR.Append("AND c.Nombre LIKE @nombre ");
                ps_704ILR.Add(new SqlParameter("@nombre", SqlDbType.NVarChar, DAL_Bitacora_704ILR.LargoPatron_704ILR(AnchoNombre_704ILR))
                    { Value = DAL_Bitacora_704ILR.PatronContiene_704ILR(f_704ILR.Nombre_704ILR, AnchoNombre_704ILR) });
            }
            if (f_704ILR.FechaInicial_704ILR.HasValue)
            {
                sb_704ILR.Append("AND c.Fecha >= @desde ");
                ps_704ILR.Add(new SqlParameter("@desde", SqlDbType.Date) { Value = f_704ILR.FechaInicial_704ILR.Value.Date });
            }
            if (f_704ILR.FechaFinal_704ILR.HasValue)
            {
                sb_704ILR.Append("AND c.Fecha <= @hasta ");
                ps_704ILR.Add(new SqlParameter("@hasta", SqlDbType.Date) { Value = f_704ILR.FechaFinal_704ILR.Value.Date });
            }
            sb_704ILR.Append("ORDER BY c.Fecha, c.Hora, c.Id");

            var list_704ILR = new List<BE_Servicio_C_704ILR>();
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(sb_704ILR.ToString(), cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.AddRange(ps_704ILR.ToArray());
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    while (r_704ILR.Read()) list_704ILR.Add(Map_704ILR(r_704ILR));
            }
            return list_704ILR;
        }

        // Una version por Id (null si no existe).
        public static BE_Servicio_C_704ILR GetById_704ILR(int id_704ILR)
        {
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE c.Id = @id", cn_704ILR.OpenConnection_704ILR()))
            {
                cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = id_704ILR;
                using (var r_704ILR = cmd_704ILR.ExecuteReader())
                    return r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
            }
        }

        // Activa una version guardada, en una transaccion: bloquea la fila del servicio
        // (dos puestos que activan a la vez se ordenan), relee la version, escribe sus datos en
        // dbo.Servicios con la clave de sesion que le indica al trigger que se trata
        // de una activacion y comprueba que la version haya quedado vigente.
        // Devuelve la version leida (null si ya no existe); activada = false si ya era
        // la vigente (no escribe nada). Un nombre que otro servicio ya usa llega como
        // SqlException de unicidad (2601/2627) y la transaccion se deshace.
        public static BE_Servicio_C_704ILR Activar_704ILR(int versionId_704ILR, string usuario_704ILR, out bool activada_704ILR)
        {
            activada_704ILR = false;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                try
                {
                    using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                    {
                        // Orden de bloqueo: primero la fila del servicio (la misma que toma
                        // una modificacion comun antes de que su trigger toque la copia) y
                        // despues la version. Asi dos activaciones del mismo servicio, o una
                        // activacion y una modificacion, se ordenan sin interbloquearse.
                        // ServicioId de una version no cambia (TRG_Servicios_C_SoloAct).
                        using (var cmd_704ILR = new SqlCommand(
                            "SELECT s.Id FROM dbo.Servicios s WITH (UPDLOCK, HOLDLOCK) " +
                            "WHERE s.Id = (SELECT c.ServicioId FROM dbo.Servicios_C c WHERE c.Id = @id)", conn_704ILR, tx_704ILR))
                        {
                            cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = versionId_704ILR;
                            cmd_704ILR.ExecuteScalar();
                        }
                        BE_Servicio_C_704ILR v_704ILR;
                        using (var cmd_704ILR = new SqlCommand(SelectBase_704ILR + "WHERE c.Id = @id", conn_704ILR, tx_704ILR))
                        {
                            cmd_704ILR.Parameters.Add("@id", SqlDbType.Int).Value = versionId_704ILR;
                            using (var r_704ILR = cmd_704ILR.ExecuteReader())
                                v_704ILR = r_704ILR.Read() ? Map_704ILR(r_704ILR) : null;
                        }
                        if (v_704ILR == null || v_704ILR.Act_704ILR)
                        {
                            tx_704ILR.Commit();
                            return v_704ILR;
                        }

                        using (var cmd_704ILR = new SqlCommand(
                            "EXEC sys.sp_set_session_context @key = @kUsuario, @value = @usuario; " +
                            "EXEC sys.sp_set_session_context @key = @kVersion, @value = @version; " +
                            "UPDATE dbo.Servicios SET Nombre = @n, Descripcion = @d, Precio = @p, Activo = @a WHERE Id = @sid; " +
                            "EXEC sys.sp_set_session_context @key = @kVersion, @value = NULL; " +
                            "SELECT Act FROM dbo.Servicios_C WHERE Id = @version;", conn_704ILR, tx_704ILR))
                        {
                            cmd_704ILR.Parameters.Add("@kUsuario", SqlDbType.NVarChar, 128).Value = ClaveUsuario_704ILR;
                            cmd_704ILR.Parameters.Add("@usuario", SqlDbType.NVarChar, 50).Value = Usuario_704ILR(usuario_704ILR);
                            cmd_704ILR.Parameters.Add("@kVersion", SqlDbType.NVarChar, 128).Value = ClaveActivarVersion_704ILR;
                            cmd_704ILR.Parameters.Add("@version", SqlDbType.Int).Value = v_704ILR.Id_704ILR;
                            cmd_704ILR.Parameters.Add("@n", SqlDbType.NVarChar, 80).Value = v_704ILR.Nombre_704ILR;
                            cmd_704ILR.Parameters.Add("@d", SqlDbType.NVarChar, 250).Value = (object)v_704ILR.Descripcion_704ILR ?? DBNull.Value;
                            cmd_704ILR.Parameters.Add("@p", SqlDbType.Decimal).Value = v_704ILR.Precio_704ILR;
                            cmd_704ILR.Parameters["@p"].Precision = 12;
                            cmd_704ILR.Parameters["@p"].Scale = 2;
                            cmd_704ILR.Parameters.Add("@a", SqlDbType.Bit).Value = v_704ILR.Activo_704ILR;
                            cmd_704ILR.Parameters.Add("@sid", SqlDbType.Int).Value = v_704ILR.ServicioId_704ILR;
                            object act_704ILR = cmd_704ILR.ExecuteScalar();
                            // La version tiene que haber quedado vigente: si el trigger no la
                            // reconocio (datos que no coinciden), no se confirma nada.
                            if (!(act_704ILR is bool vigente_704ILR) || !vigente_704ILR)
                                throw new InvalidOperationException(
                                    "La version #" + v_704ILR.Id_704ILR + " no quedo vigente al activarla: se deshace la operacion.");
                        }
                        tx_704ILR.Commit();
                        activada_704ILR = true;
                        v_704ILR.Act_704ILR = true;
                        return v_704ILR;
                    }
                }
                finally
                {
                    LimpiarSesion_704ILR(conn_704ILR);
                }
            }
        }

        // Prefijo de los comandos que escriben dbo.Servicios: deja el usuario de la
        // aplicacion en la sesion para que el trigger lo copie en la version, y quita
        // una marca de activacion que haya quedado de antes.
        internal const string PrefijoEscritura_704ILR =
            "EXEC sys.sp_set_session_context @key = N'" + ClaveUsuario_704ILR + "', @value = @usuarioCambio; " +
            "EXEC sys.sp_set_session_context @key = N'" + ClaveActivarVersion_704ILR + "', @value = NULL; ";

        internal static void AgregarUsuario_704ILR(SqlCommand cmd_704ILR, string usuario_704ILR) =>
            cmd_704ILR.Parameters.Add("@usuarioCambio", SqlDbType.NVarChar, 50).Value = Usuario_704ILR(usuario_704ILR);

        // Las claves de sesion viven lo que la conexion: se borran al terminar, asi la
        // conexion que vuelve al grupo no le deja el usuario a otra operacion.
        internal static void LimpiarSesion_704ILR(SqlConnection conn_704ILR)
        {
            if (conn_704ILR == null || conn_704ILR.State != ConnectionState.Open) return;
            try
            {
                using (var cmd_704ILR = new SqlCommand(
                    "EXEC sys.sp_set_session_context @key = N'" + ClaveUsuario_704ILR + "', @value = NULL; " +
                    "EXEC sys.sp_set_session_context @key = N'" + ClaveActivarVersion_704ILR + "', @value = NULL;", conn_704ILR))
                    cmd_704ILR.ExecuteNonQuery();
            }
            catch (SqlException) { /* la conexion se descarta igual: no hay nada que proteger */ }
        }

        // Usuario.Servicios_C es NVARCHAR(50); sin usuario, el del sistema.
        private static string Usuario_704ILR(string usuario_704ILR)
        {
            string u_704ILR = string.IsNullOrWhiteSpace(usuario_704ILR) ? "Sistema" : usuario_704ILR.Trim();
            return u_704ILR.Length > 50 ? u_704ILR.Substring(0, 50) : u_704ILR;
        }

        private static BE_Servicio_C_704ILR Map_704ILR(SqlDataReader r_704ILR) => new BE_Servicio_C_704ILR
        {
            Id_704ILR = r_704ILR.GetInt32(0),
            ServicioId_704ILR = r_704ILR.GetInt32(1),
            Fecha_704ILR = r_704ILR.GetDateTime(2),
            Hora_704ILR = r_704ILR.GetTimeSpan(3),
            Nombre_704ILR = r_704ILR.GetString(4),
            Descripcion_704ILR = r_704ILR.IsDBNull(5) ? null : r_704ILR.GetString(5),
            Precio_704ILR = r_704ILR.GetDecimal(6),
            Activo_704ILR = r_704ILR.GetBoolean(7),
            Act_704ILR = r_704ILR.GetBoolean(8),
            Operacion_704ILR = r_704ILR.GetString(9),
            Usuario_704ILR = r_704ILR.GetString(10)
        };
    }
}
