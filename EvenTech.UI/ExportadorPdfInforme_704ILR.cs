using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using EvenTech.BE;
using EvenTech.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace EvenTech.UI
{
    // Exportacion del informe de gestion a PDF (A02) con la biblioteca PDFsharp (empira,
    // licencia MIT, compilacion GDI para Windows). Es solo presentacion: recibe el
    // informe ya calculado por la capa de negocio y lo dibuja; no consulta la base ni
    // recalcula ningun importe. Es la unica clase del sistema que usa la biblioteca.
    //
    // Hoja A4 vertical: cabecera con el isologotipo y el periodo, las cinco secciones del
    // informe en tablas que continuan en la hoja siguiente con su encabezado, y un pie
    // con la version del sistema y el numero de hoja.
    public sealed class ExportadorPdfInforme_704ILR
    {
        private const double Margen_704ILR = 40;
        private const double AltoPie_704ILR = 28;
        private const double AltoFila_704ILR = 16;
        private const string Fuente_704ILR = "Arial";

        private static readonly XColor Marino_704ILR = XColor.FromArgb(Theme_704ILR.BgTitleBar_704ILR.ToArgb());
        private static readonly XColor Dorado_704ILR = XColor.FromArgb(Theme_704ILR.AccentButton_704ILR.ToArgb());
        private static readonly XColor Cebra_704ILR = XColor.FromArgb(Theme_704ILR.GridZebra_704ILR.ToArgb());
        private static readonly XColor Linea_704ILR = XColor.FromArgb(Theme_704ILR.Border_704ILR.ToArgb());
        private static readonly XColor Texto_704ILR = XColor.FromArgb(Theme_704ILR.TextOnLight_704ILR.ToArgb());
        private static readonly XColor Gris_704ILR = XColor.FromArgb(Theme_704ILR.TextMuted_704ILR.ToArgb());

        private readonly XFont _titulo_704ILR = new XFont(Fuente_704ILR, 18, XFontStyleEx.Bold);
        private readonly XFont _seccion_704ILR = new XFont(Fuente_704ILR, 12, XFontStyleEx.Bold);
        private readonly XFont _normal_704ILR = new XFont(Fuente_704ILR, 9, XFontStyleEx.Regular);
        private readonly XFont _negrita_704ILR = new XFont(Fuente_704ILR, 9, XFontStyleEx.Bold);
        private readonly XFont _chica_704ILR = new XFont(Fuente_704ILR, 8, XFontStyleEx.Regular);

        private PdfDocument _doc_704ILR;
        private PdfPage _hoja_704ILR;
        private XGraphics _g_704ILR;
        private double _y_704ILR;

        // Escribe el PDF en la ruta indicada. Devuelve la cantidad de hojas.
        public int Exportar_704ILR(BE_InformeGestion_704ILR informe_704ILR, string ruta_704ILR)
        {
            if (informe_704ILR == null) throw new ArgumentNullException(nameof(informe_704ILR));
            _doc_704ILR = new PdfDocument();
            _doc_704ILR.Info.Title = T_704ILR("INF_TITULO", "Informe de gestión");
            _doc_704ILR.Info.Author = "EvenTech";
            _doc_704ILR.Info.Subject = T_704ILR("INF_PERIODO", "Período") + ": " + Dia_704ILR(informe_704ILR.Desde_704ILR) + " – " + Dia_704ILR(informe_704ILR.Hasta_704ILR);
            _doc_704ILR.Info.Creator = "EvenTech " + Tr_704ILR.TextoVersion_704ILR();

            NuevaHoja_704ILR();
            Cabecera_704ILR(informe_704ILR);

            Seccion_704ILR(T_704ILR("INF_SEC_RESUMEN", "1. Resumen del período"));
            Tabla_704ILR(new[] { T_704ILR("INF_COL_ESTADO", "Estado"), T_704ILR("INF_COL_CANTIDAD", "Cantidad"), T_704ILR("INF_COL_MONTO", "Monto") },
                new[] { 0.5, 0.2, 0.3 }, new[] { false, true, true },
                informe_704ILR.PorEstado_704ILR.Select(e_704ILR => new[] { Tr_704ILR.Estado_704ILR(e_704ILR.Estado_704ILR), e_704ILR.Cantidad_704ILR.ToString(CultureInfo.CurrentCulture), Importe_704ILR(e_704ILR.Monto_704ILR) })
                    .Concat(new[] { new[] { T_704ILR("INF_TOTAL", "Total"), informe_704ILR.TotalReservas_704ILR.ToString(CultureInfo.CurrentCulture), Importe_704ILR(informe_704ILR.MontoTotal_704ILR) } }).ToList(),
                ultimaEnNegrita_704ILR: true);

            Seccion_704ILR(T_704ILR("INF_SEC_COBRANZA", "2. Cobranza de las reservas confirmadas"));
            Tabla_704ILR(new[] { T_704ILR("INF_COL_CONCEPTO", "Concepto"), T_704ILR("INF_COL_IMPORTE", "Importe") },
                new[] { 0.6, 0.4 }, new[] { false, true },
                new List<string[]>
                {
                    new[] { T_704ILR("INF_CONTRATADO", "Contratado"), Importe_704ILR(informe_704ILR.Contratado_704ILR) },
                    new[] { T_704ILR("INF_COBRADO", "Cobrado"), Importe_704ILR(informe_704ILR.Cobrado_704ILR) },
                    new[] { T_704ILR("INF_SALDO", "Saldo a cobrar"), Importe_704ILR(informe_704ILR.Saldo_704ILR) },
                    new[] { T_704ILR("INF_PORC_COBRADO", "Porcentaje cobrado"), Porcentaje_704ILR(informe_704ILR.PorcentajeCobrado_704ILR) }
                });

            Seccion_704ILR(T_704ILR("INF_SEC_SALONES", "3. Ocupación de los salones"));
            Tabla_704ILR(new[] { T_704ILR("INF_COL_SALON", "Salón"), T_704ILR("INF_COL_CAPACIDAD", "Capacidad"), T_704ILR("INF_COL_EVENTOS", "Eventos"),
                                 T_704ILR("INF_COL_OCUPACION", "Ocupación"), T_704ILR("INF_COL_CONTRATADO", "Contratado") },
                new[] { 0.34, 0.14, 0.14, 0.16, 0.22 }, new[] { false, true, true, true, true },
                informe_704ILR.Salones_704ILR.Select(s_704ILR => new[] { s_704ILR.SalonNombre_704ILR, s_704ILR.Capacidad_704ILR.ToString(CultureInfo.CurrentCulture),
                    s_704ILR.Eventos_704ILR.ToString(CultureInfo.CurrentCulture), Porcentaje_704ILR(s_704ILR.PorcentajeOcupacion_704ILR), Importe_704ILR(s_704ILR.Contratado_704ILR) }).ToList());
            Nota_704ILR(Tr_704ILR.F_704ILR("INF_NOTA_OCUPACION", "Ocupación: días con un evento confirmado sobre los {0} días del período.", informe_704ILR.DiasPeriodo_704ILR));

            Seccion_704ILR(T_704ILR("INF_SEC_VENCER", "4. Cotizaciones y reservas pendientes por vencer"));
            if (informe_704ILR.PorVencer_704ILR.Count == 0)
                Nota_704ILR(T_704ILR("INF_SIN_VENCER", "No hay operaciones vigentes por vencer en el período."));
            else
                Tabla_704ILR(new[] { T_704ILR("INF_COL_RESERVA", "N.º"), T_704ILR("INF_COL_FECHA", "Evento"), T_704ILR("INF_COL_CLIENTE", "Cliente"),
                                     T_704ILR("INF_COL_SALON", "Salón"), T_704ILR("INF_COL_ESTADO", "Estado"), T_704ILR("INF_COL_VENCE", "Vence") },
                    new[] { 0.08, 0.14, 0.28, 0.18, 0.14, 0.18 }, new[] { true, false, false, false, false, false },
                    informe_704ILR.PorVencer_704ILR.Select(f_704ILR => new[] { f_704ILR.ReservaId_704ILR.ToString(CultureInfo.InvariantCulture), Dia_704ILR(f_704ILR.FechaEvento_704ILR),
                        f_704ILR.ClienteNombre_704ILR, f_704ILR.SalonNombre_704ILR, Tr_704ILR.Estado_704ILR(f_704ILR.Estado_704ILR),
                        f_704ILR.VenceEl_704ILR.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) }).ToList());
            if (informe_704ILR.Vencidas_704ILR > 0)
                Nota_704ILR(Tr_704ILR.F_704ILR("INF_NOTA_VENCIDAS", "Además hay {0} operación(es) ya vencida(s): renovarlas o cancelarlas desde Reservas.", informe_704ILR.Vencidas_704ILR));

            Seccion_704ILR(T_704ILR("INF_SEC_EVENTOS", "5. Eventos confirmados"));
            if (informe_704ILR.Eventos_704ILR.Count == 0)
                Nota_704ILR(T_704ILR("INF_SIN_EVENTOS", "No hay eventos confirmados en el período."));
            else
                Tabla_704ILR(new[] { T_704ILR("INF_COL_FECHA", "Evento"), T_704ILR("INF_COL_SALON", "Salón"), T_704ILR("INF_COL_CLIENTE", "Cliente"),
                                     T_704ILR("INF_COL_COORDINACION", "Coordinación"), T_704ILR("INF_COL_CONTRATADO", "Contratado"), T_704ILR("INF_COL_SALDO", "Saldo") },
                    new[] { 0.13, 0.17, 0.24, 0.16, 0.15, 0.15 }, new[] { false, false, false, false, true, true },
                    informe_704ILR.Eventos_704ILR.Select(f_704ILR => new[] { Dia_704ILR(f_704ILR.FechaEvento_704ILR), f_704ILR.SalonNombre_704ILR, f_704ILR.ClienteNombre_704ILR,
                        Coord_704ILR.Estado_704ILR(f_704ILR.EstadoCoordinacion_704ILR), Importe_704ILR(f_704ILR.Monto_704ILR), Importe_704ILR(f_704ILR.Saldo_704ILR) }).ToList());

            CerrarHoja_704ILR();
            int hojas_704ILR = _doc_704ILR.PageCount;
            for (int i_704ILR = 0; i_704ILR < hojas_704ILR; i_704ILR++) Pie_704ILR(_doc_704ILR.Pages[i_704ILR], i_704ILR + 1, hojas_704ILR);
            // Se escribe a un temporal de la misma carpeta y, completo, reemplaza al archivo
            // elegido: una falla a mitad de camino no deja un PDF truncado con ese nombre.
            string temporal_704ILR = ruta_704ILR + ".tmp";
            try
            {
                _doc_704ILR.Save(temporal_704ILR);
                File.Move(temporal_704ILR, ruta_704ILR, true);
            }
            finally
            {
                _doc_704ILR.Dispose();
                try { if (File.Exists(temporal_704ILR)) File.Delete(temporal_704ILR); } catch (IOException) { }
            }
            return hojas_704ILR;
        }

        // ---------------------------------------------------------------- composicion

        private double Ancho_704ILR => _hoja_704ILR.Width.Point - 2 * Margen_704ILR;
        private double Limite_704ILR => _hoja_704ILR.Height.Point - Margen_704ILR - AltoPie_704ILR;

        private void NuevaHoja_704ILR()
        {
            CerrarHoja_704ILR();
            _hoja_704ILR = _doc_704ILR.AddPage();
            _hoja_704ILR.Size = PdfSharp.PageSize.A4;
            _g_704ILR = XGraphics.FromPdfPage(_hoja_704ILR);
            _y_704ILR = Margen_704ILR;
        }

        private void CerrarHoja_704ILR()
        {
            _g_704ILR?.Dispose();
            _g_704ILR = null;
        }

        // Asegura lugar para "alto" puntos; si no entra, pasa a la hoja siguiente.
        private bool Lugar_704ILR(double alto_704ILR)
        {
            if (_y_704ILR + alto_704ILR <= Limite_704ILR) return false;
            NuevaHoja_704ILR();
            return true;
        }

        private void Cabecera_704ILR(BE_InformeGestion_704ILR inf_704ILR)
        {
            double alto_704ILR = 70;
            _g_704ILR.DrawRectangle(new XSolidBrush(Marino_704ILR), Margen_704ILR, _y_704ILR, Ancho_704ILR, alto_704ILR);
            double x_704ILR = Margen_704ILR + 14;
            Image logo_704ILR = Theme_704ILR.Logo_704ILR;
            if (logo_704ILR != null)
            {
                using var ms_704ILR = new MemoryStream();
                logo_704ILR.Save(ms_704ILR, System.Drawing.Imaging.ImageFormat.Png);
                ms_704ILR.Position = 0;
                using XImage img_704ILR = XImage.FromStream(ms_704ILR);
                double h_704ILR = alto_704ILR - 16, w_704ILR = h_704ILR * img_704ILR.PixelWidth / img_704ILR.PixelHeight;
                _g_704ILR.DrawImage(img_704ILR, x_704ILR, _y_704ILR + 8, w_704ILR, h_704ILR);
                x_704ILR += w_704ILR + 16;
            }
            _g_704ILR.DrawString(T_704ILR("INF_TITULO", "Informe de gestión"), _titulo_704ILR, XBrushes.White, x_704ILR, _y_704ILR + 28);
            _g_704ILR.DrawString(T_704ILR("INF_PERIODO", "Período") + ": " + Dia_704ILR(inf_704ILR.Desde_704ILR) + " – " + Dia_704ILR(inf_704ILR.Hasta_704ILR) +
                                 "  (" + Tr_704ILR.F_704ILR("INF_DIAS", "{0} días", inf_704ILR.DiasPeriodo_704ILR) + ")",
                                 _normal_704ILR, new XSolidBrush(XColor.FromArgb(Theme_704ILR.TextLight_704ILR.ToArgb())), x_704ILR, _y_704ILR + 46);
            _g_704ILR.DrawString(Tr_704ILR.F_704ILR("INF_GENERADO", "Generado el {0} por {1}", inf_704ILR.GeneradoEl_704ILR.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), inf_704ILR.GeneradoPor_704ILR),
                                 _chica_704ILR, new XSolidBrush(XColor.FromArgb(Theme_704ILR.TextLight_704ILR.ToArgb())), x_704ILR, _y_704ILR + 60);
            _y_704ILR += alto_704ILR + 14;
        }

        private void Seccion_704ILR(string titulo_704ILR)
        {
            // Un titulo de seccion nunca queda solo al pie: pide lugar para el y dos filas.
            Lugar_704ILR(26 + 3 * AltoFila_704ILR);
            _y_704ILR += 6;
            _g_704ILR.DrawString(titulo_704ILR, _seccion_704ILR, new XSolidBrush(Dorado_704ILR), Margen_704ILR, _y_704ILR + 12);
            _y_704ILR += 18;
            _g_704ILR.DrawLine(new XPen(Dorado_704ILR, 0.8), Margen_704ILR, _y_704ILR, Margen_704ILR + Ancho_704ILR, _y_704ILR);
            _y_704ILR += 6;
        }

        private void Nota_704ILR(string texto_704ILR)
        {
            Lugar_704ILR(AltoFila_704ILR);
            _g_704ILR.DrawString(Recortar_704ILR(texto_704ILR, _chica_704ILR, Ancho_704ILR), _chica_704ILR, new XSolidBrush(Gris_704ILR), Margen_704ILR, _y_704ILR + 10);
            _y_704ILR += AltoFila_704ILR;
        }

        // Tabla con encabezado marino; si no entra, sigue en la hoja siguiente repitiendo
        // el encabezado. "proporciones": fraccion del ancho de cada columna.
        private void Tabla_704ILR(string[] columnas_704ILR, double[] proporciones_704ILR, bool[] derecha_704ILR,
                                  IList<string[]> filas_704ILR, bool ultimaEnNegrita_704ILR = false)
        {
            double[] anchos_704ILR = proporciones_704ILR.Select(p_704ILR => p_704ILR * Ancho_704ILR).ToArray();
            void Encabezado_704ILR()
            {
                _g_704ILR.DrawRectangle(new XSolidBrush(Marino_704ILR), Margen_704ILR, _y_704ILR, Ancho_704ILR, AltoFila_704ILR + 2);
                Fila_704ILR(columnas_704ILR, anchos_704ILR, derecha_704ILR, _negrita_704ILR, XBrushes.White, AltoFila_704ILR + 2);
            }
            Lugar_704ILR(2 * AltoFila_704ILR + 2);
            Encabezado_704ILR();
            for (int i_704ILR = 0; i_704ILR < filas_704ILR.Count; i_704ILR++)
            {
                if (Lugar_704ILR(AltoFila_704ILR)) Encabezado_704ILR();
                if (i_704ILR % 2 == 1) _g_704ILR.DrawRectangle(new XSolidBrush(Cebra_704ILR), Margen_704ILR, _y_704ILR, Ancho_704ILR, AltoFila_704ILR);
                bool negrita_704ILR = ultimaEnNegrita_704ILR && i_704ILR == filas_704ILR.Count - 1;
                Fila_704ILR(filas_704ILR[i_704ILR], anchos_704ILR, derecha_704ILR, negrita_704ILR ? _negrita_704ILR : _normal_704ILR, new XSolidBrush(Texto_704ILR), AltoFila_704ILR);
                _g_704ILR.DrawLine(new XPen(Linea_704ILR, 0.5), Margen_704ILR, _y_704ILR, Margen_704ILR + Ancho_704ILR, _y_704ILR);
            }
            _y_704ILR += 8;
        }

        private void Fila_704ILR(string[] celdas_704ILR, double[] anchos_704ILR, bool[] derecha_704ILR, XFont fuente_704ILR, XBrush pincel_704ILR, double alto_704ILR)
        {
            double x_704ILR = Margen_704ILR;
            for (int c_704ILR = 0; c_704ILR < anchos_704ILR.Length; c_704ILR++)
            {
                string t_704ILR = Recortar_704ILR(c_704ILR < celdas_704ILR.Length ? celdas_704ILR[c_704ILR] : string.Empty, fuente_704ILR, anchos_704ILR[c_704ILR] - 8);
                var caja_704ILR = new XRect(x_704ILR + 4, _y_704ILR, anchos_704ILR[c_704ILR] - 8, alto_704ILR);
                _g_704ILR.DrawString(t_704ILR, fuente_704ILR, pincel_704ILR, caja_704ILR, derecha_704ILR[c_704ILR] ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
                x_704ILR += anchos_704ILR[c_704ILR];
            }
            _y_704ILR += alto_704ILR;
        }

        // Un texto que no entra en su celda termina en puntos suspensivos: la tabla no se
        // desarma con un nombre largo.
        private string Recortar_704ILR(string texto_704ILR, XFont fuente_704ILR, double ancho_704ILR)
        {
            texto_704ILR ??= string.Empty;
            if (_g_704ILR.MeasureString(texto_704ILR, fuente_704ILR).Width <= ancho_704ILR) return texto_704ILR;
            const string puntos_704ILR = "…";
            int n_704ILR = texto_704ILR.Length;
            while (n_704ILR > 0 && _g_704ILR.MeasureString(texto_704ILR.Substring(0, n_704ILR) + puntos_704ILR, fuente_704ILR).Width > ancho_704ILR) n_704ILR--;
            if (n_704ILR > 0 && char.IsHighSurrogate(texto_704ILR[n_704ILR - 1])) n_704ILR--;
            return texto_704ILR.Substring(0, n_704ILR).TrimEnd() + puntos_704ILR;
        }

        private void Pie_704ILR(PdfPage hoja_704ILR, int numero_704ILR, int total_704ILR)
        {
            using XGraphics g_704ILR = XGraphics.FromPdfPage(hoja_704ILR, XGraphicsPdfPageOptions.Append);
            double y_704ILR = hoja_704ILR.Height.Point - Margen_704ILR - 10;
            double ancho_704ILR = hoja_704ILR.Width.Point - 2 * Margen_704ILR;
            g_704ILR.DrawLine(new XPen(Linea_704ILR, 0.6), Margen_704ILR, y_704ILR - 10, Margen_704ILR + ancho_704ILR, y_704ILR - 10);
            var gris_704ILR = new XSolidBrush(Gris_704ILR);
            g_704ILR.DrawString("EvenTech · " + Tr_704ILR.TextoVersion_704ILR(), _chica_704ILR, gris_704ILR,
                new XRect(Margen_704ILR, y_704ILR - 8, ancho_704ILR, 12), XStringFormats.CenterLeft);
            g_704ILR.DrawString(Tr_704ILR.F_704ILR("INF_HOJA", "Hoja {0} de {1}", numero_704ILR, total_704ILR), _chica_704ILR, gris_704ILR,
                new XRect(Margen_704ILR, y_704ILR - 8, ancho_704ILR, 12), XStringFormats.CenterRight);
        }

        // ---------------------------------------------------------------- formatos

        private static string Dia_704ILR(DateTime d_704ILR) => d_704ILR.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        private static string Importe_704ILR(decimal m_704ILR) => m_704ILR.ToString("N2", CultureInfo.CurrentCulture);
        private static string Porcentaje_704ILR(decimal p_704ILR) => p_704ILR.ToString("0.0", CultureInfo.CurrentCulture) + " %";

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }
}
