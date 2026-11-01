using System;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Popup para dar de alta un cliente (Proceso 1). Reusable desde la ficha de
    // reservas ("Nuevo cliente") y desde la seccion Clientes. Devuelve OK + NuevoId.
    public class frmNuevoCliente_704ILR : FormBase_704ILR
    {
        // Tema de la ayuda en linea de esta ventana (F1).
        protected override string TemaAyuda_704ILR => Ayuda_704ILR.TemaClientes_704ILR;

        private TextBox _txtNombre_704ILR, _txtApellido_704ILR, _txtDni_704ILR, _txtEmail_704ILR, _txtTelefono_704ILR;
        private Label _lblMsg_704ILR;
        // La fila del mensaje crece con el texto (un mensaje de dos lineas se cortaba en la
        // fila fija de 22 px) y el popup se agranda lo mismo para no tapar los botones.
        private TableLayoutPanel _body_704ILR;
        private int _altoBase_704ILR;

        public int NuevoId_704ILR { get; private set; }

        public frmNuevoCliente_704ILR()
        {
            BuildUi_704ILR();
            Shown += (s_704ILR, e_704ILR) => _txtNombre_704ILR.Focus();
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(470, 320);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblTitle_704ILR = new Label
            {
                Text = T_704ILR("CLI_NUEVO", "Nuevo cliente"),
                Font = Theme_704ILR.FontH2_704ILR, ForeColor = Theme_704ILR.TextOnDark_704ILR, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0), BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblTitle_704ILR);
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); }, danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            var body_704ILR = _body_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR)
            };
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _txtNombre_704ILR = Ui_704ILR.Input_704ILR(); _txtNombre_704ILR.MaxLength = 60;
            _txtApellido_704ILR = Ui_704ILR.Input_704ILR(); _txtApellido_704ILR.MaxLength = 60;
            _txtDni_704ILR = Ui_704ILR.Input_704ILR(); _txtDni_704ILR.MaxLength = 20;
            _txtEmail_704ILR = Ui_704ILR.Input_704ILR(); _txtEmail_704ILR.MaxLength = 120;
            _txtTelefono_704ILR = Ui_704ILR.Input_704ILR(); _txtTelefono_704ILR.MaxLength = 30;

            var fNombre_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("COL_NOMBRE", "Nombre"), _txtNombre_704ILR); fNombre_704ILR.Dock = DockStyle.Fill; fNombre_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            var fApellido_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("COL_APELLIDO", "Apellido"), _txtApellido_704ILR); fApellido_704ILR.Dock = DockStyle.Fill; fApellido_704ILR.Margin = new Padding(0);
            var fDni_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("COL_DNI", "DNI"), _txtDni_704ILR); fDni_704ILR.Dock = DockStyle.Fill; fDni_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            var fEmail_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("COL_EMAIL", "Email"), _txtEmail_704ILR); fEmail_704ILR.Dock = DockStyle.Fill; fEmail_704ILR.Margin = new Padding(0);
            var fTel_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("COL_TELEFONO", "Teléfono"), _txtTelefono_704ILR); fTel_704ILR.Dock = DockStyle.Fill; fTel_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);

            _lblMsg_704ILR = new Label { AutoSize = true, MinimumSize = new Size(0, 22), Anchor = AnchorStyles.Left, Margin = new Padding(0), Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.Error_704ILR, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };

            var acciones_704ILR = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0) };
            var btnCrear_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("CLI_NUEVO", "Nuevo cliente"), Theme_704ILR.IcoSave_704ILR); btnCrear_704ILR.Text = T_704ILR("BTN_GUARDAR", "Guardar"); btnCrear_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnCrear_704ILR.Size = new Size(140, 38); btnCrear_704ILR.Click += (s_704ILR, e_704ILR) => Crear_704ILR();
            var btnCancelar_704ILR = Ui_704ILR.Secondary_704ILR(T_704ILR("BTN_CANCELAR", "Cancelar")); btnCancelar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnCancelar_704ILR.Size = new Size(110, 38); btnCancelar_704ILR.Margin = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, 0, 0); btnCancelar_704ILR.Click += (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); };
            acciones_704ILR.Controls.Add(btnCrear_704ILR); acciones_704ILR.Controls.Add(btnCancelar_704ILR);

            body_704ILR.Controls.Add(fNombre_704ILR, 0, 0); body_704ILR.Controls.Add(fApellido_704ILR, 1, 0);
            body_704ILR.Controls.Add(fDni_704ILR, 0, 1); body_704ILR.Controls.Add(fEmail_704ILR, 1, 1);
            body_704ILR.Controls.Add(fTel_704ILR, 0, 2);
            body_704ILR.Controls.Add(_lblMsg_704ILR, 0, 3); body_704ILR.SetColumnSpan(_lblMsg_704ILR, 2);
            body_704ILR.Controls.Add(acciones_704ILR, 0, 4); body_704ILR.SetColumnSpan(acciones_704ILR, 2);

            Controls.Add(body_704ILR);
            Controls.Add(pnlTitle_704ILR);
            AcceptButton = btnCrear_704ILR;
        }

        private void Crear_704ILR()
        {
            try
            {
                var c_704ILR = new BE_Cliente_704ILR
                {
                    Nombre_704ILR = _txtNombre_704ILR.Text.Trim(),
                    Apellido_704ILR = _txtApellido_704ILR.Text.Trim(),
                    Dni_704ILR = _txtDni_704ILR.Text.Trim(),
                    Email_704ILR = _txtEmail_704ILR.Text.Trim(),
                    Telefono_704ILR = _txtTelefono_704ILR.Text.Trim()
                };
                ClienteResult_704ILR r_704ILR = BLL_Cliente_704ILR.Crear_704ILR(c_704ILR, out int id_704ILR);
                if (r_704ILR != ClienteResult_704ILR.Success_704ILR) { MostrarMensaje_704ILR(MensajeError_704ILR(r_704ILR)); return; }
                NuevoId_704ILR = id_704ILR;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Clientes", "Crear cliente (popup)");
                // Una falla tecnica se informa en un cuadro de mensaje, sin el texto del
                // motor si la causa es la base (el detalle queda en la bitacora), y el popup
                // sigue abierto con lo que se cargo.
                MostrarMensaje_704ILR("");
                MessageBox.Show(this, Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), Tr_704ILR.T_704ILR("MSG_ERROR"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string MensajeError_704ILR(ClienteResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case ClienteResult_704ILR.NombreInvalido_704ILR: return T_704ILR("MSG_CLI_NOMBRE", "Ingrese el nombre del cliente.");
                case ClienteResult_704ILR.DniDuplicado_704ILR:   return T_704ILR("MSG_CLI_DNI_DUP", "Ya existe un cliente con ese DNI.");
                case ClienteResult_704ILR.EmailInvalido_704ILR:  return T_704ILR("MSG_CLI_EMAIL", "El email no es válido.");
                case ClienteResult_704ILR.DniInvalido_704ILR:    return T_704ILR("MSG_CLI_DNI_INVALIDO", "El DNI no es válido: use solo números (7 dígitos o más).");
                case ClienteResult_704ILR.LongitudExcedida_704ILR: return T_704ILR("MSG_CLI_LARGO", "Dato muy largo: nombre y apellido 60, email 120, teléfono 30.");
                default:                           return Tr_704ILR.T_704ILR("MSG_ERROR");
            }
        }

        private void MostrarMensaje_704ILR(string texto_704ILR)
        {
            if (_altoBase_704ILR == 0) _altoBase_704ILR = ClientSize.Height;
            _lblMsg_704ILR.MaximumSize = new Size(Math.Max(1, _body_704ILR.ClientSize.Width - _body_704ILR.Padding.Horizontal), 0);
            _lblMsg_704ILR.Text = texto_704ILR ?? string.Empty;
            _body_704ILR.PerformLayout();
            ClientSize = new Size(ClientSize.Width, _altoBase_704ILR + Math.Max(0, _lblMsg_704ILR.Height - _lblMsg_704ILR.MinimumSize.Height));
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }
}
