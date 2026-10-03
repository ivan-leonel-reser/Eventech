using System.Drawing;
using System.Windows.Forms;

namespace EvenTech.UI
{
    // Estilo unificado para DataGridView (header de marca, zebra, seleccion
    // dorada, lineas suaves). Centraliza lo que antes se repetia en cada vista.
    internal static class UiGrid_704ILR
    {
        public static void Style_704ILR(DataGridView g_704ILR, bool editable_704ILR = false)
        {
            g_704ILR.BackgroundColor = Theme_704ILR.Surface_704ILR;
            g_704ILR.BorderStyle = BorderStyle.None;
            g_704ILR.GridColor = Theme_704ILR.GridLines_704ILR;
            g_704ILR.ReadOnly = !editable_704ILR;
            g_704ILR.AllowUserToAddRows = false;
            g_704ILR.AllowUserToDeleteRows = false;
            g_704ILR.AllowUserToResizeRows = false;
            g_704ILR.RowHeadersVisible = false;
            g_704ILR.AutoGenerateColumns = false;
            g_704ILR.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g_704ILR.SelectionMode = editable_704ILR
                ? DataGridViewSelectionMode.CellSelect
                : DataGridViewSelectionMode.FullRowSelect;
            g_704ILR.MultiSelect = false;
            g_704ILR.EnableHeadersVisualStyles = false;
            g_704ILR.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g_704ILR.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g_704ILR.RowTemplate.Height = 30;
            g_704ILR.ColumnHeadersHeight = 36;
            g_704ILR.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g_704ILR.Font = Theme_704ILR.FontSmall_704ILR;

            var h_704ILR = g_704ILR.ColumnHeadersDefaultCellStyle;
            h_704ILR.BackColor = Theme_704ILR.GridHeaderBg_704ILR;
            h_704ILR.ForeColor = Theme_704ILR.GridHeaderFg_704ILR;
            h_704ILR.Font = Theme_704ILR.FontBodyBold_704ILR;
            h_704ILR.Alignment = DataGridViewContentAlignment.MiddleLeft;
            h_704ILR.Padding = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, 0, 0);
            h_704ILR.SelectionBackColor = Theme_704ILR.GridHeaderBg_704ILR;
            h_704ILR.SelectionForeColor = Theme_704ILR.GridHeaderFg_704ILR;
            h_704ILR.WrapMode = DataGridViewTriState.False;

            var d_704ILR = g_704ILR.DefaultCellStyle;
            d_704ILR.BackColor = Theme_704ILR.Surface_704ILR;
            d_704ILR.ForeColor = Theme_704ILR.TextOnLight_704ILR;
            d_704ILR.SelectionBackColor = Theme_704ILR.GridSelectBg_704ILR;
            d_704ILR.SelectionForeColor = Theme_704ILR.GridSelectFg_704ILR;
            d_704ILR.Padding = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, Theme_704ILR.SpaceXs_704ILR, 0);
            d_704ILR.Font = Theme_704ILR.FontSmall_704ILR;

            var a_704ILR = g_704ILR.AlternatingRowsDefaultCellStyle;
            a_704ILR.BackColor = Theme_704ILR.GridZebra_704ILR;
            a_704ILR.ForeColor = Theme_704ILR.TextOnLight_704ILR;
            a_704ILR.SelectionBackColor = Theme_704ILR.GridSelectBg_704ILR;
            a_704ILR.SelectionForeColor = Theme_704ILR.GridSelectFg_704ILR;
        }

        // Texto entero en las columnas de texto libre (descripciones, motivos, nombres):
        // en vez de cortarlo con puntos suspensivos, la celda lo muestra en varios
        // renglones y la fila crece lo necesario. Las filas de un solo renglon conservan
        // el alto de siempre.
        public static void Multilinea_704ILR(DataGridView g_704ILR, params string[] columnas_704ILR)
        {
            Padding base_704ILR = g_704ILR.DefaultCellStyle.Padding;
            foreach (string nombre_704ILR in columnas_704ILR)
            {
                DataGridViewCellStyle estilo_704ILR = g_704ILR.Columns[nombre_704ILR].DefaultCellStyle;
                estilo_704ILR.WrapMode = DataGridViewTriState.True;
                // Aire arriba y abajo: dos renglones no quedan pegados a los bordes de la fila.
                estilo_704ILR.Padding = new Padding(base_704ILR.Left, 5, base_704ILR.Right, 5);
            }
            g_704ILR.RowTemplate.MinimumHeight = g_704ILR.RowTemplate.Height;
            g_704ILR.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        }

        // Columnas de contenido corto y cerrado (fecha, hora, estado, prioridad): toman el
        // ancho de su encabezado y de su contenido, asi no se cortan con un idioma de
        // rotulos mas largos, y dejan el resto del ancho a las columnas de texto.
        public static void AlContenido_704ILR(DataGridView g_704ILR, params string[] columnas_704ILR)
        {
            foreach (string nombre_704ILR in columnas_704ILR)
                g_704ILR.Columns[nombre_704ILR].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        }

        // Ninguna columna de ancho repartido queda mas angosta que su encabezado. Se llama
        // despues de fijar (o de traducir) los encabezados.
        public static void EncabezadosEnteros_704ILR(DataGridView g_704ILR)
        {
            DataGridViewCellStyle estilo_704ILR = g_704ILR.ColumnHeadersDefaultCellStyle;
            // Subir el ancho minimo de una columna de ancho repartido la ensancha, y la
            // grilla reparte de nuevo los pesos de las demas: se guardan y se reponen, para
            // que el reparto siga siendo el que definio la pantalla.
            var pesos_704ILR = new float[g_704ILR.Columns.Count];
            for (int i_704ILR = 0; i_704ILR < pesos_704ILR.Length; i_704ILR++) pesos_704ILR[i_704ILR] = g_704ILR.Columns[i_704ILR].FillWeight;
            foreach (DataGridViewColumn c_704ILR in g_704ILR.Columns)
            {
                int texto_704ILR = TextRenderer.MeasureText(c_704ILR.HeaderText ?? string.Empty, estilo_704ILR.Font).Width;
                // La flecha de orden reserva su lugar a la derecha del texto.
                int flecha_704ILR = c_704ILR.SortMode == DataGridViewColumnSortMode.NotSortable ? 0 : 20;
                c_704ILR.MinimumWidth = System.Math.Max(5, texto_704ILR + estilo_704ILR.Padding.Horizontal + 10 + flecha_704ILR);
            }
            for (int i_704ILR = 0; i_704ILR < pesos_704ILR.Length; i_704ILR++) g_704ILR.Columns[i_704ILR].FillWeight = pesos_704ILR[i_704ILR];
        }
    }
}
