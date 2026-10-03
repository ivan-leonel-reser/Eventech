using System.Drawing;
using System.Windows.Forms;

namespace EvenTech.UI
{
    // Fabrica de controles con el estilo del design system. Evita repetir
    // configuracion de fuentes/colores en cada vista (cohesion + reuso).
    internal static class Ui_704ILR
    {
        // ---------- Tipografia / etiquetas (sobre fondo claro) ----------
        public static Label H1_704ILR(string text_704ILR = "") => Lbl_704ILR(text_704ILR, Theme_704ILR.FontH1_704ILR, Theme_704ILR.TextOnLight_704ILR);
        public static Label H2_704ILR(string text_704ILR = "") => Lbl_704ILR(text_704ILR, Theme_704ILR.FontH2_704ILR, Theme_704ILR.TextOnLight_704ILR);
        public static Label Title_704ILR(string text_704ILR = "") => Lbl_704ILR(text_704ILR, Theme_704ILR.FontTitle_704ILR, Theme_704ILR.TextOnLight_704ILR);
        public static Label Body_704ILR(string text_704ILR = "") => Lbl_704ILR(text_704ILR, Theme_704ILR.FontBody_704ILR, Theme_704ILR.TextOnLight_704ILR);
        public static Label BodyBold_704ILR(string text_704ILR = "") => Lbl_704ILR(text_704ILR, Theme_704ILR.FontBodyBold_704ILR, Theme_704ILR.TextOnLight_704ILR);
        public static Label Caption_704ILR(string text_704ILR = "") => Lbl_704ILR(text_704ILR, Theme_704ILR.FontCaption_704ILR, Theme_704ILR.TextMuted_704ILR);
        public static Label FieldLabel_704ILR(string text_704ILR = "") => Lbl_704ILR(text_704ILR, Theme_704ILR.FontSmall_704ILR, Theme_704ILR.TextMuted_704ILR);

        private static Label Lbl_704ILR(string text_704ILR, Font f_704ILR, Color c_704ILR) => new Label
        {
            Text = text_704ILR,
            Font = f_704ILR,
            ForeColor = c_704ILR,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceXs_704ILR)
        };

        // ---------- Botones ----------
        public static AppButton_704ILR Primary_704ILR(string text_704ILR, string glyph_704ILR = null) => new AppButton_704ILR { Text = text_704ILR, Glyph_704ILR = glyph_704ILR };
        public static AppButton_704ILR Secondary_704ILR(string text_704ILR, string glyph_704ILR = null) => new AppButton_704ILR
        {
            Text = text_704ILR,
            Glyph_704ILR = glyph_704ILR,
            BaseColor_704ILR = Theme_704ILR.Neutral_704ILR,
            HoverColor_704ILR = Theme_704ILR.NeutralHover_704ILR,
            DownColor_704ILR = Theme_704ILR.NeutralDown_704ILR
        };

        // ---------- Inputs sobre fondo claro ----------
        public static TextBox Input_704ILR() => new TextBox
        {
            Font = Theme_704ILR.FontInput_704ILR,
            BorderStyle = BorderStyle.FixedSingle
        };

        // El fondo se fija explicitamente: un combo Flat sin BackColor queda gris y en
        // pantalla se lee como deshabilitado, al lado de los que dibuja DibujarEnum
        // (que los repinta con el color de superficie). Un solo estilo para todos.
        public static ComboBox Combo_704ILR()
        {
            var cbo_704ILR = new ComboBox
            {
                Font = Theme_704ILR.FontInput_704ILR,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme_704ILR.Surface_704ILR
            };
            // Un combo plano no se repinta entero cuando la disposicion lo ensancha: Windows
            // invalida solo la franja nueva y ahi queda a la vista el combo nativo (borde y
            // flecha del tema), con la flecha plana anterior en el medio del control. Al
            // cambiar de tamano se repinta completo.
            cbo_704ILR.Resize += (s_704ILR, e_704ILR) => cbo_704ILR.Invalidate();
            return cbo_704ILR;
        }

        // Hace que un ComboBox dibuje cada item con texto traducido EN VIVO: como el
        // texto se resuelve en cada repintado, al cambiar el idioma basta con un
        // Invalidate() para refrescar (incluido el item seleccionado cerrado).
        // textOf convierte el item (enum/wrapper/string) al texto a mostrar.
        public static void DibujarEnum_704ILR(ComboBox cbo_704ILR, System.Func<object, string> textOf_704ILR)
        {
            cbo_704ILR.DrawMode = DrawMode.OwnerDrawFixed;
            cbo_704ILR.DrawItem += (s_704ILR, e_704ILR) =>
            {
                // Deshabilitado, el item se dibuja como el de un combo comun deshabilitado: sobre el fondo
                // del control, con el texto en el gris del sistema y sin resaltado ni foco. Con el dibujo
                // del estado normal se seguia viendo en negro, como editable, al lado de Cliente o Salon
                // ya grises (la ficha de una reserva cancelada, incompleta o sin permiso de edicion).
                bool habilitado_704ILR = cbo_704ILR.Enabled;
                if (habilitado_704ILR)
                    e_704ILR.DrawBackground();
                else
                    using (var fondo_704ILR = new SolidBrush(cbo_704ILR.BackColor))
                        e_704ILR.Graphics.FillRectangle(fondo_704ILR, e_704ILR.Bounds);
                if (e_704ILR.Index >= 0 && e_704ILR.Index < cbo_704ILR.Items.Count)
                {
                    string txt_704ILR = textOf_704ILR(cbo_704ILR.Items[e_704ILR.Index]) ?? string.Empty;
                    var flags_704ILR = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
                    // Una traduccion que no entra en el item (por ejemplo, una editada desde la
                    // gestion de idiomas) termina en puntos suspensivos en lugar de cortarse en
                    // seco a mitad de palabra. Se decide midiendo el texto sin el margen derecho
                    // que reserva TextRenderer: EndEllipsis a secas acortaba tambien textos que
                    // hoy se ven completos ("Confirmada" en el Estado de la ficha de Reservas).
                    var alto_704ILR = new Size(int.MaxValue, e_704ILR.Bounds.Height);
                    int sinMargen_704ILR = TextRenderer.MeasureText(e_704ILR.Graphics, txt_704ILR, cbo_704ILR.Font, alto_704ILR, flags_704ILR | TextFormatFlags.NoPadding).Width;
                    int conMargen_704ILR = TextRenderer.MeasureText(e_704ILR.Graphics, txt_704ILR, cbo_704ILR.Font, alto_704ILR, flags_704ILR).Width;
                    if ((conMargen_704ILR - sinMargen_704ILR) / 2 + sinMargen_704ILR > e_704ILR.Bounds.Width)
                        flags_704ILR |= TextFormatFlags.EndEllipsis;
                    TextRenderer.DrawText(e_704ILR.Graphics, txt_704ILR, cbo_704ILR.Font, e_704ILR.Bounds,
                        habilitado_704ILR ? e_704ILR.ForeColor : SystemColors.GrayText, flags_704ILR);
                }
                if (habilitado_704ILR) e_704ILR.DrawFocusRectangle();
            };
        }

        // Formato fijo yyyy-MM-dd: el formato corto de la estacion muestra la fecha
        // rellenada con espacios ("17/ 1/2027") y no coincide con el de las grillas ni
        // con el de los comprobantes, de modo que el mismo dato se veia de dos maneras
        // en la misma pantalla.
        public static DateTimePicker DatePicker_704ILR() => new DateTimePicker
        {
            Font = Theme_704ILR.FontInput_704ILR,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd"
        };

        // Selector de hora (HH:mm, 24 horas) con flechas: las franjas de trabajo y las
        // horas del cronograma se cargan sin fecha. La fecha interna del control no se
        // usa: se lee Value.TimeOfDay. El formato es fijo, como el de las fechas.
        public static DateTimePicker TimePicker_704ILR(int hora_704ILR = 0, int minuto_704ILR = 0) => new DateTimePicker
        {
            Font = Theme_704ILR.FontInput_704ILR,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 78,
            Value = new System.DateTime(2000, 1, 1, hora_704ILR, minuto_704ILR, 0)
        };

        // ---------- Campo etiquetado (caption arriba, input abajo) ----------
        // Devuelve un panel auto-ajustable apto para apilar en un FlowLayoutPanel
        // o ubicar en una celda de TableLayoutPanel (Dock=Top/Fill).
        public static TableLayoutPanel Field_704ILR(string caption_704ILR, Control input_704ILR, int inputHeight_704ILR = 30)
        {
            var t_704ILR = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };
            t_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, inputHeight_704ILR + 2));
            input_704ILR.Dock = DockStyle.Fill;
            input_704ILR.Margin = new Padding(0);
            var lbl_704ILR = FieldLabel_704ILR(caption_704ILR);
            lbl_704ILR.Margin = new Padding(2, 0, 0, 2);
            t_704ILR.Controls.Add(lbl_704ILR, 0, 0);
            t_704ILR.Controls.Add(input_704ILR, 0, 1);
            return t_704ILR;
        }

        // ---------- Campo oscuro (estilo login): caption + input con subrayado dorado ----------
        // Devuelve tambien el caption Label (out) para poder re-traducirlo (Observer).
        public static TableLayoutPanel DarkField_704ILR(string caption_704ILR, bool password_704ILR, out TextBox box_704ILR, out Label captionLabel_704ILR)
        {
            var t_704ILR = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 2,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };
            t_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            t_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            captionLabel_704ILR = new Label
            {
                Text = caption_704ILR,
                Font = Theme_704ILR.FontSmall_704ILR,
                ForeColor = Theme_704ILR.TextLight_704ILR,
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var inputBox_704ILR = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme_704ILR.BgInput_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceSm_704ILR, 6, Theme_704ILR.SpaceSm_704ILR, 0),
                Margin = new Padding(0)
            };
            box_704ILR = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Theme_704ILR.FontInput_704ILR,
                BackColor = Theme_704ILR.BgInput_704ILR,
                ForeColor = Theme_704ILR.TextOnDark_704ILR,
                Dock = DockStyle.Top,
                UseSystemPasswordChar = password_704ILR
            };
            var underline_704ILR = new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = Theme_704ILR.Accent_704ILR };

            // Orden de Add = z-order: el ultimo agregado (underline) ancla primero
            // (Bottom, ancho completo); el ojo (Right) queda encima; el textbox
            // (Top) toma el ancho restante a la izquierda del ojo.
            inputBox_704ILR.Controls.Add(box_704ILR);
            if (password_704ILR)
            {
                var theBox_704ILR = box_704ILR;
                var eye_704ILR = new Label
                {
                    Text = Theme_704ILR.IcoEye_704ILR,
                    Font = new Font("Segoe MDL2 Assets", 11F),
                    ForeColor = Theme_704ILR.TextLight_704ILR,
                    Dock = DockStyle.Right,
                    Width = 30,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                    BackColor = Theme_704ILR.BgInput_704ILR
                };
                eye_704ILR.MouseEnter += (s_704ILR, e_704ILR) => eye_704ILR.ForeColor = Theme_704ILR.TextOnDark_704ILR;
                eye_704ILR.MouseLeave += (s_704ILR, e_704ILR) => eye_704ILR.ForeColor = Theme_704ILR.TextLight_704ILR;
                // Con la clave a la vista, el mismo ojo se dibuja tachado. La fuente de
                // iconos de Windows 10 (Segoe MDL2 Assets) no trae un ojo tachado: el
                // glifo que se usaba solo existe en la de Windows 11 y en Windows 10 se
                // veia como un recuadro vacio. La tachadura escala con la fuente (DPI).
                eye_704ILR.Paint += (s_704ILR, e_704ILR) =>
                {
                    if (theBox_704ILR.UseSystemPasswordChar) return;
                    int cx_704ILR = eye_704ILR.ClientSize.Width / 2;
                    int cy_704ILR = eye_704ILR.ClientSize.Height / 2;
                    int d_704ILR = System.Math.Max(4, eye_704ILR.Font.Height * 2 / 5);
                    e_704ILR.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var pen_704ILR = new Pen(eye_704ILR.ForeColor, System.Math.Max(1.5F, eye_704ILR.Font.Height / 9F)))
                        e_704ILR.Graphics.DrawLine(pen_704ILR, cx_704ILR - d_704ILR, cy_704ILR + d_704ILR, cx_704ILR + d_704ILR, cy_704ILR - d_704ILR);
                };
                eye_704ILR.Click += (s_704ILR, e_704ILR) =>
                {
                    theBox_704ILR.UseSystemPasswordChar = !theBox_704ILR.UseSystemPasswordChar;
                    eye_704ILR.Invalidate();
                };
                inputBox_704ILR.Controls.Add(eye_704ILR);
            }
            inputBox_704ILR.Controls.Add(underline_704ILR);

            t_704ILR.Controls.Add(captionLabel_704ILR, 0, 0);
            t_704ILR.Controls.Add(inputBox_704ILR, 0, 1);
            return t_704ILR;
        }

        // Vuelve a ocultar la contrasena de un campo armado con DarkField (lo usa el
        // login al volver de la ventana principal: el que ingresa despues no tiene que
        // tipear su clave a la vista). El ojo se repinta segun el estado del campo.
        public static void OcultarClave_704ILR(TextBox box_704ILR)
        {
            if (box_704ILR == null) return;
            box_704ILR.UseSystemPasswordChar = true;
            box_704ILR.Parent?.Invalidate(true);
        }

        // ---------- Selector de idioma compacto para barras oscuras ----------
        public static ComboBox LangCombo_704ILR() => new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = Theme_704ILR.FontCaption_704ILR,
            BackColor = Theme_704ILR.BgTitleBar_704ILR,
            ForeColor = Theme_704ILR.TextOnDark_704ILR
        };
    }
}
