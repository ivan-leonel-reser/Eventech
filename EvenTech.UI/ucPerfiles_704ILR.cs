using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Gestion de perfiles (Composite + TreeView recursivo). Dos tarjetas:
    //  - Izquierda: permisos del perfil (arbol) + alta de perfil.
    //  - Derecha: asignacion de perfiles a usuarios (grilla con combo por fila).
    // Observa el idioma para traducir sus textos y le informa a la ventana principal si
    // hay permisos tildados o asignaciones sin guardar (IVistaConCambios).
    public class ucPerfiles_704ILR : UserControl, IObservadorIdioma_704ILR, IVistaConCambios_704ILR
    {
        // Marca del nodo raiz de la rama "Perfiles incluidos" (Composite de
        // perfiles): es un titulo, no un componente seleccionable.
        private const string TagRamaPerfiles_704ILR = "PERFILES_INCLUIDOS";

        // Negrita de los grupos, del titulo de la rama y de cada perfil incluible: se le pide
        // al control nativo con el estado TVIS_BOLD del item. Con TreeNode.NodeFont, cada vez
        // que el arbol dibujaba uno de esos nodos .NET creaba un HFONT nuevo
        // (TreeView.CustomDraw -> Font.ToHfont) que solo liberaba el finalizador: con la
        // pantalla en uso (tildar, guardar, elegir otro perfil, cambiar el idioma) el proceso
        // llegaba al cupo de 10.000 objetos GDI y aparecia el cuadro de excepcion no
        // controlada. Con el estado del item el control arma una sola fuente negrita, mide
        // con ella el ancho del texto (no se recorta) y la libera al destruirse.
        private const int TVM_SETITEMW_704ILR = 0x113F;   // TV_FIRST + 63
        private const uint TVIF_STATE_704ILR = 0x0008, TVIF_HANDLE_704ILR = 0x0010, TVIS_BOLD_704ILR = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct TVITEMW_704ILR
        {
            public uint Mask_704ILR;
            public IntPtr HItem_704ILR;
            public uint State_704ILR;
            public uint StateMask_704ILR;
            public IntPtr PszText_704ILR;
            public int CchTextMax_704ILR;
            public int IImage_704ILR;
            public int ISelectedImage_704ILR;
            public int CChildren_704ILR;
            public IntPtr LParam_704ILR;
        }

        // EntryPoint obligatorio: el metodo lleva el sufijo y la exportacion de user32 no.
        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessageItem_704ILR(IntPtr hWnd_704ILR, int msg_704ILR, IntPtr wParam_704ILR, ref TVITEMW_704ILR item_704ILR);

        private ComboBox _cboPerfil_704ILR;
        private TreeView _tree_704ILR;
        private TreeNode _nodoPerfiles_704ILR; // rama con los demas perfiles para incluir
        private AppButton_704ILR _btnGuardar_704ILR, _btnNuevoPerfil_704ILR, _btnGuardarAsig_704ILR;
        private Label _lblError_704ILR, _lblOk_704ILR, _lblAsigTitulo_704ILR, _lblMsgAsig_704ILR;
        private DataGridView _gridUsuarios_704ILR;
        private bool _suppressAfterCheck_704ILR;

        // Ids de permisos cuyo tilde es heredado de un perfil incluido (el check
        // lo puso el sistema, no el usuario): se muestran marcados y no se
        // persisten como asignacion directa al guardar.
        private HashSet<int> _marcadosHeredados_704ILR = new HashSet<int>();

        // Cache de permisos efectivos por perfil incluido (evita repetir la
        // resolucion del Composite en cada tilde).
        private readonly Dictionary<int, List<BE_Permiso_704ILR>> _permisosPorPerfil_704ILR = new Dictionary<int, List<BE_Permiso_704ILR>>();

        // Perfil cuya composicion muestra hoy el arbol. Guardar solo procede si
        // coincide con el perfil elegido en el combo: si la carga fallo, o todavia
        // no ocurrio, grabar reemplazaria la composicion con la de otro perfil o
        // con una vacia.
        private int? _perfilCargadoId_704ILR;

        // Componentes del arbol por Id: de ellos salen los textos traducidos de cada nodo.
        private readonly Dictionary<int, BE_IComponentePermiso_704ILR> _componentesPorId_704ILR = new Dictionary<int, BE_IComponentePermiso_704ILR>();

        // Si el catalogo de permisos quedo armado en el arbol. Sin el, el arbol no refleja
        // ninguna composicion: no se carga ningun perfil y Guardar se niega (grabarlo
        // vaciaba los permisos del perfil elegido).
        private bool _arbolCargado_704ILR;

        // Componentes asignados al perfil cuando se cargo (lo que tiene en la base).
        private HashSet<int> _asignadosCargados_704ILR = new HashSet<int>();

        // Grupos cuyo nodo tildo el usuario desde la carga del perfil (y que no volvio a
        // destildar): junto con los asignados al cargar, deciden si un grupo completo se
        // guarda como grupo o como sus hojas (ver RecolectarDirectos).
        private readonly HashSet<int> _gruposTildados_704ILR = new HashSet<int>();

        // Mientras CargarPerfiles cambia el origen de datos del combo: los eventos del
        // enlace no cargan nada y el perfil elegido se carga una sola vez al final.
        private bool _cargandoPerfiles_704ILR;

        // Ultimo perfil cuya carga se intento (con o sin exito) y si esa carga fallo mientras
        // el usuario recorria la lista desplegada, antes de confirmar la eleccion. Una
        // seleccion del usuario dispara dos eventos del combo, el cambio de indice y la
        // confirmacion: con la lista cerrada llega primero la confirmacion y con la lista
        // abierta, el cambio. El segundo evento no repite la carga que acaba de intentar el
        // primero: si fallo, repetirla costaba otra espera completa y otro asiento de error
        // sin que el usuario lo pidiera. Volver a elegir el perfil despues si la reintenta.
        private int? _perfilIntentadoId_704ILR;
        private bool _confirmacionPendiente_704ILR;

        // Como rearmar el texto de cada aviso: al cambiar el idioma se re-traduce
        // en lugar de quedar en el idioma anterior.
        private Func<string> _textoOk_704ILR, _textoError_704ILR, _textoMsgAsig_704ILR;

        // Linea base de los cambios sin guardar (IVistaConCambios): la composicion del perfil
        // cargado tal como la enviaria Guardar (FotoComposicion), tomada al terminar su carga o
        // la recarga que sigue a un guardado. Nula mientras no hay un perfil cargado.
        private string _lineaBaseComposicion_704ILR;

        // Mientras el programa ajusta la seleccion del combo despues de preguntar (vuelve al perfil
        // cargado o fija el que se eligio): esa seleccion no es una eleccion nueva.
        private bool _seleccionPorPrograma_704ILR;

        // Mientras la pregunta de descarte esta abierta. Al tomar ella el foco, la lista abierta del
        // combo se cierra y el combo vuelve a avisar el cambio: esos avisos no repiten la pregunta.
        private bool _preguntandoDescarte_704ILR;

        // Hay una eleccion de perfil con cambios sin guardar esperando su pregunta. La pregunta no se
        // hace dentro del aviso del combo: el combo todavia no termino su gesto (con la lista abierta,
        // Enter confirma, cierra la lista y vuelve a fijar la seleccion) y cada aviso de ese gesto
        // repetia la pregunta. Se programa una sola vez y se resuelve cuando el gesto termino.
        private bool _resolucionPendiente_704ILR;

        // Con cambios sin guardar, recorrer la lista abierta del combo no carga ni pregunta en
        // cada perfil que pasa: la eleccion queda en espera y se pregunta una sola vez, al
        // confirmarla o al cerrarse la lista.
        private bool _eleccionEnLista_704ILR;

        public ucPerfiles_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) =>
            {
                // El arbol se arma al cargar el primer perfil (CargarAsignacionesPerfil). Si no
                // hubo perfil que cargar, se arma igual para mostrar el catalogo. Armarlo por
                // separado antes duplicaba la espera cuando el catalogo no respondia.
                CargarPerfiles_704ILR();
                if (!_arbolCargado_704ILR && _cboPerfil_704ILR.Items.Count == 0) ConstruirArbol_704ILR();
                CargarUsuarios_704ILR();
                GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            };
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        private void BuildUi_704ILR()
        {
            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR)
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // titulo
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // cuerpo (2 columnas)

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Gestion de Perfiles");
            lblTitle_704ILR.Tag = "T:PERF_TITULO";
            lblTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            var body_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body_704ILR.Controls.Add(BuildCardPermisos_704ILR(), 0, 0);
            body_704ILR.Controls.Add(BuildCardAsignacion_704ILR(), 1, 0);

            root_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            root_704ILR.Controls.Add(body_704ILR, 0, 1);
            Controls.Add(root_704ILR);
        }

        // ---- Tarjeta izquierda: permisos del perfil + alta de perfil ----
        private Control BuildCardPermisos_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0),
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };

            var layout_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Color.Transparent
            };
            layout_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // selector + nuevo perfil
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // hint
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // arbol
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // acciones
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // aviso de guardado
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // aviso de error

            // Fila en TableLayoutPanel: el combo se estira y el boton queda SIEMPRE
            // visible a la derecha (antes el FlowLayoutPanel sin wrap lo recortaba).
            var fila_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            fila_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fila_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fila_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fila_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var lblPerfil_704ILR = Ui_704ILR.BodyBold_704ILR("Perfil:");
            lblPerfil_704ILR.Tag = "T:PERF_PERFIL"; lblPerfil_704ILR.Anchor = AnchorStyles.Left; lblPerfil_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            _cboPerfil_704ILR = Ui_704ILR.Combo_704ILR(); _cboPerfil_704ILR.Anchor = AnchorStyles.Left | AnchorStyles.Right; _cboPerfil_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            _cboPerfil_704ILR.SelectedIndexChanged += (s_704ILR, e_704ILR) => PerfilCambiado_704ILR();
            // Volver a elegir el mismo perfil no cambia el indice ni dispara el evento de
            // arriba: este permite reintentar una carga que fallo, que es lo que pide el aviso.
            _cboPerfil_704ILR.SelectionChangeCommitted += (s_704ILR, e_704ILR) => PerfilConfirmado_704ILR();
            _cboPerfil_704ILR.DropDownClosed += (s_704ILR, e_704ILR) => ListaPerfilesCerrada_704ILR();
            _btnNuevoPerfil_704ILR = Ui_704ILR.Secondary_704ILR("Nuevo perfil", Theme_704ILR.IcoAdd_704ILR);
            _btnNuevoPerfil_704ILR.Tag = "T:PERF_NUEVO"; _btnNuevoPerfil_704ILR.Size = new Size(150, 30); _btnNuevoPerfil_704ILR.Anchor = AnchorStyles.Right; _btnNuevoPerfil_704ILR.Margin = new Padding(0);
            _btnNuevoPerfil_704ILR.Click += (s_704ILR, e_704ILR) => NuevoPerfil_704ILR();
            fila_704ILR.Controls.Add(lblPerfil_704ILR, 0, 0); fila_704ILR.Controls.Add(_cboPerfil_704ILR, 1, 0); fila_704ILR.Controls.Add(_btnNuevoPerfil_704ILR, 2, 0);

            var lblHint_704ILR = new Label
            {
                Tag = "T:PERF_HINT",
                Text = "Tilde los permisos del perfil. Marcar un grupo incluye a sus hijos.",
                Font = new Font(Theme_704ILR.FontCaption_704ILR, FontStyle.Italic),
                ForeColor = Theme_704ILR.TextMuted_704ILR, AutoSize = true, BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR)
            };

            _tree_704ILR = new TreeView
            {
                Dock = DockStyle.Fill, CheckBoxes = true, Font = Theme_704ILR.FontBody_704ILR,
                BackColor = Theme_704ILR.Surface_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, BorderStyle = BorderStyle.None,
                ShowLines = true, HideSelection = false, ItemHeight = 26, Indent = 22
            };
            _tree_704ILR.AfterCheck += Tree_AfterCheck_704ILR;
            // Si el control vuelve a crear su ventana, .NET reinserta los nodos despues de avisar
            // HandleCreated y los items nuevos llegan sin la negrita: se aplica al terminar.
            _tree_704ILR.HandleCreated += (s_704ILR, e_704ILR) =>
                _tree_704ILR.BeginInvoke((Action)(() => { if (!_tree_704ILR.IsDisposed) AplicarNegrita_704ILR(_tree_704ILR.Nodes); }));

            var acciones_704ILR = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0) };
            _btnGuardar_704ILR = Ui_704ILR.Primary_704ILR("Guardar permisos", Theme_704ILR.IcoSave_704ILR);
            _btnGuardar_704ILR.Tag = "T:PERF_GUARDAR"; _btnGuardar_704ILR.Size = new Size(190, 38); _btnGuardar_704ILR.Margin = new Padding(0);
            _btnGuardar_704ILR.Click += (s_704ILR, e_704ILR) => Guardar_704ILR();
            acciones_704ILR.Controls.Add(_btnGuardar_704ILR);

            // Los avisos van en filas propias, a todo el ancho de la tarjeta, y
            // envuelven: al lado del boton no entraban y se recortaban justo en la
            // parte que avisa que los cambios rigen desde el proximo inicio de sesion.
            _lblOk_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.Success_704ILR, Visible = false, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0) };
            _lblError_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.Error_704ILR, Visible = false, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0) };

            layout_704ILR.Controls.Add(fila_704ILR, 0, 0);
            layout_704ILR.Controls.Add(lblHint_704ILR, 0, 1);
            layout_704ILR.Controls.Add(_tree_704ILR, 0, 2);
            layout_704ILR.Controls.Add(acciones_704ILR, 0, 3);
            layout_704ILR.Controls.Add(_lblOk_704ILR, 0, 4);
            layout_704ILR.Controls.Add(_lblError_704ILR, 0, 5);
            card_704ILR.Controls.Add(layout_704ILR);
            return card_704ILR;
        }

        // ---- Tarjeta derecha: asignar perfiles a usuarios ----
        private Control BuildCardAsignacion_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(Theme_704ILR.SpaceLg_704ILR) };

            var layout_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent };
            layout_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // titulo
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grilla
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // acciones
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // aviso

            _lblAsigTitulo_704ILR = Ui_704ILR.H2_704ILR("Asignar perfil a usuarios");
            _lblAsigTitulo_704ILR.Tag = "T:PERF_ASIGNAR_TITULO";
            _lblAsigTitulo_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            _gridUsuarios_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_gridUsuarios_704ILR, editable_704ILR: true);
            _gridUsuarios_704ILR.DataError += (s_704ILR, e_704ILR) => e_704ILR.ThrowException = false; // valores de combo fuera de lista: ignorar
            _gridUsuarios_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cUsuario", HeaderText = "Usuario", FillWeight = 30, ReadOnly = true });
            var colPerfil_704ILR = new DataGridViewComboBoxColumn { Name = "cPerfil", HeaderText = "Perfil", FillWeight = 33, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton };
            _gridUsuarios_704ILR.Columns.Add(colPerfil_704ILR);
            _gridUsuarios_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstado", HeaderText = "Estado", FillWeight = 25, ReadOnly = true });
            // Boton-icono (candado abierto) para desbloquear: compacto + tooltip.
            var colDesbloq_704ILR = new DataGridViewButtonColumn { Name = "cDesbloq", HeaderText = "", FillWeight = 12, FlatStyle = FlatStyle.Flat, UseColumnTextForButtonValue = false, ToolTipText = "Desbloquear" };
            colDesbloq_704ILR.DefaultCellStyle.Font = new Font("Segoe MDL2 Assets", 10F);
            colDesbloq_704ILR.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridUsuarios_704ILR.Columns.Add(colDesbloq_704ILR);
            _gridUsuarios_704ILR.CellContentClick += GridUsuarios_CellContentClick_704ILR;

            var acciones_704ILR = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0) };
            _btnGuardarAsig_704ILR = Ui_704ILR.Primary_704ILR("Guardar asignaciones", Theme_704ILR.IcoSave_704ILR);
            _btnGuardarAsig_704ILR.Tag = "T:PERF_GUARDAR_ASIG"; _btnGuardarAsig_704ILR.Size = new Size(210, 38); _btnGuardarAsig_704ILR.Margin = new Padding(0);
            _btnGuardarAsig_704ILR.Click += (s_704ILR, e_704ILR) => GuardarAsignaciones_704ILR();
            acciones_704ILR.Controls.Add(_btnGuardarAsig_704ILR);
            // Aviso en fila propia, a todo el ancho de la tarjeta (ver la tarjeta izquierda).
            _lblMsgAsig_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.Success_704ILR, Visible = false, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0) };

            layout_704ILR.Controls.Add(_lblAsigTitulo_704ILR, 0, 0);
            layout_704ILR.Controls.Add(_gridUsuarios_704ILR, 0, 1);
            layout_704ILR.Controls.Add(acciones_704ILR, 0, 2);
            layout_704ILR.Controls.Add(_lblMsgAsig_704ILR, 0, 3);
            card_704ILR.Controls.Add(layout_704ILR);
            return card_704ILR;
        }

        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            if (_tree_704ILR != null)
            {
                _suppressAfterCheck_704ILR = true;
                try
                {
                    // Nombres de los permisos en el idioma activo.
                    RetraducirArbol_704ILR(_tree_704ILR.Nodes);
                    if (_nodoPerfiles_704ILR != null)
                    {
                        _nodoPerfiles_704ILR.Text = T_704ILR("PERF_INCLUIDOS", "Perfiles incluidos");
                        // Re-traduce el sufijo "(heredado)" de los permisos marcados.
                        ActualizarHerencia_704ILR();
                    }
                }
                finally { _suppressAfterCheck_704ILR = false; }
            }
            if (_gridUsuarios_704ILR != null && _gridUsuarios_704ILR.Columns.Count >= 4)
            {
                _gridUsuarios_704ILR.Columns["cUsuario"].HeaderText = Tr_704ILR.T_704ILR("COL_USUARIO");
                _gridUsuarios_704ILR.Columns["cPerfil"].HeaderText  = Tr_704ILR.T_704ILR("COL_PERFIL");
                _gridUsuarios_704ILR.Columns["cEstado"].HeaderText  = Tr_704ILR.T_704ILR("COL_ESTADO");
                _gridUsuarios_704ILR.Columns["cDesbloq"].ToolTipText = Tr_704ILR.T_704ILR("PERF_DESBLOQUEAR");
                RetraducirGrillaUsuarios_704ILR();
            }
            RetraducirMensajes_704ILR();
        }

        // Desbloqueo de la cuenta de un usuario (boton de la grilla).
        private void GridUsuarios_CellContentClick_704ILR(object sender_704ILR, DataGridViewCellEventArgs e_704ILR)
        {
            if (e_704ILR.RowIndex < 0 || e_704ILR.ColumnIndex < 0) return;
            if (_gridUsuarios_704ILR.Columns[e_704ILR.ColumnIndex].Name != "cDesbloq") return;
            if (!(_gridUsuarios_704ILR.Rows[e_704ILR.RowIndex].Tag is BE_User_704ILR u_704ILR) || !u_704ILR.Blocked_704ILR) return;
            if (!Permisos_704ILR.Exigir_704ILR("PERFILES_GESTION", FindForm(), "desbloquear la cuenta '" + u_704ILR.Username_704ILR + "'")) return;
            try
            {
                if (!BLL_User_704ILR.Desbloquear_704ILR(u_704ILR.Id_704ILR))
                {
                    // La cuenta ya no existe (se borro por fuera). La grilla no se recarga: se
                    // descartarian los perfiles elegidos y todavia no guardados en otras filas.
                    MensajeAsig_704ILR(() => T_704ILR("MSG_PERF_REFERENCIA", TextoReferenciaInvalida_704ILR), error_704ILR: true);
                    return;
                }
                // Solo cambia el estado de esa cuenta: se actualiza su fila sin recargar
                // la grilla, que descartaria los perfiles elegidos en otras filas y
                // todavia no guardados.
                u_704ILR.Blocked_704ILR = false;
                u_704ILR.FailedAttempts_704ILR = 0;
                PintarEstadoUsuario_704ILR(_gridUsuarios_704ILR.Rows[e_704ILR.RowIndex]);
                MensajeAsig_704ILR(() => Tr_704ILR.T_704ILR("MSG_PERF_DESBLOQ"), error_704ILR: false);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Desbloquear usuario");
                MensajeAsig_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), error_704ILR: true);
            }
        }

        // ===================== Permisos (Composite) =====================
        // Arma el catalogo de permisos en el arbol. Devuelve si quedo armado; si falla, el
        // arbol queda vacio y el error a la vista.
        private bool ConstruirArbol_704ILR()
        {
            _arbolCargado_704ILR = false;
            try
            {
                _tree_704ILR.Nodes.Clear();
                _nodoPerfiles_704ILR = null; // su nodo salio del arbol con el Clear
                _componentesPorId_704ILR.Clear();
                _marcadosHeredados_704ILR = new HashSet<int>();
                _permisosPorPerfil_704ILR.Clear();
                List<BE_IComponentePermiso_704ILR> raices_704ILR = BLL_Perfil_704ILR.GetArbolPermisos_704ILR();
                foreach (var nodo_704ILR in raices_704ILR) _tree_704ILR.Nodes.Add(CrearNodo_704ILR(nodo_704ILR));
                AplicarNegrita_704ILR(_tree_704ILR.Nodes);
                _tree_704ILR.ExpandAll();
                MostrarPrimerNodo_704ILR();
                _arbolCargado_704ILR = true;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Construir arbol de permisos");
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
            return _arbolCargado_704ILR;
        }

        private TreeNode CrearNodo_704ILR(BE_IComponentePermiso_704ILR componente_704ILR)
        {
            _componentesPorId_704ILR[componente_704ILR.Id_704ILR] = componente_704ILR;
            string texto_704ILR = TextoPermiso_704ILR(componente_704ILR);
            // Name guarda el texto base: MarcarHeredado le agrega o quita el sufijo.
            // Los grupos van en negrita: se marca al entrar el nodo al arbol (AplicarNegrita),
            // cuando ya tiene su item nativo.
            var node_704ILR = new TreeNode(texto_704ILR) { Tag = componente_704ILR.Id_704ILR, Name = texto_704ILR };
            if (componente_704ILR is BE_GrupoPermisos_704ILR grupo_704ILR)
            {
                foreach (var hijo_704ILR in grupo_704ILR.Hijos_704ILR) node_704ILR.Nodes.Add(CrearNodo_704ILR(hijo_704ILR));
            }
            return node_704ILR;
        }

        // Van en negrita los grupos del catalogo, el titulo de la rama de perfiles y cada perfil
        // incluible; las hojas y los permisos informativos de un perfil incluido, no.
        private bool VaEnNegrita_704ILR(TreeNode n_704ILR) =>
            n_704ILR.Tag as string == TagRamaPerfiles_704ILR
            || n_704ILR.Tag is BE_Perfil_704ILR
            || (n_704ILR.Tag is int id_704ILR && _componentesPorId_704ILR.TryGetValue(id_704ILR, out var c_704ILR) && c_704ILR is BE_GrupoPermisos_704ILR);

        private void AplicarNegrita_704ILR(TreeNodeCollection nodes_704ILR)
        {
            foreach (TreeNode n_704ILR in nodes_704ILR) MarcarNegrita_704ILR(n_704ILR);
        }

        // Pone la negrita nativa (TVIS_BOLD) en el nodo, si la lleva, y en sus descendientes.
        // Solo alcanza a los nodos ya insertados en el control: se llama despues de agregarlos
        // y cuando el control vuelve a crear su ventana.
        private void MarcarNegrita_704ILR(TreeNode nodo_704ILR)
        {
            if (!_tree_704ILR.IsHandleCreated || nodo_704ILR.TreeView != _tree_704ILR) return;
            if (VaEnNegrita_704ILR(nodo_704ILR))
            {
                var item_704ILR = new TVITEMW_704ILR
                {
                    Mask_704ILR = TVIF_HANDLE_704ILR | TVIF_STATE_704ILR,
                    HItem_704ILR = nodo_704ILR.Handle,
                    State_704ILR = TVIS_BOLD_704ILR,
                    StateMask_704ILR = TVIS_BOLD_704ILR
                };
                SendMessageItem_704ILR(_tree_704ILR.Handle, TVM_SETITEMW_704ILR, IntPtr.Zero, ref item_704ILR);
            }
            foreach (TreeNode hijo_704ILR in nodo_704ILR.Nodes) MarcarNegrita_704ILR(hijo_704ILR);
        }

        // Texto visible de un componente del arbol en el idioma activo. Se traduce por
        // clave: PERM_<Clave> para las hojas y PERMG_<NOMBRE> para los grupos, que no
        // tienen Clave. Si la base no trae la traduccion se muestra el nombre sembrado.
        private static string TextoPermiso_704ILR(BE_IComponentePermiso_704ILR componente_704ILR)
        {
            string clave_704ILR = componente_704ILR is BE_Permiso_704ILR hoja_704ILR && !string.IsNullOrWhiteSpace(hoja_704ILR.Clave_704ILR)
                ? "PERM_" + hoja_704ILR.Clave_704ILR
                : "PERMG_" + ClaveDeNombre_704ILR(componente_704ILR.Nombre_704ILR);
            return T_704ILR(clave_704ILR, componente_704ILR.Nombre_704ILR);
        }

        // "Administracion del sistema" -> "ADMINISTRACION_DEL_SISTEMA".
        private static string ClaveDeNombre_704ILR(string nombre_704ILR)
        {
            var sb_704ILR = new StringBuilder();
            foreach (char ch_704ILR in (nombre_704ILR ?? string.Empty).Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch_704ILR) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch_704ILR)) sb_704ILR.Append(char.ToUpperInvariant(ch_704ILR));
                else if (sb_704ILR.Length > 0 && sb_704ILR[sb_704ILR.Length - 1] != '_') sb_704ILR.Append('_');
            }
            return sb_704ILR.ToString().Trim('_');
        }

        private void RetraducirArbol_704ILR(TreeNodeCollection nodes_704ILR)
        {
            foreach (TreeNode n_704ILR in nodes_704ILR)
            {
                if (n_704ILR == _nodoPerfiles_704ILR)
                {
                    // Permisos informativos que aporta cada perfil incluido.
                    foreach (TreeNode perfil_704ILR in n_704ILR.Nodes)
                        foreach (TreeNode info_704ILR in perfil_704ILR.Nodes)
                            if (info_704ILR.Tag is BE_Permiso_704ILR p_704ILR) info_704ILR.Text = TextoPermiso_704ILR(p_704ILR);
                    continue;
                }
                if (n_704ILR.Tag is int id_704ILR && _componentesPorId_704ILR.TryGetValue(id_704ILR, out var componente_704ILR))
                {
                    n_704ILR.Name = TextoPermiso_704ILR(componente_704ILR);
                    n_704ILR.Text = _marcadosHeredados_704ILR.Contains(id_704ILR)
                        ? n_704ILR.Name + "  " + T_704ILR("PERF_HEREDADO", "(heredado)")
                        : n_704ILR.Name;
                }
                RetraducirArbol_704ILR(n_704ILR.Nodes);
            }
        }

        private void MostrarPrimerNodo_704ILR()
        {
            if (_tree_704ILR.Nodes.Count > 0) _tree_704ILR.TopNode = _tree_704ILR.Nodes[0];
        }

        private void CargarPerfiles_704ILR()
        {
            try
            {
                // Los miembros van ANTES que el origen de datos. Asignados despues, el
                // SelectedIndexChanged del enlace llegaba con el objeto del perfil y no
                // con su Id: la carga del arbol se salteaba y el primer perfil quedaba
                // sin sus tildes (guardarlo asi vaciaba su composicion).
                // Mientras cambia el origen de datos, los eventos del enlace no cargan nada
                // (cada uno podia volver a intentar el catalogo): se carga abajo, una vez.
                _cargandoPerfiles_704ILR = true;
                try
                {
                    _cboPerfil_704ILR.DisplayMember = "Nombre_704ILR";
                    _cboPerfil_704ILR.ValueMember = "Id_704ILR";
                    _cboPerfil_704ILR.DataSource = BLL_Perfil_704ILR.GetPerfiles_704ILR();
                    // Si el arbol ya muestra un perfil y sigue en la lista (se relee al sumar un
                    // perfil nuevo), el combo queda en el: volver al primero lo recargaba y
                    // descartaba lo tildado sin guardar.
                    if (_cboPerfil_704ILR.Items.Count > 0) _cboPerfil_704ILR.SelectedIndex = IndicePerfil_704ILR(_perfilCargadoId_704ILR);
                }
                finally { _cargandoPerfiles_704ILR = false; }
                // Y se carga en forma explicita, sin depender de los eventos del enlace
                // (elegir de nuevo el mismo indice no los vuelve a disparar).
                if (_cboPerfil_704ILR.SelectedValue is int sel_704ILR && _perfilCargadoId_704ILR != sel_704ILR)
                    CargarAsignacionesPerfil_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Cargar perfiles");
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        // Cambio el perfil elegido en el combo (lo cambio el usuario o el programa). Se carga si
        // el arbol no muestra ya su composicion y si no se acaba de intentar: en una seleccion
        // del usuario con la lista cerrada, la confirmacion llego antes y ya lo intento. Si se
        // eligio recorriendo la lista abierta y la carga fallo, su confirmacion llega despues y
        // no la repite. Cargar otro perfil descarta los permisos tildados sin guardar: si los
        // hay no se carga aca y se pregunta una sola vez, cuando termina el gesto del combo (con
        // la lista abierta, recien al confirmar la eleccion o al cerrar la lista).
        private void PerfilCambiado_704ILR()
        {
            if (_cargandoPerfiles_704ILR || _seleccionPorPrograma_704ILR || _preguntandoDescarte_704ILR || !(_cboPerfil_704ILR.SelectedValue is int id_704ILR)) return;
            if (_perfilCargadoId_704ILR == id_704ILR || _perfilIntentadoId_704ILR == id_704ILR) return;
            bool listaAbierta_704ILR = _cboPerfil_704ILR.DroppedDown;
            if (HayCambiosEnComposicion_704ILR)
            {
                if (listaAbierta_704ILR) _eleccionEnLista_704ILR = true;
                else ProgramarResolucion_704ILR();
                return;
            }
            CargarAsignacionesPerfil_704ILR();
            _confirmacionPendiente_704ILR = listaAbierta_704ILR && _perfilCargadoId_704ILR != id_704ILR;
        }

        // El usuario confirmo un perfil en el combo (otro, o el mismo de nuevo): se carga si el
        // arbol no muestra ya su composicion, sea porque es otro perfil o porque su carga fallo,
        // salvo que confirme la eleccion cuya carga acaba de fallar al recorrer la lista. Elegir
        // el perfil que ya esta cargado no descarta lo tildado sin guardar; elegir otro, solo si
        // el usuario lo acepta (se pregunta cuando termina el gesto del combo).
        private void PerfilConfirmado_704ILR()
        {
            if (_cargandoPerfiles_704ILR || _seleccionPorPrograma_704ILR || _preguntandoDescarte_704ILR || !(_cboPerfil_704ILR.SelectedValue is int id_704ILR)) return;
            _eleccionEnLista_704ILR = false;
            if (_perfilCargadoId_704ILR == id_704ILR) return;
            bool confirmaElIntento_704ILR = _confirmacionPendiente_704ILR && _perfilIntentadoId_704ILR == id_704ILR;
            _confirmacionPendiente_704ILR = false;
            if (confirmaElIntento_704ILR) return;
            if (HayCambiosEnComposicion_704ILR) { ProgramarResolucion_704ILR(); return; }
            CargarAsignacionesPerfil_704ILR();
        }

        // Programa la resolucion de una eleccion hecha con cambios sin guardar para cuando el combo
        // termine su gesto: los avisos de un mismo gesto quedan en una sola pregunta.
        private void ProgramarResolucion_704ILR()
        {
            if (_resolucionPendiente_704ILR) return;
            if (!IsHandleCreated || IsDisposed) { ResolverEleccion_704ILR(); return; }
            _resolucionPendiente_704ILR = true;
            BeginInvoke((Action)(() =>
            {
                _resolucionPendiente_704ILR = false;
                if (!IsDisposed) ResolverEleccion_704ILR();
            }));
        }

        // El combo muestra un perfil distinto del cargado: si el arbol tiene cambios sin guardar se
        // pregunta y, si el usuario los descarta, se carga el elegido; si no, el combo vuelve al cargado.
        private void ResolverEleccion_704ILR()
        {
            if (_cargandoPerfiles_704ILR || _preguntandoDescarte_704ILR || !(_cboPerfil_704ILR.SelectedValue is int id_704ILR) || _perfilCargadoId_704ILR == id_704ILR) return;
            if (PuedeDescartarComposicion_704ILR(id_704ILR)) CargarAsignacionesPerfil_704ILR();
        }

        // Si la lista se cierra sin confirmar (Escape, clic afuera, foco en otro control), la
        // carga que fallo al recorrerla deja de esperar su confirmacion y volver a elegir ese
        // perfil la reintenta; y la eleccion que quedo en espera por los cambios sin guardar se
        // resuelve como al elegir con la lista cerrada. Se difiere hasta que termina el gesto
        // porque la confirmacion, si la hay, puede llegar antes o despues del cierre (y en ese
        // caso la resuelve ella).
        private void ListaPerfilesCerrada_704ILR()
        {
            if (_preguntandoDescarte_704ILR || (!_confirmacionPendiente_704ILR && !_eleccionEnLista_704ILR) || !IsHandleCreated || IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || _preguntandoDescarte_704ILR) return;
                _confirmacionPendiente_704ILR = false;
                if (!_eleccionEnLista_704ILR) return;
                _eleccionEnLista_704ILR = false;
                ResolverEleccion_704ILR();
            }));
        }

        // conservarPosicion: tras guardar, el arbol vuelve a mostrar la zona en la que
        // se estaba trabajando; al elegir otro perfil arranca por su primer nodo.
        private void CargarAsignacionesPerfil_704ILR(bool conservarPosicion_704ILR = false)
        {
            if (!(_cboPerfil_704ILR.SelectedValue is int perfilId_704ILR)) return;
            _perfilIntentadoId_704ILR = perfilId_704ILR;
            _confirmacionPendiente_704ILR = false;
            // Los avisos eran del perfil anterior.
            _lblOk_704ILR.Visible = false;
            _lblError_704ILR.Visible = false;
            TreeNode topPrevio_704ILR = conservarPosicion_704ILR ? _tree_704ILR.TopNode : null;
            _perfilCargadoId_704ILR = null;
            _lineaBaseComposicion_704ILR = null;
            _eleccionEnLista_704ILR = false;
            // Sin el catalogo el arbol no refleja ninguna composicion y grabarlo la vaciaria:
            // se vuelve a armar y, si tampoco se puede, el perfil queda sin cargar (Guardar se
            // niega) y el error de esa carga queda a la vista.
            if (!_arbolCargado_704ILR && !ConstruirArbol_704ILR()) return;
            try
            {
                HashSet<int> asignados_704ILR = BLL_Perfil_704ILR.GetPermisosAsignados_704ILR(perfilId_704ILR);
                HashSet<int> incluidos_704ILR = BLL_Perfil_704ILR.GetPerfilesIncluidos_704ILR(perfilId_704ILR);
                _suppressAfterCheck_704ILR = true;
                QuitarRamaPerfiles_704ILR();
                _marcadosHeredados_704ILR = new HashSet<int>();
                _asignadosCargados_704ILR = asignados_704ILR;
                _gruposTildados_704ILR.Clear();
                AplicarChecks_704ILR(_tree_704ILR.Nodes, asignados_704ILR, false);
                ConstruirRamaPerfiles_704ILR(perfilId_704ILR, incluidos_704ILR);
                ActualizarHerencia_704ILR();
                SincronizarGrupos_704ILR(_tree_704ILR.Nodes);
                _suppressAfterCheck_704ILR = false;
                _perfilCargadoId_704ILR = perfilId_704ILR;
                // Lo que el arbol muestra recien cargado es la linea base de los cambios sin guardar.
                _lineaBaseComposicion_704ILR = FotoComposicion_704ILR();

                if (topPrevio_704ILR != null && topPrevio_704ILR.TreeView == _tree_704ILR) _tree_704ILR.TopNode = topPrevio_704ILR;
                else MostrarPrimerNodo_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                _suppressAfterCheck_704ILR = false;
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Cargar asignaciones del perfil");
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        private void QuitarRamaPerfiles_704ILR()
        {
            if (_nodoPerfiles_704ILR != null)
            {
                _tree_704ILR.Nodes.Remove(_nodoPerfiles_704ILR);
                _nodoPerfiles_704ILR = null;
            }
            _permisosPorPerfil_704ILR.Clear();
        }

        // Refleja en el arbol principal los permisos heredados de los perfiles
        // incluidos tildados: aparecen tildados y marcados "(heredado)". El tilde
        // heredado no se persiste como asignacion directa ni se puede destildar
        // (se quita destildando el perfil incluido que lo aporta).
        private void ActualizarHerencia_704ILR()
        {
            var heredados_704ILR = new HashSet<int>();
            if (_nodoPerfiles_704ILR != null)
            {
                foreach (TreeNode n_704ILR in _nodoPerfiles_704ILR.Nodes)
                {
                    if (!n_704ILR.Checked || !(n_704ILR.Tag is BE_Perfil_704ILR p_704ILR)) continue;
                    if (_permisosPorPerfil_704ILR.TryGetValue(p_704ILR.Id_704ILR, out var permisos_704ILR))
                        foreach (var permiso_704ILR in permisos_704ILR) heredados_704ILR.Add(permiso_704ILR.Id_704ILR);
                }
            }

            var nuevosMarcados_704ILR = new HashSet<int>();
            AplicarHerencia_704ILR(_tree_704ILR.Nodes, heredados_704ILR, nuevosMarcados_704ILR);
            _marcadosHeredados_704ILR = nuevosMarcados_704ILR;
        }

        private void AplicarHerencia_704ILR(TreeNodeCollection nodes_704ILR, HashSet<int> heredados_704ILR, HashSet<int> nuevosMarcados_704ILR)
        {
            foreach (TreeNode n_704ILR in nodes_704ILR)
            {
                if (n_704ILR == _nodoPerfiles_704ILR) continue; // la rama de perfiles no se marca

                if (n_704ILR.Tag is int id_704ILR)
                {
                    bool eraHeredado_704ILR = _marcadosHeredados_704ILR.Contains(id_704ILR);
                    // El tilde es "directo" si lo puso el usuario (no el sistema).
                    bool directo_704ILR = n_704ILR.Checked && !eraHeredado_704ILR;

                    if (heredados_704ILR.Contains(id_704ILR) && !directo_704ILR)
                    {
                        n_704ILR.Checked = true;
                        MarcarHeredado_704ILR(n_704ILR, true);
                        nuevosMarcados_704ILR.Add(id_704ILR);
                    }
                    else
                    {
                        if (eraHeredado_704ILR) n_704ILR.Checked = directo_704ILR; // dejo de heredarse: se destilda
                        MarcarHeredado_704ILR(n_704ILR, false);
                    }
                }
                AplicarHerencia_704ILR(n_704ILR.Nodes, heredados_704ILR, nuevosMarcados_704ILR);
            }
        }

        // Marca visual del permiso heredado: sufijo "(heredado)" + color de exito.
        // El texto original se conserva en Name para poder restaurarlo.
        private void MarcarHeredado_704ILR(TreeNode n_704ILR, bool heredado_704ILR)
        {
            if (heredado_704ILR)
            {
                if (string.IsNullOrEmpty(n_704ILR.Name)) n_704ILR.Name = n_704ILR.Text;
                n_704ILR.Text = n_704ILR.Name + "  " + T_704ILR("PERF_HEREDADO", "(heredado)");
                n_704ILR.ForeColor = Theme_704ILR.Success_704ILR;
            }
            else
            {
                if (!string.IsNullOrEmpty(n_704ILR.Name)) n_704ILR.Text = n_704ILR.Name;
                n_704ILR.ForeColor = _tree_704ILR.ForeColor;
            }
        }

        // Rama del Composite de perfiles: lista los demas perfiles para poder
        // incluirlos dentro del seleccionado (p.ej. Gerencial contiene Vendedor
        // y hereda sus permisos). Debajo de cada perfil se muestran, a modo
        // informativo, los permisos efectivos que aportaria.
        private void ConstruirRamaPerfiles_704ILR(int perfilId_704ILR, HashSet<int> incluidos_704ILR)
        {
            // El titulo y cada perfil van en negrita: se marca al agregar la rama al arbol.
            _nodoPerfiles_704ILR = new TreeNode(T_704ILR("PERF_INCLUIDOS", "Perfiles incluidos"))
            {
                Tag = TagRamaPerfiles_704ILR
            };

            foreach (var perfil_704ILR in BLL_Perfil_704ILR.GetPerfiles_704ILR())
            {
                if (perfil_704ILR.Id_704ILR == perfilId_704ILR) continue; // un perfil no puede incluirse a si mismo

                var nodoPerfil_704ILR = new TreeNode(perfil_704ILR.Nombre_704ILR) { Tag = perfil_704ILR };
                try
                {
                    // Permisos efectivos del perfil incluido (resueltos por el
                    // Composite, inclusiones anidadas incluidas). Llevan el permiso
                    // en el Tag solo para traducir su nombre: son informativos y no
                    // se recolectan al guardar.
                    List<BE_Permiso_704ILR> permisos_704ILR = BLL_Perfil_704ILR.GetPermisosEfectivosDePerfil_704ILR(perfil_704ILR.Id_704ILR);
                    _permisosPorPerfil_704ILR[perfil_704ILR.Id_704ILR] = permisos_704ILR;
                    foreach (var permiso_704ILR in permisos_704ILR)
                        nodoPerfil_704ILR.Nodes.Add(new TreeNode(TextoPermiso_704ILR(permiso_704ILR)) { Tag = permiso_704ILR });
                }
                catch (Exception ex_704ILR)
                {
                    _permisosPorPerfil_704ILR[perfil_704ILR.Id_704ILR] = new List<BE_Permiso_704ILR>();
                    BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Resolver permisos del perfil incluido");
                }

                nodoPerfil_704ILR.Checked = incluidos_704ILR.Contains(perfil_704ILR.Id_704ILR);
                if (nodoPerfil_704ILR.Checked) PropagarHijos_704ILR(nodoPerfil_704ILR, true);

                _nodoPerfiles_704ILR.Nodes.Add(nodoPerfil_704ILR);
                if (nodoPerfil_704ILR.Checked) nodoPerfil_704ILR.Expand();
            }

            _tree_704ILR.Nodes.Add(_nodoPerfiles_704ILR);
            MarcarNegrita_704ILR(_nodoPerfiles_704ILR);
            _nodoPerfiles_704ILR.Expand();
        }

        // Tilda los componentes asignados. Un grupo asignado concede todo su
        // contenido, asi que sus descendientes tambien se muestran tildados: la
        // pantalla refleja lo que el perfil realmente otorga.
        private void AplicarChecks_704ILR(TreeNodeCollection nodes_704ILR, HashSet<int> asignados_704ILR, bool grupoAsignado_704ILR)
        {
            foreach (TreeNode n_704ILR in nodes_704ILR)
            {
                bool tildado_704ILR = grupoAsignado_704ILR || (n_704ILR.Tag is int id_704ILR && asignados_704ILR.Contains(id_704ILR));
                n_704ILR.Checked = tildado_704ILR;
                AplicarChecks_704ILR(n_704ILR.Nodes, asignados_704ILR, tildado_704ILR);
            }
        }

        private void Tree_AfterCheck_704ILR(object sender_704ILR, TreeViewEventArgs e_704ILR)
        {
            if (_suppressAfterCheck_704ILR) return;
            _suppressAfterCheck_704ILR = true;
            if (e_704ILR.Node.Tag as string == TagRamaPerfiles_704ILR)
            {
                // El titulo de la rama de perfiles no es seleccionable.
                e_704ILR.Node.Checked = false;
            }
            else if (e_704ILR.Node.Tag is BE_Perfil_704ILR)
            {
                // (Des)incluir un perfil: sus hijos informativos lo siguen y los
                // permisos que aporta se reflejan en el arbol principal.
                PropagarHijos_704ILR(e_704ILR.Node, e_704ILR.Node.Checked);
                if (e_704ILR.Node.Checked) e_704ILR.Node.Expand();
                ActualizarHerencia_704ILR();
            }
            else if (e_704ILR.Node.Tag is BE_Permiso_704ILR && e_704ILR.Node.Parent?.Tag is BE_Perfil_704ILR)
            {
                // Los permisos mostrados bajo un perfil incluido son informativos:
                // siguen el estado del perfil, no se tildan sueltos.
                e_704ILR.Node.Checked = e_704ILR.Node.Parent.Checked;
            }
            else if (e_704ILR.Node.Tag is int id_704ILR && !e_704ILR.Node.Checked && _marcadosHeredados_704ILR.Contains(id_704ILR))
            {
                // Un permiso heredado no se destilda a mano: se quita destildando
                // el perfil incluido que lo aporta.
                e_704ILR.Node.Checked = true;
            }
            else
            {
                RegistrarTildeDelUsuario_704ILR(e_704ILR.Node);
                PropagarHijos_704ILR(e_704ILR.Node, e_704ILR.Node.Checked);
                // El cascadeo pudo tildar/destildar permisos heredados: se
                // restablecen sus marcas y tildes.
                ActualizarHerencia_704ILR();
            }
            SincronizarGrupos_704ILR(_tree_704ILR.Nodes);
            _suppressAfterCheck_704ILR = false;
        }

        // Tilde que el usuario pone o saca sobre un permiso del arbol principal. Tildar el
        // nodo de un grupo lo asigna como grupo, junto con los grupos que contiene; destildar
        // ese nodo lo deja de asignar. Tildar o destildar hojas no cambia que grupos se
        // asignan: completar un grupo tildando sus hojas guarda las hojas, y destildar una
        // hoja y volver a tildarla deja todo como estaba.
        private void RegistrarTildeDelUsuario_704ILR(TreeNode nodo_704ILR)
        {
            if (nodo_704ILR.Tag is int) MarcarGrupos_704ILR(nodo_704ILR, nodo_704ILR.Checked);
        }

        // Suma (o saca) el nodo, si es un grupo, y los grupos de su contenido a los tildados por el usuario.
        private void MarcarGrupos_704ILR(TreeNode nodo_704ILR, bool tildado_704ILR)
        {
            if (nodo_704ILR.Nodes.Count == 0 || !(nodo_704ILR.Tag is int id_704ILR)) return;
            if (tildado_704ILR) _gruposTildados_704ILR.Add(id_704ILR);
            else _gruposTildados_704ILR.Remove(id_704ILR);
            foreach (TreeNode hijo_704ILR in nodo_704ILR.Nodes) MarcarGrupos_704ILR(hijo_704ILR, tildado_704ILR);
        }

        private void PropagarHijos_704ILR(TreeNode node_704ILR, bool valor_704ILR)
        {
            foreach (TreeNode hijo_704ILR in node_704ILR.Nodes)
            {
                hijo_704ILR.Checked = valor_704ILR;
                PropagarHijos_704ILR(hijo_704ILR, valor_704ILR);
            }
        }

        // Un grupo figura tildado solo si todos sus hijos lo estan: destildar un hijo
        // destilda sus grupos y completar los hijos vuelve a tildarlos. Antes el grupo
        // quedaba tildado con un hijo destildado y, guardado asi, concedia ese hijo igual.
        private void SincronizarGrupos_704ILR(TreeNodeCollection nodes_704ILR)
        {
            foreach (TreeNode n_704ILR in nodes_704ILR)
            {
                if (n_704ILR == _nodoPerfiles_704ILR || !(n_704ILR.Tag is int) || n_704ILR.Nodes.Count == 0) continue;
                SincronizarGrupos_704ILR(n_704ILR.Nodes);
                bool todos_704ILR = n_704ILR.Nodes.Cast<TreeNode>().All(h_704ILR => h_704ILR.Checked);
                if (n_704ILR.Checked != todos_704ILR) n_704ILR.Checked = todos_704ILR;
            }
        }

        private void Guardar_704ILR()
        {
            // Segunda capa del control de acceso: editar la composicion de un
            // perfil redefine que puede hacer el resto del sistema.
            if (!Permisos_704ILR.Exigir_704ILR("PERFILES_GESTION", FindForm(), "guardar la composicion de un perfil")) return;

            _lblOk_704ILR.Visible = false;
            _lblError_704ILR.Visible = false;
            if (!(_cboPerfil_704ILR.SelectedValue is int perfilId_704ILR))
            {
                MostrarError_704ILR(() => T_704ILR("MSG_PERF_SELECCIONE", "Seleccione un perfil."));
                return;
            }
            // El arbol tiene que mostrar el catalogo de permisos y la composicion del perfil
            // elegido: si alguno no se pudo cargar, grabar reemplazaria la composicion con la
            // de otro perfil o con una vacia.
            if (!_arbolCargado_704ILR || _perfilCargadoId_704ILR != perfilId_704ILR)
            {
                MostrarError_704ILR(() => T_704ILR("MSG_PERF_NO_CARGADO", "No se pudo cargar la composición de este perfil. Vuelva a seleccionarlo antes de guardar."));
                return;
            }
            try
            {
                var ids_704ILR = new List<int>();
                var incluidos_704ILR = new List<int>();
                RecolectarComposicion_704ILR(ids_704ILR, incluidos_704ILR);

                PerfilResult_704ILR res_704ILR = BLL_Perfil_704ILR.GuardarComposicion_704ILR(perfilId_704ILR, ids_704ILR, incluidos_704ILR);
                if (res_704ILR == PerfilResult_704ILR.ReferenciaCircular_704ILR)
                {
                    MostrarError_704ILR(() => T_704ILR("MSG_PERF_CICLO", "No se puede incluir ese perfil: generaría una referencia circular."));
                    return;
                }
                if (res_704ILR == PerfilResult_704ILR.SinGestorDePerfiles_704ILR)
                {
                    MostrarError_704ILR(() => T_704ILR("MSG_PERF_SIN_GESTOR", TextoSinGestor_704ILR));
                    return;
                }
                if (res_704ILR == PerfilResult_704ILR.ReferenciaInvalida_704ILR)
                {
                    MostrarError_704ILR(() => T_704ILR("MSG_PERF_REFERENCIA", TextoReferenciaInvalida_704ILR));
                    return;
                }

                // Refresca la rama: los permisos heredados que muestran los demas
                // perfiles pueden haber cambiado con esta edicion.
                CargarAsignacionesPerfil_704ILR(conservarPosicion_704ILR: true);
                MostrarOk_704ILR(() => T_704ILR("MSG_PERF_OK", "Permisos guardados. Los cambios rigen desde el próximo inicio de sesión."));
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Guardar permisos");
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        // Recoge los componentes que el perfil concede en forma directa. Una hoja cuenta si
        // esta tildada y no por herencia. Un grupo cuyo contenido cuenta entero se guarda
        // como grupo solo si ya estaba asignado al cargar el perfil o si el usuario tildo su
        // nodo: completarlo tildando sus hojas una por una guarda las hojas, y asi una hoja
        // que el catalogo sume despues al grupo no se concede sola. Lo que acompana al grupo
        // guardado depende de su origen. El grupo que ya estaba asignado conserva el
        // contenido con el que se cargo, ni mas ni menos: guardar sin tocar nada no cambia la
        // composicion aunque el catalogo le haya sumado hojas, y destildar y volver a tildar
        // su nodo tampoco. El grupo que el usuario asigna ahora tildando su nodo se guarda con
        // todo su contenido. Un grupo incompleto, o completo solo gracias a la herencia, no se
        // guarda: quedan sus hijos directos. 'cubierto': un grupo de arriba ya se guarda
        // ('conContenido': lo asigna ahora el usuario y lleva todo su contenido; si no, solo
        // lo que ya estaba asignado).
        private void RecolectarDirectos_704ILR(TreeNode n_704ILR, List<int> ids_704ILR, bool cubierto_704ILR, bool conContenido_704ILR)
        {
            if (!(n_704ILR.Tag is int id_704ILR)) return;
            if (cubierto_704ILR)
            {
                if (conContenido_704ILR || _asignadosCargados_704ILR.Contains(id_704ILR)) ids_704ILR.Add(id_704ILR);
                foreach (TreeNode h_704ILR in n_704ILR.Nodes) RecolectarDirectos_704ILR(h_704ILR, ids_704ILR, true, conContenido_704ILR);
                return;
            }
            if (n_704ILR.Nodes.Count == 0)
            {
                if (EsDirecto_704ILR(n_704ILR)) ids_704ILR.Add(id_704ILR);
                return;
            }
            bool asignado_704ILR = _asignadosCargados_704ILR.Contains(id_704ILR);
            bool comoGrupo_704ILR = TodoDirecto_704ILR(n_704ILR) && (asignado_704ILR || _gruposTildados_704ILR.Contains(id_704ILR));
            if (comoGrupo_704ILR) ids_704ILR.Add(id_704ILR);
            foreach (TreeNode h_704ILR in n_704ILR.Nodes)
                RecolectarDirectos_704ILR(h_704ILR, ids_704ILR, comoGrupo_704ILR, comoGrupo_704ILR && !asignado_704ILR);
        }

        // Hoja (o grupo vacio) tildada por el perfil y no por herencia.
        private bool EsDirecto_704ILR(TreeNode n_704ILR) =>
            n_704ILR.Tag is int id_704ILR && n_704ILR.Checked && !_marcadosHeredados_704ILR.Contains(id_704ILR);

        // Todo el contenido del nodo cuenta como concedido en forma directa.
        private bool TodoDirecto_704ILR(TreeNode n_704ILR) =>
            n_704ILR.Nodes.Count == 0
                ? EsDirecto_704ILR(n_704ILR)
                : n_704ILR.Tag is int && n_704ILR.Nodes.Cast<TreeNode>().All(TodoDirecto_704ILR);

        // Composicion que muestra el arbol, tal como la envia Guardar: los componentes concedidos
        // en forma directa (los tildes heredados los puso el sistema y viven en el perfil
        // incluido que los aporta) y los perfiles incluidos tildados en la rama del Composite de
        // perfiles.
        private void RecolectarComposicion_704ILR(List<int> ids_704ILR, List<int> incluidos_704ILR)
        {
            foreach (TreeNode n_704ILR in _tree_704ILR.Nodes)
                if (n_704ILR != _nodoPerfiles_704ILR) RecolectarDirectos_704ILR(n_704ILR, ids_704ILR, false, false);
            if (_nodoPerfiles_704ILR != null)
                foreach (TreeNode n_704ILR in _nodoPerfiles_704ILR.Nodes)
                    if (n_704ILR.Checked && n_704ILR.Tag is BE_Perfil_704ILR p_704ILR) incluidos_704ILR.Add(p_704ILR.Id_704ILR);
        }

        // La composicion en una cadena comparable, sin importar el orden: "permisos|incluidos".
        private string FotoComposicion_704ILR()
        {
            var ids_704ILR = new List<int>();
            var incluidos_704ILR = new List<int>();
            RecolectarComposicion_704ILR(ids_704ILR, incluidos_704ILR);
            return string.Join(",", ids_704ILR.Distinct().OrderBy(i_704ILR => i_704ILR)) + "|" +
                   string.Join(",", incluidos_704ILR.Distinct().OrderBy(i_704ILR => i_704ILR));
        }

        // Permisos o perfiles incluidos tildados sin guardar: el arbol muestra la composicion del
        // perfil cargado y Guardar enviaria una distinta de la que se cargo (o se guardo).
        private bool HayCambiosEnComposicion_704ILR =>
            _arbolCargado_704ILR && _perfilCargadoId_704ILR.HasValue && _lineaBaseComposicion_704ILR != null
            && FotoComposicion_704ILR() != _lineaBaseComposicion_704ILR;

        // IVistaConCambios: hay cambios sin guardar si Guardar permisos o Guardar asignaciones
        // grabarian algo: la composicion del perfil cargado difiere de su linea base o alguna fila
        // de la grilla tiene un perfil distinto del que tiene la cuenta.
        bool IVistaConCambios_704ILR.HayCambiosSinGuardar_704ILR =>
            !IsDisposed && (HayCambiosEnComposicion_704ILR || CambiosDeAsignacion_704ILR().Count > 0);

        // true si se puede reemplazar la composicion que muestra el arbol por la del perfil elegido:
        // no tiene cambios sin guardar o el usuario acepto descartarlos. Mientras la pregunta esta
        // abierta la lista del combo puede cerrarse y el combo cambiar solo: al responder queda en el
        // perfil elegido (se va a cargar) o, si el usuario sigue editando, en el perfil cargado. Las
        // asignaciones de la grilla no dependen del perfil elegido y se conservan: por eso la
        // pregunta nombra solo los permisos.
        private bool PuedeDescartarComposicion_704ILR(int perfilElegidoId_704ILR)
        {
            if (!HayCambiosEnComposicion_704ILR) return true;
            bool descartar_704ILR;
            _preguntandoDescarte_704ILR = true;
            try
            {
                descartar_704ILR = MessageBox.Show(FindForm(),
                    T_704ILR("MSG_PERF_DESCARTAR_PERMISOS", "Hay cambios sin guardar en los permisos de este perfil. ¿Descartarlos y cambiar de perfil?"),
                    "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            }
            finally { _preguntandoDescarte_704ILR = false; }
            SeleccionarSinCargar_704ILR(descartar_704ILR ? perfilElegidoId_704ILR : _perfilCargadoId_704ILR);
            return descartar_704ILR;
        }

        // Deja el combo en el perfil dado sin que la seleccion cuente como una eleccion nueva.
        private void SeleccionarSinCargar_704ILR(int? perfilId_704ILR)
        {
            if (!perfilId_704ILR.HasValue) return;
            int indice_704ILR = IndicePerfil_704ILR(perfilId_704ILR);
            if (_cboPerfil_704ILR.SelectedIndex == indice_704ILR) return;
            _seleccionPorPrograma_704ILR = true;
            try { _cboPerfil_704ILR.SelectedIndex = indice_704ILR; }
            finally { _seleccionPorPrograma_704ILR = false; }
        }

        // Posicion del perfil en el combo; 0 (el primero) si no hay perfil o ya no esta en la lista.
        private int IndicePerfil_704ILR(int? perfilId_704ILR)
        {
            if (perfilId_704ILR.HasValue)
                for (int i_704ILR = 0; i_704ILR < _cboPerfil_704ILR.Items.Count; i_704ILR++)
                    if (_cboPerfil_704ILR.Items[i_704ILR] is BE_Perfil_704ILR p_704ILR && p_704ILR.Id_704ILR == perfilId_704ILR.Value) return i_704ILR;
            return 0;
        }

        private void NuevoPerfil_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("PERFILES_GESTION", FindForm(), "crear un perfil")) return;
            using (var dlg_704ILR = new frmNuevoPerfil_704ILR())
            {
                if (dlg_704ILR.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    // La lista se relee sin cambiar el perfil que muestra el arbol. Pasar al perfil
                    // nuevo es elegir otro perfil: con permisos tildados sin guardar se pregunta
                    // antes y, si el usuario sigue editando, queda el perfil anterior.
                    CargarPerfiles_704ILR();
                    _cboPerfil_704ILR.SelectedValue = dlg_704ILR.NuevoId_704ILR;
                    // El perfil nuevo se suma a las opciones de la grilla sin recargarla:
                    // recargar descartaria los perfiles elegidos y todavia no guardados.
                    RefrescarOpcionesPerfil_704ILR();
                }
            }
        }

        private const string TextoReferenciaInvalida_704ILR = "No se guardó: un perfil, un permiso o una cuenta ya no existe. Volvé a abrir la sección para ver los datos actuales.";

        private const string TextoSinGestor_704ILR = "No se puede guardar: ningún usuario activo quedaría con permiso para gestionar perfiles y desbloquear cuentas.";

        private void MostrarError_704ILR(Func<string> texto_704ILR)
        {
            _textoError_704ILR = texto_704ILR;
            _lblError_704ILR.Text = texto_704ILR();
            _lblError_704ILR.Visible = true;
        }

        private void MostrarOk_704ILR(Func<string> texto_704ILR)
        {
            _textoOk_704ILR = texto_704ILR;
            _lblOk_704ILR.Text = texto_704ILR();
            _lblOk_704ILR.Visible = true;
        }

        private void RetraducirMensajes_704ILR()
        {
            if (_lblOk_704ILR != null && _textoOk_704ILR != null) _lblOk_704ILR.Text = _textoOk_704ILR();
            if (_lblError_704ILR != null && _textoError_704ILR != null) _lblError_704ILR.Text = _textoError_704ILR();
            if (_lblMsgAsig_704ILR != null && _textoMsgAsig_704ILR != null) _lblMsgAsig_704ILR.Text = _textoMsgAsig_704ILR();
        }

        // Devuelve la traduccion de 'clave' o, si falta, el texto por defecto dado.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        // ===================== Asignacion a usuarios =====================
        // Devuelve false si no se pudo cargar (el error queda a la vista).
        private bool CargarUsuarios_704ILR()
        {
            try
            {
                // Opciones del combo: "(sin perfil)" (Id=0) + perfiles existentes.
                AsignarOpcionesPerfil_704ILR(BLL_Perfil_704ILR.GetPerfiles_704ILR());

                _gridUsuarios_704ILR.Rows.Clear();
                foreach (var u_704ILR in BLL_User_704ILR.GetAll_704ILR())
                {
                    int idx_704ILR = _gridUsuarios_704ILR.Rows.Add(u_704ILR.Username_704ILR);
                    var row_704ILR = _gridUsuarios_704ILR.Rows[idx_704ILR];
                    row_704ILR.Tag = u_704ILR;
                    row_704ILR.Cells["cPerfil"].Value = u_704ILR.PerfilId_704ILR ?? 0;
                    PintarEstadoUsuario_704ILR(row_704ILR);
                }
                return true;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Cargar usuarios");
                MensajeAsig_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), error_704ILR: true);
                return false;
            }
        }

        private void AsignarOpcionesPerfil_704ILR(IEnumerable<BE_Perfil_704ILR> perfiles_704ILR)
        {
            var opciones_704ILR = new List<BE_Perfil_704ILR> { new BE_Perfil_704ILR { Id_704ILR = 0, Nombre_704ILR = Tr_704ILR.T_704ILR("PERF_SIN") } };
            opciones_704ILR.AddRange(perfiles_704ILR);
            var col_704ILR = (DataGridViewComboBoxColumn)_gridUsuarios_704ILR.Columns["cPerfil"];
            col_704ILR.DataSource = opciones_704ILR;
            col_704ILR.ValueMember = "Id_704ILR";
            col_704ILR.DisplayMember = "Nombre_704ILR";
        }

        // Suma a la grilla los perfiles creados, conservando lo elegido en cada fila.
        private void RefrescarOpcionesPerfil_704ILR()
        {
            try
            {
                _gridUsuarios_704ILR.EndEdit();
                AsignarOpcionesPerfil_704ILR(BLL_Perfil_704ILR.GetPerfiles_704ILR());
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Actualizar perfiles de la grilla");
                MensajeAsig_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), error_704ILR: true);
            }
        }

        // Los valores de la grilla se arman en el idioma del momento de la carga: al
        // cambiar de idioma se re-traducen ahi mismo, sin volver a la base (recargar
        // descartaria los perfiles elegidos y todavia no guardados).
        private void RetraducirGrillaUsuarios_704ILR()
        {
            if (_gridUsuarios_704ILR.IsCurrentCellInEditMode) _gridUsuarios_704ILR.EndEdit();
            var col_704ILR = (DataGridViewComboBoxColumn)_gridUsuarios_704ILR.Columns["cPerfil"];
            if (col_704ILR.DataSource is List<BE_Perfil_704ILR> opciones_704ILR && opciones_704ILR.Count > 0)
                AsignarOpcionesPerfil_704ILR(opciones_704ILR.Where(p_704ILR => p_704ILR.Id_704ILR != 0).ToList());
            foreach (DataGridViewRow row_704ILR in _gridUsuarios_704ILR.Rows)
                PintarEstadoUsuario_704ILR(row_704ILR);
            _gridUsuarios_704ILR.Invalidate();
        }

        private static string TextoEstadoUsuario_704ILR(BE_User_704ILR u_704ILR) =>
            u_704ILR.Blocked_704ILR ? Tr_704ILR.T_704ILR("EST_BLOQUEADO")
            : (u_704ILR.Activo_704ILR ? Tr_704ILR.T_704ILR("EST_ACTIVO") : Tr_704ILR.T_704ILR("EST_INACTIVO"));

        // Estado, color e icono de desbloqueo de la fila de una cuenta.
        private static void PintarEstadoUsuario_704ILR(DataGridViewRow row_704ILR)
        {
            if (!(row_704ILR.Tag is BE_User_704ILR u_704ILR)) return;
            row_704ILR.Cells["cEstado"].Value = TextoEstadoUsuario_704ILR(u_704ILR);
            row_704ILR.Cells["cEstado"].Style.ForeColor = u_704ILR.Blocked_704ILR ? Theme_704ILR.Error_704ILR : Color.Empty;
            // Icono de candado (desbloquear) solo para cuentas bloqueadas.
            row_704ILR.Cells["cDesbloq"].Value = u_704ILR.Blocked_704ILR ? Theme_704ILR.IcoUnlock_704ILR : "";
        }

        private void GuardarAsignaciones_704ILR()
        {
            // Segunda capa del control de acceso (ver Permisos.cs). Asignar perfiles
            // es la accion mas sensible de la pantalla —puede elevar una cuenta a
            // Administrador—, asi que vuelve a exigir el permiso al ejecutarse y no
            // solo al mostrar la seccion.
            if (!Permisos_704ILR.Exigir_704ILR("PERFILES_GESTION", FindForm(), "asignar perfiles a usuarios")) return;
            try
            {
                _gridUsuarios_704ILR.EndEdit();
                // Se persisten SOLO las filas cuyo perfil cambio respecto del que se
                // cargo (el original viaja en el Tag). Reescribir todas las filas
                // asentaba una "Asignacion de perfil" por cada usuario aunque no se
                // hubiera tocado ninguno (ruido en la bitacora) y reponia, con la foto
                // vieja de la grilla, un perfil modificado desde otra sesion.
                Dictionary<int, int?> cambios_704ILR = CambiosDeAsignacion_704ILR();
                // Las filas cambiadas se graban juntas: la regla de que siempre quede
                // alguien que gestione perfiles se evalua sobre el resultado completo.
                AsignacionPerfilResult_704ILR resAsig_704ILR = cambios_704ILR.Count > 0
                    ? BLL_User_704ILR.AsignarPerfiles_704ILR(cambios_704ILR) : AsignacionPerfilResult_704ILR.Success_704ILR;
                if (resAsig_704ILR == AsignacionPerfilResult_704ILR.SinGestorDePerfiles_704ILR)
                {
                    // No se graba nada y la grilla conserva lo elegido para corregirlo.
                    MensajeAsig_704ILR(() => T_704ILR("MSG_PERF_SIN_GESTOR", TextoSinGestor_704ILR), error_704ILR: true);
                    return;
                }
                // Una cuenta o un perfil de la grilla ya no existe (se borro por fuera): nada se
                // grabo y la grilla conserva lo elegido.
                if (resAsig_704ILR == AsignacionPerfilResult_704ILR.ReferenciaInvalida_704ILR)
                {
                    MensajeAsig_704ILR(() => T_704ILR("MSG_PERF_REFERENCIA", TextoReferenciaInvalida_704ILR), error_704ILR: true);
                    return;
                }
                // La grilla se recarga con lo persistido: el proximo guardado parte
                // del estado real de la base y no de la foto anterior. Si la recarga
                // falla, su error queda a la vista.
                if (!CargarUsuarios_704ILR()) return;
                bool huboCambios_704ILR = cambios_704ILR.Count > 0;
                MensajeAsig_704ILR(() => huboCambios_704ILR
                    ? Tr_704ILR.T_704ILR("MSG_PERF_ASIG_OK")
                    : T_704ILR("MSG_PERF_ASIG_SIN_CAMBIOS", "No hay cambios de perfil para guardar."), error_704ILR: false);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Guardar asignaciones de usuarios");
                MensajeAsig_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), error_704ILR: true);
            }
        }

        // Filas cuyo perfil elegido difiere del que tenia la cuenta al cargarse la grilla (el
        // original viaja en el Tag): lo que grabaria Guardar asignaciones. Cuenta tambien lo
        // elegido en la celda que se esta editando y todavia no se confirmo, leido de su combo de
        // edicion SIN confirmarlo: frmMain consulta los cambios al cambiar de seccion y, si el
        // usuario sigue en la vista, Escape tiene que poder revertir esa eleccion. Guardar
        // asignaciones confirma la edicion antes de llamar a este metodo.
        private Dictionary<int, int?> CambiosDeAsignacion_704ILR()
        {
            int filaEnEdicion_704ILR = -1;
            object pendiente_704ILR = null;
            DataGridViewCell actual_704ILR = _gridUsuarios_704ILR.CurrentCell;
            if (_gridUsuarios_704ILR.IsCurrentCellDirty && actual_704ILR?.OwningColumn?.Name == "cPerfil"
                && _gridUsuarios_704ILR.EditingControl is ComboBox edicion_704ILR && edicion_704ILR.SelectedItem is BE_Perfil_704ILR elegido_704ILR)
            {
                filaEnEdicion_704ILR = actual_704ILR.RowIndex;
                pendiente_704ILR = elegido_704ILR.Id_704ILR;
            }
            var cambios_704ILR = new Dictionary<int, int?>();
            foreach (DataGridViewRow row_704ILR in _gridUsuarios_704ILR.Rows)
            {
                if (!(row_704ILR.Tag is BE_User_704ILR u_704ILR)) continue;
                object valor_704ILR = row_704ILR.Index == filaEnEdicion_704ILR ? pendiente_704ILR : row_704ILR.Cells["cPerfil"].Value;
                int val_704ILR = valor_704ILR is int v_704ILR ? v_704ILR : 0;
                int? nuevo_704ILR = val_704ILR == 0 ? (int?)null : val_704ILR;
                if (nuevo_704ILR == u_704ILR.PerfilId_704ILR) continue;
                cambios_704ILR[u_704ILR.Id_704ILR] = nuevo_704ILR;
            }
            return cambios_704ILR;
        }

        private void MensajeAsig_704ILR(Func<string> texto_704ILR, bool error_704ILR)
        {
            _textoMsgAsig_704ILR = texto_704ILR;
            _lblMsgAsig_704ILR.ForeColor = error_704ILR ? Theme_704ILR.Error_704ILR : Theme_704ILR.Success_704ILR;
            _lblMsgAsig_704ILR.Text = texto_704ILR();
            _lblMsgAsig_704ILR.Visible = true;
        }
    }
}
