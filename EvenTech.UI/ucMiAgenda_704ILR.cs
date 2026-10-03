using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Agenda del empleado (Proceso 2). El empleado ve los turnos que le asignaron,
    // acepta o rechaza los que estan pendientes (CUN007) y consulta, para el evento
    // elegido, sus tareas y el cronograma de la jornada (CUN010). La identidad sale de
    // la cuenta de la sesion: la ficha del empleado tiene que estar vinculada a ella.
    // Observa el cambio de idioma.
    public class ucMiAgenda_704ILR : UserControl, IObservadorIdioma_704ILR
    {
        private DataGridView _gridAsignaciones_704ILR, _gridTareas_704ILR, _gridCronograma_704ILR;
        private Label _lblEmpleado_704ILR, _lblError_704ILR, _lblEvento_704ILR;
        private AppButton_704ILR _btnConfirmar_704ILR, _btnRechazar_704ILR;
        private BE_Empleado_704ILR _empleado_704ILR;
        private List<BE_AsignacionPersonal_704ILR> _asignaciones_704ILR = new List<BE_AsignacionPersonal_704ILR>();
        // Tareas y cronograma del evento elegido, guardados para repintarlos al cambiar
        // de idioma sin volver a leer la base.
        private List<BE_Tarea_704ILR> _tareas_704ILR = new List<BE_Tarea_704ILR>();
        private BE_Cronograma_704ILR _cronograma_704ILR;
        private int _detalleDe_704ILR;
        private bool _pintando_704ILR;
        private Func<string> _textoError_704ILR;

        public ucMiAgenda_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => { Cargar_704ILR(0); GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this); };
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        private void BuildUi_704ILR()
        {
            var root_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme_704ILR.BgContent_704ILR };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // titulo + acciones
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 46));  // mis turnos
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // evento elegido
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 54));  // tareas | cronograma

            // --- Encabezado: titulo, empleado, confirmar y rechazar ---
            var header_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4, RowCount = 2, BackColor = Theme_704ILR.BgContent_704ILR, Padding = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Mi agenda");
            lblTitle_704ILR.Tag = "T:AGE_TITULO"; lblTitle_704ILR.Anchor = AnchorStyles.Left; lblTitle_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);
            _lblEmpleado_704ILR = Ui_704ILR.Body_704ILR(); _lblEmpleado_704ILR.ForeColor = Theme_704ILR.TextMuted_704ILR; _lblEmpleado_704ILR.Anchor = AnchorStyles.Left;

            _btnConfirmar_704ILR = Ui_704ILR.Primary_704ILR("Confirmar", Theme_704ILR.IcoOk_704ILR);
            _btnConfirmar_704ILR.Tag = "T:AGE_CONFIRMAR"; _btnConfirmar_704ILR.Size = new Size(150, 36); _btnConfirmar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnConfirmar_704ILR.Anchor = AnchorStyles.Right; _btnConfirmar_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _btnConfirmar_704ILR.Click += (s_704ILR, e_704ILR) => Responder_704ILR(true);
            _btnRechazar_704ILR = Ui_704ILR.Secondary_704ILR("Rechazar", Theme_704ILR.IcoClear_704ILR);
            _btnRechazar_704ILR.Tag = "T:AGE_RECHAZAR"; _btnRechazar_704ILR.Size = new Size(150, 36); _btnRechazar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnRechazar_704ILR.Anchor = AnchorStyles.Right; _btnRechazar_704ILR.Margin = new Padding(0);
            _btnRechazar_704ILR.Click += (s_704ILR, e_704ILR) => Responder_704ILR(false);

            _lblError_704ILR = Ui_704ILR.Body_704ILR(); _lblError_704ILR.Font = Theme_704ILR.FontBodyBold_704ILR; _lblError_704ILR.ForeColor = Theme_704ILR.Error_704ILR;
            _lblError_704ILR.Visible = false; _lblError_704ILR.AutoSize = true; _lblError_704ILR.MaximumSize = new Size(900, 0);
            _lblError_704ILR.Anchor = AnchorStyles.Left; _lblError_704ILR.Margin = new Padding(0, Theme_704ILR.SpaceXs_704ILR, 0, 0);

            header_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            header_704ILR.Controls.Add(_lblEmpleado_704ILR, 1, 0);
            header_704ILR.Controls.Add(_btnConfirmar_704ILR, 2, 0);
            header_704ILR.Controls.Add(_btnRechazar_704ILR, 3, 0);
            header_704ILR.Controls.Add(_lblError_704ILR, 0, 1);
            header_704ILR.SetColumnSpan(_lblError_704ILR, 4);

            // --- Mis turnos ---
            _gridAsignaciones_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_gridAsignaciones_704ILR);
            _gridAsignaciones_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFecha", HeaderText = "Fecha", FillWeight = 38 });
            _gridAsignaciones_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSalon", HeaderText = "Salon", FillWeight = 55 });
            _gridAsignaciones_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRol", HeaderText = "Rol", FillWeight = 50 });
            _gridAsignaciones_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFranja", HeaderText = "Franja", FillWeight = 40 });
            _gridAsignaciones_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstado", HeaderText = "Estado", FillWeight = 38 });
            _gridAsignaciones_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEvento", HeaderText = "Evento", FillWeight = 42 });
            _gridAsignaciones_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMotivo", HeaderText = "Motivo", FillWeight = 80 });
            Coord_704ILR.SinOrden_704ILR(_gridAsignaciones_704ILR);
            UiGrid_704ILR.Multilinea_704ILR(_gridAsignaciones_704ILR, "cSalon", "cRol", "cMotivo");
            UiGrid_704ILR.AlContenido_704ILR(_gridAsignaciones_704ILR, "cFecha", "cFranja", "cEstado", "cEvento");
            // Al pasar a otro turno, el aviso del rechazo anterior ya no corresponde.
            _gridAsignaciones_704ILR.SelectionChanged += (s_704ILR, e_704ILR) =>
            {
                if (_pintando_704ILR) return;
                _lblError_704ILR.Visible = false;
                _textoError_704ILR = null;
                MostrarEvento_704ILR(true);
            };
            var cardAsignaciones_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            cardAsignaciones_704ILR.Controls.Add(_gridAsignaciones_704ILR);

            _lblEvento_704ILR = Ui_704ILR.BodyBold_704ILR();
            _lblEvento_704ILR.Margin = new Padding(2, 0, 0, Theme_704ILR.SpaceXs_704ILR);
            // El nombre del salon puede llevar '&': se muestra tal cual.
            _lblEvento_704ILR.UseMnemonic = false;

            // --- Detalle del evento elegido: mis tareas y el cronograma ---
            var detalle_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme_704ILR.BgContent_704ILR, Margin = new Padding(0) };
            detalle_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            detalle_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            detalle_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _gridTareas_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_gridTareas_704ILR);
            _gridTareas_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFranja", HeaderText = "Franja", FillWeight = 58 });
            _gridTareas_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTarea", HeaderText = "Tarea", FillWeight = 105 });
            _gridTareas_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPrioridad", HeaderText = "Prioridad", FillWeight = 50 });
            _gridTareas_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRecursos", HeaderText = "Recursos", FillWeight = 62 });
            // Las tareas van en el orden del turno: la grilla no se reordena por columna.
            Coord_704ILR.SinOrden_704ILR(_gridTareas_704ILR);
            UiGrid_704ILR.Multilinea_704ILR(_gridTareas_704ILR, "cTarea", "cRecursos");
            UiGrid_704ILR.AlContenido_704ILR(_gridTareas_704ILR, "cFranja", "cPrioridad");
            var cardTareas_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            cardTareas_704ILR.Controls.Add(_gridTareas_704ILR);

            _gridCronograma_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_gridCronograma_704ILR);
            _gridCronograma_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cHora", HeaderText = "Hora", FillWeight = 26 });
            _gridCronograma_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cActividad", HeaderText = "Actividad", FillWeight = 100 });
            _gridCronograma_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cResponsable", HeaderText = "Responsable", FillWeight = 70 });
            _gridCronograma_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDuracion", HeaderText = "min", FillWeight = 22, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            foreach (DataGridViewColumn c_704ILR in _gridCronograma_704ILR.Columns) c_704ILR.SortMode = DataGridViewColumnSortMode.NotSortable;
            UiGrid_704ILR.Multilinea_704ILR(_gridCronograma_704ILR, "cActividad", "cResponsable");
            UiGrid_704ILR.AlContenido_704ILR(_gridCronograma_704ILR, "cHora", "cDuracion");
            var cardCronograma_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            cardCronograma_704ILR.Controls.Add(_gridCronograma_704ILR);

            detalle_704ILR.Controls.Add(cardTareas_704ILR, 0, 0);
            detalle_704ILR.Controls.Add(cardCronograma_704ILR, 1, 0);

            root_704ILR.Controls.Add(header_704ILR, 0, 0);
            root_704ILR.Controls.Add(cardAsignaciones_704ILR, 0, 1);
            root_704ILR.Controls.Add(_lblEvento_704ILR, 0, 2);
            root_704ILR.Controls.Add(detalle_704ILR, 0, 3);
            Controls.Add(root_704ILR);
        }

        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            _gridAsignaciones_704ILR.Columns["cFecha"].HeaderText  = T_704ILR("COL_FECHA", "Fecha");
            _gridAsignaciones_704ILR.Columns["cSalon"].HeaderText  = T_704ILR("COL_SALON", "Salón");
            _gridAsignaciones_704ILR.Columns["cRol"].HeaderText    = T_704ILR("ASG_COL_ROL", "Rol");
            _gridAsignaciones_704ILR.Columns["cFranja"].HeaderText = T_704ILR("ASG_COL_FRANJA", "Franja");
            _gridAsignaciones_704ILR.Columns["cEstado"].HeaderText = T_704ILR("COL_ESTADO", "Estado");
            _gridAsignaciones_704ILR.Columns["cEvento"].HeaderText = T_704ILR("AGE_COL_EVENTO", "Evento");
            _gridAsignaciones_704ILR.Columns["cMotivo"].HeaderText = T_704ILR("ASG_COL_MOTIVO", "Motivo del rechazo");
            _gridTareas_704ILR.Columns["cFranja"].HeaderText    = T_704ILR("ASG_COL_FRANJA", "Franja");
            _gridTareas_704ILR.Columns["cTarea"].HeaderText     = T_704ILR("AGE_COL_MI_TAREA", "Mis tareas");
            _gridTareas_704ILR.Columns["cPrioridad"].HeaderText = T_704ILR("TAR_COL_PRIORIDAD", "Prioridad");
            _gridTareas_704ILR.Columns["cRecursos"].HeaderText  = T_704ILR("TAR_COL_RECURSOS", "Recursos");
            _gridCronograma_704ILR.Columns["cHora"].HeaderText        = T_704ILR("CRO_COL_HORA", "Hora");
            _gridCronograma_704ILR.Columns["cActividad"].HeaderText   = T_704ILR("AGE_COL_CRONOGRAMA", "Cronograma del evento");
            _gridCronograma_704ILR.Columns["cResponsable"].HeaderText = T_704ILR("CRO_COL_RESPONSABLE", "Responsable");
            _gridCronograma_704ILR.Columns["cDuracion"].HeaderText    = T_704ILR("CRO_MIN", "min");
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_gridAsignaciones_704ILR);
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_gridTareas_704ILR);
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_gridCronograma_704ILR);
            if (_textoError_704ILR != null && _lblError_704ILR.Visible) _lblError_704ILR.Text = _textoError_704ILR();
            // Los estados y el detalle se escriben ya traducidos: se rehacen sin releer.
            PintarAsignaciones_704ILR(AsignacionSeleccionada_704ILR()?.Id_704ILR ?? 0);
        }

        // Lee al empleado de la sesion y sus turnos, y deja seleccionado el indicado (0, o
        // uno que ya no esta = el primero). Una falla se asienta, se informa y deja la
        // agenda vacia.
        private void Cargar_704ILR(int seleccionar_704ILR)
        {
            try
            {
                _lblError_704ILR.Visible = false;
                _textoError_704ILR = null;
                _empleado_704ILR = BLL_Empleado_704ILR.GetDeLaSesion_704ILR();
                _asignaciones_704ILR = BLL_AsignacionPersonal_704ILR.GetMisAsignaciones_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Coordinacion", "Cargar la agenda");
                _empleado_704ILR = null;
                _asignaciones_704ILR = new List<BE_AsignacionPersonal_704ILR>();
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
            _detalleDe_704ILR = 0;
            PintarAsignaciones_704ILR(seleccionar_704ILR);
        }

        private void PintarAsignaciones_704ILR(int seleccionar_704ILR)
        {
            _pintando_704ILR = true;
            bool elegida_704ILR = false;
            try
            {
                _gridAsignaciones_704ILR.Rows.Clear();
                foreach (var a_704ILR in _asignaciones_704ILR)
                {
                    int i_704ILR = _gridAsignaciones_704ILR.Rows.Add(Coord_704ILR.Fecha_704ILR(a_704ILR.FechaEvento_704ILR), a_704ILR.SalonNombre_704ILR,
                        a_704ILR.RolAsignado_704ILR, Coord_704ILR.Franja_704ILR(a_704ILR.HoraInicio_704ILR, a_704ILR.HoraFin_704ILR),
                        Coord_704ILR.Asignacion_704ILR(a_704ILR.Estado_704ILR), Coord_704ILR.Estado_704ILR(a_704ILR.EstadoCoordinacion_704ILR),
                        a_704ILR.MotivoRechazo_704ILR ?? string.Empty);
                    _gridAsignaciones_704ILR.Rows[i_704ILR].Tag = a_704ILR;
                    _gridAsignaciones_704ILR.Rows[i_704ILR].Cells["cEstado"].Style.ForeColor = Coord_704ILR.ColorAsignacion_704ILR(a_704ILR.Estado_704ILR);
                    _gridAsignaciones_704ILR.Rows[i_704ILR].Cells["cEvento"].Style.ForeColor = Coord_704ILR.Color_704ILR(a_704ILR.EstadoCoordinacion_704ILR);
                    if (a_704ILR.Id_704ILR == seleccionar_704ILR)
                    {
                        _gridAsignaciones_704ILR.CurrentCell = _gridAsignaciones_704ILR.Rows[i_704ILR].Cells[0];
                        elegida_704ILR = true;
                    }
                }
                // La agenda abre con el primer turno elegido y su detalle a la vista: la
                // carga corre antes de que la grilla se muestre y, sin fijar la celda
                // actual, quedaba sin ninguna fila resaltada.
                if (!elegida_704ILR && _gridAsignaciones_704ILR.Rows.Count > 0)
                    _gridAsignaciones_704ILR.CurrentCell = _gridAsignaciones_704ILR.Rows[0].Cells[0];
            }
            finally { _pintando_704ILR = false; }

            _lblEmpleado_704ILR.Text = _empleado_704ILR == null
                ? (_textoError_704ILR == null ? Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.SinEmpleadoVinculado_704ILR) : string.Empty)
                : _empleado_704ILR.NombreCompleto_704ILR + " · " + Coord_704ILR.Especialidad_704ILR(_empleado_704ILR.EspecialidadNombre_704ILR)
                  // Con la ficha dada de baja el empleado ya no responde turnos: se dice aca,
                  // antes de que lo intente.
                  + " · " + (_empleado_704ILR.Activo_704ILR
                      ? _asignaciones_704ILR.Count + " " + T_704ILR("AGE_COUNT", "turno(s)")
                      : Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.EmpleadoDeBaja_704ILR));
            _lblEmpleado_704ILR.ForeColor = _empleado_704ILR == null || !_empleado_704ILR.Activo_704ILR ? Theme_704ILR.Warning_704ILR : Theme_704ILR.TextMuted_704ILR;
            MostrarEvento_704ILR(false);
        }

        private BE_AsignacionPersonal_704ILR AsignacionSeleccionada_704ILR() => Coord_704ILR.Fila_704ILR(_gridAsignaciones_704ILR)?.Tag as BE_AsignacionPersonal_704ILR;

        // Detalle del evento del turno elegido: las tareas propias y el cronograma de la
        // jornada. Con 'releer' se vuelven a leer de la base (cambio de fila); sin el,
        // se repintan los ya leidos (cambio de idioma).
        private void MostrarEvento_704ILR(bool releer_704ILR)
        {
            BE_AsignacionPersonal_704ILR a_704ILR = AsignacionSeleccionada_704ILR();
            // Primera capa: responder exige el permiso, la ficha activa y un turno propio
            // pendiente, en un evento que todavia no empezo. La capa de negocio vuelve a
            // exigirlo.
            bool puedeResponder_704ILR = a_704ILR != null && a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.PENDIENTE
                && _empleado_704ILR != null && _empleado_704ILR.Activo_704ILR
                && !BLL_Coordinacion_704ILR.PlanCongelado_704ILR(a_704ILR.EstadoCoordinacion_704ILR)
                && Permisos_704ILR.Tiene_704ILR("DISPONIBILIDAD_CONFIRMAR");
            _btnConfirmar_704ILR.Enabled = _btnRechazar_704ILR.Enabled = puedeResponder_704ILR;

            if (a_704ILR == null)
            {
                _lblEvento_704ILR.Text = _empleado_704ILR != null && _asignaciones_704ILR.Count == 0
                    ? T_704ILR("AGE_SIN_TURNOS", "Todavía no tenés turnos asignados.")
                    : string.Empty;
                _tareas_704ILR = new List<BE_Tarea_704ILR>();
                _cronograma_704ILR = null;
                _detalleDe_704ILR = 0;
            }
            else
            {
                // Sin el permiso de agenda las tareas y el cronograma no se consultan: se
                // dice, en vez de mostrar dos grillas vacias como si no hubiera nada.
                _lblEvento_704ILR.Text = Tr_704ILR.F_704ILR("AGE_EVENTO", "Evento del {0} en {1} (reserva #{2})",
                    Coord_704ILR.Fecha_704ILR(a_704ILR.FechaEvento_704ILR), a_704ILR.SalonNombre_704ILR, a_704ILR.ReservaId_704ILR)
                    + (Permisos_704ILR.Tiene_704ILR("AGENDA_CONSULTAR") ? string.Empty
                        : " · " + T_704ILR("AGE_SIN_PERMISO_DETALLE", "Tu perfil no incluye la consulta de las tareas y del cronograma."));
                if (releer_704ILR || _detalleDe_704ILR != a_704ILR.ReservaId_704ILR) LeerDetalle_704ILR(a_704ILR.ReservaId_704ILR);
            }

            _gridTareas_704ILR.Rows.Clear();
            foreach (var t_704ILR in _tareas_704ILR)
            {
                int i_704ILR = _gridTareas_704ILR.Rows.Add(Coord_704ILR.Franja_704ILR(t_704ILR.HoraInicio_704ILR, t_704ILR.HoraFin_704ILR), t_704ILR.Descripcion_704ILR,
                    Coord_704ILR.Prioridad_704ILR(t_704ILR.Prioridad_704ILR), t_704ILR.Recursos_704ILR ?? string.Empty);
                if (t_704ILR.Prioridad_704ILR == PrioridadTarea_704ILR.ALTA)
                    _gridTareas_704ILR.Rows[i_704ILR].Cells["cPrioridad"].Style.ForeColor = Theme_704ILR.Error_704ILR;
            }
            _gridCronograma_704ILR.Rows.Clear();
            if (_cronograma_704ILR != null)
                foreach (var act_704ILR in _cronograma_704ILR.Actividades_704ILR)
                {
                    int i_704ILR = _gridCronograma_704ILR.Rows.Add(Coord_704ILR.Hora_704ILR(act_704ILR.Hora_704ILR), act_704ILR.Descripcion_704ILR, act_704ILR.ResponsableNombre_704ILR, act_704ILR.DuracionMinutos_704ILR);
                    // Los tramos que tiene a cargo el propio empleado van resaltados.
                    if (_empleado_704ILR != null && act_704ILR.ResponsableId_704ILR == _empleado_704ILR.Id_704ILR)
                        _gridCronograma_704ILR.Rows[i_704ILR].DefaultCellStyle.Font = FuenteResaltada_704ILR;
                }
        }

        // Tareas propias y cronograma del evento (CUN010). Se consultan con el permiso
        // de agenda; una falla se asienta, se informa y deja el detalle vacio.
        private void LeerDetalle_704ILR(int reservaId_704ILR)
        {
            _tareas_704ILR = new List<BE_Tarea_704ILR>();
            _cronograma_704ILR = null;
            _detalleDe_704ILR = reservaId_704ILR;
            if (!Permisos_704ILR.Tiene_704ILR("AGENDA_CONSULTAR")) return;
            try
            {
                _tareas_704ILR = BLL_Tarea_704ILR.GetMisTareas_704ILR(reservaId_704ILR);
                _cronograma_704ILR = BLL_Cronograma_704ILR.GetByReserva_704ILR(reservaId_704ILR);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Coordinacion", "Cargar tareas y cronograma de la reserva #" + reservaId_704ILR);
                _detalleDe_704ILR = 0;
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        // CUN007: el empleado acepta el turno o lo rechaza dejando el motivo.
        private void Responder_704ILR(bool acepta_704ILR)
        {
            BE_AsignacionPersonal_704ILR a_704ILR = AsignacionSeleccionada_704ILR();
            if (a_704ILR == null) return;
            // Segunda capa del control de acceso (ver Permisos.cs).
            if (!Permisos_704ILR.Exigir_704ILR("DISPONIBILIDAD_CONFIRMAR", FindForm(),
                    (acepta_704ILR ? "confirmar" : "rechazar") + " el turno de la reserva #" + a_704ILR.ReservaId_704ILR))
                return;
            try
            {
                _lblError_704ILR.Visible = false;
                _textoError_704ILR = null;
                CoordinacionResult_704ILR r_704ILR;
                BE_AsignacionPersonal_704ILR conflicto_704ILR = null;
                if (acepta_704ILR)
                    r_704ILR = BLL_AsignacionPersonal_704ILR.Confirmar_704ILR(a_704ILR.Id_704ILR, out conflicto_704ILR);
                else
                {
                    string motivo_704ILR;
                    using (var frm_704ILR = new frmTextoRequerido_704ILR(T_704ILR("AGE_RECHAZAR_TITULO", "Rechazar turno"),
                               T_704ILR("AGE_RECHAZAR_LBL", "Motivo del rechazo"),
                               Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.MotivoObligatorio_704ILR), 250))
                    {
                        if (frm_704ILR.ShowDialog(FindForm()) != DialogResult.OK) return;
                        motivo_704ILR = frm_704ILR.Texto_704ILR;
                    }
                    r_704ILR = BLL_AsignacionPersonal_704ILR.Rechazar_704ILR(a_704ILR.Id_704ILR, motivo_704ILR);
                }

                // La agenda se relee siempre: un rechazo puede venir de un cambio hecho
                // por el coordinador con la pantalla abierta.
                Cargar_704ILR(a_704ILR.Id_704ILR);
                if (r_704ILR != CoordinacionResult_704ILR.Success_704ILR)
                    MostrarError_704ILR(() => r_704ILR == CoordinacionResult_704ILR.Superposicion_704ILR
                        ? Coord_704ILR.Superposicion_704ILR(conflicto_704ILR)
                        : Coord_704ILR.Mensaje_704ILR(r_704ILR));
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Coordinacion", "Responder el turno de la reserva #" + a_704ILR.ReservaId_704ILR);
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        private void MostrarError_704ILR(Func<string> texto_704ILR)
        {
            _textoError_704ILR = texto_704ILR;
            _lblError_704ILR.Text = texto_704ILR();
            _lblError_704ILR.Visible = true;
        }

        // Negrita del mismo tamano que la letra de las grillas, para resaltar una fila
        // sin cambiar su alto. Se crea una sola vez (una fuente por fila agotaria los
        // objetos GDI).
        private static readonly Font FuenteResaltada_704ILR = new Font(Theme_704ILR.FontSmall_704ILR, FontStyle.Bold);

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR) => Coord_704ILR.T_704ILR(clave_704ILR, defecto_704ILR);
    }
}
