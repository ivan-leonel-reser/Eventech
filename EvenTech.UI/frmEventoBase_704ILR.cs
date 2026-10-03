using System;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Base de los dialogos que trabajan sobre UN evento (personal, cronograma, tareas
    // y supervision): barra de titulo, encabezado con los datos del evento y su estado
    // de coordinacion, y el manejo comun de avisos y fallas. Cada dialogo arma su
    // contenido y vuelve a leer el encabezado despues de cada operacion.
    public class frmEventoBase_704ILR : FormBase_704ILR
    {
        protected readonly int _reservaId_704ILR;
        // El evento segun la ultima lectura (null si la reserva ya no existe).
        protected BE_EventoCoordinacion_704ILR _evento_704ILR;
        // La ultima lectura del evento fallo: no se sabe en que estado esta.
        private bool _lecturaFallida_704ILR;
        private Label _lblEvento_704ILR, _lblEstado_704ILR;

        protected frmEventoBase_704ILR(int reservaId_704ILR)
        {
            _reservaId_704ILR = reservaId_704ILR;
        }

        // Barra de titulo con el boton de cierre. Se agrega al formulario DESPUES del
        // contenido (el ultimo control agregado ancla primero).
        protected Panel ArmarTitulo_704ILR(string titulo_704ILR)
        {
            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblTitle_704ILR = new Label
            {
                Text = titulo_704ILR,
                Font = Theme_704ILR.FontH2_704ILR, ForeColor = Theme_704ILR.TextOnDark_704ILR, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0), BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblTitle_704ILR);
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => Close(), danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);
            return pnlTitle_704ILR;
        }

        // Encabezado: los datos del evento a la izquierda y su estado de coordinacion a
        // la derecha. Va en la primera fila del contenido de cada dialogo.
        protected Control ArmarEncabezado_704ILR()
        {
            var t_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };
            t_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _lblEvento_704ILR = new Label
            {
                Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, AutoSize = true, AutoEllipsis = true,
                // El nombre del cliente o del salon puede llevar '&': se muestra tal cual.
                UseMnemonic = false,
                Anchor = AnchorStyles.Left, Margin = new Padding(2, 0, Theme_704ILR.SpaceMd_704ILR, 0), BackColor = Color.Transparent
            };
            _lblEstado_704ILR = new Label
            {
                Font = Theme_704ILR.FontBodyBold_704ILR, AutoSize = true, Anchor = AnchorStyles.Right,
                Margin = new Padding(0), BackColor = Color.Transparent
            };
            t_704ILR.Controls.Add(_lblEvento_704ILR, 0, 0);
            t_704ILR.Controls.Add(_lblEstado_704ILR, 1, 0);
            return t_704ILR;
        }

        // Vuelve a leer el evento y repinta el encabezado. Las clases derivadas la
        // llaman al refrescar, despues de cada operacion.
        protected void LeerEvento_704ILR()
        {
            try
            {
                _evento_704ILR = BLL_Coordinacion_704ILR.GetEvento_704ILR(_reservaId_704ILR);
                _lecturaFallida_704ILR = false;
            }
            catch
            {
                // Sin lectura no hay evento sobre el que ofrecer acciones: el dialogo queda
                // de solo lectura hasta la proxima lectura (ver Ejecutar_704ILR).
                _evento_704ILR = null;
                _lecturaFallida_704ILR = true;
                if (_lblEvento_704ILR != null) _lblEvento_704ILR.Text = _lblEstado_704ILR.Text = string.Empty;
                throw;
            }
            if (_lblEvento_704ILR == null) return;
            _lblEvento_704ILR.Text = Coord_704ILR.Evento_704ILR(_evento_704ILR);
            if (_evento_704ILR == null) { _lblEstado_704ILR.Text = string.Empty; return; }
            _lblEstado_704ILR.Text = Coord_704ILR.Estado_704ILR(_evento_704ILR.EstadoCoordinacion_704ILR);
            _lblEstado_704ILR.ForeColor = Coord_704ILR.Color_704ILR(_evento_704ILR.EstadoCoordinacion_704ILR);
        }

        // True si el plan del evento todavia se puede modificar: la reserva existe, sigue
        // CONFIRMADA (RN-08) y el evento no empezo (RN-13). La capa de negocio vuelve a
        // exigirlo en cada operacion.
        protected bool PlanEditable_704ILR =>
            _evento_704ILR != null && _evento_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA
            && !BLL_Coordinacion_704ILR.PlanCongelado_704ILR(_evento_704ILR.EstadoCoordinacion_704ILR);

        // Por que el plan no se puede modificar, para el aviso de cada dialogo; null si
        // se puede.
        protected string AvisoPlanNoEditable_704ILR()
        {
            if (_lecturaFallida_704ILR) return T_704ILR("MSG_OP_ERROR", "No se pudo completar la operación.");
            if (_evento_704ILR == null) return Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.ReservaInvalida_704ILR);
            if (_evento_704ILR.Estado_704ILR != EstadoReserva_704ILR.CONFIRMADA) return Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR);
            if (_evento_704ILR.EstadoCoordinacion_704ILR == EstadoCoordinacion_704ILR.CERRADO) return Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.EventoCerrado_704ILR);
            if (_evento_704ILR.EstadoCoordinacion_704ILR == EstadoCoordinacion_704ILR.EN_EJECUCION) return Coord_704ILR.Mensaje_704ILR(CoordinacionResult_704ILR.EventoEnEjecucion_704ILR);
            return null;
        }

        // Habilita cada accion segun el estado del evento y los permisos. Cada dialogo
        // la redefine; la base la llama cuando una lectura falla, para que no queden
        // ofrecidas acciones sobre un evento que no se pudo leer.
        protected virtual void ActualizarAcciones_704ILR() { }

        // Una hora a medio tipear en un selector queda pendiente hasta que el selector
        // pierde el foco: Enter ejecuta el boton por defecto sin moverlo, y la operacion
        // leeria la hora anterior. Antes de operar, el foco pasa al boton por defecto y
        // el selector da por tipeado lo que tenia.
        protected void ConfirmarHoraTipeada_704ILR()
        {
            if (ActiveControl is DateTimePicker) (AcceptButton as Control)?.Focus();
        }

        // Envuelve una accion que toca la base: una falla se asienta en la bitacora y se
        // informa, en vez de terminar la aplicacion con el dialogo abierto.
        protected void Ejecutar_704ILR(Action accion_704ILR, string contexto_704ILR)
        {
            try
            {
                ConfirmarHoraTipeada_704ILR();
                accion_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Coordinacion", contexto_704ILR + " (reserva #" + _reservaId_704ILR + ")");
                // Si la falla llego antes de leer el evento, tampoco se sabe en que estado esta.
                if (_evento_704ILR == null) _lecturaFallida_704ILR = true;
                ActualizarAcciones_704ILR();
                Aviso_704ILR(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        // Aviso de un rechazo, de un dato que falta o de una falla.
        protected void Aviso_704ILR(string msg_704ILR) =>
            MessageBox.Show(this, msg_704ILR, "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        // Confirmacion de que una operacion se completo.
        protected void Informar_704ILR(string msg_704ILR) =>
            MessageBox.Show(this, msg_704ILR, "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);

        // Pregunta destructiva o de descarte: No por defecto.
        protected bool Preguntar_704ILR(string msg_704ILR) =>
            MessageBox.Show(this, msg_704ILR, "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        // Boton primario o secundario con el fondo del dialogo y el ancho ajustado al texto.
        // Interno: AppButton_704ILR no es un tipo publico.
        internal static AppButton_704ILR Boton_704ILR(string texto_704ILR, string glifo_704ILR, int ancho_704ILR, bool secundario_704ILR = false)
        {
            AppButton_704ILR b_704ILR = secundario_704ILR ? Ui_704ILR.Secondary_704ILR(texto_704ILR, glifo_704ILR) : Ui_704ILR.Primary_704ILR(texto_704ILR, glifo_704ILR);
            b_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            b_704ILR.Size = new Size(ancho_704ILR, 32);
            Coord_704ILR.AjustarAncho_704ILR(b_704ILR, ancho_704ILR);
            return b_704ILR;
        }

        protected static string T_704ILR(string clave_704ILR, string defecto_704ILR) => Coord_704ILR.T_704ILR(clave_704ILR, defecto_704ILR);

        // Fila de alta de un dialogo: campos, rotulos y boton centrados en vertical sobre
        // el control mas alto de la fila (el boton). Alineados por arriba, el boton
        // quedaba unos pixeles mas abajo que los campos.
        protected static void CentrarFila_704ILR(FlowLayoutPanel fila_704ILR)
        {
            foreach (Control c_704ILR in fila_704ILR.Controls)
            {
                c_704ILR.Anchor = AnchorStyles.Left;
                c_704ILR.Margin = new Padding(c_704ILR.Margin.Left, 0, c_704ILR.Margin.Right, 0);
            }
        }

        // El dialogo aparece centrado sobre la ventana principal, debajo del cursor: el
        // segundo clic de un doble clic sobre el boton que lo abrio caia sobre el control
        // que quedara en ese punto (en Tareas, el boton Cerrar). Mientras dura el
        // intervalo de doble clic del sistema se descartan los clics del boton izquierdo.
        private sealed class FiltroSegundoClic_704ILR : IMessageFilter
        {
            private const int WM_LBUTTONDOWN_704ILR = 0x0201, WM_LBUTTONUP_704ILR = 0x0202, WM_LBUTTONDBLCLK_704ILR = 0x0203;
            private readonly long _hasta_704ILR = Environment.TickCount64 + SystemInformation.DoubleClickTime;

            // Miembro de la interfaz del framework (sin sufijo, REGLA 4).
            public bool PreFilterMessage(ref Message m_704ILR) =>
                Environment.TickCount64 <= _hasta_704ILR
                && (m_704ILR.Msg == WM_LBUTTONDOWN_704ILR || m_704ILR.Msg == WM_LBUTTONUP_704ILR || m_704ILR.Msg == WM_LBUTTONDBLCLK_704ILR);
        }

        private FiltroSegundoClic_704ILR _filtroSegundoClic_704ILR;

        // Overrides del framework (sin sufijo, REGLA 4).
        protected override void OnShown(EventArgs e_704ILR)
        {
            base.OnShown(e_704ILR);
            _filtroSegundoClic_704ILR = new FiltroSegundoClic_704ILR();
            Application.AddMessageFilter(_filtroSegundoClic_704ILR);
        }

        protected override void OnFormClosed(FormClosedEventArgs e_704ILR)
        {
            if (_filtroSegundoClic_704ILR != null)
            {
                Application.RemoveMessageFilter(_filtroSegundoClic_704ILR);
                _filtroSegundoClic_704ILR = null;
            }
            base.OnFormClosed(e_704ILR);
        }

        // Enter mantenido: actua solo la primera pulsacion, como en el dialogo de pagos
        // (cada repeticion pulsaba el boton por defecto y repetia la operacion). Lo mismo
        // Escape: mantenido al cerrar un aviso, la repeticion cerraba tambien el dialogo.
        // Override del framework (sin sufijo, REGLA 4).
        private const int WM_KEYDOWN_704ILR = 0x0100;
        private const long BitRepeticion_704ILR = 0x40000000;

        protected override bool ProcessCmdKey(ref Message msg_704ILR, Keys keyData_704ILR)
        {
            Keys tecla_704ILR = keyData_704ILR & Keys.KeyCode;
            if (msg_704ILR.Msg == WM_KEYDOWN_704ILR && (tecla_704ILR == Keys.Enter || tecla_704ILR == Keys.Escape) &&
                (msg_704ILR.LParam.ToInt64() & BitRepeticion_704ILR) != 0)
                return true;
            return base.ProcessCmdKey(ref msg_704ILR, keyData_704ILR);
        }
    }
}
