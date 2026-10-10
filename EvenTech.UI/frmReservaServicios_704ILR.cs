using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;

namespace EvenTech.UI
{
    // Dialogo para cargar los servicios contratados de una reserva. Trabaja sobre
    // una copia en memoria; al Aceptar, expone la lista editada en Items para que
    // la ficha de reserva la persista junto con el monto (suma de subtotales).
    public class frmReservaServicios_704ILR : FormBase_704ILR
    {
        private readonly List<BE_Servicio_704ILR> _disponibles_704ILR;
        private DataGridView _grid_704ILR;
        private ComboBox _cboServicio_704ILR;
        private NumericUpDown _numCantidad_704ILR;
        private Label _lblTotal_704ILR;

        public List<BE_ReservaServicio_704ILR> Items_704ILR { get; private set; }

        public frmReservaServicios_704ILR(IEnumerable<BE_ReservaServicio_704ILR> actuales_704ILR, List<BE_Servicio_704ILR> disponibles_704ILR)
        {
            _disponibles_704ILR = disponibles_704ILR ?? new List<BE_Servicio_704ILR>();
            Items_704ILR = actuales_704ILR == null
                ? new List<BE_ReservaServicio_704ILR>()
                : actuales_704ILR.Select(Clone_704ILR).ToList();
            BuildUi_704ILR();
            Refrescar_704ILR();
        }

        private static BE_ReservaServicio_704ILR Clone_704ILR(BE_ReservaServicio_704ILR x_704ILR) => new BE_ReservaServicio_704ILR
        {
            Id_704ILR = x_704ILR.Id_704ILR, ReservaId_704ILR = x_704ILR.ReservaId_704ILR, ServicioId_704ILR = x_704ILR.ServicioId_704ILR,
            ServicioNombre_704ILR = x_704ILR.ServicioNombre_704ILR, Cantidad_704ILR = x_704ILR.Cantidad_704ILR, PrecioUnitario_704ILR = x_704ILR.PrecioUnitario_704ILR
        };

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(680, 480);
            BackColor = Theme_704ILR.BgContent_704ILR;

            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblTitle_704ILR = new Label
            {
                Text = T_704ILR("RES_SERVICIOS", "Servicios de la reserva"),
                Font = Theme_704ILR.FontH2_704ILR, ForeColor = Theme_704ILR.TextOnDark_704ILR, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0), BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblTitle_704ILR);
            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); }, danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnClose_704ILR);

            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // alta
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grilla
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // footer

            // --- Fila de alta: servicio + cantidad + agregar ---
            var alta_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR) };
            _cboServicio_704ILR = Ui_704ILR.Combo_704ILR(); _cboServicio_704ILR.Width = 300; _cboServicio_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            foreach (var s_704ILR in _disponibles_704ILR) _cboServicio_704ILR.Items.Add(s_704ILR);
            // El catalogo se ofrece con su precio vigente a la vista (CUN004, paso 2), tambien
            // con nombres largos: ver DibujarServicio_704ILR y AnchoLista_704ILR.
            _cboServicio_704ILR.DrawMode = DrawMode.OwnerDrawFixed;
            _cboServicio_704ILR.DrawItem += DibujarServicio_704ILR;
            _cboServicio_704ILR.DropDown += (s_704ILR, e_704ILR) => _cboServicio_704ILR.DropDownWidth = AnchoLista_704ILR();
            if (_cboServicio_704ILR.Items.Count > 0) _cboServicio_704ILR.SelectedIndex = 0;
            // Campo entero: la cantidad que se ve es la que se contrata (con un
            // NumericUpDown comun "2,5" mostraba 3 y agregaba 2 unidades).
            _numCantidad_704ILR = new CampoEntero_704ILR { Minimum = 1, Maximum = 9999, Value = 1, Width = 70, Font = Theme_704ILR.FontInput_704ILR, Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0) };
            var btnAgregar_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("BTN_AGREGAR", "Agregar"), Theme_704ILR.IcoAdd_704ILR); btnAgregar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnAgregar_704ILR.Size = new Size(120, 30); btnAgregar_704ILR.Click += (s_704ILR, e_704ILR) => Agregar_704ILR();
            var btnQuitar_704ILR = Ui_704ILR.Secondary_704ILR(T_704ILR("BTN_QUITAR", "Quitar"), Theme_704ILR.IcoClear_704ILR); btnQuitar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnQuitar_704ILR.Size = new Size(110, 30); btnQuitar_704ILR.Margin = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, 0, 0); btnQuitar_704ILR.Click += (s_704ILR, e_704ILR) => Quitar_704ILR();
            alta_704ILR.Controls.Add(_cboServicio_704ILR); alta_704ILR.Controls.Add(_numCantidad_704ILR); alta_704ILR.Controls.Add(btnAgregar_704ILR); alta_704ILR.Controls.Add(btnQuitar_704ILR);

            // --- Grilla ---
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cServicio", HeaderText = T_704ILR("COL_SERVICIO", "Servicio"), FillWeight = 100 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCantidad", HeaderText = T_704ILR("COL_CANTIDAD", "Cantidad"), FillWeight = 35, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPrecio", HeaderText = T_704ILR("COL_PRECIO", "Precio"), FillWeight = 45, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSubtotal", HeaderText = T_704ILR("COL_SUBTOTAL", "Subtotal"), FillWeight = 50, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            card_704ILR.Controls.Add(_grid_704ILR);

            // --- Footer: total + aceptar ---
            var footer_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, BackColor = Color.Transparent };
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _lblTotal_704ILR = new Label { Font = Theme_704ILR.FontH2_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 6, 0, 0), BackColor = Color.Transparent };
            var btnAceptar_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("BTN_ACEPTAR", "Aceptar"), Theme_704ILR.IcoSave_704ILR); btnAceptar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; btnAceptar_704ILR.Size = new Size(140, 38); btnAceptar_704ILR.Anchor = AnchorStyles.Right;
            btnAceptar_704ILR.Click += (s_704ILR, e_704ILR) => { DialogResult = DialogResult.OK; Close(); };
            footer_704ILR.Controls.Add(_lblTotal_704ILR, 0, 0);
            footer_704ILR.Controls.Add(btnAceptar_704ILR, 1, 0);

            root_704ILR.Controls.Add(alta_704ILR, 0, 0);
            root_704ILR.Controls.Add(card_704ILR, 0, 1);
            root_704ILR.Controls.Add(footer_704ILR, 0, 2);

            Controls.Add(root_704ILR);
            Controls.Add(pnlTitle_704ILR);
            AcceptButton = btnAceptar_704ILR;
        }

        private static string TextoServicio_704ILR(BE_Servicio_704ILR sv_704ILR) =>
            sv_704ILR.Nombre_704ILR + "  \u2014  " + sv_704ILR.Precio_704ILR.ToString("N2");

        // Si "Nombre - Precio" entra en el renglon se dibuja igual que el resto de los
        // combos. Si no entra (la ficha del catalogo admite nombres de 80 caracteres), el
        // precio va completo a la derecha y el nombre se abrevia con "...": recortando el
        // final del texto, lo primero que se perdia era justamente el precio.
        private void DibujarServicio_704ILR(object sender_704ILR, DrawItemEventArgs e_704ILR)
        {
            e_704ILR.DrawBackground();
            if (e_704ILR.Index >= 0 && e_704ILR.Index < _cboServicio_704ILR.Items.Count)
            {
                const TextFormatFlags fl_704ILR = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
                object item_704ILR = _cboServicio_704ILR.Items[e_704ILR.Index];
                var sv_704ILR = item_704ILR as BE_Servicio_704ILR;
                string completo_704ILR = sv_704ILR != null ? TextoServicio_704ILR(sv_704ILR) : item_704ILR?.ToString() ?? string.Empty;
                bool entra_704ILR = TextRenderer.MeasureText(e_704ILR.Graphics, completo_704ILR, _cboServicio_704ILR.Font,
                    new Size(int.MaxValue, e_704ILR.Bounds.Height), fl_704ILR).Width <= e_704ILR.Bounds.Width;
                if (sv_704ILR == null || entra_704ILR)
                {
                    TextRenderer.DrawText(e_704ILR.Graphics, completo_704ILR, _cboServicio_704ILR.Font, e_704ILR.Bounds, e_704ILR.ForeColor, fl_704ILR);
                }
                else
                {
                    const TextFormatFlags flPrecio_704ILR = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                    string precio_704ILR = sv_704ILR.Precio_704ILR.ToString("N2");
                    int anchoPrecio_704ILR = Math.Min(e_704ILR.Bounds.Width, TextRenderer.MeasureText(e_704ILR.Graphics, precio_704ILR, _cboServicio_704ILR.Font,
                        new Size(int.MaxValue, e_704ILR.Bounds.Height), flPrecio_704ILR).Width);
                    var rPrecio_704ILR = new Rectangle(e_704ILR.Bounds.Right - anchoPrecio_704ILR, e_704ILR.Bounds.Top, anchoPrecio_704ILR, e_704ILR.Bounds.Height);
                    var rNombre_704ILR = new Rectangle(e_704ILR.Bounds.Left, e_704ILR.Bounds.Top,
                        Math.Max(0, rPrecio_704ILR.Left - e_704ILR.Bounds.Left - Theme_704ILR.SpaceSm_704ILR), e_704ILR.Bounds.Height);
                    TextRenderer.DrawText(e_704ILR.Graphics, sv_704ILR.Nombre_704ILR, _cboServicio_704ILR.Font, rNombre_704ILR, e_704ILR.ForeColor,
                        fl_704ILR | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(e_704ILR.Graphics, precio_704ILR, _cboServicio_704ILR.Font, rPrecio_704ILR, e_704ILR.ForeColor, flPrecio_704ILR);
                }
            }
            e_704ILR.DrawFocusRectangle();
        }

        // Ancho de la lista desplegada: el del item mas largo, para que en la lista cada
        // servicio se lea completo con su precio. Se calcula al desplegar, con la fuente y
        // el DPI vigentes; nunca es menor que el combo ni mayor que la pantalla.
        private int AnchoLista_704ILR()
        {
            int ancho_704ILR = _cboServicio_704ILR.Width;
            int barra_704ILR = _cboServicio_704ILR.Items.Count > _cboServicio_704ILR.MaxDropDownItems
                ? SystemInformation.GetVerticalScrollBarWidthForDpi(_cboServicio_704ILR.DeviceDpi) : 0;
            foreach (object o_704ILR in _cboServicio_704ILR.Items)
                if (o_704ILR is BE_Servicio_704ILR sv_704ILR)
                    ancho_704ILR = Math.Max(ancho_704ILR, TextRenderer.MeasureText(TextoServicio_704ILR(sv_704ILR), _cboServicio_704ILR.Font,
                        new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix).Width + barra_704ILR + 8);
            return Math.Min(ancho_704ILR, Screen.FromControl(_cboServicio_704ILR).WorkingArea.Width);
        }

        // Una unidad se suma a una linea existente solo si es del mismo servicio Y al
        // precio vigente: las unidades ya contratadas conservan su precio congelado y las
        // que se contratan hoy van al precio del catalogo (CUN004, pasos 2 y 5). Si el
        // precio cambio, la unidad nueva entra como linea aparte; antes se sumaba a la
        // linea vieja y quedaba valorizada al precio anterior.
        private void Agregar_704ILR()
        {
            if (!(_cboServicio_704ILR.SelectedItem is BE_Servicio_704ILR s_704ILR)) return;
            int cant_704ILR = (int)_numCantidad_704ILR.Value;
            var existente_704ILR = Items_704ILR.FirstOrDefault(i_704ILR =>
                i_704ILR.ServicioId_704ILR == s_704ILR.Id_704ILR && i_704ILR.PrecioUnitario_704ILR == s_704ILR.Precio_704ILR);
            if (existente_704ILR != null)
                existente_704ILR.Cantidad_704ILR += cant_704ILR;
            else
                Items_704ILR.Add(new BE_ReservaServicio_704ILR { ServicioId_704ILR = s_704ILR.Id_704ILR, ServicioNombre_704ILR = s_704ILR.Nombre_704ILR, Cantidad_704ILR = cant_704ILR, PrecioUnitario_704ILR = s_704ILR.Precio_704ILR });
            Refrescar_704ILR();
        }

        // Se quita el item POR REFERENCIA, no por posicion: la grilla se puede reordenar
        // haciendo clic en un encabezado y entonces el indice visual deja de coincidir
        // con el de la lista, con lo que se borraba un servicio distinto del elegido.
        private void Quitar_704ILR()
        {
            if (_grid_704ILR.CurrentRow?.Tag is BE_ReservaServicio_704ILR it_704ILR &&
                Items_704ILR.Remove(it_704ILR))
                Refrescar_704ILR();
        }

        private void Refrescar_704ILR()
        {
            _grid_704ILR.Rows.Clear();
            foreach (var it_704ILR in Items_704ILR)
            {
                int i_704ILR = _grid_704ILR.Rows.Add(it_704ILR.ServicioNombre_704ILR, it_704ILR.Cantidad_704ILR, it_704ILR.PrecioUnitario_704ILR, it_704ILR.Subtotal_704ILR);
                _grid_704ILR.Rows[i_704ILR].Tag = it_704ILR;
            }
            _lblTotal_704ILR.Text = T_704ILR("LBL_TOTAL", "Total") + ": " + Items_704ILR.Sum(i_704ILR => i_704ILR.Subtotal_704ILR).ToString("N2");
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }
}
