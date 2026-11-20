using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // Registro de excepciones (A03, CUS003), pestana de Auditoria. Lista los registros
    // serializados del equipo (uno por archivo XML), muestra el detalle de cada uno,
    // exporta un lote para quien da soporte y abre un lote exportado en otro equipo.
    // Mismo diseno que la bitacora: titulo, tarjeta de acciones, estado y grilla.
    public class ucExcepciones_704ILR : UserControl, IObservadorIdioma_704ILR, IAyudaContextual_704ILR
    {
        private DataGridView _grid_704ILR;
        private TextBox _txtDetalle_704ILR;
        private Label _lblOrigen_704ILR, _lblEstado_704ILR;
        private AppButton_704ILR _btnActualizar_704ILR, _btnExportar_704ILR, _btnAbrir_704ILR, _btnLocal_704ILR;

        // Lo que muestra la grilla: el registro local del equipo o un lote abierto.
        private List<BE_RegistroExcepcion_704ILR> _registros_704ILR = new List<BE_RegistroExcepcion_704ILR>();
        private BE_LoteExcepciones_704ILR _lote_704ILR;
        private string _archivoLote_704ILR;
        private Exception _error_704ILR;

        public ucExcepciones_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) =>
            {
                CargarLocal_704ILR();
                GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
                ActualizarTextos_704ILR();
            };
            Disposed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        // Tema de la ayuda en linea de esta pantalla (F1).
        public string TemaAyuda_704ILR => Ayuda_704ILR.ApartadoAuditoriaExcepciones_704ILR;

        private void BuildUi_704ILR()
        {
            var root_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.Transparent };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // titulo
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // acciones
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // estado
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 58));    // grilla
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 42));    // detalle

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Registro de excepciones");
            lblTitle_704ILR.Tag = "T:EXC_TITULO";
            lblTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill, AutoSize = true,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR),
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceMd_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceMd_704ILR)
            };
            var flow_704ILR = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, BackColor = Color.Transparent };
            _btnActualizar_704ILR = Boton_704ILR(Ui_704ILR.Primary_704ILR("Actualizar", Theme_704ILR.IcoSearch_704ILR), "EXC_BTN_ACTUALIZAR", 130, CargarLocal_704ILR);
            _btnExportar_704ILR = Boton_704ILR(Ui_704ILR.Secondary_704ILR("Exportar lote..."), "EXC_BTN_EXPORTAR", 160, Exportar_704ILR);
            _btnAbrir_704ILR = Boton_704ILR(Ui_704ILR.Secondary_704ILR("Abrir lote..."), "EXC_BTN_ABRIR", 150, AbrirLote_704ILR);
            _btnLocal_704ILR = Boton_704ILR(Ui_704ILR.Secondary_704ILR("Volver al registro del equipo"), "EXC_BTN_LOCAL", 240, CargarLocal_704ILR);
            _btnLocal_704ILR.Visible = false;
            flow_704ILR.Controls.AddRange(new Control[] { _btnActualizar_704ILR, _btnExportar_704ILR, _btnAbrir_704ILR, _btnLocal_704ILR });
            card_704ILR.Controls.Add(flow_704ILR);

            var estado_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR) };
            _lblOrigen_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontBodyBold_704ILR, ForeColor = Theme_704ILR.TextOnLight_704ILR, BackColor = Color.Transparent, MaximumSize = new Size(1000, 0) };
            _lblEstado_704ILR = new Label { AutoSize = true, Font = Theme_704ILR.FontSmall_704ILR, ForeColor = Theme_704ILR.TextMuted_704ILR, BackColor = Color.Transparent, MaximumSize = new Size(1000, 0) };
            estado_704ILR.Controls.Add(_lblOrigen_704ILR, 0, 0);
            estado_704ILR.Controls.Add(_lblEstado_704ILR, 0, 1);

            var cardGrid_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFecha", DataPropertyName = "Fecha_704ILR", FillWeight = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss", FormatProvider = CultureInfo.InvariantCulture } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cUsuario", DataPropertyName = "Usuario_704ILR", FillWeight = 45 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cModulo", DataPropertyName = "Modulo_704ILR", FillWeight = 45 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cContexto", DataPropertyName = "Contexto_704ILR", FillWeight = 80 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTipo", DataPropertyName = "Tipo_704ILR", FillWeight = 80 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMensaje", DataPropertyName = "Mensaje_704ILR", FillWeight = 160 });
            _grid_704ILR.CellFormatting += Grid_CellFormatting_704ILR;
            _grid_704ILR.SelectionChanged += (s_704ILR, e_704ILR) => MostrarDetalle_704ILR();
            cardGrid_704ILR.Controls.Add(_grid_704ILR);

            var cardDetalle_704ILR = new CardPanel_704ILR { Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(Theme_704ILR.SpaceSm_704ILR) };
            _txtDetalle_704ILR = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = new Font("Consolas", 9f), BackColor = Theme_704ILR.BgContent_704ILR, BorderStyle = BorderStyle.None
            };
            cardDetalle_704ILR.Controls.Add(_txtDetalle_704ILR);

            root_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            root_704ILR.Controls.Add(card_704ILR, 0, 1);
            root_704ILR.Controls.Add(estado_704ILR, 0, 2);
            root_704ILR.Controls.Add(cardGrid_704ILR, 0, 3);
            root_704ILR.Controls.Add(cardDetalle_704ILR, 0, 4);
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

        // Registro local del equipo. Segunda capa del control de acceso: la vista carga sola
        // al abrirse, asi que el permiso se exige aca.
        private void CargarLocal_704ILR()
        {
            _lote_704ILR = null;
            _archivoLote_704ILR = null;
            _error_704ILR = null;
            if (!Permisos_704ILR.Tiene_704ILR(BLL_Excepciones_704ILR.Permiso_704ILR))
            {
                _registros_704ILR = new List<BE_RegistroExcepcion_704ILR>();
                Enlazar_704ILR();
                _lblEstado_704ILR.Text = T_704ILR("MSG_SIN_PERMISO", "No tenés permiso para realizar esta acción.");
                return;
            }
            try { _registros_704ILR = BLL_Excepciones_704ILR.Listar_704ILR(); }
            catch (Exception ex_704ILR)
            {
                _error_704ILR = ex_704ILR;
                _registros_704ILR = new List<BE_RegistroExcepcion_704ILR>();
            }
            Enlazar_704ILR();
        }

        private void Enlazar_704ILR()
        {
            _grid_704ILR.DataSource = null;
            _grid_704ILR.DataSource = _registros_704ILR;
            _btnLocal_704ILR.Visible = _lote_704ILR != null;
            _btnExportar_704ILR.Enabled = _lote_704ILR == null && _registros_704ILR.Any(r_704ILR => !r_704ILR.Ilegible_704ILR);
            ActualizarEstado_704ILR();
            MostrarDetalle_704ILR();
        }

        private void ActualizarEstado_704ILR()
        {
            if (_lote_704ILR != null)
            {
                _lblOrigen_704ILR.Text = Tr_704ILR.F_704ILR("EXC_ORIGEN_LOTE", "Lote abierto: {0} (generado el {1} en el equipo {2}, versión {3})",
                    Path.GetFileName(_archivoLote_704ILR), _lote_704ILR.Generado_704ILR.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    _lote_704ILR.Equipo_704ILR, _lote_704ILR.Version_704ILR);
            }
            else
            {
                _lblOrigen_704ILR.Text = Tr_704ILR.F_704ILR("EXC_ORIGEN_LOCAL", "Registro de este equipo ({0})", BLL_Excepciones_704ILR.Carpeta_704ILR);
            }
            int ilegibles_704ILR = _registros_704ILR.Count(r_704ILR => r_704ILR.Ilegible_704ILR);
            if (_error_704ILR != null)
                _lblEstado_704ILR.Text = Tr_704ILR.MensajeExcepcion_704ILR(_error_704ILR);
            else if (_registros_704ILR.Count == 0)
                _lblEstado_704ILR.Text = T_704ILR("EXC_SIN_REGISTROS", "No hay excepciones registradas.");
            else
                _lblEstado_704ILR.Text = Tr_704ILR.F_704ILR("EXC_CONTEO", "{0} registro(s), {1} ilegible(s).", _registros_704ILR.Count, ilegibles_704ILR);
        }

        private BE_RegistroExcepcion_704ILR Seleccionado_704ILR() =>
            _grid_704ILR.CurrentRow?.DataBoundItem as BE_RegistroExcepcion_704ILR;

        private void MostrarDetalle_704ILR()
        {
            var r_704ILR = Seleccionado_704ILR();
            if (r_704ILR == null) { _txtDetalle_704ILR.Text = string.Empty; return; }
            if (r_704ILR.Ilegible_704ILR)
            {
                _txtDetalle_704ILR.Text = Tr_704ILR.F_704ILR("EXC_ILEGIBLE_DETALLE", "El archivo {0} no se pudo leer: está dañado o no es un registro de excepción.", r_704ILR.Archivo_704ILR);
                return;
            }
            var sb_704ILR = new StringBuilder();
            void Linea_704ILR(string clave_704ILR, string defecto_704ILR, string valor_704ILR) =>
                sb_704ILR.Append(T_704ILR(clave_704ILR, defecto_704ILR)).Append(": ").AppendLine(valor_704ILR);
            Linea_704ILR("COL_FECHA", "Fecha", r_704ILR.Fecha_704ILR.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Linea_704ILR("COL_USUARIO", "Usuario", r_704ILR.Usuario_704ILR);
            Linea_704ILR("EXC_EQUIPO", "Equipo", r_704ILR.Equipo_704ILR);
            Linea_704ILR("EXC_VERSION", "Versión", r_704ILR.Version_704ILR);
            Linea_704ILR("COL_MODULO", "Módulo", r_704ILR.Modulo_704ILR);
            Linea_704ILR("EXC_CONTEXTO", "Contexto", r_704ILR.Contexto_704ILR);
            Linea_704ILR("EXC_TIPO", "Tipo", r_704ILR.Tipo_704ILR);
            Linea_704ILR("EXC_MENSAJE", "Mensaje", r_704ILR.Mensaje_704ILR);
            if (!string.IsNullOrEmpty(r_704ILR.Origen_704ILR)) Linea_704ILR("EXC_ORIGEN", "Origen", r_704ILR.Origen_704ILR);
            foreach (var i_704ILR in r_704ILR.Internas_704ILR ?? new List<BE_ExcepcionInterna_704ILR>())
                Linea_704ILR("EXC_INTERNA", "Excepción interna", i_704ILR.Tipo_704ILR + ": " + i_704ILR.Mensaje_704ILR);
            if (!string.IsNullOrEmpty(r_704ILR.Archivo_704ILR)) Linea_704ILR("EXC_ARCHIVO", "Archivo", r_704ILR.Archivo_704ILR);
            sb_704ILR.AppendLine();
            sb_704ILR.AppendLine(T_704ILR("EXC_PILA", "Pila de llamadas") + ":");
            sb_704ILR.Append((r_704ILR.Pila_704ILR ?? string.Empty).Replace("\n", Environment.NewLine).Replace("\r" + Environment.NewLine, Environment.NewLine));
            _txtDetalle_704ILR.Text = sb_704ILR.ToString();
        }

        private void Grid_CellFormatting_704ILR(object sender_704ILR, DataGridViewCellFormattingEventArgs e_704ILR)
        {
            if (e_704ILR.RowIndex < 0 || e_704ILR.RowIndex >= _registros_704ILR.Count) return;
            var r_704ILR = _grid_704ILR.Rows[e_704ILR.RowIndex].DataBoundItem as BE_RegistroExcepcion_704ILR;
            if (r_704ILR == null || !r_704ILR.Ilegible_704ILR) return;
            if (_grid_704ILR.Columns[e_704ILR.ColumnIndex].Name == "cMensaje")
            {
                e_704ILR.Value = T_704ILR("EXC_ILEGIBLE", "(archivo ilegible)");
                e_704ILR.FormattingApplied = true;
            }
            e_704ILR.CellStyle.ForeColor = Theme_704ILR.Error_704ILR;
        }

        private void Exportar_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Excepciones_704ILR.Permiso_704ILR, FindForm(), "exportar el registro de excepciones")) return;
            var legibles_704ILR = _registros_704ILR.Where(r_704ILR => !r_704ILR.Ilegible_704ILR).ToList();
            if (legibles_704ILR.Count == 0) return;
            using var dlg_704ILR = new SaveFileDialog
            {
                Filter = T_704ILR("EXC_FILTRO", "Lote de excepciones (*.xml)") + "|*.xml",
                FileName = "excepciones_" + Environment.MachineName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".xml",
                OverwritePrompt = true
            };
            if (dlg_704ILR.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                int n_704ILR = BLL_Excepciones_704ILR.Exportar_704ILR(legibles_704ILR, dlg_704ILR.FileName);
                MessageBox.Show(FindForm(), Tr_704ILR.F_704ILR("EXC_EXPORTADO", "Se exportaron {0} registro(s) a {1}.", n_704ILR, dlg_704ILR.FileName),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Excepciones", "Exportacion del lote");
                MessageBox.Show(FindForm(), Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), T_704ILR("MSG_ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AbrirLote_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR(BLL_Excepciones_704ILR.Permiso_704ILR, FindForm(), "abrir un lote de excepciones")) return;
            using var dlg_704ILR = new OpenFileDialog { Filter = T_704ILR("EXC_FILTRO", "Lote de excepciones (*.xml)") + "|*.xml", CheckFileExists = true };
            if (dlg_704ILR.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                var lote_704ILR = BLL_Excepciones_704ILR.AbrirLote_704ILR(dlg_704ILR.FileName);
                _lote_704ILR = lote_704ILR;
                _archivoLote_704ILR = dlg_704ILR.FileName;
                _error_704ILR = null;
                _registros_704ILR = lote_704ILR.Registros_704ILR.OrderByDescending(r_704ILR => r_704ILR.Fecha_704ILR).ToList();
                Enlazar_704ILR();
            }
            catch (InvalidDataException)
            {
                MessageBox.Show(FindForm(), T_704ILR("EXC_LOTE_INVALIDO", "El archivo elegido no es un lote de excepciones válido."),
                    "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Excepciones", "Apertura de un lote");
                MessageBox.Show(FindForm(), Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), T_704ILR("MSG_ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string T_704ILR(string clave_704ILR, string defecto_704ILR = null)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? (defecto_704ILR ?? clave_704ILR) : t_704ILR;
        }

        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            if (_grid_704ILR.Columns.Count >= 6)
            {
                _grid_704ILR.Columns["cFecha"].HeaderText = T_704ILR("COL_FECHA", "Fecha");
                _grid_704ILR.Columns["cUsuario"].HeaderText = T_704ILR("COL_USUARIO", "Usuario");
                _grid_704ILR.Columns["cModulo"].HeaderText = T_704ILR("COL_MODULO", "Módulo");
                _grid_704ILR.Columns["cContexto"].HeaderText = T_704ILR("EXC_CONTEXTO", "Contexto");
                _grid_704ILR.Columns["cTipo"].HeaderText = T_704ILR("EXC_TIPO", "Tipo");
                _grid_704ILR.Columns["cMensaje"].HeaderText = T_704ILR("EXC_MENSAJE", "Mensaje");
            }
            ActualizarEstado_704ILR();
            MostrarDetalle_704ILR();
            _grid_704ILR.Invalidate();
        }
    }
}
