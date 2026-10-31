using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum PerfilResult_704ILR { Success_704ILR, NombreInvalido_704ILR, NombreDuplicado_704ILR, ReferenciaCircular_704ILR, SinGestorDePerfiles_704ILR,
        ReferenciaInvalida_704ILR }   // el perfil, un permiso o un perfil incluido de la composicion no existe

    // Composicion de un perfil todavia no guardada: permite resolver el Composite
    // "como quedaria" antes de persistir.
    internal sealed class ComposicionPropuesta_704ILR
    {
        public ComposicionPropuesta_704ILR(int perfilId_704ILR, ICollection<int> permisos_704ILR, ICollection<int> incluidos_704ILR)
        {
            PerfilId_704ILR = perfilId_704ILR;
            Permisos_704ILR = permisos_704ILR;
            Incluidos_704ILR = incluidos_704ILR;
        }

        public int PerfilId_704ILR { get; }
        public ICollection<int> Permisos_704ILR { get; }
        public ICollection<int> Incluidos_704ILR { get; }
    }

    // Logica de negocio de perfiles y arbol de permisos (Composite).
    public static class BLL_Perfil_704ILR
    {
        // Permiso que habilita la gestion de perfiles, la asignacion de perfiles a
        // cuentas y el desbloqueo: un cambio no puede empeorar quien lo resuelve (G04,
        // RNF-09; ver DejaSinGestor_704ILR, que cuenta a una cuenta gestora bloqueada
        // como recuperable).
        internal const string ClaveGestionPerfiles_704ILR = "PERFILES_GESTION";

        public static List<BE_IComponentePermiso_704ILR> GetArbolPermisos_704ILR() => DAL_Permiso_704ILR.GetArbol_704ILR();

        public static List<BE_Perfil_704ILR> GetPerfiles_704ILR() => DAL_Perfil_704ILR.GetAll_704ILR();

        // Alta de un perfil nuevo (luego se le asignan permisos y usuarios).
        public static PerfilResult_704ILR CrearPerfil_704ILR(string nombre_704ILR, string descripcion_704ILR, out int nuevoId_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR("PERFILES_GESTION", "crear un perfil");
            nuevoId_704ILR = 0;
            nombre_704ILR = NormalizarNombre_704ILR(nombre_704ILR);
            if (nombre_704ILR.Length == 0 || nombre_704ILR.Length > 80)
                return PerfilResult_704ILR.NombreInvalido_704ILR;
            if (DAL_Perfil_704ILR.ExistsNombre_704ILR(nombre_704ILR))
                return PerfilResult_704ILR.NombreDuplicado_704ILR;

            try
            {
                // La descripcion es un dato accesorio: se recorta a lo que entra en
                // Perfiles.Descripcion, como la del catalogo de servicios, sin partir un
                // caracter (antes la recortaba en silencio el parametro de la capa de datos).
                nuevoId_704ILR = DAL_Perfil_704ILR.Insert_704ILR(nombre_704ILR, string.IsNullOrWhiteSpace(descripcion_704ILR)
                    ? null : GestorDeIdioma_704ILR.Recortar_704ILR(descripcion_704ILR.Trim(), MaxDescripcion_704ILR));
            }
            catch (Microsoft.Data.SqlClient.SqlException ex_704ILR) when (ex_704ILR.Number == 2627 || ex_704ILR.Number == 2601)
            {
                // Otra sesion dio de alta el mismo nombre entre el chequeo y el INSERT (dos
                // altas simultaneas): UQ_Perfiles_Nombre es la red de seguridad y la respuesta
                // es la misma que la del chequeo previo. 2627: restriccion UNIQUE; 2601: indice
                // unico. En Perfiles la unica restriccion unica es la del nombre.
                nuevoId_704ILR = 0;
                return PerfilResult_704ILR.NombreDuplicado_704ILR;
            }
            BLL_Bitacora_704ILR.Registrar_704ILR("Perfiles", "Alta de perfil", CriticidadBitacora_704ILR.Info, $"Perfil '{nombre_704ILR}' creado");
            return PerfilResult_704ILR.Success_704ILR;
        }

        // El nombre de un perfil se compara y se guarda en su forma visible, con el mismo
        // criterio que el editor de idiomas (GestorDeIdioma_704ILR.TextoVisible_704ILR): se
        // quitan los caracteres que no ocupan lugar (formato, control, espacio de ancho
        // cero, guion blando, marcas de direccion), cada tramo de espacios o de rellenos que
        // se dibujan como un espacio (tabulador, espacio duro o ideografico, los rellenos de
        // Hangul y la celda braille vacia) cuenta como un espacio simple y la cadena se
        // lleva a su composicion canonica (una tilde combinada vale lo mismo que la letra
        // acentuada). Asi dos nombres que se ven iguales no conviven como perfiles
        // distintos: un relleno entre dos palabras las separa, como en Idiomas, y no las
        // pega. Despues se descarta lo que no se ve (GestorDeIdioma_704ILR.TextoEnBlanco_704ILR)
        // grafema por grafema (lo que se ve como un solo caracter: una letra con sus marcas
        // combinantes) y no caracter por caracter: una tilde, una virgulilla o una cedilla
        // combinante sola no dibuja nada, pero pegada a su letra es parte del nombre y
        // descartarla cambiaba el nombre (y lo dejaba convivir con el mismo nombre escrito
        // con la letra compuesta).
        private static string NormalizarNombre_704ILR(string nombre_704ILR)
        {
            if (string.IsNullOrEmpty(nombre_704ILR)) return string.Empty;
            // Primero el texto tal como se ve: los rellenos que se dibujan como un espacio ya
            // llegan como espacios comunes al pase por grafemas de abajo.
            nombre_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(nombre_704ILR);
            var sb_704ILR = new StringBuilder(nombre_704ILR.Length);
            var tramo_704ILR = new StringBuilder();
            bool espacioPendiente_704ILR = false;

            // Lo que se junto de un grafema se suma solo si se ve; si no, se descarta entero.
            void AgregarTramo_704ILR()
            {
                if (tramo_704ILR.Length == 0) return;
                string texto_704ILR = tramo_704ILR.ToString();
                tramo_704ILR.Clear();
                if (GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(texto_704ILR)) return;
                if (espacioPendiente_704ILR) { sb_704ILR.Append(' '); espacioPendiente_704ILR = false; }
                sb_704ILR.Append(texto_704ILR);
            }

            TextElementEnumerator grafemas_704ILR = StringInfo.GetTextElementEnumerator(nombre_704ILR);
            while (grafemas_704ILR.MoveNext())
            {
                foreach (Rune r_704ILR in grafemas_704ILR.GetTextElement().EnumerateRunes())
                {
                    if (Rune.IsWhiteSpace(r_704ILR))
                    {
                        // Un espacio separa aunque lleve marcas pegadas: lo anterior se evalua solo.
                        AgregarTramo_704ILR();
                        espacioPendiente_704ILR = sb_704ILR.Length > 0;
                        continue;
                    }
                    UnicodeCategory categoria_704ILR = Rune.GetUnicodeCategory(r_704ILR);
                    if (categoria_704ILR == UnicodeCategory.Format || categoria_704ILR == UnicodeCategory.Control) continue;
                    tramo_704ILR.Append(r_704ILR.ToString());
                }
                AgregarTramo_704ILR();
            }
            return sb_704ILR.ToString().Normalize(NormalizationForm.FormC);
        }

        public static HashSet<int> GetPermisosAsignados_704ILR(int perfilId_704ILR) => DAL_Perfil_704ILR.GetPermisoIds_704ILR(perfilId_704ILR);

        public static HashSet<int> GetPerfilesIncluidos_704ILR(int perfilId_704ILR) => DAL_Perfil_704ILR.GetIncluidos_704ILR(perfilId_704ILR);

        // Reemplaza los permisos del perfil conservando los perfiles que incluye. Pasa
        // por las mismas reglas que la composicion completa.
        public static PerfilResult_704ILR GuardarAsignaciones_704ILR(int perfilId_704ILR, IEnumerable<int> permisoIds_704ILR) =>
            GuardarComposicion_704ILR(perfilId_704ILR, permisoIds_704ILR, DAL_Perfil_704ILR.GetIncluidos_704ILR(perfilId_704ILR));

        // Guarda la composicion completa del perfil: sus permisos y los perfiles
        // que incluye (Composite de perfiles). Rechaza composiciones que generen
        // una referencia circular (directa o transitiva) o que empeoren la continuidad
        // de PERFILES_GESTION (ver DejaSinGestor_704ILR).
        public static PerfilResult_704ILR GuardarComposicion_704ILR(int perfilId_704ILR, IEnumerable<int> permisoIds_704ILR,
            IEnumerable<int> perfilesIncluidos_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR("PERFILES_GESTION", "guardar la composicion de un perfil");
            var incluidos_704ILR = (perfilesIncluidos_704ILR ?? Enumerable.Empty<int>()).Distinct().ToList();
            // El catalogo de permisos no lo modifica la aplicacion: se lee antes del bloqueo.
            // Tambien lo que el perfil ya tiene guardado, que solo decide si se conserva un grupo
            // propuesto con parte de su contenido (ver NormalizarComposicion): la composicion de
            // un perfil la define quien la guarda ultimo, y las reglas que dependen de otras
            // grabaciones se evaluan abajo, con el bloqueo tomado.
            var arbol_704ILR = GetArbolPermisos_704ILR();
            HashSet<int> guardados_704ILR = DAL_Perfil_704ILR.GetPermisoIds_704ILR(perfilId_704ILR);
            List<int> permisos_704ILR = NormalizarComposicion_704ILR(arbol_704ILR, permisoIds_704ILR, guardados_704ILR);
            var propuesta_704ILR = new ComposicionPropuesta_704ILR(perfilId_704ILR, permisos_704ILR, incluidos_704ILR);

            // El perfil, cada permiso y cada perfil incluido tienen que existir: un id
            // inexistente chocaba con la clave foranea dentro de la transaccion y llegaba
            // como error tecnico. La aplicacion no da de baja perfiles ni permisos, asi que
            // se controla antes del bloqueo.
            var componentesExistentes_704ILR = new Dictionary<int, string>();
            IndexarComponentes_704ILR(arbol_704ILR, componentesExistentes_704ILR);
            var perfilesExistentes_704ILR = new HashSet<int>(GetPerfiles_704ILR().Select(p_704ILR => p_704ILR.Id_704ILR));
            if (!perfilesExistentes_704ILR.Contains(perfilId_704ILR)
                || permisos_704ILR.Any(p_704ILR => !componentesExistentes_704ILR.ContainsKey(p_704ILR))
                || incluidos_704ILR.Any(i_704ILR => !perfilesExistentes_704ILR.Contains(i_704ILR)))
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Perfiles", "Composicion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"La composicion propuesta del perfil #{perfilId_704ILR} nombra un perfil o un permiso que no existe");
                return PerfilResult_704ILR.ReferenciaInvalida_704ILR;
            }

            // Las reglas se evaluan con el bloqueo de la gestion de perfiles ya tomado por la
            // transaccion que graba (DAL): otra grabacion simultanea de composiciones o de
            // asignaciones no puede cambiar lo validado antes de que esta termine. Validadas
            // fuera, dos grabaciones veian cada una el estado previo a la otra y entre las dos
            // dejaban al sistema sin gestor.
            var rechazo_704ILR = PerfilResult_704ILR.Success_704ILR;
            string perfil_704ILR = null;
            HashSet<int> permisosAntes_704ILR = null, incluidosAntes_704ILR = null;
            bool otorgaba_704ILR = false, otorgara_704ILR = false;
            bool grabado_704ILR = DAL_Perfil_704ILR.SetComposicion_704ILR(perfilId_704ILR, permisos_704ILR, incluidos_704ILR, () =>
            {
                if (GeneraCiclo_704ILR(perfilId_704ILR, incluidos_704ILR))
                {
                    rechazo_704ILR = PerfilResult_704ILR.ReferenciaCircular_704ILR;
                    return false;
                }
                perfil_704ILR = DescribirPerfil_704ILR(perfilId_704ILR);
                if (DejaSinGestor_704ILR(arbol_704ILR, propuesta_704ILR, null))
                {
                    rechazo_704ILR = PerfilResult_704ILR.SinGestorDePerfiles_704ILR;
                    return false;
                }
                permisosAntes_704ILR = DAL_Perfil_704ILR.GetPermisoIds_704ILR(perfilId_704ILR);
                incluidosAntes_704ILR = DAL_Perfil_704ILR.GetIncluidos_704ILR(perfilId_704ILR);
                otorgaba_704ILR = OtorgaGestion_704ILR(perfilId_704ILR, arbol_704ILR, null);
                otorgara_704ILR = OtorgaGestion_704ILR(perfilId_704ILR, arbol_704ILR, propuesta_704ILR);
                return true;
            });

            if (!grabado_704ILR)
            {
                if (rechazo_704ILR == PerfilResult_704ILR.SinGestorDePerfiles_704ILR)
                    BLL_Bitacora_704ILR.Registrar_704ILR("Perfiles", "Composicion rechazada", CriticidadBitacora_704ILR.Advertencia,
                        $"La composicion propuesta del perfil {perfil_704ILR} dejaba al sistema sin usuarios activos con gestion de perfiles");
                return rechazo_704ILR;
            }

            // El asiento nombra el perfil y lo que cambio: sin eso, vaciar un perfil y
            // guardarlo sin cambios dejaban el mismo texto. Quitarle a un perfil la
            // gestion de perfiles se asienta como Advertencia.
            var componentes_704ILR = new Dictionary<int, string>();
            IndexarComponentes_704ILR(arbol_704ILR, componentes_704ILR);
            var perfiles_704ILR = GetPerfiles_704ILR().ToDictionary(p_704ILR => p_704ILR.Id_704ILR, p_704ILR => $"'{p_704ILR.Nombre_704ILR}'");
            string Lista_704ILR(IEnumerable<int> ids_704ILR, IDictionary<int, string> nombres_704ILR) =>
                string.Join(", ", ids_704ILR.OrderBy(i_704ILR => i_704ILR).Select(i_704ILR => nombres_704ILR.TryGetValue(i_704ILR, out var n_704ILR) ? n_704ILR : "#" + i_704ILR));

            var partes_704ILR = new List<string>();
            var agregados_704ILR = permisos_704ILR.Where(i_704ILR => !permisosAntes_704ILR.Contains(i_704ILR)).ToList();
            var quitados_704ILR = permisosAntes_704ILR.Where(i_704ILR => !permisos_704ILR.Contains(i_704ILR)).ToList();
            var incAgregados_704ILR = incluidos_704ILR.Where(i_704ILR => !incluidosAntes_704ILR.Contains(i_704ILR)).ToList();
            var incQuitados_704ILR = incluidosAntes_704ILR.Where(i_704ILR => !incluidos_704ILR.Contains(i_704ILR)).ToList();
            if (agregados_704ILR.Count > 0) partes_704ILR.Add("permisos agregados: " + Lista_704ILR(agregados_704ILR, componentes_704ILR));
            if (quitados_704ILR.Count > 0) partes_704ILR.Add("permisos quitados: " + Lista_704ILR(quitados_704ILR, componentes_704ILR));
            if (incAgregados_704ILR.Count > 0) partes_704ILR.Add("perfiles incluidos agregados: " + Lista_704ILR(incAgregados_704ILR, perfiles_704ILR));
            if (incQuitados_704ILR.Count > 0) partes_704ILR.Add("perfiles incluidos quitados: " + Lista_704ILR(incQuitados_704ILR, perfiles_704ILR));
            bool pierdeGestion_704ILR = otorgaba_704ILR && !otorgara_704ILR;
            if (pierdeGestion_704ILR) partes_704ILR.Add("deja de otorgar " + ClaveGestionPerfiles_704ILR);

            BLL_Bitacora_704ILR.Registrar_704ILR("Perfiles", "Actualizacion de permisos",
                pierdeGestion_704ILR ? CriticidadBitacora_704ILR.Advertencia : CriticidadBitacora_704ILR.Info,
                $"Composicion del perfil {perfil_704ILR}: " + (partes_704ILR.Count == 0 ? "sin cambios" : string.Join("; ", partes_704ILR)));
            return PerfilResult_704ILR.Success_704ILR;
        }

        // Contrato de la composicion: la lista trae los componentes que el perfil tiene
        // de forma directa. Un grupo que llega junto con PARTE de su contenido es un
        // grupo incompleto (en la pantalla se destildo alguno de sus hijos): no se
        // guarda y cuentan solo los hijos que vinieron, porque un grupo asignado
        // concede todas sus hojas y concederia tambien la destildada. Un grupo que
        // llega solo, sin ninguno de sus descendientes, se asigna entero como en el
        // Composite clasico. Excepcion: el grupo incompleto se conserva si el perfil ya lo
        // tiene guardado con exactamente ese contenido ('guardados'). Pasa cuando el
        // catalogo le sumo permisos despues de guardarlo con su contenido, o cuando la
        // composicion llego asi por un script: volver a guardarla sin tocarla no quita el
        // grupo ni los permisos que hoy concede.
        private static List<int> NormalizarComposicion_704ILR(List<BE_IComponentePermiso_704ILR> arbol_704ILR, IEnumerable<int> permisoIds_704ILR,
            ICollection<int> guardados_704ILR)
        {
            var propuestos_704ILR = new HashSet<int>(permisoIds_704ILR ?? Enumerable.Empty<int>());
            var descartados_704ILR = new HashSet<int>();
            ICollection<int> enBase_704ILR = guardados_704ILR ?? new HashSet<int>();
            foreach (var raiz_704ILR in arbol_704ILR) EsCompleto_704ILR(raiz_704ILR, propuestos_704ILR, enBase_704ILR, descartados_704ILR);
            return propuestos_704ILR.Where(id_704ILR => !descartados_704ILR.Contains(id_704ILR)).ToList();
        }

        // Post-orden: un nodo es completo si es una hoja propuesta, o un grupo cuyos
        // hijos directos son todos completos. Descarta los grupos propuestos incompletos,
        // salvo el que repite tal cual su contenido guardado: ese se conserva, pero no cuenta
        // como completo para el grupo que lo contiene (que a su vez se conserva solo si
        // tambien repite lo guardado).
        private static bool EsCompleto_704ILR(BE_IComponentePermiso_704ILR nodo_704ILR, HashSet<int> propuestos_704ILR, ICollection<int> guardados_704ILR,
            HashSet<int> descartados_704ILR)
        {
            if (!(nodo_704ILR is BE_GrupoPermisos_704ILR grupo_704ILR) || grupo_704ILR.Hijos_704ILR.Count == 0)
                return propuestos_704ILR.Contains(nodo_704ILR.Id_704ILR);

            bool hijosCompletos_704ILR = true;
            foreach (var hijo_704ILR in grupo_704ILR.Hijos_704ILR)
                hijosCompletos_704ILR &= EsCompleto_704ILR(hijo_704ILR, propuestos_704ILR, guardados_704ILR, descartados_704ILR);

            if (!propuestos_704ILR.Contains(grupo_704ILR.Id_704ILR)) return hijosCompletos_704ILR;
            if (hijosCompletos_704ILR || !TieneDescendientePropuesto_704ILR(grupo_704ILR, propuestos_704ILR)) return true;
            if (!guardados_704ILR.Contains(grupo_704ILR.Id_704ILR) || !MismoContenidoGuardado_704ILR(grupo_704ILR, propuestos_704ILR, guardados_704ILR))
                descartados_704ILR.Add(grupo_704ILR.Id_704ILR);
            return false;
        }

        // Cada componente del contenido del grupo, a cualquier profundidad, viene en la
        // propuesta si y solo si el perfil ya lo tenia guardado.
        private static bool MismoContenidoGuardado_704ILR(BE_GrupoPermisos_704ILR grupo_704ILR, HashSet<int> propuestos_704ILR, ICollection<int> guardados_704ILR) =>
            grupo_704ILR.Hijos_704ILR.All(h_704ILR =>
                propuestos_704ILR.Contains(h_704ILR.Id_704ILR) == guardados_704ILR.Contains(h_704ILR.Id_704ILR) &&
                (!(h_704ILR is BE_GrupoPermisos_704ILR g_704ILR) || MismoContenidoGuardado_704ILR(g_704ILR, propuestos_704ILR, guardados_704ILR)));

        private static bool TieneDescendientePropuesto_704ILR(BE_GrupoPermisos_704ILR grupo_704ILR, HashSet<int> propuestos_704ILR) =>
            grupo_704ILR.Hijos_704ILR.Any(h_704ILR => propuestos_704ILR.Contains(h_704ILR.Id_704ILR) ||
                (h_704ILR is BE_GrupoPermisos_704ILR g_704ILR && TieneDescendientePropuesto_704ILR(g_704ILR, propuestos_704ILR)));

        private static void IndexarComponentes_704ILR(IEnumerable<BE_IComponentePermiso_704ILR> nodos_704ILR, IDictionary<int, string> nombres_704ILR)
        {
            foreach (var n_704ILR in nodos_704ILR)
            {
                nombres_704ILR[n_704ILR.Id_704ILR] = n_704ILR is BE_Permiso_704ILR h_704ILR && !string.IsNullOrEmpty(h_704ILR.Clave_704ILR)
                    ? h_704ILR.Clave_704ILR : $"grupo '{n_704ILR.Nombre_704ILR}'";
                if (n_704ILR is BE_GrupoPermisos_704ILR g_704ILR) IndexarComponentes_704ILR(g_704ILR.Hijos_704ILR, nombres_704ILR);
            }
        }

        // Nombre y Id de un perfil para los asientos de la bitacora.
        internal static string DescribirPerfil_704ILR(int? perfilId_704ILR)
        {
            if (!perfilId_704ILR.HasValue) return "(ninguno)";
            BE_Perfil_704ILR p_704ILR = DAL_Perfil_704ILR.GetById_704ILR(perfilId_704ILR.Value);
            return p_704ILR == null ? "#" + perfilId_704ILR.Value : $"'{p_704ILR.Nombre_704ILR}' (#{p_704ILR.Id_704ILR})";
        }

        // Continuidad de la administracion (G04, RNF-09: una cuenta nueva espera a que la
        // administradora le asigne un perfil): un cambio de composicion o de asignaciones
        // no puede empeorar quien resuelve PERFILES_GESTION, el permiso que asigna perfiles
        // y desbloquea cuentas desde la aplicacion. El estado se mide en tres niveles (ver
        // NivelGestion_704ILR): lo resuelve alguna cuenta activa y no bloqueada; solo lo
        // resuelven cuentas activas bloqueadas; no lo resuelve ninguna cuenta activa. Se
        // rechaza solo el cambio que baja de nivel. Una cuenta gestora bloqueada por
        // intentos cuenta como gestora recuperable (el bloqueo lo produce la propia
        // aplicacion y se levanta con el desbloqueo documentado): se le puede quitar la
        // gestion mientras quede otra cuenta gestora del mismo nivel o de uno mejor, pero no
        // si con eso el sistema baja de nivel (por ejemplo, si es la ultima gestora que
        // queda). Si la base ya llego sin gestores (por fuera de la aplicacion), las demas
        // operaciones siguen permitidas.
        internal static bool DejaSinGestor_704ILR(ComposicionPropuesta_704ILR composicion_704ILR, IDictionary<int, int?> perfilPorUsuario_704ILR) =>
            DejaSinGestor_704ILR(GetArbolPermisos_704ILR(), composicion_704ILR, perfilPorUsuario_704ILR);

        private static bool DejaSinGestor_704ILR(List<BE_IComponentePermiso_704ILR> arbol_704ILR,
            ComposicionPropuesta_704ILR composicion_704ILR, IDictionary<int, int?> perfilPorUsuario_704ILR)
        {
            List<BE_User_704ILR> usuarios_704ILR = DAL_User_704ILR.GetAll_704ILR();
            int despues_704ILR = NivelGestion_704ILR(usuarios_704ILR, arbol_704ILR, composicion_704ILR, perfilPorUsuario_704ILR);
            if (despues_704ILR == NivelGestorDisponible_704ILR) return false;
            return despues_704ILR < NivelGestion_704ILR(usuarios_704ILR, arbol_704ILR, null, null);
        }

        // Niveles de continuidad de la gestion de perfiles.
        // Ancho de Perfiles.Descripcion.
        private const int MaxDescripcion_704ILR = 250;

        private const int NivelSinGestor_704ILR = 0;          // ninguna cuenta activa resuelve PERFILES_GESTION
        private const int NivelGestorBloqueado_704ILR = 1;    // solo la resuelven cuentas activas bloqueadas
        private const int NivelGestorDisponible_704ILR = 2;   // la resuelve al menos una cuenta activa y no bloqueada

        private static int NivelGestion_704ILR(List<BE_User_704ILR> usuarios_704ILR, List<BE_IComponentePermiso_704ILR> arbol_704ILR,
            ComposicionPropuesta_704ILR composicion_704ILR, IDictionary<int, int?> perfilPorUsuario_704ILR)
        {
            var construidos_704ILR = new Dictionary<int, BE_Perfil_704ILR>();
            var otorga_704ILR = new Dictionary<int, bool>();
            int nivel_704ILR = NivelSinGestor_704ILR;
            foreach (var u_704ILR in usuarios_704ILR)
            {
                if (!u_704ILR.Activo_704ILR) continue;
                int? perfilId_704ILR = perfilPorUsuario_704ILR != null && perfilPorUsuario_704ILR.TryGetValue(u_704ILR.Id_704ILR, out int? propuesto_704ILR)
                    ? propuesto_704ILR : u_704ILR.PerfilId_704ILR;
                if (!perfilId_704ILR.HasValue) continue;
                if (!otorga_704ILR.TryGetValue(perfilId_704ILR.Value, out bool si_704ILR))
                {
                    si_704ILR = ConstruirPerfilCompuesto_704ILR(perfilId_704ILR.Value, arbol_704ILR, construidos_704ILR, composicion_704ILR)
                        .TienePermiso_704ILR(ClaveGestionPerfiles_704ILR);
                    otorga_704ILR[perfilId_704ILR.Value] = si_704ILR;
                }
                if (!si_704ILR) continue;
                if (!u_704ILR.Blocked_704ILR) return NivelGestorDisponible_704ILR;
                nivel_704ILR = NivelGestorBloqueado_704ILR;
            }
            return nivel_704ILR;
        }

        // Si el perfil (con su herencia) otorga la gestion de perfiles.
        internal static bool OtorgaGestion_704ILR(int? perfilId_704ILR) =>
            perfilId_704ILR.HasValue && OtorgaGestion_704ILR(perfilId_704ILR.Value, GetArbolPermisos_704ILR(), null);

        private static bool OtorgaGestion_704ILR(int perfilId_704ILR, List<BE_IComponentePermiso_704ILR> arbol_704ILR, ComposicionPropuesta_704ILR composicion_704ILR) =>
            ConstruirPerfilCompuesto_704ILR(perfilId_704ILR, arbol_704ILR, new Dictionary<int, BE_Perfil_704ILR>(), composicion_704ILR)
                .TienePermiso_704ILR(ClaveGestionPerfiles_704ILR);

        // El grafo de inclusiones no admite ciclos: un perfil no puede contenerse
        // a si mismo ni directa ni transitivamente (Gerencial > Vendedor > Gerencial).
        // Se simula el grafo con la composicion propuesta y se busca si desde los
        // incluidos se puede volver al perfil de partida.
        private static bool GeneraCiclo_704ILR(int perfilId_704ILR, List<int> incluidosPropuestos_704ILR)
        {
            if (incluidosPropuestos_704ILR.Contains(perfilId_704ILR)) return true;

            var grafo_704ILR = DAL_Perfil_704ILR.GetTodasLasInclusiones_704ILR();
            grafo_704ILR[perfilId_704ILR] = incluidosPropuestos_704ILR;

            var visitados_704ILR = new HashSet<int>();
            var pendientes_704ILR = new Stack<int>(incluidosPropuestos_704ILR);
            while (pendientes_704ILR.Count > 0)
            {
                int actual_704ILR = pendientes_704ILR.Pop();
                if (actual_704ILR == perfilId_704ILR) return true;
                if (!visitados_704ILR.Add(actual_704ILR)) continue;
                if (grafo_704ILR.TryGetValue(actual_704ILR, out var hijos_704ILR))
                    foreach (int h_704ILR in hijos_704ILR) pendientes_704ILR.Push(h_704ILR);
            }
            return false;
        }

        // Construye el BE_Perfil compuesto (componentes del arbol asignados +
        // perfiles incluidos, recursivamente) y delega en la operacion
        // polimorfica del Composite para resolver los permisos efectivos.
        public static List<BE_Permiso_704ILR> GetPermisosEfectivosDePerfil_704ILR(int perfilId_704ILR)
        {
            var arbol_704ILR = GetArbolPermisos_704ILR();
            BE_Perfil_704ILR perfil_704ILR = ConstruirPerfilCompuesto_704ILR(perfilId_704ILR, arbol_704ILR, new Dictionary<int, BE_Perfil_704ILR>(), null);
            return perfil_704ILR.ObtenerPermisosEfectivos_704ILR();
        }

        // 'composicion' (opcional) reemplaza, para su perfil, lo guardado en la base:
        // asi se resuelve el Composite con una composicion todavia no persistida.
        private static BE_Perfil_704ILR ConstruirPerfilCompuesto_704ILR(int perfilId_704ILR,
            List<BE_IComponentePermiso_704ILR> arbol_704ILR, Dictionary<int, BE_Perfil_704ILR> construidos_704ILR,
            ComposicionPropuesta_704ILR composicion_704ILR)
        {
            // Cada perfil se materializa una sola vez (comparte instancia si llega
            // por varios caminos y corta cualquier ciclo residual en datos).
            if (construidos_704ILR.TryGetValue(perfilId_704ILR, out var existente_704ILR)) return existente_704ILR;

            BE_Perfil_704ILR perfil_704ILR = DAL_Perfil_704ILR.GetById_704ILR(perfilId_704ILR) ?? new BE_Perfil_704ILR { Id_704ILR = perfilId_704ILR };
            construidos_704ILR[perfilId_704ILR] = perfil_704ILR;

            bool propuesto_704ILR = composicion_704ILR != null && composicion_704ILR.PerfilId_704ILR == perfilId_704ILR;
            HashSet<int> asignados_704ILR = propuesto_704ILR
                ? new HashSet<int>(composicion_704ILR.Permisos_704ILR)
                : DAL_Perfil_704ILR.GetPermisoIds_704ILR(perfilId_704ILR);
            AsignarComponentes_704ILR(arbol_704ILR, asignados_704ILR, perfil_704ILR);

            IEnumerable<int> incluidos_704ILR = propuesto_704ILR
                ? composicion_704ILR.Incluidos_704ILR
                : DAL_Perfil_704ILR.GetIncluidos_704ILR(perfilId_704ILR);
            foreach (int hijoId_704ILR in incluidos_704ILR)
                perfil_704ILR.IncluirPerfil_704ILR(ConstruirPerfilCompuesto_704ILR(hijoId_704ILR, arbol_704ILR, construidos_704ILR, composicion_704ILR));

            return perfil_704ILR;
        }

        // Recorre el arbol y asigna al perfil los nodos marcados. Si un grupo esta
        // asignado se toma entero (su subtree ya lo resuelve el Composite), por lo
        // que no hace falta descender dentro de el.
        private static void AsignarComponentes_704ILR(List<BE_IComponentePermiso_704ILR> nodos_704ILR,
            HashSet<int> asignados_704ILR, BE_Perfil_704ILR perfil_704ILR)
        {
            foreach (var nodo_704ILR in nodos_704ILR)
            {
                if (asignados_704ILR.Contains(nodo_704ILR.Id_704ILR))
                {
                    perfil_704ILR.Asignar_704ILR(nodo_704ILR);
                }
                else if (nodo_704ILR is BE_GrupoPermisos_704ILR grupo_704ILR)
                {
                    AsignarComponentes_704ILR(grupo_704ILR.Hijos_704ILR, asignados_704ILR, perfil_704ILR);
                }
            }
        }

    }
}
