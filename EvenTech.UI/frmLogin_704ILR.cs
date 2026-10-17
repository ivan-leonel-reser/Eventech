using System;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Pantalla de login. Borderless con identidad de marca (azul oscuro + dorado).
    // Layout 100% por TableLayoutPanel/Dock (DPI-aware, sin coordenadas magicas).
    // El loop principal vive aca: al validar credenciales abre frmMain modal y al
    // volver del logout queda esperando otro login. El boton cerrar termina la app.
    // El selector de idioma (+ alta rapida) vive en el pie, abajo a la derecha.
    public class frmLogin_704ILR : FormBase_704ILR, IObservadorIdioma_704ILR
    {
        private TextBox _txtUser_704ILR, _txtPass_704ILR;
        private Label _lblUser_704ILR, _lblPass_704ILR, _lblTagline_704ILR, _lblStatus_704ILR, _lblCrearCuenta_704ILR, _lblVersion_704ILR;
        private CheckBox _chkRemember_704ILR;
        private AppButton_704ILR _btnLogin_704ILR;

        // Ultimo mensaje de estado, guardado como la forma de armarlo y no como el texto
        // ya traducido: al cambiar de idioma se vuelve a armar en el idioma nuevo.
        private Func<string> _mensajeEstado_704ILR;

        public frmLogin_704ILR()
        {
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            FormClosed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);

            LoginPrefs_704ILR.Load_704ILR();
            AplicarCuentaRecordada_704ILR();
            Shown += (s_704ILR, e_704ILR) => EnfocarCampoInicial_704ILR();
        }

        // "Recordar cuenta": precarga el usuario guardado (nunca la contrasena). Corre al
        // abrir la pantalla y cada vez que se vuelve de la ventana principal, porque la
        // misma instancia de frmLogin vive toda la ejecucion.
        private void AplicarCuentaRecordada_704ILR()
        {
            if (LoginPrefs_704ILR.Remember_704ILR)
            {
                _txtUser_704ILR.Text = LoginPrefs_704ILR.Username_704ILR;
                _chkRemember_704ILR.Checked = true;
            }
            else
            {
                _txtUser_704ILR.Clear();
                _chkRemember_704ILR.Checked = false;
            }
        }

        // Foco inicial: el usuario si esta vacio; si no, la contrasena. Se fija con la
        // ventana visible (con la ventana oculta, Focus no tiene efecto).
        private void EnfocarCampoInicial_704ILR()
        {
            ActiveControl = string.IsNullOrEmpty(_txtUser_704ILR.Text) ? (Control)_txtUser_704ILR : _txtPass_704ILR;
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(420, 660);
            BackColor = Theme_704ILR.BgLogin_704ILR;
            KeyPreview = true;

            // ---------------- Barra de titulo (minimizar / cerrar) ----------------
            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);

            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => Application.Exit(), danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            var btnMin_704ILR = WindowButton_704ILR(Theme_704ILR.IcoMinimize_704ILR, (s_704ILR, e_704ILR) => WindowState = FormWindowState.Minimized);
            btnMin_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(btnMin_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            // ---------------- Pie: link "crear" (izq) + selector de idioma (der) ----------------
            var footer_704ILR = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Theme_704ILR.BgLogin_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, 0, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceSm_704ILR)
            };
            var footerGrid_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            footerGrid_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footerGrid_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footerGrid_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _lblCrearCuenta_704ILR = new Label
            {
                Font = Theme_704ILR.FontBody_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            _lblCrearCuenta_704ILR.Click += LblCrearCuenta_Click_704ILR;
            _lblCrearCuenta_704ILR.MouseEnter += (s_704ILR, e_704ILR) => _lblCrearCuenta_704ILR.ForeColor = Theme_704ILR.Accent_704ILR;
            _lblCrearCuenta_704ILR.MouseLeave += (s_704ILR, e_704ILR) => _lblCrearCuenta_704ILR.ForeColor = Theme_704ILR.TextLight_704ILR;

            var lang_704ILR = new LangSelector_704ILR(dark_704ILR: true, allowManage_704ILR: false) { Anchor = AnchorStyles.Right };

            footerGrid_704ILR.Controls.Add(_lblCrearCuenta_704ILR, 0, 0);
            footerGrid_704ILR.Controls.Add(lang_704ILR, 1, 0);
            footer_704ILR.Controls.Add(footerGrid_704ILR);

            // ---------------- Cuerpo ----------------
            var body_704ILR = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme_704ILR.BgLogin_704ILR,
                Padding = new Padding(44, 26, 44, 12)
            };

            var tbl_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 10,
                BackColor = Color.Transparent
            };
            tbl_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            // La fila 8 (mensaje de estado) crece con el texto: con alto fijo, un mensaje
            // de mas de dos lineas (una traduccion editada, por ejemplo) quedaba cortado.
            // El rotulo conserva 42 px de alto minimo, asi que el estado normal no cambia.
            int[] heights_704ILR = { 64, 22, 22, 60, 60, 30, 8, 50, /*auto*/ 42, /*fill*/ 0 };
            for (int i_704ILR = 0; i_704ILR < heights_704ILR.Length; i_704ILR++)
                tbl_704ILR.RowStyles.Add(i_704ILR == 9
                    ? new RowStyle(SizeType.Percent, 100)
                    : i_704ILR == 8
                        ? new RowStyle(SizeType.AutoSize)
                        : new RowStyle(SizeType.Absolute, heights_704ILR[i_704ILR]));

            // El isologotipo encabeza la pantalla de acceso (G05). A este tamano se
            // usa la variante compacta (isotipo + EVENTECH, sin lema): el lema no se
            // lee a 64 px de alto. Sin el recurso, el rotulo de texto cumple la misma
            // funcion.
            Control lblLogo_704ILR;
            if (Theme_704ILR.Logo_704ILR != null)
            {
                lblLogo_704ILR = new PictureBox
                {
                    Image = Theme_704ILR.LogoCompacto_704ILR ?? Theme_704ILR.Logo_704ILR,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent
                };
            }
            else
            {
                lblLogo_704ILR = new Label
                {
                    Text = "EvenTech",
                    Font = Theme_704ILR.FontDisplay_704ILR,
                    ForeColor = Theme_704ILR.Accent_704ILR,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
            }
            _lblTagline_704ILR = new Label
            {
                Font = Theme_704ILR.FontSmall_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var userField_704ILR = Ui_704ILR.DarkField_704ILR("Usuario", false, out _txtUser_704ILR, out _lblUser_704ILR);
            var passField_704ILR = Ui_704ILR.DarkField_704ILR("Contraseña", true, out _txtPass_704ILR, out _lblPass_704ILR);

            // BackColor solido (no Transparent) + FlatStyle.Standard: evita el
            // artefacto por el que el check "desaparecia" al marcarlo sobre el panel.
            _chkRemember_704ILR = new CheckBox
            {
                Text = "Recordar cuenta",
                Font = Theme_704ILR.FontSmall_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                FlatStyle = FlatStyle.Standard,
                BackColor = Theme_704ILR.BgLogin_704ILR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _btnLogin_704ILR = Ui_704ILR.Primary_704ILR("Ingresar");
            _btnLogin_704ILR.Dock = DockStyle.Fill;
            _btnLogin_704ILR.BehindColor_704ILR = Theme_704ILR.BgLogin_704ILR;
            _btnLogin_704ILR.Margin = new Padding(0);
            _btnLogin_704ILR.Click += BtnLogin_Click_704ILR;

            _lblStatus_704ILR = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                ForeColor = Color.FromArgb(255, 170, 170),
                Font = Theme_704ILR.FontSmall_704ILR,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            tbl_704ILR.Controls.Add(lblLogo_704ILR, 0, 0);
            tbl_704ILR.Controls.Add(_lblTagline_704ILR, 0, 1);
            tbl_704ILR.Controls.Add(new Panel { BackColor = Color.Transparent }, 0, 2); // spacer
            tbl_704ILR.Controls.Add(userField_704ILR, 0, 3);
            tbl_704ILR.Controls.Add(passField_704ILR, 0, 4);
            tbl_704ILR.Controls.Add(_chkRemember_704ILR, 0, 5);
            tbl_704ILR.Controls.Add(new Panel { BackColor = Color.Transparent }, 0, 6); // spacer
            tbl_704ILR.Controls.Add(_btnLogin_704ILR, 0, 7);
            tbl_704ILR.Controls.Add(_lblStatus_704ILR, 0, 8);
            // Version y commit, al pie del espacio libre: se ven antes de ingresar, para
            // comparar el programa con el commit del repositorio.
            _lblVersion_704ILR = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                Font = Theme_704ILR.FontSmall_704ILR,
                TextAlign = ContentAlignment.BottomCenter,
                BackColor = Color.Transparent
            };
            tbl_704ILR.Controls.Add(_lblVersion_704ILR, 0, 9); // fill

            body_704ILR.Controls.Add(tbl_704ILR);

            Controls.Add(body_704ILR);
            Controls.Add(footer_704ILR);
            Controls.Add(pnlTitle_704ILR);

            AcceptButton = _btnLogin_704ILR;
        }

        private void BtnLogin_Click_704ILR(object sender_704ILR, EventArgs e_704ILR)
        {
            string username_704ILR = _txtUser_704ILR.Text.Trim();
            string plain_704ILR = _txtPass_704ILR.Text;

            if (string.IsNullOrWhiteSpace(username_704ILR) || string.IsNullOrEmpty(plain_704ILR))
            {
                SetError_704ILR(() => T_704ILR("LOGIN_COMPLETAR", "Completar usuario y contraseña."));
                return;
            }

            string hashed_704ILR = Encrypt_704ILR.HashValue_704ILR(plain_704ILR);
            _txtPass_704ILR.Clear();

            LoginResponse_704ILR resp_704ILR;
            try
            {
                resp_704ILR = BLL_Login_704ILR.Authenticate_704ILR(username_704ILR, hashed_704ILR);
            }
            catch (Exception ex_704ILR)
            {
                // El detalle tecnico va a la bitacora. En pantalla, un mensaje traducido: el
                // texto del motor salia en ingles, con el nombre de la base y la cuenta de
                // Windows, y no entraba en el rotulo. La causa se clasifica como en el resto de
                // las pantallas (Tr_704ILR.MensajeExcepcion_704ILR): el aviso propio del login
                // queda para cuando no se pudo acceder a la base; un ingreso frenado por un
                // bloqueo de otra estacion, con la base en linea, es una operacion no completada.
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Login", "Autenticacion");
                SetError_704ILR(() => Tr_704ILR.EsSinBase_704ILR(ex_704ILR)
                    ? T_704ILR("LOGIN_ERR_SIN_BASE", "No se pudo acceder a la base de datos. Reintentá o contactate con un administrador.")
                    : Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
                return;
            }

            switch (resp_704ILR.Result_704ILR)
            {
                case LoginResult_704ILR.Success_704ILR:
                    // Se recuerda el nombre de la cuenta y no lo tipeado ("ADMIN" -> "admin").
                    LoginPrefs_704ILR.Save_704ILR(_chkRemember_704ILR.Checked, SessionManager_704ILR.GetInstance_704ILR.User_704ILR.Username_704ILR);
                    // Si la carga de idiomas del arranque fallo, se reintenta antes de armar
                    // la ventana principal: la base acaba de responder a la autenticacion.
                    Program_704ILR.ReintentarIdiomas_704ILR("al entrar a la ventana principal");
                    Hide();
                    using (var main_704ILR = new frmMain_704ILR()) main_704ILR.ShowDialog();
                    RestablecerPantalla_704ILR();
                    break;
                case LoginResult_704ILR.IncorrectPassword_704ILR:
                {
                    // Mismo mensaje para usuario inexistente, contrasena incorrecta y cuenta
                    // bloqueada o inactiva sin la contrasena correcta: la pantalla no revela
                    // si el nombre existe. Muestra el intento: "... Intento 2 de 3."
                    int fallidos_704ILR = resp_704ILR.FailedAttempts_704ILR, maximo_704ILR = resp_704ILR.MaxAttempts_704ILR;
                    SetError_704ILR(() => T_704ILR("LOGIN_ERR_CREDENCIALES", "Usuario o contraseña incorrectos.") + " " +
                             Tr_704ILR.F_704ILR("LOGIN_INTENTOS", "Intento {0} de {1}.", fallidos_704ILR, maximo_704ILR));
                    break;
                }
                case LoginResult_704ILR.UserBlocked_704ILR:
                    SetError_704ILR(() => T_704ILR("LOGIN_BLOQUEADA", "Cuenta bloqueada. Contactate con un administrador."));
                    break;
                case LoginResult_704ILR.AccountInactive_704ILR:
                    SetError_704ILR(() => T_704ILR("LOGIN_INACTIVA", "La cuenta está inactiva. Contactate con un administrador."));
                    break;
            }
        }

        // Deja la pantalla lista para el proximo ingreso al volver de la ventana principal
        // (por "Cerrar sesion" o por la X). La misma instancia vive toda la ejecucion: lo
        // que no se restablece aca queda como lo dejo el usuario anterior.
        private void RestablecerPantalla_704ILR()
        {
            _txtPass_704ILR.Clear();
            Ui_704ILR.OcultarClave_704ILR(_txtPass_704ILR);   // el siguiente no tipea su clave a la vista
            _mensajeEstado_704ILR = null;
            _lblStatus_704ILR.Text = "";
            // Una carga de idiomas que siguiera pendiente se reintenta antes de volver a
            // mostrar la pantalla de acceso.
            Program_704ILR.ReintentarIdiomas_704ILR("al volver a la pantalla de acceso");
            AplicarCuentaRecordada_704ILR();
            Show();
            EnfocarCampoInicial_704ILR();
        }

        private void LblCrearCuenta_Click_704ILR(object sender_704ILR, EventArgs e_704ILR)
        {
            Program_704ILR.ReintentarIdiomas_704ILR("al abrir Crear cuenta");
            using (var alta_704ILR = new frmCrearCuenta_704ILR()) alta_704ILR.ShowDialog();
            _txtUser_704ILR.Focus();
        }

        private void SetError_704ILR(Func<string> mensaje_704ILR)
        {
            _mensajeEstado_704ILR = mensaje_704ILR;
            _lblStatus_704ILR.ForeColor = Color.FromArgb(255, 170, 170);
            _lblStatus_704ILR.Text = mensaje_704ILR();
        }

        // Devuelve la traduccion de 'clave' o, si falta, el texto por defecto dado.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        // Observador del patron Observer: traduce las leyendas del login. Todas llevan
        // texto por defecto: si la carga de idiomas del arranque fallo, la pantalla de
        // acceso se ve en castellano y no con las claves crudas mientras se reintenta.
        public void ActualizarTextos_704ILR()
        {
            if (_lblUser_704ILR != null)        _lblUser_704ILR.Text        = T_704ILR("LOGIN_USER", "Usuario");
            if (_lblPass_704ILR != null)        _lblPass_704ILR.Text        = T_704ILR("LOGIN_PASS", "Contraseña");
            if (_btnLogin_704ILR != null)       _btnLogin_704ILR.Text       = T_704ILR("LOGIN_ENTER", "Ingresar");
            if (_lblCrearCuenta_704ILR != null) _lblCrearCuenta_704ILR.Text = T_704ILR("LOGIN_CREATE", "¿No tenés cuenta? Crear");
            if (_lblTagline_704ILR != null)     _lblTagline_704ILR.Text     = T_704ILR("LOGIN_TAGLINE", "Gestión de eventos y reservas");
            if (_chkRemember_704ILR != null)    _chkRemember_704ILR.Text    = T_704ILR("LOGIN_REMEMBER", "Recordar cuenta");
            if (_lblVersion_704ILR != null)     _lblVersion_704ILR.Text     = Tr_704ILR.TextoVersion_704ILR();
            // El mensaje de estado a la vista se vuelve a armar en el idioma nuevo.
            if (_lblStatus_704ILR != null)      _lblStatus_704ILR.Text      = _mensajeEstado_704ILR?.Invoke() ?? "";
        }
    }
}
