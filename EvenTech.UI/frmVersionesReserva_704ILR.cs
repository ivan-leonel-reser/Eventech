using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Ventana modal con las versiones (mementos) de una reserva: cada fila es la
    // foto completa que se guardo automaticamente antes de cada modificacion.
    // Permite restaurar cualquiera (patron Memento: la UI solo habla con el
    // Caretaker/BLL; nunca interpreta el contenido de la foto).
    // Devuelve DialogResult.OK si se restauro una version, para que la pantalla
    // de reservas recargue la grilla.
    public class frmVersionesReserva_704ILR : FormBase_704ILR, IObservadorIdioma_704ILR
    {
        private readonly int _reservaId_704ILR;

        private Label _lblTitle_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblVacio_704ILR;
        private AppButton_704ILR _btnRestaurar_704ILR;
        private bool _estadoReservaDefinido_704ILR = true;   // estado almacenado dentro del ciclo de vida
        private bool _reservaModificable_704ILR;             // RN-05: la reserva no esta CANCELADA
        private DataGridViewTextBoxColumn _colFecha_704ILR, _colUsuario_704ILR, _colCliente_704ILR, _colSalon_704ILR, _colFechaEvento_704ILR, _colInvitados_704ILR, _colEstado_704ILR, _colMonto_704ILR;

        public frmVersionesReserva_704ILR(int reservaId_704ILR)
        {
            _reservaId_704ILR = reservaId_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => CargarVersiones_704ILR();
            GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            FormClosed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            // 1070 px: con la columna Estado medida para la leyenda de estado desconocido
            // (la mas ancha, "(estado desconhecido)") la suma de los minimos sigue entrando
            // con la barra vertical, sin barra horizontal (ver AjustarAnchosColumnas_704ILR).
            ClientSize = new Size(1070, 480);
            BackColor = Theme_704ILR.BgContent_704ILR;

            // ---------------- Barra de titulo ----------------
            var pnlTop_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTop_704ILR);

            _lblTitle_704ILR = new Label
            {
                Font = Theme_704ILR.FontH2_704ILR,
                ForeColor = Theme_704ILR.TextOnDark_704ILR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0),
                BackColor = Color.Transparent
            };
            EnableDrag_704ILR(_lblTitle_704ILR);

            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => Close(), danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;

            pnlTop_704ILR.Controls.Add(_lblTitle_704ILR);
            pnlTop_704ILR.Controls.Add(btnClose_704ILR);

            // ---------------- Contenido (tarjeta con grilla + acciones) ----------------
            var pnlContent_704ILR = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };

            var layout_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            layout_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grilla
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // boton restaurar

            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                BehindColor_704ILR = Theme_704ILR.BgContent_704ILR,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR),
                Padding = new Padding(Theme_704ILR.SpaceSm_704ILR)
            };

            _grid_704ILR = new DataGridView
            {
                Name = "grid",
                Dock = DockStyle.Fill,
                BackgroundColor = Theme_704ILR.Surface_704ILR
            };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);

            // Anchos minimos medidos para lo mas ancho que muestra cada columna en
            // ES/EN/PT: encabezados ("Fecha del evento", "Convidados") y valores
            // ("mgutierrez", "Salon Principal", "9.999.999.999,99", el maximo de
            // DECIMAL(12,2)). En modo Fill el reparto por peso respeta los minimos; el
            // dialogo es lo bastante ancho para que la suma entre aun con la barra
            // vertical, asi el monto con el que se decide que version restaurar se lee.
            // Las fechas se muestran en gregoriano con separadores invariantes, como en la bitacora y
            // en el Detalle de sus asientos: el patron solo fija el orden, y con la cultura de la
            // estacion th-TH mostraba 2569 y fi-FI 01.15. Asi el texto es el mismo en cualquier
            // cultura y los minimos medidos siguen alcanzando.
            _colFecha_704ILR = new DataGridViewTextBoxColumn
            {
                Name = "Fecha",
                DataPropertyName = "Fecha_704ILR",
                FillWeight = 70,
                MinimumWidth = 125,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm", FormatProvider = CultureInfo.InvariantCulture }
            };
            _colUsuario_704ILR     = new DataGridViewTextBoxColumn { Name = "Usuario",       DataPropertyName = "Usuario_704ILR",       FillWeight = 45, MinimumWidth = 92 };
            _colCliente_704ILR     = new DataGridViewTextBoxColumn { Name = "ClienteNombre", DataPropertyName = "ClienteNombre_704ILR", FillWeight = 65, MinimumWidth = 118 };
            _colSalon_704ILR       = new DataGridViewTextBoxColumn { Name = "SalonNombre",   DataPropertyName = "SalonNombre_704ILR",   FillWeight = 55, MinimumWidth = 114 };
            _colFechaEvento_704ILR = new DataGridViewTextBoxColumn
            {
                Name = "FechaEvento",
                DataPropertyName = "FechaEvento_704ILR",
                FillWeight = 50,
                MinimumWidth = 142,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd", FormatProvider = CultureInfo.InvariantCulture }
            };
            // El memento conserva la cantidad de invitados de cada version (es parte del
            // estado de negocio y la RN-06 depende de el): la pantalla tiene que mostrarla.
            _colInvitados_704ILR = new DataGridViewTextBoxColumn
            {
                Name = "Invitados",
                DataPropertyName = "CantidadInvitados_704ILR",
                FillWeight = 40,
                MinimumWidth = 106,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            // El minimo de Estado se mide al traducir (AjustarAnchosColumnas_704ILR): incluye la
            // leyenda de estado desconocido, mas ancha que cualquier estado valido. Ahi mismo los
            // pesos de estas definiciones se reemplazan por los minimos.
            _colEstado_704ILR = new DataGridViewTextBoxColumn { Name = "Estado", DataPropertyName = "Estado_704ILR", FillWeight = 45, MinimumWidth = 96 };
            _colMonto_704ILR  = new DataGridViewTextBoxColumn
            {
                Name = "Monto",
                DataPropertyName = "Monto_704ILR",
                FillWeight = 45,
                MinimumWidth = 124,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            _grid_704ILR.Columns.AddRange(_colFecha_704ILR, _colUsuario_704ILR, _colCliente_704ILR, _colSalon_704ILR, _colFechaEvento_704ILR, _colInvitados_704ILR, _colEstado_704ILR, _colMonto_704ILR);
            _grid_704ILR.CellFormatting += Grid_CellFormatting_704ILR;

            // Estado vacio: centrado sobre la grilla, visible solo si no hay filas.
            _lblVacio_704ILR = new Label
            {
                Dock = DockStyle.Fill,
                Font = Theme_704ILR.FontBody_704ILR,
                ForeColor = Theme_704ILR.TextMuted_704ILR,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme_704ILR.Surface_704ILR,
                Visible = false
            };

            card_704ILR.Controls.Add(_lblVacio_704ILR);
            card_704ILR.Controls.Add(_grid_704ILR);

            var acciones_704ILR = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            _btnRestaurar_704ILR = Ui_704ILR.Primary_704ILR("Restaurar seleccionada");
            _btnRestaurar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnRestaurar_704ILR.Size = new Size(220, 38);
            _btnRestaurar_704ILR.Click += (s_704ILR, e_704ILR) => Restaurar_704ILR();
            _grid_704ILR.SelectionChanged += (s_704ILR, e_704ILR) =>
            {
                if (_grid_704ILR.Visible) _btnRestaurar_704ILR.Enabled = Permisos_704ILR.Tiene_704ILR("RESERVA_RESTAURAR") && SeleccionRestaurable_704ILR();
            };
            acciones_704ILR.Controls.Add(_btnRestaurar_704ILR);

            layout_704ILR.Controls.Add(card_704ILR, 0, 0);
            layout_704ILR.Controls.Add(acciones_704ILR, 0, 1);

            pnlContent_704ILR.Controls.Add(layout_704ILR);

            Controls.Add(pnlContent_704ILR);
            Controls.Add(pnlTop_704ILR);
        }

        private void CargarVersiones_704ILR()
        {
            try
            {
                // Una reserva con el estado almacenado fuera del ciclo de vida (alterada por fuera)
                // no se restaura: la ficha la muestra de solo lectura y aca Restaurar queda apagado.
                // Lo mismo una CANCELADA, que es terminal (RN-05): la ficha tambien es de solo lectura y
                // la BLL rechaza restaurarla. Las versiones se siguen consultando, pero no se ofrece una
                // restauracion que terminaba en un aviso de error y un asiento de rechazo.
                var reserva_704ILR = BLL_Reserva_704ILR.GetById_704ILR(_reservaId_704ILR);
                _estadoReservaDefinido_704ILR = reserva_704ILR != null && System.Enum.IsDefined(typeof(EstadoReserva_704ILR), reserva_704ILR.Estado_704ILR);
                // Tampoco se ofrece restaurar con el evento en ejecucion o cerrado: la
                // reserva queda congelada (RN-13) y la BLL lo rechaza.
                _reservaModificable_704ILR = BLL_Reserva_704ILR.PuedeModificar_704ILR(reserva_704ILR)
                    && !BLL_Coordinacion_704ILR.PlanCongelado_704ILR(reserva_704ILR.EstadoCoordinacion_704ILR);
                List<BE_ReservaMemento_704ILR> data_704ILR = CaretakerReserva_704ILR.GetVersiones_704ILR(_reservaId_704ILR);
                _grid_704ILR.DataSource = data_704ILR;
                ActualizarEstadoVacio_704ILR(data_704ILR == null || data_704ILR.Count == 0);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Cargar versiones de reserva");
                ActualizarEstadoVacio_704ILR(true);
                MessageBox.Show(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), T_704ILR("MSG_ERROR", "Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Restaurar_704ILR()
        {
            if (!(_grid_704ILR.CurrentRow?.DataBoundItem is BE_ReservaMemento_704ILR memento_704ILR)) return;

            // Restaurar reemplaza el estado vigente de la reserva: la pregunta arranca en No, como las
            // demas confirmaciones que reemplazan o descartan datos.
            var confirma_704ILR = MessageBox.Show(
                T_704ILR("VER_CONFIRMA", "¿Restaurar la reserva al estado de la versión seleccionada? El estado actual se guardará como una nueva versión."),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (confirma_704ILR != DialogResult.Yes) return;

            // La restauracion tambien se exige aca, no solo en quien abre el dialogo:
            // es el punto donde la operacion realmente se ejecuta.
            if (!Permisos_704ILR.Exigir_704ILR("RESERVA_RESTAURAR", this, "restaurar una version de la reserva #" + _reservaId_704ILR)) return;

            try
            {
                ReservaResult_704ILR result_704ILR = BLL_Reserva_704ILR.RestaurarVersion_704ILR(_reservaId_704ILR, memento_704ILR.Id_704ILR);
                if (result_704ILR != ReservaResult_704ILR.Success_704ILR)
                {
                    bool estadoAjeno_704ILR = result_704ILR == ReservaResult_704ILR.TransicionInvalida_704ILR
                        && (!_estadoReservaDefinido_704ILR || !System.Enum.IsDefined(typeof(EstadoReserva_704ILR), memento_704ILR.Estado_704ILR));
                    // Al restaurar se admite una fecha pasada: un rechazo por fecha solo puede venir de una
                    // version fuera del calendario de la ficha. Un rechazo por invitados puede venir de una
                    // version fuera de rango o de confirmar sin invitados (RN-06): se decide con la version
                    // tal como esta en la base, que es la que valida la BLL (pudo alterarse con el dialogo
                    // abierto).
                    BE_ReservaMemento_704ILR vigente_704ILR = result_704ILR == ReservaResult_704ILR.InvalidInvitados_704ILR
                        ? CaretakerReserva_704ILR.GetVersion_704ILR(memento_704ILR.Id_704ILR) ?? memento_704ILR
                        : memento_704ILR;
                    string aviso_704ILR = estadoAjeno_704ILR
                        ? T_704ILR("MSG_RES_ESTADO_DESCONOCIDO", "El estado registrado de la reserva no es válido: no admite modificaciones. Contactate con un administrador.")
                        : result_704ILR == ReservaResult_704ILR.InvalidFecha_704ILR
                            ? T_704ILR("MSG_RES_FECHA_FUERA_RANGO", "La fecha del evento registrada está fuera del calendario admitido: no admite modificaciones. Contactate con un administrador.")
                        : result_704ILR == ReservaResult_704ILR.InvalidInvitados_704ILR && !InvitadosEnRango_704ILR(vigente_704ILR)
                            ? T_704ILR("MSG_RES_INVITADOS_FUERA_RANGO", "La cantidad de invitados registrada está fuera del rango admitido: no admite modificaciones. Contactate con un administrador.")
                        : MensajeError_704ILR(result_704ILR);
                    MessageBox.Show(aviso_704ILR, T_704ILR("MSG_ERROR", "Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                MessageBox.Show(T_704ILR("MSG_VER_OK", "Versión restaurada."), "EvenTech",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Restaurar version de reserva");
                MessageBox.Show(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), T_704ILR("MSG_ERROR", "Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Traduce el valor de la columna Estado (el enum se muestra segun el idioma).
        private void Grid_CellFormatting_704ILR(object sender_704ILR, DataGridViewCellFormattingEventArgs e_704ILR)
        {
            if (e_704ILR.RowIndex < 0 || e_704ILR.ColumnIndex < 0 || e_704ILR.ColumnIndex >= _grid_704ILR.Columns.Count) return;
            if (_grid_704ILR.Columns[e_704ILR.ColumnIndex].Name != "Estado") return;
            if (e_704ILR.Value is EstadoReserva_704ILR est_704ILR) { e_704ILR.Value = Tr_704ILR.Estado_704ILR(est_704ILR); e_704ILR.FormattingApplied = true; }
        }

        private void ActualizarEstadoVacio_704ILR(bool vacio_704ILR)
        {
            _lblVacio_704ILR.Visible = vacio_704ILR;
            _grid_704ILR.Visible = !vacio_704ILR;
            // El dialogo se abre para CONSULTAR las versiones (basta el permiso de
            // historial). Restaurar es una correccion administrativa aparte: sin
            // RESERVA_RESTAURAR el boton queda apagado y la lista se mira igual.
            _btnRestaurar_704ILR.Enabled = !vacio_704ILR && Permisos_704ILR.Tiene_704ILR("RESERVA_RESTAURAR") && SeleccionRestaurable_704ILR();
        }

        // La reserva tiene que admitir modificaciones (no esta CANCELADA) y tener un estado dentro
        // del ciclo de vida, y la version elegida tambien: una version CANCELADA no se restaura,
        // porque la baja se registra por la via de cancelacion (RN-02/RN-05). La version tampoco
        // puede traer una fecha o una cantidad de invitados fuera de lo que admite la ficha (ver
        // FechaEnRango e InvitadosEnRango). La BLL rechaza todos esos casos; aca el boton sigue a
        // la fila elegida y no ofrece lo que se va a rechazar.
        private bool SeleccionRestaurable_704ILR()
        {
            if (!_estadoReservaDefinido_704ILR || !_reservaModificable_704ILR) return false;
            return !(_grid_704ILR.CurrentRow?.DataBoundItem is BE_ReservaMemento_704ILR m_704ILR)
                || (System.Enum.IsDefined(typeof(EstadoReserva_704ILR), m_704ILR.Estado_704ILR)
                    && m_704ILR.Estado_704ILR != EstadoReserva_704ILR.CANCELADA
                    && FechaEnRango_704ILR(m_704ILR) && InvitadosEnRango_704ILR(m_704ILR));
        }

        // Los mismos topes que BLL_Reserva_704ILR.Validar_704ILR aplica al restaurar: la fecha pasada
        // se admite, pero no una posterior al ultimo dia del selector de la ficha; los invitados van
        // de cero al maximo del campo.
        private static bool FechaEnRango_704ILR(BE_ReservaMemento_704ILR m_704ILR)
            => m_704ILR.FechaEvento_704ILR != default && m_704ILR.FechaEvento_704ILR <= BLL_Reserva_704ILR.FechaEventoMaxima_704ILR;

        private static bool InvitadosEnRango_704ILR(BE_ReservaMemento_704ILR m_704ILR)
            => m_704ILR.CantidadInvitados_704ILR >= 0 && m_704ILR.CantidadInvitados_704ILR <= BLL_Reserva_704ILR.InvitadosMaximo_704ILR;

        private static string MensajeError_704ILR(ReservaResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case ReservaResult_704ILR.InvalidCliente_704ILR: return T_704ILR("MSG_RES_CLIENTE", "Seleccione un cliente válido.");
                case ReservaResult_704ILR.InvalidSalon_704ILR:   return T_704ILR("MSG_RES_SALON", "Seleccione un salón válido.");
                case ReservaResult_704ILR.InvalidFecha_704ILR:   return T_704ILR("MSG_RES_FECHA", "La fecha del evento no puede ser anterior a hoy.");
                // El tope se escribe con el formato de la cultura actual, el mismo del Monto de la
                // grilla: con el numero fijo en el texto, otra configuracion regional mostraba
                // "9.999.999.999,99" junto a importes escritos "968,000.00".
                case ReservaResult_704ILR.InvalidMonto_704ILR:
                    return Tr_704ILR.F_704ILR("MSG_RES_MONTO", "El monto no puede ser negativo ni superar {0}.",
                        BLL_Reserva_704ILR.MontoMaximo_704ILR.ToString("N2", CultureInfo.CurrentCulture));
                case ReservaResult_704ILR.SalonOcupado_704ILR:   return T_704ILR("MSG_RES_SALON_OCUPADO", "El salón ya está reservado para esa fecha.");
                case ReservaResult_704ILR.NotFound_704ILR:       return T_704ILR("MSG_RES_NOTFOUND", "La reserva ya no existe.");
                // Motivos que la restauracion puede devolver desde que rige la RN-05/RN-06:
                // sin estos casos el dialogo mostraria un error generico sabiendo la causa.
                case ReservaResult_704ILR.NoModificable_704ILR:
                    return T_704ILR("MSG_RES_NO_MODIFICABLE", "La reserva está cancelada: no admite modificaciones.");
                case ReservaResult_704ILR.TransicionInvalida_704ILR:
                    return T_704ILR("MSG_RES_TRANSICION_GEN", "El cambio de estado solicitado no está admitido.");
                case ReservaResult_704ILR.CapacidadInsuficiente_704ILR:
                    return T_704ILR("MSG_RES_CAPACIDAD", "El salón no alcanza para la cantidad de invitados indicada.");
                case ReservaResult_704ILR.InvalidInvitados_704ILR:
                    return T_704ILR("MSG_RES_INVITADOS", "Indica la cantidad de invitados estimada: hace falta para confirmar y no puede ser negativa.");
                case ReservaResult_704ILR.MontoInferiorPagado_704ILR:
                    return T_704ILR("MSG_RES_MONTO_PAGADO", "El total de la reserva no puede quedar por debajo de lo ya cobrado.");
                case ReservaResult_704ILR.SinAdelanto_704ILR:
                    return T_704ILR("MSG_RES_SIN_ADELANTO", "Para confirmar la reserva hay que registrar el adelanto: guardala y cobra el pago desde Pagos.");
                case ReservaResult_704ILR.Vencida_704ILR:
                    return T_704ILR("MSG_RES_VENCIDA", "La operación venció: renovala antes de cambiar su estado.");
                case ReservaResult_704ILR.EventoIniciado_704ILR:
                    return T_704ILR("MSG_RES_EVENTO_INICIADO", "El evento de esta reserva está en ejecución o cerrado: la reserva ya no admite modificaciones ni cancelación.");
                default:                           return T_704ILR("MSG_RES_ERROR", "No se pudo guardar la reserva.");
            }
        }

        // Devuelve la traduccion de 'clave' o, si falta, el texto por defecto dado.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        // Observador (patron Observer): re-traduce titulo, encabezados y acciones.
        public void ActualizarTextos_704ILR()
        {
            if (_lblTitle_704ILR != null) _lblTitle_704ILR.Text = T_704ILR("VER_TITULO", "Versiones de la reserva") + " #" + _reservaId_704ILR;
            if (_colFecha_704ILR != null)
            {
                _colFecha_704ILR.HeaderText       = T_704ILR("COL_FECHA", "Fecha");
                _colUsuario_704ILR.HeaderText     = T_704ILR("COL_USUARIO", "Usuario");
                _colCliente_704ILR.HeaderText     = T_704ILR("COL_CLIENTE", "Cliente");
                _colSalon_704ILR.HeaderText       = T_704ILR("COL_SALON", "Salón");
                _colFechaEvento_704ILR.HeaderText = T_704ILR("RES_LBL_FECHA", "Fecha del evento");
                _colInvitados_704ILR.HeaderText   = T_704ILR("COL_INVITADOS", "Invitados");
                _colEstado_704ILR.HeaderText      = T_704ILR("COL_ESTADO", "Estado");
                _colMonto_704ILR.HeaderText       = T_704ILR("COL_MONTO", "Monto");
                AjustarAnchosColumnas_704ILR();
            }
            if (_lblVacio_704ILR != null) _lblVacio_704ILR.Text = T_704ILR("VER_VACIO", "Sin versiones guardadas. Se crea una automáticamente al modificar la reserva.");
            if (_btnRestaurar_704ILR != null) _btnRestaurar_704ILR.Text = T_704ILR("VER_RESTAURAR", "Restaurar seleccionada");
            _grid_704ILR?.Invalidate();
        }

        // Holgura sobre el texto medido, para que la leyenda no quede pegada al borde de la celda.
        private const int HolguraTexto_704ILR = 6;

        // Minimo de la columna Estado medido en el idioma activo, con la letra real de la
        // grilla: el encabezado, cada estado del ciclo de vida y la leyenda de un estado que no
        // es ninguno de ellos (Tr_704ILR.Estado_704ILR de un valor fuera del enum, lo que se
        // muestra si la base se altero por fuera de la aplicacion). Fijado en 96 px alcanzaba
        // para los estados validos, pero la leyenda se cortaba en "(estado de..." y la parte
        // visible no decia que el estado es desconocido. Celda: texto + Padding del estilo;
        // encabezado: texto + Padding + 4 px de la celda de encabezado.
        private void AjustarAnchosColumnas_704ILR()
        {
            var enc_704ILR = _grid_704ILR.ColumnHeadersDefaultCellStyle;
            var cel_704ILR = _grid_704ILR.DefaultCellStyle;
            int Celda_704ILR(string t_704ILR) =>
                TextRenderer.MeasureText(t_704ILR, cel_704ILR.Font).Width + cel_704ILR.Padding.Horizontal + HolguraTexto_704ILR;

            int minimo_704ILR = TextRenderer.MeasureText(_colEstado_704ILR.HeaderText, enc_704ILR.Font).Width
                + enc_704ILR.Padding.Horizontal + 4 + HolguraTexto_704ILR;
            foreach (EstadoReserva_704ILR e_704ILR in Enum.GetValues(typeof(EstadoReserva_704ILR)))
                minimo_704ILR = Math.Max(minimo_704ILR, Celda_704ILR(Tr_704ILR.Estado_704ILR(e_704ILR)));
            minimo_704ILR = Math.Max(minimo_704ILR, Celda_704ILR(Tr_704ILR.Estado_704ILR((EstadoReserva_704ILR)(-1))));
            _colEstado_704ILR.MinimumWidth = minimo_704ILR;

            // Reparto: cada columna pesa lo mismo que su minimo (reemplaza los pesos de las
            // definiciones). La grilla reparte el ancho por peso y lleva a su minimo las columnas
            // que quedan cortas, pero no vuelve a repartir lo que eso consume: con otros pesos y
            // Estado mas ancha la suma se pasaba unos pixeles del ancho disponible y aparecia la
            // barra horizontal (al abrir en ES y PT, y al pasar a PT con el dialogo abierto). Con
            // pesos proporcionales a los minimos ninguna columna queda por debajo del suyo mientras
            // la grilla tenga lugar para todos, y lo que sobra se reparte en la misma proporcion.
            // Asignar los pesos los marca pendientes: el reparto se recalcula desde ellos.
            foreach (DataGridViewColumn col_704ILR in _grid_704ILR.Columns)
                col_704ILR.FillWeight = col_704ILR.MinimumWidth;
        }
    }
}
