using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Respaldos de la base de datos: pestana de Auditoria y cuerpo del dialogo de
    // restauracion que ofrece la alerta de integridad. Lista los respaldos de la carpeta
    // del sistema, crea uno nuevo, agrega un .bak de otra carpeta y restaura el elegido.
    // Mismo diseno que el registro de excepciones: titulo, tarjeta de acciones, estado y
    // grilla.
    //
    // 'soloRestaurar': dentro del dialogo de la alerta no se ofrece crear un respaldo (la
    // base tiene datos alterados) y, al restaurar, quien la contiene decide que sigue.
    public class ucRespaldos_704ILR : UserControl, IObservadorIdioma_704ILR, IAyudaContextual_704ILR
    {
        private readonly bool _soloRestaurar_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblCarpeta_704ILR, _lblEstado_704ILR;
        private AppButton_704ILR _btnCrear_704ILR, _btnRestaurar_704ILR, _btnAgregar_704ILR, _btnActualizar_704ILR;
        private List<BE_Respaldo_704ILR> _respaldos_704ILR = new List<BE_Respaldo_704ILR>();
        private Exception _error_704ILR;

        // Una restauracion termino bien: la base ya es la del respaldo.
        public event EventHandler<ResultadoRestauracion_704ILR> Restaurado_704ILR;

        public ucRespaldos_704ILR() : this(false) { }

        public ucRespaldos_704ILR(bool soloRestaurar_704ILR)
        {
            _soloRestaurar_704ILR = soloRestaurar_704ILR;
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) =>
            {
                Cargar_704ILR();
                GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
                ActualizarTextos_704ILR();
            };
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        // Tema de la ayuda en linea de esta pantalla (F1).
        public string TemaAyuda_704ILR => Ayuda_704ILR.ApartadoAuditoriaRespaldos_704ILR;

        private void BuildUi_704ILR()
        {
            var root_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // titulo
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // acciones
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // estado
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // grilla

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Respaldos de la base de datos");
            lblTitle_704ILR.Tag = "T:RSP_TITULO";
            lblTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill, AutoSize = true,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR),
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceMd_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceMd_704ILR)
            };
            var flow_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, BackColor = Color.Transparent };
            _btnCrear_704ILR = Boton_704ILR(Ui_704ILR.Primary_704ILR("Crear respaldo", Theme_704ILR.IcoSave_704ILR), "RSP_BTN_CREAR", 170, Crear_704ILR);
            _btnRestaurar_704ILR = Boton_704ILR(Ui_704ILR.Primary_704ILR("Restaurar el respaldo elegido", Theme_704ILR.IcoHistory_704ILR), "RSP_BTN_RESTAURAR", 250, RestaurarElegido_704ILR);
            _btnAgregar_704ILR = Boton_704ILR(Ui_704ILR.Secondary_704ILR("Buscar otro archivo..."), "RSP_BTN_AGREGAR", 190, Agregar_704ILR);
            _btnActualizar_704ILR = Boton_704ILR(Ui_704ILR.Secondary_704ILR("Actualizar"), "RSP_BTN_ACTUALIZAR", 120, Cargar_704ILR);
            // Con datos alterados no se ofrece respaldar: el respaldo previo a la
            // restauracion ya guarda la base como esta.
            _btnCrear_704ILR.Visible = !_soloRestaurar_704ILR && Permisos_704ILR.Tiene_704ILR(BLL_Respaldo_704ILR.PermisoCrear_704ILR);
            bool restaura_704ILR = Permisos_704ILR.Tiene_704ILR(BLL_Respaldo_704ILR.PermisoRestaurar_704ILR);
            _btnRestaurar_704ILR.Visible = restaura_704ILR;
            _btnAgregar_704ILR.Visible = restaura_704ILR;
            flow_704ILR.Controls.AddRange(new Control[] { _btnCrear_704ILR, _btnRestaurar_704ILR, _btnAgregar_704ILR, _btnActualizar_704ILR });
            card_704ILR.Controls.Add(flow_704ILR);

            var estado_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR) };
            _lblCarpeta_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, BackColor = Color.Transparent, MaximumSize = new Size(1000, 0) };
            _lblEstado_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.TextMuted_704ILR, BackColor = Color.Transparent, MaximumSize = new Size(1000, 0) };
            estado_704ILR.Controls.Add(_lblCarpeta_704ILR, 0, 0);
            estado_704ILR.Controls.Add(_lblEstado_704ILR, 0, 1);

            var cardGrid_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFecha", DataPropertyName = "Fecha_704ILR", FillWeight = 60, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss", FormatProvider = CultureInfo.InvariantCulture } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMotivo", DataPropertyName = "Motivo_704ILR", FillWeight = 110 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cBase", DataPropertyName = "BaseOrigen_704ILR", FillWeight = 70 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTamanio", DataPropertyName = "Tamanio_704ILR", FillWeight = 30 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cArchivo", DataPropertyName = "Archivo_704ILR", FillWeight = 120 });
            // Texto entero: el motivo, la base y el archivo pasan a un segundo renglon antes
            // que cortarse; la fecha y el tamano toman el ancho de su contenido.
            UiGrid_704ILR.Multilinea_704ILR(_grid_704ILR, "cMotivo", "cBase", "cArchivo");
            UiGrid_704ILR.AlContenido_704ILR(_grid_704ILR, "cFecha", "cTamanio");
            _grid_704ILR.CellFormatting += Grid_CellFormatting_704ILR;
            _grid_704ILR.SelectionChanged += (s_704ILR, e_704ILR) => ActualizarBotones_704ILR();
            _grid_704ILR.CellDoubleClick += (s_704ILR, e_704ILR) => { if (e_704ILR.RowIndex >= 0 && _btnRestaurar_704ILR.Visible) RestaurarElegido_704ILR(); };
            cardGrid_704ILR.Controls.Add(_grid_704ILR);

            root_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            root_704ILR.Controls.Add(card_704ILR, 0, 1);
            root_704ILR.Controls.Add(estado_704ILR, 0, 2);
            root_704ILR.Controls.Add(cardGrid_704ILR, 0, 3);
            Controls.Add(root_704ILR);
        }

        private static AppButton_704ILR Boton_704ILR(AppButton_704ILR b_704ILR, string clave_704ILR, int ancho_704ILR, Action accion_704ILR)
        {
            b_704ILR.Tag = "T:" + clave_704ILR;
            b_704ILR.Size = new Size(ancho_704ILR, 32);
            b_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            b_704ILR.Click += (s_704ILR, e_704ILR) => accion_704ILR();
            return b_704ILR;
        }

        // Segunda capa del control de acceso: la vista carga sola al abrirse, asi que el
        // permiso se exige aca.
        private void Cargar_704ILR()
        {
            _error_704ILR = null;
            if (!Permisos_704ILR.TieneAlguno_704ILR(BLL_Respaldo_704ILR.PermisoCrear_704ILR, BLL_Respaldo_704ILR.PermisoRestaurar_704ILR))
            {
                _respaldos_704ILR = new List<BE_Respaldo_704ILR>();
                Enlazar_704ILR();
                _lblEstado_704ILR.Text = T_704ILR("MSG_SIN_PERMISO", "No tenés permiso para realizar esta acción.");
                return;
            }
            Cursor = Cursors.WaitCursor;
            try { _respaldos_704ILR = BLL_Respaldo_704ILR.Listar_704ILR(); }
            catch (Exception ex_704ILR)
            {
                _error_704ILR = ex_704ILR;
                _respaldos_704ILR = new List<BE_Respaldo_704ILR>();
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Respaldos", "Listado de respaldos");
            }
            finally { Cursor = Cursors.Default; }
            Enlazar_704ILR();
        }

        private void Enlazar_704ILR()
        {
            _grid_704ILR.DataSource = null;
            _grid_704ILR.DataSource = _respaldos_704ILR;
            ActualizarEstado_704ILR();
            ActualizarBotones_704ILR();
        }

        private BE_Respaldo_704ILR Elegido_704ILR =>
            _grid_704ILR.CurrentRow != null && _grid_704ILR.CurrentRow.Selected ? _grid_704ILR.CurrentRow.DataBoundItem as BE_Respaldo_704ILR : null;

        private void ActualizarBotones_704ILR()
        {
            var elegido_704ILR = Elegido_704ILR;
            _btnRestaurar_704ILR.Enabled = elegido_704ILR != null && elegido_704ILR.Legible_704ILR;
        }

        private void ActualizarEstado_704ILR()
        {
            _lblCarpeta_704ILR.Text = Tr_704ILR.F_704ILR("RSP_CARPETA", "Carpeta de respaldos: {0}", BLL_Respaldo_704ILR.Carpeta_704ILR);
            if (_error_704ILR != null)
                _lblEstado_704ILR.Text = Tr_704ILR.MensajeExcepcion_704ILR(_error_704ILR);
            else if (_respaldos_704ILR.Count == 0)
                _lblEstado_704ILR.Text = T_704ILR("RSP_SIN_RESPALDOS", "Todavía no hay respaldos en la carpeta. Con «Buscar otro archivo...» se puede agregar uno.");
            else
                _lblEstado_704ILR.Text = Tr_704ILR.F_704ILR("RSP_CONTEO", "{0} respaldo(s). Se restaura el elegido; antes se guarda un respaldo de la base actual.", _respaldos_704ILR.Count);
        }

        private void Grid_CellFormatting_704ILR(object sender_704ILR, DataGridViewCellFormattingEventArgs e_704ILR)
        {
            if (e_704ILR.RowIndex < 0 || e_704ILR.RowIndex >= _respaldos_704ILR.Count) return;
            var r_704ILR = _respaldos_704ILR[e_704ILR.RowIndex];
            string col_704ILR = _grid_704ILR.Columns[e_704ILR.ColumnIndex].Name;
            if (col_704ILR == "cMotivo")
            {
                e_704ILR.Value = TextoMotivo_704ILR(r_704ILR);
                e_704ILR.FormattingApplied = true;
            }
            else if (col_704ILR == "cTamanio")
            {
                e_704ILR.Value = BLL_Respaldo_704ILR.Tamanio_704ILR(r_704ILR.Tamanio_704ILR);
                e_704ILR.FormattingApplied = true;
            }
            if (!r_704ILR.Legible_704ILR) e_704ILR.CellStyle.ForeColor = Theme_704ILR.TextMuted_704ILR;
        }

        private static string TextoMotivo_704ILR(BE_Respaldo_704ILR r_704ILR)
        {
            if (!r_704ILR.Legible_704ILR) return T_704ILR("RSP_ILEGIBLE", "(no se puede leer: no es un respaldo de base de datos)");
            MotivoRespaldo_704ILR? motivo_704ILR = BLL_Respaldo_704ILR.MotivoDe_704ILR(r_704ILR);
            switch (motivo_704ILR)
            {
                case MotivoRespaldo_704ILR.MANUAL: return T_704ILR("RSP_MOTIVO_MANUAL", "Respaldo hecho desde el sistema");
                case MotivoRespaldo_704ILR.ANTES_DE_RESTAURAR: return T_704ILR("RSP_MOTIVO_ANTES_DE_RESTAURAR", "Automático, antes de restaurar otro respaldo");
                case MotivoRespaldo_704ILR.ANTES_DE_ALTERAR: return T_704ILR("RSP_MOTIVO_ANTES_DE_ALTERAR", "Antes de alterar los datos (prueba de integridad)");
                default: return string.IsNullOrWhiteSpace(r_704ILR.Nombre_704ILR) ? T_704ILR("RSP_MOTIVO_EXTERNO", "Respaldo agregado desde otra carpeta") : r_704ILR.Nombre_704ILR;
            }
        }

        // ------------------------------------------------------------------ acciones

        private void Crear_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Respaldo_704ILR.PermisoCrear_704ILR, FindForm(), "crear un respaldo de la base")) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                BE_Respaldo_704ILR r_704ILR = BLL_Respaldo_704ILR.Crear_704ILR();
                Cargar_704ILR();
                Seleccionar_704ILR(r_704ILR.Ruta_704ILR);
                Cursor = Cursors.Default;
                MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("RSP_CREADO", "Respaldo creado: {0} ({1}).", r_704ILR.Archivo_704ILR,
                    BLL_Respaldo_704ILR.Tamanio_704ILR(r_704ILR.Tamanio_704ILR)), "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex_704ILR)
            {
                Cursor = Cursors.Default;
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Respaldos", "Creacion de un respaldo");
                MessageBox.Show(FindForm(), MensajeFalla_704ILR(ex_704ILR), "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void Agregar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Respaldo_704ILR.PermisoRestaurar_704ILR, FindForm(), "agregar un respaldo")) return;
            using (var dlg_704ILR = new OpenFileDialog
            {
                Filter = T_704ILR("RSP_FILTRO", "Respaldo de SQL Server (*.bak)") + "|*.bak",
                CheckFileExists = true,
                Multiselect = false
            })
            {
                if (dlg_704ILR.ShowDialog(FindForm()) != DialogResult.OK) return;
                Cursor = Cursors.WaitCursor;
                try
                {
                    BE_Respaldo_704ILR r_704ILR = BLL_Respaldo_704ILR.Incorporar_704ILR(dlg_704ILR.FileName);
                    Cargar_704ILR();
                    Cursor = Cursors.Default;
                    if (!r_704ILR.Legible_704ILR)
                    {
                        MessageBox.Show(FindForm(), T_704ILR("RSP_NO_ES_RESPALDO", "El archivo elegido no es un respaldo de base de datos que se pueda restaurar.") + "\n\n" + r_704ILR.Detalle_704ILR,
                            "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Seleccionar_704ILR(r_704ILR.Ruta_704ILR);
                }
                catch (Exception ex_704ILR)
                {
                    Cursor = Cursors.Default;
                    BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Respaldos", "Agregar un respaldo");
                    MessageBox.Show(FindForm(), MensajeFalla_704ILR(ex_704ILR), "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally { Cursor = Cursors.Default; }
            }
        }

        private void Seleccionar_704ILR(string ruta_704ILR)
        {
            for (int i_704ILR = 0; i_704ILR < _respaldos_704ILR.Count; i_704ILR++)
                if (string.Equals(_respaldos_704ILR[i_704ILR].Ruta_704ILR, ruta_704ILR, StringComparison.OrdinalIgnoreCase))
                {
                    _grid_704ILR.CurrentCell = _grid_704ILR.Rows[i_704ILR].Cells[0];
                    _grid_704ILR.Rows[i_704ILR].Selected = true;
                    break;
                }
            ActualizarBotones_704ILR();
        }

        private void RestaurarElegido_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Respaldo_704ILR.PermisoRestaurar_704ILR, FindForm(), "restaurar un respaldo de la base")) return;
            BE_Respaldo_704ILR r_704ILR = Elegido_704ILR;
            if (r_704ILR == null || !r_704ILR.Legible_704ILR) return;

            // Reemplaza todos los datos actuales: la pregunta arranca en No.
            var confirma_704ILR = MessageBox.Show(FindForm(),
                Tr_704ILR.F_704ILR("RSP_CONFIRMA", "¿Restaurar el respaldo del {0}?",
                    r_704ILR.Fecha_704ILR.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)) + "\n\n" +
                T_704ILR("RSP_CONFIRMA_NOTA", "Todos los datos de la base pasan a ser los de ese momento: lo registrado después se pierde. Antes se guarda un respaldo de la base actual, y el sistema se reinicia al terminar."),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirma_704ILR != DialogResult.Yes) return;

            ResultadoRestauracion_704ILR resultado_704ILR;
            UseWaitCursor = true;
            Enabled = false;
            Application.DoEvents();
            try { resultado_704ILR = BLL_Respaldo_704ILR.Restaurar_704ILR(r_704ILR.Ruta_704ILR); }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Respaldos", "Restauracion de un respaldo");
                resultado_704ILR = null;
                MessageBox.Show(FindForm(), MensajeFalla_704ILR(ex_704ILR), "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Enabled = true;
                UseWaitCursor = false;
            }
            if (resultado_704ILR == null) return;

            if (resultado_704ILR.Resultado_704ILR == RestauracionResult_704ILR.Restaurada_704ILR)
            {
                MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("RSP_RESTAURADO", "La base de datos volvió al respaldo del {0}.",
                    r_704ILR.Fecha_704ILR.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)) + "\n\n" +
                    Tr_704ILR.F_704ILR("RSP_RESTAURADO_NOTA", "La base que había antes quedó guardada en el respaldo {0}. El sistema se reinicia para trabajar con los datos restaurados.",
                    System.IO.Path.GetFileName(resultado_704ILR.RespaldoPrevio_704ILR)),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Restaurado_704ILR?.Invoke(this, resultado_704ILR);
                return;
            }

            MessageBox.Show(FindForm(), TextoRechazo_704ILR(resultado_704ILR), "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Cargar_704ILR();
        }

        private static string TextoRechazo_704ILR(ResultadoRestauracion_704ILR r_704ILR)
        {
            string texto_704ILR;
            switch (r_704ILR.Resultado_704ILR)
            {
                case RestauracionResult_704ILR.ArchivoInvalido_704ILR:
                    texto_704ILR = T_704ILR("RSP_ERR_ARCHIVO", "El archivo no es un respaldo completo de base de datos que se pueda restaurar.");
                    break;
                case RestauracionResult_704ILR.VersionPosterior_704ILR:
                    texto_704ILR = T_704ILR("RSP_ERR_VERSION", "El respaldo lo generó una versión de SQL Server posterior a la de esta instancia: no se puede restaurar acá.");
                    break;
                case RestauracionResult_704ILR.SinRespaldoPrevio_704ILR:
                    texto_704ILR = T_704ILR("RSP_ERR_SIN_PREVIO", "No se pudo guardar el respaldo de la base actual, así que no se restauró nada: la base sigue como estaba.");
                    break;
                case RestauracionResult_704ILR.NoEsDelSistema_704ILR:
                    texto_704ILR = T_704ILR("RSP_ERR_NO_ES_DEL_SISTEMA", "El respaldo no tiene la base de datos de esta versión del sistema.");
                    break;
                default:
                    texto_704ILR = T_704ILR("RSP_ERR_FALLIDA", "SQL Server no pudo restaurar el respaldo.");
                    break;
            }
            if (r_704ILR.Resultado_704ILR == RestauracionResult_704ILR.NoEsDelSistema_704ILR || r_704ILR.Resultado_704ILR == RestauracionResult_704ILR.Fallida_704ILR)
                texto_704ILR += "\n\n" + (r_704ILR.BaseRepuesta_704ILR
                    ? T_704ILR("RSP_BASE_REPUESTA", "La base volvió a como estaba antes de intentarlo.")
                    : Tr_704ILR.F_704ILR("RSP_BASE_NO_REPUESTA", "ATENCIÓN: la base no pudo volver sola a como estaba. El respaldo de la base anterior quedó en {0}.", r_704ILR.RespaldoPrevio_704ILR));
            if (!string.IsNullOrWhiteSpace(r_704ILR.Detalle_704ILR))
                texto_704ILR += "\n\n" + T_704ILR("RSP_DETALLE", "Detalle informado por SQL Server:") + "\n" + r_704ILR.Detalle_704ILR;
            return texto_704ILR;
        }

        // Una instancia remota, una carpeta sin permiso para el servicio de SQL Server: el
        // detalle tecnico va a la bitacora y en pantalla, el motivo en el idioma activo.
        private static string MensajeFalla_704ILR(Exception ex_704ILR)
        {
            if (ex_704ILR is RespaldoNoDisponibleException_704ILR remota_704ILR)
                return Tr_704ILR.F_704ILR("RSP_ERR_REMOTA", "La instancia de SQL Server está en otro equipo ({0}): los respaldos se hacen desde ese equipo.", remota_704ILR.Equipo_704ILR);
            if (ex_704ILR is UnauthorizedAccessException || ex_704ILR is System.IO.IOException)
                return Tr_704ILR.F_704ILR("RSP_ERR_CARPETA", "No se pudo usar la carpeta de respaldos ({0}).", BLL_Respaldo_704ILR.Carpeta_704ILR) + "\n\n" + ex_704ILR.Message;
            if (ex_704ILR is Microsoft.Data.SqlClient.SqlException sql_704ILR)
                return T_704ILR("RSP_ERR_SQL", "SQL Server no pudo completar el respaldo.") + "\n\n" +
                       T_704ILR("RSP_DETALLE", "Detalle informado por SQL Server:") + "\n" + sql_704ILR.Message;
            return Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR);
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            if (_grid_704ILR.Columns.Count >= 5)
            {
                _grid_704ILR.Columns["cFecha"].HeaderText = T_704ILR("COL_FECHA", "Fecha");
                _grid_704ILR.Columns["cMotivo"].HeaderText = T_704ILR("RSP_COL_MOTIVO", "Motivo");
                _grid_704ILR.Columns["cBase"].HeaderText = T_704ILR("RSP_COL_BASE", "Base de origen");
                _grid_704ILR.Columns["cTamanio"].HeaderText = T_704ILR("RSP_COL_TAMANIO", "Tamaño");
                _grid_704ILR.Columns["cArchivo"].HeaderText = T_704ILR("RSP_COL_ARCHIVO", "Archivo");
                UiGrid_704ILR.EncabezadosEnteros_704ILR(_grid_704ILR);
            }
            ActualizarEstado_704ILR();
            _grid_704ILR.Invalidate();
        }
    }
}
