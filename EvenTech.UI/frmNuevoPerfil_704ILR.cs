using System;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Popup para dar de alta un perfil (nombre + descripcion). Reusa
    // BLL_Perfil.CrearPerfil. Devuelve DialogResult.OK y el Id en NuevoId.
    public class frmNuevoPerfil_704ILR : FormBase_704ILR
    {
        // Tema de la ayuda en linea de esta ventana (F1).
        protected override string TemaAyuda_704ILR => Ayuda_704ILR.TemaPerfiles_704ILR;

        private TextBox _txtNombre_704ILR, _txtDesc_704ILR;
        private Label _lblMsg_704ILR;
        // La fila del mensaje crece con el texto (un mensaje de dos lineas se cortaba en la
        // fila fija de 22 px) y el popup se agranda lo mismo para no tapar los botones.
        private TableLayoutPanel _body_704ILR;
        private int _altoBase_704ILR;

        public int NuevoId_704ILR { get; private set; }

        public frmNuevoPerfil_704ILR()
        {
            BuildUi_704ILR();
            Shown += (s_704ILR, e_704ILR) => _txtNombre_704ILR.Focus();
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(380, 252);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblTitle_704ILR = new Label
            {
                Text = T_704ILR("PERF_NUEVO", "Nuevo perfil"),
                Font = Theme_704ILR.FontH2_704ILR,
                ForeColor = Theme_704ILR.TextOnDark_704ILR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0),
                BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblTitle_704ILR);
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); }, danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            var body_704ILR = _body_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR)
            };
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _txtNombre_704ILR = Ui_704ILR.Input_704ILR(); _txtNombre_704ILR.MaxLength = 80;
            var fNombre_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("IDI_NOMBRE", "Nombre"), _txtNombre_704ILR); fNombre_704ILR.Dock = DockStyle.Fill; fNombre_704ILR.Margin = new Padding(0);
            _txtDesc_704ILR = Ui_704ILR.Input_704ILR(); _txtDesc_704ILR.MaxLength = 250;
            var fDesc_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("PERF_DESC", "Descripción"), _txtDesc_704ILR); fDesc_704ILR.Dock = DockStyle.Fill; fDesc_704ILR.Margin = new Padding(0);
            _lblMsg_704ILR = new Label { AutoSize = true, MinimumSize = new Size(0, 22), Anchor = AnchorStyles.Left, Margin = new Padding(0), Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.Error_704ILR, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };

            var acciones_704ILR = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0) };
            var btnCrear_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("PERF_CREAR", "Crear perfil"), Theme_704ILR.IcoAdd_704ILR); btnCrear_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnCrear_704ILR.Size = new Size(150, 38); btnCrear_704ILR.Click += (s_704ILR, e_704ILR) => Crear_704ILR();
            var btnCancelar_704ILR = Ui_704ILR.Secondary_704ILR(T_704ILR("BTN_CANCELAR", "Cancelar")); btnCancelar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnCancelar_704ILR.Size = new Size(110, 38); btnCancelar_704ILR.Margin = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, 0, 0); btnCancelar_704ILR.Click += (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); };
            acciones_704ILR.Controls.Add(btnCrear_704ILR); acciones_704ILR.Controls.Add(btnCancelar_704ILR);

            body_704ILR.Controls.Add(fNombre_704ILR, 0, 0);
            body_704ILR.Controls.Add(fDesc_704ILR, 0, 1);
            body_704ILR.Controls.Add(_lblMsg_704ILR, 0, 2);
            body_704ILR.Controls.Add(acciones_704ILR, 0, 3);
            Controls.Add(body_704ILR);
            Controls.Add(pnlTitle_704ILR);
            AcceptButton = btnCrear_704ILR;
        }

        private void Crear_704ILR()
        {
            try
            {
                PerfilResult_704ILR r_704ILR = BLL_Perfil_704ILR.CrearPerfil_704ILR(_txtNombre_704ILR.Text, _txtDesc_704ILR.Text, out int id_704ILR);
                if (r_704ILR != PerfilResult_704ILR.Success_704ILR) { MostrarMensaje_704ILR(MensajeError_704ILR(r_704ILR)); return; }
                NuevoId_704ILR = id_704ILR;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Perfiles", "Crear perfil (popup)");
                MostrarMensaje_704ILR(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        private static string MensajeError_704ILR(PerfilResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case PerfilResult_704ILR.NombreInvalido_704ILR:  return T_704ILR("MSG_PERF_NOM_INV", "Ingrese el nombre del perfil.");
                case PerfilResult_704ILR.NombreDuplicado_704ILR: return T_704ILR("MSG_PERF_DUP", "Ya existe un perfil con ese nombre.");
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
