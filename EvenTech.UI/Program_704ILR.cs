using System;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.Services;

namespace EvenTech.UI
{
    internal static class Program_704ILR
    {
        // Nombre del mutex que la aplicacion mantiene mientras esta abierta. El instalador
        // lo busca (AppMutex de EvenTech.iss) para no actualizar los archivos ni la base
        // con el sistema en uso: con el mismo nombre en el ambito de la sesion y en el
        // global, lo encuentra tambien si quien lo tiene abierto es otro usuario del equipo.
        internal const string NombreMutex_704ILR = "EvenTech_704ILR_Aplicacion";

        [STAThread]
        static void Main()
        {
            using var enUso_704ILR = AbrirMutex_704ILR(NombreMutex_704ILR);
            using var enUsoGlobal_704ILR = AbrirMutex_704ILR(@"Global\" + NombreMutex_704ILR);

            ApplicationConfiguration.Initialize();

            // Red de seguridad para lo imprevisto: una excepcion no manejada en un evento
            // de pantalla se asienta en la bitacora y se informa con el mensaje traducido,
            // en lugar del cuadro generico de .NET (Continuar/Salir). El modo se fija antes
            // de crear cualquier ventana: despues de un ShowDialog ya no se puede cambiar.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s_704ILR, e_704ILR) => ExcepcionNoControlada_704ILR(e_704ILR.Exception);

            // Verificacion de conectividad ANTES que nada: sin base no hay idiomas,
            // ni verificacion de integridad, ni login. Si falla, se ofrece
            // configurar la instancia y se reinicia con la cadena nueva.
            if (!AsegurarConexion_704ILR()) return;

            // Carga de idiomas/traducciones desde la base hacia el GestorDeIdioma
            // (patron Observer). Una falla (la base se cae, una tabla queda bloqueada o
            // inaccesible por un momento) se asienta y la carga se reintenta antes de la
            // primera pantalla, al abrir el selector de idioma y al entrar a la ventana
            // principal; mientras tanto las leyendas usan su texto por defecto. Antes la
            // falla se descartaba en silencio y quedaban las claves crudas toda la ejecucion.
            CargarIdiomas_704ILR();

            // Idioma recordado de la sesion anterior (preferencia de la estacion).
            AplicarIdiomaGuardado_704ILR();

            // Verificacion de integridad (digitos verificadores) ANTES del login.
            // Si hay inconsistencias, se muestra la alerta y se registra en bitacora.
            try
            {
                var integridad_704ILR = EvenTech.BLL.BLL_Integridad_704ILR.Verificar_704ILR();
                // La alerta y el login son las primeras pantallas con leyendas.
                ReintentarIdiomas_704ILR("antes de la primera pantalla");
                if (!integridad_704ILR.Ok_704ILR)
                {
                    using (var alerta_704ILR = new frmAlertaIntegridad_704ILR(integridad_704ILR.Inconsistencias_704ILR))
                        alerta_704ILR.ShowDialog();
                }
            }
            catch (Exception ex_704ILR)
            {
                // Si la verificacion no puede correr (tabla o columna ausente, dato
                // fuera de dominio) no se bloquea el arranque, pero "no verificado"
                // no es "verificado": se asienta en bitacora y se informa la causa.
                EvenTech.BLL.BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Integridad", "Verificacion al arranque");
                ReintentarIdiomas_704ILR("antes de la primera pantalla");
                MessageBox.Show(MensajeNoVerificada_704ILR(ex_704ILR),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // El loop principal vive en frmLogin: al validar credenciales abre
            // frmMain modal y al volver del logout queda esperando otro login.
            // El "✕" del frmLogin termina la app.
            Application.Run(new frmLogin_704ILR());
        }

        // Solo marca que la aplicacion esta abierta: no impide abrir otra ventana del
        // sistema. Si el sistema operativo no deja crearlo (el global ya lo creo otra
        // sesion con otros permisos), alcanza con el de la sesion.
        private static System.Threading.Mutex AbrirMutex_704ILR(string nombre_704ILR)
        {
            try { return new System.Threading.Mutex(false, nombre_704ILR); }
            catch (Exception ex_704ILR) when (ex_704ILR is UnauthorizedAccessException || ex_704ILR is System.Threading.WaitHandleCannotBeOpenedException || ex_704ILR is System.IO.IOException)
            {
                return null;
            }
        }

        private static void ExcepcionNoControlada_704ILR(Exception ex_704ILR)
        {
            try { EvenTech.BLL.BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Aplicacion", "Excepcion no controlada en la interfaz"); }
            catch { /* sin bitacora disponible igual se informa */ }
            try { MessageBox.Show(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
        }

        // Aviso de arranque cuando la verificacion de integridad no pudo correr. No lleva
        // el texto del motor (en ingles y con nombres de objetos de la base): el detalle
        // tecnico ya quedo en la bitacora y el usuario ve la causa traducida, con el mismo
        // criterio que el resto de las pantallas (Tr_704ILR.MensajeExcepcion_704ILR).
        private static string MensajeNoVerificada_704ILR(Exception ex_704ILR) =>
            Tr_704ILR.F_704ILR("ALERT_NO_VERIFICADA", "La verificación de integridad no pudo ejecutarse: {0}",
                Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));

        // Devuelve true si hay conexion utilizable. Si no la hay, abre la pantalla
        // de configuracion; cuando el usuario guarda una que conecta, la app se
        // reinicia para que todas las capas tomen la cadena nueva desde cero.
        private static bool AsegurarConexion_704ILR()
        {
            if (EvenTech.BLL.BLL_Conexion_704ILR.VerificarActual_704ILR(out string mensaje_704ILR)) return true;

            using (var cfg_704ILR = new frmConfiguracionConexion_704ILR(mensaje_704ILR))
            {
                if (cfg_704ILR.ShowDialog() != DialogResult.OK || !cfg_704ILR.Configurada_704ILR)
                    return false;    // el usuario decidio salir sin configurar
            }

            Application.Restart();
            return false;            // este proceso termina; sigue el reiniciado
        }

        // Primera carga de idiomas y traducciones. Una falla no corta el arranque, pero
        // queda en bitacora (si la base la puede recibir) y pendiente de reintento.
        private static void CargarIdiomas_704ILR()
        {
            try { EvenTech.BLL.BLL_Idioma_704ILR.Inicializar_704ILR(); }
            catch (Exception ex_704ILR)
            {
                EvenTech.BLL.BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Idiomas", "Carga de idiomas al arranque");
            }
        }

        // Si la carga de idiomas quedo pendiente (fallo al arrancar o al recargar), la
        // vuelve a intentar sin que el usuario tenga que hacer nada: la llaman el arranque
        // antes de la primera pantalla, el login al entrar a la ventana principal y al
        // volver de ella, y el selector de idioma al abrirse. Si prospera, aplica el idioma
        // recordado y actualiza las pantallas abiertas (patron Observer). No lanza.
        internal static void ReintentarIdiomas_704ILR(string momento_704ILR)
        {
            if (!EvenTech.BLL.BLL_Idioma_704ILR.CargaPendiente_704ILR) return;
            if (!EvenTech.BLL.BLL_Idioma_704ILR.ReintentarCarga_704ILR(momento_704ILR)) return;
            AplicarIdiomaGuardado_704ILR();
            GestorDeIdioma_704ILR.GetInstance_704ILR.Refrescar_704ILR();
        }

        // Idioma recordado en login.cfg. Se aplica solo si es uno de los idiomas cargados:
        // un codigo que ya no existe (un idioma creado desde la aplicacion y un respaldo
        // restaurado despues) quedaba activo, con los textos en el idioma por defecto y el
        // selector sin ningun idioma marcado. En ese caso la aplicacion sigue en el idioma
        // por defecto y login.cfg se corrige. Sin idiomas cargados (la carga fallo) no se
        // puede saber si el codigo existe: se decide cuando el reintento de la carga prospera.
        private static void AplicarIdiomaGuardado_704ILR()
        {
            try
            {
                LoginPrefs_704ILR.Load_704ILR();
                string codigo_704ILR = (LoginPrefs_704ILR.Idioma_704ILR ?? string.Empty).Trim();
                if (codigo_704ILR.Length == 0) return;

                var gestor_704ILR = GestorDeIdioma_704ILR.GetInstance_704ILR;
                if (gestor_704ILR.IdiomasDisponibles_704ILR.Count == 0) return;

                foreach (BE_Idioma_704ILR idioma_704ILR in gestor_704ILR.IdiomasDisponibles_704ILR)
                {
                    if (string.Equals(idioma_704ILR.Codigo_704ILR, codigo_704ILR, StringComparison.OrdinalIgnoreCase))
                    {
                        gestor_704ILR.CambiarIdioma_704ILR(idioma_704ILR.Codigo_704ILR);
                        return;
                    }
                }
                LoginPrefs_704ILR.GuardarIdioma_704ILR(gestor_704ILR.IdiomaActual_704ILR);
            }
            catch { /* preferencia no critica: se arranca en el idioma por defecto */ }
        }
    }
}
