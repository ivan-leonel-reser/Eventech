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
    // Ventana modal que muestra el historial de cambios (control de cambios) de
    // una reserva puntual, campo por campo y en orden cronologico.
    // Borderless heredando de FormBase (cromo compartido), con barra de titulo de
    // marca y la grilla agrupada en una tarjeta. Implementa el Observer de idioma
    // para re-traducir titulo, encabezados y estado vacio sin recrear la vista.
    public class frmHistorialReserva_704ILR : FormBase_704ILR, IObservadorIdioma_704ILR
    {
        // Tema de la ayuda en linea de esta ventana (F1).
        protected override string TemaAyuda_704ILR => Ayuda_704ILR.TemaHistorial_704ILR;

        private readonly int _reservaId_704ILR;

        private Label _lblTitle_704ILR;
        private DataGridView _grid_704ILR;
        private Label _lblVacio_704ILR;
        private DataGridViewTextBoxColumn _colFecha_704ILR, _colUsuario_704ILR, _colCampo_704ILR, _colAnterior_704ILR, _colNuevo_704ILR;

        // Nombres de los clientes y salones cuyos ids aparecen en el historial. Se
        // resuelven una vez al cargar: la grilla no consulta la base al pintar.
        private readonly Dictionary<int, string> _clientes_704ILR = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _salones_704ILR = new Dictionary<int, string>();

        public frmHistorialReserva_704ILR(int reservaId_704ILR)
        {
            _reservaId_704ILR = reservaId_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => CargarHistorial_704ILR();
            GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this);
            FormClosed += (s_704ILR, e_704ILR) => GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            ClientSize = new Size(680, 460);
            BackColor = Theme_704ILR.BgContent_704ILR;

            // ---------------- Barra de titulo ----------------
            var pnlTop_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTop_704ILR);

            _lblTitle_704ILR = new Label
            {
                Font = Theme_704ILR.FontH2_704ILR,
                ForeColor = Theme_704ILR.TextOnDark_704ILR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0),
                BackColor = Color.Transparent
            };
            EnableDrag_704ILR(_lblTitle_704ILR);

            var btnClose_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => Close(), danger_704ILR: true);
            btnClose_704ILR.Dock = DockStyle.Right;

            pnlTop_704ILR.Controls.Add(_lblTitle_704ILR);
            pnlTop_704ILR.Controls.Add(btnClose_704ILR);

            // ---------------- Contenido (tarjeta con grilla) ----------------
            var pnlContent_704ILR = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };

            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                BehindColor_704ILR = Theme_704ILR.BgContent_704ILR,
                Padding = new Padding(Theme_704ILR.SpaceSm_704ILR)
            };

            _grid_704ILR = new DataGridView
            {
                Name = "grid",
                Dock = DockStyle.Fill,
                BackgroundColor = Theme_704ILR.Surface_704ILR
            };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);

            // Pesos pensados para lo que se muestra: el nombre del campo traducido
            // ("Fecha del evento", "Data do evento") y valores legibles (nombres de
            // cliente y salon, importes con separadores).
            // La fecha se muestra en gregoriano con separadores invariantes, como en la bitacora y en
            // el Detalle de sus asientos: el patron solo fija el orden, y con la cultura de la
            // estacion th-TH mostraba 2569 y fi-FI 01.14.
            _colFecha_704ILR = new DataGridViewTextBoxColumn
            {
                Name = "Fecha",
                DataPropertyName = "Fecha_704ILR",
                FillWeight = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm", FormatProvider = CultureInfo.InvariantCulture }
            };
            _colUsuario_704ILR  = new DataGridViewTextBoxColumn { Name = "Usuario",       DataPropertyName = "Usuario_704ILR",       FillWeight = 50 };
            _colCampo_704ILR    = new DataGridViewTextBoxColumn { Name = "NombreCampo",   DataPropertyName = "NombreCampo_704ILR",   FillWeight = 70 };
            _colAnterior_704ILR = new DataGridViewTextBoxColumn { Name = "ValorAnterior", DataPropertyName = "ValorAnterior_704ILR", FillWeight = 75 };
            _colNuevo_704ILR    = new DataGridViewTextBoxColumn { Name = "ValorNuevo",    DataPropertyName = "ValorNuevo_704ILR",    FillWeight = 75 };
            _grid_704ILR.Columns.AddRange(_colFecha_704ILR, _colUsuario_704ILR, _colCampo_704ILR, _colAnterior_704ILR, _colNuevo_704ILR);
            _grid_704ILR.CellFormatting += Grid_CellFormatting_704ILR;

            // Estado vacio: centrado sobre la grilla, visible solo si no hay filas.
            _lblVacio_704ILR = new Label
            {
                Dock = DockStyle.Fill,
                Font = Theme_704ILR.FontBody_704ILR,
                ForeColor = Theme_704ILR.TextMuted_704ILR,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme_704ILR.Surface_704ILR,
                Visible = false
            };

            // El label de estado vacio se agrega despues del grid para quedar al
            // frente cuando se muestra (oculta la grilla sin filas).
            card_704ILR.Controls.Add(_lblVacio_704ILR);
            card_704ILR.Controls.Add(_grid_704ILR);

            pnlContent_704ILR.Controls.Add(card_704ILR);

            Controls.Add(pnlContent_704ILR);
            Controls.Add(pnlTop_704ILR);
        }

        private void CargarHistorial_704ILR()
        {
            try
            {
                List<BE_CambioEntry_704ILR> data_704ILR = RegistradorDeCambios_704ILR.GetHistorial_704ILR("Reserva", _reservaId_704ILR);
                ResolverNombres_704ILR(data_704ILR);
                _grid_704ILR.DataSource = data_704ILR;
                ActualizarEstadoVacio_704ILR(data_704ILR == null || data_704ILR.Count == 0);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "HistorialReserva", "Cargar historial de cambios");
                ActualizarEstadoVacio_704ILR(true);
                MessageBox.Show(Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR), Tr_704ILR.T_704ILR("MSG_ERROR"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Carga los nombres de los clientes y salones referidos por el historial. Si
        // la consulta falla el historial se muestra igual, con los ids guardados.
        private void ResolverNombres_704ILR(List<BE_CambioEntry_704ILR> data_704ILR)
        {
            _clientes_704ILR.Clear();
            _salones_704ILR.Clear();
            if (data_704ILR == null) return;
            try
            {
                foreach (int id_704ILR in IdsDelCampo_704ILR(data_704ILR, "ClienteId"))
                {
                    var cliente_704ILR = BLL_Cliente_704ILR.GetById_704ILR(id_704ILR);
                    if (cliente_704ILR != null) _clientes_704ILR[id_704ILR] = cliente_704ILR.NombreCompleto_704ILR;
                }
                if (IdsDelCampo_704ILR(data_704ILR, "SalonId").Count > 0)
                    foreach (var salon_704ILR in BLL_Salon_704ILR.GetAll_704ILR())
                        _salones_704ILR[salon_704ILR.Id_704ILR] = salon_704ILR.Nombre_704ILR;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "HistorialReserva", "Resolver nombres del historial");
            }
        }

        private static HashSet<int> IdsDelCampo_704ILR(List<BE_CambioEntry_704ILR> data_704ILR, string campo_704ILR)
        {
            var ids_704ILR = new HashSet<int>();
            foreach (var cambio_704ILR in data_704ILR)
            {
                if (cambio_704ILR.NombreCampo_704ILR != campo_704ILR) continue;
                if (int.TryParse(cambio_704ILR.ValorAnterior_704ILR, NumberStyles.None, CultureInfo.InvariantCulture, out int anterior_704ILR)) ids_704ILR.Add(anterior_704ILR);
                if (int.TryParse(cambio_704ILR.ValorNuevo_704ILR, NumberStyles.None, CultureInfo.InvariantCulture, out int nuevo_704ILR)) ids_704ILR.Add(nuevo_704ILR);
            }
            return ids_704ILR;
        }

        // HistorialCambios guarda el nombre logico del campo y el valor en formato
        // invariante (es dato: asi compara el control de cambios). Lo que se traduce y
        // formatea es lo que se MUESTRA, como en las otras grillas: campo traducido,
        // estado traducido, cliente y salon por nombre, importe con separadores y
        // fecha del evento sin hora. Un valor que no se reconoce (dato alterado, campo
        // nuevo) se muestra tal como esta guardado.
        private void Grid_CellFormatting_704ILR(object sender_704ILR, DataGridViewCellFormattingEventArgs e_704ILR)
        {
            if (e_704ILR.RowIndex < 0 || e_704ILR.ColumnIndex < 0 || e_704ILR.ColumnIndex >= _grid_704ILR.Columns.Count) return;
            if (!(_grid_704ILR.Rows[e_704ILR.RowIndex].DataBoundItem is BE_CambioEntry_704ILR cambio_704ILR)) return;

            string columna_704ILR = _grid_704ILR.Columns[e_704ILR.ColumnIndex].Name;
            string texto_704ILR = null;
            if (columna_704ILR == "NombreCampo")
                texto_704ILR = TextoCampo_704ILR(cambio_704ILR.NombreCampo_704ILR);
            else if (columna_704ILR == "ValorAnterior" || columna_704ILR == "ValorNuevo")
                texto_704ILR = TextoValor_704ILR(cambio_704ILR.NombreCampo_704ILR, e_704ILR.Value as string);

            if (texto_704ILR != null)
            {
                e_704ILR.Value = texto_704ILR;
                e_704ILR.FormattingApplied = true;
            }
        }

        // Nombre visible de un campo auditado de la reserva (mismas claves que los
        // encabezados de Reservas y Versiones). Null si el campo no se conoce.
        private static string TextoCampo_704ILR(string campo_704ILR)
        {
            switch (campo_704ILR)
            {
                case "ClienteId":         return T_704ILR("COL_CLIENTE", "Cliente");
                case "SalonId":           return T_704ILR("COL_SALON", "Salón");
                case "FechaEvento":       return T_704ILR("RES_LBL_FECHA", "Fecha del evento");
                case "Estado":            return T_704ILR("COL_ESTADO", "Estado");
                case "Monto":             return T_704ILR("COL_MONTO", "Monto");
                case "CantidadInvitados": return T_704ILR("COL_INVITADOS", "Invitados");
                default:                  return null;
            }
        }

        // Valor visible de un campo auditado. Null si el valor no se reconoce.
        private string TextoValor_704ILR(string campo_704ILR, string valor_704ILR)
        {
            if (string.IsNullOrEmpty(valor_704ILR)) return null;
            switch (campo_704ILR)
            {
                case "Estado":
                    // Solo el nombre exacto de un estado: un "3" no se toma por el enum.
                    return Enum.IsDefined(typeof(EstadoReserva_704ILR), valor_704ILR)
                        ? Tr_704ILR.Estado_704ILR((EstadoReserva_704ILR)Enum.Parse(typeof(EstadoReserva_704ILR), valor_704ILR))
                        : null;
                case "Monto":
                    return decimal.TryParse(valor_704ILR, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal monto_704ILR)
                        ? monto_704ILR.ToString("N2")
                        : null;
                case "FechaEvento":
                    // Gregoriano con separadores invariantes, como la columna Fecha.
                    return DateTime.TryParseExact(valor_704ILR, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha_704ILR)
                        ? fecha_704ILR.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : null;
                case "ClienteId":
                    return NombrePorId_704ILR(_clientes_704ILR, valor_704ILR);
                case "SalonId":
                    return NombrePorId_704ILR(_salones_704ILR, valor_704ILR);
                default:
                    return null;
            }
        }

        private static string NombrePorId_704ILR(Dictionary<int, string> nombres_704ILR, string valor_704ILR)
            => int.TryParse(valor_704ILR, NumberStyles.None, CultureInfo.InvariantCulture, out int id_704ILR)
               && nombres_704ILR.TryGetValue(id_704ILR, out string nombre_704ILR)
               && !string.IsNullOrWhiteSpace(nombre_704ILR)
                ? nombre_704ILR
                : null;

        // Alterna el estado vacio: muestra el mensaje y oculta la grilla cuando no
        // hay registros de cambios para la reserva.
        private void ActualizarEstadoVacio_704ILR(bool vacio_704ILR)
        {
            _lblVacio_704ILR.Visible = vacio_704ILR;
            _grid_704ILR.Visible = !vacio_704ILR;
        }

        // Devuelve la traduccion de 'clave' o, si falta, el texto por defecto dado.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        // Observador (patron Observer): re-traduce titulo, encabezados, estado vacio
        // y los valores de la grilla (nombre de campo y estado).
        public void ActualizarTextos_704ILR()
        {
            if (_lblTitle_704ILR != null) _lblTitle_704ILR.Text = Tr_704ILR.T_704ILR("HIST_TITULO") + " #" + _reservaId_704ILR;
            if (_colFecha_704ILR != null)
            {
                _colFecha_704ILR.HeaderText    = Tr_704ILR.T_704ILR("COL_FECHA");
                _colUsuario_704ILR.HeaderText  = Tr_704ILR.T_704ILR("COL_USUARIO");
                _colCampo_704ILR.HeaderText    = Tr_704ILR.T_704ILR("COL_CAMPO");
                _colAnterior_704ILR.HeaderText = Tr_704ILR.T_704ILR("COL_ANTERIOR");
                _colNuevo_704ILR.HeaderText    = Tr_704ILR.T_704ILR("COL_NUEVO");
            }
            if (_lblVacio_704ILR != null) _lblVacio_704ILR.Text = T_704ILR("HIST_VACIO", "Sin cambios registrados.");
            _grid_704ILR?.Invalidate();
        }
    }
}
