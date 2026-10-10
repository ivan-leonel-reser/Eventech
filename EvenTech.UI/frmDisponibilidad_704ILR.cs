using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Consulta de disponibilidad (Proceso 1, paso 1). El vendedor carga la fecha
    // del evento y los invitados estimados; la grilla muestra que salones estan
    // libres, cuales no alcanzan en capacidad y, para los ocupados que si
    // alcanzan, la proxima fecha libre como propuesta alternativa. "Usar en la
    // reserva" devuelve salon + fecha para precargar la ficha.
    public class frmDisponibilidad_704ILR : FormBase_704ILR
    {
        private DateTimePicker _dtFecha_704ILR;
        private NumericUpDown _numCapacidad_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblResumen_704ILR;
        private AppButton_704ILR _btnUsar_704ILR;
        private List<BE_DisponibilidadSalon_704ILR> _resultado_704ILR = new List<BE_DisponibilidadSalon_704ILR>();

        // Criterios de la ultima consulta exitosa. La grilla y "Usar en la reserva"
        // valen solo para ellos: sin consulta vigente (fecha nula) no hay nada que
        // trasladar a la ficha.
        private DateTime? _fechaConsultada_704ILR;
        private int _invitadosConsultados_704ILR;

        // Seleccion confirmada con "Usar en la reserva" (valida si DialogResult = OK).
        public int SalonSeleccionado_704ILR { get; private set; }
        public DateTime FechaSeleccionada_704ILR { get; private set; }

        // Invitados con los que se hizo la consulta: vuelve a la ficha de la
        // reserva para que la cantidad estimada quede registrada en la operacion
        // (PN1: Cantidad_Invitados) y no se pierda al cerrar el dialogo. Es el valor
        // consultado, no el que muestre el campo en ese momento (CUN002, paso 5).
        public int InvitadosConsultados_704ILR => _invitadosConsultados_704ILR;

        public frmDisponibilidad_704ILR(DateTime fechaInicial_704ILR, int invitadosIniciales_704ILR = 0)
        {
            BuildUi_704ILR();
            _dtFecha_704ILR.Value = fechaInicial_704ILR < _dtFecha_704ILR.MinDate ? _dtFecha_704ILR.MinDate : fechaInicial_704ILR;
            if (invitadosIniciales_704ILR > 0 && invitadosIniciales_704ILR <= _numCapacidad_704ILR.Maximum)
                _numCapacidad_704ILR.Value = invitadosIniciales_704ILR;
            Consultar_704ILR();
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(760, 500);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblTitle_704ILR = new Label
            {
                Text = T_704ILR("DISP_TITULO", "Consulta de disponibilidad"),
                Font = Theme_704ILR.FontH2_704ILR, ForeColor = Theme_704ILR.TextOnDark_704ILR, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0), BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblTitle_704ILR);
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); }, danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // criterios
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grilla
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // footer

            // --- Fila de criterios: fecha + invitados + consultar ---
            var criterios_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };

            var lblFecha_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("RES_LBL_FECHA", "Fecha del evento"));
            lblFecha_704ILR.Margin = new Padding(0, 9, Theme_704ILR.SpaceXs_704ILR, 0);
            _dtFecha_704ILR = Ui_704ILR.DatePicker_704ILR();
            _dtFecha_704ILR.MinDate = DateTime.Today;
            _dtFecha_704ILR.Width = 140;
            _dtFecha_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);

            var lblCap_704ILR = Ui_704ILR.FieldLabel_704ILR(T_704ILR("DISP_LBL_CAPACIDAD", "Invitados estimados"));
            lblCap_704ILR.Margin = new Padding(0, 9, Theme_704ILR.SpaceXs_704ILR, 0);
            // Campo entero: lo que se ve es lo que se consulta y lo que vuelve a la ficha
            // (con un NumericUpDown comun "80,5" mostraba 81 y consultaba 80).
            _numCapacidad_704ILR = new CampoEntero_704ILR { Minimum = 0, Maximum = BLL_Reserva_704ILR.InvitadosMaximo_704ILR, Width = 90, Font = Theme_704ILR.FontInput_704ILR, Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0), TextAlign = HorizontalAlignment.Right };

            var btnConsultar_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("BTN_CONSULTAR", "Consultar"), Theme_704ILR.IcoSearch_704ILR);
            btnConsultar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            btnConsultar_704ILR.Size = new Size(130, 30);
            btnConsultar_704ILR.Click += (s_704ILR, e_704ILR) => Consultar_704ILR();

            criterios_704ILR.Controls.Add(lblFecha_704ILR);
            criterios_704ILR.Controls.Add(_dtFecha_704ILR);
            criterios_704ILR.Controls.Add(lblCap_704ILR);
            criterios_704ILR.Controls.Add(_numCapacidad_704ILR);
            criterios_704ILR.Controls.Add(btnConsultar_704ILR);

            // --- Grilla de salones ---
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSalon",     HeaderText = T_704ILR("COL_SALON", "Salón"), FillWeight = 80 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCapacidad", HeaderText = T_704ILR("COL_CAPACIDAD", "Capacidad"), FillWeight = 45, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstado",    HeaderText = T_704ILR("COL_ESTADO", "Estado"), FillWeight = 70 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPropuesta", HeaderText = T_704ILR("DISP_COL_PROPUESTA", "Próxima fecha libre"), FillWeight = 65 });
            _grid_704ILR.SelectionChanged += (s_704ILR, e_704ILR) => ActualizarBotonUsar_704ILR();
            _grid_704ILR.CellDoubleClick += (s_704ILR, e_704ILR) => { if (e_704ILR.RowIndex >= 0) Usar_704ILR(); };
            card_704ILR.Controls.Add(_grid_704ILR);

            // --- Footer: resumen + usar en la reserva ---
            var footer_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, BackColor = Color.Transparent };
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _lblResumen_704ILR = new Label { Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 0, 0), BackColor = Color.Transparent, MaximumSize = new Size(520, 0) };
            _btnUsar_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("DISP_USAR", "Usar en la reserva"), Theme_704ILR.IcoSave_704ILR);
            _btnUsar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnUsar_704ILR.Size = new Size(190, 38);
            _btnUsar_704ILR.Anchor = AnchorStyles.Right;
            _btnUsar_704ILR.Enabled = false;   // hasta que una consulta deje un salon elegible
            _btnUsar_704ILR.Click += (s_704ILR, e_704ILR) => Usar_704ILR();
            footer_704ILR.Controls.Add(_lblResumen_704ILR, 0, 0);
            footer_704ILR.Controls.Add(_btnUsar_704ILR, 1, 0);

            root_704ILR.Controls.Add(criterios_704ILR, 0, 0);
            root_704ILR.Controls.Add(card_704ILR, 0, 1);
            root_704ILR.Controls.Add(footer_704ILR, 0, 2);

            Controls.Add(root_704ILR);
            Controls.Add(pnlTitle_704ILR);
            AcceptButton = btnConsultar_704ILR;

            // Cambiar un criterio deja sin efecto el resultado en pantalla: la grilla
            // se vacia y "Usar en la reserva" queda deshabilitado hasta volver a
            // consultar (no se reconsulta sola: cada consulta se asienta en bitacora).
            _dtFecha_704ILR.ValueChanged += (s_704ILR, e_704ILR) => InvalidarConsulta_704ILR();
            _numCapacidad_704ILR.ValueChanged += (s_704ILR, e_704ILR) => InvalidarConsulta_704ILR();
        }

        private void Consultar_704ILR()
        {
            // Los criterios se leen antes de consultar. Leer el campo de invitados
            // confirma lo tipeado y, si cambio, ya invalida el resultado anterior.
            DateTime fecha_704ILR = _dtFecha_704ILR.Value.Date;
            int invitados_704ILR = (int)_numCapacidad_704ILR.Value;
            try
            {
                _resultado_704ILR = BLL_Disponibilidad_704ILR.Consultar_704ILR(fecha_704ILR, invitados_704ILR);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Consultar disponibilidad");
                // Lo que mostraba la grilla es de una consulta anterior: no puede quedar
                // a la vista ni usarse como si respondiera a la que fallo.
                InvalidarConsulta_704ILR();
                _lblResumen_704ILR.ForeColor = Theme_704ILR.Error_704ILR;
                // Mensaje generico de operacion: aca no se estaba guardando ninguna
                // reserva, asi que "No se pudo guardar la reserva" no correspondia.
                _lblResumen_704ILR.Text = T_704ILR("MSG_OP_ERROR", "No se pudo completar la operación.");
                return;
            }

            _fechaConsultada_704ILR = fecha_704ILR;
            _invitadosConsultados_704ILR = invitados_704ILR;
            _grid_704ILR.Rows.Clear();
            foreach (var d_704ILR in _resultado_704ILR)
            {
                string estado_704ILR = d_704ILR.Disponible_704ILR
                    ? T_704ILR("DISP_EST_DISPONIBLE", "Disponible")
                    : !d_704ILR.CapacidadSuficiente_704ILR
                        ? T_704ILR("DISP_EST_CAPACIDAD", "Capacidad insuficiente")
                        : T_704ILR("DISP_EST_OCUPADO", "Ocupado");
                // Fecha propuesta en gregoriano con separadores invariantes, como las fechas de las
                // grillas y de la bitacora: con la cultura de la estacion th-TH mostraba 2569.
                string propuesta_704ILR = PropuestaUtilizable_704ILR(d_704ILR) ? d_704ILR.ProximaFechaLibre_704ILR.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";

                int i_704ILR = _grid_704ILR.Rows.Add(d_704ILR.SalonNombre_704ILR, d_704ILR.Capacidad_704ILR, estado_704ILR, propuesta_704ILR);
                _grid_704ILR.Rows[i_704ILR].Tag = d_704ILR;
                _grid_704ILR.Rows[i_704ILR].Cells["cEstado"].Style.ForeColor =
                    d_704ILR.Disponible_704ILR ? Theme_704ILR.Success_704ILR : !d_704ILR.CapacidadSuficiente_704ILR ? Theme_704ILR.TextMuted_704ILR : Theme_704ILR.Error_704ILR;
            }

            int disponibles_704ILR = _resultado_704ILR.Count(d_704ILR => d_704ILR.Disponible_704ILR);
            if (disponibles_704ILR > 0)
            {
                _lblResumen_704ILR.ForeColor = Theme_704ILR.Success_704ILR;
                _lblResumen_704ILR.Text = Tr_704ILR.F_704ILR("DISP_RESUMEN_OK", "{0} salón(es) disponible(s) para la fecha consultada.", disponibles_704ILR);
            }
            else
            {
                _lblResumen_704ILR.ForeColor = Theme_704ILR.Warning_704ILR;
                _lblResumen_704ILR.Text = ResumenSinDisponibles_704ILR(invitados_704ILR);
            }
            ActualizarBotonUsar_704ILR();
            // El asiento en bitacora de la consulta lo hace la capa de negocio
            // (BLL_Disponibilidad.Consultar), no el dialogo.
        }

        // "Usar" toma el salon seleccionado: si esta disponible usa la fecha
        // consultada; si esta ocupado pero tiene propuesta, usa la fecha
        // alternativa (asi se concreta el "ofrecer otras propuestas" del proceso).
        private void Usar_704ILR()
        {
            // Leer el campo confirma lo tipeado. Si los criterios en pantalla ya no son
            // los de la consulta vigente, el resultado no vale: se descarta y no se
            // traslada nada. Cubre tambien el doble clic sobre la fila, que no pasa
            // por el estado del boton.
            int invitados_704ILR = (int)_numCapacidad_704ILR.Value;
            if (!_fechaConsultada_704ILR.HasValue || _dtFecha_704ILR.Value.Date != _fechaConsultada_704ILR.Value
                || invitados_704ILR != _invitadosConsultados_704ILR)
            {
                InvalidarConsulta_704ILR();
                return;
            }
            if (!(_grid_704ILR.CurrentRow?.Tag is BE_DisponibilidadSalon_704ILR d_704ILR))
            {
                Aviso_704ILR(T_704ILR("DISP_SELECCIONE", "Seleccione un salón de la grilla."));
                return;
            }
            if (!d_704ILR.CapacidadSuficiente_704ILR)
            {
                Aviso_704ILR(T_704ILR("DISP_EST_CAPACIDAD", "Capacidad insuficiente"));
                return;
            }
            if (!d_704ILR.Disponible_704ILR && !PropuestaUtilizable_704ILR(d_704ILR))
            {
                Aviso_704ILR(T_704ILR("DISP_SIN_PROPUESTA", "El salón no tiene fechas libres en el horizonte consultado."));
                return;
            }

            SalonSeleccionado_704ILR = d_704ILR.SalonId_704ILR;
            FechaSeleccionada_704ILR = d_704ILR.Disponible_704ILR ? d_704ILR.FechaConsultada_704ILR : d_704ILR.ProximaFechaLibre_704ILR.Value;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ActualizarBotonUsar_704ILR()
        {
            _btnUsar_704ILR.Enabled = _fechaConsultada_704ILR.HasValue &&
                               _grid_704ILR.CurrentRow?.Tag is BE_DisponibilidadSalon_704ILR d_704ILR &&
                               d_704ILR.CapacidadSuficiente_704ILR && (d_704ILR.Disponible_704ILR || PropuestaUtilizable_704ILR(d_704ILR));
        }

        // Deja el dialogo sin consulta vigente: lo que mostraba la grilla ya no
        // responde a los criterios en pantalla, porque se cambiaron o porque la
        // consulta fallo. Sin resultado ni "Usar" hasta volver a consultar.
        private void InvalidarConsulta_704ILR()
        {
            _fechaConsultada_704ILR = null;
            _invitadosConsultados_704ILR = 0;
            _resultado_704ILR = new List<BE_DisponibilidadSalon_704ILR>();
            _grid_704ILR.Rows.Clear();
            _lblResumen_704ILR.Text = string.Empty;
            ActualizarBotonUsar_704ILR();
        }

        // Una propuesta alternativa solo sirve si la ficha puede cargarla: una fecha
        // posterior al ultimo dia del calendario se trata como "sin propuesta"
        // (flujo 4.1 del CUN002) en vez de ofrecerse y fallar al usarla.
        private bool PropuestaUtilizable_704ILR(BE_DisponibilidadSalon_704ILR d_704ILR) =>
            d_704ILR.ProximaFechaLibre_704ILR.HasValue && d_704ILR.ProximaFechaLibre_704ILR.Value.Date <= _dtFecha_704ILR.MaxDate.Date;

        // Resumen cuando ningun salon esta disponible tal cual se pidio. Solo se
        // anuncian fechas alternativas si la grilla muestra al menos una; si no, se
        // informa por que no hay ninguna (CUN002, paso 4 y flujo 4.1).
        private string ResumenSinDisponibles_704ILR(int invitados_704ILR)
        {
            if (_resultado_704ILR.Count == 0)
                return T_704ILR("DISP_RESUMEN_SIN_SALONES", "No hay salones registrados.");
            if (_resultado_704ILR.Any(d_704ILR => d_704ILR.CapacidadSuficiente_704ILR && PropuestaUtilizable_704ILR(d_704ILR)))
                return T_704ILR("DISP_RESUMEN_ALTERNATIVAS", "Ningún salón disponible para esa fecha: se proponen fechas alternativas.");
            if (!_resultado_704ILR.Any(d_704ILR => d_704ILR.CapacidadSuficiente_704ILR))
                return Tr_704ILR.F_704ILR("DISP_RESUMEN_SIN_CAPACIDAD", "Ningún salón tiene capacidad para {0} invitados.", invitados_704ILR);
            return Tr_704ILR.F_704ILR("DISP_RESUMEN_SIN_FECHAS", "Ningún salón con capacidad suficiente tiene fechas libres en los {0} días siguientes.",
                BLL_Disponibilidad_704ILR.HorizontePropuestasDias_704ILR);
        }

        private void Aviso_704ILR(string msg_704ILR) =>
            MessageBox.Show(this, msg_704ILR, "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }
}
