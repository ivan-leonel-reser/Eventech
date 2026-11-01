using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EvenTech.Services
{
    // Configuracion de la conexion a la base, persistida fuera del binario.
    //
    // La cadena de conexion puede contener credenciales, asi que no se guarda en
    // claro: se cifra con DPAPI (ambito usuario) en
    // %APPDATA%\EvenTech\connection.cfg. Es el mismo criterio que CryptoService
    // usa para la clave AES, pero con ambito CurrentUser porque la conexion es
    // una preferencia de quien opera la estacion de trabajo, no de la maquina.
    //
    // Si el archivo no existe, se usa PorDefecto: asi una instalacion nueva
    // arranca sin pedir configuracion cuando la base esta donde se espera.
    public static class ConfiguracionConexion_704ILR
    {
        // Instancia y base esperadas en una instalacion estandar.
        // SQL Server Express es el motor que declara el README y su instancia se
        // llama SQLEXPRESS: apuntar ahi de fabrica hace que el sistema conecte al
        // primer arranque en una instalacion limpia. Si no responde, la aplicacion
        // igual prueba las otras instancias habituales y ofrece configurarla.
        public const string ServidorPorDefecto_704ILR = @"localhost\SQLEXPRESS";
        public const string BaseDatosPorDefecto_704ILR = "EvenTechDB";

        public static string PorDefecto_704ILR => Construir_704ILR(ServidorPorDefecto_704ILR, BaseDatosPorDefecto_704ILR);

        private static readonly object _lock_704ILR = new object();
        private static string _cache_704ILR;

        // Cadena fijada para esta ejecucion por una herramienta de prueba (la prueba
        // automatizada, la captura de pantallas, la carga de los datos de demostracion):
        // vale solo dentro del proceso, no se escribe en ningun archivo y no la pisa
        // Borrar. Asi las pruebas corren contra una base exclusiva sin tocar la
        // configuracion del puesto. La aplicacion nunca la usa.
        private static string _deEstaEjecucion_704ILR;

        public static void UsarEnEstaEjecucion_704ILR(string connectionString_704ILR)
        {
            if (string.IsNullOrWhiteSpace(connectionString_704ILR))
                throw new ArgumentException("La cadena de conexion no puede estar vacia.", nameof(connectionString_704ILR));
            lock (_lock_704ILR) { _deEstaEjecucion_704ILR = connectionString_704ILR; }
        }

        private static string Dir_704ILR =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EvenTech");
        private static string Archivo_704ILR => Path.Combine(Dir_704ILR, "connection.cfg");

        public static bool EstaConfigurada_704ILR => File.Exists(Archivo_704ILR);

        // Cadena vigente: la de esta ejecucion si una herramienta de prueba la fijo; si no,
        // la guardada si existe y la de fabrica si no.
        public static string Actual_704ILR
        {
            get
            {
                if (_deEstaEjecucion_704ILR != null) return _deEstaEjecucion_704ILR;
                if (_cache_704ILR != null) return _cache_704ILR;
                lock (_lock_704ILR)
                {
                    _cache_704ILR ??= Leer_704ILR() ?? PorDefecto_704ILR;
                    return _cache_704ILR;
                }
            }
        }

        // Arma una cadena estandar (autenticacion integrada de Windows: el sistema
        // no maneja usuario/clave de SQL, de modo que no hay credenciales que
        // custodiar mas alla de la sesion del propio usuario).
        //
        // Instancia y base son obligatorias. Una entrada en blanco se rechaza: si se
        // reemplazara por los valores de fabrica, la prueba y el guardado usarian una
        // base distinta de la que muestra la pantalla. Los valores se escriben con el
        // constructor de cadenas de ADO.NET, que los entrecomilla cuando traen ';',
        // '=' o comillas: un nombre como "A;Initial Catalog=B" queda como un unico
        // nombre de base y no puede agregar ni pisar otras claves de la cadena.
        public static string Construir_704ILR(string servidor_704ILR, string baseDatos_704ILR)
        {
            if (string.IsNullOrWhiteSpace(servidor_704ILR))
                throw new ArgumentException("La instancia de SQL Server no puede estar vacía.", nameof(servidor_704ILR));
            if (string.IsNullOrWhiteSpace(baseDatos_704ILR))
                throw new ArgumentException("El nombre de la base de datos no puede estar vacío.", nameof(baseDatos_704ILR));

            var cadena_704ILR = new DbConnectionStringBuilder();
            cadena_704ILR["Data Source"] = servidor_704ILR.Trim();
            cadena_704ILR["Initial Catalog"] = baseDatos_704ILR.Trim();
            cadena_704ILR["Integrated Security"] = "True";
            cadena_704ILR["TrustServerCertificate"] = "True";
            return cadena_704ILR.ConnectionString;
        }

        // Nombres alternativos que el cliente de SQL Server acepta para las dos
        // claves que se muestran en la pantalla de configuracion.
        private static readonly string[] ClavesServidor_704ILR = { "Data Source", "Server", "Address", "Addr", "Network Address" };
        private static readonly string[] ClavesBase_704ILR = { "Initial Catalog", "Database" };

        // Extrae un valor de la cadena vigente (para precargar el formulario de
        // configuracion sin acoplar la UI al formato de la cadena). Se interpreta
        // como lo hace el cliente de SQL Server: respeta los valores entrecomillados y,
        // entre una clave y sus sinonimos ("Server" es "Data Source", "Database" es
        // "Initial Catalog"), vale la que aparece ULTIMA en la cadena, que es la que
        // usa la conexion. Asi se muestran la instancia y la base a las que realmente
        // conecta, tambien con una cadena guardada por una version anterior.
        public static string ValorDe_704ILR(string clave_704ILR, string porDefecto_704ILR)
        {
            string cadena_704ILR = Actual_704ILR ?? "";
            try
            {
                _ = new DbConnectionStringBuilder { ConnectionString = cadena_704ILR };
            }
            catch (ArgumentException)
            {
                return porDefecto_704ILR;   // cadena mal formada: se precarga el valor de fabrica
            }

            string[] claves_704ILR =
                Array.Exists(ClavesServidor_704ILR, k_704ILR => k_704ILR.Equals(clave_704ILR, StringComparison.OrdinalIgnoreCase)) ? ClavesServidor_704ILR :
                Array.Exists(ClavesBase_704ILR, k_704ILR => k_704ILR.Equals(clave_704ILR, StringComparison.OrdinalIgnoreCase)) ? ClavesBase_704ILR :
                new[] { clave_704ILR };

            bool hallada_704ILR = false;
            string valor_704ILR = null;
            foreach (KeyValuePair<string, string> par_704ILR in Pares_704ILR(cadena_704ILR))
            {
                if (!Array.Exists(claves_704ILR, k_704ILR => k_704ILR.Equals(par_704ILR.Key, StringComparison.OrdinalIgnoreCase))) continue;
                hallada_704ILR = true;
                valor_704ILR = par_704ILR.Value;     // gana la ultima aparicion
            }
            // Una clave sin valor ("Database=") deja el valor por defecto del cliente: vacio.
            return hallada_704ILR ? (valor_704ILR ?? "") : porDefecto_704ILR;
        }

        // Pares clave=valor de una cadena valida, EN EL ORDEN en que aparecen.
        // DbConnectionStringBuilder separa y desentrecomilla igual que el cliente de
        // SQL Server, pero guarda las claves en un diccionario y no conserva su orden.
        // Para recuperarlo se lee la cadena de a un par: cada tramo termina en el
        // primer ';' hasta el cual lo leido es un par completo. Un ';' que forma parte
        // de un valor entrecomillado (o de un nombre de clave) deja el tramo abierto,
        // el constructor lo rechaza y la lectura sigue hasta el ';' siguiente.
        private static List<KeyValuePair<string, string>> Pares_704ILR(string cadena_704ILR)
        {
            var pares_704ILR = new List<KeyValuePair<string, string>>();
            int inicio_704ILR = 0;
            for (int fin_704ILR = 0; fin_704ILR <= cadena_704ILR.Length; fin_704ILR++)
            {
                if (fin_704ILR < cadena_704ILR.Length && cadena_704ILR[fin_704ILR] != ';') continue;
                string tramo_704ILR = cadena_704ILR.Substring(inicio_704ILR, fin_704ILR - inicio_704ILR);
                if (!LeerPar_704ILR(tramo_704ILR, out string clave_704ILR, out string valor_704ILR)) continue;
                if (clave_704ILR != null) pares_704ILR.Add(new KeyValuePair<string, string>(clave_704ILR, valor_704ILR));
                inicio_704ILR = fin_704ILR + 1;
            }
            return pares_704ILR;
        }

        // Lee un tramo como un unico par; false si el tramo no esta cerrado. Un tramo
        // vacio no aporta clave. "Clave=" aporta la clave con valor nulo: el
        // constructor la descarta, asi que su nombre se recupera completando un valor.
        private static bool LeerPar_704ILR(string tramo_704ILR, out string clave_704ILR, out string valor_704ILR)
        {
            clave_704ILR = null;
            valor_704ILR = null;
            DbConnectionStringBuilder par_704ILR;
            try
            {
                par_704ILR = new DbConnectionStringBuilder { ConnectionString = tramo_704ILR };
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (par_704ILR.Count > 1) return false;
            foreach (string k_704ILR in par_704ILR.Keys)
            {
                clave_704ILR = k_704ILR;
                valor_704ILR = Convert.ToString(par_704ILR[k_704ILR]);
            }
            if (clave_704ILR != null || tramo_704ILR.Trim().Length == 0) return true;

            try
            {
                foreach (string k_704ILR in new DbConnectionStringBuilder { ConnectionString = tramo_704ILR + "x" }.Keys)
                    clave_704ILR = k_704ILR;
            }
            catch (ArgumentException) { /* no es una clave sin valor: el tramo no aporta clave */ }
            return true;
        }

        public static string ServidorActual_704ILR => ValorDe_704ILR("Data Source", ServidorPorDefecto_704ILR);
        public static string BaseDatosActual_704ILR => ValorDe_704ILR("Initial Catalog", BaseDatosPorDefecto_704ILR);

        public static void Guardar_704ILR(string connectionString_704ILR)
        {
            if (string.IsNullOrWhiteSpace(connectionString_704ILR))
                throw new ArgumentException("La cadena de conexion no puede estar vacia.", nameof(connectionString_704ILR));

            lock (_lock_704ILR)
            {
                Directory.CreateDirectory(Dir_704ILR);
                byte[] datos_704ILR = Encoding.UTF8.GetBytes(connectionString_704ILR);
                File.WriteAllBytes(Archivo_704ILR, ProtectedData.Protect(datos_704ILR, null, DataProtectionScope.CurrentUser));
                _cache_704ILR = connectionString_704ILR;
            }
        }

        // Borra la configuracion guardada: la proxima lectura vuelve a la de fabrica.
        public static void Borrar_704ILR()
        {
            lock (_lock_704ILR)
            {
                try { if (File.Exists(Archivo_704ILR)) File.Delete(Archivo_704ILR); }
                catch { /* si no se puede borrar, la cadena guardada sigue vigente */ }
                _cache_704ILR = null;
            }
        }

        private static string Leer_704ILR()
        {
            try
            {
                if (!File.Exists(Archivo_704ILR)) return null;
                byte[] plano_704ILR = ProtectedData.Unprotect(File.ReadAllBytes(Archivo_704ILR), null, DataProtectionScope.CurrentUser);
                string cs_704ILR = Encoding.UTF8.GetString(plano_704ILR);
                return string.IsNullOrWhiteSpace(cs_704ILR) ? null : cs_704ILR;
            }
            catch (CryptographicException)
            {
                // Archivo de otro usuario o corrupto: se ignora y se cae a la de
                // fabrica, de modo que la app siempre tenga por donde intentar.
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // Permisos denegados sobre el archivo (no deriva de IOException). El
                // proposito de esta clase es ofrecer configurar la conexion, no abortar
                // el arranque: se cae a la cadena de fabrica como en los demas casos.
                return null;
            }
        }
    }
}
