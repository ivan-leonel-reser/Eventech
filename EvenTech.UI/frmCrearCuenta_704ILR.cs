using System;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Alta de usuario accesible desde el login (link "¿No tenes cuenta? Crear").
    // Borderless con la identidad de marca de frmLogin (azul oscuro + dorado).
    // Layout por TableLayoutPanel/Dock (DPI-aware, sin coordenadas magicas) e
    // implementa el patron Observer para re-traducir labels al cambiar de idioma.
    public class frmCrearCuenta_704ILR : FormBase_704ILR, IObservadorIdioma_704ILR
    {
        // Tema de la ayuda en linea de esta ventana (F1).
        protected override string TemaAyuda_704ILR => Ayuda_704ILR.TemaPrimerosPasos_704ILR;

        private TextBox _txtUser_704ILR, _txtPass_704ILR, _txtPass2_704ILR;
        private Label _lblUser_704ILR, _lblPass_704ILR, _lblPass2_704ILR, _lblTitle_704ILR, _lblStatus_704ILR;
        private AppButton_704ILR _btnCrear_704ILR;

        // Ultimo mensaje de estado, guardado como la forma de armarlo (se re-traduce).
        private Func<string> _mensajeEstado_704ILR;

        public frmCrearCuenta_704ILR()
        {
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            FormClosed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
            Shown += (s_704ILR, e_704ILR) => _txtUser_704ILR.Focus();
        }

        private void BuildUi_704ILR()
        {
            // Con texto por defecto, como las leyendas: si la carga de idiomas sigue fallando, el
            // titulo de la ventana no muestra la clave cruda.
            Text = "EvenTech - " + T_704ILR("CC_TITULO", "Crear cuenta");
            ClientSize = new Size(420, 660); // misma altura que el login para cubrirlo por completo
            BackColor = Theme_704ILR.BgLogin_704ILR;
            KeyPreview = true;

            // ---------------- Barra de titulo ----------------
            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);

            _lblTitle_704ILR = new Label
            {
                Font = Theme_704ILR.FontH2_704ILR,
                ForeColor = Theme_704ILR.Accent_704ILR,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0),
                BackColor = Color.Transparent
            };

            // Dock=Right: reserva su lugar a la derecha sin que el titulo (Dock=Fill)
            // lo tape (antes quedaba oculto e impedia cerrar la ventana).
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => Close(), danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;

            pnlTitle_704ILR.Controls.Add(_lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            // ---------------- Cuerpo ----------------
            var body_704ILR = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme_704ILR.BgLogin_704ILR,
                Padding = new Padding(44, 26, 44, 22)
            };

            var tbl_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                BackColor = Color.Transparent
            };
            tbl_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            // La fila 6 (mensaje de estado) crece con el texto, con 42 px de alto minimo
            // en el rotulo: con alto fijo, un mensaje de mas de dos lineas quedaba cortado.
            int[] heights_704ILR = { 64, 60, 60, 60, 8, 50, /*auto*/ 42, /*fill*/ 0 };
            for (int i_704ILR = 0; i_704ILR < heights_704ILR.Length; i_704ILR++)
                tbl_704ILR.RowStyles.Add(i_704ILR == 7
                    ? new RowStyle(SizeType.Percent, 100)
                    : i_704ILR == 6
                        ? new RowStyle(SizeType.AutoSize)
                        : new RowStyle(SizeType.Absolute, heights_704ILR[i_704ILR]));

            var lblLogo_704ILR = new Label
            {
                Text = "EvenTech",
                Font = Theme_704ILR.FontDisplay_704ILR,
                ForeColor = Theme_704ILR.Accent_704ILR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            // Campos oscuros estilo login; se capturan los caption Label para re-traducir.
            var userField_704ILR  = Ui_704ILR.DarkField_704ILR("Usuario", false, out _txtUser_704ILR,  out _lblUser_704ILR);
            var passField_704ILR  = Ui_704ILR.DarkField_704ILR("Contraseña", true, out _txtPass_704ILR,  out _lblPass_704ILR);
            var pass2Field_704ILR = Ui_704ILR.DarkField_704ILR("Repetir contraseña", true, out _txtPass2_704ILR, out _lblPass2_704ILR);

            _btnCrear_704ILR = Ui_704ILR.Primary_704ILR("Crear cuenta");
            _btnCrear_704ILR.Dock = DockStyle.Fill;
            _btnCrear_704ILR.BehindColor_704ILR = Theme_704ILR.BgLogin_704ILR;
            _btnCrear_704ILR.Margin = new Padding(0);
            _btnCrear_704ILR.Click += BtnCrear_Click_704ILR;

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
            tbl_704ILR.Controls.Add(userField_704ILR, 0, 1);
            tbl_704ILR.Controls.Add(passField_704ILR, 0, 2);
            tbl_704ILR.Controls.Add(pass2Field_704ILR, 0, 3);
            tbl_704ILR.Controls.Add(new Panel { BackColor = Color.Transparent }, 0, 4); // spacer
            tbl_704ILR.Controls.Add(_btnCrear_704ILR, 0, 5);
            tbl_704ILR.Controls.Add(_lblStatus_704ILR, 0, 6);
            tbl_704ILR.Controls.Add(new Panel { BackColor = Color.Transparent }, 0, 7); // fill

            body_704ILR.Controls.Add(tbl_704ILR);

            Controls.Add(body_704ILR);
            Controls.Add(pnlTitle_704ILR);

            AcceptButton = _btnCrear_704ILR;
        }

        private void BtnCrear_Click_704ILR(object sender_704ILR, EventArgs e_704ILR)
        {
            string user_704ILR = _txtUser_704ILR.Text.Trim();
            string p1_704ILR = _txtPass_704ILR.Text;
            string p2_704ILR = _txtPass2_704ILR.Text;

            if (string.IsNullOrWhiteSpace(user_704ILR) || string.IsNullOrEmpty(p1_704ILR))
            {
                SetStatus_704ILR(() => T_704ILR("CC_MSG_COMPLETAR", "Completar todos los campos."), error_704ILR: true);
                return;
            }
            if (p1_704ILR != p2_704ILR)
            {
                SetStatus_704ILR(() => T_704ILR("CC_MSG_NO_COINCIDEN", "Las contraseñas no coinciden."), error_704ILR: true);
                return;
            }
            if (p1_704ILR.Length < 4)
            {
                SetStatus_704ILR(() => T_704ILR("CC_MSG_PASS_CORTA", "La contraseña debe tener al menos 4 caracteres."), error_704ILR: true);
                return;
            }

            string hash_704ILR = Encrypt_704ILR.HashValue_704ILR(p1_704ILR);
            _txtPass_704ILR.Clear();
            _txtPass2_704ILR.Clear();

            try
            {
                var r_704ILR = BLL_User_704ILR.CreateUser_704ILR(user_704ILR, hash_704ILR);
                switch (r_704ILR)
                {
                    case CreateUserResult_704ILR.Success_704ILR:
                        SetStatus_704ILR(() => T_704ILR("CC_MSG_OK", "Usuario creado. Ya podés iniciar sesión."), error_704ILR: false);
                        _txtUser_704ILR.Clear();
                        break;
                    case CreateUserResult_704ILR.InvalidUsername_704ILR:
                        SetStatus_704ILR(() => T_704ILR("CC_MSG_USER_INVALIDO", "Usuario inválido (3-50, letras/números/._-)."), error_704ILR: true);
                        break;
                    case CreateUserResult_704ILR.UsernameAlreadyExists_704ILR:
                        SetStatus_704ILR(() => T_704ILR("CC_MSG_USER_EXISTE", "Ese usuario ya existe."), error_704ILR: true);
                        break;
                    case CreateUserResult_704ILR.InvalidPassword_704ILR:
                        SetStatus_704ILR(() => T_704ILR("CC_MSG_PASS_INVALIDA", "Contraseña inválida."), error_704ILR: true);
                        break;
                }
            }
            catch (Exception ex_704ILR)
            {
                // El detalle tecnico va a la bitacora. En pantalla, un mensaje traducido: el
                // texto del motor salia en ingles, con el nombre de la base y la cuenta de
                // Windows, y no entraba en el rotulo. La causa se clasifica como en el resto de
                // las pantallas (Tr_704ILR.MensajeExcepcion_704ILR): el aviso propio del alta
                // queda para cuando no se pudo acceder a la base; un alta frenada por un bloqueo
                // de otra estacion, con la base en linea, es una operacion no completada.
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "CrearCuenta", "Alta de usuario");
                SetStatus_704ILR(() => Tr_704ILR.EsSinBase_704ILR(ex_704ILR)
                    ? T_704ILR("CC_MSG_ERR_SIN_BASE", "La cuenta no se creó: no se pudo acceder a la base de datos. Reintentá más tarde.")
                    : Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), error_704ILR: true);
            }
        }

        private void SetStatus_704ILR(Func<string> mensaje_704ILR, bool error_704ILR)
        {
            _mensajeEstado_704ILR = mensaje_704ILR;
            _lblStatus_704ILR.ForeColor = error_704ILR ? Color.FromArgb(255, 170, 170) : Color.FromArgb(170, 235, 170);
            _lblStatus_704ILR.Text = mensaje_704ILR();
        }

        // Devuelve la traduccion de 'clave' o, si falta, el texto por defecto dado.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        // Observador (patron Observer): re-traduce titulo, captions y boton. Todas llevan texto
        // por defecto, como el login: si la carga de idiomas sigue fallando al abrir el alta (el
        // reintento no alcanza con la base caida o una tabla bloqueada), la ventana se ve en
        // castellano y no con las claves crudas.
        public void ActualizarTextos_704ILR()
        {
            if (_lblTitle_704ILR != null) _lblTitle_704ILR.Text = T_704ILR("CC_TITULO", "Crear cuenta");
            if (_lblUser_704ILR != null)  _lblUser_704ILR.Text  = T_704ILR("CC_USER", "Usuario");
            if (_lblPass_704ILR != null)  _lblPass_704ILR.Text  = T_704ILR("CC_PASS", "Contraseña");
            if (_lblPass2_704ILR != null) _lblPass2_704ILR.Text = T_704ILR("CC_PASS2", "Repetir contraseña");
            if (_btnCrear_704ILR != null) _btnCrear_704ILR.Text = T_704ILR("CC_CREAR", "Crear");
            // El mensaje de estado a la vista se vuelve a armar en el idioma nuevo.
            if (_lblStatus_704ILR != null) _lblStatus_704ILR.Text = _mensajeEstado_704ILR?.Invoke() ?? "";
        }
    }
}
