using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Vista principal post-login (shell). Borderless.
    //   - Sidebar (BgSidebar) con logo + saludo y items icono+texto con indicador
    //     de activo.
    //   - Topbar (BgTitleBar) con titulo de seccion, salir y botones de ventana.
    //   - Panel central (BgContent) que hospeda UserControls (Dock=Fill).
    //   - Pie (footer) con el selector de idioma + alta rapida, abajo a la derecha.
    // Layout por Dock + TableLayoutPanel, sin coordenadas magicas. Las medidas son de
    // 96 DPI: con la escala de Windows por encima de 100 % el sistema escala la ventana
    // entera (la aplicacion no declara conciencia de DPI, ver EvenTech.UI.csproj).
    public class frmMain_704ILR : FormBase_704ILR, IObservadorIdioma_704ILR
    {
        private Panel _pnlContent_704ILR;
        private Label _lblPageTitle_704ILR, _lblWelcome_704ILR;
        private AppButton_704ILR _btnLogout_704ILR;
        private LangSelector_704ILR _lang_704ILR;
        private Label _btnMax_704ILR;

        private SideMenuItem_704ILR _itInicio_704ILR, _itReservas_704ILR, _itClientes_704ILR, _itServicios_704ILR, _itPerfiles_704ILR, _itAuditoria_704ILR;
        // Proceso 2: operaciones de los eventos, agenda del empleado y personal.
        private SideMenuItem_704ILR _itOperaciones_704ILR, _itAgenda_704ILR, _itEmpleados_704ILR;
        private SideMenuItem_704ILR _activo_704ILR;
        private readonly List<SideMenuItem_704ILR> _items_704ILR = new List<SideMenuItem_704ILR>();

        // Hay una pregunta de descarte a la vista (al cerrar o al cambiar de seccion). Mientras
        // tanto, un segundo pedido de cierre sobre un cierre en curso ("Finalizar tarea" o
        // taskkill sin /F) no vuelve a preguntar: decide la pregunta que ya esta abierta.
        private bool _preguntandoDescarte_704ILR;

        // La ventana acepto cerrarse y la sesion termino: no se arma ninguna vista mas.
        private bool _cerrando_704ILR;

        public frmMain_704ILR()
        {
            BuildUi_704ILR();
            bool bloqueado_704ILR = SessionManager_704ILR.IsSessionActive_704ILR && SessionManager_704ILR.GetInstance_704ILR.SinPerfil_704ILR;
            if (bloqueado_704ILR)
            {
                foreach (var it_704ILR in _items_704ILR) it_704ILR.Visible = false;
                // Sin perfil no hay ningun permiso: el globo no debe ofrecer el ABM
                // de idiomas (se construye con allowManage:true por defecto).
                if (_lang_704ILR != null) _lang_704ILR.PermitirGestion_704ILR = false;
            }
            else AplicarPermisos_704ILR();
            ActualizarTextos_704ILR();              // si esta bloqueado, muestra el mensaje
            if (!bloqueado_704ILR) Navegar_704ILR(_itInicio_704ILR);
            GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            AvisarPermisosNoDisponibles_704ILR();
        }

        // Si los permisos del perfil no se pudieron resolver, la sesion quedo sin
        // ninguno (denegar por defecto). Se avisa para que el usuario entienda por
        // que no ve sus secciones y no lo confunda con una baja de permisos.
        private void AvisarPermisosNoDisponibles_704ILR()
        {
            if (!SessionManager_704ILR.IsSessionActive_704ILR || !SessionManager_704ILR.GetInstance_704ILR.PermisosNoDisponibles_704ILR) return;
            Shown += (s_704ILR, e_704ILR) => MessageBox.Show(this,
                T_704ILR("MAIN_PERMISOS_ERROR",
                  "No se pudieron cargar los permisos de tu perfil, así que la sesión quedó sin acceso a las secciones. " +
                  "Volvé a iniciar sesión; si el problema sigue, avisale a un administrador."),
                "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Permisos que habilitan cada seccion del menu. Es la UNICA fuente para las
        // dos capas del control de acceso (AplicarPermisos y Navegar): antes eran
        // dos literales repetidos y podian desincronizarse.
        // Una seccion se abre con CUALQUIER hoja cuya accion viva adentro: consultar
        // disponibilidad, cobrar/anular pagos y restaurar versiones se hacen desde
        // Reservas, y el recalculo de la linea base desde Auditoria. Sin esas hojas
        // en la compuerta, un perfil que solo las tuviera no llegaba a ninguna
        // pantalla. Entrar no concede nada: cada accion exige su propio permiso al
        // ejecutarse (segunda capa) y la ficha deshabilita lo que no corresponde.
        private static readonly string[] PermisosReservas_704ILR =
        {
            "RESERVA_CREAR", "RESERVA_EDITAR", "RESERVA_HISTORIAL",
            "DISPONIBILIDAD_CONSULTAR", "PAGOS_REGISTRAR", "PAGOS_ANULAR", "RESERVA_RESTAURAR"
        };
        private static readonly string[] PermisosClientes_704ILR  = { "CLIENTES_GESTION" };
        private static readonly string[] PermisosServicios_704ILR = { "SERVICIOS_GESTION" };
        private static readonly string[] PermisosPerfiles_704ILR  = { "PERFILES_GESTION" };
        private static readonly string[] PermisosAuditoria_704ILR = { "BITACORA_VER", "AUDIT_LOGIN_VER", "INTEGRIDAD_RECALC" };
        // Proceso 2. Operaciones reune los pasos del coordinador y la supervision de la
        // ejecucion; Mi agenda es la vista del empleado sobre sus propios turnos.
        private static readonly string[] PermisosOperaciones_704ILR = { "PERSONAL_ASIGNAR", "CRONOGRAMA_GESTION", "TAREAS_ASIGNAR", "EJECUCION_SUPERVISAR" };
        private static readonly string[] PermisosAgenda_704ILR      = { "DISPONIBILIDAD_CONFIRMAR", "AGENDA_CONSULTAR" };
        private static readonly string[] PermisosEmpleados_704ILR   = { "EMPLEADOS_GESTION" };

        // Control de acceso (T04), primera capa: muestra/oculta cada seccion segun
        // los permisos efectivos del perfil. Toda seccion exige su permiso; la
        // unica sin restriccion es Inicio (portada de la sesion). La segunda capa
        // vive en Navegar(), que vuelve a exigir el permiso al abrir la vista.
        private void AplicarPermisos_704ILR()
        {
            if (!SessionManager_704ILR.IsSessionActive_704ILR) return;
            _itReservas_704ILR.Visible  = Permisos_704ILR.TieneAlguno_704ILR(PermisosReservas_704ILR);
            _itClientes_704ILR.Visible  = Permisos_704ILR.TieneAlguno_704ILR(PermisosClientes_704ILR);
            _itServicios_704ILR.Visible = Permisos_704ILR.TieneAlguno_704ILR(PermisosServicios_704ILR);
            _itPerfiles_704ILR.Visible  = Permisos_704ILR.TieneAlguno_704ILR(PermisosPerfiles_704ILR);
            _itAuditoria_704ILR.Visible = Permisos_704ILR.TieneAlguno_704ILR(PermisosAuditoria_704ILR);
            _itOperaciones_704ILR.Visible = Permisos_704ILR.TieneAlguno_704ILR(PermisosOperaciones_704ILR);
            _itAgenda_704ILR.Visible      = Permisos_704ILR.TieneAlguno_704ILR(PermisosAgenda_704ILR);
            _itEmpleados_704ILR.Visible   = Permisos_704ILR.TieneAlguno_704ILR(PermisosEmpleados_704ILR);
            // La gestion de idiomas (ABM de traducciones) cuelga del globo del pie.
            if (_lang_704ILR != null) _lang_704ILR.PermitirGestion_704ILR = Permisos_704ILR.Tiene_704ILR("IDIOMAS_GESTION");
        }

        // Permisos que habilitan cada seccion del menu (misma lista que usa la
        // primera capa: ver los campos de arriba).
        private string[] PermisosDe_704ILR(SideMenuItem_704ILR item_704ILR)
        {
            if (item_704ILR == _itReservas_704ILR)  return PermisosReservas_704ILR;
            if (item_704ILR == _itClientes_704ILR)  return PermisosClientes_704ILR;
            if (item_704ILR == _itServicios_704ILR) return PermisosServicios_704ILR;
            if (item_704ILR == _itPerfiles_704ILR)  return PermisosPerfiles_704ILR;
            if (item_704ILR == _itAuditoria_704ILR) return PermisosAuditoria_704ILR;
            if (item_704ILR == _itOperaciones_704ILR) return PermisosOperaciones_704ILR;
            if (item_704ILR == _itAgenda_704ILR)      return PermisosAgenda_704ILR;
            if (item_704ILR == _itEmpleados_704ILR)   return PermisosEmpleados_704ILR;
            return null;   // Inicio: sin restriccion
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            // Tamano por defecto acorde a la resolucion minima declarada en G05
            // (1366x768). El area de contenido resultante mide 1075 x 585 (ancho
            // menos el menu lateral de 232 y los margenes de 24+24; alto menos la
            // barra superior de 56, el pie de 46 y los margenes de 16+12): la seccion
            // de reservas se diseno para entrar COMPLETA ahi (grilla sin columnas
            // truncadas y ficha con sus siete campos y sus botones a la vista, sin
            // barra de desplazamiento), verificado midiendo el layout resultante.
            // La ventana ademas se puede maximizar y redimensionar, asi que en
            // pantallas mas grandes aprovecha el espacio; en una pantalla mas chica
            // que este tamano, al abrir se achica hasta el area de trabajo (OnLoad).
            // Con el mouse no se puede achicar por debajo de ese tamano: el minimo era
            // 1100x680 y ahi la grilla de reservas ocultaba Monto y Vence y la ficha pedia
            // desplazamiento. Si el area de trabajo es mas chica, manda el area (OnLoad).
            ClientSize = TamanoDiseno_704ILR;
            BackColor = Theme_704ILR.BgContent_704ILR;
            MinimumSize = TamanoDiseno_704ILR;
            Redimensionable_704ILR = true;

            // ---------------- Sidebar ----------------
            var pnlMenu_704ILR = new Panel { Dock = DockStyle.Left, Width = 232, BackColor = Theme_704ILR.BgSidebar_704ILR };

            var pnlLogo_704ILR = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Theme_704ILR.BgSidebar_704ILR };
            EnableDrag_704ILR(pnlLogo_704ILR);
            // Isologotipo del sistema en el extremo superior izquierdo, visible desde
            // cualquier seccion (G05). Si el recurso no estuviera disponible se cae al
            // rotulo de texto, de modo que la pantalla nunca queda sin identidad.
            Control lblLogo_704ILR;
            Image logoMenu_704ILR = LogoSinLema_704ILR(Theme_704ILR.Logo_704ILR);
            if (logoMenu_704ILR != null)
            {
                lblLogo_704ILR = new PictureBox
                {
                    Image = logoMenu_704ILR,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Dock = DockStyle.Top,
                    Height = 62,
                    BackColor = Color.Transparent
                };
            }
            else
            {
                lblLogo_704ILR = new Label
                {
                    Text = "EvenTech",
                    Font = Theme_704ILR.FontH1_704ILR,
                    ForeColor = Theme_704ILR.Accent_704ILR,
                    Dock = DockStyle.Top,
                    Height = 52,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
            }
            EnableDrag_704ILR(lblLogo_704ILR);
            // AutoEllipsis: un nombre largo (el alta admite 50 caracteres) se recorta con
            // puntos suspensivos y el nombre completo queda en la ayuda emergente. Sin
            // esto el texto partia en dos lineas y en los 22 px solo se veia el saludo.
            _lblWelcome_704ILR = new Label
            {
                Font = Theme_704ILR.FontCaption_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            EnableDrag_704ILR(_lblWelcome_704ILR);
            var sep_704ILR = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme_704ILR.SidebarHover_704ILR };
            pnlLogo_704ILR.Controls.Add(_lblWelcome_704ILR);
            pnlLogo_704ILR.Controls.Add(lblLogo_704ILR);
            pnlLogo_704ILR.Controls.Add(sep_704ILR);

            _itInicio_704ILR    = new SideMenuItem_704ILR(Theme_704ILR.IcoHome_704ILR,     "MENU_INICIO",    (s_704ILR, e_704ILR) => Navegar_704ILR(_itInicio_704ILR));
            _itReservas_704ILR  = new SideMenuItem_704ILR(Theme_704ILR.IcoCalendar_704ILR, "MENU_RESERVAS",  (s_704ILR, e_704ILR) => Navegar_704ILR(_itReservas_704ILR));
            _itClientes_704ILR  = new SideMenuItem_704ILR(Theme_704ILR.IcoContact_704ILR,  "MENU_CLIENTES",  (s_704ILR, e_704ILR) => Navegar_704ILR(_itClientes_704ILR));
            _itServicios_704ILR = new SideMenuItem_704ILR(Theme_704ILR.IcoServicio_704ILR, "MENU_SERVICIOS", (s_704ILR, e_704ILR) => Navegar_704ILR(_itServicios_704ILR));
            _itPerfiles_704ILR  = new SideMenuItem_704ILR(Theme_704ILR.IcoPeople_704ILR,   "MENU_PERFILES",  (s_704ILR, e_704ILR) => Navegar_704ILR(_itPerfiles_704ILR));
            _itAuditoria_704ILR = new SideMenuItem_704ILR(Theme_704ILR.IcoHistory_704ILR,  "MENU_AUDITORIA", (s_704ILR, e_704ILR) => Navegar_704ILR(_itAuditoria_704ILR));
            _itOperaciones_704ILR = new SideMenuItem_704ILR(Theme_704ILR.IcoOperaciones_704ILR, "MENU_OPERACIONES", (s_704ILR, e_704ILR) => Navegar_704ILR(_itOperaciones_704ILR));
            _itAgenda_704ILR      = new SideMenuItem_704ILR(Theme_704ILR.IcoAgenda_704ILR,      "MENU_AGENDA",      (s_704ILR, e_704ILR) => Navegar_704ILR(_itAgenda_704ILR));
            _itEmpleados_704ILR   = new SideMenuItem_704ILR(Theme_704ILR.IcoEmpleado_704ILR,    "MENU_EMPLEADOS",   (s_704ILR, e_704ILR) => Navegar_704ILR(_itEmpleados_704ILR));
            _items_704ILR.AddRange(new[] { _itInicio_704ILR, _itReservas_704ILR, _itClientes_704ILR, _itServicios_704ILR,
                                           _itOperaciones_704ILR, _itAgenda_704ILR, _itEmpleados_704ILR, _itPerfiles_704ILR, _itAuditoria_704ILR });

            // Dock=Top se apila en orden inverso al de agregado.
            // Idiomas salio del menu (se gestiona desde el globo del pie).
            pnlMenu_704ILR.Controls.Add(_itAuditoria_704ILR);
            pnlMenu_704ILR.Controls.Add(_itPerfiles_704ILR);
            pnlMenu_704ILR.Controls.Add(_itEmpleados_704ILR);
            pnlMenu_704ILR.Controls.Add(_itAgenda_704ILR);
            pnlMenu_704ILR.Controls.Add(_itOperaciones_704ILR);
            pnlMenu_704ILR.Controls.Add(_itServicios_704ILR);
            pnlMenu_704ILR.Controls.Add(_itClientes_704ILR);
            pnlMenu_704ILR.Controls.Add(_itReservas_704ILR);
            pnlMenu_704ILR.Controls.Add(_itInicio_704ILR);
            pnlMenu_704ILR.Controls.Add(pnlLogo_704ILR);

            // ---------------- Topbar ----------------
            var pnlTop_704ILR = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTop_704ILR);

            var topGrid_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            topGrid_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // titulo
            for (int i_704ILR = 0; i_704ILR < 4; i_704ILR++) topGrid_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topGrid_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            EnableDrag_704ILR(topGrid_704ILR);

            _lblPageTitle_704ILR = new Label
            {
                Font = Theme_704ILR.FontH2_704ILR,
                ForeColor = Theme_704ILR.TextOnDark_704ILR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, 0, 0, 0),
                BackColor = Color.Transparent
            };
            EnableDrag_704ILR(_lblPageTitle_704ILR);

            _btnLogout_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("MENU_SALIR", "Cerrar sesión"), Theme_704ILR.IcoLogout_704ILR);
            _btnLogout_704ILR.Size = new Size(150, 34);
            _btnLogout_704ILR.Anchor = AnchorStyles.Left;
            _btnLogout_704ILR.BehindColor_704ILR = Theme_704ILR.BgTitleBar_704ILR;
            _btnLogout_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            _btnLogout_704ILR.Click += (s_704ILR, e_704ILR) => DoLogout_704ILR();

            var btnMin_704ILR = WindowButton_704ILR(Theme_704ILR.IcoMinimize_704ILR, (s_704ILR, e_704ILR) => WindowState = FormWindowState.Minimized);
            btnMin_704ILR.Dock = DockStyle.Fill;
            btnMin_704ILR.Margin = new Padding(0);
            var btnMax_704ILR = WindowButton_704ILR(Theme_704ILR.IcoMaximize_704ILR, (s_704ILR, e_704ILR) =>
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized);
            btnMax_704ILR.Dock = DockStyle.Fill;
            btnMax_704ILR.Margin = new Padding(0);
            _btnMax_704ILR = btnMax_704ILR;
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => Close(), danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Fill;
            btnClose_704ILR.Margin = new Padding(0);

            topGrid_704ILR.Controls.Add(_lblPageTitle_704ILR, 0, 0);
            topGrid_704ILR.Controls.Add(_btnLogout_704ILR, 1, 0);
            topGrid_704ILR.Controls.Add(btnMin_704ILR, 2, 0);
            topGrid_704ILR.Controls.Add(btnMax_704ILR, 3, 0);
            topGrid_704ILR.Controls.Add(btnClose_704ILR, 4, 0);
            pnlTop_704ILR.Controls.Add(topGrid_704ILR);

            // ---------------- Pie (footer) con selector de idioma a la derecha ----------------
            var footer_704ILR = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(0, 0, Theme_704ILR.SpaceXl_704ILR, 0)
            };
            var footerSep_704ILR = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme_704ILR.Border_704ILR };
            var footerGrid_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            footerGrid_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footerGrid_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footerGrid_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _lang_704ILR = new LangSelector_704ILR(dark_704ILR: false, allowManage_704ILR: true) { Anchor = AnchorStyles.Right };
            footerGrid_704ILR.Controls.Add(_lang_704ILR, 1, 0);
            footer_704ILR.Controls.Add(footerGrid_704ILR);
            footer_704ILR.Controls.Add(footerSep_704ILR);

            // ---------------- Contenido ----------------
            _pnlContent_704ILR = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceMd_704ILR)
            };

            // Orden de Add: menu (Left) primero en docking, luego topbar (Top),
            // footer (Bottom) y por ultimo el contenido (Fill). Topbar y footer
            // abarcan solo la columna de contenido (a la derecha del menu).
            Controls.Add(_pnlContent_704ILR);
            Controls.Add(footer_704ILR);
            Controls.Add(pnlTop_704ILR);
            Controls.Add(pnlMenu_704ILR);
        }

        // -------- Navegacion --------
        // Clic en el menu lateral. Volver a pulsar la seccion que ya esta abierta la vuelve a
        // crear: es la forma de reintentar una carga que fallo (por ejemplo, con la base caida
        // al abrirla, el aviso pide reintentar y las vistas cargan solo al crearse). Si la
        // vista tiene cambios sin guardar, AbrirSeccion pregunta antes, igual que al cambiar
        // de seccion, y con "No" queda todo como estaba.
        private void Navegar_704ILR(SideMenuItem_704ILR item_704ILR)
        {
            AbrirSeccion_704ILR(item_704ILR);
        }

        // Arma la vista de la seccion en el panel central, aunque sea la activa (el cambio
        // de idioma la usa para rearmar la portada traducida).
        private void AbrirSeccion_704ILR(SideMenuItem_704ILR item_704ILR)
        {
            // Con la ventana cerrandose no se abre nada (ni se avisa de un permiso faltante).
            if (_cerrando_704ILR || IsDisposed || Disposing) return;

            // Segunda capa del control de acceso: el permiso se vuelve a exigir
            // aca y no solo al armar el menu. Si el item quedo visible por error
            // o la vista se alcanza por otra via, la navegacion se corta igual.
            string[] requeridos_704ILR = PermisosDe_704ILR(item_704ILR);
            if (requeridos_704ILR != null &&
                !Permisos_704ILR.ExigirAlguno_704ILR(this, "abrir la seccion " + TextoMenu_704ILR(item_704ILR.Key_704ILR), requeridos_704ILR))
                return;

            // La vista saliente puede tener ediciones sin guardar: se pregunta antes de
            // descartarlas y, si el usuario elige seguir editando, no se navega.
            bool conSesion_704ILR = SessionManager_704ILR.IsSessionActive_704ILR;
            if (!ConfirmarDescarteDeVista_704ILR()) return;

            // Con la pregunta abierta la ventana pudo empezar a cerrarse ("Finalizar tarea" no
            // pregunta) y la sesion terminar: armar la vista pedida la dejaba trabajando sin
            // sesion hasta que la ventana se cerraba.
            if (_cerrando_704ILR || IsDisposed || Disposing ||
                (conSesion_704ILR && !SessionManager_704ILR.IsSessionActive_704ILR))
                return;

            SetActive_704ILR(item_704ILR);
            LiberarContenido_704ILR();

            Control vista_704ILR;
            if (item_704ILR == _itInicio_704ILR)         vista_704ILR = BuildInicio_704ILR();
            else if (item_704ILR == _itReservas_704ILR)  vista_704ILR = new ucReservas_704ILR();
            else if (item_704ILR == _itClientes_704ILR)  vista_704ILR = new ucClientes_704ILR();
            else if (item_704ILR == _itServicios_704ILR) vista_704ILR = new ucServicios_704ILR();
            else if (item_704ILR == _itPerfiles_704ILR)  vista_704ILR = new ucPerfiles_704ILR();
            else if (item_704ILR == _itOperaciones_704ILR) vista_704ILR = new ucOperaciones_704ILR();
            else if (item_704ILR == _itAgenda_704ILR)      vista_704ILR = new ucMiAgenda_704ILR();
            else if (item_704ILR == _itEmpleados_704ILR)   vista_704ILR = new ucEmpleados_704ILR();
            else                           vista_704ILR = new ucAuditoriaHub_704ILR();

            vista_704ILR.Dock = DockStyle.Fill;
            _pnlContent_704ILR.Controls.Add(vista_704ILR);
        }

        // true si se puede descartar lo que ocupa el panel central: ninguna vista tiene
        // cambios sin guardar, o el usuario acepto descartarlos. Lo usan todas las salidas de
        // la vista: cambiar de seccion, volver a pulsar la activa, cerrar sesion y cerrar la
        // ventana. Pregunta a cualquier vista que implemente IVistaConCambios_704ILR.
        private bool ConfirmarDescarteDeVista_704ILR()
        {
            foreach (Control c_704ILR in _pnlContent_704ILR.Controls)
            {
                if (c_704ILR is IVistaConCambios_704ILR vista_704ILR && vista_704ILR.HayCambiosSinGuardar_704ILR)
                {
                    bool previo_704ILR = _preguntandoDescarte_704ILR;
                    _preguntandoDescarte_704ILR = true;
                    try
                    {
                        return MessageBox.Show(this,
                            T_704ILR("MAIN_CAMBIOS_SIN_GUARDAR", "Hay cambios sin guardar en la sección actual. ¿Descartarlos y continuar?"),
                            "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                    }
                    finally { _preguntandoDescarte_704ILR = previo_704ILR; }
                }
            }
            return true;
        }

        // Quita y LIBERA lo que ocupa el panel central. Sin Dispose la vista conservaba sus
        // ventanas nativas y su suscripcion al gestor de idioma (cada vista se desuscribe en
        // su Disposed): cada cambio de seccion dejaba viva la anterior y tras unos cientos el
        // proceso agotaba los handles de Windows y se cerraba (RNF-02).
        // Cada vista se libera SIN desprenderla antes: Dispose la quita del panel ya marcada
        // como en liberacion, y asi no recibe los avisos de un control que cambia de contenedor.
        // Con Controls.Clear() previo, la grilla de Reservas se volvia a enlazar al quedar sin
        // contenedor, pasaba a la primera fila y la ficha volvia a preguntar si descartaba los
        // cambios que el usuario acababa de aceptar descartar (y esa respuesta no decidia nada).
        private void LiberarContenido_704ILR()
        {
            var salientes_704ILR = new List<Control>();
            foreach (Control c_704ILR in _pnlContent_704ILR.Controls) salientes_704ILR.Add(c_704ILR);
            foreach (var c_704ILR in salientes_704ILR) c_704ILR.Dispose();
        }

        private void SetActive_704ILR(SideMenuItem_704ILR item_704ILR)
        {
            if (_activo_704ILR != null) _activo_704ILR.SetActive_704ILR(false);
            _activo_704ILR = item_704ILR;
            if (item_704ILR != null)
            {
                item_704ILR.SetActive_704ILR(true);
                _lblPageTitle_704ILR.Text = TextoMenu_704ILR(item_704ILR.Key_704ILR);
            }
        }

        private Control BuildInicio_704ILR()
        {
            var host_704ILR = new Panel { Dock = DockStyle.Fill, BackColor = Theme_704ILR.BgContent_704ILR };

            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Top,
                Height = 170,
                BehindColor_704ILR = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceXl_704ILR)
            };

            string usuario_704ILR = SessionManager_704ILR.IsSessionActive_704ILR ? SessionManager_704ILR.GetInstance_704ILR.User_704ILR.Username_704ILR : "?";
            var lblWelcome_704ILR = new Label
            {
                Text = T_704ILR("MAIN_WELCOME", "Bienvenido a EvenTech"),
                Font = Theme_704ILR.FontH1_704ILR,
                ForeColor = Theme_704ILR.TextOnLight_704ILR,
                AutoSize = true,
                Location = new Point(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR),
                BackColor = Color.Transparent
            };
            var lblBody_704ILR = new Label
            {
                Text = T_704ILR("MAIN_SESSION", "Sesión iniciada por:") + " " + usuario_704ILR + Environment.NewLine + Environment.NewLine +
                       T_704ILR("MAIN_SUBTITLE", "Usa el menú de la izquierda para gestionar el sistema."),
                Font = Theme_704ILR.FontBody_704ILR,
                ForeColor = Theme_704ILR.TextMuted_704ILR,
                AutoSize = true,
                MaximumSize = new Size(820, 0),
                Location = new Point(Theme_704ILR.SpaceXl_704ILR, 64),
                BackColor = Color.Transparent
            };
            card_704ILR.Controls.Add(lblBody_704ILR);
            card_704ILR.Controls.Add(lblWelcome_704ILR);

            host_704ILR.Controls.Add(card_704ILR);
            return host_704ILR;
        }

        // Variante del isologotipo para el menu lateral: isotipo + nombre, SIN la
        // bajada "GESTION DE EVENTOS". El recurso completo mide 720 px de alto y la
        // bajada ocupa 27 px de ellos: dibujado a los 62 px que tiene el panel del
        // menu quedaba en 2 px, una franja gris ilegible. Se recorta la franja
        // superior (icono + nombre) y el resto del alto lo aprovechan esos dos, que
        // se ven casi al doble de tamano. Las proporciones son las del lienzo del
        // recurso (icono desde el 20% del alto, nombre hasta el 70%).
        private static Image LogoSinLema_704ILR(Image logo_704ILR)
        {
            if (logo_704ILR == null) return null;
            try
            {
                int arriba_704ILR = (int)(logo_704ILR.Height * 0.18);
                int abajo_704ILR = (int)(logo_704ILR.Height * 0.72);
                var recorte_704ILR = new Rectangle(0, arriba_704ILR, logo_704ILR.Width, abajo_704ILR - arriba_704ILR);
                using (var bmp_704ILR = new Bitmap(logo_704ILR))
                    return bmp_704ILR.Clone(recorte_704ILR, bmp_704ILR.PixelFormat);
            }
            catch { return logo_704ILR; }   // ante cualquier falla, el logo completo
        }

        // Refresca el selector de idioma (lo usa ucIdiomas tras crear/editar idiomas).
        public void RefrescarIdiomas_704ILR() => _lang_704ILR?.Repopulate_704ILR();

        // Observador (patron Observer): refresca textos sin recrear el form.
        public void ActualizarTextos_704ILR()
        {
            foreach (var it_704ILR in _items_704ILR) it_704ILR.Caption_704ILR.Text = TextoMenu_704ILR(it_704ILR.Key_704ILR);
            if (_btnLogout_704ILR != null) _btnLogout_704ILR.Text = T_704ILR("MENU_SALIR", "Cerrar sesión");
            if (_lblWelcome_704ILR != null)
            {
                string usuario_704ILR = SessionManager_704ILR.IsSessionActive_704ILR ? SessionManager_704ILR.GetInstance_704ILR.User_704ILR.Username_704ILR : "?";
                _lblWelcome_704ILR.Text = T_704ILR("MAIN_HELLO", "Bienvenido") + " " + usuario_704ILR;
            }
            if (_activo_704ILR != null) _lblPageTitle_704ILR.Text = TextoMenu_704ILR(_activo_704ILR.Key_704ILR);

            if (SessionManager_704ILR.IsSessionActive_704ILR && SessionManager_704ILR.GetInstance_704ILR.SinPerfil_704ILR)
                MostrarBloqueado_704ILR();                       // usuario sin rol: pantalla bloqueada
            else if (_activo_704ILR == _itInicio_704ILR)
                AbrirSeccion_704ILR(_itInicio_704ILR);                  // recarga la portada traducida
        }

        // Pantalla para usuarios sin perfil asignado: mensaje + sin navegacion
        // (solo pueden cerrar sesion).
        private void MostrarBloqueado_704ILR()
        {
            _activo_704ILR = null;
            _lblPageTitle_704ILR.Text = "EvenTech";
            LiberarContenido_704ILR();

            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Top,
                Height = 150,
                BehindColor_704ILR = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR)
            };
            var icon_704ILR = new Label
            {
                Text = Theme_704ILR.IcoLock_704ILR,
                Font = new Font("Segoe MDL2 Assets", 26F),
                ForeColor = Theme_704ILR.Warning_704ILR,
                AutoSize = true,
                Location = new Point(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceXl_704ILR),
                BackColor = Color.Transparent
            };
            var lblTit_704ILR = new Label
            {
                Text = T_704ILR("MAIN_SIN_ROL_TIT", "Acceso restringido"),
                Font = Theme_704ILR.FontH1_704ILR,
                ForeColor = Theme_704ILR.TextOnLight_704ILR,
                AutoSize = true,
                Location = new Point(72, Theme_704ILR.SpaceLg_704ILR),
                BackColor = Color.Transparent
            };
            var lblMsg_704ILR = new Label
            {
                Text = T_704ILR("MAIN_SIN_ROL", "Tu cuenta todavía no tiene un perfil asignado. Contactate con un administrador para que te asigne uno."),
                Font = Theme_704ILR.FontBody_704ILR,
                ForeColor = Theme_704ILR.TextMuted_704ILR,
                AutoSize = false,
                Location = new Point(72, 60),
                Size = new Size(760, 50),
                BackColor = Color.Transparent
            };
            card_704ILR.Controls.Add(lblMsg_704ILR);
            card_704ILR.Controls.Add(lblTit_704ILR);
            card_704ILR.Controls.Add(icon_704ILR);
            _pnlContent_704ILR.Controls.Add(card_704ILR);
        }

        // Leyenda de un item del menu (y titulo de su pagina) con respaldo: si la carga de
        // idiomas sigue fallando, la ventana principal no muestra claves crudas.
        private static string TextoMenu_704ILR(string clave_704ILR)
        {
            switch (clave_704ILR)
            {
                case "MENU_INICIO": return T_704ILR(clave_704ILR, "Inicio");
                case "MENU_RESERVAS": return T_704ILR(clave_704ILR, "Reservas");
                case "MENU_CLIENTES": return T_704ILR(clave_704ILR, "Clientes");
                case "MENU_SERVICIOS": return T_704ILR(clave_704ILR, "Servicios");
                case "MENU_PERFILES": return T_704ILR(clave_704ILR, "Perfiles");
                case "MENU_AUDITORIA": return T_704ILR(clave_704ILR, "Auditoría");
                case "MENU_OPERACIONES": return T_704ILR(clave_704ILR, "Operaciones");
                case "MENU_AGENDA": return T_704ILR(clave_704ILR, "Mi agenda");
                case "MENU_EMPLEADOS": return T_704ILR(clave_704ILR, "Empleados");
                default: return Tr_704ILR.T_704ILR(clave_704ILR);
            }
        }

        // Traduccion con fallback al texto por defecto si la clave no existe.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        // "Cerrar sesion" cierra la ventana y la sesion termina en OnFormClosing, una vez que
        // la vista acepto descartar sus cambios. Antes la sesion se cerraba aca, antes de
        // cualquier pregunta: un cierre cancelado habria dejado la ventana abierta sin sesion.
        private void DoLogout_704ILR()
        {
            Close();
        }

        // Por aca pasan "Cerrar sesion", la X de la barra superior y Alt+F4. Si el cierre lo
        // pide el usuario y la vista tiene cambios sin guardar, se pregunta igual que al
        // cambiar de seccion; con "No" el cierre se cancela y siguen la ventana, la sesion,
        // la suscripcion al idioma y lo tipeado. Un cierre que no pide el usuario (apagado de
        // Windows, fin de la aplicacion) no se detiene con una pregunta.
        protected override void OnFormClosing(FormClosingEventArgs e_704ILR)
        {
            if (!e_704ILR.Cancel && e_704ILR.CloseReason == CloseReason.UserClosing)
            {
                if (_preguntandoDescarte_704ILR)
                {
                    // Segundo pedido de cierre con la pregunta abierta: se cancela sin preguntar.
                    // Antes preguntaba de nuevo y, en la ventana modal, un "No" a una pregunta y
                    // un "Si" a la otra cerraban la sesion con la ventana abierta.
                    e_704ILR.Cancel = true;
                }
                else if (!ConfirmarDescarteDeVista_704ILR())
                {
                    e_704ILR.Cancel = true;
                }
                else if (Modal && DialogResult == DialogResult.None)
                {
                    // Un pedido de cierre cancelado mientras la pregunta estaba abierta deja la
                    // ventana modal sin resultado: sin reponerlo, la sesion se cerraba y la
                    // ventana seguia abierta.
                    DialogResult = DialogResult.Cancel;
                }
            }
            base.OnFormClosing(e_704ILR);
            if (e_704ILR.Cancel) return;

            _cerrando_704ILR = true;
            GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
            if (SessionManager_704ILR.IsSessionActive_704ILR)
            {
                try { BLL_Login_704ILR.Logout_704ILR(); } catch { /* ignorar: la ventana se cierra igual */ }
            }
        }

        // Enter mantenido. Mientras la tecla sigue apretada Windows repite el WM_KEYDOWN con el bit 30
        // del lParam en 1. Al cerrar un dialogo con Enter (por ejemplo Cerrar en Pagos) las
        // repeticiones llegaban a la vista y pulsaban el boton con el foco, que volvia a abrir el
        // dialogo; en una grilla recorrian las filas cargando cada reserva en la ficha. En la ventana
        // principal actua solo la primera pulsacion, como en Pagos; un cuadro de texto de varias
        // lineas conserva la repeticion. Override del framework (sin sufijo, REGLA 4).
        private const int WM_KEYDOWN_704ILR = 0x0100;
        private const long BitRepeticion_704ILR = 0x40000000;

        protected override bool ProcessCmdKey(ref Message msg_704ILR, Keys keyData_704ILR)
        {
            if (msg_704ILR.Msg == WM_KEYDOWN_704ILR && (keyData_704ILR & Keys.KeyCode) == Keys.Enter &&
                (msg_704ILR.LParam.ToInt64() & BitRepeticion_704ILR) != 0 &&
                !(Control.FromHandle(msg_704ILR.HWnd) is TextBoxBase texto_704ILR && texto_704ILR.Multiline))
                return true;
            return base.ProcessCmdKey(ref msg_704ILR, keyData_704ILR);
        }

        // Al abrir, la ventana tiene que entrar en el area de trabajo del monitor: en una
        // pantalla chica o con la escala de Windows a 125/150 %, el tamano de diseno
        // (1355x715) la supera y los botones de la barra superior quedaban afuera. Si no
        // entra, se achica hasta el area de trabajo y se centra.
        protected override void OnLoad(EventArgs e_704ILR)
        {
            AjustarAlAreaDeTrabajo_704ILR();
            base.OnLoad(e_704ILR);
        }

        // Tamano para el que se diseno la seccion de reservas (ver BuildUi).
        private static readonly Size TamanoDiseno_704ILR = new Size(1355, 715);

        private void AjustarAlAreaDeTrabajo_704ILR()
        {
            AjustarAlAreaDeTrabajo_704ILR(Screen.FromControl(this).WorkingArea.Size);
        }

        private void AjustarAlAreaDeTrabajo_704ILR(Size area_704ILR)
        {
            FijarMinimo_704ILR(area_704ILR);
            if (WindowState != FormWindowState.Normal) return;
            Size ajustado_704ILR = TamanoQueEntra_704ILR(Size, MinimumSize, area_704ILR);
            if (ajustado_704ILR == Size) return;
            Size = ajustado_704ILR;
            if (StartPosition == FormStartPosition.CenterScreen) CenterToScreen();
        }

        // El tamano minimo es el de diseno acotado al area de trabajo: arrastrando el borde no
        // se deja la seccion de reservas por debajo de su diseno, salvo que el area sea mas
        // chica, y en ese caso manda el area (un minimo que no entra impediria achicar la
        // ventana o maximizarla dentro de la pantalla).
        private void FijarMinimo_704ILR(Size area_704ILR)
        {
            Size minimo_704ILR = TamanoQueEntra_704ILR(TamanoDiseno_704ILR, Size.Empty, area_704ILR);
            if (MinimumSize != minimo_704ILR) MinimumSize = minimo_704ILR;
        }

        // Si la ventana pasa a otro monitor, el minimo se recalcula con el area de trabajo de
        // ese monitor (con uno solo, el area no cambia y no se hace nada).
        protected override void OnLocationChanged(EventArgs e_704ILR)
        {
            base.OnLocationChanged(e_704ILR);
            if (IsHandleCreated && WindowState == FormWindowState.Normal)
                FijarMinimo_704ILR(Screen.FromControl(this).WorkingArea.Size);
        }

        // Tamano de diseno acotado al area de trabajo, sin bajar del minimo en ningun eje.
        private static Size TamanoQueEntra_704ILR(Size diseno_704ILR, Size minimo_704ILR, Size area_704ILR) =>
            new Size(Math.Max(minimo_704ILR.Width, Math.Min(diseno_704ILR.Width, area_704ILR.Width)),
                     Math.Max(minimo_704ILR.Height, Math.Min(diseno_704ILR.Height, area_704ILR.Height)));

        // El segundo boton de la barra superior muestra "restaurar" con la ventana
        // maximizada y "maximizar" en los demas casos, como en cualquier ventana de Windows.
        protected override void OnResize(EventArgs e_704ILR)
        {
            base.OnResize(e_704ILR);
            if (_btnMax_704ILR == null) return;
            string glifo_704ILR = WindowState == FormWindowState.Maximized ? Theme_704ILR.IcoRestore_704ILR : Theme_704ILR.IcoMaximize_704ILR;
            if (_btnMax_704ILR.Text != glifo_704ILR) _btnMax_704ILR.Text = glifo_704ILR;
        }

        // Maximizar una ventana sin borde la lleva al monitor completo y tapa la barra de
        // tareas. Cada vez que Windows pide los limites de la ventana (WM_GETMINMAXINFO,
        // antes de maximizar por el boton, por teclado o arrastrandola al borde) se le
        // indica el area de trabajo del monitor donde esta.
        private const int WM_GETMINMAXINFO_704ILR = 0x0024;

        protected override void WndProc(ref Message m_704ILR)
        {
            if (m_704ILR.Msg == WM_GETMINMAXINFO_704ILR)
            {
                Screen pantalla_704ILR = Screen.FromHandle(m_704ILR.HWnd);
                Size principal_704ILR = Screen.PrimaryScreen?.Bounds.Size ?? pantalla_704ILR.Bounds.Size;
                MaximizedBounds = LimitesMaximizada_704ILR(pantalla_704ILR.Bounds, pantalla_704ILR.WorkingArea, principal_704ILR);
            }
            base.WndProc(ref m_704ILR);
        }

        // Limites maximizados en el formato de MINMAXINFO, que Windows interpreta respecto
        // del monitor principal: la posicion se traslada al monitor de la ventana y, en cada
        // eje, un tamano igual o mayor que el del principal se corrige por la diferencia
        // entre los dos monitores. Por eso, un eje que el area de trabajo ocupa entero se
        // informa con el tamano del principal (Windows lo lleva al del monitor real) y uno
        // recortado por la barra de tareas, con el area tal cual. Resultado exacto con un
        // monitor o con monitores del mismo tamano, y nunca fuera del area de trabajo.
        private static Rectangle LimitesMaximizada_704ILR(Rectangle monitor_704ILR, Rectangle area_704ILR, Size principal_704ILR) =>
            new Rectangle(area_704ILR.X - monitor_704ILR.X, area_704ILR.Y - monitor_704ILR.Y,
                          EjeMaximizado_704ILR(monitor_704ILR.Width, area_704ILR.Width, principal_704ILR.Width),
                          EjeMaximizado_704ILR(monitor_704ILR.Height, area_704ILR.Height, principal_704ILR.Height));

        private static int EjeMaximizado_704ILR(int monitor_704ILR, int area_704ILR, int principal_704ILR) =>
            area_704ILR < principal_704ILR ? area_704ILR
            : area_704ILR >= monitor_704ILR ? principal_704ILR
            : principal_704ILR - (monitor_704ILR - area_704ILR);

        // ============================================================
        // Item de menu lateral: barra de acento + icono + texto, con
        // estados hover/activo. Encapsula su propio cromo (cohesion).
        // ============================================================
        private sealed class SideMenuItem_704ILR : Panel
        {
            public readonly string Key_704ILR;
            public readonly Label Caption_704ILR;
            private readonly Panel _bar_704ILR;
            private readonly Label _icon_704ILR;
            private bool _active_704ILR;

            public SideMenuItem_704ILR(string glyph_704ILR, string key_704ILR, EventHandler onClick_704ILR)
            {
                Key_704ILR = key_704ILR;
                Dock = DockStyle.Top;
                Height = 48;
                BackColor = Theme_704ILR.BgSidebar_704ILR;
                Cursor = Cursors.Hand;

                _bar_704ILR = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = Theme_704ILR.BgSidebar_704ILR };
                _icon_704ILR = new Label
                {
                    Text = glyph_704ILR,
                    Font = Theme_704ILR.FontIcon_704ILR,
                    ForeColor = Theme_704ILR.Accent_704ILR,
                    Dock = DockStyle.Left,
                    Width = 50,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                Caption_704ILR = new Label
                {
                    Font = Theme_704ILR.FontMenu_704ILR,
                    ForeColor = Theme_704ILR.TextLight_704ILR,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent
                };

                Controls.Add(Caption_704ILR);
                Controls.Add(_icon_704ILR);
                Controls.Add(_bar_704ILR);

                foreach (Control c_704ILR in new Control[] { this, _icon_704ILR, Caption_704ILR })
                {
                    c_704ILR.MouseEnter += (s_704ILR, e_704ILR) => Hover_704ILR(true);
                    c_704ILR.MouseLeave += (s_704ILR, e_704ILR) => Hover_704ILR(false);
                    c_704ILR.Click += onClick_704ILR;
                }
            }

            private void Hover_704ILR(bool on_704ILR)
            {
                if (_active_704ILR) return;
                BackColor = on_704ILR ? Theme_704ILR.SidebarHover_704ILR : Theme_704ILR.BgSidebar_704ILR;
            }

            public void SetActive_704ILR(bool active_704ILR)
            {
                _active_704ILR = active_704ILR;
                _bar_704ILR.BackColor = active_704ILR ? Theme_704ILR.Accent_704ILR : Theme_704ILR.BgSidebar_704ILR;
                BackColor = active_704ILR ? Theme_704ILR.SidebarActive_704ILR : Theme_704ILR.BgSidebar_704ILR;
                Caption_704ILR.ForeColor = active_704ILR ? Theme_704ILR.TextOnDark_704ILR : Theme_704ILR.TextLight_704ILR;
                _icon_704ILR.ForeColor = Theme_704ILR.Accent_704ILR;
            }
        }
    }
}
