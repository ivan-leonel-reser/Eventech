using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum IdiomaResult_704ILR
    {
        Success_704ILR,
        CodigoInvalido_704ILR,
        NombreInvalido_704ILR,
        CodigoDuplicado_704ILR,
        NombreDuplicado_704ILR      // otro idioma ya usa ese nombre: en el selector serian indistinguibles
    }

    // Resultado de guardar un lote de traducciones desde el editor de idiomas.
    public enum TraduccionResult_704ILR
    {
        Success_704ILR,
        TextoVacio_704ILR,          // un texto quedaba vacio, solo con espacios o con caracteres que no se ven
        PlantillaInvalida_704ILR,   // llaves sin cerrar o marcadores que no coinciden con los de la clave
        FiltroInvalido_704ILR       // CMP_FILTER que no es un filtro del cuadro "Guardar como" (descripcion|patron)
    }

    // Orquesta la carga de idiomas/traducciones desde la base hacia el
    // GestorDeIdioma (que vive en Services y no accede a datos). Se invoca una
    // vez al iniciar la aplicacion y cada vez que el admin modifica idiomas.
    public static class BLL_Idioma_704ILR
    {
        private const string CodigoPorDefecto_704ILR = "ES";

        // Ancho real de Idiomas.Nombre: la DAL recortaba en silencio un nombre mas largo.
        private const int MaxNombre_704ILR = 50;

        // Codigo de idioma: 1 a 5 caracteres (ancho de Idiomas.Codigo), letras
        // ASCII, digitos o guion, empezando por letra ("ES", "PT-BR"). Se valida ya
        // normalizado a mayusculas.
        private static readonly Regex CodigoValido_704ILR =
            new Regex(@"^[A-Z][A-Z0-9-]{0,4}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Falla de la ultima carga de idiomas y traducciones, o null si la ultima carga se
        // completo. Mientras haya una falla pendiente, las pantallas usan los textos que el
        // gestor tenga (ninguno, si fallo la carga del arranque): la interfaz reintenta la
        // carga con ReintentarCarga_704ILR antes de mostrar sus leyendas.
        private static Exception _fallaCarga_704ILR;

        // Hay una carga de idiomas que fallo y todavia no se completo.
        public static bool CargaPendiente_704ILR => _fallaCarga_704ILR != null;

        // Lee todos los idiomas con sus traducciones y recien entonces los pasa al gestor:
        // una falla a mitad de la lectura (una tabla bloqueada por otra estacion, la base
        // que se cae) no deja idiomas cargados sin sus textos, que el selector ofrecia sin
        // que elegirlos cambiara nada. La falla queda pendiente de reintento y se propaga.
        public static void Inicializar_704ILR()
        {
            List<BE_Idioma_704ILR> idiomas_704ILR;
            var tablas_704ILR = new List<KeyValuePair<string, Dictionary<string, string>>>();
            try
            {
                idiomas_704ILR = DAL_Idioma_704ILR.GetIdiomas_704ILR();
                foreach (var idioma_704ILR in idiomas_704ILR)
                    tablas_704ILR.Add(new KeyValuePair<string, Dictionary<string, string>>(
                        idioma_704ILR.Codigo_704ILR, DAL_Idioma_704ILR.GetTraducciones_704ILR(idioma_704ILR.Id_704ILR)));
            }
            catch (Exception ex_704ILR)
            {
                // Se conserva la primera falla: es la causa que se asienta al recuperarse.
                if (_fallaCarga_704ILR == null) _fallaCarga_704ILR = ex_704ILR;
                throw;
            }

            var gestor_704ILR = GestorDeIdioma_704ILR.GetInstance_704ILR;
            gestor_704ILR.CargarIdiomas_704ILR(idiomas_704ILR);
            foreach (var tabla_704ILR in tablas_704ILR)
                gestor_704ILR.CargarTraducciones_704ILR(tabla_704ILR.Key, tabla_704ILR.Value);
            _fallaCarga_704ILR = null;
        }

        // Reintenta la carga si la ultima fallo. Devuelve true si el gestor quedo con la
        // carga completa (o no habia nada pendiente). Nunca lanza: un reintento fallido se
        // asienta con su causa, y uno que prospera asienta la recuperacion con la causa de
        // la falla original, que al arrancar puede no haberse podido asentar (la base
        // estaba caida). momento_704ILR dice donde se reintento, para el asiento.
        public static bool ReintentarCarga_704ILR(string momento_704ILR)
        {
            Exception falla_704ILR = _fallaCarga_704ILR;
            if (falla_704ILR == null) return true;
            try
            {
                Inicializar_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Idiomas", "Reintento de la carga de idiomas (" + momento_704ILR + ")");
                return false;
            }
            BLL_Bitacora_704ILR.Registrar_704ILR("Idiomas", "Carga de idiomas recuperada", CriticidadBitacora_704ILR.Advertencia,
                $"La carga de idiomas y traducciones habia fallado ({falla_704ILR.Message}) y se completo al reintentarla {momento_704ILR}.");
            return true;
        }

        public static List<BE_Idioma_704ILR> GetIdiomas_704ILR() => DAL_Idioma_704ILR.GetIdiomas_704ILR();

        public static Dictionary<string, string> GetTraducciones_704ILR(int idiomaId_704ILR) => DAL_Idioma_704ILR.GetTraducciones_704ILR(idiomaId_704ILR);

        // Alta de un idioma nuevo desde la interfaz. Inicializa sus leyendas
        // copiando las claves del idioma por defecto como punto de partida (el
        // admin luego puede editar cada texto). Recarga el gestor al terminar.
        // Un alta rechazada queda en bitacora con el motivo, como los demas rechazos.
        public static IdiomaResult_704ILR CrearIdioma_704ILR(string codigo_704ILR, string nombre_704ILR, out int nuevoId_704ILR)
        {
            nuevoId_704ILR = 0;
            string codigoNorm_704ILR = (codigo_704ILR ?? string.Empty).Trim().ToUpperInvariant();
            // El nombre se guarda y se compara tal como se ve: sin caracteres invisibles
            // (espacio de ancho cero, guion blando, controles), con cada tramo de espacios
            // o rellenos como un espacio comun y en forma canonica. Antes solo se
            // recortaban los bordes y un "English" seguido de un espacio de ancho cero
            // entraba como un segundo "English".
            string nombreNorm_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(nombre_704ILR);

            IdiomaResult_704ILR validacion_704ILR = IdiomaResult_704ILR.Success_704ILR;
            if (!CodigoValido_704ILR.IsMatch(codigoNorm_704ILR))
                validacion_704ILR = IdiomaResult_704ILR.CodigoInvalido_704ILR;
            // Un nombre que no se ve (invisibles o rellenos que se dibujan en blanco)
            // dejaba una entrada vacia en el selector de idioma.
            else if (GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(nombreNorm_704ILR) || nombreNorm_704ILR.Length > MaxNombre_704ILR)
                validacion_704ILR = IdiomaResult_704ILR.NombreInvalido_704ILR;
            else if (DAL_Idioma_704ILR.ExistsCodigo_704ILR(codigoNorm_704ILR))
                validacion_704ILR = IdiomaResult_704ILR.CodigoDuplicado_704ILR;
            // El selector de idioma solo muestra el nombre: dos nombres que se ven
            // iguales no se distinguen. Se comparan las formas visibles, sin mayusculas y
            // en compatibilidad (las letras de ancho completo valen como las comunes).
            else
            {
                string claveNombre_704ILR = ClaveNombre_704ILR(nombreNorm_704ILR);
                if (DAL_Idioma_704ILR.GetIdiomas_704ILR().Any(i_704ILR =>
                        string.Equals(ClaveNombre_704ILR(i_704ILR.Nombre_704ILR), claveNombre_704ILR, StringComparison.OrdinalIgnoreCase)))
                    validacion_704ILR = IdiomaResult_704ILR.NombreDuplicado_704ILR;
            }

            if (validacion_704ILR != IdiomaResult_704ILR.Success_704ILR)
                return Rechazar_704ILR(validacion_704ILR, nombreNorm_704ILR, codigoNorm_704ILR);

            // Claves del idioma por defecto: se leen antes de abrir la transaccion.
            var idiomas_704ILR = DAL_Idioma_704ILR.GetIdiomas_704ILR();
            var baseIdioma_704ILR = idiomas_704ILR.FirstOrDefault(i_704ILR => i_704ILR.Codigo_704ILR == CodigoPorDefecto_704ILR) ?? idiomas_704ILR.FirstOrDefault();
            Dictionary<string, string> textosBase_704ILR = baseIdioma_704ILR == null
                ? new Dictionary<string, string>()
                : DAL_Idioma_704ILR.GetTraducciones_704ILR(baseIdioma_704ILR.Id_704ILR);

            // El idioma y la copia de sus leyendas van en UNA transaccion: si falla una
            // insercion a mitad de la copia no queda un idioma incompleto que, por
            // tener el codigo tomado, ya no se podria volver a dar de alta.
            bool idiomaInsertado_704ILR = false;
            try
            {
                using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                {
                    SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                    using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                    {
                        nuevoId_704ILR = DAL_Idioma_704ILR.InsertIdioma_704ILR(codigoNorm_704ILR, nombreNorm_704ILR, conn_704ILR, tx_704ILR);
                        idiomaInsertado_704ILR = true;
                        foreach (var kv_704ILR in textosBase_704ILR)
                            DAL_Idioma_704ILR.UpsertTraduccion_704ILR(nuevoId_704ILR, kv_704ILR.Key, kv_704ILR.Value, conn_704ILR, tx_704ILR);
                        tx_704ILR.Commit();
                    }
                }
            }
            catch (SqlException ex_704ILR) when (!idiomaInsertado_704ILR && EsChoqueDeUnicidad_704ILR(ex_704ILR))
            {
                // Otra estacion dio de alta el mismo codigo entre la verificacion y el
                // INSERT: UQ_Idiomas_Codigo es la red de seguridad, la transaccion ya se
                // deshizo y la respuesta es la del chequeo previo, no un error de la base.
                nuevoId_704ILR = 0;
                return Rechazar_704ILR(IdiomaResult_704ILR.CodigoDuplicado_704ILR, nombreNorm_704ILR, codigoNorm_704ILR);
            }
            catch
            {
                nuevoId_704ILR = 0;
                throw;
            }

            Inicializar_704ILR();
            BLL_Bitacora_704ILR.Registrar_704ILR("Idiomas", "Alta de idioma", CriticidadBitacora_704ILR.Info,
                $"Idioma '{nombreNorm_704ILR}' ({codigoNorm_704ILR}) creado");
            return IdiomaResult_704ILR.Success_704ILR;
        }

        private static string MotivoRechazo_704ILR(IdiomaResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case IdiomaResult_704ILR.CodigoInvalido_704ILR:  return "codigo invalido (1 a 5 letras, digitos o guion, empezando por letra)";
                case IdiomaResult_704ILR.NombreInvalido_704ILR:  return "nombre vacio, invisible o de mas de " + MaxNombre_704ILR + " caracteres";
                case IdiomaResult_704ILR.CodigoDuplicado_704ILR: return "ya existe un idioma con ese codigo";
                case IdiomaResult_704ILR.NombreDuplicado_704ILR: return "ya existe un idioma con ese nombre";
                default:                                         return r_704ILR.ToString();
            }
        }

        private static string MotivoRechazo_704ILR(TraduccionResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case TraduccionResult_704ILR.TextoVacio_704ILR:        return "quedaba vacia.";
                case TraduccionResult_704ILR.PlantillaInvalida_704ILR: return "tiene llaves sin cerrar o marcadores que no coinciden con los de la clave.";
                case TraduccionResult_704ILR.FiltroInvalido_704ILR:    return "no es un filtro de archivos (descripcion|patron) del cuadro Guardar como.";
                default:                                              return r_704ILR.ToString();
            }
        }

        // Asienta el rechazo de un alta con su motivo y devuelve el resultado.
        private static IdiomaResult_704ILR Rechazar_704ILR(IdiomaResult_704ILR r_704ILR, string nombre_704ILR, string codigo_704ILR)
        {
            BLL_Bitacora_704ILR.Registrar_704ILR("Idiomas", "Idioma rechazado", CriticidadBitacora_704ILR.Advertencia,
                $"Alta de idioma '{nombre_704ILR}' ({codigo_704ILR}) rechazada: {MotivoRechazo_704ILR(r_704ILR)}");
            return r_704ILR;
        }

        // Forma de un nombre para compararlo con los demas: la visible, en
        // compatibilidad (las letras de ancho completo valen como las comunes).
        private static string ClaveNombre_704ILR(string nombre_704ILR)
        {
            string visible_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(nombre_704ILR);
            try { return visible_704ILR.Normalize(NormalizationForm.FormKC); }
            catch (ArgumentException) { return visible_704ILR; }
        }

        // 2627: violacion de una restriccion UNIQUE; 2601: fila duplicada en un indice
        // unico. En Idiomas la unica restriccion unica es UQ_Idiomas_Codigo.
        private static bool EsChoqueDeUnicidad_704ILR(SqlException ex_704ILR)
            => ex_704ILR.Number == 2627 || ex_704ILR.Number == 2601;

        // ------------------------------------------------------------------
        // Validacion de plantillas
        // ------------------------------------------------------------------

        // Ninguna clave usa mas de cinco argumentos (ver MarcadoresPorClave_704ILR): un
        // indice de tres cifras no es de ninguna clave y string.Format necesitaria un
        // arreglo de ese tamano para probarlo.
        private const int MaxIndice_704ILR = 99;

        // Marcador de una plantilla compuesta: {indice[,alineacion][:formato]}. Solo
        // digitos ASCII: un digito de otro alfabeto no es un indice para string.Format.
        private static readonly Regex Marcador_704ILR = new Regex(
            @"\{([0-9]+)[ ]*(?:,[ ]*(-?[0-9]+)[ ]*)?(?::([^{}]*))?\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Marcadores que el codigo le pasa a cada clave que se formatea con
        // argumentos (Tr_704ILR.F_704ILR, string.Format y los respaldos de Services
        // y DAL), con el formato del texto de fabrica. Es la referencia INMUTABLE de
        // la validacion: no depende de lo que el administrador haya guardado en el
        // idioma por defecto. Una clave que no figura aca no lleva marcadores, asi que
        // TODA clave nueva que se formatee con argumentos se agrega a esta lista: si
        // falta, el editor rechaza cualquier texto suyo que conserve el marcador.
        private static readonly Dictionary<string, string> MarcadoresPorClave_704ILR = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "AGE_EVENTO",                 "{0} {1} {2}" },    // fecha del evento, salon, numero de reserva
            { "ALERT_DVH_FALTANTE",         "{0}" },            // numero de reserva sin DV horizontal almacenado
            { "ALERT_DVH_NO_COINCIDE",      "{0}" },            // numero de reserva con el DV horizontal alterado
            { "ALERT_ESTADO_FUERA_DOMINIO", "{0}" },            // numero de reserva con el estado fuera del dominio
            { "ALERT_NO_VERIFICADA",        "{0}" },            // causa de la falla de la verificacion
            { "ALERT_PAGO_DVH_FALTANTE",    "{0} {1}" },        // numero de pago sin DV horizontal, numero de su reserva
            { "ALERT_PAGO_DVH_NO_COINCIDE", "{0} {1}" },        // numero de pago con el DV horizontal alterado, numero de su reserva
            { "ASG_QUITAR_CONF",            "{0}" },            // empleado que se quita del equipo
            { "ASG_RESUMEN",                "{0} {1} {2} {3}" },// asignados, confirmados, pendientes, rechazados
            { "ASG_SIN_CUENTA_CONF",        "{0}" },            // empleado sin cuenta vinculada
            { "AUD_RECALC_OK",              "{0} {1}" },        // reservas recalculadas (con sus pagos), inconsistencias
            { "CONN_ESQUEMA_INCOMPLETO",    "{0} {1}" },        // base, objetos faltantes
            { "COORD_EVENTO",               "{0} {1} {2} {3} {4}" }, // reserva, fecha, salon, cliente, invitados
            { "CRYPTO_CLAVE_INVALIDA",      "{0}" },            // ruta del archivo de clave
            { "DISP_RESUMEN_OK",            "{0}" },            // salones disponibles
            { "DISP_RESUMEN_SIN_CAPACIDAD", "{0}" },            // invitados pedidos, sin salon que los admita
            { "DISP_RESUMEN_SIN_FECHAS",    "{0}" },            // dias del horizonte sin fechas libres
            { "EMAIL_INTRO",                "{0}" },            // numero de reserva
            { "EMAIL_SALUDO",               "{0}" },            // nombre del cliente
            { "IDI_CAMBIOS_PENDIENTES",     "{0}" },            // idioma con ediciones sin guardar
            { "IDI_FILTRO_INVALIDO",        "{0}" },            // clave rechazada (CMP_FILTER)
            { "IDI_PLANTILLA_INVALIDA",     "{0}" },            // clave rechazada
            { "IDI_TEXTO_VACIO",            "{0}" },            // clave rechazada
            { "LOGIN_INTENTOS",             "{0} {1}" },        // intento, maximo de intentos
            { "MSG_COORD_SUPERPOSICION_DET", "{0} {1} {2} {3}" }, // RN-09: empleado y reserva, fecha y franja del turno con el que se superpone
            { "MSG_CRO_RESPONSABLE_ACTIVIDAD", "{0} {1}" },     // RN-11: actividad a corregir y su responsable
            { "MSG_PAGO_ANULAR_CONF",       "{0}" },            // importe ya formateado
            { "MSG_RES_CANCELADA",          "{0:N2} {1:N2}" },  // RN-02: retenido, reintegro
            { "MSG_RES_CANCELAR",           "{0}" },            // numero de reserva
            { "MSG_RES_CANCELAR_DETALLE",   "{0:N2} {1:N2}" },  // RN-02: retenido, reintegro
            { "MSG_RES_CONFIRMACIONES_REINICIADAS", "{0}" },    // RN-08: confirmaciones que vuelven a pendiente
            { "MSG_RES_MONTO",              "{0}" },            // monto maximo admitido, ya formateado
            { "MSG_RES_TRANSICION",         "{0} {1}" },        // RN-05: estado actual, estado pedido
            { "MSG_SRV_PRECIO_MAX",         "{0}" },            // precio maximo admitido, ya formateado
            { "OPE_AVANCE_CRONOGRAMA",      "{0}" },            // actividades del cronograma
            { "OPE_AVANCE_INCIDENCIAS",     "{0} {1}" },        // incidencias, abiertas
            { "OPE_AVANCE_PERSONAL",        "{0} {1} {2} {3}" },// asignados, confirmados, pendientes, rechazados
            { "OPE_AVANCE_TAREAS",          "{0}" },            // tareas asignadas
            { "SUP_FUERA_FECHA",            "{0} {1}" },        // RN-13: fecha del evento, fecha de hoy
            { "SUP_RESUMEN",                "{0} {1}" },        // incidencias, abiertas
            { "TAR_QUITAR_CONF",            "{0}" },            // empleado de la tarea que se quita
            { "TAR_RESUMEN",                "{0}" },            // tareas asignadas
        };

        // Marcadores de una plantilla, sin las llaves escapadas ("{{" y "}}"), o null
        // si algun indice no se puede leer como un entero chico ("{99999999999}").
        private static List<(int Indice_704ILR, string Alineacion_704ILR, string Formato_704ILR)> LeerMarcadores_704ILR(string texto_704ILR)
        {
            var marcadores_704ILR = new List<(int Indice_704ILR, string Alineacion_704ILR, string Formato_704ILR)>();
            string limpio_704ILR = (texto_704ILR ?? string.Empty).Replace("{{", string.Empty).Replace("}}", string.Empty);
            foreach (Match m_704ILR in Marcador_704ILR.Matches(limpio_704ILR))
            {
                if (!int.TryParse(m_704ILR.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int indice_704ILR)
                    || indice_704ILR > MaxIndice_704ILR)
                    return null;
                marcadores_704ILR.Add((indice_704ILR, m_704ILR.Groups[2].Value, m_704ILR.Groups[3].Value));
            }
            return marcadores_704ILR;
        }

        // Un texto es valido contra la plantilla de referencia de su clave si:
        //  * cada marcador usa un indice de la referencia;
        //  * su formato ({0:N2}) y su alineacion ({0,10}) son los de la referencia
        //    para ese indice, o no tiene: con argumentos nulos {0:D} formatea bien,
        //    pero con el importe real lanza en la pantalla que lo muestra;
        //  * con exigirTodos, estan todos los indices de la referencia: sin el numero
        //    de reserva o sin los importes de la RN-02 el texto ya no informa lo que
        //    la clave tiene que informar;
        //  * las llaves cierran (string.Format no lanza).
        private static bool ValidarPlantilla_704ILR(string referencia_704ILR, string textoNuevo_704ILR, bool exigirTodos_704ILR)
        {
            if (string.IsNullOrEmpty(textoNuevo_704ILR)) return true;

            var admitidos_704ILR = LeerMarcadores_704ILR(referencia_704ILR)
                ?? new List<(int Indice_704ILR, string Alineacion_704ILR, string Formato_704ILR)>();
            var usados_704ILR = LeerMarcadores_704ILR(textoNuevo_704ILR);
            if (usados_704ILR == null) return false;

            foreach (var usado_704ILR in usados_704ILR)
            {
                bool admitido_704ILR = admitidos_704ILR.Any(a_704ILR =>
                    a_704ILR.Indice_704ILR == usado_704ILR.Indice_704ILR
                    && (usado_704ILR.Formato_704ILR.Length == 0
                        || string.Equals(usado_704ILR.Formato_704ILR, a_704ILR.Formato_704ILR, StringComparison.OrdinalIgnoreCase))
                    && (usado_704ILR.Alineacion_704ILR.Length == 0 || usado_704ILR.Alineacion_704ILR == a_704ILR.Alineacion_704ILR));
                if (!admitido_704ILR) return false;
            }
            if (exigirTodos_704ILR &&
                admitidos_704ILR.Any(a_704ILR => usados_704ILR.All(u_704ILR => u_704ILR.Indice_704ILR != a_704ILR.Indice_704ILR)))
                return false;

            int cantidad_704ILR = admitidos_704ILR.Count == 0 ? 0 : admitidos_704ILR.Max(a_704ILR => a_704ILR.Indice_704ILR) + 1;
            try { string.Format(CultureInfo.InvariantCulture, textoNuevo_704ILR, new object[cantidad_704ILR]); return true; }
            catch (FormatException) { return false; }
        }

        // Un texto editado solo puede usar los marcadores {n} del texto de fabrica
        // de la misma clave, con su mismo formato, y sus llaves tienen que cerrar:
        // string.Format con una plantilla rota lanza en la pantalla que la usa, no
        // en el editor.
        public static bool PlantillaValida_704ILR(string textoFabrica_704ILR, string textoNuevo_704ILR)
            => ValidarPlantilla_704ILR(textoFabrica_704ILR, textoNuevo_704ILR, false);

        // Primera clave del lote cuyo texto no respeta los marcadores de su clave
        // (catalogo del codigo: ni ajenos, ni faltantes, ni con otro formato), o null
        // si todas son validas.
        public static string PrimeraPlantillaInvalida_704ILR(IDictionary<string, string> textos_704ILR)
        {
            if (textos_704ILR == null) return null;
            foreach (var kv_704ILR in textos_704ILR)
            {
                MarcadoresPorClave_704ILR.TryGetValue(kv_704ILR.Key ?? string.Empty, out string referencia_704ILR);
                if (!ValidarPlantilla_704ILR(referencia_704ILR, kv_704ILR.Value, true)) return kv_704ILR.Key;
            }
            return null;
        }

        // Una traduccion en blanco (vacia, solo espacios o solo caracteres que no se ven:
        // invisibles como el espacio de ancho cero o rellenos que se dibujan vacios)
        // deja menus, titulos, botones y confirmaciones sin texto.
        private static string PrimerTextoVacio_704ILR(IDictionary<string, string> textos_704ILR)
            => textos_704ILR.FirstOrDefault(kv_704ILR => GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(kv_704ILR.Value)).Key;

        // Clave del filtro del cuadro "Guardar como" del comprobante.
        private const string ClaveFiltroArchivos_704ILR = "CMP_FILTER";

        // Un filtro del cuadro "Guardar como" son pares descripcion|patron separados por
        // '|'. Con una cantidad impar de tramos el cuadro lanza al recibir el filtro (el
        // boton Comprobante quedaba inservible en ese idioma) y con un tramo en blanco
        // muestra una opcion sin nombre o que no lista ningun archivo.
        private static bool FiltroArchivosValido_704ILR(string filtro_704ILR)
        {
            if (GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(filtro_704ILR)) return false;
            string[] tramos_704ILR = filtro_704ILR.Split('|');
            return tramos_704ILR.Length % 2 == 0 && tramos_704ILR.All(t_704ILR => !GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(t_704ILR));
        }

        // Guarda los textos editados de un idioma, todos o ninguno, y recarga el
        // gestor en caliente. El editor manda solo las filas que cambiaron. Un lote
        // con un texto vacio, una plantilla invalida o un filtro de archivos invalido
        // (CMP_FILTER) se rechaza entero, sin guardar
        // nada; el rechazo queda en bitacora y claveRechazada_704ILR nombra la
        // primera clave que lo provoco. Un lote vacio no escribe ni asienta nada.
        public static TraduccionResult_704ILR GuardarTraducciones_704ILR(int idiomaId_704ILR, IDictionary<string, string> textos_704ILR, out string claveRechazada_704ILR)
        {
            claveRechazada_704ILR = null;
            if (textos_704ILR == null || textos_704ILR.Count == 0) return TraduccionResult_704ILR.Success_704ILR;

            TraduccionResult_704ILR resultado_704ILR = TraduccionResult_704ILR.Success_704ILR;
            claveRechazada_704ILR = PrimerTextoVacio_704ILR(textos_704ILR);
            if (claveRechazada_704ILR != null)
                resultado_704ILR = TraduccionResult_704ILR.TextoVacio_704ILR;
            else
            {
                claveRechazada_704ILR = PrimeraPlantillaInvalida_704ILR(textos_704ILR);
                if (claveRechazada_704ILR != null) resultado_704ILR = TraduccionResult_704ILR.PlantillaInvalida_704ILR;
                else if (textos_704ILR.TryGetValue(ClaveFiltroArchivos_704ILR, out string filtro_704ILR) && !FiltroArchivosValido_704ILR(filtro_704ILR))
                {
                    claveRechazada_704ILR = ClaveFiltroArchivos_704ILR;
                    resultado_704ILR = TraduccionResult_704ILR.FiltroInvalido_704ILR;
                }
            }
            if (resultado_704ILR != TraduccionResult_704ILR.Success_704ILR)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Idiomas", "Traducciones rechazadas", CriticidadBitacora_704ILR.Advertencia,
                    $"Idioma #{idiomaId_704ILR}: la traduccion de '{claveRechazada_704ILR}' " + MotivoRechazo_704ILR(resultado_704ILR));
                return resultado_704ILR;
            }

            // Todo el lote en una transaccion: una falla a mitad no deja unas claves
            // con el texto nuevo y otras con el viejo.
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    foreach (var kv_704ILR in textos_704ILR)
                        DAL_Idioma_704ILR.UpsertTraduccion_704ILR(idiomaId_704ILR, kv_704ILR.Key, kv_704ILR.Value, conn_704ILR, tx_704ILR);
                    tx_704ILR.Commit();
                }
            }

            Inicializar_704ILR();
            BLL_Bitacora_704ILR.Registrar_704ILR("Idiomas", "Edicion de traducciones", CriticidadBitacora_704ILR.Info,
                $"Se actualizaron las traducciones del idioma #{idiomaId_704ILR}");
            return TraduccionResult_704ILR.Success_704ILR;
        }

        // Variante que lanza ArgumentException si el lote se rechaza.
        public static void GuardarTraducciones_704ILR(int idiomaId_704ILR, IDictionary<string, string> textos_704ILR)
        {
            TraduccionResult_704ILR resultado_704ILR = GuardarTraducciones_704ILR(idiomaId_704ILR, textos_704ILR, out string clave_704ILR);
            if (resultado_704ILR == TraduccionResult_704ILR.TextoVacio_704ILR)
                throw new ArgumentException("La traduccion de '" + clave_704ILR + "' esta vacia.");
            if (resultado_704ILR == TraduccionResult_704ILR.PlantillaInvalida_704ILR)
                throw new ArgumentException("La traduccion de '" + clave_704ILR + "' tiene llaves sin cerrar o marcadores que no coinciden con los de la clave.");
            if (resultado_704ILR == TraduccionResult_704ILR.FiltroInvalido_704ILR)
                throw new ArgumentException("La traduccion de '" + clave_704ILR + "' no es un filtro de archivos (descripcion|patron) del cuadro Guardar como.");
        }
    }
}
