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
    // Gestion de idiomas: alta de idioma y edicion de traducciones en grilla editable.
    // Look de panel de administracion (titulo + tarjetas), layout 100% por
    // TableLayoutPanel/FlowLayoutPanel + Dock (sin coordenadas magicas).
    // Observa el cambio de idioma (patron Observer) para re-traducir sus textos.
    public class ucIdiomas_704ILR : UserControl, IObservadorIdioma_704ILR
    {
        private ComboBox _cboIdioma_704ILR;
        private TextBox _txtCodigo_704ILR, _txtNombre_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblMsg_704ILR;

        // Idioma que muestra la grilla y sus textos tal como se leyeron de la base:
        // con eso se sabe que filas edito el usuario (se guardan solo esas) y se
        // avisa antes de descartarlas.
        private int _idiomaCargadoId_704ILR;
        private Dictionary<string, string> _textosCargados_704ILR = new Dictionary<string, string>();
        // Evita reaccionar a los SelectedIndexChanged que dispara el propio codigo
        // (enlace del combo o vuelta atras de una seleccion cancelada).
        private bool _ignorarSeleccion_704ILR;

        public ucIdiomas_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => { CargarIdiomas_704ILR(); GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this); };
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        private void BuildUi_704ILR()
        {
            // ---------------- Raiz: titulo / alta / selector / grilla / acciones ----------------
            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR)
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // titulo
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 116)); // tarjeta nuevo idioma
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // selector de idioma
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // tarjeta con grilla
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // acciones

            // ---------------- Titulo de pagina ----------------
            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Gestion de Idiomas");
            lblTitle_704ILR.Tag = "T:IDI_TITULO";
            lblTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            // ---------------- Tarjeta: nuevo idioma ----------------
            root_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            root_704ILR.Controls.Add(BuildTarjetaNuevo_704ILR(), 0, 1);
            root_704ILR.Controls.Add(BuildSelector_704ILR(), 0, 2);
            root_704ILR.Controls.Add(BuildTarjetaGrilla_704ILR(), 0, 3);
            root_704ILR.Controls.Add(BuildAcciones_704ILR(), 0, 4);

            Controls.Add(root_704ILR);
        }

        // Tarjeta de alta: codigo + nombre + boton crear (en fila, alineados al fondo).
        private CardPanel_704ILR BuildTarjetaNuevo_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR),
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };

            var inner_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            inner_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inner_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            inner_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblNuevo_704ILR = Ui_704ILR.H2_704ILR("Nuevo idioma");
            lblNuevo_704ILR.Tag = "T:IDI_NUEVO";
            lblNuevo_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR);

            // Fila horizontal: campo codigo + campo nombre + boton crear.
            var fila_704ILR = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            _txtCodigo_704ILR = Ui_704ILR.Input_704ILR();
            _txtCodigo_704ILR.MaxLength = 5;
            _txtCodigo_704ILR.Width = 110;
            var fldCodigo_704ILR = Ui_704ILR.Field_704ILR("Codigo (ej. PT)", _txtCodigo_704ILR);
            fldCodigo_704ILR.Width = 130;
            fldCodigo_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            ((Label)fldCodigo_704ILR.GetControlFromPosition(0, 0)).Tag = "T:IDI_CODIGO";

            _txtNombre_704ILR = Ui_704ILR.Input_704ILR();
            // Ancho real de Idiomas.Nombre: sin este tope un nombre mas largo se
            // guardaba recortado y sin aviso.
            _txtNombre_704ILR.MaxLength = 50;
            _txtNombre_704ILR.Width = 240;
            var fldNombre_704ILR = Ui_704ILR.Field_704ILR("Nombre", _txtNombre_704ILR);
            fldNombre_704ILR.Width = 260;
            fldNombre_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);
            ((Label)fldNombre_704ILR.GetControlFromPosition(0, 0)).Tag = "T:IDI_NOMBRE";

            var btnCrear_704ILR = Ui_704ILR.Primary_704ILR("Crear idioma", Theme_704ILR.IcoAdd_704ILR);
            btnCrear_704ILR.Tag = "T:IDI_CREAR";
            btnCrear_704ILR.Size = new Size(170, 32);
            // El boton vive sobre la tarjeta blanca -> BehindColor por defecto (Surface) es correcto.
            btnCrear_704ILR.Margin = new Padding(0, 18, 0, 0); // alinea con el input (debajo del caption)
            btnCrear_704ILR.Click += (s_704ILR, e_704ILR) => CrearIdioma_704ILR();

            fila_704ILR.Controls.Add(fldCodigo_704ILR);
            fila_704ILR.Controls.Add(fldNombre_704ILR);
            fila_704ILR.Controls.Add(btnCrear_704ILR);

            inner_704ILR.Controls.Add(lblNuevo_704ILR, 0, 0);
            inner_704ILR.Controls.Add(fila_704ILR, 0, 1);
            card_704ILR.Controls.Add(inner_704ILR);
            return card_704ILR;
        }

        // Fila: label "Idioma:" + combo de seleccion.
        private FlowLayoutPanel BuildSelector_704ILR()
        {
            var fila_704ILR = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };

            var lblEditar_704ILR = Ui_704ILR.BodyBold_704ILR("Idioma:");
            lblEditar_704ILR.Tag = "T:IDI_IDIOMA";
            lblEditar_704ILR.Margin = new Padding(0, 6, Theme_704ILR.SpaceMd_704ILR, 0);
            lblEditar_704ILR.AutoSize = true;

            _cboIdioma_704ILR = Ui_704ILR.Combo_704ILR();
            _cboIdioma_704ILR.Width = 240;
            _cboIdioma_704ILR.Margin = new Padding(0, 2, 0, 0);
            _cboIdioma_704ILR.SelectedIndexChanged += (s_704ILR, e_704ILR) => IdiomaElegido_704ILR();

            fila_704ILR.Controls.Add(lblEditar_704ILR);
            fila_704ILR.Controls.Add(_cboIdioma_704ILR);
            return fila_704ILR;
        }

        // Tarjeta que contiene la grilla editable (Dock=Fill).
        private CardPanel_704ILR BuildTarjetaGrilla_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR),
                Padding = new Padding(Theme_704ILR.SpaceSm_704ILR)
            };

            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR, editable_704ILR: true);
            _grid_704ILR.SelectionMode = DataGridViewSelectionMode.CellSelect;

            var colClave_704ILR = new DataGridViewTextBoxColumn
            {
                HeaderText = "Clave",
                Name = "colClave",
                FillWeight = 40,
                ReadOnly = true
            };
            colClave_704ILR.DefaultCellStyle.BackColor = Theme_704ILR.SurfaceAlt_704ILR;
            colClave_704ILR.DefaultCellStyle.ForeColor = Theme_704ILR.TextMuted_704ILR;

            var colTexto_704ILR = new DataGridViewTextBoxColumn
            {
                HeaderText = "Texto",
                Name = "colTexto",
                FillWeight = 60,
                // Ancho real de Traducciones.Texto: sin este tope un texto mas largo
                // se guardaria recortado y sin aviso.
                MaxInputLength = 250
            };

            _grid_704ILR.Columns.Add(colClave_704ILR);
            _grid_704ILR.Columns.Add(colTexto_704ILR);

            card_704ILR.Controls.Add(_grid_704ILR);
            return card_704ILR;
        }

        // Fila inferior: boton guardar + mensaje de estado.
        private FlowLayoutPanel BuildAcciones_704ILR()
        {
            var fila_704ILR = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            var btnGuardar_704ILR = Ui_704ILR.Primary_704ILR("Guardar traducciones", Theme_704ILR.IcoSave_704ILR);
            btnGuardar_704ILR.Tag = "T:IDI_GUARDAR";
            btnGuardar_704ILR.Size = new Size(220, 38);
            btnGuardar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; // vive sobre el area de contenido, no sobre tarjeta
            btnGuardar_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);
            btnGuardar_704ILR.Click += (s_704ILR, e_704ILR) => GuardarTraducciones_704ILR();

            _lblMsg_704ILR = new Label
            {
                AutoSize = true,
                Font = Theme_704ILR.FontBodyBold_704ILR,
                BackColor = Color.Transparent,
                MaximumSize = new Size(560, 0),
                Margin = new Padding(0, 9, 0, 0)
            };

            fila_704ILR.Controls.Add(btnGuardar_704ILR);
            fila_704ILR.Controls.Add(_lblMsg_704ILR);
            return fila_704ILR;
        }

        // Observer: re-traduce textos por Tag + encabezados de columnas.
        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            if (_grid_704ILR.Columns.Count >= 2)
            {
                _grid_704ILR.Columns["colClave"].HeaderText = Tr_704ILR.T_704ILR("COL_CLAVE");
                _grid_704ILR.Columns["colTexto"].HeaderText = Tr_704ILR.T_704ILR("COL_TEXTO");
            }
        }

        private void CargarIdiomas_704ILR()
        {
            try
            {
                // DisplayMember y ValueMember ANTES que DataSource: asignar la fuente
                // dispara SelectedIndexChanged y, sin ValueMember, SelectedValue era el
                // objeto BE_Idioma y no su Id, asi que la grilla abria vacia aunque el
                // combo mostrara un idioma. La grilla se carga una vez, al final.
                bool ignorabaSeleccion_704ILR = _ignorarSeleccion_704ILR;
                _ignorarSeleccion_704ILR = true;
                try
                {
                    _cboIdioma_704ILR.DisplayMember = "Nombre_704ILR";
                    _cboIdioma_704ILR.ValueMember = "Id_704ILR";
                    _cboIdioma_704ILR.DataSource = BLL_Idioma_704ILR.GetIdiomas_704ILR();
                    if (_cboIdioma_704ILR.Items.Count > 0) _cboIdioma_704ILR.SelectedIndex = 0;
                }
                finally { _ignorarSeleccion_704ILR = ignorabaSeleccion_704ILR; }
                CargarTraducciones_704ILR();
            }
            catch (Exception ex_704ILR) { BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Idiomas", "Cargar idiomas"); Mensaje_704ILR(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), true); }
        }

        // El usuario eligio otro idioma en el combo. Con ediciones sin guardar se
        // pregunta antes de descartarlas; si cancela, el combo vuelve al idioma que
        // se estaba editando y la edicion sigue a la vista.
        // El idioma elegido se toma ANTES de preguntar y, mientras dura la pregunta, se
        // ignoran los cambios de seleccion: con la lista desplegada, abrir el aviso la
        // cierra y el combo nativo vuelve a la seleccion anterior, y ese segundo
        // SelectedIndexChanged apilaba otro aviso igual. Al terminar, el combo queda en
        // el idioma elegido o, si se cancelo, en el que se estaba editando.
        private void IdiomaElegido_704ILR()
        {
            if (_ignorarSeleccion_704ILR) return;
            object destino_704ILR = _cboIdioma_704ILR.SelectedValue;
            // El combo nativo avisa un cambio de seleccion aunque el indice no cambie
            // (clic sobre el idioma ya elegido en la lista desplegada o, a veces, solo al
            // abrirla): si es el idioma que ya muestra la grilla no hay nada que
            // preguntar ni recargar, y la edicion en curso sigue a la vista.
            if (_idiomaCargadoId_704ILR != 0 && Equals(destino_704ILR, _idiomaCargadoId_704ILR)) return;
            bool continuar_704ILR;
            _ignorarSeleccion_704ILR = true;
            try
            {
                continuar_704ILR = ConfirmarDescarte_704ILR();
                object final_704ILR = continuar_704ILR ? destino_704ILR : _idiomaCargadoId_704ILR;
                if (final_704ILR != null && !Equals(_cboIdioma_704ILR.SelectedValue, final_704ILR))
                    _cboIdioma_704ILR.SelectedValue = final_704ILR;
            }
            finally { _ignorarSeleccion_704ILR = false; }
            if (continuar_704ILR) CargarTraducciones_704ILR();
        }

        private void CargarTraducciones_704ILR()
        {
            _grid_704ILR.Rows.Clear();
            _idiomaCargadoId_704ILR = 0;
            _textosCargados_704ILR = new Dictionary<string, string>();
            if (!(_cboIdioma_704ILR.SelectedValue is int idiomaId_704ILR)) return;
            try
            {
                Dictionary<string, string> trads_704ILR = BLL_Idioma_704ILR.GetTraducciones_704ILR(idiomaId_704ILR);
                foreach (var kv_704ILR in trads_704ILR)
                    _grid_704ILR.Rows.Add(kv_704ILR.Key, kv_704ILR.Value);
                _textosCargados_704ILR = trads_704ILR;
                _idiomaCargadoId_704ILR = idiomaId_704ILR;
            }
            catch (Exception ex_704ILR) { BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Idiomas", "Cargar traducciones"); Mensaje_704ILR(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), true); }
        }

        private void CrearIdioma_704ILR()
        {
            // Segunda capa del control de acceso (ver Permisos.cs).
            if (!Permisos_704ILR.Exigir_704ILR("IDIOMAS_GESTION", FindForm(), "crear un idioma")) return;
            // El alta deja la grilla en el idioma nuevo: antes se pregunta por las
            // ediciones pendientes del idioma actual. Con "No" se descartan recien si el
            // alta prospera: un alta rechazada (codigo invalido, nombre repetido) no le
            // hace perder al usuario lo que habia editado.
            DialogResult pendientes_704ILR = PreguntarPendientes_704ILR();
            if (pendientes_704ILR == DialogResult.Cancel) return;
            try
            {
                IdiomaResult_704ILR res_704ILR = BLL_Idioma_704ILR.CrearIdioma_704ILR(_txtCodigo_704ILR.Text, _txtNombre_704ILR.Text, out int nuevoId_704ILR);
                if (res_704ILR != IdiomaResult_704ILR.Success_704ILR)
                {
                    Mensaje_704ILR(MensajeError_704ILR(res_704ILR), true);
                    return;
                }
                if (pendientes_704ILR == DialogResult.No) DescartarCambios_704ILR();
                _txtCodigo_704ILR.Clear();
                _txtNombre_704ILR.Clear();
                CargarIdiomas_704ILR();
                _cboIdioma_704ILR.SelectedValue = nuevoId_704ILR;
                RefrescarSelectorPrincipal_704ILR();
                Mensaje_704ILR(Tr_704ILR.T_704ILR("MSG_IDI_CREADO"), false);
            }
            catch (Exception ex_704ILR) { BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Idiomas", "Crear idioma"); Mensaje_704ILR(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), true); }
        }

        private void GuardarTraducciones_704ILR() => GuardarCambios_704ILR();

        // Guarda las filas editadas del idioma que muestra la grilla. Devuelve true
        // si no queda nada pendiente (se guardo o no habia cambios).
        private bool GuardarCambios_704ILR()
        {
            if (_idiomaCargadoId_704ILR == 0)
            {
                Mensaje_704ILR(Tr_704ILR.F_704ILR("MSG_IDI_SELECCIONE", "Seleccione un idioma."), true);
                return false;
            }
            if (!Permisos_704ILR.Exigir_704ILR("IDIOMAS_GESTION", FindForm(), "guardar traducciones")) return false;
            try
            {
                // Solo las filas que cambiaron: guardar sin tocar nada no es una edicion
                // (ni se informa como tal ni se asienta), y un texto de otra clave que
                // ya estaba guardado no bloquea lo que el usuario si edito.
                Dictionary<string, string> cambios_704ILR = TextosModificados_704ILR();
                if (cambios_704ILR.Count == 0)
                {
                    Aviso_704ILR(Tr_704ILR.F_704ILR("IDI_SIN_CAMBIOS", "No hay cambios para guardar."));
                    return true;
                }
                // Un texto vacio, con llaves sin cerrar o con marcadores que no son los
                // de la clave deja sin texto o rompe la pantalla que lo muestra: la capa
                // de negocio rechaza el lote entero, lo asienta y nombra la clave.
                TraduccionResult_704ILR res_704ILR = BLL_Idioma_704ILR.GuardarTraducciones_704ILR(
                    _idiomaCargadoId_704ILR, cambios_704ILR, out string clave_704ILR);
                if (res_704ILR == TraduccionResult_704ILR.TextoVacio_704ILR)
                {
                    Mensaje_704ILR(Tr_704ILR.F_704ILR("IDI_TEXTO_VACIO",
                        "El texto de '{0}' no puede quedar vacío.", clave_704ILR), true);
                    return false;
                }
                if (res_704ILR == TraduccionResult_704ILR.PlantillaInvalida_704ILR)
                {
                    Mensaje_704ILR(Tr_704ILR.F_704ILR("IDI_PLANTILLA_INVALIDA",
                        "El texto de '{0}' tiene llaves sin cerrar o marcadores que no coinciden con los que la clave necesita.", clave_704ILR), true);
                    return false;
                }
                // CMP_FILTER es el filtro del cuadro "Guardar como" del comprobante: sin
                // pares descripcion|patron el cuadro no se abre y el boton queda inservible.
                if (res_704ILR == TraduccionResult_704ILR.TextoLargo_704ILR)
                {
                    Mensaje_704ILR(Tr_704ILR.F_704ILR("IDI_TEXTO_LARGO",
                        "El texto de '{0}' supera los 250 caracteres.", clave_704ILR), true);
                    return false;
                }
                if (res_704ILR == TraduccionResult_704ILR.ClaveInvalida_704ILR)
                {
                    Mensaje_704ILR(Tr_704ILR.F_704ILR("IDI_CLAVE_INVALIDA",
                        "'{0}' no es una clave de este idioma. Volvé a cargar el idioma.", clave_704ILR), true);
                    return false;
                }
                if (res_704ILR == TraduccionResult_704ILR.FiltroInvalido_704ILR)
                {
                    Mensaje_704ILR(Tr_704ILR.F_704ILR("IDI_FILTRO_INVALIDO",
                        "El texto de '{0}' tiene que ser un filtro de archivos: descripción y patrón separados por '|', por ejemplo: Documento HTML (*.html)|*.html", clave_704ILR), true);
                    return false;
                }
                foreach (var kv_704ILR in cambios_704ILR) _textosCargados_704ILR[kv_704ILR.Key] = kv_704ILR.Value;
                RefrescarSelectorPrincipal_704ILR();
                // Si edite el idioma activo, refrescar esta misma vista. No sirve
                // "cambiar" al idioma que ya esta puesto: el gestor corta por igualdad
                // de codigo y no notifica. Refrescar_704ILR avisa a los observadores
                // sin tocar el idioma seleccionado (patron Observer).
                GestorDeIdioma_704ILR.GetInstance_704ILR.Refrescar_704ILR();
                Mensaje_704ILR(Tr_704ILR.F_704ILR("MSG_IDI_GUARDADO", "Traducciones guardadas."), false);
                return true;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Idiomas", "Guardar traducciones");
                Mensaje_704ILR(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), true);
                return false;
            }
        }

        // Filas cuyo texto difiere del que se leyo de la base (clave -> texto nuevo).
        private Dictionary<string, string> TextosModificados_704ILR()
        {
            _grid_704ILR.EndEdit();
            var cambios_704ILR = new Dictionary<string, string>();
            foreach (DataGridViewRow row_704ILR in _grid_704ILR.Rows)
            {
                string clave_704ILR = row_704ILR.Cells["colClave"].Value?.ToString();
                if (string.IsNullOrEmpty(clave_704ILR)) continue;
                string texto_704ILR = row_704ILR.Cells["colTexto"].Value?.ToString() ?? string.Empty;
                _textosCargados_704ILR.TryGetValue(clave_704ILR, out string original_704ILR);
                if (!string.Equals(texto_704ILR, original_704ILR ?? string.Empty, StringComparison.Ordinal))
                    cambios_704ILR[clave_704ILR] = texto_704ILR;
            }
            return cambios_704ILR;
        }

        // Antes de dejar el idioma que muestra la grilla (elegir otro, crear uno o
        // cerrar el dialogo): con ediciones sin guardar, pregunta si guardarlas.
        // Devuelve false si el usuario cancela o si el guardado se rechazo (la
        // edicion sigue a la vista, con el motivo).
        internal bool ConfirmarDescarte_704ILR()
        {
            DialogResult respuesta_704ILR = PreguntarPendientes_704ILR();
            if (respuesta_704ILR == DialogResult.Cancel) return false;
            if (respuesta_704ILR == DialogResult.No) DescartarCambios_704ILR();
            return true;
        }

        // Pregunta por las ediciones sin guardar del idioma que muestra la grilla y
        // guarda si el usuario lo pide. Devuelve None si no habia nada pendiente, Yes
        // si se guardo, No si eligio no guardar (las ediciones siguen en la grilla:
        // quien pregunta decide cuando descartarlas) y Cancel si cancelo o si el
        // guardado se rechazo.
        private DialogResult PreguntarPendientes_704ILR()
        {
            if (_idiomaCargadoId_704ILR == 0 || TextosModificados_704ILR().Count == 0) return DialogResult.None;

            string nombre_704ILR = _cboIdioma_704ILR.Items.OfType<BE_Idioma_704ILR>()
                .FirstOrDefault(i_704ILR => i_704ILR.Id_704ILR == _idiomaCargadoId_704ILR)?.Nombre_704ILR
                ?? ("#" + _idiomaCargadoId_704ILR);
            DialogResult respuesta_704ILR = MessageBox.Show(FindForm(),
                Tr_704ILR.F_704ILR("IDI_CAMBIOS_PENDIENTES",
                    "Hay traducciones sin guardar en '{0}'. ¿Desea guardarlas antes de continuar?", nombre_704ILR),
                "EvenTech", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (respuesta_704ILR == DialogResult.Yes)
                return GuardarCambios_704ILR() ? DialogResult.Yes : DialogResult.Cancel;
            return respuesta_704ILR == DialogResult.No ? DialogResult.No : DialogResult.Cancel;
        }

        // Vuelve cada celda editada al texto que se leyo de la base.
        private void DescartarCambios_704ILR()
        {
            foreach (DataGridViewRow row_704ILR in _grid_704ILR.Rows)
            {
                string clave_704ILR = row_704ILR.Cells["colClave"].Value?.ToString();
                if (clave_704ILR != null && _textosCargados_704ILR.TryGetValue(clave_704ILR, out string original_704ILR))
                    row_704ILR.Cells["colTexto"].Value = original_704ILR;
            }
        }

        private void RefrescarSelectorPrincipal_704ILR()
        {
            if (FindForm() is frmMain_704ILR main_704ILR) main_704ILR.RefrescarIdiomas_704ILR();
        }

        private static string MensajeError_704ILR(IdiomaResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case IdiomaResult_704ILR.CodigoInvalido_704ILR:  return Tr_704ILR.T_704ILR("MSG_IDI_COD_INV");
                case IdiomaResult_704ILR.NombreInvalido_704ILR:  return Tr_704ILR.T_704ILR("MSG_IDI_NOM_INV");
                case IdiomaResult_704ILR.CodigoDuplicado_704ILR: return Tr_704ILR.T_704ILR("MSG_IDI_DUP");
                case IdiomaResult_704ILR.NombreDuplicado_704ILR: return Tr_704ILR.F_704ILR("MSG_IDI_NOM_DUP", "Ya existe un idioma con ese nombre.");
                default:                           return Tr_704ILR.T_704ILR("MSG_IDI_ERROR");
            }
        }

        private void Mensaje_704ILR(string texto_704ILR, bool error_704ILR)
        {
            _lblMsg_704ILR.ForeColor = error_704ILR ? Theme_704ILR.Error_704ILR : Theme_704ILR.Success_704ILR;
            _lblMsg_704ILR.Text = texto_704ILR;
        }

        // Aviso neutro (ni exito ni error): la accion no tenia nada que hacer.
        private void Aviso_704ILR(string texto_704ILR)
        {
            _lblMsg_704ILR.ForeColor = Theme_704ILR.Warning_704ILR;
            _lblMsg_704ILR.Text = texto_704ILR;
        }
    }
}
