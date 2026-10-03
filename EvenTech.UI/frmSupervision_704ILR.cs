using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Supervision de la ejecucion del evento (CUN011). El supervisor de operaciones
    // inicia la ejecucion, tiene a la vista el plan (cronograma y tareas, de solo
    // lectura), anota lo que se sale de el como incidencias, las da por resueltas y
    // cierra el evento. Cada operacion persiste en el acto.
    public class frmSupervision_704ILR : frmEventoBase_704ILR
    {
        private DataGridView _gridCronograma_704ILR, _gridTareas_704ILR, _gridIncidencias_704ILR;
        private ComboBox _cboTipo_704ILR, _cboReporta_704ILR;
        private TextBox _txtDescripcion_704ILR;
        private AppButton_704ILR _btnIniciar_704ILR, _btnCerrarEvento_704ILR, _btnRegistrar_704ILR, _btnResolver_704ILR;
        private Label _lblAviso_704ILR, _lblResumen_704ILR;

        public frmSupervision_704ILR(int reservaId_704ILR) : base(reservaId_704ILR)
        {
            BuildUi_704ILR();
            Refrescar_704ILR();
            // El foco inicial no cae en un boton que cambia el estado del evento: Enter
            // registra la incidencia tipeada (o no hace nada si el evento no empezo).
            ActiveControl = _txtDescripcion_704ILR.Enabled ? (Control)_txtDescripcion_704ILR : _gridIncidencias_704ILR;
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(1240, 690);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // encabezado
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // acciones de ejecucion + aviso
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // plan | incidencias
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // pie

            // --- Acciones de ejecucion ---
            var ejecucion_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };
            ejecucion_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ejecucion_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ejecucion_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _btnIniciar_704ILR = Boton_704ILR(T_704ILR("SUP_INICIAR", "Iniciar ejecución"), Theme_704ILR.IcoIniciar_704ILR, 180);
            _btnIniciar_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _btnIniciar_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Iniciar_704ILR, "Iniciar ejecucion");
            _btnCerrarEvento_704ILR = Boton_704ILR(T_704ILR("SUP_CERRAR_EVENTO", "Cerrar evento"), Theme_704ILR.IcoOk_704ILR, 160);
            _btnCerrarEvento_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            _btnCerrarEvento_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(CerrarEvento_704ILR, "Cerrar evento");
            _lblAviso_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.Warning_704ILR, BackColor = Color.Transparent, Anchor = AnchorStyles.Left, Margin = new Padding(0), MaximumSize = new Size(800, 0) };
            ejecucion_704ILR.Controls.Add(_btnIniciar_704ILR, 0, 0);
            ejecucion_704ILR.Controls.Add(_btnCerrarEvento_704ILR, 1, 0);
            ejecucion_704ILR.Controls.Add(_lblAviso_704ILR, 2, 0);

            // --- Cuerpo: el plan a la izquierda, las incidencias a la derecha ---
            var body_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body_704ILR.Controls.Add(BuildPlan_704ILR(), 0, 0);
            body_704ILR.Controls.Add(BuildIncidencias_704ILR(), 1, 0);

            // --- Pie: resumen + cerrar el dialogo ---
            var footer_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, BackColor = Color.Transparent };
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _lblResumen_704ILR = new Label { Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 0, 0), BackColor = Color.Transparent };
            var btnCerrar_704ILR = Boton_704ILR(T_704ILR("BTN_CERRAR", "Cerrar"), Theme_704ILR.IcoClose_704ILR, 120);
            btnCerrar_704ILR.Anchor = AnchorStyles.Right;
            btnCerrar_704ILR.Click += (s_704ILR, e_704ILR) => Close();
            footer_704ILR.Controls.Add(_lblResumen_704ILR, 0, 0);
            footer_704ILR.Controls.Add(btnCerrar_704ILR, 1, 0);
            CancelButton = btnCerrar_704ILR;

            root_704ILR.Controls.Add(ArmarEncabezado_704ILR(), 0, 0);
            root_704ILR.Controls.Add(ejecucion_704ILR, 0, 1);
            root_704ILR.Controls.Add(body_704ILR, 0, 2);
            root_704ILR.Controls.Add(footer_704ILR, 0, 3);

            Controls.Add(root_704ILR);
            Controls.Add(ArmarTitulo_704ILR(T_704ILR("SUP_TITULO", "Supervisión del evento")));
            // Enter registra la incidencia tipeada.
            AcceptButton = _btnRegistrar_704ILR;
        }

        // El plan del evento, de solo lectura: cronograma arriba, tareas abajo.
        private Control BuildPlan_704ILR()
        {
            var plan_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent, Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0) };
            plan_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            plan_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            plan_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
            plan_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            plan_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 44));

            _gridCronograma_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_gridCronograma_704ILR);
            _gridCronograma_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cHora", HeaderText = T_704ILR("CRO_COL_HORA", "Hora"), FillWeight = 36 });
            _gridCronograma_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cActividad", HeaderText = T_704ILR("CRO_ACTIVIDAD", "Actividad"), FillWeight = 100 });
            _gridCronograma_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cResponsable", HeaderText = T_704ILR("CRO_COL_RESPONSABLE", "Responsable"), FillWeight = 72 });
            _gridCronograma_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDuracion", HeaderText = T_704ILR("CRO_MIN", "min"), FillWeight = 30, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            foreach (DataGridViewColumn c_704ILR in _gridCronograma_704ILR.Columns) c_704ILR.SortMode = DataGridViewColumnSortMode.NotSortable;
            UiGrid_704ILR.Multilinea_704ILR(_gridCronograma_704ILR, "cActividad", "cResponsable");
            UiGrid_704ILR.AlContenido_704ILR(_gridCronograma_704ILR, "cHora", "cDuracion");
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_gridCronograma_704ILR);

            _gridTareas_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_gridTareas_704ILR);
            _gridTareas_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEmpleado", HeaderText = T_704ILR("EMP_COL_EMPLEADO", "Empleado"), FillWeight = 65 });
            _gridTareas_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTarea", HeaderText = T_704ILR("TAR_COL_TAREA", "Tarea"), FillWeight = 95 });
            _gridTareas_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFranja", HeaderText = T_704ILR("ASG_COL_FRANJA", "Franja"), FillWeight = 48 });
            Coord_704ILR.SinOrden_704ILR(_gridTareas_704ILR);
            UiGrid_704ILR.Multilinea_704ILR(_gridTareas_704ILR, "cEmpleado", "cTarea");
            UiGrid_704ILR.AlContenido_704ILR(_gridTareas_704ILR, "cFranja");
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_gridTareas_704ILR);

            plan_704ILR.Controls.Add(Subtitulo_704ILR(T_704ILR("SUP_CRONOGRAMA", "Cronograma")), 0, 0);
            plan_704ILR.Controls.Add(Tarjeta_704ILR(_gridCronograma_704ILR, Theme_704ILR.SpaceSm_704ILR), 0, 1);
            plan_704ILR.Controls.Add(Subtitulo_704ILR(T_704ILR("SUP_TAREAS", "Tareas")), 0, 2);
            plan_704ILR.Controls.Add(Tarjeta_704ILR(_gridTareas_704ILR, 0), 0, 3);
            return plan_704ILR;
        }

        private Control BuildIncidencias_704ILR()
        {
            var panel_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.Transparent, Margin = new Padding(0) };
            panel_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // --- Alta, primera fila: de que tipo es y quien la informo ---
            var alta_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR) };
            var lblTipo_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("SUP_COL_TIPO", "Tipo")); lblTipo_704ILR.Margin = new Padding(2, 8, Theme_704ILR.SpaceXs_704ILR, 0);
            _cboTipo_704ILR = Ui_704ILR.Combo_704ILR(); _cboTipo_704ILR.Width = 160; _cboTipo_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            _cboTipo_704ILR.FormattingEnabled = true;
            _cboTipo_704ILR.Format += (s_704ILR, e_704ILR) =>
            {
                if (e_704ILR.ListItem is TipoIncidencia_704ILR t_704ILR) e_704ILR.Value = Coord_704ILR.TextoTipo_704ILR(t_704ILR);
            };
            foreach (TipoIncidencia_704ILR t_704ILR in Enum.GetValues(typeof(TipoIncidencia_704ILR))) _cboTipo_704ILR.Items.Add(t_704ILR);
            _cboTipo_704ILR.SelectedIndex = 0;
            var lblReporta_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("SUP_COL_REPORTA", "Informó")); lblReporta_704ILR.Margin = new Padding(0, 8, Theme_704ILR.SpaceXs_704ILR, 0);
            _cboReporta_704ILR = Ui_704ILR.Combo_704ILR(); _cboReporta_704ILR.Width = 230; _cboReporta_704ILR.Margin = new Padding(0);
            alta_704ILR.Controls.Add(lblTipo_704ILR); alta_704ILR.Controls.Add(_cboTipo_704ILR); alta_704ILR.Controls.Add(lblReporta_704ILR); alta_704ILR.Controls.Add(_cboReporta_704ILR);
            CentrarFila_704ILR(alta_704ILR);
            lblTipo_704ILR.Margin = new Padding(2, 0, Theme_704ILR.SpaceXs_704ILR, 0);

            // --- Alta, segunda fila: que paso (todo el ancho) + registrar ---
            var alta2_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR) };
            alta2_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            alta2_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            alta2_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            // La descripcion lleva rotulo, como el tipo y quien informo: el dialogo abre con
            // el foco en ella y el texto de ejemplo solo se ve sin el foco.
            // MaxLength = ancho de Incidencias.Descripcion.
            var lblDescripcion_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("COL_DESCRIPCION", "Descripción"));
            lblDescripcion_704ILR.Anchor = AnchorStyles.Left; lblDescripcion_704ILR.Margin = new Padding(2, 0, Theme_704ILR.SpaceXs_704ILR, 0);
            _txtDescripcion_704ILR = Ui_704ILR.Input_704ILR(); _txtDescripcion_704ILR.MaxLength = BLL_Incidencia_704ILR.MaxDescripcion_704ILR;
            _txtDescripcion_704ILR.Anchor = AnchorStyles.Left | AnchorStyles.Right; _txtDescripcion_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _txtDescripcion_704ILR.PlaceholderText = T_704ILR("SUP_DESCRIPCION", "Qué pasó");
            _btnRegistrar_704ILR = Boton_704ILR(T_704ILR("SUP_REGISTRAR", "Registrar"), Theme_704ILR.IcoAdd_704ILR, 120);
            _btnRegistrar_704ILR.Margin = new Padding(0);
            _btnRegistrar_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Registrar_704ILR, "Registrar incidencia");
            alta2_704ILR.Controls.Add(lblDescripcion_704ILR, 0, 0);
            alta2_704ILR.Controls.Add(_txtDescripcion_704ILR, 1, 0);
            alta2_704ILR.Controls.Add(_btnRegistrar_704ILR, 2, 0);

            _gridIncidencias_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_gridIncidencias_704ILR);
            // La fecha va completa: un evento puede cruzar la medianoche, y las incidencias
            // de dias distintos, con la hora sola, parecian desordenadas.
            _gridIncidencias_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFecha", HeaderText = T_704ILR("COL_FECHA", "Fecha"), FillWeight = 60 });
            _gridIncidencias_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTipo", HeaderText = T_704ILR("SUP_COL_TIPO", "Tipo"), FillWeight = 60 });
            _gridIncidencias_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDescripcion", HeaderText = T_704ILR("COL_DESCRIPCION", "Descripción"), FillWeight = 110 });
            _gridIncidencias_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cReporta", HeaderText = T_704ILR("SUP_COL_REPORTA", "Informó"), FillWeight = 72 });
            _gridIncidencias_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstado", HeaderText = T_704ILR("COL_ESTADO", "Estado"), FillWeight = 50 });
            _gridIncidencias_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cResolucion", HeaderText = T_704ILR("SUP_COL_RESOLUCION", "Resolución"), FillWeight = 90 });
            Coord_704ILR.SinOrden_704ILR(_gridIncidencias_704ILR);
            UiGrid_704ILR.Multilinea_704ILR(_gridIncidencias_704ILR, "cDescripcion", "cReporta", "cResolucion");
            UiGrid_704ILR.AlContenido_704ILR(_gridIncidencias_704ILR, "cFecha", "cTipo", "cEstado");
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_gridIncidencias_704ILR);
            _gridIncidencias_704ILR.SelectionChanged += (s_704ILR, e_704ILR) => ActualizarAcciones_704ILR();

            _btnResolver_704ILR = Boton_704ILR(T_704ILR("SUP_RESOLVER", "Resolver"), Theme_704ILR.IcoOk_704ILR, 130, secundario_704ILR: true);
            _btnResolver_704ILR.Anchor = AnchorStyles.Right; _btnResolver_704ILR.Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0);
            _btnResolver_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Resolver_704ILR, "Resolver incidencia");

            panel_704ILR.Controls.Add(Subtitulo_704ILR(T_704ILR("SUP_INCIDENCIAS", "Incidencias")), 0, 0);
            panel_704ILR.Controls.Add(alta_704ILR, 0, 1);
            panel_704ILR.Controls.Add(alta2_704ILR, 0, 2);
            panel_704ILR.Controls.Add(Tarjeta_704ILR(_gridIncidencias_704ILR, 0), 0, 3);
            panel_704ILR.Controls.Add(_btnResolver_704ILR, 0, 4);
            return panel_704ILR;
        }

        private static Label Subtitulo_704ILR(string texto_704ILR)
        {
            var l_704ILR = Ui_704ILR.BodyBold_704ILR(texto_704ILR);
            l_704ILR.Margin = new Padding(2, 0, 0, Theme_704ILR.SpaceXs_704ILR);
            return l_704ILR;
        }

        private static CardPanel_704ILR Tarjeta_704ILR(Control contenido_704ILR, int margenInferior_704ILR)
        {
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, margenInferior_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            card_704ILR.Controls.Add(contenido_704ILR);
            return card_704ILR;
        }

        private void Refrescar_704ILR() => Ejecutar_704ILR(RefrescarTodo_704ILR, "Cargar supervision");

        private void RefrescarTodo_704ILR()
        {
            LeerEvento_704ILR();

            _gridCronograma_704ILR.Rows.Clear();
            BE_Cronograma_704ILR cronograma_704ILR = BLL_Cronograma_704ILR.GetByReserva_704ILR(_reservaId_704ILR);
            if (cronograma_704ILR != null)
                foreach (var a_704ILR in cronograma_704ILR.Actividades_704ILR)
                    _gridCronograma_704ILR.Rows.Add(Coord_704ILR.Hora_704ILR(a_704ILR.Hora_704ILR), a_704ILR.Descripcion_704ILR, a_704ILR.ResponsableNombre_704ILR, a_704ILR.DuracionMinutos_704ILR);

            _gridTareas_704ILR.Rows.Clear();
            foreach (var t_704ILR in BLL_Tarea_704ILR.GetByReserva_704ILR(_reservaId_704ILR))
                _gridTareas_704ILR.Rows.Add(t_704ILR.EmpleadoNombre_704ILR, t_704ILR.Descripcion_704ILR, Coord_704ILR.Franja_704ILR(t_704ILR.HoraInicio_704ILR, t_704ILR.HoraFin_704ILR));

            // Quien informo la incidencia: opcional, entre el equipo del evento.
            int elegido_704ILR = _cboReporta_704ILR.SelectedItem is ReportaItem_704ILR r_704ILR ? r_704ILR.EmpleadoId_704ILR ?? 0 : 0;
            _cboReporta_704ILR.Items.Clear();
            _cboReporta_704ILR.Items.Add(new ReportaItem_704ILR(null, null));
            foreach (var a_704ILR in BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(_reservaId_704ILR))
                _cboReporta_704ILR.Items.Add(new ReportaItem_704ILR(a_704ILR.EmpleadoId_704ILR, a_704ILR.EmpleadoNombre_704ILR));
            _cboReporta_704ILR.SelectedIndex = 0;
            for (int i_704ILR = 0; i_704ILR < _cboReporta_704ILR.Items.Count; i_704ILR++)
                if (((ReportaItem_704ILR)_cboReporta_704ILR.Items[i_704ILR]).EmpleadoId_704ILR == elegido_704ILR && elegido_704ILR != 0) { _cboReporta_704ILR.SelectedIndex = i_704ILR; break; }
            Coord_704ILR.AjustarDesplegable_704ILR(_cboReporta_704ILR);

            var incidencias_704ILR = BLL_Incidencia_704ILR.GetByReserva_704ILR(_reservaId_704ILR);
            _gridIncidencias_704ILR.Rows.Clear();
            foreach (var inc_704ILR in incidencias_704ILR)
            {
                int i_704ILR = _gridIncidencias_704ILR.Rows.Add(Coord_704ILR.FechaHora_704ILR(inc_704ILR.FechaHora_704ILR),
                    Coord_704ILR.TextoTipo_704ILR(inc_704ILR.Tipo_704ILR), inc_704ILR.Descripcion_704ILR,
                    inc_704ILR.EmpleadoReportaNombre_704ILR ?? string.Empty, Coord_704ILR.TextoIncidencia_704ILR(inc_704ILR.Estado_704ILR),
                    inc_704ILR.Resolucion_704ILR ?? string.Empty);
                _gridIncidencias_704ILR.Rows[i_704ILR].Tag = inc_704ILR;
                _gridIncidencias_704ILR.Rows[i_704ILR].Cells["cEstado"].Style.ForeColor =
                    inc_704ILR.Estado_704ILR == EstadoIncidencia_704ILR.RESUELTA ? Theme_704ILR.Success_704ILR : Theme_704ILR.Error_704ILR;
            }
            _lblResumen_704ILR.Text = Tr_704ILR.F_704ILR("SUP_RESUMEN", "Incidencias: {0}    Abiertas: {1}",
                incidencias_704ILR.Count, incidencias_704ILR.Count(i_704ILR => i_704ILR.Estado_704ILR == EstadoIncidencia_704ILR.ABIERTA));
            ActualizarAcciones_704ILR();
        }

        // Primera capa: cada accion se ofrece con el permiso y en el estado que
        // corresponde (iniciar con el evento listo; incidencias y cierre en ejecucion).
        protected override void ActualizarAcciones_704ILR()
        {
            if (_btnResolver_704ILR == null) return;
            // Solo se opera sobre el evento de una reserva que sigue CONFIRMADA (RN-08).
            bool permiso_704ILR = Permisos_704ILR.Tiene_704ILR("EJECUCION_SUPERVISAR")
                && _evento_704ILR != null && _evento_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA;
            EstadoCoordinacion_704ILR? estado_704ILR = _evento_704ILR?.EstadoCoordinacion_704ILR;
            bool enEjecucion_704ILR = permiso_704ILR && estado_704ILR == EstadoCoordinacion_704ILR.EN_EJECUCION;

            _btnIniciar_704ILR.Enabled = permiso_704ILR && estado_704ILR == EstadoCoordinacion_704ILR.LISTO;
            _btnCerrarEvento_704ILR.Enabled = enEjecucion_704ILR;
            _cboTipo_704ILR.Enabled = _cboReporta_704ILR.Enabled = _txtDescripcion_704ILR.Enabled = _btnRegistrar_704ILR.Enabled = enEjecucion_704ILR;
            _btnResolver_704ILR.Enabled = enEjecucion_704ILR && Coord_704ILR.Fila_704ILR(_gridIncidencias_704ILR)?.Tag is BE_Incidencia_704ILR inc_704ILR
                && inc_704ILR.Estado_704ILR == EstadoIncidencia_704ILR.ABIERTA;

            string aviso_704ILR;
            if (_evento_704ILR == null || _evento_704ILR.Estado_704ILR != EstadoReserva_704ILR.CONFIRMADA)
                aviso_704ILR = AvisoPlanNoEditable_704ILR();
            else switch (estado_704ILR)
            {
                case EstadoCoordinacion_704ILR.LISTO: aviso_704ILR = T_704ILR("SUP_AVISO_LISTO", "El evento está listo: inicie la ejecución cuando comience."); break;
                case EstadoCoordinacion_704ILR.EN_EJECUCION: aviso_704ILR = T_704ILR("SUP_AVISO_EJECUCION", "Evento en ejecución: registre lo que se salga del plan."); break;
                case EstadoCoordinacion_704ILR.CERRADO: aviso_704ILR = Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.EventoCerrado_704ILR); break;
                default: aviso_704ILR = Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.NoListo_704ILR); break;
            }
            _lblAviso_704ILR.Text = aviso_704ILR;
        }

        // Inicia la ejecucion. Iniciar congela el plan y la reserva y no tiene vuelta
        // atras (RN-13), asi que siempre se pregunta antes; si hoy no es el dia del
        // evento la pregunta lo dice. La capa de negocio vuelve a exigir esa
        // confirmacion: si la fecha cambio entre la lectura y la operacion devuelve
        // FueraDeFecha, se pregunta con la fecha vigente y, si el supervisor acepta,
        // se repite.
        private void Iniciar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("EJECUCION_SUPERVISAR", this, "iniciar la ejecucion de la reserva #" + _reservaId_704ILR)) return;
            bool fueraDeFecha_704ILR = _evento_704ILR != null && _evento_704ILR.FechaEvento_704ILR.Date != DateTime.Today;
            if (!Preguntar_704ILR(fueraDeFecha_704ILR
                    ? PreguntaFueraDeFecha_704ILR()
                    : T_704ILR("SUP_INICIAR_CONF", "¿Iniciar la ejecución del evento? Desde ese momento el plan y la reserva quedan congelados y no se puede volver atrás.")))
                return;
            var r_704ILR = BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(_reservaId_704ILR, fueraDeFecha_704ILR);
            if (r_704ILR == CoordinacionResult_704ILR.FueraDeFecha_704ILR)
            {
                LeerEvento_704ILR();
                if (!Preguntar_704ILR(PreguntaFueraDeFecha_704ILR())) { RefrescarTodo_704ILR(); return; }
                r_704ILR = BLL_Coordinacion_704ILR.IniciarEjecucion_704ILR(_reservaId_704ILR, true);
            }
            RefrescarTodo_704ILR();
            if (r_704ILR != CoordinacionResult_704ILR.Success_704ILR) Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }

        private string PreguntaFueraDeFecha_704ILR() =>
            Tr_704ILR.F_704ILR("SUP_FUERA_FECHA", "El evento está agendado para el {0} y hoy es {1}. Al iniciar la ejecución, el plan y la reserva quedan congelados y no se puede volver atrás. ¿Iniciar igual?",
                _evento_704ILR == null ? string.Empty : Coord_704ILR.Fecha_704ILR(_evento_704ILR.FechaEvento_704ILR),
                Coord_704ILR.Fecha_704ILR(DateTime.Today));

        private void CerrarEvento_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("EJECUCION_SUPERVISAR", this, "cerrar el evento de la reserva #" + _reservaId_704ILR)) return;
            if (!Preguntar_704ILR(T_704ILR("SUP_CERRAR_CONF", "¿Cerrar el evento? Un evento cerrado ya no admite cambios ni incidencias."))) return;
            var r_704ILR = BLL_Coordinacion_704ILR.CerrarEvento_704ILR(_reservaId_704ILR);
            RefrescarTodo_704ILR();
            if (r_704ILR != CoordinacionResult_704ILR.Success_704ILR) Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }

        private void Registrar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("EJECUCION_SUPERVISAR", this, "registrar una incidencia en la reserva #" + _reservaId_704ILR)) return;
            var incidencia_704ILR = new BE_Incidencia_704ILR
            {
                ReservaId_704ILR = _reservaId_704ILR,
                Tipo_704ILR = _cboTipo_704ILR.SelectedItem is TipoIncidencia_704ILR t_704ILR ? t_704ILR : TipoIncidencia_704ILR.OTRO,
                Descripcion_704ILR = _txtDescripcion_704ILR.Text,
                EmpleadoReportaId_704ILR = _cboReporta_704ILR.SelectedItem is ReportaItem_704ILR rep_704ILR ? rep_704ILR.EmpleadoId_704ILR : null
            };
            var r_704ILR = BLL_Incidencia_704ILR.Registrar_704ILR(incidencia_704ILR, out _);
            if (r_704ILR == CoordinacionResult_704ILR.Success_704ILR)
            {
                _txtDescripcion_704ILR.Clear();
                RefrescarTodo_704ILR();
                _txtDescripcion_704ILR.Focus();
                return;
            }
            if (r_704ILR != CoordinacionResult_704ILR.DescripcionInvalida_704ILR) RefrescarTodo_704ILR();
            Aviso_704ILR(r_704ILR == CoordinacionResult_704ILR.DescripcionInvalida_704ILR
                ? T_704ILR("MSG_SUP_DESCRIPCION", "Describa la incidencia.")
                : Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }

        private void Resolver_704ILR()
        {
            if (!(Coord_704ILR.Fila_704ILR(_gridIncidencias_704ILR)?.Tag is BE_Incidencia_704ILR inc_704ILR)) return;
            if (!Permisos_704ILR.Exigir_704ILR("EJECUCION_SUPERVISAR", this, "resolver una incidencia de la reserva #" + _reservaId_704ILR)) return;
            string resolucion_704ILR;
            using (var frm_704ILR = new frmTextoRequerido_704ILR(T_704ILR("SUP_RESOLVER_TITULO", "Resolver incidencia"),
                       T_704ILR("SUP_RESOLVER_LBL", "¿Cómo se resolvió?"),
                       Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.ResolucionObligatoria_704ILR), BLL_Incidencia_704ILR.MaxResolucion_704ILR))
            {
                if (frm_704ILR.ShowDialog(this) != DialogResult.OK) return;
                resolucion_704ILR = frm_704ILR.Texto_704ILR;
            }
            var r_704ILR = BLL_Incidencia_704ILR.Resolver_704ILR(inc_704ILR.Id_704ILR, resolucion_704ILR);
            RefrescarTodo_704ILR();
            if (r_704ILR != CoordinacionResult_704ILR.Success_704ILR) Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }

        // Item del combo "Informo": un integrante del equipo o nadie en particular (la
        // incidencia la observo el propio supervisor).
        private sealed class ReportaItem_704ILR
        {
            public int? EmpleadoId_704ILR { get; }
            private readonly string _nombre_704ILR;
            public ReportaItem_704ILR(int? empleadoId_704ILR, string nombre_704ILR) { EmpleadoId_704ILR = empleadoId_704ILR; _nombre_704ILR = nombre_704ILR; }
            public override string ToString() => EmpleadoId_704ILR.HasValue ? _nombre_704ILR : T_704ILR("SUP_REPORTA_NADIE", "(supervisión)");
        }
    }
}
