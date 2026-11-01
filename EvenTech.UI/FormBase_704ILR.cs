using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EvenTech.UI
{
    // Form base sin bordes con utilidades de cromo compartidas: arrastre por la
    // barra de titulo (Win32) y botones de ventana (minimizar/cerrar). Evita
    // duplicar el P/Invoke y el armado de la barra en cada formulario.
    public class FormBase_704ILR : Form
    {
        // El sufijo de autoria nombra al metodo del lado de C#, pero el runtime busca
        // ese mismo nombre DENTRO de user32.dll si no se declara EntryPoint. Como la
        // exportacion nativa se llama ReleaseCapture / SendMessage, hay que fijarla:
        // sin EntryPoint la llamada lanza EntryPointNotFoundException al arrastrar.
        [DllImport("user32.dll", EntryPoint = "ReleaseCapture")]
        private static extern bool ReleaseCapture_704ILR();

        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage_704ILR(IntPtr hWnd_704ILR, int Msg_704ILR, IntPtr wParam_704ILR, IntPtr lParam_704ILR);
        private const int WM_NCLBUTTONDOWN_704ILR = 0xA1;
        private const int HT_CAPTION_704ILR = 0x2;

        protected FormBase_704ILR()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            Font = Theme_704ILR.FontBody_704ILR;
            BackColor = Theme_704ILR.BgContent_704ILR;
            DoubleBuffered = true;
        }

        // Ayuda en linea (D02): F1 abre el tema de esta ventana. Cada dialogo declara el
        // suyo; la ventana principal devuelve el de la seccion abierta. Sin tema, el indice.
        protected virtual string TemaAyuda_704ILR => Ayuda_704ILR.TemaInicio_704ILR;

        protected override bool ProcessCmdKey(ref Message msg_704ILR, Keys keyData_704ILR)
        {
            if (keyData_704ILR == Keys.F1)
            {
                Ayuda_704ILR.Mostrar_704ILR(this, TemaAyuda_704ILR);
                return true;
            }
            return base.ProcessCmdKey(ref msg_704ILR, keyData_704ILR);
        }

        // Una ventana sin borde no trae el redimensionado del sistema. Activandolo,
        // el formulario responde el codigo de zona que corresponde a cada borde y
        // Windows la redimensiona como a cualquier otra. Lo usa frmMain, para que en
        // pantallas mas grandes la ventana pueda aprovechar el espacio.
        protected bool Redimensionable_704ILR
        {
            get => _redimensionable_704ILR;
            set
            {
                _redimensionable_704ILR = value;
                if (value) AtenderFranja_704ILR(this);
            }
        }
        private bool _redimensionable_704ILR;

        private const int BordeRedimension_704ILR = 6;
        private const int WM_NCHITTEST_704ILR = 0x0084;
        private const int HTTRANSPARENT_704ILR = -1, HTCLIENT_704ILR = 1;
        private const int HTLEFT_704ILR = 10, HTRIGHT_704ILR = 11, HTTOP_704ILR = 12;
        private const int HTTOPLEFT_704ILR = 13, HTTOPRIGHT_704ILR = 14, HTBOTTOM_704ILR = 15;
        private const int HTBOTTOMLEFT_704ILR = 16, HTBOTTOMRIGHT_704ILR = 17;

        protected override void WndProc(ref Message m_704ILR)
        {
            base.WndProc(ref m_704ILR);

            if (!_redimensionable_704ILR || m_704ILR.Msg != WM_NCHITTEST_704ILR ||
                WindowState != FormWindowState.Normal || (int)m_704ILR.Result != HTCLIENT_704ILR)
                return;

            int zona_704ILR = ZonaDeBorde_704ILR(m_704ILR.LParam);
            if (zona_704ILR != HTCLIENT_704ILR) m_704ILR.Result = (IntPtr)zona_704ILR;
        }

        // Codigo de zona de un punto de pantalla (el lParam de WM_NCHITTEST): el borde o
        // la esquina si cae en la franja de redimension, HTCLIENT si no.
        private int ZonaDeBorde_704ILR(IntPtr lParam_704ILR)
        {
            int lp_704ILR = unchecked((int)lParam_704ILR.ToInt64());
            Point p_704ILR = PointToClient(new Point(unchecked((short)lp_704ILR),
                                                     unchecked((short)(lp_704ILR >> 16))));
            if (!ClientRectangle.Contains(p_704ILR)) return HTCLIENT_704ILR;
            bool izq_704ILR = p_704ILR.X <= BordeRedimension_704ILR;
            bool der_704ILR = p_704ILR.X >= ClientSize.Width - BordeRedimension_704ILR;
            bool arr_704ILR = p_704ILR.Y <= BordeRedimension_704ILR;
            bool aba_704ILR = p_704ILR.Y >= ClientSize.Height - BordeRedimension_704ILR;

            return
                arr_704ILR && izq_704ILR ? HTTOPLEFT_704ILR :
                arr_704ILR && der_704ILR ? HTTOPRIGHT_704ILR :
                aba_704ILR && izq_704ILR ? HTBOTTOMLEFT_704ILR :
                aba_704ILR && der_704ILR ? HTBOTTOMRIGHT_704ILR :
                izq_704ILR ? HTLEFT_704ILR :
                der_704ILR ? HTRIGHT_704ILR :
                arr_704ILR ? HTTOP_704ILR :
                aba_704ILR ? HTBOTTOM_704ILR : HTCLIENT_704ILR;
        }

        // Los controles acoplados de una ventana sin borde cubren toda su area cliente, y
        // Windows le consulta la zona del cursor (WM_NCHITTEST) a la ventana hija que esta
        // debajo: el formulario nunca recibia la consulta de su propio borde y no se podia
        // redimensionar con el mouse. Cada descendiente responde "transparente" dentro de
        // la franja de redimension y Windows le pasa la consulta a su contenedor, hasta
        // llegar al formulario, que devuelve la zona del borde. Fuera de la franja, con la
        // ventana maximizada o sin redimension, los controles responden como siempre y el
        // layout no cambia.
        private readonly ConditionalWeakTable<Control, FranjaDeBorde_704ILR> _franjas_704ILR =
            new ConditionalWeakTable<Control, FranjaDeBorde_704ILR>();

        protected override void OnControlAdded(ControlEventArgs e_704ILR)
        {
            base.OnControlAdded(e_704ILR);
            if (_redimensionable_704ILR) AtenderFranja_704ILR(e_704ILR.Control);
        }

        // Suma el control, sus descendientes y los que se le agreguen despues.
        // Ni el manejador ni el gancho guardan una referencia fuerte al formulario: un
        // control que sobrevive a la ventana (por ejemplo, el origen de un menu contextual
        // que nadie libero) no puede mantener vivo al formulario cerrado.
        private void AtenderFranja_704ILR(Control c_704ILR)
        {
            if (c_704ILR == null) return;
            if (c_704ILR != this)
            {
                if (_franjas_704ILR.TryGetValue(c_704ILR, out _)) return;
                _franjas_704ILR.Add(c_704ILR, new FranjaDeBorde_704ILR(this, c_704ILR));
                c_704ILR.ControlAdded += HijoAgregado_704ILR;
            }
            foreach (Control hijo_704ILR in c_704ILR.Controls) AtenderFranja_704ILR(hijo_704ILR);
        }

        private static void HijoAgregado_704ILR(object s_704ILR, ControlEventArgs e_704ILR)
        {
            if ((s_704ILR as Control)?.FindForm() is FormBase_704ILR form_704ILR && form_704ILR._redimensionable_704ILR)
                form_704ILR.AtenderFranja_704ILR(e_704ILR.Control);
        }

        private bool EnFranjaDeRedimension_704ILR(IntPtr lParam_704ILR) =>
            _redimensionable_704ILR && IsHandleCreated && WindowState == FormWindowState.Normal &&
            ZonaDeBorde_704ILR(lParam_704ILR) != HTCLIENT_704ILR;

        // Engancha la ventana de un control descendiente para responder HTTRANSPARENT en la
        // franja de redimension del formulario; el resto de los mensajes siguen su curso.
        private sealed class FranjaDeBorde_704ILR : NativeWindow
        {
            private readonly WeakReference<FormBase_704ILR> _form_704ILR;
            private readonly Control _control_704ILR;

            public FranjaDeBorde_704ILR(FormBase_704ILR form_704ILR, Control control_704ILR)
            {
                _form_704ILR = new WeakReference<FormBase_704ILR>(form_704ILR);
                _control_704ILR = control_704ILR;
                // La suscripcion mantiene vivo este objeto mientras viva el control, y si el
                // control vuelve a crear su ventana se engancha a la nueva.
                control_704ILR.HandleCreated += VentanaCreada_704ILR;
                if (control_704ILR.IsHandleCreated) AssignHandle(control_704ILR.Handle);
            }

            private void VentanaCreada_704ILR(object s_704ILR, EventArgs e_704ILR)
            {
                if (Handle == _control_704ILR.Handle) return;
                if (Handle != IntPtr.Zero) ReleaseHandle();
                AssignHandle(_control_704ILR.Handle);
            }

            protected override void WndProc(ref Message m_704ILR)
            {
                if (m_704ILR.Msg == WM_NCHITTEST_704ILR && _form_704ILR.TryGetTarget(out FormBase_704ILR form_704ILR) &&
                    _control_704ILR.TopLevelControl == form_704ILR && form_704ILR.EnFranjaDeRedimension_704ILR(m_704ILR.LParam))
                {
                    m_704ILR.Result = (IntPtr)HTTRANSPARENT_704ILR;
                    return;
                }
                base.WndProc(ref m_704ILR);
            }
        }

        // Permite arrastrar la ventana tomando el control indicado (barra de titulo).
        public void EnableDrag_704ILR(Control c_704ILR)
        {
            c_704ILR.MouseDown += (s_704ILR, e_704ILR) =>
            {
                if (e_704ILR.Button == MouseButtons.Left)
                {
                    ReleaseCapture_704ILR();
                    SendMessage_704ILR(Handle, WM_NCLBUTTONDOWN_704ILR,
                        new IntPtr(HT_CAPTION_704ILR), IntPtr.Zero);
                }
            };
        }

        // Boton de ventana (minimizar / cerrar) con glifo Segoe MDL2. Si es de
        // cierre (danger), el hover se pinta rojo; si no, gris oscuro.
        protected Label WindowButton_704ILR(string glyph_704ILR, EventHandler onClick_704ILR, bool danger_704ILR = false)
        {
            var l_704ILR = new Label
            {
                Text = glyph_704ILR,
                Font = Theme_704ILR.FontWinCtl_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(44, 30),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            l_704ILR.MouseEnter += (s_704ILR, e_704ILR) => { l_704ILR.BackColor = danger_704ILR ? Theme_704ILR.Error_704ILR : Theme_704ILR.SidebarHover_704ILR; l_704ILR.ForeColor = Color.White; };
            l_704ILR.MouseLeave += (s_704ILR, e_704ILR) => { l_704ILR.BackColor = Color.Transparent; l_704ILR.ForeColor = Theme_704ILR.TextLight_704ILR; };
            l_704ILR.Click += onClick_704ILR;
            return l_704ILR;
        }
    }
}
