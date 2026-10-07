using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Cronograma del evento (CUN008). El coordinador arma la lista de actividades de la
    // jornada (hora, descripcion, responsable y duracion estimada), las ordena y la
    // guarda entera: el cronograma se genera o se reemplaza en una sola operacion.
    // Los responsables se eligen entre el personal confirmado del evento.
    public class frmCronograma_704ILR : frmEventoBase_704ILR
    {
        private DateTimePicker _dtHora_704ILR;
        private TextBox _txtDescripcion_704ILR;
        private ComboBox _cboResponsable_704ILR;
        private NumericUpDown _numDuracion_704ILR;
        private AppButton_704ILR _btnAgregar_704ILR, _btnSubir_704ILR, _btnBajar_704ILR, _btnQuitar_704ILR, _btnEliminar_704ILR, _btnGuardar_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblAviso_704ILR;

        // Lista de trabajo (lo que muestra la grilla) y lo que hay guardado: el dialogo
        // tiene cambios sin guardar cuando difieren.
        private readonly List<BE_CronogramaActividad_704ILR> _actividades_704ILR = new List<BE_CronogramaActividad_704ILR>();
        private List<BE_CronogramaActividad_704ILR> _guardadas_704ILR = new List<BE_CronogramaActividad_704ILR>();
        private bool _existe_704ILR;
        // Avance del equipo (RN-11): el cronograma se genera con personal confirmado y
        // ninguna respuesta pendiente; uno ya generado se sigue modificando con
        // respuestas pendientes, siempre con responsables confirmados.
        private bool _hayConfirmados_704ILR, _hayPendientes_704ILR;
        private bool EquipoAdmiteEdicion_704ILR => _hayConfirmados_704ILR && (_existe_704ILR || !_hayPendientes_704ILR);
        // El usuario ya acepto descartar los cambios al cerrar.
        private bool _descarteAceptado_704ILR;

        public frmCronograma_704ILR(int reservaId_704ILR) : base(reservaId_704ILR)
        {
            BuildUi_704ILR();
            Refrescar_704ILR();
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(940, 580);
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

            _lblAviso_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.Warning_704ILR, BackColor = Color.Transparent, Margin = new Padding(2, 0, 0, Theme_704ILR.SpaceSm_704ILR), Visible = false, MaximumSize = new Size(880, 0) };

            // --- Fila de alta: hora + actividad + responsable + duracion + agregar ---
            var alta_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };
            _dtHora_704ILR = Ui_704ILR.TimePicker_704ILR(20, 0); _dtHora_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            // La actividad no lleva rotulo en la fila: el texto de ejemplo le da nombre.
            // MaxLength = ancho de CronogramaActividades.Descripcion.
            _txtDescripcion_704ILR = Ui_704ILR.Input_704ILR(); _txtDescripcion_704ILR.Width = 262; _txtDescripcion_704ILR.MaxLength = BLL_Cronograma_704ILR.MaxDescripcion_704ILR;
            _txtDescripcion_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _txtDescripcion_704ILR.PlaceholderText = T_704ILR("CRO_ACTIVIDAD", "Actividad");
            _cboResponsable_704ILR = Ui_704ILR.Combo_704ILR(); _cboResponsable_704ILR.Width = 284; _cboResponsable_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _numDuracion_704ILR = new CampoEntero_704ILR { Minimum = BLL_Cronograma_704ILR.DuracionMinima_704ILR, Maximum = BLL_Cronograma_704ILR.DuracionMaxima_704ILR, Value = 30, Increment = 5, Width = 70, Font = Theme_704ILR.FontInput_704ILR, Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, 0), TextAlign = HorizontalAlignment.Right };
            var lblMin_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("CRO_MIN", "min")); lblMin_704ILR.Margin = new Padding(0, 8, Theme_704ILR.SpaceSm_704ILR, 0);
            _btnAgregar_704ILR = Boton_704ILR(T_704ILR("CRO_AGREGAR", "Agregar"), Theme_704ILR.IcoAdd_704ILR, 120);
            _btnAgregar_704ILR.Click += (s_704ILR, e_704ILR) => Agregar_704ILR();
            alta_704ILR.Controls.Add(_dtHora_704ILR); alta_704ILR.Controls.Add(_txtDescripcion_704ILR); alta_704ILR.Controls.Add(_cboResponsable_704ILR);
            alta_704ILR.Controls.Add(_numDuracion_704ILR); alta_704ILR.Controls.Add(lblMin_704ILR); alta_704ILR.Controls.Add(_btnAgregar_704ILR);
            CentrarFila_704ILR(alta_704ILR);
            // Los campos de la fila no llevan rotulo: el nombre accesible dice que es cada uno.
            _dtHora_704ILR.AccessibleName = T_704ILR("CRO_COL_HORA", "Hora");
            _txtDescripcion_704ILR.AccessibleName = T_704ILR("CRO_ACTIVIDAD", "Actividad");
            _cboResponsable_704ILR.AccessibleName = T_704ILR("CRO_COL_RESPONSABLE", "Responsable");
            _numDuracion_704ILR.AccessibleName = T_704ILR("CRO_COL_DURACION", "Duración (min)");

            // --- Grilla de actividades ---
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOrden", HeaderText = "#", FillWeight = 14, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cHora", HeaderText = T_704ILR("CRO_COL_HORA", "Hora"), FillWeight = 24 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cActividad", HeaderText = T_704ILR("CRO_ACTIVIDAD", "Actividad"), FillWeight = 120 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cResponsable", HeaderText = T_704ILR("CRO_COL_RESPONSABLE", "Responsable"), FillWeight = 75 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDuracion", HeaderText = T_704ILR("CRO_COL_DURACION", "Duración (min)"), FillWeight = 40, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            // El orden es el del coordinador: la grilla no se reordena por columna.
            foreach (DataGridViewColumn c_704ILR in _grid_704ILR.Columns) c_704ILR.SortMode = DataGridViewColumnSortMode.NotSortable;
            UiGrid_704ILR.Multilinea_704ILR(_grid_704ILR, "cActividad", "cResponsable");
            UiGrid_704ILR.AlContenido_704ILR(_grid_704ILR, "cOrden", "cHora", "cDuracion");
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_grid_704ILR);
            _grid_704ILR.SelectionChanged += (s_704ILR, e_704ILR) => ActualizarAcciones_704ILR();
            card_704ILR.Controls.Add(_grid_704ILR);

            // --- Pie: ordenar/quitar a la izquierda; eliminar, guardar y cerrar a la derecha ---
            var footer_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, BackColor = Color.Transparent };
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var izquierda_704ILR = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Anchor = AnchorStyles.Left, Margin = new Padding(0) };
            _btnSubir_704ILR = Boton_704ILR(T_704ILR("CRO_SUBIR", "Subir"), Theme_704ILR.IcoArriba_704ILR, 100, secundario_704ILR: true);
            _btnSubir_704ILR.Click += (s_704ILR, e_704ILR) => Mover_704ILR(-1);
            _btnBajar_704ILR = Boton_704ILR(T_704ILR("CRO_BAJAR", "Bajar"), Theme_704ILR.IcoAbajo_704ILR, 100, secundario_704ILR: true);
            _btnBajar_704ILR.Click += (s_704ILR, e_704ILR) => Mover_704ILR(1);
            _btnQuitar_704ILR = Boton_704ILR(T_704ILR("BTN_QUITAR", "Quitar"), Theme_704ILR.IcoClear_704ILR, 100, secundario_704ILR: true);
            _btnQuitar_704ILR.Click += (s_704ILR, e_704ILR) => QuitarActividad_704ILR();
            foreach (var b_704ILR in new[] { _btnSubir_704ILR, _btnBajar_704ILR, _btnQuitar_704ILR }) { b_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0); izquierda_704ILR.Controls.Add(b_704ILR); }
            var derecha_704ILR = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Anchor = AnchorStyles.Right, Margin = new Padding(0) };
            _btnEliminar_704ILR = Boton_704ILR(T_704ILR("CRO_ELIMINAR", "Eliminar cronograma"), Theme_704ILR.IcoEliminar_704ILR, 190, secundario_704ILR: true);
            _btnEliminar_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Eliminar_704ILR, "Eliminar cronograma");
            _btnGuardar_704ILR = Boton_704ILR(T_704ILR("CRO_GENERAR", "Generar cronograma"), Theme_704ILR.IcoSave_704ILR, 190);
            _btnGuardar_704ILR.Click += (s_704ILR, e_704ILR) => Ejecutar_704ILR(Guardar_704ILR, "Guardar cronograma");
            var btnCerrar_704ILR = Boton_704ILR(T_704ILR("BTN_CERRAR", "Cerrar"), Theme_704ILR.IcoClose_704ILR, 110);
            btnCerrar_704ILR.Click += (s_704ILR, e_704ILR) => Close();
            foreach (var b_704ILR in new[] { _btnEliminar_704ILR, _btnGuardar_704ILR, btnCerrar_704ILR }) { b_704ILR.Margin = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, 0, 0); derecha_704ILR.Controls.Add(b_704ILR); }
            footer_704ILR.Controls.Add(izquierda_704ILR, 0, 0);
            footer_704ILR.Controls.Add(derecha_704ILR, 1, 0);

            root_704ILR.Controls.Add(ArmarEncabezado_704ILR(), 0, 0);
            root_704ILR.Controls.Add(_lblAviso_704ILR, 0, 1);
            root_704ILR.Controls.Add(alta_704ILR, 0, 2);
            root_704ILR.Controls.Add(card_704ILR, 0, 3);
            root_704ILR.Controls.Add(footer_704ILR, 0, 4);

            Controls.Add(root_704ILR);
            Controls.Add(ArmarTitulo_704ILR(T_704ILR("CRO_TITULO", "Cronograma del evento")));
            // Enter agrega la actividad tipeada a la lista (no guarda: Guardar va con clic).
            // Escape cierra; con cambios sin guardar, el cierre pregunta antes.
            AcceptButton = _btnAgregar_704ILR;
            CancelButton = btnCerrar_704ILR;
        }

        private void Refrescar_704ILR() => Ejecutar_704ILR(RefrescarCronograma_704ILR, "Cargar cronograma");

        // Lee el evento, el equipo confirmado y el cronograma guardado, y deja la lista
        // de trabajo igual a lo guardado.
        private void RefrescarCronograma_704ILR()
        {
            LeerEvento_704ILR();
            var asignaciones_704ILR = BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(_reservaId_704ILR);
            var confirmados_704ILR = asignaciones_704ILR.Where(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.CONFIRMADA).ToList();
            _hayConfirmados_704ILR = confirmados_704ILR.Count > 0;
            _hayPendientes_704ILR = asignaciones_704ILR.Any(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.PENDIENTE);
            ArmarResponsables_704ILR(confirmados_704ILR);

            BE_Cronograma_704ILR cronograma_704ILR = BLL_Cronograma_704ILR.GetByReserva_704ILR(_reservaId_704ILR);
            _existe_704ILR = cronograma_704ILR != null;
            _guardadas_704ILR = cronograma_704ILR == null ? new List<BE_CronogramaActividad_704ILR>() : cronograma_704ILR.Actividades_704ILR;
            _actividades_704ILR.Clear();
            foreach (var a_704ILR in _guardadas_704ILR) _actividades_704ILR.Add(Copia_704ILR(a_704ILR));
            // La fila de alta arranca donde termina la ultima actividad (o, sin
            // actividades, al comienzo del primer turno confirmado).
            TimeSpan? proxima_704ILR = _actividades_704ILR.Count > 0
                ? _actividades_704ILR[_actividades_704ILR.Count - 1].Hora_704ILR + TimeSpan.FromMinutes(_actividades_704ILR[_actividades_704ILR.Count - 1].DuracionMinutos_704ILR)
                : confirmados_704ILR.Count > 0 ? confirmados_704ILR.Min(a_704ILR => a_704ILR.HoraInicio_704ILR) : (TimeSpan?)null;
            if (proxima_704ILR.HasValue)
                _dtHora_704ILR.Value = _dtHora_704ILR.Value.Date + TimeSpan.FromMinutes(Math.Floor(proxima_704ILR.Value.TotalMinutes) % 1440);
            Pintar_704ILR(0);
        }

        // El combo de responsables ofrece al personal confirmado; conserva al elegido si
        // sigue en el equipo.
        private void ArmarResponsables_704ILR(List<BE_AsignacionPersonal_704ILR> confirmados_704ILR)
        {
            int elegido_704ILR = _cboResponsable_704ILR.SelectedItem is ResponsableItem_704ILR r_704ILR ? r_704ILR.EmpleadoId_704ILR : 0;
            _cboResponsable_704ILR.Items.Clear();
            foreach (var a_704ILR in confirmados_704ILR) _cboResponsable_704ILR.Items.Add(new ResponsableItem_704ILR(a_704ILR));
            int indice_704ILR = _cboResponsable_704ILR.Items.Count > 0 ? 0 : -1;
            for (int i_704ILR = 0; i_704ILR < _cboResponsable_704ILR.Items.Count; i_704ILR++)
                if (((ResponsableItem_704ILR)_cboResponsable_704ILR.Items[i_704ILR]).EmpleadoId_704ILR == elegido_704ILR) { indice_704ILR = i_704ILR; break; }
            _cboResponsable_704ILR.SelectedIndex = indice_704ILR;
            Coord_704ILR.AjustarDesplegable_704ILR(_cboResponsable_704ILR);
        }

        private void Pintar_704ILR(int seleccionar_704ILR)
        {
            _grid_704ILR.Rows.Clear();
            for (int i_704ILR = 0; i_704ILR < _actividades_704ILR.Count; i_704ILR++)
            {
                var a_704ILR = _actividades_704ILR[i_704ILR];
                _grid_704ILR.Rows.Add(i_704ILR + 1, Coord_704ILR.Hora_704ILR(a_704ILR.Hora_704ILR), a_704ILR.Descripcion_704ILR, a_704ILR.ResponsableNombre_704ILR, a_704ILR.DuracionMinutos_704ILR);
            }
            if (_grid_704ILR.Rows.Count > 0)
                _grid_704ILR.CurrentCell = _grid_704ILR.Rows[Math.Max(0, Math.Min(seleccionar_704ILR, _grid_704ILR.Rows.Count - 1))].Cells[0];
            ActualizarAcciones_704ILR();
        }

        // Primera capa: la edicion se ofrece con el permiso, con el plan todavia editable
        // y con el equipo que la RN-11 exige (ver EquipoAdmiteEdicion_704ILR). El aviso
        // dice por que no se puede editar o, con respuestas pendientes sobre un
        // cronograma ya generado, que los responsables tienen que ser confirmados.
        protected override void ActualizarAcciones_704ILR()
        {
            if (_btnGuardar_704ILR == null) return;
            bool permiso_704ILR = Permisos_704ILR.Tiene_704ILR("CRONOGRAMA_GESTION");
            bool editable_704ILR = PlanEditable_704ILR && permiso_704ILR && EquipoAdmiteEdicion_704ILR;
            int fila_704ILR = Coord_704ILR.Fila_704ILR(_grid_704ILR)?.Index ?? -1;

            _dtHora_704ILR.Enabled = _txtDescripcion_704ILR.Enabled = _cboResponsable_704ILR.Enabled = _numDuracion_704ILR.Enabled = editable_704ILR;
            _btnAgregar_704ILR.Enabled = editable_704ILR && _cboResponsable_704ILR.Items.Count > 0;
            _btnSubir_704ILR.Enabled = editable_704ILR && fila_704ILR > 0;
            _btnBajar_704ILR.Enabled = editable_704ILR && fila_704ILR >= 0 && fila_704ILR < _actividades_704ILR.Count - 1;
            _btnQuitar_704ILR.Enabled = editable_704ILR && fila_704ILR >= 0;
            _btnGuardar_704ILR.Enabled = editable_704ILR && _actividades_704ILR.Count > 0 && HayCambios_704ILR();
            // Eliminar no exige al equipo confirmado: tambien se elimina para rehacerlo.
            _btnEliminar_704ILR.Enabled = PlanEditable_704ILR && permiso_704ILR && _existe_704ILR;
            _btnGuardar_704ILR.Text = _existe_704ILR ? T_704ILR("CRO_GUARDAR", "Guardar cambios") : T_704ILR("CRO_GENERAR", "Generar cronograma");

            string aviso_704ILR = AvisoPlanNoEditable_704ILR()
                ?? (!EquipoAdmiteEdicion_704ILR ? Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.PersonalSinConfirmar_704ILR)
                    : _hayPendientes_704ILR ? T_704ILR("MSG_CRO_PENDIENTES", "Hay respuestas pendientes: cada actividad tiene que quedar a cargo de personal confirmado.")
                    : null);
            _lblAviso_704ILR.Text = aviso_704ILR ?? string.Empty;
            _lblAviso_704ILR.Visible = aviso_704ILR != null;
        }

        // Agrega la actividad tipeada al final de la lista de trabajo. Las mismas reglas
        // de la capa de negocio se anticipan aca para avisar en el momento.
        private void Agregar_704ILR()
        {
            ConfirmarHoraTipeada_704ILR();
            string descripcion_704ILR = (_txtDescripcion_704ILR.Text ?? string.Empty).Trim();
            if (descripcion_704ILR.Length == 0)
            {
                Aviso_704ILR(T_704ILR("MSG_CRO_DESCRIPCION", "Ingrese la descripción de la actividad."));
                _txtDescripcion_704ILR.Focus();
                return;
            }
            if (!(_cboResponsable_704ILR.SelectedItem is ResponsableItem_704ILR responsable_704ILR))
            {
                Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.ResponsableInvalido_704ILR));
                return;
            }
            _actividades_704ILR.Add(new BE_CronogramaActividad_704ILR
            {
                Hora_704ILR = new TimeSpan(_dtHora_704ILR.Value.Hour, _dtHora_704ILR.Value.Minute, 0),
                Descripcion_704ILR = descripcion_704ILR,
                ResponsableId_704ILR = responsable_704ILR.EmpleadoId_704ILR,
                ResponsableNombre_704ILR = responsable_704ILR.Nombre_704ILR,
                DuracionMinutos_704ILR = (int)_numDuracion_704ILR.Value
            });
            // La actividad siguiente arranca donde termina esta: es lo habitual al cargar
            // una jornada de corrido.
            _dtHora_704ILR.Value = _dtHora_704ILR.Value.Date + TimeSpan.FromMinutes((_dtHora_704ILR.Value.TimeOfDay.TotalMinutes + (double)_numDuracion_704ILR.Value) % 1440);
            _txtDescripcion_704ILR.Clear();
            _txtDescripcion_704ILR.Focus();
            Pintar_704ILR(_actividades_704ILR.Count - 1);
        }

        private void Mover_704ILR(int paso_704ILR)
        {
            int i_704ILR = Coord_704ILR.Fila_704ILR(_grid_704ILR)?.Index ?? -1;
            int j_704ILR = i_704ILR + paso_704ILR;
            if (i_704ILR < 0 || j_704ILR < 0 || j_704ILR >= _actividades_704ILR.Count) return;
            var a_704ILR = _actividades_704ILR[i_704ILR];
            _actividades_704ILR[i_704ILR] = _actividades_704ILR[j_704ILR];
            _actividades_704ILR[j_704ILR] = a_704ILR;
            Pintar_704ILR(j_704ILR);
        }

        private void QuitarActividad_704ILR()
        {
            int i_704ILR = Coord_704ILR.Fila_704ILR(_grid_704ILR)?.Index ?? -1;
            if (i_704ILR < 0 || i_704ILR >= _actividades_704ILR.Count) return;
            _actividades_704ILR.RemoveAt(i_704ILR);
            Pintar_704ILR(i_704ILR);
        }

        private void Guardar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("CRONOGRAMA_GESTION", this, "guardar el cronograma de la reserva #" + _reservaId_704ILR)) return;
            bool generar_704ILR = !_existe_704ILR;
            var r_704ILR = BLL_Cronograma_704ILR.Guardar_704ILR(_reservaId_704ILR, _actividades_704ILR);
            if (r_704ILR != CoordinacionResult_704ILR.Success_704ILR)
            {
                // La lista de trabajo se conserva: el aviso dice que corregir. El estado
                // del evento y el equipo se releen, pudieron cambiar desde otra sesion.
                var confirmados_704ILR = RefrescarContexto_704ILR();
                // Si el rechazo es por un responsable, se nombra la actividad a corregir.
                BE_CronogramaActividad_704ILR huerfana_704ILR = r_704ILR != CoordinacionResult_704ILR.ResponsableInvalido_704ILR ? null
                    : _actividades_704ILR.FirstOrDefault(a_704ILR => !confirmados_704ILR.Contains(a_704ILR.ResponsableId_704ILR));
                Aviso_704ILR(huerfana_704ILR == null
                    ? Coord_704ILR.Mensaje_704ILR(r_704ILR)
                    : Tr_704ILR.F_704ILR("MSG_CRO_RESPONSABLE_ACTIVIDAD", "La actividad «{0}» está a cargo de {1}, que ya no es personal confirmado del evento: quítela y vuelva a agregarla con otro responsable.",
                        huerfana_704ILR.Descripcion_704ILR, huerfana_704ILR.ResponsableNombre_704ILR));
                return;
            }
            RefrescarCronograma_704ILR();
            Informar_704ILR(generar_704ILR ? T_704ILR("MSG_CRO_GENERADO", "Cronograma generado.") : T_704ILR("MSG_CRO_GUARDADO", "Cronograma guardado."));
        }

        private void Eliminar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("CRONOGRAMA_GESTION", this, "eliminar el cronograma de la reserva #" + _reservaId_704ILR)) return;
            if (!Preguntar_704ILR(T_704ILR("CRO_ELIMINAR_CONF", "¿Eliminar el cronograma de este evento? La operación no se puede deshacer."))) return;
            var r_704ILR = BLL_Cronograma_704ILR.Eliminar_704ILR(_reservaId_704ILR);
            if (r_704ILR == CoordinacionResult_704ILR.Success_704ILR) { RefrescarCronograma_704ILR(); return; }
            // Un rechazo no elimina nada: la lista de trabajo, con sus cambios sin
            // guardar, se conserva.
            RefrescarContexto_704ILR();
            Aviso_704ILR(Coord_704ILR.Mensaje_704ILR(r_704ILR));
        }

        // Relee el evento y el equipo sin tocar la lista de trabajo. Devuelve los
        // empleados confirmados del evento.
        private HashSet<int> RefrescarContexto_704ILR()
        {
            LeerEvento_704ILR();
            // Tambien si el cronograma sigue existiendo: otra sesion pudo eliminarlo, y de
            // eso dependen el rotulo de Guardar, el boton Eliminar y la regla del equipo
            // (RN-11). Si ya no existe, la lista de trabajo pasa a ser un cronograma nuevo.
            if (_evento_704ILR != null && _existe_704ILR && !_evento_704ILR.TieneCronograma_704ILR)
            {
                _existe_704ILR = false;
                _guardadas_704ILR = new List<BE_CronogramaActividad_704ILR>();
            }
            var asignaciones_704ILR = BLL_AsignacionPersonal_704ILR.GetByReserva_704ILR(_reservaId_704ILR);
            var confirmados_704ILR = asignaciones_704ILR.Where(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.CONFIRMADA).ToList();
            _hayConfirmados_704ILR = confirmados_704ILR.Count > 0;
            _hayPendientes_704ILR = asignaciones_704ILR.Any(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.PENDIENTE);
            ArmarResponsables_704ILR(confirmados_704ILR);
            ActualizarAcciones_704ILR();
            return new HashSet<int>(confirmados_704ILR.Select(a_704ILR => a_704ILR.EmpleadoId_704ILR));
        }

        // La lista de trabajo difiere de lo guardado (en contenido o en orden).
        private bool HayCambios_704ILR()
        {
            if (_actividades_704ILR.Count != _guardadas_704ILR.Count) return true;
            for (int i_704ILR = 0; i_704ILR < _actividades_704ILR.Count; i_704ILR++)
            {
                var a_704ILR = _actividades_704ILR[i_704ILR];
                var g_704ILR = _guardadas_704ILR[i_704ILR];
                if (a_704ILR.Hora_704ILR != g_704ILR.Hora_704ILR || a_704ILR.Descripcion_704ILR != g_704ILR.Descripcion_704ILR
                    || a_704ILR.ResponsableId_704ILR != g_704ILR.ResponsableId_704ILR || a_704ILR.DuracionMinutos_704ILR != g_704ILR.DuracionMinutos_704ILR)
                    return true;
            }
            return false;
        }

        private static BE_CronogramaActividad_704ILR Copia_704ILR(BE_CronogramaActividad_704ILR a_704ILR) => new BE_CronogramaActividad_704ILR
        {
            Id_704ILR = a_704ILR.Id_704ILR,
            CronogramaId_704ILR = a_704ILR.CronogramaId_704ILR,
            Orden_704ILR = a_704ILR.Orden_704ILR,
            Hora_704ILR = a_704ILR.Hora_704ILR,
            Descripcion_704ILR = a_704ILR.Descripcion_704ILR,
            ResponsableId_704ILR = a_704ILR.ResponsableId_704ILR,
            ResponsableNombre_704ILR = a_704ILR.ResponsableNombre_704ILR,
            DuracionMinutos_704ILR = a_704ILR.DuracionMinutos_704ILR
        };

        // Cerrar con la lista cambiada y sin guardar pregunta antes de descartar, con No
        // por defecto. Override del framework (sin sufijo, REGLA 4).
        protected override void OnFormClosing(FormClosingEventArgs e_704ILR)
        {
            if (!e_704ILR.Cancel && e_704ILR.CloseReason == CloseReason.UserClosing && !_descarteAceptado_704ILR
                && PlanEditable_704ILR && Permisos_704ILR.Tiene_704ILR("CRONOGRAMA_GESTION") && HayCambios_704ILR())
            {
                if (Preguntar_704ILR(T_704ILR("CRO_DESCARTAR", "El cronograma tiene cambios sin guardar. ¿Descartarlos y cerrar?")))
                    _descarteAceptado_704ILR = true;
                else
                    e_704ILR.Cancel = true;
            }
            base.OnFormClosing(e_704ILR);
        }

        // Item del combo de responsables: un integrante confirmado del equipo, con su
        // rol para distinguirlo.
        private sealed class ResponsableItem_704ILR
        {
            public int EmpleadoId_704ILR { get; }
            public string Nombre_704ILR { get; }
            private readonly string _rol_704ILR;
            public ResponsableItem_704ILR(BE_AsignacionPersonal_704ILR a_704ILR)
            {
                EmpleadoId_704ILR = a_704ILR.EmpleadoId_704ILR;
                Nombre_704ILR = a_704ILR.EmpleadoNombre_704ILR;
                _rol_704ILR = a_704ILR.RolAsignado_704ILR;
            }
            public override string ToString() => Nombre_704ILR + " (" + _rol_704ILR + ")";
        }
    }
}
