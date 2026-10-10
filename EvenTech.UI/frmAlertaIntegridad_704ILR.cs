using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EvenTech.UI
{
    // Pantalla de alerta que se muestra al iniciar la app cuando la verificacion
    // de integridad (digitos verificadores) detecta inconsistencias. Bloquea el
    // flujo hasta que el administrador la revise (la cierra explicitamente).
    // Borderless heredando de FormBase, con identidad de marca (azul oscuro + dorado).
    public class frmAlertaIntegridad_704ILR : FormBase_704ILR
    {
        // La verificacion devuelve cada inconsistencia como texto en castellano (es
        // tambien el detalle que queda asentado en bitacora). La pantalla reconoce
        // las formas que produce, de las reservas y de los pagos, y las muestra con
        // la leyenda del idioma activo.
        private static readonly Regex RxDvhFaltante_704ILR =
            new Regex(@"^Reserva #(\d+): sin DV horizontal almacenado", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex RxDvhNoCoincide_704ILR =
            new Regex(@"^Reserva #(\d+): (el )?DV horizontal no coincide", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex RxDvvNoCoincide_704ILR =
            new Regex(@"^(el )?DV vertical de Reservas no coincide", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex RxEstadoFueraDominio_704ILR =
            new Regex(@"^Reserva #(\d+): estado almacenado fuera del dominio", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex RxPagoDvhFaltante_704ILR =
            new Regex(@"^Pago #(\d+) \(reserva #(\d+)\): sin DV horizontal almacenado", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex RxPagoDvhNoCoincide_704ILR =
            new Regex(@"^Pago #(\d+) \(reserva #(\d+)\): (el )?DV horizontal no coincide", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex RxDvvPagosNoCoincide_704ILR =
            new Regex(@"^(el )?DV vertical de Pagos no coincide", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public frmAlertaIntegridad_704ILR(IReadOnlyList<string> inconsistencias_704ILR)
        {
            BuildUi_704ILR(inconsistencias_704ILR);
        }

        private void BuildUi_704ILR(IReadOnlyList<string> inconsistencias_704ILR)
        {
            Text = "EvenTech";
            ClientSize = new Size(560, 400);
            BackColor = Theme_704ILR.BgLogin_704ILR;

            // ---------------- Barra de titulo ----------------
            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);

            var lblBar_704ILR = new Label
            {
                Text = "EvenTech",
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

            // ---------------- Cuerpo ----------------
            // Layout por TableLayoutPanel: encabezado / hint / lista (fill) / boton.
            var body_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme_704ILR.BgLogin_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR)
            };
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // encabezado (icono + titulo)
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // hint
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));        // lista de inconsistencias
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));           // boton continuar

            // Encabezado de advertencia: icono dorado + titulo.
            var header_704ILR = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR)
            };
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var lblIcon_704ILR = new Label
            {
                Text = Theme_704ILR.IcoWarning_704ILR,
                Font = Theme_704ILR.FontIcon_704ILR,
                ForeColor = Theme_704ILR.Accent_704ILR,
                AutoSize = false,
                Size = new Size(32, 36),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0)
            };

            var lblTitle_704ILR = new Label
            {
                Text = Tr_704ILR.F_704ILR("ALERT_TITULO", "Se detectaron problemas de integridad"),
                Tag = "T:ALERT_TITULO",
                Font = Theme_704ILR.FontH1_704ILR,
                ForeColor = Theme_704ILR.TextOnDark_704ILR,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            header_704ILR.Controls.Add(lblIcon_704ILR, 0, 0);
            header_704ILR.Controls.Add(lblTitle_704ILR, 1, 0);

            var lblHint_704ILR = new Label
            {
                Text = Tr_704ILR.F_704ILR("ALERT_HINT", "La verificación de dígitos verificadores encontró datos alterados por fuera del sistema. Avise al administrador antes de operar."),
                Tag = "T:ALERT_HINT",
                Font = Theme_704ILR.FontSmall_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 44,
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };

            // Lista de inconsistencias: superficie oscura, texto claro, monoespaciado.
            // Con barra horizontal: una linea mas ancha que la lista (la del DV vertical
            // mide unos 700 px contra 510) se puede leer entera. El ancho desplazable lo
            // calcula la lista a partir del item mas largo.
            var lst_704ILR = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.5F),
                BackColor = Theme_704ILR.BgTitleBar_704ILR,
                ForeColor = Theme_704ILR.TextOnDark_704ILR,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                HorizontalScrollbar = true,
                Margin = new Padding(0)
            };
            foreach (var i_704ILR in inconsistencias_704ILR) lst_704ILR.Items.Add(TextoInconsistencia_704ILR(i_704ILR));

            // Boton continuar: primario dorado, alineado a la derecha. Cierra el dialogo.
            var btnContinuar_704ILR = Ui_704ILR.Primary_704ILR(Tr_704ILR.F_704ILR("ALERT_BTN", "Revisado, continuar"));
            btnContinuar_704ILR.Tag = "T:ALERT_BTN";
            btnContinuar_704ILR.BehindColor_704ILR = Theme_704ILR.BgLogin_704ILR;
            btnContinuar_704ILR.Size = new Size(200, 40);
            btnContinuar_704ILR.Anchor = AnchorStyles.Right;
            btnContinuar_704ILR.Margin = new Padding(0, Theme_704ILR.SpaceMd_704ILR, 0, 0);
            btnContinuar_704ILR.Click += (s_704ILR, e_704ILR) => Close();

            body_704ILR.Controls.Add(header_704ILR, 0, 0);
            body_704ILR.Controls.Add(lblHint_704ILR, 0, 1);
            body_704ILR.Controls.Add(lst_704ILR, 0, 2);
            body_704ILR.Controls.Add(btnContinuar_704ILR, 0, 3);

            Controls.Add(body_704ILR);
            Controls.Add(pnlTitle_704ILR);

            AcceptButton = btnContinuar_704ILR;
        }

        // Leyenda de una inconsistencia en el idioma activo. Un texto que no responde a
        // ninguna de las formas conocidas (otra verificacion) se muestra tal cual.
        private static string TextoInconsistencia_704ILR(string inconsistencia_704ILR)
        {
            if (string.IsNullOrEmpty(inconsistencia_704ILR)) return inconsistencia_704ILR;

            Match faltante_704ILR = RxDvhFaltante_704ILR.Match(inconsistencia_704ILR);
            if (faltante_704ILR.Success && int.TryParse(faltante_704ILR.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int idFaltante_704ILR))
                return Tr_704ILR.F_704ILR("ALERT_DVH_FALTANTE", "Reserva #{0}: sin DV horizontal almacenado.", idFaltante_704ILR);

            Match distinto_704ILR = RxDvhNoCoincide_704ILR.Match(inconsistencia_704ILR);
            if (distinto_704ILR.Success && int.TryParse(distinto_704ILR.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int idDistinto_704ILR))
                return Tr_704ILR.F_704ILR("ALERT_DVH_NO_COINCIDE", "Reserva #{0}: el DV horizontal no coincide (posible alteración externa).", idDistinto_704ILR);

            Match estado_704ILR = RxEstadoFueraDominio_704ILR.Match(inconsistencia_704ILR);
            if (estado_704ILR.Success && int.TryParse(estado_704ILR.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int idEstado_704ILR))
                return Tr_704ILR.F_704ILR("ALERT_ESTADO_FUERA_DOMINIO", "Reserva #{0}: estado almacenado fuera del dominio de la tabla de estados (posible alteración externa).", idEstado_704ILR);

            if (RxDvvNoCoincide_704ILR.IsMatch(inconsistencia_704ILR))
                return Tr_704ILR.F_704ILR("ALERT_DVV_NO_COINCIDE", "El DV vertical de Reservas no coincide (filas agregadas, quitadas o reordenadas por fuera del sistema).");

            Match pagoFaltante_704ILR = RxPagoDvhFaltante_704ILR.Match(inconsistencia_704ILR);
            if (pagoFaltante_704ILR.Success
                && int.TryParse(pagoFaltante_704ILR.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int idPagoFaltante_704ILR)
                && int.TryParse(pagoFaltante_704ILR.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int idReservaFaltante_704ILR))
                return Tr_704ILR.F_704ILR("ALERT_PAGO_DVH_FALTANTE", "Pago #{0} (reserva #{1}): sin DV horizontal almacenado.",
                    idPagoFaltante_704ILR, idReservaFaltante_704ILR);

            Match pagoDistinto_704ILR = RxPagoDvhNoCoincide_704ILR.Match(inconsistencia_704ILR);
            if (pagoDistinto_704ILR.Success
                && int.TryParse(pagoDistinto_704ILR.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int idPagoDistinto_704ILR)
                && int.TryParse(pagoDistinto_704ILR.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int idReservaDistinta_704ILR))
                return Tr_704ILR.F_704ILR("ALERT_PAGO_DVH_NO_COINCIDE", "Pago #{0} (reserva #{1}): el DV horizontal no coincide (posible alteración externa).",
                    idPagoDistinto_704ILR, idReservaDistinta_704ILR);

            if (RxDvvPagosNoCoincide_704ILR.IsMatch(inconsistencia_704ILR))
                return Tr_704ILR.F_704ILR("ALERT_DVV_PAGOS_NO_COINCIDE", "El DV vertical de Pagos no coincide (filas agregadas, quitadas o reordenadas por fuera del sistema).");

            return inconsistencia_704ILR;
        }
    }
}
