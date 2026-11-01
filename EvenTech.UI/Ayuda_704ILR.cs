using System;
using System.IO;
using System.Windows.Forms;

namespace EvenTech.UI
{
    // Pantalla o seccion que declara su tema de la ayuda en linea: F1 abre ese tema.
    public interface IAyudaContextual_704ILR
    {
        string TemaAyuda_704ILR { get; }
    }

    // Ayuda en linea (D02). El manual de usuario esta compilado en formato HTML Help
    // (Ayuda\EvenTech.chm, junto al ejecutable) y se abre con el visor de ayuda de
    // Windows por medio de Help.ShowHelp, de WinForms: no hace falta ningun componente
    // adicional. Se llega de tres maneras: el item Ayuda del menu lateral (abre el
    // manual en su primera pagina), la tecla F1 en cualquier seccion o dialogo (abre el tema de esa pantalla)
    // y las descripciones emergentes del menu, la version y los botones del informe.
    //
    // Cada tema es una pagina del manual; el nombre de la constante es el de la pagina.
    // La prueba automatizada comprueba que cada tema exista dentro del archivo compilado.
    public static class Ayuda_704ILR
    {
        public const string Carpeta_704ILR = "Ayuda";
        public const string Archivo_704ILR = "EvenTech.chm";

        public const string TemaInicio_704ILR = "inicio";
        public const string TemaPrimerosPasos_704ILR = "primeros_pasos";
        public const string TemaReservas_704ILR = "reservas";
        public const string TemaDisponibilidad_704ILR = "disponibilidad";
        public const string TemaServiciosReserva_704ILR = "servicios_reserva";
        public const string TemaPagos_704ILR = "pagos";
        public const string TemaHistorial_704ILR = "historial";
        public const string TemaClientes_704ILR = "clientes";
        public const string TemaServicios_704ILR = "servicios";
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
            TemaServicios_704ILR, TemaOperaciones_704ILR, TemaAsignarPersonal_704ILR, TemaCronograma_704ILR,
            TemaTareas_704ILR, TemaSupervision_704ILR, TemaAgenda_704ILR, TemaEmpleados_704ILR,
            TemaPerfiles_704ILR, TemaAuditoria_704ILR, TemaInformes_704ILR, TemaIdiomas_704ILR,
            TemaConexion_704ILR, TemaProblemas_704ILR
        };

        public static string Ruta_704ILR => Path.Combine(AppContext.BaseDirectory, Carpeta_704ILR, Archivo_704ILR);

        public static bool Disponible_704ILR => File.Exists(Ruta_704ILR);

        // Pagina del tema dentro del archivo compilado.
        public static string Pagina_704ILR(string tema_704ILR) => (string.IsNullOrWhiteSpace(tema_704ILR) ? TemaInicio_704ILR : tema_704ILR) + ".htm";

        // Abre el tema pedido. Sin el archivo de ayuda (instalacion incompleta) lo informa
        // en lugar de que el visor de Windows muestre un error propio.
        public static void Mostrar_704ILR(Control duenio_704ILR, string tema_704ILR)
        {
            if (!Disponible_704ILR)
            {
                MessageBox.Show(duenio_704ILR,
                    Tr_704ILR.F_704ILR("AYU_NO_DISPONIBLE", "La ayuda en línea no está disponible: falta el archivo {0}.", Ruta_704ILR),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Help.ShowHelp(duenio_704ILR, Ruta_704ILR, HelpNavigator.Topic, Pagina_704ILR(tema_704ILR));
        }

        // Tema de una pantalla: el que declara el control o, si no declara ninguno, el del
        // primer contenedor que si lo hace; en ultimo caso, el indice.
        public static string TemaDe_704ILR(Control control_704ILR)
        {
            for (Control c_704ILR = control_704ILR; c_704ILR != null; c_704ILR = c_704ILR.Parent)
                if (c_704ILR is IAyudaContextual_704ILR a_704ILR && !string.IsNullOrWhiteSpace(a_704ILR.TemaAyuda_704ILR))
                    return a_704ILR.TemaAyuda_704ILR;
            return TemaInicio_704ILR;
        }

        // Descripcion emergente de un control, traducida (D02). Comparte un ToolTip por
        // ventana, que se crea con la primera descripcion y se libera con la ventana.
        public static void Describir_704ILR(ToolTip tip_704ILR, Control control_704ILR, string clave_704ILR, string defecto_704ILR)
        {
            if (tip_704ILR == null || control_704ILR == null) return;
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            tip_704ILR.SetToolTip(control_704ILR, t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR);
        }
    }
}
