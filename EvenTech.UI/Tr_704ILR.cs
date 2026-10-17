using System;
using System.Collections.Generic;
using System.Windows.Forms;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Helper de traduccion para la UI. Cada control que deba traducirse lleva en
    // su Tag la cadena "T:CLAVE"; AplicarTags recorre el arbol de controles y les
    // asigna el texto del idioma activo. Asi no hay que promover cada label a
    // campo: un solo metodo traduce toda la vista (patron Observer -> ActualizarTextos).
    internal static class Tr_704ILR
    {
        public static string T_704ILR(string clave_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Traducir_704ILR(clave_704ILR);

        // Traduccion con marcadores {n}. Se usa el texto por defecto, que es una
        // constante del codigo y siempre formatea, si la clave falta, si el texto
        // guardado esta mal formado (llave sin cerrar, indice que no existe) o si es
        // desmesurado: un relleno enorme ({0,999999}) armaba un texto de un millon de
        // caracteres y el cuadro de mensaje que lo mostraba dejaba la pantalla casi dos
        // minutos sin responder. Ver GestorDeIdioma_704ILR.Formatear_704ILR.
        public static string F_704ILR(string clave_704ILR, string defecto_704ILR, params object[] args_704ILR)
            => GestorDeIdioma_704ILR.GetInstance_704ILR.Formatear_704ILR(clave_704ILR, defecto_704ILR, args_704ILR);

        // Version y commit en una linea, para el pie de la pantalla de acceso y de la ventana
        // principal: "Versión 2.0.0 · commit 480f5c4". El commit es el que el repositorio
        // muestra en su historial; con el se comprueba que el programa compilado desde el
        // zip entregado es el del repositorio (ver VersionSistema_704ILR).
        public static string TextoVersion_704ILR()
        {
            VersionSistema_704ILR v_704ILR = VersionSistema_704ILR.Actual_704ILR;
            string texto_704ILR = F_704ILR("APP_VERSION", "Versión {0}", v_704ILR.Version_704ILR) + " · ";
            if (v_704ILR.Commit_704ILR == null)
                return texto_704ILR + F_704ILR("APP_SIN_COMMIT", "sin datos del commit");
            texto_704ILR += F_704ILR("APP_COMMIT", "commit {0}", v_704ILR.CommitCorto_704ILR);
            if (v_704ILR.ConCambios_704ILR)
                texto_704ILR += " " + F_704ILR("APP_CON_CAMBIOS", "(compilado con cambios sin commitear)");
            return texto_704ILR;
        }

        // Detalle de "Acerca de": la version, el commit completo y su fecha.
        public static string DetalleVersion_704ILR()
        {
            VersionSistema_704ILR v_704ILR = VersionSistema_704ILR.Actual_704ILR;
            var lineas_704ILR = new List<string>
            {
                "EvenTech",
                F_704ILR("APP_VERSION", "Versión {0}", v_704ILR.Version_704ILR),
                F_704ILR("ACERCA_COMMIT", "Commit: {0}", v_704ILR.Commit_704ILR ?? F_704ILR("APP_SIN_COMMIT", "sin datos del commit"))
            };
            // Fecha con patron fijo y cultura invariante, como el resto de las fechas en pantalla.
            if (v_704ILR.FechaCommit_704ILR.HasValue)
                lineas_704ILR.Add(F_704ILR("ACERCA_FECHA", "Fecha del commit: {0}",
                    v_704ILR.FechaCommit_704ILR.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)));
            if (v_704ILR.ConCambios_704ILR)
                lineas_704ILR.Add(F_704ILR("APP_CON_CAMBIOS", "(compilado con cambios sin commitear)"));
            return string.Join(Environment.NewLine, lineas_704ILR);
        }

        // Texto para el usuario ante una excepcion capturada en una pantalla. Si la causa
        // es la base de datos (una SqlException en la cadena de excepciones) nunca se
        // muestra el texto del motor, que sale en ingles, con el nombre de la base y la
        // cuenta de Windows; el detalle tecnico ya lo asienta la bitacora en cada catch:
        //  * si no se pudo establecer la conexion con la base (red, servidor, tiempo de
        //    espera al conectar, base inexistente, fuera de linea o sin acceso) o la
        //    conexion se perdio, el aviso de base no disponible;
        //  * si la conexion estaba abierta y la base rechazo o no completo la operacion
        //    (una restriccion, un dato que no entra, un bloqueo de otra estacion que agota
        //    el tiempo de espera del comando), el aviso de operacion no completada: decir
        //    que no se pudo acceder a la base con la base en linea confunde al usuario.
        // Cualquier otra excepcion conserva su mensaje, detras del prefijo con su texto por
        // defecto: la red de seguridad de Program_704ILR se suscribe antes de cargar los
        // idiomas (la pantalla de conexion del arranque ya puede mostrar el aviso) y sin el
        // defecto la clave cruda MSG_ERROR_PREFIJO quedaba pegada al mensaje. Se inspecciona
        // por nombre de tipo y de propiedad para no atar la UI al proveedor de datos.
        public static string MensajeExcepcion_704ILR(Exception ex_704ILR)
        {
            Exception sql_704ILR = PrimeraSqlException_704ILR(ex_704ILR);
            if (sql_704ILR != null)
                return SinConexion_704ILR(sql_704ILR)
                    ? F_704ILR("MSG_ERROR_SIN_BASE", "No se pudo acceder a la base de datos. Reintentá o contactate con un administrador.")
                    : F_704ILR("MSG_ERROR_OPERACION", "No se pudo completar la operación. Reintentá o contactate con un administrador.");
            return F_704ILR("MSG_ERROR_PREFIJO", "Error: ") + (ex_704ILR?.Message ?? string.Empty);
        }

        // true si MensajeExcepcion_704ILR informa la excepcion como base de datos no disponible
        // (no se pudo establecer la conexion o se perdio). El login y el alta de cuenta muestran
        // en ese caso su propio aviso; en los demas, el mismo que el resto de las pantallas.
        public static bool EsSinBase_704ILR(Exception ex_704ILR)
        {
            Exception sql_704ILR = PrimeraSqlException_704ILR(ex_704ILR);
            return sql_704ILR != null && SinConexion_704ILR(sql_704ILR);
        }

        // Primera SqlException de la cadena de excepciones (la propia o una interna), o null.
        private static Exception PrimeraSqlException_704ILR(Exception ex_704ILR)
        {
            for (var e_704ILR = ex_704ILR; e_704ILR != null; e_704ILR = e_704ILR.InnerException)
                if (e_704ILR.GetType().FullName == "Microsoft.Data.SqlClient.SqlException")
                    return e_704ILR;
            return null;
        }

        // Errores de SQL Server y del proveedor que indican que la base no esta disponible:
        //  -1, 2, 26, 40, 53 servidor o instancia no encontrados; 64, 1231, 10060, 10061
        //  red (conexion cortada, rechazada, sin ruta o sin respuesta);
        //  4060, 4064 base inexistente o sin acceso; 18452, 18456, 18486, 18487, 18488
        //  inicio de sesion rechazado; 922, 924, 927, 942, 945, 952, 976, 978, 983 base en
        //  recuperacion, en uso exclusivo, restaurandose, fuera de linea, con archivos
        //  inaccesibles, cambiando de estado o no disponible en la replica;
        //  596, 6005, 17142 sesion terminada, servidor apagandose o en pausa.
        // No figuran:
        //  * el -2, tiempo de espera: al conectar es falta de conexion (lo cubre la marca
        //    de apertura), pero con la conexion abierta es un comando que espero de mas,
        //    por ejemplo por un bloqueo de otra estacion, y la base esta en linea;
        //  * 121, 232, 233, 258, 10053, 10054, 10065, 11001 y 11004: el motor usa esos
        //    mismos numeros para errores de una consulta con la base en linea (un INSERT
        //    con mas columnas, un desbordamiento aritmetico, una columna que no admite
        //    NULL). Como errores de red del proveedor llegan al abrir, con severidad 20 o
        //    sin identificador de conexion, y los cubren las otras reglas.
        private static readonly HashSet<int> ErroresSinConexion_704ILR = new HashSet<int>
        {
            -1, 2, 26, 40, 53, 64, 1231, 10060, 10061,
            4060, 4064, 18452, 18456, 18486, 18487, 18488,
            922, 924, 927, 942, 945, 952, 976, 978, 983,
            596, 6005, 17142
        };

        // Severidad 20 o mas: error fatal, el servidor cierra la conexion.
        private const byte SeveridadFatal_704ILR = 20;

        // Marca que DAL_DB_Connection_704ILR.OpenConnection_704ILR deja en Exception.Data
        // cuando la excepcion se produjo al abrir la conexion. La cadena se repite en los dos
        // archivos porque la interfaz no referencia a la capa de datos.
        private const string MarcaApertura_704ILR = "EvenTech.AperturaDeConexion";

        // Sin conexion utilizable: la excepcion se produjo al abrir la conexion (tambien un
        // tiempo de espera al conectar, que trae identificador de conexion), el cliente no
        // llego a identificarla, algun error es de disponibilidad de la base o es fatal y
        // cerro la conexion. Con la conexion abierta, un tiempo de espera del comando o un
        // error del motor es una operacion que no se completo.
        private static bool SinConexion_704ILR(Exception sql_704ILR)
        {
            if (sql_704ILR.Data.Contains(MarcaApertura_704ILR)) return true;
            Type tipo_704ILR = sql_704ILR.GetType();
            if (tipo_704ILR.GetProperty("ClientConnectionId")?.GetValue(sql_704ILR) is Guid id_704ILR && id_704ILR == Guid.Empty)
                return true;
            if (tipo_704ILR.GetProperty("Errors")?.GetValue(sql_704ILR) is System.Collections.IEnumerable errores_704ILR)
                foreach (object error_704ILR in errores_704ILR)
                {
                    if (error_704ILR == null) continue;
                    Type tipoError_704ILR = error_704ILR.GetType();
                    if (tipoError_704ILR.GetProperty("Number")?.GetValue(error_704ILR) is int numero_704ILR && ErroresSinConexion_704ILR.Contains(numero_704ILR))
                        return true;
                    if (tipoError_704ILR.GetProperty("Class")?.GetValue(error_704ILR) is byte severidad_704ILR && severidad_704ILR >= SeveridadFatal_704ILR)
                        return true;
                }
            return tipo_704ILR.GetProperty("Number")?.GetValue(sql_704ILR) is int n_704ILR && ErroresSinConexion_704ILR.Contains(n_704ILR);
        }

        // Traduccion de valores de enumeraciones mostrados al usuario (grillas/combos).
        // La clave se arma por convencion PREFIJO_VALOR para no acoplar el enum a la UI.
        // Un estado que no es ninguno de la tabla de estados (la base se altero por fuera
        // de la aplicacion) se muestra con una leyenda traducida y no con la clave cruda
        // "EST_-1", que no existe.
        public static string Estado_704ILR(EvenTech.BE.EstadoReserva_704ILR e_704ILR)
            => Enum.IsDefined(e_704ILR)
                ? T_704ILR("EST_" + e_704ILR.ToString())
                : F_704ILR("EST_DESCONOCIDO", "(estado desconocido)");
        // Lo mismo con la criticidad de un asiento: Bitacora.Criticidad no tiene una
        // restriccion que la limite a los valores del enum, y un valor alterado por fuera
        // de la aplicacion (0, 7, 255) mostraba la clave cruda "CRIT_7".
        public static string Criticidad_704ILR(EvenTech.BE.CriticidadBitacora_704ILR c_704ILR)
            => Enum.IsDefined(c_704ILR)
                ? T_704ILR("CRIT_" + c_704ILR.ToString().ToUpperInvariant())
                : F_704ILR("CRIT_DESCONOCIDA", "(desconocida)");
        public static string Accion_704ILR(string codigo_704ILR) => T_704ILR("ACC_" + codigo_704ILR);

        // Asigna el Text traducido a todos los controles cuyo Tag sea "T:CLAVE".
        public static void AplicarTags_704ILR(Control root_704ILR)
        {
            foreach (Control c_704ILR in root_704ILR.Controls)
            {
                if (c_704ILR.Tag is string tag_704ILR)
                {
                    // "T:CLAVE": traduce el Text del propio control.
                    if (tag_704ILR.StartsWith("T:"))
                        c_704ILR.Text = T_704ILR(tag_704ILR.Substring(2));
                    // "FIELD:CLAVE": traduce el caption (primer Label hijo) de un Ui.Field.
                    else if (tag_704ILR.StartsWith("FIELD:"))
                    {
                        string clave_704ILR = tag_704ILR.Substring(6);
                        foreach (Control hijo_704ILR in c_704ILR.Controls)
                            if (hijo_704ILR is Label lbl_704ILR) { lbl_704ILR.Text = T_704ILR(clave_704ILR); break; }
                    }
                }
                if (c_704ILR.HasChildren)
                    AplicarTags_704ILR(c_704ILR);
            }
        }
    }
}
