using System;
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using EvenTech.BE;

namespace EvenTech.DAL
{
    // Respaldo y restauracion de la base del sistema con las sentencias de SQL Server
    // (BACKUP / RESTORE). Quien escribe y lee el archivo .bak es la cuenta del servicio de
    // SQL Server, no la de quien usa el sistema: la carpeta tiene que darle acceso a esa
    // cuenta (lo resuelve BLL_Respaldo_704ILR).
    //
    // Todo se ejecuta conectado a master, en una conexion propia y sin pool: la base se
    // pone en uso exclusivo para restaurarla y ninguna conexion del sistema puede quedar
    // abierta sobre ella.
    public static class DAL_Respaldo_704ILR
    {
        // Un respaldo o una restauracion de una base de demostracion demoran segundos; el
        // limite es holgado para un equipo lento.
        private const int Espera_704ILR = 600;

        public class DatosInstancia_704ILR
        {
            public string Equipo_704ILR { get; set; }
            // Nombre de la instancia (SQLEXPRESS); null en la instancia predeterminada.
            public string Instancia_704ILR { get; set; }
            public int VersionMayor_704ILR { get; set; }
        }

        // Base de la conexion vigente (la que respalda y restaura el sistema).
        public static string BaseActual_704ILR()
            => new SqlConnectionStringBuilder(DAL_DB_Connection_704ILR.ConnectionString_704ILR).InitialCatalog;

        private static string Nombre_704ILR(string baseDatos_704ILR) => "[" + baseDatos_704ILR.Replace("]", "]]") + "]";

        private static SqlConnection AbrirMaster_704ILR()
        {
            var cadena_704ILR = new SqlConnectionStringBuilder(DAL_DB_Connection_704ILR.ConnectionString_704ILR)
            {
                InitialCatalog = "master",
                Pooling = false
            };
            var cn_704ILR = new SqlConnection(cadena_704ILR.ConnectionString);
            cn_704ILR.Open();
            return cn_704ILR;
        }

        private static SqlCommand Comando_704ILR(SqlConnection cn_704ILR, string sql_704ILR)
            => new SqlCommand(sql_704ILR, cn_704ILR) { CommandTimeout = Espera_704ILR };

        public static DatosInstancia_704ILR Instancia_704ILR()
        {
            using (var cn_704ILR = AbrirMaster_704ILR())
            using (var cmd_704ILR = Comando_704ILR(cn_704ILR,
                "SELECT CAST(SERVERPROPERTY('ComputerNamePhysicalNetBIOS') AS NVARCHAR(128)), " +
                "CAST(SERVERPROPERTY('InstanceName') AS NVARCHAR(128)), " +
                "CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(40))"))
            using (var rd_704ILR = cmd_704ILR.ExecuteReader())
            {
                rd_704ILR.Read();
                string version_704ILR = rd_704ILR.IsDBNull(2) ? "0" : rd_704ILR.GetString(2);
                int.TryParse(version_704ILR.Split('.')[0], NumberStyles.None, CultureInfo.InvariantCulture, out int mayor_704ILR);
                return new DatosInstancia_704ILR
                {
                    Equipo_704ILR = rd_704ILR.IsDBNull(0) ? null : rd_704ILR.GetString(0),
                    Instancia_704ILR = rd_704ILR.IsDBNull(1) ? null : rd_704ILR.GetString(1),
                    VersionMayor_704ILR = mayor_704ILR
                };
            }
        }

        // Respaldo completo de la base, sin alterar la cadena de respaldos de quien
        // administre la instancia (COPY_ONLY), con suma de verificacion, y verificado
        // despues de escribirlo. Pisa el archivo si existiera.
        public static void Respaldar_704ILR(string ruta_704ILR, string nombre_704ILR, string descripcion_704ILR)
        {
            string baseDatos_704ILR = BaseActual_704ILR();
            using (var cn_704ILR = AbrirMaster_704ILR())
            {
                using (var cmd_704ILR = Comando_704ILR(cn_704ILR,
                    "BACKUP DATABASE " + Nombre_704ILR(baseDatos_704ILR) +
                    " TO DISK = @ruta WITH COPY_ONLY, CHECKSUM, INIT, FORMAT, NAME = @nombre, DESCRIPTION = @descripcion"))
                {
                    cmd_704ILR.Parameters.Add("@ruta", SqlDbType.NVarChar, 400).Value = ruta_704ILR;
                    cmd_704ILR.Parameters.Add("@nombre", SqlDbType.NVarChar, 128).Value = nombre_704ILR ?? string.Empty;
                    cmd_704ILR.Parameters.Add("@descripcion", SqlDbType.NVarChar, 255).Value = descripcion_704ILR ?? string.Empty;
                    cmd_704ILR.ExecuteNonQuery();
                }
                using (var cmd_704ILR = Comando_704ILR(cn_704ILR, "RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM"))
                {
                    cmd_704ILR.Parameters.Add("@ruta", SqlDbType.NVarChar, 400).Value = ruta_704ILR;
                    cmd_704ILR.ExecuteNonQuery();
                }
            }
        }

        // Cabecera del respaldo (RESTORE HEADERONLY): el primer conjunto del archivo.
        // Devuelve null si el archivo no tiene ningun conjunto de respaldo.
        public static BE_Respaldo_704ILR LeerCabecera_704ILR(string ruta_704ILR)
        {
            using (var cn_704ILR = AbrirMaster_704ILR())
            using (var cmd_704ILR = Comando_704ILR(cn_704ILR, "RESTORE HEADERONLY FROM DISK = @ruta"))
            {
                cmd_704ILR.Parameters.Add("@ruta", SqlDbType.NVarChar, 400).Value = ruta_704ILR;
                using (var rd_704ILR = cmd_704ILR.ExecuteReader())
                {
                    if (!rd_704ILR.Read()) return null;
                    return new BE_Respaldo_704ILR
                    {
                        Ruta_704ILR = ruta_704ILR,
                        Nombre_704ILR = Texto_704ILR(rd_704ILR, "BackupName"),
                        Motivo_704ILR = Texto_704ILR(rd_704ILR, "BackupDescription"),
                        BaseOrigen_704ILR = Texto_704ILR(rd_704ILR, "DatabaseName"),
                        Tipo_704ILR = Convert.ToInt32(rd_704ILR["BackupType"], CultureInfo.InvariantCulture),
                        VersionMotor_704ILR = Convert.ToInt32(rd_704ILR["SoftwareVersionMajor"], CultureInfo.InvariantCulture),
                        Fecha_704ILR = rd_704ILR["BackupFinishDate"] is DateTime fin_704ILR ? fin_704ILR : DateTime.MinValue,
                        Legible_704ILR = true
                    };
                }
            }
        }

        private static string Texto_704ILR(SqlDataReader rd_704ILR, string columna_704ILR)
            => rd_704ILR[columna_704ILR] is string s_704ILR ? s_704ILR : null;

        // Restaura el respaldo sobre la base del sistema, en sus mismos archivos: la base
        // queda exactamente como estaba al tomarse el respaldo. Mientras tanto la base esta
        // en uso exclusivo (se desconectan las demas sesiones) y vuelve al uso normal
        // aunque la restauracion falle. Antes se vacia el pool de conexiones del sistema.
        public static void Restaurar_704ILR(string ruta_704ILR)
        {
            string baseDatos_704ILR = BaseActual_704ILR();
            SqlConnection.ClearAllPools();
            using (var cn_704ILR = AbrirMaster_704ILR())
            {
                string logicoDatos_704ILR = null, logicoRegistro_704ILR = null;
                using (var cmd_704ILR = Comando_704ILR(cn_704ILR, "RESTORE FILELISTONLY FROM DISK = @ruta"))
                {
                    cmd_704ILR.Parameters.Add("@ruta", SqlDbType.NVarChar, 400).Value = ruta_704ILR;
                    using (var rd_704ILR = cmd_704ILR.ExecuteReader())
                        while (rd_704ILR.Read())
                        {
                            string tipo_704ILR = Convert.ToString(rd_704ILR["Type"], CultureInfo.InvariantCulture);
                            if (tipo_704ILR == "D" && logicoDatos_704ILR == null) logicoDatos_704ILR = (string)rd_704ILR["LogicalName"];
                            if (tipo_704ILR == "L" && logicoRegistro_704ILR == null) logicoRegistro_704ILR = (string)rd_704ILR["LogicalName"];
                        }
                }

                string datos_704ILR = null, registro_704ILR = null;
                using (var cmd_704ILR = Comando_704ILR(cn_704ILR,
                    "SELECT type_desc, physical_name FROM sys.master_files WHERE database_id = DB_ID(@base) ORDER BY file_id"))
                {
                    cmd_704ILR.Parameters.Add("@base", SqlDbType.NVarChar, 128).Value = baseDatos_704ILR;
                    using (var rd_704ILR = cmd_704ILR.ExecuteReader())
                        while (rd_704ILR.Read())
                        {
                            string tipo_704ILR = rd_704ILR.GetString(0);
                            if (tipo_704ILR == "ROWS" && datos_704ILR == null) datos_704ILR = rd_704ILR.GetString(1);
                            if (tipo_704ILR == "LOG" && registro_704ILR == null) registro_704ILR = rd_704ILR.GetString(1);
                        }
                }
                if (logicoDatos_704ILR == null || logicoRegistro_704ILR == null || datos_704ILR == null || registro_704ILR == null)
                    throw new InvalidOperationException("No se pudieron leer los archivos de la base o del respaldo.");

                try
                {
                    using (var cmd_704ILR = Comando_704ILR(cn_704ILR,
                        "ALTER DATABASE " + Nombre_704ILR(baseDatos_704ILR) + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                        "RESTORE DATABASE " + Nombre_704ILR(baseDatos_704ILR) + " FROM DISK = @ruta " +
                        "WITH REPLACE, RECOVERY, MOVE @ld TO @d, MOVE @ll TO @l;"))
                    {
                        cmd_704ILR.Parameters.Add("@ruta", SqlDbType.NVarChar, 400).Value = ruta_704ILR;
                        cmd_704ILR.Parameters.Add("@ld", SqlDbType.NVarChar, 128).Value = logicoDatos_704ILR;
                        cmd_704ILR.Parameters.Add("@d", SqlDbType.NVarChar, 400).Value = datos_704ILR;
                        cmd_704ILR.Parameters.Add("@ll", SqlDbType.NVarChar, 128).Value = logicoRegistro_704ILR;
                        cmd_704ILR.Parameters.Add("@l", SqlDbType.NVarChar, 400).Value = registro_704ILR;
                        cmd_704ILR.ExecuteNonQuery();
                    }
                }
                finally
                {
                    try
                    {
                        using (var cmd_704ILR = Comando_704ILR(cn_704ILR,
                            "IF DB_ID(@base) IS NOT NULL AND CAST(DATABASEPROPERTYEX(@base, 'UserAccess') AS NVARCHAR(20)) <> N'MULTI_USER' " +
                            "ALTER DATABASE " + Nombre_704ILR(baseDatos_704ILR) + " SET MULTI_USER;"))
                        {
                            cmd_704ILR.Parameters.Add("@base", SqlDbType.NVarChar, 128).Value = baseDatos_704ILR;
                            cmd_704ILR.ExecuteNonQuery();
                        }
                    }
                    catch (SqlException) { /* la base quedo como la dejo la restauracion: lo informa quien llama */ }
                }
            }
            SqlConnection.ClearAllPools();
        }
    }
}
