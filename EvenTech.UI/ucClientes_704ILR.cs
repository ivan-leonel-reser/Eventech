using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Gestion de clientes (Proceso 1): grilla + ficha de alta/edicion.
    // Mismo patron visual que ucReservas. Observa el cambio de idioma y, como Reservas,
    // informa a frmMain si la ficha tiene cambios sin guardar (IVistaConCambios).
    public class ucClientes_704ILR : UserControl, IObservadorIdioma_704ILR, IVistaConCambios_704ILR
    {
        private DataGridView _grid_704ILR;
        private Label _lblCount_704ILR, _lblError_704ILR, _lblOk_704ILR, _lblFormTitle_704ILR;
        private TextBox _txtNombre_704ILR, _txtApellido_704ILR, _txtDni_704ILR, _txtEmail_704ILR, _txtTelefono_704ILR;
        private AppButton_704ILR _btnNuevo_704ILR, _btnGuardar_704ILR;
        private int _editId_704ILR;
        // Texto de los mensajes a la vista, guardado como funcion: el cambio de idioma lo
        // vuelve a evaluar, asi un error o una confirmacion visible se traduce con el resto
        // de la pantalla (el detalle de una excepcion se conserva tal cual).
        private Func<string> _textoError_704ILR, _textoOk_704ILR;
        // Linea base de la ficha: lo que mostraba al cargar un cliente (CargarEnForm) o al
        // limpiarse para un alta (LimpiarForm); tras guardar se pasa por las dos, asi que
        // tambien es lo guardado. Contra ella se decide si hay cambios sin guardar
        // (IVistaConCambios): frmMain pregunta antes de reemplazar la vista y la propia vista
        // antes de reemplazar la ficha (otra fila de la grilla o Nuevo), como en Reservas.
        private string[] _lineaBase_704ILR;
        // Cambios de seleccion que hace la pantalla y no el usuario (recargar la grilla, marcar el
        // cliente recien guardado): cargan la ficha sin preguntar. Y mientras la grilla vuelve a
        // la fila de la ficha tras responder "No", los demas cambios de seleccion del mismo gesto
        // no preguntan de nuevo. Mismo criterio que Reservas.
        private int _seleccionProgramada_704ILR, _seleccionSuspendida_704ILR;
        // El menor minimo de columna que admite la grilla.
        private const int MinimoColumna_704ILR = 2;

        public ucClientes_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => { LimpiarForm_704ILR(); SafeLoadData_704ILR(); GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this); };
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        private void BuildUi_704ILR()
        {
            var root_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme_704ILR.BgContent_704ILR };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root_704ILR.Controls.Add(BuildHeader_704ILR(), 0, 0);
            root_704ILR.Controls.Add(BuildBody_704ILR(), 0, 1);
            Controls.Add(root_704ILR);
        }

        private Control BuildHeader_704ILR()
        {
            var header_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4, RowCount = 2, BackColor = Theme_704ILR.BgContent_704ILR, Padding = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Gestión de Clientes");
            lblTitle_704ILR.Tag = "T:CLI_TITULO"; lblTitle_704ILR.Anchor = AnchorStyles.Left; lblTitle_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);

            // Cliente y Servicio son masculinos: llevan BTN_NUEVO. BTN_NUEVA queda para
            // Reservas, que es la unica seccion cuyo sustantivo es femenino.
            _btnNuevo_704ILR = Ui_704ILR.Primary_704ILR("Nuevo", Theme_704ILR.IcoAdd_704ILR);
            _btnNuevo_704ILR.Tag = "T:BTN_NUEVO"; _btnNuevo_704ILR.Size = new Size(120, 36); _btnNuevo_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnNuevo_704ILR.Anchor = AnchorStyles.Left; _btnNuevo_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            // Nuevo reemplaza la ficha: con cambios sin guardar se pregunta antes (el mismo aviso
            // que al cambiar de seccion) y "No" deja la ficha como estaba, igual que en Reservas.
            _btnNuevo_704ILR.Click += (s_704ILR, e_704ILR) => { if (ConfirmarDescarte_704ILR()) LimpiarForm_704ILR(); };

            _lblCount_704ILR = Ui_704ILR.Body_704ILR(); _lblCount_704ILR.ForeColor = Theme_704ILR.TextMuted_704ILR; _lblCount_704ILR.Anchor = AnchorStyles.Left;

            _lblError_704ILR = Ui_704ILR.Body_704ILR(); _lblError_704ILR.Font = Theme_704ILR.FontBodyBold_704ILR; _lblError_704ILR.ForeColor = Theme_704ILR.Error_704ILR;
            _lblError_704ILR.Visible = false; _lblError_704ILR.AutoSize = true; _lblError_704ILR.MaximumSize = new Size(900, 0);
            _lblError_704ILR.Anchor = AnchorStyles.Left; _lblError_704ILR.Margin = new Padding(0, Theme_704ILR.SpaceXs_704ILR, 0, 0);

            header_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            header_704ILR.Controls.Add(_btnNuevo_704ILR, 1, 0);
            header_704ILR.Controls.Add(_lblCount_704ILR, 2, 0);
            header_704ILR.Controls.Add(_lblError_704ILR, 0, 1);
            header_704ILR.SetColumnSpan(_lblError_704ILR, 4);
            return header_704ILR;
        }

        private Control BuildBody_704ILR()
        {
            var body_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme_704ILR.BgContent_704ILR, Margin = new Padding(0) };
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body_704ILR.Controls.Add(BuildGridCard_704ILR(), 0, 0);
            body_704ILR.Controls.Add(BuildFormCard_704ILR(), 1, 0);
            return body_704ILR;
        }

        private Control BuildGridCard_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNombre",   HeaderText = "Nombre",   DataPropertyName = "Nombre_704ILR",   FillWeight = 60 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cApellido", HeaderText = "Apellido", DataPropertyName = "Apellido_704ILR", FillWeight = 60 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDni",      HeaderText = "DNI",      DataPropertyName = "Dni_704ILR",      FillWeight = 45 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEmail",    HeaderText = "Email",    DataPropertyName = "Email_704ILR",    FillWeight = 90 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTelefono", HeaderText = "Telefono", DataPropertyName = "Telefono_704ILR", FillWeight = 60 });
            _grid_704ILR.SelectionChanged += Grid_SelectionChanged_704ILR;
            _grid_704ILR.SizeChanged += Grid_SizeChanged_704ILR;
            card_704ILR.Controls.Add(_grid_704ILR);
            return card_704ILR;
        }

        private Control BuildFormCard_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, MinimumSize = new Size(280, 0), Margin = new Padding(0), Padding = new Padding(Theme_704ILR.SpaceLg_704ILR) };
            var layout_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
            layout_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblFormTitle_704ILR = Ui_704ILR.Title_704ILR("Nuevo cliente");
            _lblFormTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            var fields_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, AutoScroll = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            fields_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i_704ILR = 0; i_704ILR < 5; i_704ILR++) fields_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Mismos topes que la ficha de alta (frmNuevoCliente). La BLL los hace cumplir
            // tambien para un valor que no llegue tipeado.
            _txtNombre_704ILR = Ui_704ILR.Input_704ILR(); _txtNombre_704ILR.MaxLength = 60; var fN_704ILR = Field_704ILR(_txtNombre_704ILR, "COL_NOMBRE", "Nombre");
            _txtApellido_704ILR = Ui_704ILR.Input_704ILR(); _txtApellido_704ILR.MaxLength = 60; var fA_704ILR = Field_704ILR(_txtApellido_704ILR, "COL_APELLIDO", "Apellido");
            _txtDni_704ILR = Ui_704ILR.Input_704ILR(); _txtDni_704ILR.MaxLength = 20; var fD_704ILR = Field_704ILR(_txtDni_704ILR, "COL_DNI", "DNI");
            _txtEmail_704ILR = Ui_704ILR.Input_704ILR(); _txtEmail_704ILR.MaxLength = 120; var fE_704ILR = Field_704ILR(_txtEmail_704ILR, "COL_EMAIL", "Email");
            _txtTelefono_704ILR = Ui_704ILR.Input_704ILR(); _txtTelefono_704ILR.MaxLength = 30; var fT_704ILR = Field_704ILR(_txtTelefono_704ILR, "COL_TELEFONO", "Teléfono");
            int row_704ILR = 0;
            foreach (var f_704ILR in new[] { fN_704ILR, fA_704ILR, fD_704ILR, fE_704ILR, fT_704ILR }) { f_704ILR.Dock = DockStyle.Fill; f_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR); fields_704ILR.Controls.Add(f_704ILR, 0, row_704ILR++); }

            var actions_704ILR = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0) };
            actions_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            actions_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _btnGuardar_704ILR = Ui_704ILR.Primary_704ILR("Guardar", Theme_704ILR.IcoSave_704ILR);
            _btnGuardar_704ILR.Tag = "T:BTN_GUARDAR"; _btnGuardar_704ILR.Dock = DockStyle.Fill; _btnGuardar_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR);
            _btnGuardar_704ILR.Click += (s_704ILR, e_704ILR) => Guardar_704ILR();
            _lblOk_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.Success_704ILR, Visible = false, BackColor = Color.Transparent };
            actions_704ILR.Controls.Add(_btnGuardar_704ILR, 0, 0);
            actions_704ILR.Controls.Add(_lblOk_704ILR, 0, 1);

            layout_704ILR.Controls.Add(_lblFormTitle_704ILR, 0, 0);
            layout_704ILR.Controls.Add(fields_704ILR, 0, 1);
            layout_704ILR.Controls.Add(actions_704ILR, 0, 2);
            card_704ILR.Controls.Add(layout_704ILR);
            return card_704ILR;
        }

        private TableLayoutPanel Field_704ILR(Control input_704ILR, string tagKey_704ILR, string defecto_704ILR)
        {
            var f_704ILR = Ui_704ILR.Field_704ILR(T_704ILR(tagKey_704ILR, defecto_704ILR), input_704ILR);
            ((Label)f_704ILR.GetControlFromPosition(0, 0)).Tag = "T:" + tagKey_704ILR;
            return f_704ILR;
        }

        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            if (_grid_704ILR.Columns.Count >= 5)
            {
                _grid_704ILR.Columns["cNombre"].HeaderText   = Tr_704ILR.T_704ILR("COL_NOMBRE");
                _grid_704ILR.Columns["cApellido"].HeaderText = Tr_704ILR.T_704ILR("COL_APELLIDO");
                _grid_704ILR.Columns["cDni"].HeaderText      = Tr_704ILR.T_704ILR("COL_DNI");
                _grid_704ILR.Columns["cEmail"].HeaderText    = Tr_704ILR.T_704ILR("COL_EMAIL");
                _grid_704ILR.Columns["cTelefono"].HeaderText = Tr_704ILR.T_704ILR("COL_TELEFONO");
                AjustarMinimosDeEncabezado_704ILR();
            }
            _lblFormTitle_704ILR.Text = _editId_704ILR == 0 ? T_704ILR("CLI_NUEVO", "Nuevo cliente") : T_704ILR("CLI_FORM_EDITAR", "Editar cliente") + " #" + _editId_704ILR;
            if (_textoError_704ILR != null) _lblError_704ILR.Text = _textoError_704ILR();
            if (_textoOk_704ILR != null) _lblOk_704ILR.Text = _textoOk_704ILR();
            ActualizarCount_704ILR();
        }

        // Minimo de cada columna: su encabezado entero, medido en el idioma activo con la
        // letra real en lugar de fijar pixeles (mismo criterio que la grilla de Reservas:
        // texto + Padding del estilo + 4 px de la celda de encabezado + 1 px de holgura para
        // el redondeo del reparto). Con la ventana en su tamano minimo el reparto proporcional
        // cortaba "Sobrenome"; un rotulo traducido mas largo agranda su columna en vez de
        // cortarse. Al cambiar de idioma con la pantalla abierta se recalcula el reparto con
        // los minimos nuevos.
        // Ese minimo tiene tope: la cuarta parte del ancho de la grilla. Los rotulos de
        // fabrica quedan por debajo (el mas largo, "Sobrenome", pide 102 px y la grilla mide
        // 492 px con la ventana en su tamano minimo), pero uno desmedido cargado desde la
        // gestion de idiomas se media entero: empujaba a Telefono fuera de la vista con una
        // barra horizontal o dejaba a Email en su minimo con todos los correos recortados.
        // Con el tope ese rotulo vuelve a cortarse con puntos suspensivos y el resto del
        // reparto se conserva. Como el tope depende del ancho, se recalcula tambien al
        // cambiar de tamano (ver Grid_SizeChanged).
        // El tope por si solo no garantiza que el reparto entre: ver EncajarReparto.
        private void AjustarMinimosDeEncabezado_704ILR()
        {
            var enc_704ILR = _grid_704ILR.ColumnHeadersDefaultCellStyle;
            int tope_704ILR = Math.Max(MinimoColumna_704ILR, Math.Max(110, _grid_704ILR.ClientSize.Width / 4));   // piso: los rotulos de fabrica en un area de trabajo chica
            // Subir el minimo de una columna por encima de su ancho actual hace que la grilla,
            // en modo Fill, reescriba los PESOS de todas las columnas para darle ese ancho: tras
            // pasar por portugues quedaban 60/65,3/43,8/87,6/58,4 en lugar de 60/60/45/90/60 y
            // el reparto seguia deformado al volver a espanol hasta salir de la seccion. Por eso
            // los pesos se toman antes de fijar los minimos y se restituyen despues.
            int n_704ILR = _grid_704ILR.Columns.Count;
            var pesos_704ILR = new float[n_704ILR];
            var minimos_704ILR = new int[n_704ILR];
            // Cortable: el rotulo mide mas que el tope y se muestra cortado con puntos suspensivos.
            var cortable_704ILR = new bool[n_704ILR];
            for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                pesos_704ILR[i_704ILR] = _grid_704ILR.Columns[i_704ILR].FillWeight;
            for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
            {
                var col_704ILR = _grid_704ILR.Columns[i_704ILR];
                int medido_704ILR = TextRenderer.MeasureText(col_704ILR.HeaderText ?? string.Empty, enc_704ILR.Font).Width
                    + enc_704ILR.Padding.Horizontal + 4 + 1;
                minimos_704ILR[i_704ILR] = Math.Max(MinimoColumna_704ILR, Math.Min(medido_704ILR, tope_704ILR));
                cortable_704ILR[i_704ILR] = medido_704ILR > tope_704ILR;
                col_704ILR.MinimumWidth = minimos_704ILR[i_704ILR];
            }
            RecalcularReparto_704ILR(pesos_704ILR);
            EncajarReparto_704ILR(minimos_704ILR, cortable_704ILR, pesos_704ILR);
        }

        // En modo Fill la grilla fija en su minimo, en UNA sola pasada, las columnas cuya parte del
        // ancho total no llega a ese minimo y reparte el resto entre las demas segun su peso. Si con
        // ese resto a otra columna tampoco le alcanza, la grilla igual la agranda hasta su minimo sin
        // achicar las que ya repartio: la suma de anchos supera el ancho visible y aparece la barra
        // horizontal aunque los minimos entren (tres rotulos largos con la ventana en su tamano de
        // diseno: 674 px en 656; los rotulos de fabrica en ingles en un area de 880x600: 361 en 335).
        // Esa barra ademas puede traer la vertical (las filas dejan de entrar a lo alto), y el ancho
        // disponible cambia mientras se corrige. Por eso, solo cuando la suma no entra:
        //  1. se reparte sin minimos: sin barra horizontal, la vertical queda como va a quedar;
        //  2. para ese ancho se calculan los minimos con los que el reparto entra (MinimosQueEntran);
        //  3. se aplican, se reparte y se comprueba con los anchos reales. Un minimo que se subio solo
        //     para que la grilla lo fije en su primera pasada vuelve despues a su valor (bajarlo no
        //     cambia anchos ni pesos); los rotulos cortables quedan con el minimo bajado.
        // Los pesos no cambian. Si los minimos no entran ni cortando, queda la barra, como antes.
        private void EncajarReparto_704ILR(int[] medidos_704ILR, bool[] cortable_704ILR, float[] pesos_704ILR)
        {
            int n_704ILR = Math.Min(medidos_704ILR.Length, _grid_704ILR.Columns.Count);
            if (n_704ILR == 0 || !_grid_704ILR.IsHandleCreated || _grid_704ILR.ClientSize.Width <= 0) return;
            if (SumaAnchos_704ILR(n_704ILR) <= AnchoDisponible_704ILR()) return;
            int sumaMin_704ILR = 0, bajable_704ILR = 0;
            for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
            {
                sumaMin_704ILR += medidos_704ILR[i_704ILR];
                if (cortable_704ILR[i_704ILR]) bajable_704ILR += medidos_704ILR[i_704ILR] - MinimoColumna_704ILR;
            }
            // No entran ni sin barra vertical ni cortando los rotulos: queda la barra.
            if (sumaMin_704ILR - bajable_704ILR > _grid_704ILR.ClientSize.Width) return;

            for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++) _grid_704ILR.Columns[i_704ILR].MinimumWidth = MinimoColumna_704ILR;
            ForzarReparto_704ILR(pesos_704ILR);
            int holgura_704ILR = 0;
            for (int vuelta_704ILR = 0; vuelta_704ILR < 3; vuelta_704ILR++)
            {
                var minimos_704ILR = (int[])medidos_704ILR.Clone();
                bool entra_704ILR = MinimosQueEntran_704ILR(minimos_704ILR, cortable_704ILR, pesos_704ILR, AnchoDisponible_704ILR() - holgura_704ILR);
                if (!entra_704ILR) minimos_704ILR = (int[])medidos_704ILR.Clone();
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++) _grid_704ILR.Columns[i_704ILR].MinimumWidth = minimos_704ILR[i_704ILR];
                ForzarReparto_704ILR(pesos_704ILR);
                if (!entra_704ILR) return;
                int suma_704ILR = SumaAnchos_704ILR(n_704ILR), disp_704ILR = AnchoDisponible_704ILR();
                if (suma_704ILR <= disp_704ILR)
                {
                    for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                        if (minimos_704ILR[i_704ILR] > medidos_704ILR[i_704ILR]) _grid_704ILR.Columns[i_704ILR].MinimumWidth = medidos_704ILR[i_704ILR];
                    return;
                }
                // Un pixel de redondeo de la grilla: se vuelve a calcular con ese margen.
                holgura_704ILR += suma_704ILR - disp_704ILR;
            }
            // No se logro: quedan los minimos medidos, como sin este ajuste.
            for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++) _grid_704ILR.Columns[i_704ILR].MinimumWidth = medidos_704ILR[i_704ILR];
            ForzarReparto_704ILR(pesos_704ILR);
        }

        // Modelo del reparto Fill de la grilla (ver EncajarReparto). Recibe en 'minimos' los medidos y
        // deja unos con los que la suma de anchos entra en 'disp'; devuelve false si no los hay.
        //  * Si los minimos no entran, se bajan los de rotulo cortable, desde el mas ancho.
        //  * Si a una columna que la grilla no fija en la primera pasada no le alcanza su parte del
        //    resto: si su rotulo es cortable, su minimo baja a esa parte; si no, se bajan lo necesario
        //    los cortables fijados y, si no los hay, la columna pasa a fijarse en la primera pasada con
        //    su parte del total mas 1 px (su rotulo sigue entero).
        private static bool MinimosQueEntran_704ILR(int[] minimos_704ILR, bool[] cortable_704ILR, float[] pesos_704ILR, int disp_704ILR)
        {
            int n_704ILR = minimos_704ILR.Length;
            float total_704ILR = 0;
            for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++) total_704ILR += pesos_704ILR[i_704ILR];
            if (total_704ILR <= 0 || disp_704ILR <= 0) return false;
            for (int vuelta_704ILR = 0; vuelta_704ILR < 4 * n_704ILR; vuelta_704ILR++)
            {
                int sumaMin_704ILR = 0, bajable_704ILR = 0;
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                {
                    sumaMin_704ILR += minimos_704ILR[i_704ILR];
                    if (cortable_704ILR[i_704ILR]) bajable_704ILR += minimos_704ILR[i_704ILR] - MinimoColumna_704ILR;
                }
                if (sumaMin_704ILR > disp_704ILR)
                {
                    if (sumaMin_704ILR - bajable_704ILR > disp_704ILR) return false;
                    BajarMasAnchos_704ILR(minimos_704ILR, cortable_704ILR, sumaMin_704ILR - disp_704ILR);
                    continue;
                }
                if (sumaMin_704ILR == disp_704ILR) return true;   // la grilla deja todas en su minimo

                // La parte de cada columna tal como la calcula la grilla: redondeada, y la ultima se
                // queda con lo que sobra. Fija: su parte no llega a su minimo.
                var cuota_704ILR = new int[n_704ILR];
                int usado_704ILR = 0;
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                {
                    cuota_704ILR[i_704ILR] = i_704ILR == n_704ILR - 1
                        ? disp_704ILR - usado_704ILR
                        : (int)Math.Round(pesos_704ILR[i_704ILR] / total_704ILR * disp_704ILR, MidpointRounding.AwayFromZero);
                    if (i_704ILR < n_704ILR - 1) usado_704ILR += cuota_704ILR[i_704ILR];
                }
                var fija_704ILR = new bool[n_704ILR];
                int minFijas_704ILR = 0;
                float pesoFijas_704ILR = 0;
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                {
                    fija_704ILR[i_704ILR] = cuota_704ILR[i_704ILR] < minimos_704ILR[i_704ILR];
                    if (fija_704ILR[i_704ILR]) { minFijas_704ILR += minimos_704ILR[i_704ILR]; pesoFijas_704ILR += pesos_704ILR[i_704ILR]; }
                }
                if (total_704ILR - pesoFijas_704ILR <= 0) return true;
                double porPeso_704ILR = (double)(disp_704ILR - minFijas_704ILR) / (total_704ILR - pesoFijas_704ILR);
                var falta_704ILR = new bool[n_704ILR];
                bool hayFalta_704ILR = false;
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                {
                    falta_704ILR[i_704ILR] = !fija_704ILR[i_704ILR] && pesos_704ILR[i_704ILR] * porPeso_704ILR < minimos_704ILR[i_704ILR];
                    hayFalta_704ILR |= falta_704ILR[i_704ILR];
                }
                if (!hayFalta_704ILR) return true;

                bool cambio_704ILR = false;
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                {
                    if (!falta_704ILR[i_704ILR] || !cortable_704ILR[i_704ILR]) continue;
                    int nuevo_704ILR = Math.Max(MinimoColumna_704ILR, (int)Math.Floor(pesos_704ILR[i_704ILR] * porPeso_704ILR));
                    if (nuevo_704ILR < minimos_704ILR[i_704ILR]) { minimos_704ILR[i_704ILR] = nuevo_704ILR; cambio_704ILR = true; }
                }
                if (cambio_704ILR) continue;

                double razon_704ILR = 0;
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                    if (falta_704ILR[i_704ILR]) razon_704ILR = Math.Max(razon_704ILR, (double)minimos_704ILR[i_704ILR] / pesos_704ILR[i_704ILR]);
                int bajar_704ILR = minFijas_704ILR - (int)Math.Floor(disp_704ILR - razon_704ILR * (total_704ILR - pesoFijas_704ILR));
                var fijaCortable_704ILR = new bool[n_704ILR];
                int bajableFijas_704ILR = 0;
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                    if (fija_704ILR[i_704ILR] && cortable_704ILR[i_704ILR])
                    {
                        fijaCortable_704ILR[i_704ILR] = true;
                        bajableFijas_704ILR += minimos_704ILR[i_704ILR] - MinimoColumna_704ILR;
                    }
                if (bajar_704ILR > 0 && bajableFijas_704ILR >= bajar_704ILR)
                {
                    BajarMasAnchos_704ILR(minimos_704ILR, fijaCortable_704ILR, bajar_704ILR);
                    continue;
                }
                for (int i_704ILR = 0; i_704ILR < n_704ILR; i_704ILR++)
                {
                    int nuevo_704ILR = cuota_704ILR[i_704ILR] + 1;
                    if (!falta_704ILR[i_704ILR] || sumaMin_704ILR - minimos_704ILR[i_704ILR] + nuevo_704ILR > disp_704ILR) continue;
                    sumaMin_704ILR += nuevo_704ILR - minimos_704ILR[i_704ILR];
                    minimos_704ILR[i_704ILR] = nuevo_704ILR;
                    cambio_704ILR = true;
                }
                if (!cambio_704ILR) return false;
            }
            return false;
        }

        // Reparto desde los pesos aunque ninguno haya cambiado: asignar a una columna el peso que ya
        // tiene no hace nada (la grilla no vuelve a repartir), asi que el de la primera se mueve un
        // 0,1% y vuelve a su valor.
        private void ForzarReparto_704ILR(float[] pesos_704ILR)
        {
            if (_grid_704ILR.Columns.Count == 0 || pesos_704ILR.Length == 0) return;
            _grid_704ILR.Columns[0].FillWeight = pesos_704ILR[0] > 1f ? pesos_704ILR[0] * 0.999f : pesos_704ILR[0] * 1.001f;
            RecalcularReparto_704ILR(pesos_704ILR);
        }

        private int SumaAnchos_704ILR(int n_704ILR)
        {
            int suma_704ILR = 0;
            for (int i_704ILR = 0; i_704ILR < n_704ILR && i_704ILR < _grid_704ILR.Columns.Count; i_704ILR++) suma_704ILR += _grid_704ILR.Columns[i_704ILR].Width;
            return suma_704ILR;
        }

        // Ancho en el que la grilla reparte las columnas: el suyo menos la barra vertical si esta a la vista.
        private int AnchoDisponible_704ILR()
        {
            foreach (Control c_704ILR in _grid_704ILR.Controls)
                if (c_704ILR is VScrollBar barra_704ILR && barra_704ILR.Visible) return _grid_704ILR.ClientSize.Width - barra_704ILR.Width;
            return _grid_704ILR.ClientSize.Width;
        }

        // Baja en total 'cuanto' px de los minimos marcados, siempre del mas ancho, sin bajar de MinimoColumna.
        private static void BajarMasAnchos_704ILR(int[] minimos_704ILR, bool[] marcadas_704ILR, int cuanto_704ILR)
        {
            while (cuanto_704ILR > 0)
            {
                int k_704ILR = -1;
                for (int i_704ILR = 0; i_704ILR < minimos_704ILR.Length; i_704ILR++)
                    if (marcadas_704ILR[i_704ILR] && minimos_704ILR[i_704ILR] > MinimoColumna_704ILR && (k_704ILR < 0 || minimos_704ILR[i_704ILR] > minimos_704ILR[k_704ILR]))
                        k_704ILR = i_704ILR;
                if (k_704ILR < 0) return;
                minimos_704ILR[k_704ILR]--;
                cuanto_704ILR--;
            }
        }

        // En modo Fill el reparto parte de los anchos que la grilla ya tenia. Los minimos se
        // aplican en el constructor, con la grilla de pocos pixeles, y sin recalcular al
        // montarla en su tamano final (o tras maximizar y restaurar) las proporciones quedaban
        // deformadas: abierta en portugues, Email salia angosto y con mas correos cortados.
        // Cada cambio de tamano vuelve a fijar los minimos (su tope depende del ancho),
        // restituye los pesos (ver RecalcularReparto) y comprueba que el reparto entre
        // (ver EncajarReparto).
        private void Grid_SizeChanged_704ILR(object sender_704ILR, EventArgs e_704ILR) => AjustarMinimosDeEncabezado_704ILR();

        // Reasigna a cada columna su peso. Si alguno habia cambiado (la grilla los reescribe al
        // subir un minimo por encima del ancho de la columna), la grilla recalcula el reparto desde
        // los pesos; asignar el mismo valor no lo recalcula (ver ForzarReparto).
        private void RecalcularReparto_704ILR(float[] pesos_704ILR)
        {
            for (int i_704ILR = 0; i_704ILR < pesos_704ILR.Length && i_704ILR < _grid_704ILR.Columns.Count; i_704ILR++)
                _grid_704ILR.Columns[i_704ILR].FillWeight = pesos_704ILR[i_704ILR];
        }

        private void ActualizarCount_704ILR()
        {
            if (_grid_704ILR.DataSource is List<BE_Cliente_704ILR> data_704ILR) _lblCount_704ILR.Text = data_704ILR.Count + " " + Tr_704ILR.T_704ILR("CLI_COUNT");
        }

        private void MostrarError_704ILR(Func<string> texto_704ILR)
        {
            _textoError_704ILR = texto_704ILR;
            _lblError_704ILR.Text = texto_704ILR();
            _lblError_704ILR.Visible = true;
        }

        private void MostrarOk_704ILR(Func<string> texto_704ILR)
        {
            _textoOk_704ILR = texto_704ILR;
            _lblOk_704ILR.Text = texto_704ILR();
            _lblOk_704ILR.Visible = true;
        }

        // Devuelve false si la carga fallo (el error queda a la vista).
        private bool SafeLoadData_704ILR()
        {
            try
            {
                _lblError_704ILR.Visible = false;
                var data_704ILR = BLL_Cliente_704ILR.GetAll_704ILR();
                // Recargar la grilla la reposiciona y carga en la ficha la fila que queda
                // marcada. Lo hace la pantalla (al abrir, tras guardar o si el cliente ya no
                // existe), no el usuario: no se pregunta por descartes.
                _seleccionProgramada_704ILR++;
                try { _grid_704ILR.DataSource = data_704ILR; }
                finally { _seleccionProgramada_704ILR--; }
                // Las filas pueden traer la barra vertical, que achica el ancho util: el reparto se
                // vuelve a encajar (sin esto, un alta que supera el alto visible dejaba la barra
                // horizontal hasta redimensionar o cambiar de idioma).
                AjustarMinimosDeEncabezado_704ILR();
                ActualizarCount_704ILR();
                return true;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Clientes", "Cargar clientes");
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
                _lblCount_704ILR.Text = "";
                return false;
            }
        }

        // Deja seleccionada en la grilla la fila del cliente indicado, con su ficha cargada.
        private void SeleccionarCliente_704ILR(int clienteId_704ILR)
        {
            foreach (DataGridViewRow fila_704ILR in _grid_704ILR.Rows)
            {
                if (fila_704ILR.DataBoundItem is BE_Cliente_704ILR c_704ILR && c_704ILR.Id_704ILR == clienteId_704ILR)
                {
                    // Seleccion hecha por la pantalla: carga la ficha sin preguntar.
                    _seleccionProgramada_704ILR++;
                    try
                    {
                        _grid_704ILR.CurrentCell = fila_704ILR.Cells[0];
                        fila_704ILR.Selected = true;
                    }
                    finally { _seleccionProgramada_704ILR--; }
                    // La seleccion ya carga la ficha; si la fila ya era la actual la grilla
                    // no dispara el evento, y la ficha se carga igual.
                    if (_editId_704ILR != clienteId_704ILR) CargarEnForm_704ILR(c_704ILR);
                    break;
                }
            }
        }

        // La ficha sigue a la fila RESALTADA y no a CurrentRow. ClearSelection (Nuevo) deja
        // la grilla sin seleccion pero con la celda actual en la fila anterior, y asignar
        // CurrentCell por codigo resalta la fila nueva antes de mover la celda actual.
        // Leyendo CurrentRow, Nuevo volvia a cargar al cliente anterior (y Guardar lo
        // sobrescribia) y, tras guardar, la ficha mostraba un cliente distinto del resaltado.
        // Si el usuario marca otro cliente (o la ficha esta en un alta), la ficha se reemplaza: con
        // cambios sin guardar se pregunta antes, con el mismo aviso que al cambiar de seccion, y
        // "No" devuelve la grilla a la fila de la ficha sin tocar lo cargado. Marcar el cliente que
        // la ficha ya muestra no la recarga. Antes se descartaba lo tipeado sin aviso, cuando
        // Reservas si preguntaba. Ver _seleccionProgramada y _seleccionSuspendida.
        private void Grid_SelectionChanged_704ILR(object sender_704ILR, EventArgs e_704ILR)
        {
            // Una vista que se esta liberando o que ya salio de la ventana no pregunta ni carga:
            // frmMain ya pregunto antes de reemplazarla.
            if (Parent == null || Disposing || IsDisposed) return;
            if (_seleccionSuspendida_704ILR > 0) return;
            if (_grid_704ILR.SelectedRows.Count == 0) return;
            if (!(_grid_704ILR.SelectedRows[0].DataBoundItem is BE_Cliente_704ILR c_704ILR)) return;
            if (_seleccionProgramada_704ILR > 0) { CargarEnForm_704ILR(c_704ILR); return; }
            if (c_704ILR.Id_704ILR == _editId_704ILR) return;
            if (!ConfirmarDescarte_704ILR()) { VolverAFilaDeLaFicha_704ILR(); return; }
            CargarEnForm_704ILR(c_704ILR);
        }

        // Pregunta antes de reemplazar una ficha con cambios sin guardar (otra fila de la grilla o
        // Nuevo). true = no hay nada que perder o el usuario acepto descartarlo. Es el aviso con
        // que la ventana principal pregunta al cambiar de seccion, con "No" por defecto.
        private bool ConfirmarDescarte_704ILR()
        {
            if (!((IVistaConCambios_704ILR)this).HayCambiosSinGuardar_704ILR) return true;
            return MessageBox.Show(FindForm(),
                T_704ILR("MAIN_CAMBIOS_SIN_GUARDAR", "Hay cambios sin guardar en la sección actual. ¿Descartarlos y continuar?"),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        // Tras responder "No", la grilla vuelve a marcar el cliente de la ficha (en un alta,
        // ninguno). Se difiere hasta que la grilla termine el cambio de fila en curso: mover la
        // fila actual desde su propio evento de seleccion no esta admitido. Hasta entonces los
        // demas cambios de seleccion del mismo gesto no preguntan de nuevo.
        private void VolverAFilaDeLaFicha_704ILR()
        {
            if (IsDisposed || !IsHandleCreated) return;
            _seleccionSuspendida_704ILR++;
            BeginInvoke((Action)(() =>
            {
                try
                {
                    if (IsDisposed) return;
                    _grid_704ILR.ClearSelection();
                    if (_editId_704ILR <= 0) return;
                    foreach (DataGridViewRow fila_704ILR in _grid_704ILR.Rows)
                    {
                        if (fila_704ILR.DataBoundItem is BE_Cliente_704ILR c_704ILR && c_704ILR.Id_704ILR == _editId_704ILR)
                        {
                            _grid_704ILR.CurrentCell = fila_704ILR.Cells[0];
                            fila_704ILR.Selected = true;
                            return;
                        }
                    }
                }
                finally { _seleccionSuspendida_704ILR--; }
            }));
        }

        private void CargarEnForm_704ILR(BE_Cliente_704ILR c_704ILR)
        {
            _editId_704ILR = c_704ILR.Id_704ILR;
            // Los mensajes eran del intento anterior (otro cliente): no se arrastran.
            _lblOk_704ILR.Visible = false;
            _lblError_704ILR.Visible = false;
            _lblFormTitle_704ILR.Text = T_704ILR("CLI_FORM_EDITAR", "Editar cliente") + " #" + _editId_704ILR;
            _txtNombre_704ILR.Text = c_704ILR.Nombre_704ILR;
            _txtApellido_704ILR.Text = c_704ILR.Apellido_704ILR;
            _txtDni_704ILR.Text = c_704ILR.Dni_704ILR;
            _txtEmail_704ILR.Text = c_704ILR.Email_704ILR;
            _txtTelefono_704ILR.Text = c_704ILR.Telefono_704ILR;
            _lineaBase_704ILR = FotoFicha_704ILR();
        }

        private void LimpiarForm_704ILR()
        {
            // ClearSelection va PRIMERO, como en Reservas: dispara Grid_SelectionChanged y,
            // limpiando despues, el estado "nuevo cliente" es el que sobrevive.
            _grid_704ILR.ClearSelection();

            _editId_704ILR = 0;
            _lblOk_704ILR.Visible = false;
            _lblError_704ILR.Visible = false;
            _lblFormTitle_704ILR.Text = T_704ILR("CLI_NUEVO", "Nuevo cliente");
            _txtNombre_704ILR.Text = _txtApellido_704ILR.Text = _txtDni_704ILR.Text = _txtEmail_704ILR.Text = _txtTelefono_704ILR.Text = "";
            _lineaBase_704ILR = FotoFicha_704ILR();
        }

        // IVistaConCambios: hay cambios sin guardar cuando el perfil puede guardar clientes y la
        // ficha difiere de su linea base (el cliente tal como se abrio o se guardo, o el alta
        // limpia). Salir de la seccion con un alta a medio cargar o con un dato editado lo
        // descartaba sin preguntar, cuando Reservas si preguntaba.
        bool IVistaConCambios_704ILR.HayCambiosSinGuardar_704ILR =>
            !IsDisposed && _lineaBase_704ILR != null && Permisos_704ILR.Tiene_704ILR("CLIENTES_GESTION")
            && !MismaFicha_704ILR(FotoFicha_704ILR(), _lineaBase_704ILR);

        // Lo que Guardar enviaria de cada campo: sin los espacios de los bordes y con un dato
        // que no se ve (espacios, invisibles o rellenos) como vacio, igual que lo trata la BLL.
        // Asi recorrer la grilla, cambiar de idioma o agregar un espacio al final no cuentan
        // como cambios.
        private string[] FotoFicha_704ILR() => new[]
        {
            ValorFicha_704ILR(_txtNombre_704ILR), ValorFicha_704ILR(_txtApellido_704ILR), ValorFicha_704ILR(_txtDni_704ILR),
            ValorFicha_704ILR(_txtEmail_704ILR), ValorFicha_704ILR(_txtTelefono_704ILR)
        };

        private static string ValorFicha_704ILR(TextBox txt_704ILR)
        {
            string valor_704ILR = (txt_704ILR.Text ?? string.Empty).Trim();
            return GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(valor_704ILR) ? string.Empty : valor_704ILR;
        }

        private static bool MismaFicha_704ILR(string[] a_704ILR, string[] b_704ILR)
        {
            if (a_704ILR.Length != b_704ILR.Length) return false;
            for (int i_704ILR = 0; i_704ILR < a_704ILR.Length; i_704ILR++)
                if (!string.Equals(a_704ILR[i_704ILR], b_704ILR[i_704ILR], StringComparison.Ordinal)) return false;
            return true;
        }

        // Igual que en Reservas: la escritura queda envuelta para que una falla de base
        // se asiente e informe en vez de terminar la aplicacion.
        private void Guardar_704ILR()
        {
            try
            {
                GuardarCliente_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Clientes",
                    _editId_704ILR == 0 ? "Guardar cliente nuevo" : "Guardar cliente #" + _editId_704ILR);
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        private void GuardarCliente_704ILR()
        {
            // Segunda capa del control de acceso (ver Permisos.cs).
            if (!Permisos_704ILR.Exigir_704ILR("CLIENTES_GESTION", FindForm(),
                    _editId_704ILR == 0 ? "crear un cliente" : "editar el cliente #" + _editId_704ILR))
                return;

            _lblError_704ILR.Visible = false;
            _lblOk_704ILR.Visible = false;
            var c_704ILR = new BE_Cliente_704ILR
            {
                Id_704ILR = _editId_704ILR,
                Nombre_704ILR = _txtNombre_704ILR.Text.Trim(),
                Apellido_704ILR = _txtApellido_704ILR.Text.Trim(),
                Dni_704ILR = _txtDni_704ILR.Text.Trim(),
                Email_704ILR = _txtEmail_704ILR.Text.Trim(),
                Telefono_704ILR = _txtTelefono_704ILR.Text.Trim()
            };
            bool esAlta_704ILR = _editId_704ILR == 0;
            int nuevoId_704ILR = 0;
            ClienteResult_704ILR r_704ILR = esAlta_704ILR ? BLL_Cliente_704ILR.Crear_704ILR(c_704ILR, out nuevoId_704ILR) : BLL_Cliente_704ILR.Actualizar_704ILR(c_704ILR);
            if (r_704ILR == ClienteResult_704ILR.Success_704ILR)
            {
                LimpiarForm_704ILR();
                // CUN003, paso 5: el cliente que se acaba de guardar queda seleccionado en la
                // grilla con su ficha cargada, para poder seguir operando con el sin buscarlo.
                // Vale para el alta y para la edicion. Si la recarga fallo, su error queda a
                // la vista y no se selecciona una fila de la lista anterior.
                if (SafeLoadData_704ILR())
                    SeleccionarCliente_704ILR(esAlta_704ILR ? nuevoId_704ILR : c_704ILR.Id_704ILR);
                MostrarOk_704ILR(() => Tr_704ILR.T_704ILR(esAlta_704ILR ? "MSG_CLI_CREADO" : "MSG_CLI_OK"));
            }
            else if (r_704ILR == ClienteResult_704ILR.NotFound_704ILR)
            {
                // El cliente se borro por fuera de esta pantalla: la grilla se recarga (ya no
                // lo muestra) y la ficha vuelve al alta, para que otro Guardar no apunte a un
                // registro que no existe.
                SafeLoadData_704ILR();
                LimpiarForm_704ILR();
                MostrarError_704ILR(() => MensajeError_704ILR(r_704ILR));
            }
            else
            {
                MostrarError_704ILR(() => MensajeError_704ILR(r_704ILR));
            }
        }

        private static string MensajeError_704ILR(ClienteResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case ClienteResult_704ILR.NombreInvalido_704ILR:   return Tr_704ILR.T_704ILR("MSG_CLI_NOMBRE");
                case ClienteResult_704ILR.DniDuplicado_704ILR:     return Tr_704ILR.T_704ILR("MSG_CLI_DNI_DUP");
                case ClienteResult_704ILR.EmailInvalido_704ILR:    return Tr_704ILR.T_704ILR("MSG_CLI_EMAIL");
                case ClienteResult_704ILR.NotFound_704ILR:         return T_704ILR("MSG_CLI_NOTFOUND", "El cliente ya no existe.");
                case ClienteResult_704ILR.DniInvalido_704ILR:      return T_704ILR("MSG_CLI_DNI_INVALIDO", "El DNI no es válido: use solo números (7 dígitos o más).");
                case ClienteResult_704ILR.LongitudExcedida_704ILR: return T_704ILR("MSG_CLI_LARGO", "Dato muy largo: nombre y apellido 60, email 120, teléfono 30.");
                default:                                           return Tr_704ILR.T_704ILR("MSG_ERROR");
            }
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }
}
