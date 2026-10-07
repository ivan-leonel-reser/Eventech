using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Tareas del evento (CUN009). Sobre el cronograma ya generado, el coordinador le
    // asigna tareas especificas a cada integrante confirmado del equipo, dentro de su
    // turno. Las tareas persisten en el acto, como las asignaciones de personal.
    public class frmTareas_704ILR : frmEventoBase_704ILR
    {
        private ComboBox _cboEmpleado_704ILR, _cboPrioridad_704ILR;
        private TextBox _txtDescripcion_704ILR, _txtRecursos_704ILR;
        private DateTimePicker _dtDesde_704ILR, _dtHasta_704ILR;
        private AppButton_704ILR _btnAsignar_704ILR, _btnQuitar_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblAviso_704ILR, _lblResumen_704ILR;
        private bool _tieneCronograma_704ILR;
        // Tareas del evento segun la ultima lectura: la grilla las muestra y la franja
        // propuesta las esquiva.
        private List<BE_Tarea_704ILR> _tareas_704ILR = new List<BE_Tarea_704ILR>();
        // El combo de empleados se esta rearmando: no es el usuario el que elige.
        private bool _rearmando_704ILR;

        public frmTareas_704ILR(int reservaId_704ILR) : base(reservaId_704ILR)
        {
            BuildUi_704ILR();
            Refrescar_704ILR();
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(960, 580);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // encabezado
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // aviso
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // alta 1
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // alta 2
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grilla
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // pie

            _lblAviso_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.Warning_704ILR, BackColor = Color.Transparent, Margin = new Padding(2, 0, 0, Theme_704ILR.SpaceSm_704ILR), Visible = false, MaximumSize = new Size(900, 0) };

            // --- Alta, primera fila: a quien y que ---
            var alta1_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR) };
            _cboEmpleado_704ILR = Ui_704ILR.Combo_704ILR(); _cboEmpleado_704ILR.Width = 300; _cboEmpleado_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _cboEmpleado_704ILR.SelectedIndexChanged += (s_704ILR, e_704ILR) => { if (!_rearmando_704ILR) ProponerFranja_704ILR(); };
            // La descripcion y los recursos no llevan rotulo en la fila: el texto de
            // ejemplo les da nombre. MaxLength = ancho de las columnas de dbo.Tareas.
            _txtDescripcion_704ILR = Ui_704ILR.Input_704ILR(); _txtDescripcion_704ILR.Width = 600; _txtDescripcion_704ILR.MaxLength = BLL_Tarea_704ILR.MaxDescripcion_704ILR;
            _txtDescripcion_704ILR.PlaceholderText = T_704ILR("TAR_DESCRIPCION", "Tarea a realizar");
            alta1_704ILR.Controls.Add(_cboEmpleado_704ILR); alta1_704ILR.Controls.Add(_txtDescripcion_704ILR);
            CentrarFila_704ILR(alta1_704ILR);
            _cboEmpleado_704ILR.AccessibleName = T_704ILR("EMP_COL_EMPLEADO", "Empleado");
            _txtDescripcion_704ILR.AccessibleName = T_704ILR("TAR_COL_TAREA", "Tarea");

            // --- Alta, segunda fila: cuando, con que prioridad y con que recursos ---
            var alta2_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };
            var lblDe_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("ASG_DE", "de")); lblDe_704ILR.Margin = new Padding(0, 8, Theme_704ILR.SpaceXs_704ILR, 0);
            _dtDesde_704ILR = Ui_704ILR.TimePicker_704ILR(20, 0); _dtDesde_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, 0);
            var lblA_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("ASG_A", "a")); lblA_704ILR.Margin = new Padding(0, 8, Theme_704ILR.SpaceXs_704ILR, 0);
            _dtHasta_704ILR = Ui_704ILR.TimePicker_704ILR(21, 0); _dtHasta_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            var lblPrio_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("TAR_COL_PRIORIDAD", "Prioridad")); lblPrio_704ILR.Margin = new Padding(0, 8, Theme_704ILR.SpaceXs_704ILR, 0);
            _cboPrioridad_704ILR = Ui_704ILR.Combo_704ILR(); _cboPrioridad_704ILR.Width = 110; _cboPrioridad_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _cboPrioridad_704ILR.FormattingEnabled = true;
            _cboPrioridad_704ILR.Format += (s_704ILR, e_704ILR) =>
            {
                if (e_704ILR.ListItem is PrioridadTarea_704ILR p_704ILR) e_704ILR.Value = Coord_704ILR.Prioridad_704ILR(p_704ILR);
            };
            foreach (PrioridadTarea_704ILR p_704ILR in new[] { PrioridadTarea_704ILR.ALTA, PrioridadTarea_704ILR.MEDIA, PrioridadTarea_704ILR.BAJA }) _cboPrioridad_704ILR.Items.Add(p_704ILR);
            _cboPrioridad_704ILR.SelectedIndex = 1;
            _txtRecursos_704ILR = Ui_704ILR.Input_704ILR(); _txtRecursos_704ILR.Width = 330; _txtRecursos_704ILR.MaxLength = BLL_Tarea_704ILR.MaxRecursos_704ILR;
            _txtRecursos_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _txtRecursos_704ILR.PlaceholderText = T_704ILR("TAR_RECURSOS", "Recursos necesarios (opcional)");
            _btnAsignar_704ILR = Boton_704ILR(T_704ILR("ASG_ASIGNAR", "Asignar"), Theme_704ILR.IcoAdd_704ILR, 120);
            _btnAsignar_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Asignar_704ILR, "Asignar tarea");
            alta2_704ILR.Controls.Add(lblDe_704ILR); alta2_704ILR.Controls.Add(_dtDesde_704ILR); alta2_704ILR.Controls.Add(lblA_704ILR); alta2_704ILR.Controls.Add(_dtHasta_704ILR);
            alta2_704ILR.Controls.Add(lblPrio_704ILR); alta2_704ILR.Controls.Add(_cboPrioridad_704ILR); alta2_704ILR.Controls.Add(_txtRecursos_704ILR); alta2_704ILR.Controls.Add(_btnAsignar_704ILR);
            CentrarFila_704ILR(alta2_704ILR);
            _txtRecursos_704ILR.AccessibleName = T_704ILR("TAR_COL_RECURSOS", "Recursos");

            // --- Grilla de tareas ---
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEmpleado", HeaderText = T_704ILR("EMP_COL_EMPLEADO", "Empleado"), FillWeight = 65 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTarea", HeaderText = T_704ILR("TAR_COL_TAREA", "Tarea"), FillWeight = 110 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFranja", HeaderText = T_704ILR("ASG_COL_FRANJA", "Franja"), FillWeight = 40 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPrioridad", HeaderText = T_704ILR("TAR_COL_PRIORIDAD", "Prioridad"), FillWeight = 34 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRecursos", HeaderText = T_704ILR("TAR_COL_RECURSOS", "Recursos"), FillWeight = 80 });
            Coord_704ILR.SinOrden_704ILR(_grid_704ILR);
            UiGrid_704ILR.Multilinea_704ILR(_grid_704ILR, "cEmpleado", "cTarea", "cRecursos");
            UiGrid_704ILR.AlContenido_704ILR(_grid_704ILR, "cFranja", "cPrioridad");
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_grid_704ILR);
            _grid_704ILR.SelectionChanged += (s_704ILR, e_704ILR) => ActualizarAcciones_704ILR();
            card_704ILR.Controls.Add(_grid_704ILR);

            // --- Pie: resumen + quitar + cerrar ---
            var footer_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true, BackColor = Color.Transparent };
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _lblResumen_704ILR = new Label { Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 0, 0), BackColor = Color.Transparent };
            _btnQuitar_704ILR = Boton_704ILR(T_704ILR("BTN_QUITAR", "Quitar"), Theme_704ILR.IcoClear_704ILR, 110, secundario_704ILR: true);
            _btnQuitar_704ILR.Anchor = AnchorStyles.Right; _btnQuitar_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _btnQuitar_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Quitar_704ILR, "Quitar tarea");
            var btnCerrar_704ILR = Boton_704ILR(T_704ILR("BTN_CERRAR", "Cerrar"), Theme_704ILR.IcoClose_704ILR, 120);
            btnCerrar_704ILR.Anchor = AnchorStyles.Right;
            btnCerrar_704ILR.Click += (s_704ILR, e_704ILR) => Close();
            footer_704ILR.Controls.Add(_lblResumen_704ILR, 0, 0);
            footer_704ILR.Controls.Add(_btnQuitar_704ILR, 1, 0);
            footer_704ILR.Controls.Add(btnCerrar_704ILR, 2, 0);

            root_704ILR.Controls.Add(ArmarEncabezado_704ILR(), 0, 0);
            root_704ILR.Controls.Add(_lblAviso_704ILR, 0, 1);
            root_704ILR.Controls.Add(alta1_704ILR, 0, 2);
            root_704ILR.Controls.Add(alta2_704ILR, 0, 3);
            root_704ILR.Controls.Add(card_704ILR, 0, 4);
            root_704ILR.Controls.Add(footer_704ILR, 0, 5);

            Controls.Add(root_704ILR);
            Controls.Add(ArmarTitulo_704ILR(T_704ILR("TAR_TITULO", "Tareas del evento")));
            // Enter asigna la tarea tipeada; Escape cierra.
            AcceptButton = _btnAsignar_704ILR;
            CancelButton = btnCerrar_704ILR;
        }

        private void Refrescar_704ILR() => Ejecutar_704ILR(RefrescarTareas_704ILR, "Cargar tareas");

        private void RefrescarTareas_704ILR()
        {
            LeerEvento_704ILR();
            _tieneCronograma_704ILR = _evento_704ILR != null && _evento_704ILR.TieneCronograma_704ILR;

            // El combo ofrece al personal confirmado, con su turno a la vista: la tarea
            // tiene que caer adentro (RN-12). Se conserva al elegido, y con el la franja
            // que el coordinador ya habia tipeado: la franja solo se vuelve a proponer
            // cuando cambia el empleado.
            int elegido_704ILR = _cboEmpleado_704ILR.SelectedItem is TurnoItem_704ILR t_704ILR ? t_704ILR.Turno_704ILR.EmpleadoId_704ILR : 0;
            var confirmados_704ILR = BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(_reservaId_704ILR)
                .Where(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.CONFIRMADA).ToList();
            _tareas_704ILR = BLL_Tarea_704ILR.GetByReserva_704ILR(_reservaId_704ILR);
            bool mismoEmpleado_704ILR;
            _rearmando_704ILR = true;
            try
            {
                _cboEmpleado_704ILR.Items.Clear();
                foreach (var a_704ILR in confirmados_704ILR) _cboEmpleado_704ILR.Items.Add(new TurnoItem_704ILR(a_704ILR));
                int indice_704ILR = -1;
                for (int i_704ILR = 0; i_704ILR < _cboEmpleado_704ILR.Items.Count; i_704ILR++)
                    if (((TurnoItem_704ILR)_cboEmpleado_704ILR.Items[i_704ILR]).Turno_704ILR.EmpleadoId_704ILR == elegido_704ILR) { indice_704ILR = i_704ILR; break; }
                mismoEmpleado_704ILR = indice_704ILR >= 0;
                _cboEmpleado_704ILR.SelectedIndex = mismoEmpleado_704ILR ? indice_704ILR : (_cboEmpleado_704ILR.Items.Count > 0 ? 0 : -1);
                Coord_704ILR.AjustarDesplegable_704ILR(_cboEmpleado_704ILR);
            }
            finally { _rearmando_704ILR = false; }
            if (!mismoEmpleado_704ILR) ProponerFranja_704ILR();

            _grid_704ILR.Rows.Clear();
            foreach (var tarea_704ILR in _tareas_704ILR)
            {
                int i_704ILR = _grid_704ILR.Rows.Add(tarea_704ILR.EmpleadoNombre_704ILR, tarea_704ILR.Descripcion_704ILR,
                    Coord_704ILR.Franja_704ILR(tarea_704ILR.HoraInicio_704ILR, tarea_704ILR.HoraFin_704ILR),
                    Coord_704ILR.Prioridad_704ILR(tarea_704ILR.Prioridad_704ILR), tarea_704ILR.Recursos_704ILR ?? string.Empty);
                _grid_704ILR.Rows[i_704ILR].Tag = tarea_704ILR;
                if (tarea_704ILR.Prioridad_704ILR == PrioridadTarea_704ILR.ALTA)
                    _grid_704ILR.Rows[i_704ILR].Cells["cPrioridad"].Style.ForeColor = Theme_704ILR.Error_704ILR;
            }
            _lblResumen_704ILR.Text = Tr_704ILR.F_704ILR("TAR_RESUMEN", "Tareas asignadas: {0}", _tareas_704ILR.Count);
            ActualizarAcciones_704ILR();
        }

        // Al elegir a un empleado se propone la primera hora libre de su turno: proponer
        // siempre el inicio ofrecia una franja que la asignacion rechazaba cuando el
        // empleado ya tenia una tarea ahi (RN-12).
        private void ProponerFranja_704ILR()
        {
            if (!(_cboEmpleado_704ILR.SelectedItem is TurnoItem_704ILR t_704ILR)) return;
            BLL_Tarea_704ILR.PrimeraFranjaLibre_704ILR(t_704ILR.Turno_704ILR, _tareas_704ILR, out TimeSpan desde_704ILR, out TimeSpan hasta_704ILR);
            DateTime dia_704ILR = _dtDesde_704ILR.Value.Date;
            _dtDesde_704ILR.Value = dia_704ILR + desde_704ILR;
            _dtHasta_704ILR.Value = dia_704ILR + hasta_704ILR;
        }

        // Primera capa: el alta y Quitar se ofrecen con el permiso, con el plan todavia
        // editable y con el cronograma generado. El aviso dice por que no se puede.
        protected override void ActualizarAcciones_704ILR()
        {
            if (_btnAsignar_704ILR == null) return;
            bool editable_704ILR = PlanEditable_704ILR && Permisos_704ILR.Tiene_704ILR("TAREAS_ASIGNAR");
            bool alta_704ILR = editable_704ILR && _tieneCronograma_704ILR && _cboEmpleado_704ILR.Items.Count > 0;
            _cboEmpleado_704ILR.Enabled = _txtDescripcion_704ILR.Enabled = _dtDesde_704ILR.Enabled = _dtHasta_704ILR.Enabled =
                _cboPrioridad_704ILR.Enabled = _txtRecursos_704ILR.Enabled = _btnAsignar_704ILR.Enabled = alta_704ILR;
            _btnQuitar_704ILR.Enabled = editable_704ILR && Coord_704ILR.Fila_704ILR(_grid_704ILR)?.Tag is BE_Tarea_704ILR;

            string aviso_704ILR = AvisoPlanNoEditable_704ILR()
                ?? (!_tieneCronograma_704ILR ? Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.SinCronograma_704ILR)
                    : _cboEmpleado_704ILR.Items.Count == 0 ? T_704ILR("MSG_TAR_SIN_EQUIPO", "No hay personal confirmado al que asignarle tareas.")
                    : null);
            _lblAviso_704ILR.Text = aviso_704ILR ?? string.Empty;
            _lblAviso_704ILR.Visible = aviso_704ILR != null;
        }

        private void Asignar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("TAREAS_ASIGNAR", this, "asignar una tarea en la reserva #" + _reservaId_704ILR)) return;
            if (!(_cboEmpleado_704ILR.SelectedItem is TurnoItem_704ILR t_704ILR))
            {
                Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.ResponsableInvalido_704ILR));
                return;
            }
            var tarea_704ILR = new BE_Tarea_704ILR
            {
                ReservaId_704ILR = _reservaId_704ILR,
                EmpleadoId_704ILR = t_704ILR.Turno_704ILR.EmpleadoId_704ILR,
                Descripcion_704ILR = _txtDescripcion_704ILR.Text,
                HoraInicio_704ILR = _dtDesde_704ILR.Value.TimeOfDay,
                HoraFin_704ILR = _dtHasta_704ILR.Value.TimeOfDay,
                Prioridad_704ILR = _cboPrioridad_704ILR.SelectedItem is PrioridadTarea_704ILR p_704ILR ? p_704ILR : PrioridadTarea_704ILR.MEDIA,
                Recursos_704ILR = _txtRecursos_704ILR.Text
            };
            var r_704ILR = BLL_Tarea_704ILR.Asignar_704ILR(tarea_704ILR, out _);
            if (r_704ILR == CoordinacionResult_704ILR.Success_704ILR)
            {
                _txtDescripcion_704ILR.Clear();
                _txtRecursos_704ILR.Clear();
                // Para la tarea siguiente se vuelve a proponer la primera hora libre del
                // turno, con las tareas ya releidas: "donde termino la anterior, con la
                // misma duracion" proponia franjas fuera del turno o pisadas con otra tarea,
                // que la asignacion rechazaba (RN-12).
                RefrescarTareas_704ILR();
                ProponerFranja_704ILR();
                _txtDescripcion_704ILR.Focus();
                return;
            }
            // Los errores de lo tipeado conservan la fila; los demas pueden venir de un
            // cambio hecho desde otra sesion y se relee antes del aviso.
            if (r_704ILR != CoordinacionResult_704ILR.DescripcionInvalida_704ILR && r_704ILR != CoordinacionResult_704ILR.FranjaInvalida_704ILR
                && r_704ILR != CoordinacionResult_704ILR.FueraDeFranja_704ILR && r_704ILR != CoordinacionResult_704ILR.TareaSuperpuesta_704ILR)
                RefrescarTareas_704ILR();
            Aviso_704ILR(r_704ILR == CoordinacionResult_704ILR.DescripcionInvalida_704ILR
                ? T_704ILR("MSG_TAR_DESCRIPCION", "Ingrese la tarea a realizar.")
                : Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }

        private void Quitar_704ILR()
        {
            if (!(Coord_704ILR.Fila_704ILR(_grid_704ILR)?.Tag is BE_Tarea_704ILR tarea_704ILR)) return;
            if (!Permisos_704ILR.Exigir_704ILR("TAREAS_ASIGNAR", this, "quitar una tarea de la reserva #" + _reservaId_704ILR)) return;
            if (!Preguntar_704ILR(Tr_704ILR.F_704ILR("TAR_QUITAR_CONF", "¿Quitar la tarea de {0}?", tarea_704ILR.EmpleadoNombre_704ILR))) return;

            var r_704ILR = BLL_Tarea_704ILR.Quitar_704ILR(tarea_704ILR.Id_704ILR);
            RefrescarTareas_704ILR();
            if (r_704ILR != CoordinacionResult_704ILR.Success_704ILR) Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }

        // Item del combo de empleados: un integrante confirmado con su turno.
        private sealed class TurnoItem_704ILR
        {
            public BE_AsignacionPersonal_704ILR Turno_704ILR { get; }
            public TurnoItem_704ILR(BE_AsignacionPersonal_704ILR turno_704ILR) { Turno_704ILR = turno_704ILR; }
            public override string ToString() =>
                Turno_704ILR.EmpleadoNombre_704ILR + "  (" + Coord_704ILR.Franja_704ILR(Turno_704ILR.HoraInicio_704ILR, Turno_704ILR.HoraFin_704ILR) + ")";
        }
    }
}
