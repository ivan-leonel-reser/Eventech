using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Dialogo para registrar/anular pagos de una reserva (Proceso 1, paso 5).
    // A diferencia de los servicios, los pagos persisten en el acto: cada alta o
    // baja impacta la base y se recalcula el saldo (tope = total de la reserva).
    public class frmReservaPagos_704ILR : FormBase_704ILR
    {
        private readonly int _reservaId_704ILR;
        // Total de la reserva segun la ultima lectura. Llega al abrir, pero no queda fijo:
        // cada refresco lo vuelve a leer de la base (ver RefrescarPagos_704ILR), porque
        // otra sesion puede cambiar los servicios con el dialogo abierto.
        private decimal _montoReserva_704ILR;
        private DataGridView _grid_704ILR;
        private ComboBox _cboMetodo_704ILR;
        private NumericUpDown _numMonto_704ILR;
        private TextBox _txtObs_704ILR;
        private Label _lblResumen_704ILR;
        // Proteccion del doble clic en Registrar (ver Registrar_704ILR): un intento en
        // curso y el instante en que termino el ultimo cobro registrado, mientras no se
        // toque la fila de alta (null si no hubo cobro o si despues se cambio algo).
        private bool _registrando_704ILR;
        private long? _finCobro_704ILR;

        public frmReservaPagos_704ILR(int reservaId_704ILR, decimal montoReserva_704ILR)
        {
            _reservaId_704ILR = reservaId_704ILR;
            _montoReserva_704ILR = montoReserva_704ILR;
            BuildUi_704ILR();
            Refrescar_704ILR();
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            // Ancho holgado: el resumen del pie muestra Total, Pagado y Saldo en una
            // sola linea, y los importes del dominio (catering por invitado) son largos.
            ClientSize = new Size(880, 500);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblTitle_704ILR = new Label
            {
                Text = T_704ILR("RES_PAGOS", "Pagos de la reserva"),
                Font = Theme_704ILR.FontH2_704ILR, ForeColor = Theme_704ILR.TextOnDark_704ILR, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0), BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblTitle_704ILR);
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => { DialogResult = DialogResult.OK; Close(); }, danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // alta
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grilla
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // footer

            // --- Fila de alta: metodo + monto + observacion + registrar/quitar ---
            var alta_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };
            _cboMetodo_704ILR = Ui_704ILR.Combo_704ILR(); _cboMetodo_704ILR.Width = 170; _cboMetodo_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            // El item sigue siendo la entidad (Registrar lee su Id); lo que cambia es el
            // texto que se muestra, que sale traducido (ver TextoMetodo_704ILR). El
            // formato se engancha antes de cargar los items para que ya entren con el.
            _cboMetodo_704ILR.FormattingEnabled = true;
            _cboMetodo_704ILR.Format += (s_704ILR, e_704ILR) =>
            {
                if (e_704ILR.ListItem is BE_MetodoPago_704ILR mp_704ILR) e_704ILR.Value = TextoMetodo_704ILR(mp_704ILR.Nombre_704ILR);
            };
            foreach (var m_704ILR in BLL_Pago_704ILR.GetMetodos_704ILR()) _cboMetodo_704ILR.Items.Add(m_704ILR);
            if (_cboMetodo_704ILR.Items.Count > 0) _cboMetodo_704ILR.SelectedIndex = 0;
            // El maximo del campo es lo que admite Pagos.Monto (DECIMAL(12,2)), el mismo tope
            // que el total de una reserva: un importe mayor no se ajusta al maximo en silencio,
            // se rechaza como monto invalido (ver CampoImporte_704ILR). El ancho alcanza para
            // ver entero el importe maximo ("9999999999,99").
            _numMonto_704ILR = new CampoImporte_704ILR { Minimum = 0, Maximum = BLL_Reserva_704ILR.MontoMaximo_704ILR, DecimalPlaces = 2, Increment = 1000, Width = 135, Font = Theme_704ILR.FontInput_704ILR, Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0), TextAlign = HorizontalAlignment.Right };
            // La observacion es el unico campo de la fila sin rotulo (la fila es
            // horizontal y no hay lugar para uno): el texto de ejemplo le da nombre en
            // pantalla, que es el que cita el CUN005. MaxLength = ancho real de
            // Pagos.Observacion: sin el, un texto mas largo se guardaria recortado.
            _txtObs_704ILR = Ui_704ILR.Input_704ILR(); _txtObs_704ILR.Width = 150; _txtObs_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _txtObs_704ILR.MaxLength = 200;
            _txtObs_704ILR.PlaceholderText = T_704ILR("COL_OBSERVACION", "Observación");
            // Lo que se cambie en el monto o en la observacion despues de un cobro ya no es
            // el segundo clic de ese cobro: se valida como cualquier intento (ver
            // Registrar_704ILR). El propio cobro vacia la fila antes de dejar la marca.
            _numMonto_704ILR.TextChanged += (s_704ILR, e_704ILR) => _finCobro_704ILR = null;
            _txtObs_704ILR.TextChanged += (s_704ILR, e_704ILR) => _finCobro_704ILR = null;
            var btnRegistrar_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("BTN_REGISTRAR", "Registrar"), Theme_704ILR.IcoAdd_704ILR); btnRegistrar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnRegistrar_704ILR.Size = new Size(130, 30); btnRegistrar_704ILR.Click += (s_704ILR, e_704ILR) => Registrar_704ILR();
            var btnQuitar_704ILR = Ui_704ILR.Secondary_704ILR(T_704ILR("BTN_QUITAR", "Quitar"), Theme_704ILR.IcoClear_704ILR); btnQuitar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnQuitar_704ILR.Size = new Size(100, 30); btnQuitar_704ILR.Margin = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, 0, 0); btnQuitar_704ILR.Click += (s_704ILR, e_704ILR) => Quitar_704ILR();
            // Los anchos de diseno alcanzan para los rotulos en espanol; un idioma con
            // rotulos mas largos ensancha el boton en vez de recortar el texto.
            AjustarAncho_704ILR(btnRegistrar_704ILR, 130);
            AjustarAncho_704ILR(btnQuitar_704ILR, 100);
            // Anular un pago es una operacion sensible: se oculta a quien no la tiene.
            btnQuitar_704ILR.Visible = Permisos_704ILR.Tiene_704ILR("PAGOS_ANULAR");
            // Primera capa del cobro, con el mismo criterio que la ficha de reservas: a
            // un perfil que entra solo para anular no se le ofrece la fila de alta (cada
            // clic terminaba en "sin permiso" y en un asiento de Acceso denegado). Se
            // deshabilita y no se oculta, para que Quitar no se corra de lugar. La
            // segunda capa (Exigir en RegistrarPago_704ILR) se mantiene.
            bool puedeRegistrar_704ILR = Permisos_704ILR.Tiene_704ILR("PAGOS_REGISTRAR");
            _cboMetodo_704ILR.Enabled = puedeRegistrar_704ILR;
            _numMonto_704ILR.Enabled = puedeRegistrar_704ILR;
            _txtObs_704ILR.Enabled = puedeRegistrar_704ILR;
            btnRegistrar_704ILR.Enabled = puedeRegistrar_704ILR;
            alta_704ILR.Controls.Add(_cboMetodo_704ILR); alta_704ILR.Controls.Add(_numMonto_704ILR); alta_704ILR.Controls.Add(_txtObs_704ILR); alta_704ILR.Controls.Add(btnRegistrar_704ILR); alta_704ILR.Controls.Add(btnQuitar_704ILR);

            // --- Grilla ---
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFecha", HeaderText = T_704ILR("COL_FECHA", "Fecha"), FillWeight = 55 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMetodo", HeaderText = T_704ILR("COL_METODO", "Método"), FillWeight = 60 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMonto", HeaderText = T_704ILR("COL_MONTO", "Monto"), FillWeight = 50, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cObs", HeaderText = T_704ILR("COL_OBSERVACION", "Observación"), FillWeight = 90 });
            card_704ILR.Controls.Add(_grid_704ILR);

            // --- Footer: resumen (total/pagado/saldo) + cerrar ---
            var footer_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, BackColor = Color.Transparent };
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _lblResumen_704ILR = new Label { Font = Theme_704ILR.FontH2_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 0, 0), BackColor = Color.Transparent };
            // Cerrar lleva el glifo de cerrar: el de guardar sugeria que el dialogo
            // confirma cambios pendientes, y aca cada pago ya persistio al registrarse.
            var btnCerrar_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("BTN_CERRAR", "Cerrar"), Theme_704ILR.IcoClose_704ILR); btnCerrar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnCerrar_704ILR.Size = new Size(130, 38); btnCerrar_704ILR.Anchor = AnchorStyles.Right;
            AjustarAncho_704ILR(btnCerrar_704ILR, 130);
            btnCerrar_704ILR.Click += (s_704ILR, e_704ILR) => { DialogResult = DialogResult.OK; Close(); };
            footer_704ILR.Controls.Add(_lblResumen_704ILR, 0, 0);
            footer_704ILR.Controls.Add(btnCerrar_704ILR, 1, 0);

            root_704ILR.Controls.Add(alta_704ILR, 0, 0);
            root_704ILR.Controls.Add(card_704ILR, 0, 1);
            root_704ILR.Controls.Add(footer_704ILR, 0, 2);

            Controls.Add(root_704ILR);
            Controls.Add(pnlTitle_704ILR);
            // Enter ejecuta la accion de la fila de alta, que es la del dialogo: con el
            // foco en el monto o en la observacion registra el cobro tipeado. Antes el
            // boton por defecto era Cerrar y Enter cerraba el dialogo descartando sin
            // aviso el importe que el usuario creia cobrado (despues la reserva no se
            // podia confirmar por RN-07). A un perfil sin PAGOS_REGISTRAR el boton le
            // queda deshabilitado y Enter no hace nada. Cerrar sigue con clic.
            AcceptButton = btnRegistrar_704ILR;
        }

        // Enter mantenido. Mientras la tecla sigue apretada Windows repite el WM_KEYDOWN con el
        // bit 30 del lParam en 1, y cada repeticion pulsaba el boton por defecto o el que tenia el
        // foco: despues de un cobro Registrar volvia a validar la caja vacia ("Ingrese un monto
        // valido.") o repetia un rechazo por saldo con un asiento por repeticion, y despues de una
        // anulacion Quitar volvia a pedir confirmacion sobre el pago siguiente. En este dialogo
        // actua solo la primera pulsacion: las repeticiones de Enter se descartan antes de llegar a
        // cualquier control. Soltar y volver a apretar actua como siempre (importe, observacion o
        // boton con el foco), y la guarda de doble clic de Registrar queda para el mouse. Override
        // del framework (sin sufijo, REGLA 4).
        private const int WM_KEYDOWN_704ILR = 0x0100;
        private const long BitRepeticion_704ILR = 0x40000000;

        protected override bool ProcessCmdKey(ref Message msg_704ILR, Keys keyData_704ILR)
        {
            if (msg_704ILR.Msg == WM_KEYDOWN_704ILR && (keyData_704ILR & Keys.KeyCode) == Keys.Enter &&
                (msg_704ILR.LParam.ToInt64() & BitRepeticion_704ILR) != 0)
                return true;
            return base.ProcessCmdKey(ref msg_704ILR, keyData_704ILR);
        }

        // Los tres handlers del dialogo tocan la base (alta, baja y lectura de pagos).
        // Se envuelven para que una falla se asiente en la bitacora y se informe, en vez
        // de terminar la aplicacion con el dialogo abierto.
        //
        // Registrar, ademas, no admite un segundo intento por un doble clic (o por Enter
        // pulsado dos veces). El segundo clic llega cuando el primero ya termino: se
        // despacha enseguida si quedo encolado mientras se cobraba, o dentro del intervalo
        // de doble clic del sistema. Encontraba la caja vaciada por el cobro y avisaba
        // "Ingrese un monto valido.", como si el cobro hubiera fallado, y el usuario podia
        // volver a tipear el importe y cobrarlo dos veces. Mientras nadie toque el monto ni
        // la observacion que dejo el cobro, ese clic se ignora sin aviso; lo que se tipee o
        // pegue despues se valida y se registra como siempre (un importe ilegible sigue
        // dando su aviso). La bandera corta la reentrada: un clic encolado puede
        // despacharse dentro del aviso que abre el propio intento. No se deshabilita el
        // boton durante el cobro: si tiene el foco, WinForms lo pasa a Quitar y el Enter
        // siguiente pulsaria Quitar.
        private void Registrar_704ILR()
        {
            if (_registrando_704ILR || SegundoClicTrasCobro_704ILR()) return;
            _registrando_704ILR = true;
            try { RegistrarPago_704ILR(); }
            catch (Exception ex_704ILR) { Fallo_704ILR(ex_704ILR, "Registrar pago en la reserva #" + _reservaId_704ILR); }
            finally { _registrando_704ILR = false; }
        }

        // Hubo un cobro, la fila de alta no se toco desde entonces (cualquier cambio del
        // monto o de la observacion borra la marca, ver BuildUi_704ILR) y todavia no paso
        // el intervalo de doble clic desde que termino.
        private bool SegundoClicTrasCobro_704ILR() =>
            _finCobro_704ILR.HasValue &&
            Environment.TickCount64 - _finCobro_704ILR.Value < SystemInformation.DoubleClickTime;

        private void Quitar_704ILR()
        {
            try { QuitarPago_704ILR(); }
            catch (Exception ex_704ILR) { Fallo_704ILR(ex_704ILR, "Anular pago de la reserva #" + _reservaId_704ILR); }
        }

        private void Refrescar_704ILR()
        {
            try { RefrescarPagos_704ILR(); }
            catch (Exception ex_704ILR) { Fallo_704ILR(ex_704ILR, "Cargar pagos de la reserva #" + _reservaId_704ILR); }
        }

        // Resultado de un cobro o de una anulacion que puede venir de un cambio hecho desde
        // otra sesion. La grilla y el resumen se releen ANTES del aviso: si se releian
        // despues, mientras el usuario leia "El pago supera el saldo pendiente." detras
        // seguian los pagos y el saldo viejos, que lo contradecian. Si la relectura falla,
        // su aviso va despues del rechazo, que es la respuesta a lo que pidio el usuario.
        // Sin aviso (anulacion registrada) es un refresco comun.
        private void RefrescarYAvisar_704ILR(string aviso_704ILR)
        {
            Exception falloLectura_704ILR = null;
            try { RefrescarPagos_704ILR(); }
            catch (Exception ex_704ILR) { falloLectura_704ILR = ex_704ILR; }
            if (aviso_704ILR != null) Aviso_704ILR(aviso_704ILR);
            if (falloLectura_704ILR != null) Fallo_704ILR(falloLectura_704ILR, "Cargar pagos de la reserva #" + _reservaId_704ILR);
        }

        private void Fallo_704ILR(Exception ex_704ILR, string contexto_704ILR)
        {
            BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Pagos", contexto_704ILR);
            Aviso_704ILR(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
        }

        private void RegistrarPago_704ILR()
        {
            // Registrar un cobro mueve el saldo de la reserva: es una escritura y
            // exige su permiso, igual que la anulacion.
            if (!Permisos_704ILR.Exigir_704ILR("PAGOS_REGISTRAR", this, "registrar un pago en la reserva #" + _reservaId_704ILR)) return;
            if (!(_cboMetodo_704ILR.SelectedItem is BE_MetodoPago_704ILR m_704ILR)) return;
            var pago_704ILR = new BE_Pago_704ILR
            {
                ReservaId_704ILR = _reservaId_704ILR,
                MetodoPagoId_704ILR = m_704ILR.Id_704ILR,
                Monto_704ILR = _numMonto_704ILR.Value,
                Observacion_704ILR = string.IsNullOrWhiteSpace(_txtObs_704ILR.Text) ? null : _txtObs_704ILR.Text.Trim()
            };
            int id_704ILR;
            var res_704ILR = BLL_Pago_704ILR.Registrar_704ILR(pago_704ILR, out id_704ILR);
            switch (res_704ILR)
            {
                // Errores de lo tipeado: la base no cambio, se conserva la fila elegida.
                case PagoResult_704ILR.MontoInvalido_704ILR:
                    Aviso_704ILR(T_704ILR("MSG_PAGO_MONTO", "Ingrese un monto válido.")); return;
                case PagoResult_704ILR.MetodoInvalido_704ILR:
                    Aviso_704ILR(T_704ILR("MSG_PAGO_METODO", "Seleccione un método de pago.")); return;
                // Estos tres rechazos pueden venir de un cambio hecho desde otra sesion (otro
                // cobro, un total distinto, la reserva cancelada o con un estado ajeno): se
                // vuelven a leer los pagos y el total antes del aviso, con el mismo criterio
                // que la anulacion, para que el aviso coincida con la grilla y el resumen que
                // se ven detras. Lo tipeado se conserva.
                case PagoResult_704ILR.ExcedeSaldo_704ILR:
                    RefrescarYAvisar_704ILR(T_704ILR("MSG_PAGO_EXCEDE", "El pago supera el saldo pendiente.")); return;
                case PagoResult_704ILR.ReservaInvalida_704ILR:
                    RefrescarYAvisar_704ILR(T_704ILR("MSG_PAGO_RESERVA", "Reserva inválida.")); return;
                case PagoResult_704ILR.ReservaCancelada_704ILR:
                    RefrescarYAvisar_704ILR(T_704ILR("MSG_RES_NO_MODIFICABLE", "La reserva está cancelada: no admite modificaciones.")); return;
            }
            _numMonto_704ILR.Value = 0;
            // El cero queda seleccionado. Con Enter el foco sigue en el monto y el cursor
            // quedaba delante del cero: el importe siguiente se escribia delante ("7000"
            // quedaba "70000,00") y se cobraba otro importe. Asi lo tipeado lo reemplaza.
            _numMonto_704ILR.Select(0, _numMonto_704ILR.Text.Length);
            _txtObs_704ILR.Clear();
            Refrescar_704ILR();
            _finCobro_704ILR = Environment.TickCount64;
        }

        // Anulacion de un pago. Es destructiva e irreversible (no hay versionado de
        // pagos como si lo hay de reservas), asi que exige permiso propio y una
        // confirmacion explicita que nombra el importe que se va a anular.
        private void QuitarPago_704ILR()
        {
            if (_grid_704ILR.CurrentRow == null) return;
            if (!(_grid_704ILR.CurrentRow.Tag is int pagoId_704ILR)) return;

            if (!Permisos_704ILR.Exigir_704ILR("PAGOS_ANULAR", this, "anular el pago #" + pagoId_704ILR + " de la reserva #" + _reservaId_704ILR))
                return;

            decimal monto_704ILR = _grid_704ILR.CurrentRow.Cells["cMonto"].Value is decimal m_704ILR ? m_704ILR : 0m;
            // No por defecto, como las demas preguntas destructivas o de descarte: con Si por
            // defecto, el Enter que el usuario mantenia apretado desde Quitar (o con el que
            // confirmaba) aceptaba la anulacion sin una eleccion explicita.
            var confirma_704ILR = MessageBox.Show(this,
                Tr_704ILR.F_704ILR("MSG_PAGO_ANULAR_CONF",
                    "¿Anular el pago de {0}? La operación no se puede deshacer.", monto_704ILR.ToString("N2")),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirma_704ILR != DialogResult.Yes) return;

            var anul_704ILR = BLL_Pago_704ILR.Eliminar_704ILR(pagoId_704ILR, _reservaId_704ILR);
            // Todo rechazo vuelve a leer los pagos, antes del aviso: el motivo puede ser un
            // cambio hecho desde otra sesion (otro pago anulado, la reserva cancelada) y el
            // aviso tiene que coincidir con lo que muestran la grilla y el resumen.
            string aviso_704ILR = null;
            switch (anul_704ILR)
            {
                case PagoResult_704ILR.ReservaCancelada_704ILR:
                    aviso_704ILR = T_704ILR("MSG_RES_NO_MODIFICABLE", "La reserva está cancelada: no admite modificaciones.");
                    break;
                case PagoResult_704ILR.ConfirmadaSinAdelanto_704ILR:
                    // El codigo de la regla queda en el asiento de bitacora, no en el aviso.
                    aviso_704ILR = T_704ILR("MSG_PAGO_ANULAR_SIN_ADELANTO",
                        "La reserva está confirmada: anular este pago la dejaría sin adelanto. Registre primero el pago que lo reemplaza o cancele la reserva.");
                    break;
                case PagoResult_704ILR.PagoInvalido_704ILR:
                    aviso_704ILR = T_704ILR("MSG_PAGO_NO_ANULABLE", "El pago ya no existe o no pertenece a esta reserva.");
                    break;
                // La reserva no admite movimientos (estado almacenado fuera del ciclo de
                // vida): el pago existe, asi que no se dice que ya no existe.
                case PagoResult_704ILR.ReservaInvalida_704ILR:
                    aviso_704ILR = T_704ILR("MSG_PAGO_RESERVA", "Reserva inválida.");
                    break;
            }
            RefrescarYAvisar_704ILR(aviso_704ILR);
        }

        private void RefrescarPagos_704ILR()
        {
            _grid_704ILR.Rows.Clear();
            var pagos_704ILR = BLL_Pago_704ILR.GetByReserva_704ILR(_reservaId_704ILR);
            foreach (var p_704ILR in pagos_704ILR)
            {
                // Fecha con patron fijo en calendario gregoriano y con separadores fijos, como las
                // demas fechas de la aplicacion: con la cultura del equipo el anio salia en otro
                // calendario (2569 en th-TH) y la hora con otro separador (01.15 en fi-FI). El
                // importe sigue con el formato de la cultura.
                int i_704ILR = _grid_704ILR.Rows.Add(p_704ILR.Fecha_704ILR.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), TextoMetodo_704ILR(p_704ILR.MetodoNombre_704ILR), p_704ILR.Monto_704ILR, p_704ILR.Observacion_704ILR ?? "");
                _grid_704ILR.Rows[i_704ILR].Tag = p_704ILR.Id_704ILR;
            }
            // El total se relee en cada refresco, como los pagos: el que se recibia al abrir
            // quedaba fijo y, si otra sesion cambiaba los servicios con el dialogo abierto, el
            // resumen mostraba un saldo negativo o un aviso de saldo que contradecia la pantalla.
            _montoReserva_704ILR = BLL_Pago_704ILR.MontoReserva_704ILR(_reservaId_704ILR);
            decimal pagado_704ILR = pagos_704ILR.Sum(p_704ILR => p_704ILR.Monto_704ILR);
            decimal saldo_704ILR = _montoReserva_704ILR - pagado_704ILR;
            _lblResumen_704ILR.Text =
                T_704ILR("LBL_TOTAL", "Total") + ": " + _montoReserva_704ILR.ToString("N2") + "    " +
                T_704ILR("LBL_PAGADO", "Pagado") + ": " + pagado_704ILR.ToString("N2") + "    " +
                T_704ILR("LBL_SALDO", "Saldo") + ": " + saldo_704ILR.ToString("N2");
            _lblResumen_704ILR.ForeColor = saldo_704ILR <= 0 ? Theme_704ILR.Success_704ILR : Theme_704ILR.TextOnLight_704ILR;
        }

        private void Aviso_704ILR(string msg_704ILR) =>
            MessageBox.Show(msg_704ILR, "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        // Nombre visible de un metodo de pago. El catalogo MetodosPago guarda el nombre
        // en espanol y la pantalla lo muestra por la clave de traduccion MP_<NOMBRE>
        // (mayusculas, sin tildes, lo que no es letra ni digito pasa a '_'): asi el
        // nombre con o sin tilde da la misma clave. Un metodo sin clave sembrada se
        // muestra con su nombre tal cual. Interno para que el comprobante pueda usar
        // el mismo texto.
        internal static string TextoMetodo_704ILR(string nombre_704ILR)
        {
            if (string.IsNullOrWhiteSpace(nombre_704ILR)) return nombre_704ILR ?? string.Empty;
            var clave_704ILR = new StringBuilder("MP_");
            bool separador_704ILR = false;
            foreach (char c_704ILR in nombre_704ILR.Trim().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c_704ILR) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c_704ILR)) { clave_704ILR.Append(char.ToUpperInvariant(c_704ILR)); separador_704ILR = false; }
                else if (!separador_704ILR) { clave_704ILR.Append('_'); separador_704ILR = true; }
            }
            string texto_704ILR = clave_704ILR.ToString().TrimEnd('_');
            if (texto_704ILR.Length > 60) texto_704ILR = texto_704ILR.Substring(0, 60);   // Traducciones.Clave es NVARCHAR(60)
            return T_704ILR(texto_704ILR, nombre_704ILR);
        }

        // Ancho de un boton segun su texto traducido, sin bajar del ancho de diseno.
        // AppButton reserva 30 px para el glifo y centra el texto en el resto con
        // elipsis: con los anchos fijos pensados para el espanol, "Add payment" (EN) y
        // "Remover" (PT) quedaban cortados por un pixel. Se mide con la misma fuente y
        // los mismos flags con que el boton dibuja el texto, mas un margen derecho igual
        // al izquierdo del glifo. El ancho tiene tope (1,5 veces el de diseno): la fila de
        // alta no hace wrap y con Registrar y Quitar en su tope todavia entra en el
        // dialogo. Un rotulo mas largo se recorta con elipsis, como lo dibuja el boton,
        // en vez de empujar la fila fuera del borde.
        private static void AjustarAncho_704ILR(AppButton_704ILR boton_704ILR, int minimo_704ILR)
        {
            int glifo_704ILR = string.IsNullOrEmpty(boton_704ILR.Glyph_704ILR) ? 0 : 30;
            int texto_704ILR = string.IsNullOrEmpty(boton_704ILR.Text) ? 0 : TextRenderer.MeasureText(boton_704ILR.Text, boton_704ILR.Font,
                new Size(int.MaxValue, Math.Max(1, boton_704ILR.Height - 1)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis).Width;
            int tope_704ILR = minimo_704ILR * 3 / 2;
            boton_704ILR.Width = Math.Min(tope_704ILR, Math.Max(minimo_704ILR, glifo_704ILR + texto_704ILR + 14 + 1));
        }

        // Monto del cobro. El NumericUpDown estandar lee el texto con la cultura del
        // equipo y admite el separador de miles en cualquier posicion: en es-AR el
        // punto del teclado numerico hacia que "1500.50" se leyera 150050 y se cobrara
        // sin aviso. Aca el texto se interpreta sin separador de miles y, si la cultura
        // usa coma decimal, un unico punto seguido de uno o dos digitos se toma como
        // decimal. Un texto ambiguo ("150.000", "1.500,50"), ilegible o fuera del rango
        // del campo deja el control en cero, y la capa de negocio lo rechaza como monto
        // invalido: nunca se cobra otro importe ni el valor que habia quedado de antes.
        private sealed class CampoImporte_704ILR : NumericUpDown
        {
            private bool _normalizando_704ILR;

            internal static bool Parsear_704ILR(string texto_704ILR, CultureInfo cultura_704ILR, out decimal valor_704ILR)
            {
                const NumberStyles estilo_704ILR = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                                                   NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
                string t_704ILR = (texto_704ILR ?? string.Empty).Trim();
                if (decimal.TryParse(t_704ILR, estilo_704ILR, cultura_704ILR, out valor_704ILR)) return true;
                string separador_704ILR = cultura_704ILR.NumberFormat.NumberDecimalSeparator;
                int punto_704ILR = t_704ILR.IndexOf('.');
                int decimales_704ILR = t_704ILR.Length - punto_704ILR - 1;
                if (separador_704ILR != "." && t_704ILR.IndexOf(separador_704ILR, StringComparison.Ordinal) < 0 &&
                    punto_704ILR >= 0 && punto_704ILR == t_704ILR.LastIndexOf('.') &&
                    decimales_704ILR >= 1 && decimales_704ILR <= 2)
                    return decimal.TryParse(t_704ILR, estilo_704ILR, CultureInfo.InvariantCulture, out valor_704ILR);
                valor_704ILR = 0m;
                return false;
            }

            // Reescribe el texto tipeado con la cultura antes de que el control lo lea.
            // Asignar Text vuelve a validar: la bandera corta esa reentrada.
            private void Normalizar_704ILR()
            {
                if (_normalizando_704ILR) return;
                _normalizando_704ILR = true;
                try
                {
                    var cultura_704ILR = CultureInfo.CurrentCulture;
                    // Un importe legible pero fuera del rango (negativo o mayor que lo que
                    // admite la columna) tambien vuelve a cero: el control estandar lo
                    // ajustaba al maximo en silencio y se cobraba otro importe.
                    Text = Parsear_704ILR(Text, cultura_704ILR, out decimal valor_704ILR) &&
                           valor_704ILR >= Minimum && valor_704ILR <= Maximum
                        ? valor_704ILR.ToString(cultura_704ILR)
                        : Minimum.ToString(cultura_704ILR);
                }
                finally { _normalizando_704ILR = false; }
            }

            // Overrides del framework (sin sufijo, REGLA 4): son los dos puntos en que el
            // control interpreta lo tipeado, al leer Value y al perder el foco.
            protected override void ValidateEditText()
            {
                if (UserEdit) Normalizar_704ILR();
                base.ValidateEditText();
            }

            protected override void UpdateEditText()
            {
                if (UserEdit) Normalizar_704ILR();
                base.UpdateEditText();
            }

            // Las flechas leen lo tipeado por su cuenta (sin pasar por los dos metodos de
            // arriba): se normaliza antes de sumar o restar el incremento.
            public override void UpButton()
            {
                if (UserEdit) Normalizar_704ILR();
                base.UpButton();
            }

            public override void DownButton()
            {
                if (UserEdit) Normalizar_704ILR();
                base.DownButton();
            }

            // El filtro de teclas del control estandar solo deja pasar digitos y los
            // separadores decimal y de miles de la cultura. Con coma decimal y espacio duro
            // como separador de miles (es-CR, fr-FR, pt-PT) descartaba el punto tipeado o el
            // de la tecla decimal del teclado numerico: "1500.50" llegaba como "150050" y se
            // cobraba otro importe. El punto y la coma llegan siempre al texto y los
            // interpreta Parsear_704ILR, con el mismo criterio en cualquier configuracion.
            protected override void OnTextBoxKeyPress(object source_704ILR, KeyPressEventArgs e_704ILR)
            {
                if (e_704ILR.KeyChar == '.' || e_704ILR.KeyChar == ',')
                {
                    OnKeyPress(e_704ILR);   // lo que hace UpDownBase, sin el filtro
                    return;
                }
                base.OnTextBoxKeyPress(source_704ILR, e_704ILR);
            }

            // La rueda del mouse no cambia un importe a cobrar: cada muesca sumaba o restaba
            // tres veces el incremento y se cobraba sin aviso, incluso sin haber tipeado
            // nada. Se consume sin mover el valor; las flechas siguen funcionando.
            protected override void OnMouseWheel(MouseEventArgs e_704ILR)
            {
                if (e_704ILR is HandledMouseEventArgs manejado_704ILR) manejado_704ILR.Handled = true;
            }
        }
    }
}
