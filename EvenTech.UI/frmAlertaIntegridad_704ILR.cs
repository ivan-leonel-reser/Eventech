using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Alerta de integridad: la verificacion de los digitos verificadores encontro datos
    // alterados por fuera del sistema. Tiene dos usos.
    //
    //  - Aviso, al iniciar la aplicacion y antes del ingreso: muestra las inconsistencias
    //    y avisa que el sistema queda bloqueado hasta que alguien con permiso lo resuelva.
    //  - Resolucion, al ingresar con una cuenta que tenga permiso para restaurar un
    //    respaldo (RESPALDO_RESTAURAR) o para recalcular la linea base (INTEGRIDAD_RECALC):
    //    ofrece las dos salidas. Restaurar un respaldo devuelve los datos a un momento
    //    anterior a la alteracion; recalcular toma los datos actuales como la referencia
    //    nueva (despues de corregirlos a mano, por ejemplo). Sin resolverlo, no se ingresa.
    //
    // Borderless heredando de FormBase, con identidad de marca (azul oscuro + dorado).
    public class frmAlertaIntegridad_704ILR : FormBase_704ILR
    {
        // Como termino la resolucion.
        public enum Salida_704ILR { Ninguna_704ILR, Recalculada_704ILR, Restaurada_704ILR }

        // Tema de la ayuda en linea de esta ventana (F1).
        protected override string TemaAyuda_704ILR => Ayuda_704ILR.ApartadoProblemasIntegridad_704ILR;

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

        private readonly bool _resolver_704ILR;
        private ListBox _lst_704ILR;
        private AppButton_704ILR _btnRestaurar_704ILR, _btnRecalcular_704ILR, _btnSalir_704ILR;

        public Salida_704ILR Resultado_704ILR { get; private set; } = Salida_704ILR.Ninguna_704ILR;

        // Restauracion hecha desde esta ventana (Resultado = Restaurada).
        public EvenTech.BE.ResultadoRestauracion_704ILR Restauracion_704ILR { get; private set; }

        // Aviso al iniciar, antes del ingreso.
        public frmAlertaIntegridad_704ILR(IReadOnlyList<string> inconsistencias_704ILR) : this(inconsistencias_704ILR, false) { }

        // resolver: al ingresar con permiso para restaurar o recalcular.
        public frmAlertaIntegridad_704ILR(IReadOnlyList<string> inconsistencias_704ILR, bool resolver_704ILR)
        {
            _resolver_704ILR = resolver_704ILR;
            BuildUi_704ILR(inconsistencias_704ILR);
        }

        private void BuildUi_704ILR(IReadOnlyList<string> inconsistencias_704ILR)
        {
            Text = "EvenTech";
            ClientSize = _resolver_704ILR ? new Size(720, 440) : new Size(600, 420);
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
            // Layout por TableLayoutPanel: encabezado / hint / lista (fill) / botones.
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
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // botones

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

            // El aviso dice que el sistema queda bloqueado; la resolucion, que hacer.
            var lblHint_704ILR = new Label
            {
                Text = _resolver_704ILR
                    ? Tr_704ILR.F_704ILR("ALERT_HINT_RESOLVER", "Restaurar un respaldo tomado antes de la alteración devuelve los datos a ese momento. Recalcular los dígitos verificadores toma los datos actuales como la nueva referencia: solo si se comprobó que son correctos.")
                    : Tr_704ILR.F_704ILR("ALERT_HINT_BLOQUEO", "La verificación de dígitos verificadores encontró datos alterados por fuera del sistema. Hasta que se resuelva, solo puede ingresar un usuario con permiso para restaurar un respaldo o recalcular los dígitos verificadores."),
                Tag = _resolver_704ILR ? "T:ALERT_HINT_RESOLVER" : "T:ALERT_HINT_BLOQUEO",
                Font = Theme_704ILR.FontSmall_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 62,
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };

            // Lista de inconsistencias: superficie oscura, texto claro, monoespaciado.
            // Con barra horizontal: una linea mas ancha que la lista (la del DV vertical
            // mide unos 700 px contra 510) se puede leer entera. El ancho desplazable lo
            // calcula la lista a partir del item mas largo.
            _lst_704ILR = new ListBox
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
            CargarLista_704ILR(inconsistencias_704ILR);

            // Botones, alineados a la derecha.
            var botones_704ILR = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, Theme_704ILR.SpaceMd_704ILR, 0, 0)
            };
            if (_resolver_704ILR)
            {
                _btnSalir_704ILR = Boton_704ILR(Ui_704ILR.Secondary_704ILR(Tr_704ILR.F_704ILR("ALERT_BTN_SALIR", "Salir")), "ALERT_BTN_SALIR", 110, Salir_704ILR);
                botones_704ILR.Controls.Add(_btnSalir_704ILR);
                if (Permisos_704ILR.Tiene_704ILR("INTEGRIDAD_RECALC"))
                {
                    _btnRecalcular_704ILR = Boton_704ILR(Ui_704ILR.Primary_704ILR(Tr_704ILR.F_704ILR("ALERT_BTN_RECALCULAR", "Recalcular dígitos verificadores")),
                        "ALERT_BTN_RECALCULAR", 260, Recalcular_704ILR);
                    botones_704ILR.Controls.Add(_btnRecalcular_704ILR);
                }
                if (Permisos_704ILR.Tiene_704ILR(BLL_Respaldo_704ILR.PermisoRestaurar_704ILR))
                {
                    _btnRestaurar_704ILR = Boton_704ILR(Ui_704ILR.Primary_704ILR(Tr_704ILR.F_704ILR("ALERT_BTN_RESTAURAR", "Restaurar un respaldo...")),
                        "ALERT_BTN_RESTAURAR", 220, Restaurar_704ILR);
                    botones_704ILR.Controls.Add(_btnRestaurar_704ILR);
                }
                // La primera salida que se ofrece es volver a un respaldo: no legitima los
                // datos alterados. Enter no ejecuta nada por si solo.
                CancelButton = _btnSalir_704ILR;
            }
            else
            {
                var btnContinuar_704ILR = Boton_704ILR(Ui_704ILR.Primary_704ILR(Tr_704ILR.F_704ILR("ALERT_BTN_INGRESAR", "Continuar al ingreso")),
                    "ALERT_BTN_INGRESAR", 200, Close);
                botones_704ILR.Controls.Add(btnContinuar_704ILR);
                AcceptButton = btnContinuar_704ILR;
            }

            body_704ILR.Controls.Add(header_704ILR, 0, 0);
            body_704ILR.Controls.Add(lblHint_704ILR, 0, 1);
            body_704ILR.Controls.Add(_lst_704ILR, 0, 2);
            body_704ILR.Controls.Add(botones_704ILR, 0, 3);

            Controls.Add(body_704ILR);
            Controls.Add(pnlTitle_704ILR);
        }

        private AppButton_704ILR Boton_704ILR(AppButton_704ILR b_704ILR, string clave_704ILR, int ancho_704ILR, Action accion_704ILR)
        {
            b_704ILR.Tag = "T:" + clave_704ILR;
            b_704ILR.BehindColor_704ILR = Theme_704ILR.BgLogin_704ILR;
            b_704ILR.Size = new Size(ancho_704ILR, 40);
            b_704ILR.Margin = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, 0, 0);
            b_704ILR.Click += (s_704ILR, e_704ILR) => accion_704ILR();
            return b_704ILR;
        }

        private void CargarLista_704ILR(IReadOnlyList<string> inconsistencias_704ILR)
        {
            _lst_704ILR.Items.Clear();
            foreach (var i_704ILR in inconsistencias_704ILR) _lst_704ILR.Items.Add(TextoInconsistencia_704ILR(i_704ILR));
        }

        // ------------------------------------------------------------------ resolucion

        private void Restaurar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Respaldo_704ILR.PermisoRestaurar_704ILR, this, "restaurar un respaldo de la base")) return;
            using (var dlg_704ILR = new frmRestaurarRespaldo_704ILR())
            {
                if (dlg_704ILR.ShowDialog(this) != DialogResult.OK) return;
                Restauracion_704ILR = dlg_704ILR.Resultado_704ILR;
            }
            Resultado_704ILR = Salida_704ILR.Restaurada_704ILR;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Recalcular_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("INTEGRIDAD_RECALC", this, "recalcular la linea base de DV")) return;

            // Toma los datos actuales (los alterados incluidos) como la referencia nueva: la
            // pregunta arranca en No.
            var confirma_704ILR = MessageBox.Show(this,
                Tr_704ILR.F_704ILR("ALERT_RECALC_CONFIRMA", "¿Recalcular los dígitos verificadores con los datos tal como están ahora?") + "\n\n" +
                Tr_704ILR.F_704ILR("ALERT_RECALC_NOTA", "Los datos actuales, incluidos los que se modificaron por fuera del sistema, pasan a ser la referencia de integridad. Hacerlo solo si se comprobó que son correctos; si no, restaurar un respaldo."),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirma_704ILR != DialogResult.Yes) return;

            ResultadoIntegridad_704ILR verificacion_704ILR;
            Cursor = Cursors.WaitCursor;
            try
            {
                BLL_Integridad_704ILR.RecalcularTodo_704ILR();
                verificacion_704ILR = BLL_Integridad_704ILR.Verificar_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                Cursor = Cursors.Default;
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Integridad", "Recalculo de linea base desde la alerta");
                MessageBox.Show(this, Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            finally { Cursor = Cursors.Default; }

            if (verificacion_704ILR.Ok_704ILR)
            {
                MessageBox.Show(this, Tr_704ILR.F_704ILR("ALERT_RECALC_OK",
                    "Se recalcularon los dígitos verificadores: la verificación ya no encuentra inconsistencias."),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Resultado_704ILR = Salida_704ILR.Recalculada_704ILR;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            // Lo que el recalculo no corrige (un estado fuera del dominio de la tabla de
            // estados) sigue a la vista: queda restaurar un respaldo.
            CargarLista_704ILR(verificacion_704ILR.Inconsistencias_704ILR);
            MessageBox.Show(this, Tr_704ILR.F_704ILR("ALERT_RECALC_PENDIENTE",
                "Se recalcularon los dígitos verificadores, pero quedan {0} inconsistencia(s) que el recálculo no corrige (un estado de reserva que no existe en la tabla de estados). Para resolverlas, restaure un respaldo.",
                verificacion_704ILR.Inconsistencias_704ILR.Count),
                "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Salir_704ILR()
        {
            DialogResult = DialogResult.Cancel;
            Close();
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
