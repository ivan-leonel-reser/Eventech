using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Personal de la organizacion (Proceso 2): grilla + ficha de alta/edicion.
    // Mismo patron visual que ucServicios/ucClientes. Observa el cambio de idioma.
    // Implementa IVistaConCambios: frmMain pregunta antes de reemplazar la vista si la
    // ficha tiene datos tipeados sin guardar.
    public class ucEmpleados_704ILR : UserControl, IObservadorIdioma_704ILR, IVistaConCambios_704ILR
    {
        private DataGridView _grid_704ILR;
        private Label _lblCount_704ILR, _lblError_704ILR, _lblOk_704ILR, _lblFormTitle_704ILR;
        private TextBox _txtNombre_704ILR, _txtApellido_704ILR, _txtDni_704ILR;
        private ComboBox _cboEspecialidad_704ILR, _cboCuenta_704ILR;
        private CheckBox _chkActivo_704ILR;
        private AppButton_704ILR _btnNuevo_704ILR, _btnGuardar_704ILR;
        private int _editId_704ILR;
        // Como se arma el ultimo mensaje de error/exito, para rehacerlo en el idioma
        // nuevo si se cambia el idioma con el mensaje a la vista.
        private Func<string> _textoError_704ILR, _textoOk_704ILR;
        // Linea base de la ficha (IVistaConCambios): el empleado tal como se cargo o se
        // guardo por ultima vez, o el alta limpia.
        private BE_Empleado_704ILR _lineaBase_704ILR;
        // Cuentas de usuario y a que empleado representa cada una (para ofrecer en la
        // ficha solo las libres y la del propio empleado).
        private List<BE_User_704ILR> _usuarios_704ILR = new List<BE_User_704ILR>();
        // Cambios de seleccion de la grilla que no hace el usuario (ver ucServicios).
        private int _seleccionSuspendida_704ILR;
        private int _seleccionProgramada_704ILR;

        public ucEmpleados_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) =>
            {
                Func<string> errorCatalogos_704ILR = CargarCatalogos_704ILR();
                LimpiarForm_704ILR();
                SafeLoadData_704ILR();
                // El aviso de los catalogos va al final: limpiar la ficha y cargar la grilla
                // ocultan el rotulo de error.
                if (errorCatalogos_704ILR != null) MostrarError_704ILR(errorCatalogos_704ILR);
                GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            };
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

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Gestión de Empleados");
            lblTitle_704ILR.Tag = "T:EMP_TITULO"; lblTitle_704ILR.Anchor = AnchorStyles.Left; lblTitle_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);

            _btnNuevo_704ILR = Ui_704ILR.Primary_704ILR("Nuevo", Theme_704ILR.IcoAdd_704ILR);
            _btnNuevo_704ILR.Tag = "T:BTN_NUEVO"; _btnNuevo_704ILR.Size = new Size(120, 36); _btnNuevo_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnNuevo_704ILR.Anchor = AnchorStyles.Left; _btnNuevo_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            // Con lo tipeado sin guardar se pregunta antes de vaciar la ficha.
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
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEmpleado", HeaderText = "Empleado", DataPropertyName = "NombreCompleto_704ILR", FillWeight = 80 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDni", HeaderText = "DNI", DataPropertyName = "Dni_704ILR", FillWeight = 40 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEspecialidad", HeaderText = "Especialidad", DataPropertyName = "EspecialidadNombre_704ILR", FillWeight = 60 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCuenta", HeaderText = "Cuenta", DataPropertyName = "Username_704ILR", FillWeight = 45 });
            _grid_704ILR.Columns.Add(new DataGridViewCheckBoxColumn { Name = "cActivo", HeaderText = "Activo", DataPropertyName = "Activo_704ILR", FillWeight = 28, ReadOnly = true });
            _grid_704ILR.SelectionChanged += Grid_SelectionChanged_704ILR;
            // La especialidad se muestra traducida (el catalogo guarda el nombre en espanol).
            _grid_704ILR.CellFormatting += (s_704ILR, e_704ILR) =>
            {
                if (e_704ILR.RowIndex < 0 || e_704ILR.ColumnIndex < 0) return;
                if (_grid_704ILR.Columns[e_704ILR.ColumnIndex].Name != "cEspecialidad" || !(e_704ILR.Value is string nombre_704ILR)) return;
                e_704ILR.Value = Coord_704ILR.Especialidad_704ILR(nombre_704ILR);
                e_704ILR.FormattingApplied = true;
            };
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

            _lblFormTitle_704ILR = Ui_704ILR.Title_704ILR("Nuevo empleado");
            _lblFormTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            var fields_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, AutoScroll = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            fields_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i_704ILR = 0; i_704ILR < 6; i_704ILR++) fields_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // MaxLength = ancho real de las columnas de dbo.Empleados.
            _txtNombre_704ILR = Ui_704ILR.Input_704ILR(); _txtNombre_704ILR.MaxLength = 60;
            _txtApellido_704ILR = Ui_704ILR.Input_704ILR(); _txtApellido_704ILR.MaxLength = 60;
            _txtDni_704ILR = Ui_704ILR.Input_704ILR(); _txtDni_704ILR.MaxLength = 20;
            _cboEspecialidad_704ILR = Ui_704ILR.Combo_704ILR();
            // El item es la entidad (Guardar lee su Id); el texto sale traducido.
            _cboEspecialidad_704ILR.FormattingEnabled = true;
            _cboEspecialidad_704ILR.Format += (s_704ILR, e_704ILR) =>
            {
                if (e_704ILR.ListItem is BE_Especialidad_704ILR esp_704ILR) e_704ILR.Value = Coord_704ILR.Especialidad_704ILR(esp_704ILR.Nombre_704ILR);
            };
            _cboCuenta_704ILR = Ui_704ILR.Combo_704ILR();

            var fN_704ILR = Field_704ILR(_txtNombre_704ILR, "COL_NOMBRE", "Nombre");
            var fA_704ILR = Field_704ILR(_txtApellido_704ILR, "COL_APELLIDO", "Apellido");
            var fD_704ILR = Field_704ILR(_txtDni_704ILR, "COL_DNI", "DNI");
            var fE_704ILR = Field_704ILR(_cboEspecialidad_704ILR, "EMP_ESPECIALIDAD", "Especialidad");
            var fC_704ILR = Field_704ILR(_cboCuenta_704ILR, "EMP_CUENTA", "Cuenta de usuario");

            _chkActivo_704ILR = new CheckBox
            {
                Text = "Activo", Tag = "T:COL_ACTIVO", Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR,
                FlatStyle = FlatStyle.Standard, BackColor = Color.Transparent, AutoSize = true, Checked = true,
                Margin = new Padding(2, 4, 0, 0)
            };

            int row_704ILR = 0;
            foreach (var f_704ILR in new Control[] { fN_704ILR, fA_704ILR, fD_704ILR, fE_704ILR, fC_704ILR, _chkActivo_704ILR })
            {
                f_704ILR.Dock = f_704ILR is CheckBox ? DockStyle.Left : DockStyle.Fill;
                f_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR);
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
            if (_grid_704ILR.Columns.Count >= 5)
            {
                _grid_704ILR.Columns["cEmpleado"].HeaderText     = T_704ILR("EMP_COL_EMPLEADO", "Empleado");
                _grid_704ILR.Columns["cDni"].HeaderText          = T_704ILR("COL_DNI", "DNI");
                _grid_704ILR.Columns["cEspecialidad"].HeaderText = T_704ILR("EMP_ESPECIALIDAD", "Especialidad");
                _grid_704ILR.Columns["cCuenta"].HeaderText       = T_704ILR("EMP_COL_CUENTA", "Cuenta");
                _grid_704ILR.Columns["cActivo"].HeaderText       = T_704ILR("COL_ACTIVO", "Activo");
            }
            _lblFormTitle_704ILR.Text = _editId_704ILR == 0 ? T_704ILR("EMP_NUEVO", "Nuevo empleado") : T_704ILR("EMP_FORM_EDITAR", "Editar empleado") + " #" + _editId_704ILR;
            if (_textoError_704ILR != null && _lblError_704ILR.Visible) _lblError_704ILR.Text = _textoError_704ILR();
            if (_textoOk_704ILR != null && _lblOk_704ILR.Visible) _lblOk_704ILR.Text = _textoOk_704ILR();
            // Los combos y la columna de especialidad dibujan el texto traducido al repintarse.
            Coord_704ILR.Retraducir_704ILR(_cboEspecialidad_704ILR);
            Coord_704ILR.Retraducir_704ILR(_cboCuenta_704ILR);
            _grid_704ILR.Invalidate();
            ActualizarCount_704ILR();
        }

        private void ActualizarCount_704ILR()
        {
            if (_grid_704ILR.DataSource is List<BE_Empleado_704ILR> data_704ILR) _lblCount_704ILR.Text = data_704ILR.Count + " " + T_704ILR("EMP_COUNT", "empleado(s)");
        }

        // Especialidades y cuentas de usuario para los combos de la ficha. Una falla se
        // asienta y deja los combos como estaban (Guardar rechaza una ficha sin
        // especialidad); devuelve el aviso para que quien llama lo muestre, o null.
        private Func<string> CargarCatalogos_704ILR()
        {
            try
            {
                _cboEspecialidad_704ILR.Items.Clear();
                foreach (var esp_704ILR in BLL_Empleado_704ILR.GetEspecialidades_704ILR()) _cboEspecialidad_704ILR.Items.Add(esp_704ILR);
                _usuarios_704ILR = BLL_User_704ILR.GetAll_704ILR();
                return null;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Empleados", "Cargar especialidades y cuentas");
                return () => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR);
            }
        }

        // Arma el combo de cuentas para la ficha: "(sin cuenta)", la cuenta del propio
        // empleado y las que todavia no representan a nadie. Las ya vinculadas a otro
        // empleado no se ofrecen (una cuenta responde por una sola persona). La cuenta
        // del propio empleado se ofrece SIEMPRE, aunque no este en la lista leida al
        // abrir la pantalla (una cuenta creada despues, o la lista que no se pudo leer):
        // si faltara, la ficha mostraria "(sin cuenta)" y un guardado por cualquier otro
        // dato desvincularia al empleado sin que nadie lo pidiera.
        private void ArmarCuentas_704ILR(int? seleccionada_704ILR, string usuario_704ILR = null)
        {
            var ocupadas_704ILR = new HashSet<int>();
            if (_grid_704ILR.DataSource is List<BE_Empleado_704ILR> data_704ILR)
                foreach (var e_704ILR in data_704ILR)
                    if (e_704ILR.UserId_704ILR.HasValue && e_704ILR.Id_704ILR != _editId_704ILR) ocupadas_704ILR.Add(e_704ILR.UserId_704ILR.Value);

            _cboCuenta_704ILR.Items.Clear();
            _cboCuenta_704ILR.Items.Add(new CuentaItem_704ILR(null, null));
            foreach (var u_704ILR in _usuarios_704ILR)
                if (!ocupadas_704ILR.Contains(u_704ILR.Id_704ILR)) _cboCuenta_704ILR.Items.Add(new CuentaItem_704ILR(u_704ILR.Id_704ILR, u_704ILR.Username_704ILR));

            _cboCuenta_704ILR.SelectedIndex = 0;
            if (!seleccionada_704ILR.HasValue) return;
            for (int i_704ILR = 0; i_704ILR < _cboCuenta_704ILR.Items.Count; i_704ILR++)
                if (((CuentaItem_704ILR)_cboCuenta_704ILR.Items[i_704ILR]).UserId_704ILR == seleccionada_704ILR) { _cboCuenta_704ILR.SelectedIndex = i_704ILR; return; }
            _cboCuenta_704ILR.SelectedIndex = _cboCuenta_704ILR.Items.Add(
                new CuentaItem_704ILR(seleccionada_704ILR, string.IsNullOrEmpty(usuario_704ILR) ? "#" + seleccionada_704ILR.Value : usuario_704ILR));
        }

        // true si la grilla se recargo. Si fallo, su aviso queda a la vista (ver Guardar).
        private bool SafeLoadData_704ILR()
        {
            try
            {
                _lblError_704ILR.Visible = false;
                _seleccionProgramada_704ILR++;
                try { _grid_704ILR.DataSource = BLL_Empleado_704ILR.GetAll_704ILR(); }
                finally { _seleccionProgramada_704ILR--; }
                ActualizarCount_704ILR();
                return true;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Empleados", "Cargar empleados");
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
                _lblCount_704ILR.Text = "";
                return false;
            }
        }

        // Cambio de seleccion de la grilla (mismo criterio que ucServicios): si lo hizo el
        // usuario y la fila es otro empleado, la ficha se reemplaza, preguntando antes si
        // tiene cambios sin guardar.
        private void Grid_SelectionChanged_704ILR(object sender_704ILR, EventArgs e_704ILR)
        {
            if (Parent == null || Disposing || IsDisposed) return;
            if (_seleccionSuspendida_704ILR > 0) return;
            if (!(_grid_704ILR.CurrentRow?.DataBoundItem is BE_Empleado_704ILR emp_704ILR)) return;
            if (_seleccionProgramada_704ILR > 0) { CargarEnForm_704ILR(emp_704ILR); return; }
            if (emp_704ILR.Id_704ILR == _editId_704ILR) return;
            if (!ConfirmarDescarte_704ILR()) { VolverAFilaDeLaFicha_704ILR(); return; }
            CargarEnForm_704ILR(emp_704ILR);
        }

        private bool ConfirmarDescarte_704ILR()
        {
            if (!((IVistaConCambios_704ILR)this).HayCambiosSinGuardar_704ILR) return true;
            return MessageBox.Show(FindForm(),
                T_704ILR("MAIN_CAMBIOS_SIN_GUARDAR", "Hay cambios sin guardar en la sección actual. ¿Descartarlos y continuar?"),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        // Tras responder "No", la grilla vuelve a marcar el empleado que muestra la ficha
        // (en un alta, ninguno). Se difiere hasta que la grilla termine el cambio de fila.
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
                        if (row_704ILR.DataBoundItem is BE_Empleado_704ILR emp_704ILR && emp_704ILR.Id_704ILR == _editId_704ILR)
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

        // Deja seleccionado en la grilla el empleado indicado y cargado en la ficha.
        private void SeleccionarEmpleado_704ILR(int id_704ILR)
        {
            if (id_704ILR <= 0) return;
            foreach (DataGridViewRow row_704ILR in _grid_704ILR.Rows)
            {
                if (row_704ILR.DataBoundItem is BE_Empleado_704ILR emp_704ILR && emp_704ILR.Id_704ILR == id_704ILR)
                {
                    bool yaActual_704ILR = _grid_704ILR.CurrentRow == row_704ILR;
                    _seleccionProgramada_704ILR++;
                    try
                    {
                        _grid_704ILR.CurrentCell = row_704ILR.Cells[0];
                        row_704ILR.Selected = true;
                    }
                    finally { _seleccionProgramada_704ILR--; }
                    if (yaActual_704ILR || _editId_704ILR != id_704ILR) CargarEnForm_704ILR(emp_704ILR);
                    return;
                }
            }
        }

        private void CargarEnForm_704ILR(BE_Empleado_704ILR emp_704ILR)
        {
            _editId_704ILR = emp_704ILR.Id_704ILR;
            _lblOk_704ILR.Visible = false;
            _lblError_704ILR.Visible = false;
            _textoError_704ILR = null;
            _lblFormTitle_704ILR.Text = T_704ILR("EMP_FORM_EDITAR", "Editar empleado") + " #" + _editId_704ILR;
            _txtNombre_704ILR.Text = emp_704ILR.Nombre_704ILR;
            _txtApellido_704ILR.Text = emp_704ILR.Apellido_704ILR;
            _txtDni_704ILR.Text = emp_704ILR.Dni_704ILR;
            SeleccionarEspecialidad_704ILR(emp_704ILR.EspecialidadId_704ILR);
            ArmarCuentas_704ILR(emp_704ILR.UserId_704ILR, emp_704ILR.Username_704ILR);
            _chkActivo_704ILR.Checked = emp_704ILR.Activo_704ILR;
            FijarLineaBase_704ILR(emp_704ILR);
        }

        private void LimpiarForm_704ILR()
        {
            // Vaciar la seleccion dispara Grid_SelectionChanged: se hace PRIMERO y con la
            // seleccion suspendida, asi no vuelve a cargar en la ficha la fila actual.
            _seleccionSuspendida_704ILR++;
            try { _grid_704ILR.ClearSelection(); }
            finally { _seleccionSuspendida_704ILR--; }

            _editId_704ILR = 0;
            _lblOk_704ILR.Visible = false;
            _lblError_704ILR.Visible = false;
            _textoError_704ILR = null;
            _lblFormTitle_704ILR.Text = T_704ILR("EMP_NUEVO", "Nuevo empleado");
            _txtNombre_704ILR.Text = _txtApellido_704ILR.Text = _txtDni_704ILR.Text = "";
            // El alta abre sin especialidad elegida: con la primera de la lista puesta de
            // antemano, un empleado quedaba guardado con una especialidad que nadie eligio.
            _cboEspecialidad_704ILR.SelectedIndex = -1;
            ArmarCuentas_704ILR(null);
            _chkActivo_704ILR.Checked = true;
            FijarLineaBase_704ILR(new BE_Empleado_704ILR
            {
                Nombre_704ILR = "", Apellido_704ILR = "", Dni_704ILR = "", Activo_704ILR = true,
                EspecialidadId_704ILR = EspecialidadElegida_704ILR()
            });
        }

        private void SeleccionarEspecialidad_704ILR(int id_704ILR)
        {
            for (int i_704ILR = 0; i_704ILR < _cboEspecialidad_704ILR.Items.Count; i_704ILR++)
                if (((BE_Especialidad_704ILR)_cboEspecialidad_704ILR.Items[i_704ILR]).Id_704ILR == id_704ILR) { _cboEspecialidad_704ILR.SelectedIndex = i_704ILR; return; }
            _cboEspecialidad_704ILR.SelectedIndex = -1;
        }

        private int EspecialidadElegida_704ILR() =>
            _cboEspecialidad_704ILR.SelectedItem is BE_Especialidad_704ILR esp_704ILR ? esp_704ILR.Id_704ILR : 0;

        private int? CuentaElegida_704ILR() =>
            _cboCuenta_704ILR.SelectedItem is CuentaItem_704ILR c_704ILR ? c_704ILR.UserId_704ILR : null;

        // IVistaConCambios: la ficha tiene cambios sin guardar cuando lo que Guardar
        // enviaria difiere de su linea base. Sin el permiso de gestion no hay nada que
        // guardar ni que perder.
        bool IVistaConCambios_704ILR.HayCambiosSinGuardar_704ILR =>
            !IsDisposed && _lineaBase_704ILR != null && Permisos_704ILR.Tiene_704ILR("EMPLEADOS_GESTION") && !FichaIgualALineaBase_704ILR();

        private void FijarLineaBase_704ILR(BE_Empleado_704ILR emp_704ILR)
        {
            _lineaBase_704ILR = new BE_Empleado_704ILR
            {
                Id_704ILR = emp_704ILR.Id_704ILR,
                Nombre_704ILR = emp_704ILR.Nombre_704ILR,
                Apellido_704ILR = emp_704ILR.Apellido_704ILR,
                Dni_704ILR = emp_704ILR.Dni_704ILR,
                EspecialidadId_704ILR = emp_704ILR.EspecialidadId_704ILR,
                UserId_704ILR = emp_704ILR.UserId_704ILR,
                Activo_704ILR = emp_704ILR.Activo_704ILR
            };
        }

        // Los textos se comparan como los guarda la capa de negocio: tal como se ven.
        private bool FichaIgualALineaBase_704ILR()
        {
            var b_704ILR = _lineaBase_704ILR;
            return GestorDeIdioma_704ILR.TextoVisible_704ILR(_txtNombre_704ILR.Text) == GestorDeIdioma_704ILR.TextoVisible_704ILR(b_704ILR.Nombre_704ILR)
                && GestorDeIdioma_704ILR.TextoVisible_704ILR(_txtApellido_704ILR.Text) == GestorDeIdioma_704ILR.TextoVisible_704ILR(b_704ILR.Apellido_704ILR)
                && GestorDeIdioma_704ILR.TextoVisible_704ILR(_txtDni_704ILR.Text) == GestorDeIdioma_704ILR.TextoVisible_704ILR(b_704ILR.Dni_704ILR)
                && EspecialidadElegida_704ILR() == b_704ILR.EspecialidadId_704ILR
                && CuentaElegida_704ILR() == b_704ILR.UserId_704ILR
                && _chkActivo_704ILR.Checked == b_704ILR.Activo_704ILR;
        }

        // La escritura queda envuelta para que una falla de base se asiente e informe en
        // la ficha en vez de terminar en el dialogo de excepcion no controlada.
        private void Guardar_704ILR()
        {
            try
            {
                GuardarEmpleado_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Empleados",
                    _editId_704ILR == 0 ? "Guardar empleado nuevo" : "Guardar empleado #" + _editId_704ILR);
                _lblOk_704ILR.Visible = false;
                MostrarError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        private void GuardarEmpleado_704ILR()
        {
            // Segunda capa del control de acceso (ver Permisos.cs).
            if (!Permisos_704ILR.Exigir_704ILR("EMPLEADOS_GESTION", FindForm(),
                    _editId_704ILR == 0 ? "crear un empleado" : "editar el empleado #" + _editId_704ILR))
                return;

            _lblError_704ILR.Visible = false;
            _lblOk_704ILR.Visible = false;

            var emp_704ILR = new BE_Empleado_704ILR
            {
                Id_704ILR = _editId_704ILR,
                Nombre_704ILR = _txtNombre_704ILR.Text,
                Apellido_704ILR = _txtApellido_704ILR.Text,
                Dni_704ILR = _txtDni_704ILR.Text,
                EspecialidadId_704ILR = EspecialidadElegida_704ILR(),
                UserId_704ILR = CuentaElegida_704ILR(),
                Activo_704ILR = _chkActivo_704ILR.Checked
            };
            bool esAlta_704ILR = _editId_704ILR == 0;
            int nuevoId_704ILR = 0;
            EmpleadoResult_704ILR r_704ILR = esAlta_704ILR ? BLL_Empleado_704ILR.Crear_704ILR(emp_704ILR, out nuevoId_704ILR) : BLL_Empleado_704ILR.Actualizar_704ILR(emp_704ILR);
            if (r_704ILR == EmpleadoResult_704ILR.Success_704ILR)
            {
                LimpiarForm_704ILR();
                // El empleado guardado queda seleccionado y en la ficha. Va antes del cartel:
                // CargarEnForm lo oculta. Si la recarga fallo, su aviso queda a la vista.
                if (SafeLoadData_704ILR())
                    SeleccionarEmpleado_704ILR(esAlta_704ILR ? nuevoId_704ILR : emp_704ILR.Id_704ILR);
                MostrarOk_704ILR(() => T_704ILR("MSG_EMP_OK", "Empleado guardado."));
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

        private static string MensajeError_704ILR(EmpleadoResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case EmpleadoResult_704ILR.NombreInvalido_704ILR:          return T_704ILR("MSG_EMP_NOMBRE", "Ingrese el nombre y el apellido del empleado.");
                case EmpleadoResult_704ILR.LongitudExcedida_704ILR:        return T_704ILR("MSG_EMP_LARGO", "Dato muy largo: nombre y apellido admiten hasta 60 caracteres.");
                case EmpleadoResult_704ILR.DniInvalido_704ILR:             return T_704ILR("MSG_EMP_DNI", "Ingrese el DNI del empleado: solo números (7 dígitos o más).");
                case EmpleadoResult_704ILR.DniDuplicado_704ILR:            return T_704ILR("MSG_EMP_DNI_DUP", "Ya existe un empleado con ese DNI.");
                case EmpleadoResult_704ILR.EspecialidadInvalida_704ILR:    return T_704ILR("MSG_EMP_ESPECIALIDAD", "Seleccione la especialidad del empleado.");
                case EmpleadoResult_704ILR.CuentaInvalida_704ILR:          return T_704ILR("MSG_EMP_CUENTA", "La cuenta elegida ya no existe.");
                case EmpleadoResult_704ILR.CuentaYaVinculada_704ILR:       return T_704ILR("MSG_EMP_CUENTA_DUP", "Esa cuenta ya está vinculada a otro empleado.");
                case EmpleadoResult_704ILR.ConAsignacionesVigentes_704ILR: return T_704ILR("MSG_EMP_CON_TURNOS", "El empleado tiene turnos en eventos confirmados: quítelo de esos eventos, o espere al cierre de los que están en ejecución, antes de darlo de baja.");
                case EmpleadoResult_704ILR.NotFound_704ILR:                return T_704ILR("MSG_EMP_NOTFOUND", "El empleado ya no existe.");
                default:                                                   return T_704ILR("MSG_ERROR", "Error");
            }
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR) => Coord_704ILR.T_704ILR(clave_704ILR, defecto_704ILR);

        // Item del combo de cuentas: "(sin cuenta)" o el nombre de usuario. El texto de
        // la opcion vacia se traduce al repintarse.
        private sealed class CuentaItem_704ILR
        {
            public int? UserId_704ILR { get; }
            private readonly string _username_704ILR;
            public CuentaItem_704ILR(int? userId_704ILR, string username_704ILR) { UserId_704ILR = userId_704ILR; _username_704ILR = username_704ILR; }
            public override string ToString() => UserId_704ILR.HasValue ? _username_704ILR : T_704ILR("EMP_SIN_CUENTA", "(sin cuenta)");
        }
    }
}
