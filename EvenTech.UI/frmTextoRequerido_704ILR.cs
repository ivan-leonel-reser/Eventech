using System.Drawing;
using System.Windows.Forms;

namespace EvenTech.UI
{
    // Dialogo chico que pide un texto obligatorio: el motivo con el que un empleado
    // rechaza un turno y la resolucion de una incidencia. Devuelve OK + Texto.
    public class frmTextoRequerido_704ILR : FormBase_704ILR
    {
        private TextBox _txt_704ILR;
        private Label _lblMsg_704ILR;
        private readonly string _avisoVacio_704ILR;

        public string Texto_704ILR { get; private set; }

        public frmTextoRequerido_704ILR(string titulo_704ILR, string etiqueta_704ILR, string avisoVacio_704ILR, int largoMaximo_704ILR)
        {
            _avisoVacio_704ILR = avisoVacio_704ILR;
            BuildUi_704ILR(titulo_704ILR, etiqueta_704ILR, largoMaximo_704ILR);
            Shown += (s_704ILR, e_704ILR) => _txt_704ILR.Focus();
        }

        private void BuildUi_704ILR(string titulo_704ILR, string etiqueta_704ILR, int largoMaximo_704ILR)
        {
            Text = "EvenTech";
            ClientSize = new Size(480, 270);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblTitle_704ILR = new Label
            {
                Text = titulo_704ILR,
                Font = Theme_704ILR.FontH2_704ILR, ForeColor = Theme_704ILR.TextOnDark_704ILR, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0), BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblTitle_704ILR);
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); }, danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            var body_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR)
            };
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl_704ILR = Ui_704ILR.FieldLabel_704ILR(etiqueta_704ILR);
            lbl_704ILR.Margin = new Padding(2, 0, 0, 2);
            _txt_704ILR = Ui_704ILR.Input_704ILR();
            _txt_704ILR.Multiline = true;
            _txt_704ILR.MaxLength = largoMaximo_704ILR;
            _txt_704ILR.Dock = DockStyle.Fill;
            _txt_704ILR.Margin = new Padding(0);

            _lblMsg_704ILR = new Label { AutoSize = true, MinimumSize = new Size(0, 22), Anchor = AnchorStyles.Left, Margin = new Padding(0, Theme_704ILR.SpaceXs_704ILR, 0, 0), Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.Error_704ILR, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };

            var acciones_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0, Theme_704ILR.SpaceXs_704ILR, 0, 0) };
            var btnAceptar_704ILR = Ui_704ILR.Primary_704ILR(Coord_704ILR.T_704ILR("BTN_ACEPTAR", "Aceptar"), Theme_704ILR.IcoOk_704ILR);
            btnAceptar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnAceptar_704ILR.Size = new Size(130, 36); btnAceptar_704ILR.Margin = new Padding(0);
            btnAceptar_704ILR.Click += (s_704ILR, e_704ILR) => Aceptar_704ILR();
            var btnCancelar_704ILR = Ui_704ILR.Secondary_704ILR(Coord_704ILR.T_704ILR("BTN_CANCELAR", "Cancelar"));
            btnCancelar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnCancelar_704ILR.Size = new Size(110, 36); btnCancelar_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            btnCancelar_704ILR.Click += (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); };
            acciones_704ILR.Controls.Add(btnAceptar_704ILR);
            acciones_704ILR.Controls.Add(btnCancelar_704ILR);

            body_704ILR.Controls.Add(lbl_704ILR, 0, 0);
            body_704ILR.Controls.Add(_txt_704ILR, 0, 1);
            body_704ILR.Controls.Add(_lblMsg_704ILR, 0, 2);
            body_704ILR.Controls.Add(acciones_704ILR, 0, 3);

            Controls.Add(body_704ILR);
            Controls.Add(pnlTitle_704ILR);
            // Sin AcceptButton: en un cuadro de varias lineas Enter escribe un salto de
            // linea. Escape cancela.
            CancelButton = btnCancelar_704ILR;
        }

        // El texto es obligatorio: uno vacio o de espacios no cierra el dialogo. La capa
        // de negocio vuelve a exigirlo (tambien descarta los caracteres invisibles).
        private void Aceptar_704ILR()
        {
            string texto_704ILR = (_txt_704ILR.Text ?? string.Empty).Trim();
            if (texto_704ILR.Length == 0)
            {
                _lblMsg_704ILR.Text = _avisoVacio_704ILR;
                _txt_704ILR.Focus();
                return;
            }
            Texto_704ILR = texto_704ILR;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
