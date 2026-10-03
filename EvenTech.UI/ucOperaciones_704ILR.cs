using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Operaciones de los eventos (Proceso 2): los eventos de las reservas confirmadas
    // con su estado de coordinacion, el repaso de lo contratado y el acceso a cada
    // paso de la coordinacion (personal, cronograma, tareas) y a la supervision de la
    // ejecucion. Cada accion se habilita segun el permiso del perfil. Observa el
    // cambio de idioma.
    public class ucOperaciones_704ILR : UserControl, IObservadorIdioma_704ILR
    {
        private DataGridView _grid_704ILR;
        private Label _lblCount_704ILR, _lblError_704ILR, _lblDetalleTitulo_704ILR, _lblDatos_704ILR, _lblServicios_704ILR, _lblAvance_704ILR;
        // La ultima lectura de los eventos fallo: la grilla vacia no significa "no hay eventos".
        private bool _cargaFallida_704ILR;
        private AppButton_704ILR _btnPersonal_704ILR, _btnCronograma_704ILR, _btnTareas_704ILR, _btnSupervision_704ILR;
        private List<BE_EventoCoordinacion_704ILR> _eventos_704ILR = new List<BE_EventoCoordinacion_704ILR>();
        // Servicios contratados del evento seleccionado (se leen al cambiar de fila).
        private List<BE_ReservaServicio_704ILR> _servicios_704ILR = new List<BE_ReservaServicio_704ILR>();
        private int _serviciosDe_704ILR;
        // La grilla se esta rearmando: los cambios de seleccion que eso provoca no
        // repintan el detalle (se pinta una vez, al terminar).
        private bool _pintando_704ILR;
        private Func<string> _textoError_704ILR;

        public ucOperaciones_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => { Cargar_704ILR(0); GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this); };
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        private void BuildUi_704ILR()
        {
            var root_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme_704ILR.BgContent_704ILR };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root_704ILR.Controls.Add(BuildHeader_704ILR(), 0, 0);
            root_704ILR.Controls.Add(BuildBody_704ILR(), 0, 1);
            Controls.Add(root_704ILR);
        }

        private Control BuildHeader_704ILR()
        {
            var header_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3, RowCount = 2, BackColor = Theme_704ILR.BgContent_704ILR, Padding = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Operaciones de eventos");
            lblTitle_704ILR.Tag = "T:OPE_TITULO"; lblTitle_704ILR.Anchor = AnchorStyles.Left; lblTitle_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);

            _lblCount_704ILR = Ui_704ILR.Body_704ILR(); _lblCount_704ILR.ForeColor = Theme_704ILR.TextMuted_704ILR; _lblCount_704ILR.Anchor = AnchorStyles.Left;

            _lblError_704ILR = Ui_704ILR.Body_704ILR(); _lblError_704ILR.Font = Theme_704ILR.FontBodyBold_704ILR; _lblError_704ILR.ForeColor = Theme_704ILR.Error_704ILR;
            _lblError_704ILR.Visible = false; _lblError_704ILR.AutoSize = true; _lblError_704ILR.MaximumSize = new Size(900, 0);
            _lblError_704ILR.Anchor = AnchorStyles.Left; _lblError_704ILR.Margin = new Padding(0, Theme_704ILR.SpaceXs_704ILR, 0, 0);

            header_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            header_704ILR.Controls.Add(_lblCount_704ILR, 1, 0);
            header_704ILR.Controls.Add(_lblError_704ILR, 0, 1);
            header_704ILR.SetColumnSpan(_lblError_704ILR, 3);
            return header_704ILR;
        }

        private Control BuildBody_704ILR()
        {
            var body_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme_704ILR.BgContent_704ILR, Margin = new Padding(0) };
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body_704ILR.Controls.Add(BuildGridCard_704ILR(), 0, 0);
            body_704ILR.Controls.Add(BuildDetalleCard_704ILR(), 1, 0);
            return body_704ILR;
        }

        private Control BuildGridCard_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFecha", HeaderText = "Fecha", FillWeight = 42 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCliente", HeaderText = "Cliente", FillWeight = 70 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSalon", HeaderText = "Salon", FillWeight = 60 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPersonal", HeaderText = "Personal", FillWeight = 36, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstado", HeaderText = "Coordinacion", FillWeight = 58 });
            Coord_704ILR.SinOrden_704ILR(_grid_704ILR);
            _grid_704ILR.SelectionChanged += (s_704ILR, e_704ILR) => { if (!_pintando_704ILR) MostrarDetalle_704ILR(); };
            card_704ILR.Controls.Add(_grid_704ILR);
            return card_704ILR;
        }

        private Control BuildDetalleCard_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, MinimumSize = new Size(300, 0), Margin = new Padding(0), Padding = new Padding(Theme_704ILR.SpaceLg_704ILR) };
            var layout_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
            layout_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblDetalleTitulo_704ILR = Ui_704ILR.Title_704ILR("");
            _lblDetalleTitulo_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR);

            // Repaso de lo contratado y avance de la coordinacion. Tres bloques de texto
            // apilados dentro de un panel con desplazamiento: una reserva con muchos
            // servicios no empuja los botones fuera de la tarjeta.
            var datos_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            _lblDatos_704ILR = Bloque_704ILR();
            _lblServicios_704ILR = Bloque_704ILR();
            _lblAvance_704ILR = Bloque_704ILR();
            datos_704ILR.Controls.Add(_lblDatos_704ILR);
            datos_704ILR.Controls.Add(_lblServicios_704ILR);
            datos_704ILR.Controls.Add(_lblAvance_704ILR);
            // El ancho de los bloques sigue al del panel (menos la barra de desplazamiento).
            datos_704ILR.ClientSizeChanged += (s_704ILR, e_704ILR) =>
            {
                int ancho_704ILR = Math.Max(120, datos_704ILR.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
                foreach (Control c_704ILR in datos_704ILR.Controls) c_704ILR.MaximumSize = new Size(ancho_704ILR, 0);
            };

            var acciones_704ILR = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0) };
            acciones_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            acciones_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            acciones_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            acciones_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            _btnPersonal_704ILR = Accion_704ILR("Personal", "OPE_BTN_PERSONAL", Theme_704ILR.IcoPeople_704ILR, AbrirPersonal_704ILR);
            _btnCronograma_704ILR = Accion_704ILR("Cronograma", "OPE_BTN_CRONOGRAMA", Theme_704ILR.IcoReloj_704ILR, AbrirCronograma_704ILR);
            _btnTareas_704ILR = Accion_704ILR("Tareas", "OPE_BTN_TAREAS", Theme_704ILR.IcoTarea_704ILR, AbrirTareas_704ILR);
            _btnSupervision_704ILR = Accion_704ILR("Supervisión", "OPE_BTN_SUPERVISION", Theme_704ILR.IcoIniciar_704ILR, AbrirSupervision_704ILR);
            _btnPersonal_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, Theme_704ILR.SpaceSm_704ILR);
            _btnCronograma_704ILR.Margin = new Padding(Theme_704ILR.SpaceXs_704ILR, 0, 0, Theme_704ILR.SpaceSm_704ILR);
            _btnTareas_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, Theme_704ILR.SpaceSm_704ILR);
            _btnSupervision_704ILR.Margin = new Padding(Theme_704ILR.SpaceXs_704ILR, 0, 0, Theme_704ILR.SpaceSm_704ILR);
            acciones_704ILR.Controls.Add(_btnPersonal_704ILR, 0, 0);
            acciones_704ILR.Controls.Add(_btnCronograma_704ILR, 1, 0);
            acciones_704ILR.Controls.Add(_btnTareas_704ILR, 0, 1);
            acciones_704ILR.Controls.Add(_btnSupervision_704ILR, 1, 1);

            layout_704ILR.Controls.Add(_lblDetalleTitulo_704ILR, 0, 0);
            layout_704ILR.Controls.Add(datos_704ILR, 0, 1);
            layout_704ILR.Controls.Add(acciones_704ILR, 0, 2);
            card_704ILR.Controls.Add(layout_704ILR);
            return card_704ILR;
        }

        private static Label Bloque_704ILR() => new Label
        {
            AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR,
            BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR), UseMnemonic = false
        };

        private AppButton_704ILR Accion_704ILR(string texto_704ILR, string clave_704ILR, string glifo_704ILR, Action abrir_704ILR)
        {
            var b_704ILR = Ui_704ILR.Primary_704ILR(texto_704ILR, glifo_704ILR);
            b_704ILR.Tag = "T:" + clave_704ILR;
            b_704ILR.Dock = DockStyle.Fill;
            b_704ILR.Click += (s_704ILR, e_704ILR) => abrir_704ILR();
            return b_704ILR;
        }

        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            if (_grid_704ILR.Columns.Count >= 5)
            {
                _grid_704ILR.Columns["cFecha"].HeaderText    = T_704ILR("COL_FECHA", "Fecha");
                _grid_704ILR.Columns["cCliente"].HeaderText  = T_704ILR("COL_CLIENTE", "Cliente");
                _grid_704ILR.Columns["cSalon"].HeaderText    = T_704ILR("COL_SALON", "Salón");
                _grid_704ILR.Columns["cPersonal"].HeaderText = T_704ILR("OPE_COL_PERSONAL", "Personal");
                _grid_704ILR.Columns["cEstado"].HeaderText   = T_704ILR("OPE_COL_ESTADO", "Coordinación");
            }
            if (_textoError_704ILR != null && _lblError_704ILR.Visible) _lblError_704ILR.Text = _textoError_704ILR();
            // Los estados de la grilla y el detalle se escriben ya traducidos: se rehacen.
            PintarGrilla_704ILR(EventoSeleccionado_704ILR()?.ReservaId_704ILR ?? 0);
        }

        // Lee los eventos y deja seleccionado el indicado (0 = el primero). Una falla
        // se asienta, se informa y deja la grilla vacia.
        private void Cargar_704ILR(int seleccionar_704ILR)
        {
            try
            {
                _lblError_704ILR.Visible = false;
                _textoError_704ILR = null;
                _eventos_704ILR = BLL_Coordinacion_704ILR.GetEventos_704ILR();
                _cargaFallida_704ILR = false;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Coordinacion", "Cargar eventos");
                _eventos_704ILR = new List<BE_EventoCoordinacion_704ILR>();
                _cargaFallida_704ILR = true;
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
            PintarGrilla_704ILR(seleccionar_704ILR);
        }

        private void PintarGrilla_704ILR(int seleccionar_704ILR)
        {
            _pintando_704ILR = true;
            try
            {
                _grid_704ILR.Rows.Clear();
                foreach (var ev_704ILR in _eventos_704ILR)
                {
                    int i_704ILR = _grid_704ILR.Rows.Add(Coord_704ILR.Fecha_704ILR(ev_704ILR.FechaEvento_704ILR), ev_704ILR.ClienteNombre_704ILR,
                        ev_704ILR.SalonNombre_704ILR, ev_704ILR.Personal_704ILR, Coord_704ILR.Estado_704ILR(ev_704ILR.EstadoCoordinacion_704ILR));
                    _grid_704ILR.Rows[i_704ILR].Tag = ev_704ILR;
                    _grid_704ILR.Rows[i_704ILR].Cells["cEstado"].Style.ForeColor = Coord_704ILR.Color_704ILR(ev_704ILR.EstadoCoordinacion_704ILR);
                    if (ev_704ILR.ReservaId_704ILR == seleccionar_704ILR)
                        _grid_704ILR.CurrentCell = _grid_704ILR.Rows[i_704ILR].Cells[0];
                }
            }
            finally { _pintando_704ILR = false; }
            // Una lectura fallida no es "cero eventos": el contador queda vacio.
            _lblCount_704ILR.Text = _cargaFallida_704ILR ? string.Empty : _eventos_704ILR.Count + " " + T_704ILR("OPE_COUNT", "evento(s) confirmado(s)");
            MostrarDetalle_704ILR();
        }

        private BE_EventoCoordinacion_704ILR EventoSeleccionado_704ILR() => Coord_704ILR.Fila_704ILR(_grid_704ILR)?.Tag as BE_EventoCoordinacion_704ILR;

        // Detalle del evento seleccionado: lo que se contrato (de ahi sale cuanta gente
        // hace falta y de que especialidad) y cuanto se avanzo en la coordinacion.
        private void MostrarDetalle_704ILR()
        {
            if (_lblDatos_704ILR == null) return;
            BE_EventoCoordinacion_704ILR ev_704ILR = EventoSeleccionado_704ILR();
            HabilitarAcciones_704ILR(ev_704ILR);
            if (ev_704ILR == null)
            {
                _lblDetalleTitulo_704ILR.Text = T_704ILR("OPE_SIN_SELECCION", "Seleccione un evento");
                _lblDatos_704ILR.Text = _eventos_704ILR.Count == 0 && !_cargaFallida_704ILR
                    ? T_704ILR("OPE_SIN_EVENTOS", "No hay reservas confirmadas para coordinar. Un evento aparece acá cuando su reserva queda confirmada.")
                    : string.Empty;
                _lblServicios_704ILR.Text = _lblAvance_704ILR.Text = string.Empty;
                return;
            }

            _lblDetalleTitulo_704ILR.Text = T_704ILR("OPE_EVENTO", "Evento de la reserva") + " #" + ev_704ILR.ReservaId_704ILR;
            _lblDatos_704ILR.Text =
                T_704ILR("COL_FECHA", "Fecha") + ": " + Coord_704ILR.Fecha_704ILR(ev_704ILR.FechaEvento_704ILR) + Environment.NewLine +
                T_704ILR("COL_SALON", "Salón") + ": " + ev_704ILR.SalonNombre_704ILR + Environment.NewLine +
                T_704ILR("COL_CLIENTE", "Cliente") + ": " + ev_704ILR.ClienteNombre_704ILR + Environment.NewLine +
                T_704ILR("COL_INVITADOS", "Invitados") + ": " + ev_704ILR.CantidadInvitados_704ILR;

            var sb_704ILR = new StringBuilder(T_704ILR("OPE_SERVICIOS", "Servicios contratados") + ":");
            var servicios_704ILR = ServiciosDe_704ILR(ev_704ILR.ReservaId_704ILR);
            // Una lectura fallida no es "sin servicios": se dice que no se pudieron leer.
            if (_serviciosDe_704ILR != ev_704ILR.ReservaId_704ILR) sb_704ILR.Append(Environment.NewLine + "  " + T_704ILR("OPE_SERVICIOS_ERROR", "(no se pudieron leer)"));
            else if (servicios_704ILR.Count == 0) sb_704ILR.Append(Environment.NewLine + "  " + T_704ILR("OPE_SIN_SERVICIOS", "(sin servicios)"));
            foreach (var srv_704ILR in servicios_704ILR)
                sb_704ILR.Append(Environment.NewLine + "  " + srv_704ILR.Cantidad_704ILR + " x " + srv_704ILR.ServicioNombre_704ILR);
            _lblServicios_704ILR.Text = sb_704ILR.ToString();

            _lblAvance_704ILR.Text =
                T_704ILR("OPE_COL_ESTADO", "Coordinación") + ": " + Coord_704ILR.Estado_704ILR(ev_704ILR.EstadoCoordinacion_704ILR) + Environment.NewLine +
                Tr_704ILR.F_704ILR("OPE_AVANCE_PERSONAL", "Personal: {0} asignado(s), {1} confirmado(s), {2} pendiente(s), {3} rechazado(s)",
                    ev_704ILR.Asignados_704ILR, ev_704ILR.Confirmados_704ILR, ev_704ILR.Pendientes_704ILR, ev_704ILR.Rechazados_704ILR) + Environment.NewLine +
                (ev_704ILR.TieneCronograma_704ILR
                    ? Tr_704ILR.F_704ILR("OPE_AVANCE_CRONOGRAMA", "Cronograma: {0} actividad(es)", ev_704ILR.Actividades_704ILR)
                    : T_704ILR("OPE_AVANCE_SIN_CRONOGRAMA", "Cronograma: sin generar")) + Environment.NewLine +
                Tr_704ILR.F_704ILR("OPE_AVANCE_TAREAS", "Tareas: {0}", ev_704ILR.Tareas_704ILR) + Environment.NewLine +
                Tr_704ILR.F_704ILR("OPE_AVANCE_INCIDENCIAS", "Incidencias: {0} ({1} abierta(s))", ev_704ILR.Incidencias_704ILR, ev_704ILR.IncidenciasAbiertas_704ILR);
        }

        // Servicios contratados de la reserva. Se leen una vez por evento seleccionado
        // (el cambio de idioma vuelve a pintar el detalle sin volver a leerlos). Si la
        // lectura falla el detalle sigue sin la lista, y la falla queda asentada y a la vista.
        private List<BE_ReservaServicio_704ILR> ServiciosDe_704ILR(int reservaId_704ILR)
        {
            if (_serviciosDe_704ILR == reservaId_704ILR) return _servicios_704ILR;
            try
            {
                _servicios_704ILR = BLL_ReservaServicio_704ILR.GetByReserva_704ILR(reservaId_704ILR);
                _serviciosDe_704ILR = reservaId_704ILR;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Coordinacion", "Cargar servicios de la reserva #" + reservaId_704ILR);
                _servicios_704ILR = new List<BE_ReservaServicio_704ILR>();
                _serviciosDe_704ILR = 0;
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
            return _servicios_704ILR;
        }

        // Primera capa del control de acceso: cada accion se ofrece solo a quien tiene su
        // permiso (la segunda capa vuelve a exigirlo al abrir el dialogo y al operar).
        private void HabilitarAcciones_704ILR(BE_EventoCoordinacion_704ILR ev_704ILR)
        {
            bool hay_704ILR = ev_704ILR != null;
            _btnPersonal_704ILR.Enabled = hay_704ILR && Permisos_704ILR.Tiene_704ILR("PERSONAL_ASIGNAR");
            _btnCronograma_704ILR.Enabled = hay_704ILR && Permisos_704ILR.Tiene_704ILR("CRONOGRAMA_GESTION");
            _btnTareas_704ILR.Enabled = hay_704ILR && Permisos_704ILR.Tiene_704ILR("TAREAS_ASIGNAR");
            _btnSupervision_704ILR.Enabled = hay_704ILR && Permisos_704ILR.Tiene_704ILR("EJECUCION_SUPERVISAR");
        }

        private void AbrirPersonal_704ILR() =>
            Abrir_704ILR("PERSONAL_ASIGNAR", "abrir el personal de la reserva #{0}", id_704ILR => new frmAsignarPersonal_704ILR(id_704ILR));

        private void AbrirCronograma_704ILR() =>
            Abrir_704ILR("CRONOGRAMA_GESTION", "abrir el cronograma de la reserva #{0}", id_704ILR => new frmCronograma_704ILR(id_704ILR));

        private void AbrirTareas_704ILR() =>
            Abrir_704ILR("TAREAS_ASIGNAR", "abrir las tareas de la reserva #{0}", id_704ILR => new frmTareas_704ILR(id_704ILR));

        private void AbrirSupervision_704ILR() =>
            Abrir_704ILR("EJECUCION_SUPERVISAR", "abrir la supervision de la reserva #{0}", id_704ILR => new frmSupervision_704ILR(id_704ILR));

        // Abre el dialogo del evento seleccionado y, al cerrarlo, vuelve a leer los
        // eventos: lo que se hizo adentro cambia el avance y el estado de coordinacion.
        private void Abrir_704ILR(string permiso_704ILR, string accion_704ILR, Func<int, Form> crear_704ILR)
        {
            BE_EventoCoordinacion_704ILR ev_704ILR = EventoSeleccionado_704ILR();
            if (ev_704ILR == null) return;
            int id_704ILR = ev_704ILR.ReservaId_704ILR;
            if (!Permisos_704ILR.Exigir_704ILR(permiso_704ILR, FindForm(), string.Format(accion_704ILR, id_704ILR))) return;
            try
            {
                using (Form frm_704ILR = crear_704ILR(id_704ILR))
                    frm_704ILR.ShowDialog(FindForm());
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Coordinacion", string.Format(accion_704ILR, id_704ILR));
                MessageBox.Show(FindForm(), Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (IsDisposed) return;
            _serviciosDe_704ILR = 0;
            Cargar_704ILR(id_704ILR);
        }

        private void MostrarError_704ILR(Func<string> texto_704ILR)
        {
            _textoError_704ILR = texto_704ILR;
            _lblError_704ILR.Text = texto_704ILR();
            _lblError_704ILR.Visible = true;
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR) => Coord_704ILR.T_704ILR(clave_704ILR, defecto_704ILR);
    }
}
