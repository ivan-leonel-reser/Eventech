using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Informe de gestion del periodo (A02). Se elige el periodo, la capa de negocio
    // calcula el informe y la pantalla lo muestra en indicadores y cuatro tablas; el
    // boton Exportar lo pasa a PDF con la biblioteca de terceros
    // (ExportadorPdfInforme_704ILR). Ningun importe se calcula en la pantalla.
    public class ucInformes_704ILR : UserControl, IObservadorIdioma_704ILR, IAyudaContextual_704ILR
    {
        private DateTimePicker _dtDesde_704ILR, _dtHasta_704ILR;
        private AppButton_704ILR _btnGenerar_704ILR, _btnExportar_704ILR;
        private Label _lblEstado_704ILR;
        private readonly Label[] _kpiTitulo_704ILR = new Label[5], _kpiValor_704ILR = new Label[5];
        private TabControl _tabs_704ILR;
        private TabPage _tabEstados_704ILR, _tabSalones_704ILR, _tabVencer_704ILR, _tabEventos_704ILR;
        private DataGridView _gEstados_704ILR, _gSalones_704ILR, _gVencer_704ILR, _gEventos_704ILR;
        private readonly ToolTip _tip_704ILR = new ToolTip();

        private BE_InformeGestion_704ILR _informe_704ILR;
        // Aviso vigente, re-traducible: el resultado de la ultima generacion o su excepcion.
        private InformeResult_704ILR? _rechazo_704ILR;
        private Exception _error_704ILR;

        public string TemaAyuda_704ILR => Ayuda_704ILR.TemaInformes_704ILR;

        public ucInformes_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) =>
            {
                GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
                ActualizarTextos_704ILR();
            };
            Disposed += (s_704ILR, e_704ILR) =>
            {
                GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
                _tip_704ILR.Dispose();
            };
        }

        private void BuildUi_704ILR()
        {
            var root_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.Transparent };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Informe de gestión");
            lblTitle_704ILR.Tag = "T:INF_TITULO";
            lblTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            // Periodo: por defecto, el mes en curso.
            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR),
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceMd_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceMd_704ILR)
            };
            var flow_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, BackColor = Color.Transparent };
            DateTime hoy_704ILR = DateTime.Today;
            _dtDesde_704ILR = Ui_704ILR.DatePicker_704ILR();
            _dtDesde_704ILR.Width = 130;
            _dtDesde_704ILR.Value = new DateTime(hoy_704ILR.Year, hoy_704ILR.Month, 1);
            _dtHasta_704ILR = Ui_704ILR.DatePicker_704ILR();
            _dtHasta_704ILR.Width = 130;
            _dtHasta_704ILR.Value = new DateTime(hoy_704ILR.Year, hoy_704ILR.Month, 1).AddMonths(1).AddDays(-1);
            var fDesde_704ILR = Ui_704ILR.Field_704ILR("Desde", _dtDesde_704ILR);
            fDesde_704ILR.Tag = "FIELD:INF_DESDE";
            fDesde_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);
            var fHasta_704ILR = Ui_704ILR.Field_704ILR("Hasta", _dtHasta_704ILR);
            fHasta_704ILR.Tag = "FIELD:INF_HASTA";
            fHasta_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);
            _btnGenerar_704ILR = Ui_704ILR.Primary_704ILR("Generar", Theme_704ILR.IcoSearch_704ILR);
            _btnGenerar_704ILR.Tag = "T:INF_BTN_GENERAR";
            _btnGenerar_704ILR.Size = new Size(130, 32);
            _btnGenerar_704ILR.Margin = new Padding(0, 18, Theme_704ILR.SpaceSm_704ILR, 0);
            _btnGenerar_704ILR.Click += (s_704ILR, e_704ILR) => Generar_704ILR();
            _btnExportar_704ILR = Ui_704ILR.Secondary_704ILR("Exportar PDF...");
            _btnExportar_704ILR.Tag = "T:INF_BTN_EXPORTAR";
            _btnExportar_704ILR.Size = new Size(160, 32);
            _btnExportar_704ILR.Margin = new Padding(0, 18, 0, 0);
            _btnExportar_704ILR.Enabled = false;
            _btnExportar_704ILR.Click += (s_704ILR, e_704ILR) => ExportarPdf_704ILR();
            flow_704ILR.Controls.AddRange(new Control[] { fDesde_704ILR, fHasta_704ILR, _btnGenerar_704ILR, _btnExportar_704ILR });
            // Ayuda en linea (D02): el "?" del periodo abre el apartado "Generar el informe" y
            // el que sigue a Exportar, el de la exportacion a PDF.
            flow_704ILR.Controls.Add(new BotonAyuda_704ILR(Ayuda_704ILR.ApartadoInformesExportar_704ILR) { Margin = new Padding(Theme_704ILR.SpaceSm_704ILR, 25, 0, 0) });
            card_704ILR.Controls.Add(flow_704ILR);
            Ayuda_704ILR.JuntoA_704ILR(Ayuda_704ILR.RotuloDeCampo_704ILR(fDesde_704ILR), Ayuda_704ILR.ApartadoInformesGenerar_704ILR, card_704ILR);
            Ayuda_704ILR.Zona_704ILR(_btnExportar_704ILR, Ayuda_704ILR.ApartadoInformesExportar_704ILR);

            _lblEstado_704ILR = new Label
            {
                AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.TextMuted_704ILR,
                BackColor = Color.Transparent, Margin = new Padding(2, 0, 0, Theme_704ILR.SpaceSm_704ILR), MaximumSize = new Size(1000, 0)
            };

            // Indicadores: cinco tarjetas en una fila.
            var kpis_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };
            // Una sola fila al alto de la celda: con el estilo automatico la fila tomaba el alto
            // preferido de la tarjeta y el borde inferior quedaba recortado.
            kpis_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i_704ILR = 0; i_704ILR < 5; i_704ILR++)
            {
                kpis_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
                var tarjeta_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(i_704ILR == 0 ? 0 : Theme_704ILR.SpaceSm_704ILR, 0, 0, 0), Padding = new Padding(Theme_704ILR.SpaceMd_704ILR, 6, Theme_704ILR.SpaceMd_704ILR, 6) };
                // El importe va en la letra de titulo y con puntos suspensivos si no entra: con
                // importes de millones y la ventana en su tamano minimo, la de seccion no entraba.
                _kpiValor_704ILR[i_704ILR] = new Label { Dock = DockStyle.Fill, Font = Theme_704ILR.FontTitle_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, BackColor = Color.Transparent, Text = "—" };
                _kpiTitulo_704ILR[i_704ILR] = new Label { Dock = DockStyle.Top, Height = 18, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.TextMuted_704ILR, BackColor = Color.Transparent };
                tarjeta_704ILR.Controls.Add(_kpiValor_704ILR[i_704ILR]);
                tarjeta_704ILR.Controls.Add(_kpiTitulo_704ILR[i_704ILR]);
                kpis_704ILR.Controls.Add(tarjeta_704ILR, i_704ILR, 0);
            }

            _tabs_704ILR = new TabControl { Dock = DockStyle.Fill, Font = Theme_704ILR.FontBody_704ILR };
            _gEstados_704ILR = Grilla_704ILR(("cEstado", 50, false), ("cCantidad", 20, true), ("cMonto", 30, true));
            _gSalones_704ILR = Grilla_704ILR(("cSalon", 34, false), ("cCapacidad", 14, true), ("cEventos", 14, true), ("cOcupacion", 16, true), ("cContratado", 22, true));
            _gVencer_704ILR = Grilla_704ILR(("cReserva", 8, true), ("cFecha", 14, false), ("cCliente", 28, false), ("cSalon", 18, false), ("cEstado", 14, false), ("cVence", 18, false));
            _gEventos_704ILR = Grilla_704ILR(("cFecha", 13, false), ("cSalon", 17, false), ("cCliente", 24, false), ("cCoordinacion", 16, false), ("cContratado", 15, true), ("cSaldo", 15, true));
            _tabEstados_704ILR = Pestania_704ILR(_gEstados_704ILR);
            _tabSalones_704ILR = Pestania_704ILR(_gSalones_704ILR);
            _tabVencer_704ILR = Pestania_704ILR(_gVencer_704ILR);
            _tabEventos_704ILR = Pestania_704ILR(_gEventos_704ILR);
            _tabs_704ILR.TabPages.AddRange(new[] { _tabEstados_704ILR, _tabSalones_704ILR, _tabVencer_704ILR, _tabEventos_704ILR });
            Ayuda_704ILR.Zona_704ILR(_tabs_704ILR, Ayuda_704ILR.ApartadoInformesContenido_704ILR);
            Ayuda_704ILR.Zona_704ILR(kpis_704ILR, Ayuda_704ILR.ApartadoInformesContenido_704ILR);

            root_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            root_704ILR.Controls.Add(card_704ILR, 0, 1);
            root_704ILR.Controls.Add(_lblEstado_704ILR, 0, 2);
            root_704ILR.Controls.Add(kpis_704ILR, 0, 3);
            root_704ILR.Controls.Add(_tabs_704ILR, 0, 4);
            Controls.Add(root_704ILR);
        }

        private static DataGridView Grilla_704ILR(params (string nombre_704ILR, int peso_704ILR, bool derecha_704ILR)[] columnas_704ILR)
        {
            var g_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(g_704ILR);
            foreach (var c_704ILR in columnas_704ILR)
            {
                var col_704ILR = new DataGridViewTextBoxColumn { Name = c_704ILR.nombre_704ILR, FillWeight = c_704ILR.peso_704ILR, SortMode = DataGridViewColumnSortMode.NotSortable };
                if (c_704ILR.derecha_704ILR) col_704ILR.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                g_704ILR.Columns.Add(col_704ILR);
            }
            return g_704ILR;
        }

        private static TabPage Pestania_704ILR(Control contenido_704ILR)
        {
            var t_704ILR = new TabPage { BackColor = Theme_704ILR.BgContent_704ILR, Padding = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0), UseVisualStyleBackColor = true };
            t_704ILR.Controls.Add(contenido_704ILR);
            return t_704ILR;
        }

        private void Generar_704ILR()
        {
            // Segunda capa: el boton existe solo dentro de la seccion, pero la accion
            // vuelve a exigir el permiso (la capa de negocio lo exige otra vez).
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Informe_704ILR.Permiso_704ILR, FindForm(), "generar el informe de gestion")) return;
            _rechazo_704ILR = null;
            _error_704ILR = null;
            try
            {
                var r_704ILR = BLL_Informe_704ILR.Generar_704ILR(_dtDesde_704ILR.Value, _dtHasta_704ILR.Value, out BE_InformeGestion_704ILR inf_704ILR);
                if (r_704ILR == InformeResult_704ILR.Success_704ILR) _informe_704ILR = inf_704ILR;
                else { _rechazo_704ILR = r_704ILR; _informe_704ILR = null; }
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Informes", "Generacion del informe");
                _error_704ILR = ex_704ILR;
                _informe_704ILR = null;
            }
            Mostrar_704ILR();
        }

        private void Mostrar_704ILR()
        {
            foreach (var g_704ILR in new[] { _gEstados_704ILR, _gSalones_704ILR, _gVencer_704ILR, _gEventos_704ILR }) g_704ILR.Rows.Clear();
            _btnExportar_704ILR.Enabled = _informe_704ILR != null;
            ActualizarEstado_704ILR();
            var inf_704ILR = _informe_704ILR;
            if (inf_704ILR == null)
            {
                foreach (var v_704ILR in _kpiValor_704ILR) v_704ILR.Text = "—";
                return;
            }
            int confirmadas_704ILR = inf_704ILR.PorEstado_704ILR.Where(e_704ILR => e_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA).Sum(e_704ILR => e_704ILR.Cantidad_704ILR);
            _kpiValor_704ILR[0].Text = inf_704ILR.TotalReservas_704ILR.ToString(CultureInfo.CurrentCulture);
            _kpiValor_704ILR[1].Text = confirmadas_704ILR.ToString(CultureInfo.CurrentCulture);
            _kpiValor_704ILR[2].Text = Importe_704ILR(inf_704ILR.Contratado_704ILR);
            _kpiValor_704ILR[3].Text = Importe_704ILR(inf_704ILR.Cobrado_704ILR);
            _kpiTitulo_704ILR[3].Text = T_704ILR("INF_COBRADO", "Cobrado") + " (" + Porcentaje_704ILR(inf_704ILR.PorcentajeCobrado_704ILR) + ")";
            _kpiValor_704ILR[4].Text = Importe_704ILR(inf_704ILR.Saldo_704ILR);
            foreach (var v_704ILR in _kpiValor_704ILR) _tip_704ILR.SetToolTip(v_704ILR, v_704ILR.Text);

            foreach (var e_704ILR in inf_704ILR.PorEstado_704ILR)
                _gEstados_704ILR.Rows.Add(Tr_704ILR.Estado_704ILR(e_704ILR.Estado_704ILR), e_704ILR.Cantidad_704ILR.ToString(CultureInfo.CurrentCulture), Importe_704ILR(e_704ILR.Monto_704ILR));
            foreach (var s_704ILR in inf_704ILR.Salones_704ILR)
                _gSalones_704ILR.Rows.Add(s_704ILR.SalonNombre_704ILR, s_704ILR.Capacidad_704ILR.ToString(CultureInfo.CurrentCulture), s_704ILR.Eventos_704ILR.ToString(CultureInfo.CurrentCulture),
                    Porcentaje_704ILR(s_704ILR.PorcentajeOcupacion_704ILR), Importe_704ILR(s_704ILR.Contratado_704ILR));
            foreach (var f_704ILR in inf_704ILR.PorVencer_704ILR)
                _gVencer_704ILR.Rows.Add(f_704ILR.ReservaId_704ILR.ToString(CultureInfo.InvariantCulture), Coord_704ILR.Fecha_704ILR(f_704ILR.FechaEvento_704ILR), f_704ILR.ClienteNombre_704ILR,
                    f_704ILR.SalonNombre_704ILR, Tr_704ILR.Estado_704ILR(f_704ILR.Estado_704ILR), f_704ILR.VenceEl_704ILR.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            foreach (var f_704ILR in inf_704ILR.Eventos_704ILR)
                _gEventos_704ILR.Rows.Add(Coord_704ILR.Fecha_704ILR(f_704ILR.FechaEvento_704ILR), f_704ILR.SalonNombre_704ILR, f_704ILR.ClienteNombre_704ILR,
                    Coord_704ILR.Estado_704ILR(f_704ILR.EstadoCoordinacion_704ILR), Importe_704ILR(f_704ILR.Monto_704ILR), Importe_704ILR(f_704ILR.Saldo_704ILR));
        }

        private void ActualizarEstado_704ILR()
        {
            if (_error_704ILR != null) { _lblEstado_704ILR.ForeColor = Theme_704ILR.Error_704ILR; _lblEstado_704ILR.Text = Tr_704ILR.MensajeExcepcion_704ILR(_error_704ILR); return; }
            if (_rechazo_704ILR == InformeResult_704ILR.RangoInvalido_704ILR)
            { _lblEstado_704ILR.ForeColor = Theme_704ILR.Error_704ILR; _lblEstado_704ILR.Text = T_704ILR("INF_RANGO_INVALIDO", "La fecha «desde» no puede ser posterior a la fecha «hasta»."); return; }
            if (_rechazo_704ILR == InformeResult_704ILR.RangoExcesivo_704ILR)
            { _lblEstado_704ILR.ForeColor = Theme_704ILR.Error_704ILR; _lblEstado_704ILR.Text = Tr_704ILR.F_704ILR("INF_RANGO_EXCESIVO", "El período no puede superar los {0} días.", BLL_Informe_704ILR.MaxDias_704ILR); return; }
            _lblEstado_704ILR.ForeColor = Theme_704ILR.TextMuted_704ILR;
            if (_informe_704ILR == null) { _lblEstado_704ILR.Text = T_704ILR("INF_ELEGIR", "Elija el período y presione Generar."); return; }
            string texto_704ILR = Tr_704ILR.F_704ILR("INF_ESTADO", "Período {0} – {1} ({2} días). Generado el {3}.",
                Coord_704ILR.Fecha_704ILR(_informe_704ILR.Desde_704ILR), Coord_704ILR.Fecha_704ILR(_informe_704ILR.Hasta_704ILR), _informe_704ILR.DiasPeriodo_704ILR,
                _informe_704ILR.GeneradoEl_704ILR.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            if (_informe_704ILR.Vencidas_704ILR > 0)
                texto_704ILR += " " + Tr_704ILR.F_704ILR("INF_NOTA_VENCIDAS", "Además hay {0} operación(es) ya vencida(s): renovarlas o cancelarlas desde Reservas.", _informe_704ILR.Vencidas_704ILR);
            _lblEstado_704ILR.Text = texto_704ILR;
        }

        private void ExportarPdf_704ILR()
        {
            if (_informe_704ILR == null) return;
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Informe_704ILR.Permiso_704ILR, FindForm(), "exportar el informe de gestion")) return;
            using var dlg_704ILR = new SaveFileDialog
            {
                Filter = T_704ILR("INF_FILTRO_PDF", "Documento PDF (*.pdf)") + "|*.pdf",
                FileName = string.Format(CultureInfo.InvariantCulture, "Informe_de_gestion_{0:yyyy-MM-dd}_{1:yyyy-MM-dd}.pdf", _informe_704ILR.Desde_704ILR, _informe_704ILR.Hasta_704ILR),
                OverwritePrompt = true
            };
            if (dlg_704ILR.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                int hojas_704ILR = new ExportadorPdfInforme_704ILR().Exportar_704ILR(_informe_704ILR, dlg_704ILR.FileName);
                BLL_Informe_704ILR.RegistrarExportacion_704ILR(_informe_704ILR, dlg_704ILR.FileName);
                MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("INF_EXPORTADO", "Informe exportado ({0} hoja/s): {1}", hojas_704ILR, dlg_704ILR.FileName),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Informes", "Exportacion a PDF");
                MessageBox.Show(FindForm(), Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), T_704ILR("MSG_ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string Importe_704ILR(decimal m_704ILR) => m_704ILR.ToString("N2", CultureInfo.CurrentCulture);
        private static string Porcentaje_704ILR(decimal p_704ILR) => p_704ILR.ToString("0.0", CultureInfo.CurrentCulture) + " %";

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        private static void Encabezados_704ILR(DataGridView g_704ILR, params (string col_704ILR, string clave_704ILR, string defecto_704ILR)[] textos_704ILR)
        {
            foreach (var t_704ILR in textos_704ILR) g_704ILR.Columns[t_704ILR.col_704ILR].HeaderText = T_704ILR(t_704ILR.clave_704ILR, t_704ILR.defecto_704ILR);
        }

        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            foreach (Control c_704ILR in new Control[] { _dtDesde_704ILR.Parent, _dtHasta_704ILR.Parent })
                if (c_704ILR?.Tag is string tag_704ILR && tag_704ILR.StartsWith("FIELD:"))
                    foreach (Control h_704ILR in c_704ILR.Controls) if (h_704ILR is Label l_704ILR) { l_704ILR.Text = T_704ILR(tag_704ILR.Substring(6), l_704ILR.Text); break; }
            _kpiTitulo_704ILR[0].Text = T_704ILR("INF_KPI_RESERVAS", "Reservas del período");
            _kpiTitulo_704ILR[1].Text = T_704ILR("INF_KPI_CONFIRMADAS", "Confirmadas");
            _kpiTitulo_704ILR[2].Text = T_704ILR("INF_CONTRATADO", "Contratado");
            _kpiTitulo_704ILR[3].Text = _informe_704ILR == null ? T_704ILR("INF_COBRADO", "Cobrado")
                : T_704ILR("INF_COBRADO", "Cobrado") + " (" + Porcentaje_704ILR(_informe_704ILR.PorcentajeCobrado_704ILR) + ")";
            _kpiTitulo_704ILR[4].Text = T_704ILR("INF_SALDO", "Saldo a cobrar");
            _tabEstados_704ILR.Text = T_704ILR("INF_TAB_ESTADOS", "Por estado");
            _tabSalones_704ILR.Text = T_704ILR("INF_TAB_SALONES", "Ocupación de salones");
            _tabVencer_704ILR.Text = T_704ILR("INF_TAB_VENCER", "Por vencer");
            _tabEventos_704ILR.Text = T_704ILR("INF_TAB_EVENTOS", "Eventos confirmados");
            Encabezados_704ILR(_gEstados_704ILR, ("cEstado", "INF_COL_ESTADO", "Estado"), ("cCantidad", "INF_COL_CANTIDAD", "Cantidad"), ("cMonto", "INF_COL_MONTO", "Monto"));
            Encabezados_704ILR(_gSalones_704ILR, ("cSalon", "INF_COL_SALON", "Salón"), ("cCapacidad", "INF_COL_CAPACIDAD", "Capacidad"), ("cEventos", "INF_COL_EVENTOS", "Eventos"),
                ("cOcupacion", "INF_COL_OCUPACION", "Ocupación"), ("cContratado", "INF_COL_CONTRATADO", "Contratado"));
            Encabezados_704ILR(_gVencer_704ILR, ("cReserva", "INF_COL_RESERVA", "N.º"), ("cFecha", "INF_COL_FECHA", "Evento"), ("cCliente", "INF_COL_CLIENTE", "Cliente"),
                ("cSalon", "INF_COL_SALON", "Salón"), ("cEstado", "INF_COL_ESTADO", "Estado"), ("cVence", "INF_COL_VENCE", "Vence"));
            Encabezados_704ILR(_gEventos_704ILR, ("cFecha", "INF_COL_FECHA", "Evento"), ("cSalon", "INF_COL_SALON", "Salón"), ("cCliente", "INF_COL_CLIENTE", "Cliente"),
                ("cCoordinacion", "INF_COL_COORDINACION", "Coordinación"), ("cContratado", "INF_COL_CONTRATADO", "Contratado"), ("cSaldo", "INF_COL_SALDO", "Saldo"));
            Ayuda_704ILR.Describir_704ILR(_tip_704ILR, _btnGenerar_704ILR, "AYU_TT_INF_GENERAR", "Calcula el informe del período elegido.");
            Ayuda_704ILR.Describir_704ILR(_tip_704ILR, _btnExportar_704ILR, "AYU_TT_INF_EXPORTAR", "Guarda el informe generado como documento PDF.");
            // Los valores de las tablas (estados, coordinacion) y el aviso se re-traducen.
            if (_informe_704ILR != null) Mostrar_704ILR(); else ActualizarEstado_704ILR();
        }
    }
}
