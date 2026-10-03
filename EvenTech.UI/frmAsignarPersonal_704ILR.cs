using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Personal del evento (CUN006). El coordinador elige a cada empleado, por
    // especialidad, con su rol y su franja de trabajo; la grilla muestra al equipo con
    // la respuesta de cada uno. Las asignaciones persisten en el acto, como los pagos:
    // cada alta o baja impacta la base y recalcula el estado de coordinacion.
    public class frmAsignarPersonal_704ILR : frmEventoBase_704ILR
    {
        private ComboBox _cboEspecialidad_704ILR, _cboEmpleado_704ILR;
        private TextBox _txtRol_704ILR;
        private DateTimePicker _dtDesde_704ILR, _dtHasta_704ILR;
        private AppButton_704ILR _btnAsignar_704ILR, _btnQuitar_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblAviso_704ILR, _lblResumen_704ILR;
        private List<BE_Empleado_704ILR> _empleados_704ILR = new List<BE_Empleado_704ILR>();
        private List<BE_AsignacionPersonal_704ILR> _asignaciones_704ILR = new List<BE_AsignacionPersonal_704ILR>();
        // Rol que propuso la pantalla al elegir al empleado (su especialidad): si el
        // coordinador no lo cambio, se reemplaza al elegir a otro.
        private string _rolPropuesto_704ILR = string.Empty;

        public frmAsignarPersonal_704ILR(int reservaId_704ILR) : base(reservaId_704ILR)
        {
            BuildUi_704ILR();
            // Los catalogos y el equipo se leen en una sola accion protegida: con la base
            // caida sale un solo aviso.
            Ejecutar_704ILR(() => { CargarCatalogos_704ILR(); RefrescarEquipo_704ILR(); }, "Cargar personal del evento");
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(1020, 560);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // encabezado
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // aviso
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // alta
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grilla
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // pie

            _lblAviso_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.Warning_704ILR, BackColor = Color.Transparent, Margin = new Padding(2, 0, 0, Theme_704ILR.SpaceSm_704ILR), Visible = false, MaximumSize = new Size(960, 0) };

            // --- Fila de alta: especialidad + empleado + rol + franja + asignar ---
            var alta_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };
            _cboEspecialidad_704ILR = Ui_704ILR.Combo_704ILR(); _cboEspecialidad_704ILR.Width = 198; _cboEspecialidad_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _cboEspecialidad_704ILR.FormattingEnabled = true;
            _cboEspecialidad_704ILR.Format += (s_704ILR, e_704ILR) =>
            {
                if (e_704ILR.ListItem is BE_Especialidad_704ILR esp_704ILR)
                    e_704ILR.Value = esp_704ILR.Id_704ILR == 0 ? T_704ILR("ASG_TODAS", "(todas las especialidades)") : Coord_704ILR.Especialidad_704ILR(esp_704ILR.Nombre_704ILR);
            };
            _cboEspecialidad_704ILR.SelectedIndexChanged += (s_704ILR, e_704ILR) => ArmarEmpleados_704ILR();
            // El combo de empleados abre sin nadie elegido (la pantalla no elige por el
            // coordinador): el rotulo dice que es.
            var lblEmpleado_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("EMP_COL_EMPLEADO", "Empleado")); lblEmpleado_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, 0);
            _cboEmpleado_704ILR = Ui_704ILR.Combo_704ILR(); _cboEmpleado_704ILR.Width = 186; _cboEmpleado_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _cboEmpleado_704ILR.SelectedIndexChanged += (s_704ILR, e_704ILR) => { ProponerRol_704ILR(); ActualizarAcciones_704ILR(); };
            // El rol no lleva rotulo en la fila: el texto de ejemplo le da nombre en
            // pantalla. MaxLength = ancho de AsignacionesPersonal.RolAsignado.
            _txtRol_704ILR = Ui_704ILR.Input_704ILR(); _txtRol_704ILR.Width = 146; _txtRol_704ILR.MaxLength = 60; _txtRol_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _txtRol_704ILR.PlaceholderText = T_704ILR("ASG_ROL", "Rol en el evento");
            var lblDe_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("ASG_DE", "de")); lblDe_704ILR.Margin = new Padding(0, 8, Theme_704ILR.SpaceXs_704ILR, 0);
            _dtDesde_704ILR = Ui_704ILR.TimePicker_704ILR(20, 0); _dtDesde_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, 0);
            var lblA_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("ASG_A", "a")); lblA_704ILR.Margin = new Padding(0, 8, Theme_704ILR.SpaceXs_704ILR, 0);
            _dtHasta_704ILR = Ui_704ILR.TimePicker_704ILR(4, 0); _dtHasta_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _btnAsignar_704ILR = Boton_704ILR(T_704ILR("ASG_ASIGNAR", "Asignar"), Theme_704ILR.IcoAdd_704ILR, 120);
            _btnAsignar_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Asignar_704ILR, "Asignar personal");
            alta_704ILR.Controls.Add(_cboEspecialidad_704ILR); alta_704ILR.Controls.Add(lblEmpleado_704ILR); alta_704ILR.Controls.Add(_cboEmpleado_704ILR); alta_704ILR.Controls.Add(_txtRol_704ILR);
            alta_704ILR.Controls.Add(lblDe_704ILR); alta_704ILR.Controls.Add(_dtDesde_704ILR); alta_704ILR.Controls.Add(lblA_704ILR); alta_704ILR.Controls.Add(_dtHasta_704ILR);
            alta_704ILR.Controls.Add(_btnAsignar_704ILR);
            CentrarFila_704ILR(alta_704ILR);
            _cboEspecialidad_704ILR.AccessibleName = T_704ILR("EMP_ESPECIALIDAD", "Especialidad");
            _cboEmpleado_704ILR.AccessibleName = T_704ILR("EMP_COL_EMPLEADO", "Empleado");
            _txtRol_704ILR.AccessibleName = T_704ILR("ASG_ROL", "Rol en el evento");

            // --- Grilla del equipo ---
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEmpleado", HeaderText = T_704ILR("EMP_COL_EMPLEADO", "Empleado"), FillWeight = 70 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEspecialidad", HeaderText = T_704ILR("EMP_ESPECIALIDAD", "Especialidad"), FillWeight = 55 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRol", HeaderText = T_704ILR("ASG_COL_ROL", "Rol"), FillWeight = 55 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFranja", HeaderText = T_704ILR("ASG_COL_FRANJA", "Franja"), FillWeight = 42 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstado", HeaderText = T_704ILR("COL_ESTADO", "Estado"), FillWeight = 40 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRespuesta", HeaderText = T_704ILR("ASG_COL_RESPUESTA", "Respondió"), FillWeight = 52 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMotivo", HeaderText = T_704ILR("ASG_COL_MOTIVO", "Motivo del rechazo"), FillWeight = 85 });
            Coord_704ILR.SinOrden_704ILR(_grid_704ILR);
            UiGrid_704ILR.Multilinea_704ILR(_grid_704ILR, "cEmpleado", "cEspecialidad", "cRol", "cMotivo");
            UiGrid_704ILR.AlContenido_704ILR(_grid_704ILR, "cFranja", "cEstado", "cRespuesta");
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
            _btnQuitar_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Quitar_704ILR, "Quitar asignacion");
            var btnCerrar_704ILR = Boton_704ILR(T_704ILR("BTN_CERRAR", "Cerrar"), Theme_704ILR.IcoClose_704ILR, 120);
            btnCerrar_704ILR.Anchor = AnchorStyles.Right;
            btnCerrar_704ILR.Click += (s_704ILR, e_704ILR) => Close();
            footer_704ILR.Controls.Add(_lblResumen_704ILR, 0, 0);
            footer_704ILR.Controls.Add(_btnQuitar_704ILR, 1, 0);
            footer_704ILR.Controls.Add(btnCerrar_704ILR, 2, 0);

            root_704ILR.Controls.Add(ArmarEncabezado_704ILR(), 0, 0);
            root_704ILR.Controls.Add(_lblAviso_704ILR, 0, 1);
            root_704ILR.Controls.Add(alta_704ILR, 0, 2);
            root_704ILR.Controls.Add(card_704ILR, 0, 3);
            root_704ILR.Controls.Add(footer_704ILR, 0, 4);

            Controls.Add(root_704ILR);
            Controls.Add(ArmarTitulo_704ILR(T_704ILR("ASG_TITULO", "Personal del evento")));
            // Enter ejecuta la accion de la fila de alta, que es la del dialogo; Escape cierra.
            AcceptButton = _btnAsignar_704ILR;
            CancelButton = btnCerrar_704ILR;
        }

        // Especialidades y empleados activos para la fila de alta.
        private void CargarCatalogos_704ILR()
        {
            _empleados_704ILR = BLL_Empleado_704ILR.GetActivos_704ILR();
            _cboEspecialidad_704ILR.Items.Clear();
            _cboEspecialidad_704ILR.Items.Add(new BE_Especialidad_704ILR { Id_704ILR = 0, Nombre_704ILR = string.Empty });
            foreach (var esp_704ILR in BLL_Empleado_704ILR.GetEspecialidades_704ILR()) _cboEspecialidad_704ILR.Items.Add(esp_704ILR);
            _cboEspecialidad_704ILR.SelectedIndex = 0;
        }

        // Empleados que se pueden asignar: los activos de la especialidad elegida que
        // todavia no estan en el equipo. Quien rechazo el turno sigue en la lista: se le
        // puede volver a ofrecer. El combo conserva al elegido si sigue en la lista y, si
        // no, queda sin nadie: la pantalla nunca elige a un empleado por el coordinador
        // (despues de una alta, un segundo clic ya encolado asignaba al siguiente).
        private void ArmarEmpleados_704ILR()
        {
            int especialidad_704ILR = _cboEspecialidad_704ILR.SelectedItem is BE_Especialidad_704ILR esp_704ILR ? esp_704ILR.Id_704ILR : 0;
            int elegido_704ILR = _cboEmpleado_704ILR.SelectedItem is BE_Empleado_704ILR emp_704ILR ? emp_704ILR.Id_704ILR : 0;
            var enEquipo_704ILR = new HashSet<int>(_asignaciones_704ILR
                .Where(a_704ILR => a_704ILR.Estado_704ILR != EstadoAsignacion_704ILR.RECHAZADA)
                .Select(a_704ILR => a_704ILR.EmpleadoId_704ILR));

            _cboEmpleado_704ILR.Items.Clear();
            foreach (var e_704ILR in _empleados_704ILR)
                if ((especialidad_704ILR == 0 || e_704ILR.EspecialidadId_704ILR == especialidad_704ILR) && !enEquipo_704ILR.Contains(e_704ILR.Id_704ILR))
                    _cboEmpleado_704ILR.Items.Add(e_704ILR);

            int indice_704ILR = -1;
            for (int i_704ILR = 0; i_704ILR < _cboEmpleado_704ILR.Items.Count; i_704ILR++)
                if (((BE_Empleado_704ILR)_cboEmpleado_704ILR.Items[i_704ILR]).Id_704ILR == elegido_704ILR) { indice_704ILR = i_704ILR; break; }
            _cboEmpleado_704ILR.SelectedIndex = indice_704ILR;
            Coord_704ILR.AjustarDesplegable_704ILR(_cboEmpleado_704ILR);
            ProponerRol_704ILR();
            ActualizarAcciones_704ILR();
        }

        // Propone como rol la especialidad del empleado elegido, salvo que el
        // coordinador ya haya escrito otro.
        private void ProponerRol_704ILR()
        {
            if (!(_cboEmpleado_704ILR.SelectedItem is BE_Empleado_704ILR emp_704ILR)) return;
            if (_txtRol_704ILR.Text.Trim().Length != 0 && _txtRol_704ILR.Text != _rolPropuesto_704ILR) return;
            _rolPropuesto_704ILR = Coord_704ILR.Especialidad_704ILR(emp_704ILR.EspecialidadNombre_704ILR);
            if (_rolPropuesto_704ILR.Length > _txtRol_704ILR.MaxLength) _rolPropuesto_704ILR = _rolPropuesto_704ILR.Substring(0, _txtRol_704ILR.MaxLength);
            _txtRol_704ILR.Text = _rolPropuesto_704ILR;
        }

        private void RefrescarEquipo_704ILR()
        {
            LeerEvento_704ILR();
            _asignaciones_704ILR = BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(_reservaId_704ILR);
            _grid_704ILR.Rows.Clear();
            foreach (var a_704ILR in _asignaciones_704ILR)
            {
                int i_704ILR = _grid_704ILR.Rows.Add(a_704ILR.EmpleadoNombre_704ILR, Coord_704ILR.Especialidad_704ILR(a_704ILR.EspecialidadNombre_704ILR),
                    a_704ILR.RolAsignado_704ILR, Coord_704ILR.Franja_704ILR(a_704ILR.HoraInicio_704ILR, a_704ILR.HoraFin_704ILR),
                    Coord_704ILR.Asignacion_704ILR(a_704ILR.Estado_704ILR),
                    a_704ILR.FechaConfirmacion_704ILR.HasValue ? Coord_704ILR.FechaHora_704ILR(a_704ILR.FechaConfirmacion_704ILR.Value) : string.Empty,
                    a_704ILR.MotivoRechazo_704ILR ?? string.Empty);
                _grid_704ILR.Rows[i_704ILR].Tag = a_704ILR;
                _grid_704ILR.Rows[i_704ILR].Cells["cEstado"].Style.ForeColor = Coord_704ILR.ColorAsignacion_704ILR(a_704ILR.Estado_704ILR);
            }
            _lblResumen_704ILR.Text = Tr_704ILR.F_704ILR("ASG_RESUMEN", "Asignados: {0}    Confirmados: {1}    Pendientes: {2}    Rechazados: {3}",
                _asignaciones_704ILR.Count,
                _asignaciones_704ILR.Count(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.CONFIRMADA),
                _asignaciones_704ILR.Count(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.PENDIENTE),
                _asignaciones_704ILR.Count(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.RECHAZADA));
            ArmarEmpleados_704ILR();
        }

        // Primera capa: la fila de alta y Quitar se ofrecen solo con el permiso y con el
        // plan todavia editable (el evento no empezo). La capa de negocio lo vuelve a exigir.
        protected override void ActualizarAcciones_704ILR()
        {
            if (_btnQuitar_704ILR == null) return;
            bool editable_704ILR = PlanEditable_704ILR && Permisos_704ILR.Tiene_704ILR("PERSONAL_ASIGNAR");
            _cboEspecialidad_704ILR.Enabled = editable_704ILR;
            _cboEmpleado_704ILR.Enabled = editable_704ILR;
            _txtRol_704ILR.Enabled = editable_704ILR;
            _dtDesde_704ILR.Enabled = editable_704ILR;
            _dtHasta_704ILR.Enabled = editable_704ILR;
            _btnAsignar_704ILR.Enabled = editable_704ILR && _cboEmpleado_704ILR.SelectedItem is BE_Empleado_704ILR;
            _btnQuitar_704ILR.Enabled = editable_704ILR && Coord_704ILR.Fila_704ILR(_grid_704ILR)?.Tag is BE_AsignacionPersonal_704ILR;

            string aviso_704ILR = AvisoPlanNoEditable_704ILR();
            _lblAviso_704ILR.Text = aviso_704ILR ?? string.Empty;
            _lblAviso_704ILR.Visible = aviso_704ILR != null;
        }

        private void Asignar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("PERSONAL_ASIGNAR", this, "asignar personal a la reserva #" + _reservaId_704ILR)) return;
            if (!(_cboEmpleado_704ILR.SelectedItem is BE_Empleado_704ILR emp_704ILR))
            {
                Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.EmpleadoInvalido_704ILR));
                return;
            }
            // RN-10: el turno lo responde el propio empleado desde su cuenta. A quien no
            // tiene una vinculada se lo puede asignar igual (la cuenta se vincula despues
            // desde Empleados), pero el coordinador tiene que saber que hasta entonces el
            // turno queda pendiente y el evento no llega a estar listo.
            if (!emp_704ILR.UserId_704ILR.HasValue && !Preguntar_704ILR(Tr_704ILR.F_704ILR("ASG_SIN_CUENTA_CONF",
                    "{0} no tiene una cuenta vinculada: no va a poder confirmar el turno hasta que se la vinculen desde Empleados. ¿Asignar igual?",
                    emp_704ILR.NombreCompleto_704ILR)))
                return;
            var r_704ILR = BLL_AsignacionPersonal_704ILR.Asignar_704ILR(_reservaId_704ILR, emp_704ILR.Id_704ILR, _txtRol_704ILR.Text,
                _dtDesde_704ILR.Value.TimeOfDay, _dtHasta_704ILR.Value.TimeOfDay, out _, out BE_AsignacionPersonal_704ILR conflicto_704ILR);
            if (r_704ILR == CoordinacionResult_704ILR.Success_704ILR)
            {
                // El rol propuesto se vacia para que el proximo empleado traiga el suyo.
                _txtRol_704ILR.Text = _rolPropuesto_704ILR = string.Empty;
                RefrescarEquipo_704ILR();
                return;
            }
            // Un rechazo puede venir de un cambio hecho desde otra sesion: se relee antes
            // del aviso para que coincida con lo que muestra la grilla.
            if (r_704ILR != CoordinacionResult_704ILR.RolInvalido_704ILR && r_704ILR != CoordinacionResult_704ILR.FranjaInvalida_704ILR)
            {
                // El empleado pudo darse de baja desde otra estacion: se relee el personal.
                if (r_704ILR == CoordinacionResult_704ILR.EmpleadoInvalido_704ILR) _empleados_704ILR = BLL_Empleado_704ILR.GetActivos_704ILR();
                RefrescarEquipo_704ILR();
            }
            Aviso_704ILR(r_704ILR == CoordinacionResult_704ILR.Superposicion_704ILR
                ? Coord_704ILR.Superposicion_704ILR(conflicto_704ILR)
                // Al volver a ofrecer un turno rechazado, este rechazo es por la franja nueva.
                : r_704ILR == CoordinacionResult_704ILR.TieneCarga_704ILR
                    ? T_704ILR("MSG_ASG_FRANJA_TAREAS", "La franja nueva deja afuera tareas que el empleado ya tiene en este evento: ajuste la franja o quite antes esas tareas.")
                    : Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }

        private void Quitar_704ILR()
        {
            if (!(Coord_704ILR.Fila_704ILR(_grid_704ILR)?.Tag is BE_AsignacionPersonal_704ILR a_704ILR)) return;
            if (!Permisos_704ILR.Exigir_704ILR("PERSONAL_ASIGNAR", this, "quitar personal de la reserva #" + _reservaId_704ILR)) return;
            if (!Preguntar_704ILR(Tr_704ILR.F_704ILR("ASG_QUITAR_CONF", "¿Quitar a {0} del equipo de este evento?", a_704ILR.EmpleadoNombre_704ILR))) return;

            var r_704ILR = BLL_AsignacionPersonal_704ILR.Quitar_704ILR(a_704ILR.Id_704ILR);
            RefrescarEquipo_704ILR();
            if (r_704ILR != CoordinacionResult_704ILR.Success_704ILR) Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }
    }
}
