using System;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace EvenTech.UI
{
    // Pantalla o seccion que declara su tema de la ayuda en linea: F1 y el boton "?" de la
    // barra de titulo abren ese tema (o ese apartado: "tema#apartado").
    public interface IAyudaContextual_704ILR
    {
        string TemaAyuda_704ILR { get; }
    }

    // Ayuda en linea (D02). El manual de usuario esta compilado en formato HTML Help
    // (Ayuda\EvenTech.chm, junto al ejecutable) y se abre con el visor de ayuda de
    // Windows por medio de Help.ShowHelp, de WinForms: no hace falta ningun componente
    // adicional. Se llega de cuatro maneras:
    //  * el boton "?" de la barra de titulo de cada ventana (la principal y cada dialogo):
    //    abre el tema de la pantalla en uso;
    //  * el boton "?" junto a un apartado de una pantalla (la ficha, los filtros, una
    //    pestana...): abre la guia directamente en ese apartado;
    //  * la tecla F1: abre el apartado de la parte de la pantalla que tiene el foco o, si
    //    esa parte no declara ninguno, el tema de la pantalla;
    //  * el item Ayuda del menu lateral: abre el manual en su primera pagina.
    // Ademas, las descripciones emergentes del menu, la version y los botones del informe.
    //
    // Un destino es un tema ("reservas", la pagina reservas.htm) o un apartado de un tema
    // ("reservas#estados": el subtitulo con id="estados" de esa pagina). El visor abre la
    // pagina y la desplaza hasta el apartado. Cada constante Tema* es una pagina y cada
    // Apartado* un subtitulo; la prueba automatizada comprueba que todos existan dentro
    // del archivo compilado.
    public static class Ayuda_704ILR
    {
        public const string Carpeta_704ILR = "Ayuda";
        public const string Archivo_704ILR = "EvenTech.chm";
        public const char SeparadorApartado_704ILR = '#';

        public const string TemaInicio_704ILR = "inicio";
        public const string TemaPrimerosPasos_704ILR = "primeros_pasos";
        public const string TemaReservas_704ILR = "reservas";
        public const string TemaDisponibilidad_704ILR = "disponibilidad";
        public const string TemaServiciosReserva_704ILR = "servicios_reserva";
        public const string TemaPagos_704ILR = "pagos";
        public const string TemaHistorial_704ILR = "historial";
        public const string TemaClientes_704ILR = "clientes";
        public const string TemaServicios_704ILR = "servicios";
        public const string TemaBitacoraCambios_704ILR = "bitacora_cambios";
        public const string TemaOperaciones_704ILR = "operaciones";
        public const string TemaAsignarPersonal_704ILR = "asignar_personal";
        public const string TemaCronograma_704ILR = "cronograma";
        public const string TemaTareas_704ILR = "tareas";
        public const string TemaSupervision_704ILR = "supervision";
        public const string TemaAgenda_704ILR = "agenda";
        public const string TemaEmpleados_704ILR = "empleados";
        public const string TemaPerfiles_704ILR = "perfiles";
        public const string TemaAuditoria_704ILR = "auditoria";
        public const string TemaInformes_704ILR = "informes";
        public const string TemaIdiomas_704ILR = "idiomas";
        public const string TemaConexion_704ILR = "conexion";
        public const string TemaProblemas_704ILR = "problemas";

        // Todos los temas que la aplicacion puede pedir (los controla la prueba).
        public static readonly string[] Temas_704ILR =
        {
            TemaInicio_704ILR, TemaPrimerosPasos_704ILR, TemaReservas_704ILR, TemaDisponibilidad_704ILR,
            TemaServiciosReserva_704ILR, TemaPagos_704ILR, TemaHistorial_704ILR, TemaClientes_704ILR,
            TemaServicios_704ILR, TemaBitacoraCambios_704ILR, TemaOperaciones_704ILR, TemaAsignarPersonal_704ILR, TemaCronograma_704ILR,
            TemaTareas_704ILR, TemaSupervision_704ILR, TemaAgenda_704ILR, TemaEmpleados_704ILR,
            TemaPerfiles_704ILR, TemaAuditoria_704ILR, TemaInformes_704ILR, TemaIdiomas_704ILR,
            TemaConexion_704ILR, TemaProblemas_704ILR
        };

        // Apartados: "tema#id del subtitulo". El id lo fija el contenido de la ayuda y no
        // cambia aunque cambie el texto del subtitulo.
        public const string ApartadoIngreso_704ILR = "primeros_pasos#ingreso";
        public const string ApartadoCuentaNueva_704ILR = "primeros_pasos#cuenta-nueva";
        public const string ApartadoIdioma_704ILR = "primeros_pasos#idioma";
        public const string ApartadoVentanaPrincipal_704ILR = "primeros_pasos#ventana-principal";
        public const string ApartadoReservasGenerar_704ILR = "reservas#generar";
        public const string ApartadoReservasEstados_704ILR = "reservas#estados";
        public const string ApartadoReservasDocumentacion_704ILR = "reservas#documentacion";
        public const string ApartadoPagosRegistrar_704ILR = "pagos#registrar";
        public const string ApartadoPagosAnular_704ILR = "pagos#anular";
        public const string ApartadoHistorialCambios_704ILR = "historial#cambios";
        public const string ApartadoHistorialVersiones_704ILR = "historial#versiones";
        public const string ApartadoClientesAlta_704ILR = "clientes#alta";
        public const string ApartadoServiciosAlta_704ILR = "servicios#alta";
        public const string ApartadoBitacoraCambiosBuscar_704ILR = "bitacora_cambios#buscar";
        public const string ApartadoBitacoraCambiosActivar_704ILR = "bitacora_cambios#activar";
        public const string ApartadoOperacionesEstados_704ILR = "operaciones#estados";
        public const string ApartadoOperacionesEvento_704ILR = "operaciones#evento";
        public const string ApartadoSupervisionEjecucion_704ILR = "supervision#ejecucion";
        public const string ApartadoSupervisionIncidencias_704ILR = "supervision#incidencias";
        public const string ApartadoAgendaResponder_704ILR = "agenda#responder";
        public const string ApartadoAgendaConsultar_704ILR = "agenda#consultar";
        public const string ApartadoEmpleadosAlta_704ILR = "empleados#alta";
        public const string ApartadoInformesGenerar_704ILR = "informes#generar";
        public const string ApartadoInformesContenido_704ILR = "informes#contenido";
        public const string ApartadoInformesExportar_704ILR = "informes#exportar";
        public const string ApartadoPerfilesPermisos_704ILR = "perfiles#permisos";
        public const string ApartadoPerfilesCuentas_704ILR = "perfiles#cuentas";
        public const string ApartadoAuditoriaBitacora_704ILR = "auditoria#bitacora";
        public const string ApartadoAuditoriaLogin_704ILR = "auditoria#login";
        public const string ApartadoAuditoriaLineaBase_704ILR = "auditoria#linea-base";
        public const string ApartadoAuditoriaRespaldos_704ILR = "auditoria#respaldos";
        public const string ApartadoAuditoriaExcepciones_704ILR = "auditoria#excepciones";
        public const string ApartadoIdiomasTraducir_704ILR = "idiomas#traducir";
        public const string ApartadoIdiomasCrear_704ILR = "idiomas#crear";
        public const string ApartadoProblemasIntegridad_704ILR = "problemas#integridad";

        // Todos los apartados que la aplicacion puede pedir (los controla la prueba).
        public static readonly string[] Apartados_704ILR =
        {
            ApartadoIngreso_704ILR, ApartadoCuentaNueva_704ILR, ApartadoIdioma_704ILR, ApartadoVentanaPrincipal_704ILR,
            ApartadoReservasGenerar_704ILR, ApartadoReservasEstados_704ILR, ApartadoReservasDocumentacion_704ILR,
            ApartadoPagosRegistrar_704ILR, ApartadoPagosAnular_704ILR,
            ApartadoHistorialCambios_704ILR, ApartadoHistorialVersiones_704ILR, ApartadoClientesAlta_704ILR,
            ApartadoServiciosAlta_704ILR, ApartadoBitacoraCambiosBuscar_704ILR, ApartadoBitacoraCambiosActivar_704ILR,
            ApartadoOperacionesEstados_704ILR, ApartadoOperacionesEvento_704ILR,
            ApartadoSupervisionEjecucion_704ILR, ApartadoSupervisionIncidencias_704ILR,
            ApartadoAgendaResponder_704ILR, ApartadoAgendaConsultar_704ILR, ApartadoEmpleadosAlta_704ILR,
            ApartadoInformesGenerar_704ILR, ApartadoInformesContenido_704ILR, ApartadoInformesExportar_704ILR,
            ApartadoPerfilesPermisos_704ILR, ApartadoPerfilesCuentas_704ILR,
            ApartadoAuditoriaBitacora_704ILR, ApartadoAuditoriaLogin_704ILR, ApartadoAuditoriaLineaBase_704ILR,
            ApartadoAuditoriaRespaldos_704ILR, ApartadoAuditoriaExcepciones_704ILR,
            ApartadoIdiomasTraducir_704ILR, ApartadoIdiomasCrear_704ILR, ApartadoProblemasIntegridad_704ILR
        };

        public static string Ruta_704ILR => Path.Combine(AppContext.BaseDirectory, Carpeta_704ILR, Archivo_704ILR);

        public static bool Disponible_704ILR => File.Exists(Ruta_704ILR);

        // Tema de un destino ("reservas#estados" -> "reservas"); vacio = el indice.
        public static string TemaDelDestino_704ILR(string destino_704ILR)
        {
            if (string.IsNullOrWhiteSpace(destino_704ILR)) return TemaInicio_704ILR;
            int i_704ILR = destino_704ILR.IndexOf(SeparadorApartado_704ILR);
            return i_704ILR < 0 ? destino_704ILR : destino_704ILR.Substring(0, i_704ILR);
        }

        // Id del apartado de un destino ("reservas#estados" -> "estados"); null si es un tema.
        public static string ApartadoDelDestino_704ILR(string destino_704ILR)
        {
            int i_704ILR = destino_704ILR?.IndexOf(SeparadorApartado_704ILR) ?? -1;
            return i_704ILR < 0 ? null : destino_704ILR.Substring(i_704ILR + 1);
        }

        // Pagina del destino dentro del archivo compilado, con el ancla del apartado:
        // "reservas" -> "reservas.htm"; "reservas#estados" -> "reservas.htm#estados".
        public static string Pagina_704ILR(string destino_704ILR)
        {
            string ancla_704ILR = ApartadoDelDestino_704ILR(destino_704ILR);
            return TemaDelDestino_704ILR(destino_704ILR) + ".htm" + (string.IsNullOrEmpty(ancla_704ILR) ? "" : SeparadorApartado_704ILR + ancla_704ILR);
        }

        // Para las pruebas de pantallas: con un receptor asignado, pedir la ayuda no abre
        // el visor de Windows sino que le entrega el destino al receptor.
        public static Action<Control, string> ReceptorDePrueba_704ILR { get; set; }

        // Abre el destino pedido (un tema o un apartado). Sin el archivo de ayuda
        // (instalacion incompleta) lo informa en lugar de que el visor de Windows
        // muestre un error propio.
        public static void Mostrar_704ILR(Control duenio_704ILR, string destino_704ILR)
        {
            Control ventana_704ILR = duenio_704ILR?.FindForm() ?? duenio_704ILR;
            if (ReceptorDePrueba_704ILR != null) { ReceptorDePrueba_704ILR(ventana_704ILR, destino_704ILR); return; }
            if (!Disponible_704ILR)
            {
                MessageBox.Show(ventana_704ILR,
                    Tr_704ILR.F_704ILR("AYU_NO_DISPONIBLE", "La ayuda en línea no está disponible: falta el archivo {0}.", Ruta_704ILR),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Help.ShowHelp(ventana_704ILR, Ruta_704ILR, HelpNavigator.Topic, Pagina_704ILR(destino_704ILR));
        }

        // ---------- Zonas: la parte de una pantalla que corresponde a un apartado ----------

        // Cada zona es un control (un panel, una grilla, un campo) con el apartado que la
        // explica. La tabla no retiene los controles: una zona liberada desaparece sola.
        private static readonly ConditionalWeakTable<Control, string> _zonas_704ILR = new ConditionalWeakTable<Control, string>();

        public static void Zona_704ILR(Control zona_704ILR, string destino_704ILR)
        {
            if (zona_704ILR == null || string.IsNullOrWhiteSpace(destino_704ILR)) return;
            _zonas_704ILR.AddOrUpdate(zona_704ILR, destino_704ILR);
        }

        // Destino de la ayuda para un control: el de la zona que lo contiene (la mas cercana)
        // o, si ninguna lo declara, el de la pantalla o seccion que lo contiene; en ultimo
        // caso, el que se indique (el tema de la ventana).
        public static string DestinoDe_704ILR(Control control_704ILR, string porDefecto_704ILR = TemaInicio_704ILR)
        {
            for (Control c_704ILR = control_704ILR; c_704ILR != null; c_704ILR = c_704ILR.Parent)
            {
                if (_zonas_704ILR.TryGetValue(c_704ILR, out string d_704ILR)) return d_704ILR;
                if (c_704ILR is IAyudaContextual_704ILR a_704ILR && !string.IsNullOrWhiteSpace(a_704ILR.TemaAyuda_704ILR))
                    return a_704ILR.TemaAyuda_704ILR;
            }
            return string.IsNullOrWhiteSpace(porDefecto_704ILR) ? TemaInicio_704ILR : porDefecto_704ILR;
        }

        // Control que tiene el foco dentro de una ventana, bajando por los contenedores
        // (una seccion es un UserControl con su propio control activo).
        public static Control Enfocado_704ILR(ContainerControl ventana_704ILR)
        {
            Control c_704ILR = ventana_704ILR?.ActiveControl;
            while (c_704ILR is ContainerControl cc_704ILR && cc_704ILR.ActiveControl != null) c_704ILR = cc_704ILR.ActiveControl;
            return c_704ILR;
        }

        // ---------- Boton "?" junto a un apartado ----------

        // Pone el boton "?" a continuacion del texto de un rotulo (el titulo de la ficha, el
        // de una pestana o el de un campo) y declara la zona del apartado para F1. El rotulo
        // reserva a su derecha el lugar del boton; el boton sigue al texto cuando el texto
        // cambia (idioma, "Nuevo"/"Editar"). Va en rotulos de UNA linea: en uno que parte el
        // texto en varias, el boton quedaria sobre el texto.
        public static BotonAyuda_704ILR JuntoA_704ILR(Label rotulo_704ILR, string destino_704ILR, Control zona_704ILR = null)
        {
            var b_704ILR = new BotonAyuda_704ILR(destino_704ILR);
            if (rotulo_704ILR == null) return b_704ILR;
            var p_704ILR = rotulo_704ILR.Padding;
            rotulo_704ILR.Padding = new Padding(p_704ILR.Left, p_704ILR.Top, p_704ILR.Right + b_704ILR.Width + 4, p_704ILR.Bottom);
            rotulo_704ILR.Controls.Add(b_704ILR);
            void Ubicar_704ILR(object s_704ILR, EventArgs e_704ILR)
            {
                Size t_704ILR = TextRenderer.MeasureText(rotulo_704ILR.Text ?? "", rotulo_704ILR.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
                // El boton no es mas alto que la linea de texto: el rotulo de un campo (letra
                // chica) mide 17 px y un boton de 18 quedaba cortado abajo.
                int lado_704ILR = Math.Max(14, Math.Min(18, t_704ILR.Height));
                if (b_704ILR.Height != lado_704ILR) b_704ILR.Size = new Size(lado_704ILR, lado_704ILR);
                int x_704ILR = Math.Min(rotulo_704ILR.Padding.Left + t_704ILR.Width, Math.Max(0, rotulo_704ILR.Width - b_704ILR.Width));
                bool alMedio_704ILR = rotulo_704ILR.TextAlign == ContentAlignment.MiddleLeft || rotulo_704ILR.TextAlign == ContentAlignment.MiddleCenter;
                int y_704ILR = alMedio_704ILR
                    ? (rotulo_704ILR.Height - b_704ILR.Height) / 2
                    : rotulo_704ILR.Padding.Top + Math.Max(0, (t_704ILR.Height - b_704ILR.Height) / 2);
                b_704ILR.Location = new Point(x_704ILR, Math.Max(0, y_704ILR));
            }
            rotulo_704ILR.TextChanged += Ubicar_704ILR;
            rotulo_704ILR.FontChanged += Ubicar_704ILR;
            rotulo_704ILR.Resize += Ubicar_704ILR;
            Ubicar_704ILR(null, EventArgs.Empty);
            Zona_704ILR(zona_704ILR, destino_704ILR);
            return b_704ILR;
        }

        // Rotulo (caption) de un campo armado con Ui_704ILR.Field_704ILR.
        public static Label RotuloDeCampo_704ILR(TableLayoutPanel campo_704ILR) => campo_704ILR?.GetControlFromPosition(0, 0) as Label;

        // Descripcion emergente de un control, traducida (D02). Comparte un ToolTip por
        // ventana, que se crea con la primera descripcion y se libera con la ventana.
        public static void Describir_704ILR(ToolTip tip_704ILR, Control control_704ILR, string clave_704ILR, string defecto_704ILR)
        {
            if (tip_704ILR == null || control_704ILR == null) return;
            tip_704ILR.SetToolTip(control_704ILR, Texto_704ILR(clave_704ILR, defecto_704ILR));
        }

        internal static string Texto_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }

    // Boton "?" de un apartado: un circulo con el signo de pregunta, en el color de acento.
    // Un clic abre la guia en su destino. La descripcion emergente se lee al pasar el
    // mouse, asi que sigue al idioma activo.
    public sealed class BotonAyuda_704ILR : Label
    {
        private static readonly Font FuenteGlifo_704ILR = new Font("Segoe MDL2 Assets", 10F);
        private readonly ToolTip _tip_704ILR = new ToolTip();

        public string Destino_704ILR { get; }

        public BotonAyuda_704ILR(string destino_704ILR)
        {
            Destino_704ILR = destino_704ILR;
            Text = Theme_704ILR.IcoAyudaApartado_704ILR;
            Font = FuenteGlifo_704ILR;
            ForeColor = Theme_704ILR.AccentButton_704ILR;
            BackColor = Color.Transparent;
            AutoSize = false;
            Size = new Size(18, 18);
            TextAlign = ContentAlignment.MiddleCenter;
            Cursor = Cursors.Hand;
            Margin = new Padding(0);
            AccessibleName = "Ayuda";
            AccessibleRole = AccessibleRole.HelpBalloon;
            MouseEnter += (s_704ILR, e_704ILR) =>
            {
                ForeColor = Theme_704ILR.AccentButtonHover_704ILR;
                _tip_704ILR.SetToolTip(this, Ayuda_704ILR.Texto_704ILR("AYU_TT_APARTADO", "Ayuda sobre esta parte de la pantalla."));
            };
            MouseLeave += (s_704ILR, e_704ILR) => ForeColor = Theme_704ILR.AccentButton_704ILR;
            Click += (s_704ILR, e_704ILR) => Abrir_704ILR();
        }

        public void Abrir_704ILR() => Ayuda_704ILR.Mostrar_704ILR(this, Destino_704ILR);

        protected override void Dispose(bool disposing_704ILR)
        {
            if (disposing_704ILR) _tip_704ILR.Dispose();
            base.Dispose(disposing_704ILR);
        }
    }
}
