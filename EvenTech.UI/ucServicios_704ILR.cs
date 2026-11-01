using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Catalogo de servicios (Proceso 1): grilla + ficha de alta/edicion.
    // Mismo patron visual que ucClientes/ucReservas. Observa el cambio de idioma.
    // Implementa IVistaConCambios: frmMain pregunta antes de reemplazar la vista si la
    // ficha tiene datos tipeados sin guardar.
    public class ucServicios_704ILR : UserControl, IObservadorIdioma_704ILR, IVistaConCambios_704ILR, IAyudaContextual_704ILR
    {
        // Tema de la ayuda en linea de esta seccion (F1).
        public string TemaAyuda_704ILR => Ayuda_704ILR.TemaServicios_704ILR;

        private DataGridView _grid_704ILR;
        private Label _lblCount_704ILR, _lblError_704ILR, _lblOk_704ILR, _lblFormTitle_704ILR;
        private TextBox _txtNombre_704ILR, _txtDescripcion_704ILR, _txtPrecio_704ILR;
        private CheckBox _chkActivo_704ILR;
        private AppButton_704ILR _btnNuevo_704ILR, _btnGuardar_704ILR;
        private int _editId_704ILR;
        // Como se arma el ultimo mensaje de error/exito, para rehacerlo en el idioma
        // nuevo si se cambia el idioma con el mensaje a la vista.
        private Func<string> _textoError_704ILR, _textoOk_704ILR;
        // Linea base de la ficha (IVistaConCambios): el servicio tal como se cargo o se
        // guardo por ultima vez, o el alta limpia, y el texto con que se escribio su precio.
        private BE_Servicio_704ILR _lineaBase_704ILR;
        private string _precioBaseTexto_704ILR;
        // Cambios de seleccion de la grilla que no hace el usuario (ver Grid_SelectionChanged):
        // suspendida = se ignoran (vaciar la seleccion, volver a la fila de la ficha tras un "No");
        // programada = la pantalla recarga o posiciona la grilla y la ficha se carga sin preguntar.
        private int _seleccionSuspendida_704ILR;
        private int _seleccionProgramada_704ILR;

        public ucServicios_704ILR()
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

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Gesti\u00F3n de Servicios");
            lblTitle_704ILR.Tag = "T:SRV_TITULO"; lblTitle_704ILR.Anchor = AnchorStyles.Left; lblTitle_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);

            _btnNuevo_704ILR = Ui_704ILR.Primary_704ILR("Nuevo", Theme_704ILR.IcoAdd_704ILR);
            // Servicio es masculino: BTN_NUEVO (ver ucClientes).
            _btnNuevo_704ILR.Tag = "T:BTN_NUEVO"; _btnNuevo_704ILR.Size = new Size(120, 36); _btnNuevo_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnNuevo_704ILR.Anchor = AnchorStyles.Left; _btnNuevo_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            // Con lo tipeado sin guardar se pregunta antes de vaciar la ficha, como en Reservas.
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
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNombre", HeaderText = "Nombre", DataPropertyName = "Nombre_704ILR", FillWeight = 55 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDescripcion", HeaderText = "Descripcion", DataPropertyName = "Descripcion_704ILR", FillWeight = 90 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPrecio", HeaderText = "Precio", DataPropertyName = "Precio_704ILR", FillWeight = 45, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid_704ILR.Columns.Add(new DataGridViewCheckBoxColumn { Name = "cActivo", HeaderText = "Activo", DataPropertyName = "Activo_704ILR", FillWeight = 30, ReadOnly = true });
            _grid_704ILR.SelectionChanged += Grid_SelectionChanged_704ILR;
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

            _lblFormTitle_704ILR = Ui_704ILR.Title_704ILR("Nuevo servicio");
            _lblFormTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            var fields_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, AutoScroll = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            fields_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i_704ILR = 0; i_704ILR < 4; i_704ILR++) fields_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // MaxLength = ancho real de las columnas de dbo.Servicios: sin el tope, un
            // texto mas largo llegaba recortado a la base y sin aviso (la validacion de
            // negocio rechaza el nombre excedido, esto lo evita antes de intentarlo).
            _txtNombre_704ILR = Ui_704ILR.Input_704ILR();
            _txtNombre_704ILR.MaxLength = 80;
            _txtDescripcion_704ILR = Ui_704ILR.Input_704ILR();
            _txtDescripcion_704ILR.MaxLength = 250;
            _txtPrecio_704ILR = Ui_704ILR.Input_704ILR();
            var fN_704ILR = Field_704ILR(_txtNombre_704ILR, "COL_NOMBRE", "Nombre");
            var fD_704ILR = Field_704ILR(_txtDescripcion_704ILR, "COL_DESCRIPCION", "Descripción");
            var fP_704ILR = Field_704ILR(_txtPrecio_704ILR, "COL_PRECIO", "Precio");

            _chkActivo_704ILR = new CheckBox
            {
                Text = "Activo", Tag = "T:COL_ACTIVO", Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR,
                FlatStyle = FlatStyle.Standard, BackColor = Color.Transparent, AutoSize = true, Checked = true,
                Margin = new Padding(2, 4, 0, 0)
            };

            int row_704ILR = 0;
            foreach (var f_704ILR in new Control[] { fN_704ILR, fD_704ILR, fP_704ILR, _chkActivo_704ILR })
            {
                f_704ILR.Dock = f_704ILR is CheckBox ? DockStyle.Left : DockStyle.Fill;
                f_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);
                fields_704ILR.Controls.Add(f_704ILR, 0, row_704ILR++);
            }

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
            if (_grid_704ILR.Columns.Count >= 4)
            {
                _grid_704ILR.Columns["cNombre"].HeaderText      = Tr_704ILR.T_704ILR("COL_NOMBRE");
                _grid_704ILR.Columns["cDescripcion"].HeaderText = Tr_704ILR.T_704ILR("COL_DESCRIPCION");
                _grid_704ILR.Columns["cPrecio"].HeaderText      = Tr_704ILR.T_704ILR("COL_PRECIO");
                _grid_704ILR.Columns["cActivo"].HeaderText      = Tr_704ILR.T_704ILR("COL_ACTIVO");
            }
            _lblFormTitle_704ILR.Text = _editId_704ILR == 0 ? Tr_704ILR.T_704ILR("SRV_NUEVO") : Tr_704ILR.T_704ILR("SRV_FORM_EDITAR") + " #" + _editId_704ILR;
            // El mensaje a la vista se vuelve a armar en el idioma nuevo: su texto se fijo
            // ya traducido y, sin esto, quedaba en el idioma anterior junto al resto traducido.
            if (_textoError_704ILR != null && _lblError_704ILR.Visible) _lblError_704ILR.Text = _textoError_704ILR();
            if (_textoOk_704ILR != null && _lblOk_704ILR.Visible) _lblOk_704ILR.Text = _textoOk_704ILR();
            ActualizarCount_704ILR();
        }

        private void ActualizarCount_704ILR()
        {
            if (_grid_704ILR.DataSource is List<BE_Servicio_704ILR> data_704ILR) _lblCount_704ILR.Text = data_704ILR.Count + " " + Tr_704ILR.T_704ILR("SRV_COUNT");
        }

        // true si la grilla se recargo. Si fallo, su aviso queda a la vista (ver Guardar).
        private bool SafeLoadData_704ILR()
        {
            try
            {
                _lblError_704ILR.Visible = false;
                // Recargar la grilla la reposiciona y carga en la ficha la fila que queda
                // seleccionada. Lo hace la pantalla (al abrir y tras guardar), no el usuario:
                // no se pregunta por descartes.
                _seleccionProgramada_704ILR++;
                try { _grid_704ILR.DataSource = BLL_Servicio_704ILR.GetAll_704ILR(); }
                finally { _seleccionProgramada_704ILR--; }
                ActualizarCount_704ILR();
                return true;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Servicios", "Cargar servicios");
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
                _lblCount_704ILR.Text = "";
                return false;
            }
        }

        // Cambio de seleccion de la grilla. Si lo hizo el usuario y la fila es otro servicio (o la
        // ficha esta en un alta), la ficha se reemplaza: con cambios sin guardar se pregunta antes,
        // con el mismo aviso que al cambiar de seccion, y "No" devuelve la grilla a la fila de la
        // ficha sin tocar lo cargado (mismo criterio que Reservas). Elegir el servicio que la ficha
        // ya muestra no lo recarga: antes descartaba lo tipeado. Una vista que se esta liberando
        // (desprendida de la ventana o en Dispose) no pregunta: la ventana principal ya pregunto.
        private void Grid_SelectionChanged_704ILR(object sender_704ILR, EventArgs e_704ILR)
        {
            if (Parent == null || Disposing || IsDisposed) return;
            if (_seleccionSuspendida_704ILR > 0) return;
            if (!(_grid_704ILR.CurrentRow?.DataBoundItem is BE_Servicio_704ILR s_704ILR)) return;
            if (_seleccionProgramada_704ILR > 0) { CargarEnForm_704ILR(s_704ILR); return; }
            if (s_704ILR.Id_704ILR == _editId_704ILR) return;
            if (!ConfirmarDescarte_704ILR()) { VolverAFilaDeLaFicha_704ILR(); return; }
            CargarEnForm_704ILR(s_704ILR);
        }

        // Pregunta antes de reemplazar una ficha con cambios sin guardar (otra fila de la grilla,
        // Nuevo). true = no hay nada que perder o el usuario acepto descartarlo. Es el aviso con
        // que la ventana principal pregunta al cambiar de seccion, con "No" como respuesta por
        // defecto.
        private bool ConfirmarDescarte_704ILR()
        {
            if (!((IVistaConCambios_704ILR)this).HayCambiosSinGuardar_704ILR) return true;
            return MessageBox.Show(FindForm(),
                T_704ILR("MAIN_CAMBIOS_SIN_GUARDAR", "Hay cambios sin guardar en la secci\u00F3n actual. \u00BFDescartarlos y continuar?"),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        // Tras responder "No", la grilla vuelve a marcar el servicio que muestra la ficha (en un
        // alta, ninguno). Se difiere hasta que la grilla termine el cambio de fila en curso: mover
        // la fila actual desde su propio evento de seleccion no esta admitido. Hasta entonces los
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
                    foreach (DataGridViewRow row_704ILR in _grid_704ILR.Rows)
                    {
                        if (row_704ILR.DataBoundItem is BE_Servicio_704ILR s_704ILR && s_704ILR.Id_704ILR == _editId_704ILR)
                        {
                            _grid_704ILR.CurrentCell = row_704ILR.Cells[0];
                            row_704ILR.Selected = true;
                            return;
                        }
                    }
                }
                finally { _seleccionSuspendida_704ILR--; }
            }));
        }

        // Deja seleccionado en la grilla el servicio indicado y cargado en la ficha (mismo
        // criterio que ucReservas). Si la fila ya era la actual la grilla no dispara el
        // evento de seleccion, y CargarEnForm se llama aca. La seleccion la mueve la
        // pantalla (tras guardar): la ficha se carga sin preguntar.
        private void SeleccionarServicio_704ILR(int id_704ILR)
        {
            if (id_704ILR <= 0) return;
            foreach (DataGridViewRow row_704ILR in _grid_704ILR.Rows)
            {
                if (row_704ILR.DataBoundItem is BE_Servicio_704ILR s_704ILR && s_704ILR.Id_704ILR == id_704ILR)
                {
                    bool yaActual_704ILR = _grid_704ILR.CurrentRow == row_704ILR;
                    _seleccionProgramada_704ILR++;
                    try
                    {
                        _grid_704ILR.CurrentCell = row_704ILR.Cells[0];
                        row_704ILR.Selected = true;
                    }
                    finally { _seleccionProgramada_704ILR--; }
                    if (yaActual_704ILR || _editId_704ILR != id_704ILR) CargarEnForm_704ILR(s_704ILR);
                    return;
                }
            }
        }

        private void CargarEnForm_704ILR(BE_Servicio_704ILR s_704ILR)
        {
            _editId_704ILR = s_704ILR.Id_704ILR;
            // Los mensajes eran del intento anterior (otro servicio o la misma ficha antes
            // de recargarla): no se arrastran, igual que en Clientes y Reservas. Sin anular
            // el texto, el cambio de idioma volvia a escribir el aviso viejo.
            _lblOk_704ILR.Visible = false;
            _lblError_704ILR.Visible = false;
            _textoError_704ILR = null;
            _lblFormTitle_704ILR.Text = Tr_704ILR.T_704ILR("SRV_FORM_EDITAR") + " #" + _editId_704ILR;
            _txtNombre_704ILR.Text = s_704ILR.Nombre_704ILR;
            _txtDescripcion_704ILR.Text = s_704ILR.Descripcion_704ILR;
            _txtPrecio_704ILR.Text = s_704ILR.Precio_704ILR.ToString("0.##");
            _chkActivo_704ILR.Checked = s_704ILR.Activo_704ILR;
            FijarLineaBase_704ILR(s_704ILR);
        }

        private void LimpiarForm_704ILR()
        {
            // Vaciar la seleccion dispara Grid_SelectionChanged: se hace PRIMERO y con la
            // seleccion suspendida (igual que en ucReservas), asi no vuelve a cargar en la
            // ficha la fila actual (el "alta" la modificaba) ni pregunta por descartes.
            _seleccionSuspendida_704ILR++;
            try { _grid_704ILR.ClearSelection(); }
            finally { _seleccionSuspendida_704ILR--; }

            _editId_704ILR = 0;
            _lblOk_704ILR.Visible = false;
            _lblError_704ILR.Visible = false;
            _textoError_704ILR = null;
            _lblFormTitle_704ILR.Text = T_704ILR("SRV_NUEVO", "Nuevo servicio");
            _txtNombre_704ILR.Text = _txtDescripcion_704ILR.Text = "";
            _txtPrecio_704ILR.Text = "0";
            _chkActivo_704ILR.Checked = true;
            FijarLineaBase_704ILR(new BE_Servicio_704ILR { Nombre_704ILR = "", Precio_704ILR = 0m, Activo_704ILR = true });
        }

        // IVistaConCambios: la ficha tiene cambios sin guardar cuando lo que Guardar
        // enviaria difiere de su linea base (el servicio como se cargo o se guardo, o el
        // alta limpia). Se compara como compara la capa de negocio: nombre tal como se ve,
        // descripcion sin espacios de mas, descripcion vacia = sin descripcion, precio
        // redondeado a centavos. Asi recorrer la grilla, cambiar de idioma o volver a tipear el mismo
        // precio con otro formato ("120.000,00") no es un cambio. Sin el permiso de gestion
        // no hay nada que guardar ni que perder.
        bool IVistaConCambios_704ILR.HayCambiosSinGuardar_704ILR =>
            !IsDisposed && _lineaBase_704ILR != null && Permisos_704ILR.Tiene_704ILR("SERVICIOS_GESTION") && !FichaIgualALineaBase_704ILR();

        private void FijarLineaBase_704ILR(BE_Servicio_704ILR s_704ILR)
        {
            _lineaBase_704ILR = new BE_Servicio_704ILR
            {
                Id_704ILR = s_704ILR.Id_704ILR,
                Nombre_704ILR = s_704ILR.Nombre_704ILR,
                Descripcion_704ILR = s_704ILR.Descripcion_704ILR,
                Precio_704ILR = s_704ILR.Precio_704ILR,
                Activo_704ILR = s_704ILR.Activo_704ILR
            };
            _precioBaseTexto_704ILR = _txtPrecio_704ILR.Text;
        }

        private bool FichaIgualALineaBase_704ILR()
        {
            var b_704ILR = _lineaBase_704ILR;
            // El nombre, como lo guarda la capa de negocio: tal como se ve (sin invisibles, con
            // espacios y rellenos como un espacio comun). Un invisible pegado no es un cambio.
            if (GestorDeIdioma_704ILR.TextoVisible_704ILR(_txtNombre_704ILR.Text) != GestorDeIdioma_704ILR.TextoVisible_704ILR(b_704ILR.Nombre_704ILR)) return false;
            if (SinEspaciosDeMas_704ILR(_txtDescripcion_704ILR.Text) != SinEspaciosDeMas_704ILR(b_704ILR.Descripcion_704ILR)) return false;
            if (_chkActivo_704ILR.Checked != b_704ILR.Activo_704ILR) return false;
            // Precio: el mismo texto con que se cargo; la caja vacia en un alta (no hay dato);
            // o el mismo importe a centavos. Un texto que no se puede leer es un cambio.
            string precio_704ILR = _txtPrecio_704ILR.Text;
            if (precio_704ILR == _precioBaseTexto_704ILR) return true;
            decimal base_704ILR = decimal.Round(b_704ILR.Precio_704ILR, 2, MidpointRounding.AwayFromZero);
            if (string.IsNullOrWhiteSpace(precio_704ILR)) return _editId_704ILR == 0 && base_704ILR == 0m;
            // Un importe negativo es un cambio aunque redondee a 0,00: Guardar lo rechaza.
            return ParsearPrecio_704ILR(precio_704ILR, out decimal leido_704ILR)
                && leido_704ILR >= 0m
                && decimal.Round(leido_704ILR, 2, MidpointRounding.AwayFromZero) == base_704ILR;
        }

        private static string SinEspaciosDeMas_704ILR(string texto_704ILR) =>
            string.IsNullOrWhiteSpace(texto_704ILR) ? null : texto_704ILR.Trim();

        // Igual que en Clientes y Reservas: la escritura queda envuelta para que una falla
        // de base (servidor caido, conexion perdida) se asiente e informe en la ficha en
        // vez de terminar en el dialogo de excepcion no controlada.
        private void Guardar_704ILR()
        {
            try
            {
                GuardarServicio_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Servicios",
                    _editId_704ILR == 0 ? "Guardar servicio nuevo" : "Guardar servicio #" + _editId_704ILR);
                _lblOk_704ILR.Visible = false;
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        private void GuardarServicio_704ILR()
        {
            // Segunda capa del control de acceso (ver Permisos.cs).
            if (!Permisos_704ILR.Exigir_704ILR("SERVICIOS_GESTION", FindForm(),
                    _editId_704ILR == 0 ? "crear un servicio" : "editar el servicio #" + _editId_704ILR))
                return;

            _lblError_704ILR.Visible = false;
            _lblOk_704ILR.Visible = false;

            if (!ParsearPrecio_704ILR(_txtPrecio_704ILR.Text, out decimal precio_704ILR))
            {
                MostrarError_704ILR(() => Tr_704ILR.T_704ILR("MSG_MONTO_INVALIDO"));
                return;
            }

            var s_704ILR = new BE_Servicio_704ILR
            {
                Id_704ILR = _editId_704ILR,
                Nombre_704ILR = _txtNombre_704ILR.Text.Trim(),
                Descripcion_704ILR = _txtDescripcion_704ILR.Text.Trim(),
                Precio_704ILR = precio_704ILR,
                Activo_704ILR = _chkActivo_704ILR.Checked
            };
            bool esAlta_704ILR = _editId_704ILR == 0;
            int nuevoId_704ILR = 0;
            ServicioResult_704ILR r_704ILR = esAlta_704ILR ? BLL_Servicio_704ILR.Crear_704ILR(s_704ILR, out nuevoId_704ILR) : BLL_Servicio_704ILR.Actualizar_704ILR(s_704ILR);
            if (r_704ILR == ServicioResult_704ILR.Success_704ILR)
            {
                LimpiarForm_704ILR();
                // El servicio guardado queda seleccionado y en la ficha, en el alta y en la
                // edicion: al reasignar los datos la grilla vuelve a la primera fila y, sin
                // esto, la ficha mostraba OTRO servicio bajo el cartel "Servicio guardado." y
                // el siguiente Guardar lo modificaba. Va antes del cartel: CargarEnForm lo oculta.
                // Si la recarga fallo, su aviso queda a la vista y no se carga una fila de la
                // lista anterior (CargarEnForm lo ocultaria), como en Clientes.
                if (SafeLoadData_704ILR())
                    SeleccionarServicio_704ILR(esAlta_704ILR ? nuevoId_704ILR : s_704ILR.Id_704ILR);
                MostrarOk_704ILR(() => Tr_704ILR.T_704ILR("MSG_SRV_OK"));
            }
            else
            {
                MostrarError_704ILR(() => MensajeError_704ILR(r_704ILR));
            }
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

        // Lee el precio tipeado con la convencion de la cultura de la maquina, la misma
        // con la que la grilla lo muestra (es-AR: "1.500,50"): el separador de miles se
        // acepta solo en grupos exactos de tres digitos, asi "1.500" es mil quinientos
        // (antes se releia con el punto como decimal y el catalogo guardaba 1,50 con el
        // cartel "Servicio guardado."). En una cultura de coma decimal el punto se admite
        // ademas como decimal solo con uno o dos digitos detras ("1500.50", "1.5"): es el
        // mismo criterio que el importe de los cobros. Con tres o mas ("1.500", "150.000")
        // el punto puede ser separador de miles, y en las culturas que agrupan con espacio
        // duro (es-CR, fr-FR, pt-PT) se leia como decimal y se guardaba 1,50 o 150,00.
        // Un texto ambiguo o que no encaja en ninguna de las dos formas ("1.5.0",
        // "1,500.50") se rechaza con mensaje: nunca se guarda otro numero.
        // Tambien se acepta el importe escrito EXACTAMENTE como lo escriben la columna
        // Precio y el aviso del tope (formato N de la cultura, que respeta
        // NumberGroupSizes): en hi-IN o en-IN los miles se agrupan de a tres y despues de a
        // dos ("1,20,000.00") y la ficha rechazaba el numero que ella misma mostraba. Se
        // admite solo si el texto es, caracter por caracter, el formato propio de ese
        // importe, y despues de las dos formas anteriores: lo que ya se aceptaba se sigue
        // leyendo igual y no aparece una segunda lectura.
        // En las culturas que agrupan con un espacio duro (fr-FR, es-CR, sv-SE) la barra
        // espaciadora escribe un espacio comun y no el de la cultura: se toma como ese
        // separador, y los grupos se siguen exigiendo igual.
        // Lo mismo en las que agrupan con el apostrofo tipografico U+2019 (de-CH, it-CH, en-CH,
        // de-LI, rm-CH, gsw-CH): la tecla del apostrofo escribe U+0027 y el precio tipeado
        // ("1'234'567.89") se rechazaba como numero invalido aunque la grilla lo mostrara igual.
        // En esas culturas el apostrofo no tiene otro uso numerico, asi que no aparece una
        // segunda lectura.
        private static bool ParsearPrecio_704ILR(string texto_704ILR, out decimal precio_704ILR)
        {
            precio_704ILR = 0m;
            CultureInfo cultura_704ILR = CultureInfo.CurrentCulture;
            NumberFormatInfo nf_704ILR = cultura_704ILR.NumberFormat;
            string t_704ILR = (texto_704ILR ?? "").Trim();
            string separador_704ILR = nf_704ILR.NumberGroupSeparator;
            if (separador_704ILR.Length == 1 && char.IsWhiteSpace(separador_704ILR[0]))
                t_704ILR = t_704ILR.Replace(' ', separador_704ILR[0]);
            else if (separador_704ILR == "\u2019")
                t_704ILR = t_704ILR.Replace('\'', '\u2019');
            string decimal_704ILR = Regex.Escape(nf_704ILR.NumberDecimalSeparator);
            string miles_704ILR = Regex.Escape(separador_704ILR);
            if (Regex.IsMatch(t_704ILR, "^[+-]?([0-9]*|[1-9][0-9]{0,2}(" + miles_704ILR + "[0-9]{3})+)(" + decimal_704ILR + "[0-9]+)?$"))
                return decimal.TryParse(t_704ILR, NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                    cultura_704ILR, out precio_704ILR);
            if (nf_704ILR.NumberDecimalSeparator != "." && Regex.IsMatch(t_704ILR, "^[+-]?[0-9]*\\.[0-9]{1,2}$"))
                return decimal.TryParse(t_704ILR, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out precio_704ILR);
            return EsFormatoDeLaCultura_704ILR(t_704ILR, cultura_704ILR, out precio_704ILR);
        }

        // true si el texto es exactamente el formato N de la cultura (con tantos decimales
        // como se escribieron) del importe que significa en esa cultura.
        private static bool EsFormatoDeLaCultura_704ILR(string texto_704ILR, CultureInfo cultura_704ILR, out decimal importe_704ILR)
        {
            importe_704ILR = 0m;
            NumberFormatInfo nf_704ILR = cultura_704ILR.NumberFormat;
            if (texto_704ILR.Length == 0 || nf_704ILR.NumberDecimalSeparator.Length == 0
                || !decimal.TryParse(texto_704ILR, NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                    cultura_704ILR, out decimal leido_704ILR))
                return false;
            int coma_704ILR = texto_704ILR.IndexOf(nf_704ILR.NumberDecimalSeparator, StringComparison.Ordinal);
            int decimales_704ILR = coma_704ILR < 0 ? 0 : texto_704ILR.Length - coma_704ILR - nf_704ILR.NumberDecimalSeparator.Length;
            if (decimales_704ILR > 28 || leido_704ILR.ToString("N" + decimales_704ILR, cultura_704ILR) != texto_704ILR)
                return false;
            importe_704ILR = leido_704ILR;
            return true;
        }

        private static string MensajeError_704ILR(ServicioResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case ServicioResult_704ILR.NombreInvalido_704ILR:  return T_704ILR("MSG_SRV_NOMBRE", "Ingrese el nombre del servicio.");
                case ServicioResult_704ILR.NombreDuplicado_704ILR: return T_704ILR("MSG_SRV_DUP", "Ya existe un servicio con ese nombre.");
                case ServicioResult_704ILR.PrecioInvalido_704ILR:  return T_704ILR("MSG_SRV_PRECIO", "El precio no puede ser negativo.");
                // El tope se escribe con la cultura de la maquina, igual que la columna Precio
                // de la grilla y que la ficha lo lee: con un numero fijo por idioma, el
                // mensaje mostraba un formato que la propia ficha rechazaba como invalido.
                case ServicioResult_704ILR.PrecioExcedido_704ILR:  return Tr_704ILR.F_704ILR("MSG_SRV_PRECIO_MAX", "El precio no puede superar {0}.",
                                                                        BLL_Servicio_704ILR.MaxPrecio_704ILR.ToString("N2", CultureInfo.CurrentCulture));
                // Un servicio borrado por fuera de la pantalla: aviso propio (antes se mostraba el
                // de Reservas, "La reserva ya no existe."), como MSG_CLI_NOTFOUND en Clientes.
                case ServicioResult_704ILR.NotFound_704ILR:        return T_704ILR("MSG_SRV_NOTFOUND", "El servicio ya no existe.");
                default:                             return T_704ILR("MSG_ERROR", "Error");
            }
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }
}
