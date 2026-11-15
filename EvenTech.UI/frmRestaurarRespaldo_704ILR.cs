using System.Drawing;
using System.Windows.Forms;
using EvenTech.BE;

namespace EvenTech.UI
{
    // Dialogo de restauracion de un respaldo que abre la alerta de integridad: la lista de
    // respaldos de la base (ucRespaldos_704ILR, sin la opcion de crear uno) y Cerrar.
    // Devuelve OK si se restauro un respaldo; el resultado queda en Resultado_704ILR.
    public class frmRestaurarRespaldo_704ILR : FormBase_704ILR
    {
        protected override string TemaAyuda_704ILR => Ayuda_704ILR.TemaProblemas_704ILR;

        public ResultadoRestauracion_704ILR Resultado_704ILR { get; private set; }

        public frmRestaurarRespaldo_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(1040, 560);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblBar_704ILR = new Label
            {
                Text = Tr_704ILR.F_704ILR("RSP_DLG_TITULO", "EvenTech - Restaurar un respaldo"),
                Tag = "T:RSP_DLG_TITULO",
                Font = Theme_704ILR.FontBodyBold_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0),
                BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblBar_704ILR);
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => Close(), danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblBar_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            var cuerpo_704ILR = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceSm_704ILR)
            };
            var respaldos_704ILR = new ucRespaldos_704ILR(soloRestaurar_704ILR: true) { Dock = DockStyle.Fill };
            respaldos_704ILR.Restaurado_704ILR += (s_704ILR, r_704ILR) =>
            {
                Resultado_704ILR = r_704ILR;
                DialogResult = DialogResult.OK;
                Close();
            };
            cuerpo_704ILR.Controls.Add(respaldos_704ILR);

            var pie_704ILR = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme_704ILR.BgContent_704ILR };
            var btnCerrar_704ILR = Ui_704ILR.Secondary_704ILR(Tr_704ILR.F_704ILR("BTN_CERRAR", "Cerrar"));
            btnCerrar_704ILR.Tag = "T:BTN_CERRAR";
            btnCerrar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            btnCerrar_704ILR.Size = new Size(130, 36);
            btnCerrar_704ILR.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            // Con el ancho propio del panel: el anclaje conserva esa distancia al borde derecho.
            btnCerrar_704ILR.Location = new Point(pie_704ILR.Width - btnCerrar_704ILR.Width - Theme_704ILR.SpaceXl_704ILR, 10);
            btnCerrar_704ILR.Click += (s_704ILR, e_704ILR) => Close();
            pie_704ILR.Controls.Add(btnCerrar_704ILR);

            Controls.Add(cuerpo_704ILR);
            Controls.Add(pie_704ILR);
            Controls.Add(pnlTitle_704ILR);
            CancelButton = btnCerrar_704ILR;
        }
    }
}
