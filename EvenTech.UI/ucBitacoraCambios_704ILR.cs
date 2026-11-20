using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Bitacora de cambios del catalogo de servicios (T06.B, seccion Maestros). Muestra
    // la tabla copia dbo.Servicios_C: cada version de cada servicio con la fecha y la
    // hora del cambio y la marca Act (1 = la version vigente). Filtra por codigo,
    // nombre y fechas (Aplicar / Limpiar), activa la version elegida (Activar: el
    // servicio vuelve a esos datos y la version pasa a Act = 1) y cierra la seccion
    // (Salir). Las versiones las escriben los triggers de dbo.Servicios: esta pantalla
    // no agrega ni borra filas de la copia. Mismo molde visual que la Bitacora general.
    public class ucBitacoraCambios_704ILR : UserControl, IObservadorIdioma_704ILR, IAyudaContextual_704ILR
    {
        // Tema de la ayuda en linea de esta pantalla (F1).
        public string TemaAyuda_704ILR => Ayuda_704ILR.TemaBitacoraCambios_704ILR;

        // Pedido de cerrar la seccion (boton Salir): lo atiende la ventana principal.
        public event EventHandler SalirSolicitado_704ILR;

        private ComboBox _cboCodigo_704ILR, _cboNombre_704ILR;
        private DateTimePicker _dtInicial_704ILR, _dtFinal_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblCount_704ILR, _lblAviso_704ILR;
        private AppButton_704ILR _btnAplicar_704ILR, _btnLimpiar_704ILR, _btnActivar_704ILR, _btnSalir_704ILR;
        private readonly ToolTip _tip_704ILR = new ToolTip();
        private List<BE_Servicio_C_704ILR> _versiones_704ILR = new List<BE_Servicio_C_704ILR>();
        // Aviso vigente guardado de forma re-traducible (clave + defecto + argumentos, o la
        // excepcion); null = no hay aviso. El aviso de exito se pinta en verde.
        private string _claveAviso_704ILR, _defectoAviso_704ILR;
        private object[] _argsAviso_704ILR;
        private Exception _excepcionAviso_704ILR;
        private bool _avisoOk_704ILR;
        private bool _cargada_704ILR;

        public ucBitacoraCambios_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            // El control puede crearse antes de mostrarse (pestana no seleccionada): al
            // cargarse se suscribe al idioma y se re-traduce.
            Load += (s_704ILR, e_704ILR) =>
            {
                CargarCombos_704ILR();
                Aplicar_704ILR();
                _cargada_704ILR = true;
                GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
                ActualizarTextos_704ILR();
            };
            Disposed += (s_704ILR, e_704ILR) =>
            {
                GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
                _tip_704ILR.Dispose();
            };
        }

        // Vuelve a consultar con los filtros a la vista (la seccion lo pide al volver a
        // esta pestana: desde Servicios y productos se pudo dar de alta o modificar un
        // servicio). Antes de la primera carga no hace nada: la hace Load.
        public void Refrescar_704ILR()
        {
            if (!_cargada_704ILR || IsDisposed) return;
            CargarCombos_704ILR();
            Aplicar_704ILR();
        }

        private void BuildUi_704ILR()
        {
            // Filas: 0 titulo, 1 filtros, 2 estado, 3 grilla (fill), 4 acciones.
            var root_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.Transparent };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Bitácora de cambios de servicios");
            lblTitle_704ILR.Tag = "T:BCS_TITULO";
            lblTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            // ---------------- Filtros ----------------
            var cardFiltros_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR),
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceMd_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceSm_704ILR)
            };
            var flow_704ILR = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            // Codigo: el servicio (numero y nombre vigente). Nombre: los nombres que tuvo
            // cada version (un servicio pudo llamarse distinto). Los dos se dibujan con la
            // leyenda del idioma activo, como los combos de la Bitacora general.
            _cboCodigo_704ILR = Ui_704ILR.Combo_704ILR();
            _cboCodigo_704ILR.Width = 205;
            Ui_704ILR.DibujarEnum_704ILR(_cboCodigo_704ILR, o_704ILR => o_704ILR?.ToString());
            _cboNombre_704ILR = Ui_704ILR.Combo_704ILR();
            _cboNombre_704ILR.Width = 205;
            Ui_704ILR.DibujarEnum_704ILR(_cboNombre_704ILR, o_704ILR => o_704ILR?.ToString());

            _dtInicial_704ILR = Ui_704ILR.DatePicker_704ILR();
            _dtInicial_704ILR.Width = 130;
            _dtInicial_704ILR.ShowCheckBox = true;
            _dtInicial_704ILR.Checked = false;
            _dtFinal_704ILR = Ui_704ILR.DatePicker_704ILR();
            _dtFinal_704ILR.Width = 130;
            _dtFinal_704ILR.ShowCheckBox = true;
            _dtFinal_704ILR.Checked = false;

            var fCodigo_704ILR = Ui_704ILR.Field_704ILR("Código", _cboCodigo_704ILR); fCodigo_704ILR.Tag = "FIELD:BCS_CODIGO";
            var fNombre_704ILR = Ui_704ILR.Field_704ILR("Nombre", _cboNombre_704ILR); fNombre_704ILR.Tag = "FIELD:BCS_NOMBRE";
            var fInicial_704ILR = Ui_704ILR.Field_704ILR("Fecha inicial", _dtInicial_704ILR); fInicial_704ILR.Tag = "FIELD:BCS_FECHA_INI";
            var fFinal_704ILR = Ui_704ILR.Field_704ILR("Fecha final", _dtFinal_704ILR); fFinal_704ILR.Tag = "FIELD:BCS_FECHA_FIN";
            // Los cuatro filtros y los dos botones entran en una fila con el ancho minimo
            // del area de contenido de la ventana principal.
            foreach (var f_704ILR in new[] { fCodigo_704ILR, fNombre_704ILR, fInicial_704ILR, fFinal_704ILR })
                f_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, Theme_704ILR.SpaceSm_704ILR);

            _btnAplicar_704ILR = Ui_704ILR.Primary_704ILR("Aplicar", Theme_704ILR.IcoSearch_704ILR);
            _btnAplicar_704ILR.Tag = "T:BCS_BTN_APLICAR";
            _btnAplicar_704ILR.Size = new Size(115, 32);
            _btnAplicar_704ILR.Margin = new Padding(0, 18, Theme_704ILR.SpaceSm_704ILR, 0);
            _btnAplicar_704ILR.Click += (s_704ILR, e_704ILR) => Aplicar_704ILR();

            _btnLimpiar_704ILR = Ui_704ILR.Secondary_704ILR("Limpiar", Theme_704ILR.IcoClear_704ILR);
            _btnLimpiar_704ILR.Tag = "T:BCS_BTN_LIMPIAR";
            _btnLimpiar_704ILR.Size = new Size(115, 32);
            _btnLimpiar_704ILR.Margin = new Padding(0, 18, 0, 0);
            _btnLimpiar_704ILR.Click += (s_704ILR, e_704ILR) => { LimpiarFiltros_704ILR(); Aplicar_704ILR(); };

            flow_704ILR.Controls.AddRange(new Control[] { fCodigo_704ILR, fNombre_704ILR, fInicial_704ILR, fFinal_704ILR, _btnAplicar_704ILR, _btnLimpiar_704ILR });
            cardFiltros_704ILR.Controls.Add(flow_704ILR);
            // Ayuda en linea (D02): el "?" de los filtros abre el apartado "Buscar".
            Ayuda_704ILR.JuntoA_704ILR(Ayuda_704ILR.RotuloDeCampo_704ILR(fCodigo_704ILR), Ayuda_704ILR.ApartadoBitacoraCambiosBuscar_704ILR, cardFiltros_704ILR);

            // ---------------- Estado (conteo / aviso) ----------------
            var pnlEstado_704ILR = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR)
            };
            _lblCount_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.TextMuted_704ILR, BackColor = Color.Transparent, Margin = new Padding(2, 0, 0, 0) };
            _lblAviso_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.Error_704ILR, BackColor = Color.Transparent, Visible = false, MaximumSize = new Size(1000, 0), Margin = new Padding(2, 2, 0, 0) };
            pnlEstado_704ILR.Controls.Add(_lblCount_704ILR);
            pnlEstado_704ILR.Controls.Add(_lblAviso_704ILR);

            // ---------------- Grilla ----------------
            var cardGrid_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            var fechaInv_704ILR = new DataGridViewCellStyle { Format = "yyyy-MM-dd", FormatProvider = CultureInfo.InvariantCulture };
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCodigo", HeaderText = "Código", DataPropertyName = "ServicioId_704ILR", FillWeight = 34 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFecha", HeaderText = "Fecha", DataPropertyName = "Fecha_704ILR", FillWeight = 52, DefaultCellStyle = fechaInv_704ILR });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cHora", HeaderText = "Hora", DataPropertyName = "Hora_704ILR", FillWeight = 40 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNombre", HeaderText = "Nombre", DataPropertyName = "Nombre_704ILR", FillWeight = 130 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPrecio", HeaderText = "Precio", DataPropertyName = "Precio_704ILR", FillWeight = 50, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDescripcion", HeaderText = "Descripción", DataPropertyName = "Descripcion_704ILR", FillWeight = 110 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstado", HeaderText = "Estado", DataPropertyName = "Activo_704ILR", FillWeight = 52 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOperacion", HeaderText = "Operación", DataPropertyName = "Operacion_704ILR", FillWeight = 58 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cUsuario", HeaderText = "Usuario", DataPropertyName = "Usuario_704ILR", FillWeight = 70 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAct", HeaderText = "Act.", DataPropertyName = "ActNumero_704ILR", FillWeight = 26, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            UiGrid_704ILR.Multilinea_704ILR(_grid_704ILR, "cNombre", "cDescripcion", "cUsuario");
            // Columnas de contenido corto y cerrado: toman el ancho de su encabezado y su
            // contenido; nombre, descripcion y usuario (un cambio hecho fuera del sistema
            // registra el inicio de sesion de Windows, que puede ser largo) se reparten el resto.
            UiGrid_704ILR.AlContenido_704ILR(_grid_704ILR, "cCodigo", "cFecha", "cHora", "cPrecio", "cEstado", "cOperacion", "cAct");
            _grid_704ILR.CellFormatting += Grid_CellFormatting_704ILR;
            _grid_704ILR.SelectionChanged += (s_704ILR, e_704ILR) => ActualizarBotones_704ILR();
            // La grilla elige sola la primera fila cuando se muestra por primera vez (la
            // pestana se arma oculta): se deshace, para que ninguna version quede elegida
            // sin que el usuario la elija (Activar depende de esa eleccion).
            _grid_704ILR.VisibleChanged += (s_704ILR, e_704ILR) =>
            {
                if (_grid_704ILR.Visible && _grid_704ILR.IsHandleCreated && !_conEleccion_704ILR)
                    _grid_704ILR.BeginInvoke(new Action(SinSeleccion_704ILR));
            };
            _grid_704ILR.CellDoubleClick += (s_704ILR, e_704ILR) => { if (e_704ILR.RowIndex >= 0) Activar_704ILR(); };
            cardGrid_704ILR.Controls.Add(_grid_704ILR);

            // ---------------- Acciones ----------------
            var acciones_704ILR = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
                BackColor = Color.Transparent, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0)
            };
            _btnSalir_704ILR = Ui_704ILR.Secondary_704ILR("Salir", Theme_704ILR.IcoClose_704ILR);
            _btnSalir_704ILR.Tag = "T:BCS_BTN_SALIR";
            _btnSalir_704ILR.Size = new Size(130, 38);
            _btnSalir_704ILR.Margin = new Padding(Theme_704ILR.SpaceSm_704ILR, 0, 0, 0);
            _btnSalir_704ILR.Click += (s_704ILR, e_704ILR) => SalirSolicitado_704ILR?.Invoke(this, EventArgs.Empty);
            _btnActivar_704ILR = Ui_704ILR.Primary_704ILR("Activar", Theme_704ILR.IcoOk_704ILR);
            _btnActivar_704ILR.Tag = "T:BCS_BTN_ACTIVAR";
            _btnActivar_704ILR.Size = new Size(150, 38);
            _btnActivar_704ILR.Margin = new Padding(0);
            _btnActivar_704ILR.Enabled = false;
            _btnActivar_704ILR.Click += (s_704ILR, e_704ILR) => Activar_704ILR();
            // Sin el permiso de activacion el boton no se muestra (primera capa).
            _btnActivar_704ILR.Visible = Permisos_704ILR.Tiene_704ILR(BLL_Servicio_C_704ILR.PermisoActivar_704ILR);
            acciones_704ILR.Controls.Add(_btnSalir_704ILR);
            acciones_704ILR.Controls.Add(_btnActivar_704ILR);
            // Ayuda (D02): junto a Activar, el "?" del apartado "Volver a una version anterior"
            // (solo para quien puede activar: sin el boton, el apartado no corresponde).
            if (_btnActivar_704ILR.Visible)
                acciones_704ILR.Controls.Add(new BotonAyuda_704ILR(Ayuda_704ILR.ApartadoBitacoraCambiosActivar_704ILR) { Margin = new Padding(0, 10, Theme_704ILR.SpaceSm_704ILR, 0) });
            Ayuda_704ILR.Zona_704ILR(acciones_704ILR, Ayuda_704ILR.ApartadoBitacoraCambiosActivar_704ILR);

            root_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            root_704ILR.Controls.Add(cardFiltros_704ILR, 0, 1);
            root_704ILR.Controls.Add(pnlEstado_704ILR, 0, 2);
            root_704ILR.Controls.Add(cardGrid_704ILR, 0, 3);
            root_704ILR.Controls.Add(acciones_704ILR, 0, 4);
            Controls.Add(root_704ILR);
        }

        // ---------------- Combos de filtro ----------------

        // Codigo: "(Todos)" y cada servicio del catalogo. Nombre: los nombres que
        // aparecen en la bitacora (un servicio pudo llamarse distinto en otra version).
        // Se conserva lo elegido; si la base no responde, quedan como estaban.
        private void CargarCombos_704ILR()
        {
            List<BE_Servicio_704ILR> servicios_704ILR;
            List<string> nombres_704ILR;
            try
            {
                servicios_704ILR = BLL_Servicio_704ILR.GetAll_704ILR();
                nombres_704ILR = BLL_Servicio_C_704ILR.Buscar_704ILR(null)
                    .Select(v_704ILR => v_704ILR.Nombre_704ILR).Distinct(StringComparer.CurrentCulture)
                    .OrderBy(n_704ILR => n_704ILR, StringComparer.CurrentCulture).ToList();
            }
            catch
            {
                foreach (var cbo_704ILR in new[] { _cboCodigo_704ILR, _cboNombre_704ILR })
                {
                    if (cbo_704ILR.Items.Count == 0) cbo_704ILR.Items.Add(new ItemCodigo_704ILR(null, null));
                    if (cbo_704ILR.SelectedIndex < 0) cbo_704ILR.SelectedIndex = 0;
                }
                return;
            }

            int? elegido_704ILR = (_cboCodigo_704ILR.SelectedItem as ItemCodigo_704ILR)?.Id_704ILR;
            _cboCodigo_704ILR.BeginUpdate();
            _cboCodigo_704ILR.Items.Clear();
            _cboCodigo_704ILR.Items.Add(new ItemCodigo_704ILR(null, null));
            _cboCodigo_704ILR.SelectedIndex = 0;
            foreach (var s_704ILR in servicios_704ILR.OrderBy(x_704ILR => x_704ILR.Id_704ILR))
            {
                int i_704ILR = _cboCodigo_704ILR.Items.Add(new ItemCodigo_704ILR(s_704ILR.Id_704ILR, s_704ILR.Nombre_704ILR));
                if (s_704ILR.Id_704ILR == elegido_704ILR) _cboCodigo_704ILR.SelectedIndex = i_704ILR;
            }
            _cboCodigo_704ILR.EndUpdate();

            string nombreElegido_704ILR = _cboNombre_704ILR.SelectedItem as string;
            _cboNombre_704ILR.BeginUpdate();
            _cboNombre_704ILR.Items.Clear();
            _cboNombre_704ILR.Items.Add(new ItemCodigo_704ILR(null, null));
            _cboNombre_704ILR.SelectedIndex = 0;
            foreach (var n_704ILR in nombres_704ILR)
            {
                int i_704ILR = _cboNombre_704ILR.Items.Add(n_704ILR);
                if (n_704ILR == nombreElegido_704ILR) _cboNombre_704ILR.SelectedIndex = i_704ILR;
            }
            _cboNombre_704ILR.EndUpdate();
        }

        private void LimpiarFiltros_704ILR()
        {
            if (_cboCodigo_704ILR.Items.Count > 0) _cboCodigo_704ILR.SelectedIndex = 0;
            if (_cboNombre_704ILR.Items.Count > 0) _cboNombre_704ILR.SelectedIndex = 0;
            _dtInicial_704ILR.Checked = false;
            _dtFinal_704ILR.Checked = false;
        }

        // ---------------- Consulta ----------------

        private void Aplicar_704ILR()
        {
            // Segunda capa: la pantalla consulta sola al abrirse, asi que el permiso se
            // comprueba aca y no solo donde se decide mostrarla.
            if (!Permisos_704ILR.TieneAlguno_704ILR(BLL_Servicio_C_704ILR.PermisoVer_704ILR, BLL_Servicio_C_704ILR.PermisoActivar_704ILR))
            {
                Avisar_704ILR("MSG_SIN_PERMISO", "No tenés permiso para realizar esta acción.");
                MostrarVersiones_704ILR(new List<BE_Servicio_C_704ILR>());
                return;
            }
            DateTime? inicial_704ILR = _dtInicial_704ILR.Checked ? _dtInicial_704ILR.Value.Date : (DateTime?)null;
            DateTime? final_704ILR = _dtFinal_704ILR.Checked ? _dtFinal_704ILR.Value.Date : (DateTime?)null;
            // Un rango invertido no se consulta: la lista vacia haria pensar que no hubo cambios.
            if (!BLL_Servicio_C_704ILR.RangoValido_704ILR(inicial_704ILR, final_704ILR))
            {
                Avisar_704ILR("BCS_RANGO", "La fecha inicial no puede ser posterior a la fecha final.");
                MostrarVersiones_704ILR(new List<BE_Servicio_C_704ILR>());
                return;
            }
            try
            {
                var filtros_704ILR = new FiltrosServicio_C_704ILR
                {
                    ServicioId_704ILR = (_cboCodigo_704ILR.SelectedItem as ItemCodigo_704ILR)?.Id_704ILR,
                    Nombre_704ILR = _cboNombre_704ILR.SelectedItem as string,
                    NombreExacto_704ILR = true,
                    FechaInicial_704ILR = inicial_704ILR,
                    FechaFinal_704ILR = final_704ILR
                };
                QuitarAviso_704ILR();
                MostrarVersiones_704ILR(BLL_Servicio_C_704ILR.Buscar_704ILR(filtros_704ILR));
            }
            catch (Exception ex_704ILR)
            {
                if (!(ex_704ILR is PermisoDenegadoException_704ILR))
                    BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Servicios", "Consulta de la bitacora de cambios");
                AvisarExcepcion_704ILR(ex_704ILR);
                MostrarVersiones_704ILR(new List<BE_Servicio_C_704ILR>());
            }
        }

        // Llena la grilla sin elegir ninguna fila por el usuario: Activar se habilita
        // recien cuando el usuario elige una version. Se muestran las ultimas (abajo).
        private void MostrarVersiones_704ILR(List<BE_Servicio_C_704ILR> versiones_704ILR)
        {
            _versiones_704ILR = versiones_704ILR ?? new List<BE_Servicio_C_704ILR>();
            _grid_704ILR.DataSource = null;
            _grid_704ILR.DataSource = _versiones_704ILR;
            _conEleccion_704ILR = false;
            SinSeleccion_704ILR();
            _lblCount_704ILR.Text = Tr_704ILR.F_704ILR("BCS_COUNT", "{0} versión(es)", _versiones_704ILR.Count);
        }

        // Sin version elegida (y las ultimas a la vista). Despues de activar, la version
        // activada queda elegida (Seleccionar_704ILR) y esto ya no la borra.
        private bool _conEleccion_704ILR;
        private void SinSeleccion_704ILR()
        {
            if (_grid_704ILR.IsDisposed || _conEleccion_704ILR) return;
            _grid_704ILR.ClearSelection();
            _grid_704ILR.CurrentCell = null;
            if (_grid_704ILR.Rows.Count > 0 && _grid_704ILR.DisplayRectangle.Height > 0)
            {
                try { _grid_704ILR.FirstDisplayedScrollingRowIndex = _grid_704ILR.Rows.Count - 1; }
                catch (InvalidOperationException) { /* sin alto visible todavia */ }
            }
            ActualizarBotones_704ILR();
        }

        private BE_Servicio_C_704ILR VersionElegida_704ILR() =>
            _grid_704ILR.SelectedRows.Count == 1 ? _grid_704ILR.SelectedRows[0].DataBoundItem as BE_Servicio_C_704ILR : null;

        private void ActualizarBotones_704ILR()
        {
            var v_704ILR = VersionElegida_704ILR();
            _btnActivar_704ILR.Enabled = v_704ILR != null && !v_704ILR.Act_704ILR
                && Permisos_704ILR.Tiene_704ILR(BLL_Servicio_C_704ILR.PermisoActivar_704ILR);
        }

        // ---------------- Activar ----------------

        private void Activar_704ILR()
        {
            if (!_btnActivar_704ILR.Visible) return;   // sin el permiso, el doble clic no hace nada
            // Segunda capa: la accion vuelve a exigir el permiso (la capa de negocio, otra vez).
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Servicio_C_704ILR.PermisoActivar_704ILR, FindForm(), "activar una version de un servicio")) return;
            var v_704ILR = VersionElegida_704ILR();
            if (v_704ILR == null)
            {
                MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("BCS_ELEGIR", "Seleccione en la lista la versión que quiere activar."),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (v_704ILR.Act_704ILR)
            {
                MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("BCS_YA_VIGENTE", "La versión elegida ya es la vigente (Act. = 1): no hay nada que activar."),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Reemplaza los datos vigentes del servicio: la pregunta arranca en No.
            string confirma_704ILR =
                Tr_704ILR.F_704ILR("BCS_CONFIRMA", "¿Activar la versión del {0} a las {1} del servicio {2}?",
                    Fecha_704ILR(v_704ILR.Fecha_704ILR), Hora_704ILR(v_704ILR.Hora_704ILR), "#" + v_704ILR.ServicioId_704ILR)
                + Environment.NewLine + Environment.NewLine
                + Tr_704ILR.F_704ILR("BCS_CONFIRMA_DATOS", "El servicio pasa a tener nombre «{0}», precio {1} y estado «{2}».",
                    v_704ILR.Nombre_704ILR, v_704ILR.Precio_704ILR.ToString("N2"), TextoEstado_704ILR(v_704ILR.Activo_704ILR))
                + Environment.NewLine + Environment.NewLine
                + Tr_704ILR.F_704ILR("BCS_CONFIRMA_RESERVAS", "Las reservas ya registradas conservan el precio con que se contrató cada servicio.");
            if (MessageBox.Show(FindForm(), confirma_704ILR, "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            try
            {
                var r_704ILR = BLL_Servicio_C_704ILR.Activar_704ILR(v_704ILR.Id_704ILR, out BE_Servicio_C_704ILR activada_704ILR);
                switch (r_704ILR)
                {
                    case ActivacionVersionResult_704ILR.Success_704ILR:
                        CargarCombos_704ILR();
                        Aplicar_704ILR();
                        Seleccionar_704ILR(activada_704ILR.Id_704ILR);
                        Avisar_704ILR("BCS_OK", "Versión activada: el servicio {0} tiene ahora los datos del {1} a las {2}.", true,
                            "#" + activada_704ILR.ServicioId_704ILR, Fecha_704ILR(activada_704ILR.Fecha_704ILR), Hora_704ILR(activada_704ILR.Hora_704ILR));
                        break;
                    case ActivacionVersionResult_704ILR.YaVigente_704ILR:
                        Aplicar_704ILR();
                        MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("BCS_YA_VIGENTE", "La versión elegida ya es la vigente (Act. = 1): no hay nada que activar."),
                            "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    case ActivacionVersionResult_704ILR.VersionInexistente_704ILR:
                        Aplicar_704ILR();
                        MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("BCS_INEXISTENTE", "La versión elegida ya no existe."),
                            "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                    case ActivacionVersionResult_704ILR.NombreDuplicado_704ILR:
                        Aplicar_704ILR();
                        MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("BCS_NOMBRE_DUPLICADO", "No se puede activar: otro servicio ya usa el nombre «{0}».", v_704ILR.Nombre_704ILR),
                            "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                }
            }
            catch (Exception ex_704ILR)
            {
                if (!(ex_704ILR is PermisoDenegadoException_704ILR))
                    BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Servicios", "Activar version #" + v_704ILR.Id_704ILR);
                MessageBox.Show(FindForm(), Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), Tr_704ILR.F_704ILR("MSG_ERROR", "Error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Deja elegida la version activada (si quedo en la lista filtrada), para que se
        // vea su Act = 1 junto a la fila.
        private void Seleccionar_704ILR(int versionId_704ILR)
        {
            foreach (DataGridViewRow fila_704ILR in _grid_704ILR.Rows)
            {
                if (fila_704ILR.DataBoundItem is BE_Servicio_C_704ILR v_704ILR && v_704ILR.Id_704ILR == versionId_704ILR)
                {
                    _conEleccion_704ILR = true;
                    _grid_704ILR.CurrentCell = fila_704ILR.Cells["cNombre"];
                    fila_704ILR.Selected = true;
                    break;
                }
            }
            ActualizarBotones_704ILR();
        }

        // ---------------- Formato ----------------

        // Negrita del mismo tamano que la letra de la grilla (la de cuerpo es mas grande y
        // cortaba fecha y hora en la fila vigente). Compartida: no se libera.
        private static readonly Font FuenteVigente_704ILR = new Font(Theme_704ILR.FontSmall_704ILR, FontStyle.Bold);

        private static string Fecha_704ILR(DateTime fecha_704ILR) => fecha_704ILR.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        private static string Hora_704ILR(TimeSpan hora_704ILR) => hora_704ILR.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

        private static string TextoEstado_704ILR(bool activo_704ILR) => activo_704ILR
            ? Tr_704ILR.F_704ILR("BCS_ACTIVO", "Activo")
            : Tr_704ILR.F_704ILR("BCS_INACTIVO", "Dado de baja");

        private static string TextoOperacion_704ILR(string operacion_704ILR)
        {
            switch (operacion_704ILR)
            {
                case "INICIAL": return Tr_704ILR.F_704ILR("BCS_OP_INICIAL", "Versión inicial");
                case "ALTA": return Tr_704ILR.F_704ILR("BCS_OP_ALTA", "Alta");
                case "MODIFICACION": return Tr_704ILR.F_704ILR("BCS_OP_MODIFICACION", "Modificación");
                case "BAJA": return Tr_704ILR.F_704ILR("BCS_OP_BAJA", "Baja");
                default: return operacion_704ILR;
            }
        }

        private void Grid_CellFormatting_704ILR(object sender_704ILR, DataGridViewCellFormattingEventArgs e_704ILR)
        {
            if (e_704ILR.RowIndex < 0 || e_704ILR.ColumnIndex < 0) return;
            string col_704ILR = _grid_704ILR.Columns[e_704ILR.ColumnIndex].Name;
            var v_704ILR = _grid_704ILR.Rows[e_704ILR.RowIndex].DataBoundItem as BE_Servicio_C_704ILR;
            // La version vigente (Act = 1) se distingue en negrita en toda la fila.
            if (v_704ILR != null && v_704ILR.Act_704ILR) e_704ILR.CellStyle.Font = FuenteVigente_704ILR;
            switch (col_704ILR)
            {
                case "cHora":
                    if (e_704ILR.Value is TimeSpan h_704ILR) { e_704ILR.Value = Hora_704ILR(h_704ILR); e_704ILR.FormattingApplied = true; }
                    break;
                case "cEstado":
                    if (e_704ILR.Value is bool a_704ILR)
                    {
                        e_704ILR.Value = TextoEstado_704ILR(a_704ILR);
                        if (!a_704ILR) e_704ILR.CellStyle.ForeColor = Theme_704ILR.Warning_704ILR;
                        e_704ILR.FormattingApplied = true;
                    }
                    break;
                case "cOperacion":
                    if (e_704ILR.Value is string o_704ILR) { e_704ILR.Value = TextoOperacion_704ILR(o_704ILR); e_704ILR.FormattingApplied = true; }
                    break;
                case "cAct":
                    if (v_704ILR != null && v_704ILR.Act_704ILR) e_704ILR.CellStyle.ForeColor = Theme_704ILR.Success_704ILR;
                    break;
            }
        }

        // ---------------- Avisos ----------------

        private void Avisar_704ILR(string clave_704ILR, string defecto_704ILR, bool ok_704ILR = false, params object[] args_704ILR)
        {
            _claveAviso_704ILR = clave_704ILR;
            _defectoAviso_704ILR = defecto_704ILR;
            _argsAviso_704ILR = args_704ILR;
            _excepcionAviso_704ILR = null;
            _avisoOk_704ILR = ok_704ILR;
            PintarAviso_704ILR();
        }

        private void AvisarExcepcion_704ILR(Exception ex_704ILR)
        {
            _claveAviso_704ILR = null;
            _excepcionAviso_704ILR = ex_704ILR;
            _avisoOk_704ILR = false;
            PintarAviso_704ILR();
        }

        private void QuitarAviso_704ILR()
        {
            _claveAviso_704ILR = null;
            _excepcionAviso_704ILR = null;
            _lblAviso_704ILR.Visible = false;
        }

        private void PintarAviso_704ILR()
        {
            _lblAviso_704ILR.Text = _claveAviso_704ILR != null
                ? Tr_704ILR.F_704ILR(_claveAviso_704ILR, _defectoAviso_704ILR, _argsAviso_704ILR ?? new object[0])
                : Tr_704ILR.MensajeExcepcion_704ILR(_excepcionAviso_704ILR);
            _lblAviso_704ILR.ForeColor = _avisoOk_704ILR ? Theme_704ILR.Success_704ILR : Theme_704ILR.Error_704ILR;
            _lblAviso_704ILR.Visible = true;
        }

        // ---------------- Idioma ----------------

        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            ActualizarFieldLabels_704ILR(this);
            var cols_704ILR = _grid_704ILR.Columns;
            cols_704ILR["cCodigo"].HeaderText = Tr_704ILR.F_704ILR("BCS_CODIGO", "Código");
            cols_704ILR["cFecha"].HeaderText = Tr_704ILR.F_704ILR("BCS_COL_FECHA", "Fecha");
            cols_704ILR["cHora"].HeaderText = Tr_704ILR.F_704ILR("BCS_COL_HORA", "Hora");
            cols_704ILR["cNombre"].HeaderText = Tr_704ILR.F_704ILR("BCS_NOMBRE", "Nombre");
            cols_704ILR["cPrecio"].HeaderText = Tr_704ILR.F_704ILR("BCS_COL_PRECIO", "Precio");
            cols_704ILR["cDescripcion"].HeaderText = Tr_704ILR.F_704ILR("BCS_COL_DESCRIPCION", "Descripción");
            cols_704ILR["cEstado"].HeaderText = Tr_704ILR.F_704ILR("BCS_COL_ESTADO", "Estado");
            cols_704ILR["cOperacion"].HeaderText = Tr_704ILR.F_704ILR("BCS_COL_OPERACION", "Operación");
            cols_704ILR["cUsuario"].HeaderText = Tr_704ILR.F_704ILR("BCS_COL_USUARIO", "Usuario");
            cols_704ILR["cAct"].HeaderText = Tr_704ILR.F_704ILR("BCS_COL_ACT", "Act.");
            // "(Todos)" se traduce al dibujarse.
            _cboCodigo_704ILR.Invalidate();
            _cboNombre_704ILR.Invalidate();
            _lblCount_704ILR.Text = Tr_704ILR.F_704ILR("BCS_COUNT", "{0} versión(es)", _versiones_704ILR.Count);
            if (_claveAviso_704ILR != null || _excepcionAviso_704ILR != null) PintarAviso_704ILR();
            Ayuda_704ILR.Describir_704ILR(_tip_704ILR, _btnAplicar_704ILR, "AYU_TT_BCS_APLICAR", "Filtra la bitácora por código, nombre y fechas.");
            Ayuda_704ILR.Describir_704ILR(_tip_704ILR, _btnActivar_704ILR, "AYU_TT_BCS_ACTIVAR", "Vuelve el servicio a los datos de la versión elegida, que pasa a tener Act. = 1.");
            Ayuda_704ILR.Describir_704ILR(_tip_704ILR, _btnSalir_704ILR, "AYU_TT_BCS_SALIR", "Cierra la sección y vuelve a Inicio.");
            UiGrid_704ILR.EncabezadosEnteros_704ILR(_grid_704ILR);
            _grid_704ILR.Invalidate();   // estado y operacion se traducen al dibujarse
        }

        // Traduce el caption de cada Ui.Field con Tag "FIELD:CLAVE" (su primer Label).
        private static void ActualizarFieldLabels_704ILR(Control root_704ILR)
        {
            foreach (Control c_704ILR in root_704ILR.Controls)
            {
                if (c_704ILR.Tag is string tag_704ILR && tag_704ILR.StartsWith("FIELD:", StringComparison.Ordinal))
                {
                    string clave_704ILR = tag_704ILR.Substring(6);
                    foreach (Control hijo_704ILR in c_704ILR.Controls)
                        if (hijo_704ILR is Label lbl_704ILR) { lbl_704ILR.Text = Tr_704ILR.T_704ILR(clave_704ILR); break; }
                }
                if (c_704ILR.HasChildren) ActualizarFieldLabels_704ILR(c_704ILR);
            }
        }

        // Item del combo Codigo: el numero del servicio y su nombre vigente; sin Id es
        // "(Todos)", traducido al dibujarse (tambien es el primer item del combo Nombre).
        private sealed class ItemCodigo_704ILR
        {
            public int? Id_704ILR { get; }
            private readonly string _nombre_704ILR;
            public ItemCodigo_704ILR(int? id_704ILR, string nombre_704ILR) { Id_704ILR = id_704ILR; _nombre_704ILR = nombre_704ILR; }
            public override string ToString() => Id_704ILR.HasValue
                ? Id_704ILR.Value + " – " + _nombre_704ILR
                : Tr_704ILR.F_704ILR("BCS_TODOS", "(Todos)");
        }
    }
}
