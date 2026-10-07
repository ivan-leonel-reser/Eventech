using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;
using EvenTech.Services;

namespace EvenTech.UI
{
    // UserControl de gestion de reservas: grilla + ficha de alta/edicion.
    // Layout por TableLayoutPanel/Dock (DPI-aware, sin coordenadas magicas):
    // fila 0 = barra de titulo, fila 1 = cuerpo en dos columnas (grilla / ficha).
    // Observa el cambio de idioma (patron Observer) para traducir sus textos.
    public class ucReservas_704ILR : UserControl, IObservadorIdioma_704ILR, IVistaConCambios_704ILR
    {
        private DataGridView _grid_704ILR;
        private Label _lblCount_704ILR, _lblError_704ILR, _lblFormTitle_704ILR;
        private TextBox _txtMonto_704ILR;   // solo lectura: total = suma de los servicios contratados
        private ComboBox _cboCliente_704ILR, _cboSalon_704ILR, _cboEstado_704ILR;
        private NumericUpDown _numInvitados_704ILR;   // PN1: Cantidad_Invitados (RN-06)
        private DateTimePicker _dtFecha_704ILR;
        private AppButton_704ILR _btnNuevo_704ILR, _btnDisponibilidad_704ILR, _btnGuardar_704ILR, _btnHistorial_704ILR, _btnNuevoCliente_704ILR, _btnServicios_704ILR, _btnPagos_704ILR, _btnComprobante_704ILR, _btnEmail_704ILR, _btnVersiones_704ILR;
        private List<BE_ReservaServicio_704ILR> _serviciosReserva_704ILR = new List<BE_ReservaServicio_704ILR>();
        // false = no se pudo leer la composicion de servicios de la reserva en edicion.
        // La lista queda vacia, pero vacia NO es su composicion: la ficha se bloquea
        // (ver AplicarModificabilidad) para que un Guardar no la tome como el pedido de
        // quitar todas las lineas y dejar el monto en cero.
        private bool _serviciosLeidos_704ILR = true;
        // El alta rapida de cliente es un boton de icono, sin rotulo: el ToolTip es lo
        // que le pone nombre en pantalla ("Nuevo cliente", CUN002 paso 1).
        private readonly ToolTip _tip_704ILR = new ToolTip();
        // Receta del aviso visible: se guarda como se arma el texto y no el texto ya
        // traducido, para volver a componerlo si cambia el idioma (ver ActualizarTextos).
        private Func<string> _mensajeError_704ILR;
        // Glifo de diseno de cada boton con icono (ver AjustarGlifo).
        private readonly Dictionary<AppButton_704ILR, string> _glifos_704ILR = new Dictionary<AppButton_704ILR, string>();

        private int _editId_704ILR; // 0 = alta, >0 = edicion

        // Linea base de la ficha: lo que mostraba al abrir la reserva (CargarEnForm) o al
        // limpiarse para un alta (LimpiarForm). Contra ella se decide si hay cambios sin
        // guardar (IVistaConCambios): frmMain pregunta antes de reemplazar la vista.
        private string _lineaBase_704ILR;
        // Guardar habilitado (condicion de la reserva + permiso): si la ficha no se puede
        // guardar, lo que el usuario haya tocado no son cambios pendientes de guardar.
        private bool _puedeGuardar_704ILR;
        // La reserva abierta tiene un estado almacenado que no es ninguno de la tabla de
        // estados (alteracion externa; la DAL lo entrega como un valor fuera del enum).
        private bool _estadoDesconocido_704ILR;
        // La reserva abierta no se pudo mostrar completa: su cliente o su salon no figuran
        // en las listas de la ficha ni despues de recargarlas, su fecha esta fuera del
        // calendario del selector o su cantidad de invitados fuera del rango del campo
        // (alteradas por fuera de la aplicacion), o fallo la carga (tambien la de esas listas).
        // Guarda el aviso que lo explica; con el, la ficha queda de solo lectura (ver
        // AplicarModificabilidad y FichaCompleta). null = la ficha muestra la reserva entera.
        private Func<string> _fichaIncompleta_704ILR;

        // Como reacciona la ficha a un cambio de seleccion de la grilla (ver
        // Grid_SelectionChanged). Mayor que cero en _seleccionSuspendida: la pantalla mueve
        // la seleccion sin tocar la ficha (vaciar la seleccion de un alta, volver a la fila de
        // la ficha tras responder "No"). Mayor que cero en _seleccionProgramada: la pantalla
        // recarga la grilla o reselecciona la reserva recien guardada, y la ficha se carga sin
        // preguntar. Fuera de esos dos casos el cambio lo hizo el usuario.
        private int _seleccionSuspendida_704ILR;
        private int _seleccionProgramada_704ILR;

        // El aviso a la vista es de los que resuelve una precarga desde Disponibilidad (un
        // salon invalido u ocupado, una fecha invalida, una capacidad insuficiente o una
        // consulta anterior sin propuesta). Los demas avisos siguen a la vista tras precargar.
        private bool _avisoResueltoPorPrecarga_704ILR;

        // La grilla muestra al menos una reserva con un estado almacenado fuera de la tabla de
        // estados: su leyenda entra en el ancho minimo de la columna Estado (ver
        // AjustarMinimosDeTexto).
        private bool _hayEstadoDesconocidoEnGrilla_704ILR;

        // Formato del selector de fecha de la ficha (el de Ui_704ILR.DatePicker_704ILR).
        private const string FormatoFecha_704ILR = "yyyy-MM-dd";

        // Tramos de la foto de la ficha (ver FotoFicha).
        private const int TramoEstado_704ILR = 4;
        private const int TramoServicios_704ILR = 5;

        // Nombre del archivo del comprobante (ver NombreArchivoComprobante).
        private const string PrefijoComprobantePorDefecto_704ILR = "Comprobante_Reserva_";
        private const int LargoMaximoPrefijo_704ILR = 100;
        private static readonly HashSet<string> NombresReservados_704ILR = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM\u00B9", "COM\u00B2", "COM\u00B3",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3"
        };

        // Ancho de la tarjeta de la ficha (ver BuildBody).
        private const int AnchoFicha_704ILR = 252;
        // Pisos de las columnas numericas de largo variable (ver SafeLoadData).
        private const int AnchoMinimoId_704ILR = 44;
        private const int AnchoMinimoMonto_704ILR = 94;

        // Filtro del cuadro "Guardar como" del comprobante cuando la traduccion CMP_FILTER
        // no es un filtro valido. Es neutro de idioma y constante del codigo: siempre sirve.
        private const string FiltroComprobantePorDefecto_704ILR = "HTML (*.html)|*.html";

        // Orden del ciclo de vida de la reserva (tabla de estados): el combo Estado ofrece los
        // estados siempre en este orden, tambien los que se agregan al abrir una reserva
        // registrada (ver AjustarEstado).
        private static readonly EstadoReserva_704ILR[] OrdenEstados_704ILR =
        {
            EstadoReserva_704ILR.COTIZACION, EstadoReserva_704ILR.PENDIENTE, EstadoReserva_704ILR.CONFIRMADA, EstadoReserva_704ILR.CANCELADA
        };

        // Destinatario del correo con el comprobante (ver EvaluarDestinatario).
        private enum DestinatarioCorreo_704ILR { Valido_704ILR, SinCorreo_704ILR, Ilegible_704ILR }

        public ucReservas_704ILR()
        {
            BackColor = Theme_704ILR.BgContent_704ILR;
            BuildUi_704ILR();
            ActualizarTextos_704ILR();
            Load += (s_704ILR, e_704ILR) => { CargarClientes_704ILR(); CargarSalones_704ILR(); LimpiarForm_704ILR(); SafeLoadData_704ILR(); GestorDeIdioma_704ILR.GetInstance_704ILR.Suscribir_704ILR(this); };
            // El ToolTip no es hijo de ningun control: si no se libera con la vista, su
            // ventana nativa y sus recursos sobreviven a cada visita a la seccion.
            Disposed += (s_704ILR, e_704ILR) =>
            {
                GestorDeIdioma_704ILR.GetInstance_704ILR.Desuscribir_704ILR(this);
                _tip_704ILR.Dispose();
            };
        }

        private void BuildUi_704ILR()
        {
            // ---------------- Estructura raiz ----------------
            var root_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Theme_704ILR.BgContent_704ILR
            };
            root_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // barra de titulo
            root_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // cuerpo

            root_704ILR.Controls.Add(BuildHeader_704ILR(), 0, 0);
            root_704ILR.Controls.Add(BuildBody_704ILR(), 0, 1);

            Controls.Add(root_704ILR);

            foreach (var btn_704ILR in new[] { _btnNuevo_704ILR, _btnDisponibilidad_704ILR, _btnGuardar_704ILR, _btnServicios_704ILR,
                                               _btnPagos_704ILR, _btnComprobante_704ILR, _btnEmail_704ILR, _btnVersiones_704ILR })
                RegistrarGlifo_704ILR(btn_704ILR);
        }

        // Barra superior: titulo de pagina, boton "Nueva", disponibilidad y conteo.
        private Control BuildHeader_704ILR()
        {
            var header_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // titulo
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // boton nueva
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // boton disponibilidad
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // conteo
            header_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // relleno
            header_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblTitle_704ILR = Ui_704ILR.H1_704ILR("Gestion de Reservas");
            lblTitle_704ILR.Tag = "T:RES_TITULO";
            lblTitle_704ILR.Anchor = AnchorStyles.Left;
            lblTitle_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0);

            _btnNuevo_704ILR = Ui_704ILR.Primary_704ILR("Nueva", Theme_704ILR.IcoAdd_704ILR);
            _btnNuevo_704ILR.Tag = "T:BTN_NUEVA";
            _btnNuevo_704ILR.Size = new Size(120, 36);
            _btnNuevo_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR; // vive sobre el area de contenido
            _btnNuevo_704ILR.Anchor = AnchorStyles.Left;
            _btnNuevo_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            // Nueva reemplaza la ficha: con cambios sin guardar se pregunta antes (el mismo
            // aviso que al cambiar de seccion) y "No" deja la ficha como estaba.
            _btnNuevo_704ILR.Click += (s_704ILR, e_704ILR) => { if (ConfirmarDescarte_704ILR()) LimpiarForm_704ILR(); };
            // Primera capa dentro de la seccion: la accion se ofrece solo a quien la
            // tiene (la ficha hace lo mismo con cada boton, ver AplicarPermisosFicha).
            _btnNuevo_704ILR.Enabled = Permisos_704ILR.Tiene_704ILR("RESERVA_CREAR");

            // Consulta de disponibilidad (Proceso 1, paso 1): se hace antes de
            // armar la reserva, por eso vive en el header y no en la ficha.
            _btnDisponibilidad_704ILR = Ui_704ILR.Secondary_704ILR("Disponibilidad", Theme_704ILR.IcoCalendar_704ILR);
            _btnDisponibilidad_704ILR.Tag = "T:RES_DISPONIBILIDAD_BTN";
            _btnDisponibilidad_704ILR.Size = new Size(160, 36);
            _btnDisponibilidad_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnDisponibilidad_704ILR.Anchor = AnchorStyles.Left;
            _btnDisponibilidad_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceMd_704ILR, 0);
            _btnDisponibilidad_704ILR.Click += (s_704ILR, e_704ILR) => ConsultarDisponibilidad_704ILR();
            _btnDisponibilidad_704ILR.Enabled = Permisos_704ILR.Tiene_704ILR("DISPONIBILIDAD_CONSULTAR");

            _lblCount_704ILR = Ui_704ILR.Body_704ILR();
            _lblCount_704ILR.ForeColor = Theme_704ILR.TextMuted_704ILR;
            _lblCount_704ILR.Anchor = AnchorStyles.Left;
            _lblCount_704ILR.Margin = new Padding(0, 0, 0, 0);

            header_704ILR.Controls.Add(lblTitle_704ILR, 0, 0);
            header_704ILR.Controls.Add(_btnNuevo_704ILR, 1, 0);
            header_704ILR.Controls.Add(_btnDisponibilidad_704ILR, 2, 0);
            header_704ILR.Controls.Add(_lblCount_704ILR, 3, 0);

            return header_704ILR;
        }

        // Cuerpo: dos columnas (grilla a la izquierda, ficha a la derecha).
        private Control BuildBody_704ILR()
        {
            var body_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme_704ILR.BgContent_704ILR,
                Margin = new Padding(0)
            };
            // La ficha tiene ancho FIJO (252 px: le alcanza con sus campos en dos
            // columnas) y la grilla se queda con todo el resto: tiene ocho columnas y
            // es la que necesita cada pixel para no truncar nombres ni fechas en la
            // ventana por defecto; en una ventana mas grande es la que crece.
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoFicha_704ILR));
            body_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            body_704ILR.Controls.Add(BuildGridCard_704ILR(), 0, 0);
            body_704ILR.Controls.Add(BuildFormCard_704ILR(), 1, 0);

            return body_704ILR;
        }

        // Tarjeta con la grilla de reservas.
        private Control BuildGridCard_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, Theme_704ILR.SpaceLg_704ILR, 0),
                Padding = new Padding(Theme_704ILR.SpaceSm_704ILR)
            };

            _grid_704ILR = new DataGridView { Dock = DockStyle.Fill };
            UiGrid_704ILR.Style_704ILR(_grid_704ILR);

            // TODAS las columnas llevan ancho minimo: en modo Fill una columna sin minimo
            // absorbe lo que falte y en la ventana por defecto Estado e Invitados salian
            // con puntos suspensivos ("Confirm...", "Invitad..."). Cliente y Salon son
            // texto libre y llevan un minimo fijo que alcanza para los nombres habituales.
            // Estado, Invitados, Fecha y Vence tienen contenido conocido y su minimo se
            // MIDE en el idioma activo (ver AjustarMinimosDeTexto); Id y Monto dependen de
            // los datos cargados (ver SafeLoadData).
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cId",      HeaderText = "Id",      DataPropertyName = "Id_704ILR",            FillWeight = 6,  MinimumWidth = AnchoMinimoId_704ILR });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCliente", HeaderText = "Cliente", DataPropertyName = "ClienteNombre_704ILR", FillWeight = 16, MinimumWidth = 120 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSalon",   HeaderText = "Salon",   DataPropertyName = "SalonNombre_704ILR",   FillWeight = 14, MinimumWidth = 106 });
            // Fecha y Vence tienen patron fijo y se escriben con la cultura invariante: calendario
            // gregoriano y ':' como separador de hora con cualquier configuracion regional (con la
            // de la estacion salia 2570 en th-TH y 20.58 en fi-FI). Los importes siguen en la cultura.
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cFecha",   HeaderText = "Fecha",   DataPropertyName = "FechaEvento_704ILR",   FillWeight = 11, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd", FormatProvider = System.Globalization.CultureInfo.InvariantCulture } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstado",  HeaderText = "Estado",  DataPropertyName = "Estado_704ILR",        FillWeight = 12 });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cInvitados", HeaderText = "Invitados", DataPropertyName = "CantidadInvitados_704ILR", FillWeight = 13, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMonto",   HeaderText = "Monto",   DataPropertyName = "Monto_704ILR",         FillWeight = 12, MinimumWidth = AnchoMinimoMonto_704ILR, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
            // RN-01: vigencia de la cotizacion / reserva pendiente. Se muestra CON la
            // hora: el plazo se cuenta desde el momento exacto de la emision (15 dias
            // o 72 horas sobre DateTime.Now) y la regla rechaza la operacion pasada esa
            // hora, asi que solo con la fecha el vendedor no podia prever el rechazo el
            // ultimo dia.
            _grid_704ILR.Columns.Add(new DataGridViewTextBoxColumn { Name = "cVence",   HeaderText = "Vence",   DataPropertyName = "VenceEl_704ILR",       FillWeight = 16, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm", FormatProvider = System.Globalization.CultureInfo.InvariantCulture } });
            _grid_704ILR.SelectionChanged += Grid_SelectionChanged_704ILR;
            // Tambien al cambiar la celda actual: cuando la fila actual se mueve por codigo, la
            // grilla avisa el cambio de seleccion ANTES de mover la fila actual, y sin este
            // evento la ficha seguia en la reserva anterior con otra fila marcada.
            _grid_704ILR.CurrentCellChanged += Grid_SelectionChanged_704ILR;
            _grid_704ILR.CellFormatting += Grid_CellFormatting_704ILR;
            _grid_704ILR.SizeChanged += Grid_SizeChanged_704ILR;

            // Aviso de la seccion (rechazo, error o reserva cancelada): va ARRIBA DE LA
            // GRILLA, dentro de su tarjeta. En la cabecera le quitaba alto a todo el
            // cuerpo y la ficha, que en la ventana por defecto entra justa, se llenaba de
            // barras de desplazamiento y escondia Servicios. Aca el alto lo cede la
            // grilla, que ya tiene su propio desplazamiento.
            _lblError_704ILR = Ui_704ILR.Body_704ILR();
            _lblError_704ILR.Font = Theme_704ILR.FontBodyBold_704ILR;
            _lblError_704ILR.ForeColor = Theme_704ILR.Error_704ILR;
            _lblError_704ILR.Visible = false;
            _lblError_704ILR.AutoSize = true;
            _lblError_704ILR.Dock = DockStyle.Fill;
            _lblError_704ILR.Margin = new Padding(Theme_704ILR.SpaceXs_704ILR, 2, Theme_704ILR.SpaceXs_704ILR, Theme_704ILR.SpaceSm_704ILR);

            var gridLayout_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            gridLayout_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            gridLayout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // aviso
            gridLayout_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // grilla
            // Sin margen: el de 3 px por defecto de una celda le quitaria ancho a la grilla.
            _grid_704ILR.Margin = new Padding(0);
            gridLayout_704ILR.Controls.Add(_lblError_704ILR, 0, 0);
            gridLayout_704ILR.Controls.Add(_grid_704ILR, 0, 1);

            card_704ILR.Controls.Add(gridLayout_704ILR);
            return card_704ILR;
        }

        // Tarjeta con la ficha de alta/edicion.
        private Control BuildFormCard_704ILR()
        {
            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR)
            };

            // Layout interno de la ficha: titulo, campos (scrollables) y botones.
            var layout_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            layout_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // titulo ficha
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // campos
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // botones

            _lblFormTitle_704ILR = Ui_704ILR.Title_704ILR("Nueva reserva");
            _lblFormTitle_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR);

            // Campos etiquetados (caption arriba, input abajo) en una grilla de DOS
            // columnas: Cliente, Salon y Servicios ocupan el ancho completo y los
            // cuatro campos cortos van de a pares (Fecha | Invitados, Estado | Monto).
            // Con los siete campos apilados en una sola columna la ficha media mas
            // que el area de contenido de la ventana por defecto (1366x768) y
            // Servicios y Monto quedaban bajo la barra de desplazamiento: la pantalla
            // principal del proceso se entregaba recortada. Cada campo Dock=Fill se
            // ajusta solo al redimensionar (sin calculos manuales).
            var fields_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            fields_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            // Las filas van AutoSize: con una fila en Percent el panel se estiraba
            // para ocupar el alto disponible y AutoScroll no llegaba a activarse nunca,
            // de modo que en una ventana chica los ultimos campos quedaban inalcanzables.
            for (int i_704ILR = 0; i_704ILR < 6; i_704ILR++) fields_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Cliente: combo para elegir uno existente + boton de alta rapida.
            _cboCliente_704ILR = Ui_704ILR.Combo_704ILR();
            _cboCliente_704ILR.Dock = DockStyle.Fill;
            _cboCliente_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, 0);
            _btnNuevoCliente_704ILR = Ui_704ILR.Secondary_704ILR("", Theme_704ILR.IcoAdd_704ILR);
            _btnNuevoCliente_704ILR.Dock = DockStyle.Fill;
            _btnNuevoCliente_704ILR.Margin = new Padding(0);
            _btnNuevoCliente_704ILR.Click += (s_704ILR, e_704ILR) => NuevoCliente_704ILR();
            _btnNuevoCliente_704ILR.Enabled = Permisos_704ILR.Tiene_704ILR("CLIENTES_GESTION");
            _tip_704ILR.SetToolTip(_btnNuevoCliente_704ILR, T_704ILR("CLI_NUEVO", "Nuevo cliente"));
            var clientePanel_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            clientePanel_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            clientePanel_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            clientePanel_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            clientePanel_704ILR.Controls.Add(_cboCliente_704ILR, 0, 0);
            clientePanel_704ILR.Controls.Add(_btnNuevoCliente_704ILR, 1, 0);
            var fldCliente_704ILR = Ui_704ILR.Field_704ILR("Cliente", clientePanel_704ILR);
            ((Label)fldCliente_704ILR.GetControlFromPosition(0, 0)).Tag = "T:COL_CLIENTE";

            _cboSalon_704ILR = Ui_704ILR.Combo_704ILR();
            var fldSalon_704ILR = Ui_704ILR.Field_704ILR("Salon", _cboSalon_704ILR);
            ((Label)fldSalon_704ILR.GetControlFromPosition(0, 0)).Tag = "T:COL_SALON";

            // Fecha e Invitados van de a par en la ficha (ver mas abajo), asi que
            // llevan los rotulos cortos de la grilla ("Fecha", "Invitados"): los
            // largos ("Fecha del evento", "Invitados estimados") no entran en media
            // ficha y se partian en dos lineas.
            _dtFecha_704ILR = Ui_704ILR.DatePicker_704ILR();
            _dtFecha_704ILR.MinDate = DateTime.Today;
            var fldFecha_704ILR = Ui_704ILR.Field_704ILR("Fecha", _dtFecha_704ILR);
            ((Label)fldFecha_704ILR.GetControlFromPosition(0, 0)).Tag = "T:COL_FECHA";

            // Invitados estimados: el mismo dato con el que se consulta la
            // disponibilidad, ahora persistido en la reserva (PN1 / RN-06).
            // Campo entero: el numero que se ve es el que se guarda (con un NumericUpDown
            // comun "250,5" mostraba 251 y se guardaba 250).
            _numInvitados_704ILR = new CampoEntero_704ILR
            {
                Minimum = 0, Maximum = BLL_Reserva_704ILR.InvitadosMaximo_704ILR, Dock = DockStyle.Fill,
                Font = Theme_704ILR.FontInput_704ILR, TextAlign = HorizontalAlignment.Right
            };
            var fldInvitados_704ILR = Ui_704ILR.Field_704ILR("Invitados", _numInvitados_704ILR);
            ((Label)fldInvitados_704ILR.GetControlFromPosition(0, 0)).Tag = "T:COL_INVITADOS";

            _cboEstado_704ILR = Ui_704ILR.Combo_704ILR();
            _cboEstado_704ILR.Items.AddRange(Array.ConvertAll(OrdenEstados_704ILR, e_704ILR => (object)e_704ILR));
            Ui_704ILR.DibujarEnum_704ILR(_cboEstado_704ILR, o_704ILR => o_704ILR is EstadoReserva_704ILR est_704ILR ? Tr_704ILR.Estado_704ILR(est_704ILR) : o_704ILR?.ToString());
            var fldEstado_704ILR = Ui_704ILR.Field_704ILR("Estado", _cboEstado_704ILR);
            ((Label)fldEstado_704ILR.GetControlFromPosition(0, 0)).Tag = "T:COL_ESTADO";

            // Servicios contratados: boton que abre el dialogo de carga.
            _btnServicios_704ILR = Ui_704ILR.Secondary_704ILR("Servicios", Theme_704ILR.IcoServicio_704ILR);
            _btnServicios_704ILR.Click += (s_704ILR, e_704ILR) => EditarServicios_704ILR();
            var fldServicios_704ILR = Ui_704ILR.Field_704ILR("Servicios", _btnServicios_704ILR);
            ((Label)fldServicios_704ILR.GetControlFromPosition(0, 0)).Tag = "T:MENU_SERVICIOS";

            // Monto = total (suma de servicios), de solo lectura.
            _txtMonto_704ILR = Ui_704ILR.Input_704ILR();
            _txtMonto_704ILR.ReadOnly = true;
            _txtMonto_704ILR.BackColor = Theme_704ILR.SurfaceAlt_704ILR;
            var fldMonto_704ILR = Ui_704ILR.Field_704ILR("Monto", _txtMonto_704ILR);
            ((Label)fldMonto_704ILR.GetControlFromPosition(0, 0)).Tag = "T:COL_MONTO";

            // Ubicacion de cada campo: (columna, fila, cuantas columnas abarca).
            var ubic_704ILR = new (TableLayoutPanel fld_704ILR, int col_704ILR, int fila_704ILR, int span_704ILR)[]
            {
                (fldCliente_704ILR,   0, 0, 2),
                (fldSalon_704ILR,     0, 1, 2),
                (fldFecha_704ILR,     0, 2, 1), (fldInvitados_704ILR, 1, 2, 1),
                (fldEstado_704ILR,    0, 3, 1), (fldMonto_704ILR,     1, 3, 1),
                (fldServicios_704ILR, 0, 4, 2)
            };
            foreach (var u_704ILR in ubic_704ILR)
            {
                u_704ILR.fld_704ILR.Dock = DockStyle.Fill;
                // Entre los dos campos de un par queda una canaleta de 2 x SpaceXs.
                int izq_704ILR = u_704ILR.col_704ILR == 1 ? Theme_704ILR.SpaceXs_704ILR : 0;
                int der_704ILR = u_704ILR.span_704ILR == 1 && u_704ILR.col_704ILR == 0 ? Theme_704ILR.SpaceXs_704ILR : 0;
                u_704ILR.fld_704ILR.Margin = new Padding(izq_704ILR, 0, der_704ILR, Theme_704ILR.SpaceSm_704ILR);
                fields_704ILR.Controls.Add(u_704ILR.fld_704ILR, u_704ILR.col_704ILR, u_704ILR.fila_704ILR);
                if (u_704ILR.span_704ILR > 1) fields_704ILR.SetColumnSpan(u_704ILR.fld_704ILR, u_704ILR.span_704ILR);
            }

            // Botones de accion apilados al pie de la ficha.
            var actions_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0)
            };
            actions_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            actions_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            actions_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            actions_704ILR.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            _btnGuardar_704ILR = Ui_704ILR.Primary_704ILR("Guardar", Theme_704ILR.IcoSave_704ILR);
            _btnGuardar_704ILR.Tag = "T:BTN_GUARDAR";
            _btnGuardar_704ILR.Dock = DockStyle.Fill;
            _btnGuardar_704ILR.Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceSm_704ILR);
            _btnGuardar_704ILR.Click += (s_704ILR, e_704ILR) => Guardar_704ILR();

            // Fila inferior: historial + pagos lado a lado.
            var secondary_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            secondary_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            secondary_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            secondary_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _btnHistorial_704ILR = Ui_704ILR.Secondary_704ILR("Ver historial de cambios");
            _btnHistorial_704ILR.Tag = "T:RES_HISTORIAL";
            _btnHistorial_704ILR.Dock = DockStyle.Fill;
            _btnHistorial_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, 0);
            _btnHistorial_704ILR.Click += (s_704ILR, e_704ILR) => VerHistorial_704ILR();

            _btnPagos_704ILR = Ui_704ILR.Secondary_704ILR("Pagos", Theme_704ILR.IcoPago_704ILR);
            _btnPagos_704ILR.Tag = "T:RES_PAGOS_BTN";
            _btnPagos_704ILR.Dock = DockStyle.Fill;
            _btnPagos_704ILR.Margin = new Padding(Theme_704ILR.SpaceXs_704ILR, 0, 0, 0);
            _btnPagos_704ILR.Click += (s_704ILR, e_704ILR) => EditarPagos_704ILR();

            secondary_704ILR.Controls.Add(_btnHistorial_704ILR, 0, 0);
            secondary_704ILR.Controls.Add(_btnPagos_704ILR, 1, 0);

            // Fila del comprobante: es la etiqueta mas larga de la ficha y va sola,
            // a todo el ancho, para que el rotulo entre completo.
            _btnComprobante_704ILR = Ui_704ILR.Secondary_704ILR("Comprobante", Theme_704ILR.IcoDocumento_704ILR);
            _btnComprobante_704ILR.Tag = "T:RES_COMPROBANTE_BTN";
            _btnComprobante_704ILR.Dock = DockStyle.Fill;
            _btnComprobante_704ILR.Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0);
            _btnComprobante_704ILR.Click += (s_704ILR, e_704ILR) => GenerarComprobante_704ILR();

            // Ultima fila: email + versiones lado a lado.
            var docRow_704ILR = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, Theme_704ILR.SpaceSm_704ILR, 0, 0) };
            docRow_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            docRow_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            docRow_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _btnEmail_704ILR = Ui_704ILR.Secondary_704ILR("Email", Theme_704ILR.IcoEmail_704ILR);
            _btnEmail_704ILR.Tag = "T:RES_EMAIL_BTN";
            _btnEmail_704ILR.Dock = DockStyle.Fill;
            _btnEmail_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceXs_704ILR, 0);
            _btnEmail_704ILR.Click += (s_704ILR, e_704ILR) => EnviarEmail_704ILR();

            // Versiones (patron Memento): abre el dialogo para restaurar la reserva
            // a un estado anterior.
            _btnVersiones_704ILR = Ui_704ILR.Secondary_704ILR("Versiones", Theme_704ILR.IcoDocumento_704ILR);
            _btnVersiones_704ILR.Tag = "T:RES_VERSIONES";
            _btnVersiones_704ILR.Dock = DockStyle.Fill;
            _btnVersiones_704ILR.Margin = new Padding(Theme_704ILR.SpaceXs_704ILR, 0, 0, 0);
            _btnVersiones_704ILR.Click += (s_704ILR, e_704ILR) => VerVersiones_704ILR();

            docRow_704ILR.Controls.Add(_btnEmail_704ILR, 0, 0);
            docRow_704ILR.Controls.Add(_btnVersiones_704ILR, 1, 0);

            actions_704ILR.Controls.Add(_btnGuardar_704ILR, 0, 0);
            actions_704ILR.Controls.Add(secondary_704ILR, 0, 1);
            actions_704ILR.Controls.Add(_btnComprobante_704ILR, 0, 2);
            actions_704ILR.Controls.Add(docRow_704ILR, 0, 3);

            layout_704ILR.Controls.Add(_lblFormTitle_704ILR, 0, 0);
            layout_704ILR.Controls.Add(fields_704ILR, 0, 1);
            layout_704ILR.Controls.Add(actions_704ILR, 0, 2);

            card_704ILR.Controls.Add(layout_704ILR);
            return card_704ILR;
        }

        // Observer: re-traduce textos estaticos, encabezados de grilla y etiquetas dinamicas.
        public void ActualizarTextos_704ILR()
        {
            Tr_704ILR.AplicarTags_704ILR(this);
            if (_grid_704ILR.Columns.Count >= 8)
            {
                _grid_704ILR.Columns["cId"].HeaderText      = Tr_704ILR.T_704ILR("COL_ID");
                _grid_704ILR.Columns["cCliente"].HeaderText = Tr_704ILR.T_704ILR("COL_CLIENTE");
                _grid_704ILR.Columns["cSalon"].HeaderText   = Tr_704ILR.T_704ILR("COL_SALON");
                _grid_704ILR.Columns["cFecha"].HeaderText   = Tr_704ILR.T_704ILR("COL_FECHA");
                _grid_704ILR.Columns["cInvitados"].HeaderText = T_704ILR("COL_INVITADOS", "Invitados");
                _grid_704ILR.Columns["cVence"].HeaderText   = T_704ILR("COL_VENCE", "Vence");
                _grid_704ILR.Columns["cEstado"].HeaderText  = Tr_704ILR.T_704ILR("COL_ESTADO");
                _grid_704ILR.Columns["cMonto"].HeaderText   = Tr_704ILR.T_704ILR("COL_MONTO");
                AjustarMinimosDeTexto_704ILR();
            }
            // El ToolTip no lleva Tag: se re-traduce a mano como los encabezados.
            if (_btnNuevoCliente_704ILR != null)
                _tip_704ILR.SetToolTip(_btnNuevoCliente_704ILR, T_704ILR("CLI_NUEVO", "Nuevo cliente"));
            // Re-traduce los valores de Estado (grilla por celda, combo por display).
            _grid_704ILR.Invalidate();
            _cboEstado_704ILR.Invalidate();
            ActualizarTituloForm_704ILR();
            ActualizarCount_704ILR();
            ActualizarMonto_704ILR();
            // El aviso se vuelve a componer en el idioma nuevo.
            if (_mensajeError_704ILR != null) _lblError_704ILR.Text = _mensajeError_704ILR();
        }

        // Minimo de las columnas cuyo contenido es un texto conocido: se mide en el
        // idioma activo, con la letra y la escala reales, en lugar de fijar pixeles.
        // Asi ningun rotulo traducido ni valor de Estado sale con puntos suspensivos, y
        // un idioma con textos mas largos agranda la columna en vez de cortarla.
        // Celda: texto + Padding del estilo. Encabezado: texto + Padding + 4 px de la
        // celda de encabezado. Se suma 1 px de holgura para el redondeo del reparto.
        private void AjustarMinimosDeTexto_704ILR()
        {
            var enc_704ILR = _grid_704ILR.ColumnHeadersDefaultCellStyle;
            var cel_704ILR = _grid_704ILR.DefaultCellStyle;
            int Encabezado_704ILR(string col_704ILR) =>
                TextRenderer.MeasureText(_grid_704ILR.Columns[col_704ILR].HeaderText, enc_704ILR.Font).Width + enc_704ILR.Padding.Horizontal + 4 + 1;
            int Celda_704ILR(string t_704ILR) =>
                TextRenderer.MeasureText(t_704ILR, cel_704ILR.Font).Width + cel_704ILR.Padding.Horizontal + 1;

            int estado_704ILR = Encabezado_704ILR("cEstado");
            foreach (EstadoReserva_704ILR e_704ILR in Enum.GetValues(typeof(EstadoReserva_704ILR)))
                estado_704ILR = Math.Max(estado_704ILR, Celda_704ILR(Tr_704ILR.Estado_704ILR(e_704ILR)));
            // La leyenda de un estado almacenado fuera de la tabla de estados tambien entra
            // en la medida, pero solo cuando la grilla muestra una fila asi (ver SafeLoadData):
            // es mas larga que cualquier estado y, medida siempre, la suma de los minimos
            // superaba el ancho de la grilla en la ventana por defecto (808 px en castellano y
            // 830 en portugues sobre 774) y todas las sesiones veian una barra horizontal.
            if (_hayEstadoDesconocidoEnGrilla_704ILR)
                estado_704ILR = Math.Max(estado_704ILR, Celda_704ILR(Tr_704ILR.Estado_704ILR((EstadoReserva_704ILR)(-1))));
            _grid_704ILR.Columns["cEstado"].MinimumWidth = estado_704ILR;
            _grid_704ILR.Columns["cInvitados"].MinimumWidth = Encabezado_704ILR("cInvitados");
            // La muestra se escribe como las celdas: con la cultura invariante (ver BuildGridCard).
            var muestra_704ILR = new DateTime(2000, 12, 28, 20, 58, 0);
            _grid_704ILR.Columns["cFecha"].MinimumWidth = Math.Max(Encabezado_704ILR("cFecha"), Celda_704ILR(muestra_704ILR.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
            _grid_704ILR.Columns["cVence"].MinimumWidth = Math.Max(Encabezado_704ILR("cVence"), Celda_704ILR(muestra_704ILR.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)));
        }

        // Minimo de una columna numerica de largo variable: el valor mas largo cargado.
        private void AjustarMinimoPorContenido_704ILR(string col_704ILR, int piso_704ILR)
        {
            var c_704ILR = _grid_704ILR.Columns[col_704ILR];
            c_704ILR.MinimumWidth = Math.Max(piso_704ILR, c_704ILR.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCellsExceptHeader, true));
        }

        // En modo Fill el reparto parte de los anchos que la grilla ya tenia. Montada
        // chica (Navegar la agrega antes de darle su tamano final) o tras maximizar y
        // restaurar, las proporciones quedaban deformadas: columnas truncadas, una barra
        // horizontal y una pantalla distinta de la figura. Reasignar un peso marca los
        // pesos como pendientes y el reparto vuelve a calcularse desde ellos.
        private void Grid_SizeChanged_704ILR(object sender_704ILR, EventArgs e_704ILR)
        {
            if (_grid_704ILR.Columns.Count == 0) return;
            var col_704ILR = _grid_704ILR.Columns[0];
            col_704ILR.FillWeight = col_704ILR.FillWeight;
        }

        private void ActualizarTituloForm_704ILR()
        {
            _lblFormTitle_704ILR.Text = _editId_704ILR == 0
                ? Tr_704ILR.T_704ILR("RES_FORM_NUEVA")
                : Tr_704ILR.T_704ILR("RES_FORM_EDITAR") + " #" + _editId_704ILR;
        }

        private void ActualizarCount_704ILR()
        {
            if (_grid_704ILR.DataSource is List<BE_Reserva_704ILR> data_704ILR)
                _lblCount_704ILR.Text = data_704ILR.Count + " " + Tr_704ILR.T_704ILR("RES_COUNT");
        }

        // Carga (o recarga) la lista de salones de la ficha. Devuelve false si no se pudo leer:
        // la falla queda asentada y a la vista, y quien recargaba para buscar un salon no la
        // confunde con un salon que no existe (ver ElegirPorId).
        private bool CargarSalones_704ILR()
        {
            try
            {
                _cboSalon_704ILR.DataSource = BLL_Salon_704ILR.GetAll_704ILR();
                _cboSalon_704ILR.DisplayMember = "Nombre_704ILR";
                _cboSalon_704ILR.ValueMember = "Id_704ILR";
                _cboSalon_704ILR.SelectedIndex = _cboSalon_704ILR.Items.Count > 0 ? 0 : -1;
                return true;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Cargar salones");
                ShowError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
                return false;
            }
        }

        // Lo mismo con la lista de clientes (ver CargarSalones).
        private bool CargarClientes_704ILR()
        {
            try
            {
                _cboCliente_704ILR.DataSource = BLL_Cliente_704ILR.GetAll_704ILR();
                _cboCliente_704ILR.DisplayMember = "NombreCompleto_704ILR";
                _cboCliente_704ILR.ValueMember = "Id_704ILR";
                _cboCliente_704ILR.SelectedIndex = _cboCliente_704ILR.Items.Count > 0 ? 0 : -1;
                return true;
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Cargar clientes");
                ShowError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
                return false;
            }
        }

        // Alta rapida de cliente desde la ficha (Proceso 1: "si es nuevo, registrarlo").
        // El alta rapida de cliente desde la ficha de reserva persiste igual que la
        // pantalla de Clientes, asi que exige el mismo permiso: si no, seria una
        // via para eludir el gating de CLIENTES_GESTION.
        private void NuevoCliente_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("CLIENTES_GESTION", FindForm(), "crear un cliente desde la ficha de reserva")) return;
            using (var dlg_704ILR = new frmNuevoCliente_704ILR())
            {
                if (dlg_704ILR.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    CargarClientes_704ILR();
                    // CUN002, paso 5: se informa el alta y el cliente queda
                    // seleccionado para seguir armando la reserva sin buscarlo.
                    _cboCliente_704ILR.SelectedValue = dlg_704ILR.NuevoId_704ILR;
                    MessageBox.Show(T_704ILR("MSG_CLI_CREADO", "Cliente registrado."), "EvenTech",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void SafeLoadData_704ILR()
        {
            try
            {
                _lblError_704ILR.Visible = false;
                List<BE_Reserva_704ILR> data_704ILR = BLL_Reserva_704ILR.GetAll_704ILR();
                _hayEstadoDesconocidoEnGrilla_704ILR = data_704ILR.Exists(r_704ILR => !Enum.IsDefined(typeof(EstadoReserva_704ILR), r_704ILR.Estado_704ILR));
                AjustarMinimosDeTexto_704ILR();
                // Recargar la grilla la reposiciona y carga en la ficha la fila que queda
                // seleccionada. Lo hace la pantalla (al abrir, tras guardar, cancelar o
                // restaurar), no el usuario: no se pregunta por descartes.
                _seleccionProgramada_704ILR++;
                try { _grid_704ILR.DataSource = data_704ILR; }
                finally { _seleccionProgramada_704ILR--; }
                // Id y Monto tienen largo variable: el valor mas largo cargado fija su ancho
                // minimo (un numero cortado con puntos suspensivos no sirve).
                AjustarMinimoPorContenido_704ILR("cId", AnchoMinimoId_704ILR);
                AjustarMinimoPorContenido_704ILR("cMonto", AnchoMinimoMonto_704ILR);
                ActualizarCount_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Cargar reservas");
                ShowError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
                _lblCount_704ILR.Text = "";
            }
        }

        // Cambio de seleccion de la grilla. Si lo hizo el usuario y la fila es otra reserva (o la
        // ficha esta en un alta), la ficha se reemplaza: con cambios sin guardar se pregunta
        // antes, con el mismo aviso que al cambiar de seccion, y "No" devuelve la grilla a la
        // fila de la ficha sin tocar lo cargado. Elegir la reserva que la ficha ya muestra no
        // la recarga (antes descartaba lo tipeado). Ver _seleccionSuspendida y
        // _seleccionProgramada.
        private void Grid_SelectionChanged_704ILR(object sender_704ILR, EventArgs e_704ILR)
        {
            // La vista ya salio de su contenedor o se esta liberando: al quitarla, la grilla vuelve a
            // ubicar su fila actual y avisa un cambio de seleccion que no hizo el usuario. Sin esta
            // guarda se volvia a preguntar por los cambios que la ventana principal ya habia hecho
            // descartar, y un "No" a esa segunda pregunta no tenia efecto.
            if (Parent == null || Disposing || IsDisposed) return;
            if (_seleccionSuspendida_704ILR > 0) return;
            if (!(_grid_704ILR.CurrentRow?.DataBoundItem is BE_Reserva_704ILR r_704ILR)) return;
            if (_seleccionProgramada_704ILR > 0) { CargarEnForm_704ILR(r_704ILR); return; }
            if (r_704ILR.Id_704ILR == _editId_704ILR) return;
            if (!ConfirmarDescarte_704ILR()) { VolverAFilaDeLaFicha_704ILR(); return; }
            CargarEnForm_704ILR(r_704ILR);
        }

        // Pregunta antes de reemplazar una ficha con cambios sin guardar (otra fila de la
        // grilla, Nueva, la precarga desde Disponibilidad, restaurar una version). true = no hay
        // nada que perder o el usuario acepto descartarlo. Es el aviso con que la ventana
        // principal pregunta al cambiar de seccion, con "No" como respuesta por defecto.
        private bool ConfirmarDescarte_704ILR()
        {
            if (!((IVistaConCambios_704ILR)this).HayCambiosSinGuardar_704ILR) return true;
            return MessageBox.Show(FindForm(),
                T_704ILR("MAIN_CAMBIOS_SIN_GUARDAR", "Hay cambios sin guardar en la secci\u00F3n actual. \u00BFDescartarlos y continuar?"),
                "EvenTech", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        // Tras responder "No", la grilla vuelve a marcar la reserva que muestra la ficha (en un
        // alta, ninguna). Se difiere hasta que la grilla termine el cambio de fila en curso:
        // mover la fila actual desde su propio evento de seleccion no esta admitido. Hasta
        // entonces los demas cambios de seleccion del mismo gesto no preguntan de nuevo.
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
                        if (row_704ILR.DataBoundItem is BE_Reserva_704ILR r_704ILR && r_704ILR.Id_704ILR == _editId_704ILR)
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

        // Traduce el valor de la columna Estado (el enum se muestra segun el idioma).
        private void Grid_CellFormatting_704ILR(object sender_704ILR, DataGridViewCellFormattingEventArgs e_704ILR)
        {
            if (e_704ILR.RowIndex < 0 || e_704ILR.ColumnIndex < 0 || e_704ILR.ColumnIndex >= _grid_704ILR.Columns.Count) return;
            string columna_704ILR = _grid_704ILR.Columns[e_704ILR.ColumnIndex].Name;
            // Una fecha que el calendario de la cultura no admite (escrita por fuera de la
            // aplicacion: 2080 con el calendario Um Al-Qura de ar-SA) no se puede formatear con
            // el: la grilla mostraba su cuadro de error por cada celda y la dejaba vacia. Se
            // escribe con el calendario gregoriano, en el mismo formato de la columna.
            if ((columna_704ILR == "cFecha" || columna_704ILR == "cVence") && e_704ILR.Value is DateTime f_704ILR && !FechaEnCalendario_704ILR(f_704ILR))
            {
                e_704ILR.Value = f_704ILR.ToString(e_704ILR.CellStyle.Format, System.Globalization.CultureInfo.InvariantCulture);
                e_704ILR.FormattingApplied = true;
                return;
            }
            if (columna_704ILR != "cEstado") return;
            if (e_704ILR.Value is EstadoReserva_704ILR est_704ILR) { e_704ILR.Value = Tr_704ILR.Estado_704ILR(est_704ILR); e_704ILR.FormattingApplied = true; }
        }

        private static bool FechaEnCalendario_704ILR(DateTime fecha_704ILR)
        {
            var calendario_704ILR = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.Calendar;
            return fecha_704ILR >= calendario_704ILR.MinSupportedDateTime && fecha_704ILR <= calendario_704ILR.MaxSupportedDateTime;
        }

        // Muestra la reserva en la ficha y toma su linea base. Ninguna falla deja la ficha a
        // medio cargar y guardable ni corta el enlace de la grilla: con la excepcion saliendo de
        // aca la grilla quedaba con una sola fila y trabada, y Guardar escribia sobre la reserva
        // lo que la ficha alcanzo a cargar (fecha de hoy, sin servicios, monto cero). Si la
        // reserva no se puede mostrar entera, la ficha queda de solo lectura con el aviso.
        private void CargarEnForm_704ILR(BE_Reserva_704ILR r_704ILR)
        {
            _editId_704ILR = r_704ILR.Id_704ILR;
            _fichaIncompleta_704ILR = null;
            try
            {
                MostrarReserva_704ILR(r_704ILR);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Mostrar la reserva #" + r_704ILR.Id_704ILR);
                _fichaIncompleta_704ILR = MensajeReservaNoCargada_704ILR;
            }
            AplicarModificabilidad_704ILR(r_704ILR);
            _lineaBase_704ILR = FotoFicha_704ILR();
        }

        private void MostrarReserva_704ILR(BE_Reserva_704ILR r_704ILR)
        {
            ActualizarTituloForm_704ILR();
            AjustarEstadosDisponibles_704ILR();
            // El cliente y el salon se eligen por id y se comprueba que hayan quedado elegidos:
            // los combos se cargan una sola vez y, con una reserva de un cliente registrado en
            // otra sesion despues de abrir la pantalla, el combo quedaba mostrando a OTRO
            // cliente y Guardar le reasignaba la reserva. Se recarga la lista; si tampoco esta,
            // la ficha no se ofrece para guardar. Si la lista no se pudo releer (la base no
            // respondio) no se sabe si el dato falta: la reserva no se pudo mostrar, y el aviso es
            // el que invita a volver a abrirla, no el de un cliente o un salon inexistente.
            bool clienteElegido_704ILR = ElegirPorId_704ILR(_cboCliente_704ILR, r_704ILR.ClienteId_704ILR, CargarClientes_704ILR, out bool clientesLeidos_704ILR);
            bool salonElegido_704ILR = ElegirPorId_704ILR(_cboSalon_704ILR, r_704ILR.SalonId_704ILR, CargarSalones_704ILR, out bool salonesLeidos_704ILR);
            if (!clienteElegido_704ILR || !salonElegido_704ILR)
                _fichaIncompleta_704ILR = clientesLeidos_704ILR && salonesLeidos_704ILR ? MensajeDatoNoDisponible_704ILR : MensajeReservaNoCargada_704ILR;
            // Una reserva cuyo evento ya paso se muestra CON SU FECHA REAL: si se dejara
            // el minimo en hoy, el control subiria la fecha al abrir la ficha y guardar
            // reprogramaria el evento en silencio. Bajando el minimo, el dato se ve tal
            // cual es y quien decide es la regla de negocio, que rechaza guardar con
            // fecha anterior a hoy.
            // Una fecha que el selector no puede mostrar (anterior a su minimo, posterior a
            // 9998-12-31 o fuera del calendario de la cultura, como 2080 en ar-SA) solo llega
            // alterando la base: asignarla lanzaba. El selector queda en blanco, sin mostrar una
            // fecha que no es la de la reserva, y la ficha de solo lectura con su aviso.
            if (FechaMostrable_704ILR(r_704ILR.FechaEvento_704ILR))
            {
                _dtFecha_704ILR.CustomFormat = FormatoFecha_704ILR;
                _dtFecha_704ILR.MinDate = r_704ILR.FechaEvento_704ILR.Date < DateTime.Today
                    ? r_704ILR.FechaEvento_704ILR.Date
                    : DateTime.Today;
                _dtFecha_704ILR.Value = r_704ILR.FechaEvento_704ILR;
            }
            else
            {
                _dtFecha_704ILR.CustomFormat = " ";
                if (_fichaIncompleta_704ILR == null) _fichaIncompleta_704ILR = MensajeFechaFueraDeCalendario_704ILR;
            }
            // Una cantidad de invitados fuera del rango del campo (negativa o mayor que el maximo) solo
            // llega alterando la base. Acotada, la ficha mostraba otra cantidad que la registrada y un
            // Guardar sin tocar nada la reescribia con version y asiento: el campo muestra la cantidad
            // registrada y la ficha queda de solo lectura con su aviso, igual que con una fecha fuera
            // del calendario.
            MostrarInvitados_704ILR(r_704ILR.CantidadInvitados_704ILR);
            if (!InvitadosEnRango_704ILR(r_704ILR.CantidadInvitados_704ILR) && _fichaIncompleta_704ILR == null)
                _fichaIncompleta_704ILR = MensajeInvitadosFueraDeRango_704ILR;
            // Asignar SelectedItem con un valor que no esta en la lista NO cambia la
            // seleccion: con un estado almacenado fuera de la tabla de estados el combo
            // seguia mostrando el de la reserva anterior, y Guardar lo enviaba como pedido
            // de cambio. Por indice, un estado que no figura deja el combo sin seleccion.
            _estadoDesconocido_704ILR = !Enum.IsDefined(typeof(EstadoReserva_704ILR), r_704ILR.Estado_704ILR);
            _cboEstado_704ILR.SelectedIndex = _cboEstado_704ILR.Items.IndexOf(r_704ILR.Estado_704ILR);
            _serviciosLeidos_704ILR = true;
            try { _serviciosReserva_704ILR = BLL_ReservaServicio_704ILR.GetByReserva_704ILR(r_704ILR.Id_704ILR); }
            catch (Exception ex_704ILR)
            {
                // Una falla de lectura no es una composicion vacia: se marca, y la ficha
                // queda de solo lectura y avisada hasta volver a abrir la reserva.
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Cargar servicios de la reserva #" + r_704ILR.Id_704ILR);
                _serviciosReserva_704ILR = new List<BE_ReservaServicio_704ILR>();
                _serviciosLeidos_704ILR = false;
            }
            ActualizarMonto_704ILR();
        }

        // Deja elegido en el combo el elemento con ese id. Asignar SelectedValue con un id que no
        // esta en la lista no deja el combo vacio: queda en otro elemento. Si no esta (se dio de
        // alta en otra sesion despues de cargar la lista) se recarga la lista y se vuelve a
        // buscar; si tampoco esta, el combo queda sin seleccion y devuelve false.
        // listaLeida es false solo si hizo falta recargar y la recarga fallo: en ese caso no se
        // sabe si el elemento existe (la lista quedo como estaba).
        private static bool ElegirPorId_704ILR(ComboBox cbo_704ILR, int id_704ILR, Func<bool> recargar_704ILR, out bool listaLeida_704ILR)
        {
            listaLeida_704ILR = true;
            if (Elegir_704ILR(cbo_704ILR, id_704ILR)) return true;
            listaLeida_704ILR = recargar_704ILR();
            if (listaLeida_704ILR && Elegir_704ILR(cbo_704ILR, id_704ILR)) return true;
            cbo_704ILR.SelectedIndex = -1;
            return false;
        }

        private static bool Elegir_704ILR(ComboBox cbo_704ILR, int id_704ILR)
        {
            cbo_704ILR.SelectedValue = id_704ILR;
            return cbo_704ILR.SelectedValue is int elegido_704ILR && elegido_704ILR == id_704ILR;
        }

        // Rango de carga del campo Invitados: de cero al maximo que admite la operacion.
        private static bool InvitadosEnRango_704ILR(int cantidad_704ILR) =>
            cantidad_704ILR >= 0 && cantidad_704ILR <= BLL_Reserva_704ILR.InvitadosMaximo_704ILR;

        // Muestra una cantidad en el campo Invitados. El rango del campo es el de carga; solo para
        // mostrar la cantidad registrada de una reserva que esta fuera de ese rango (la ficha queda
        // de solo lectura, ver MostrarReserva) se amplia hasta ella, y la proxima cantidad que se
        // muestra lo devuelve al de carga. Primero va el minimo y despues el maximo: los dos rangos
        // contienen al cero, asi que ningun paso deja el minimo por encima del maximo.
        private void MostrarInvitados_704ILR(int cantidad_704ILR)
        {
            _numInvitados_704ILR.Minimum = Math.Min(0, cantidad_704ILR);
            _numInvitados_704ILR.Maximum = Math.Max(BLL_Reserva_704ILR.InvitadosMaximo_704ILR, cantidad_704ILR);
            _numInvitados_704ILR.Value = cantidad_704ILR;
        }

        // Rango que admite el selector de fecha: desde su minimo (1753, o el primer dia del
        // calendario de la cultura) hasta su maximo (9998-12-31, o el ultimo dia de ese calendario).
        private bool FechaMostrable_704ILR(DateTime fecha_704ILR) =>
            fecha_704ILR >= DateTimePicker.MinimumDateTime && fecha_704ILR <= _dtFecha_704ILR.MaxDate;

        // Una reserva cancelada no admite ediciones: se avisa en la ficha y se
        // desactivan Guardar y Pagos. Los pagos importan aparte porque persisten en el
        // acto (no esperan a Guardar), asi que sin bloquearlos se podria seguir moviendo
        // el saldo de una reserva cancelada.
        // Comprobante y Email tambien se apagan: el documento que emiten dice
        // "Comprobante de Reserva" y agradece la contratacion, de modo que sobre una
        // reserva dada de baja afirmaria algo que ya no es cierto (PN1).
        // Los campos de la ficha y el alta rapida de cliente tambien quedan de solo
        // lectura (ver AplicarPermisosFicha): se podian cambiar valores que no se pueden
        // guardar y la ficha terminaba mostrando datos distintos de los de la base.
        // Versiones queda HABILITADO: consultar el historial de versiones no es
        // modificar, y la RN-05 prohibe restaurar, no mirar. La restauracion en si la
        // rechaza la BLL (RestaurarVersion_704ILR devuelve NoModificable), y el dialogo de
        // Versiones deja "Restaurar seleccionada" apagado para una reserva cancelada.
        // El mismo bloqueo, con su propio aviso, rige cuando no se pudo leer la
        // composicion de servicios (ver CargarEnForm): sin ella no hay monto real.
        // Y cuando el estado almacenado no es ninguno de la tabla de estados: no hay
        // transicion posible desde un estado que no existe, asi que la ficha no ofrece
        // cambios que la capa de negocio va a rechazar, ni cobros ni documentos.
        private void AplicarModificabilidad_704ILR(BE_Reserva_704ILR r_704ILR)
        {
            // Tambien cuando la reserva no se pudo mostrar entera (ver CargarEnForm): la ficha no
            // ofrece guardar datos que no son los de la reserva, y su aviso va primero.
            bool modificable_704ILR = _fichaIncompleta_704ILR == null && !_estadoDesconocido_704ILR && BLL_Reserva_704ILR.PuedeModificar_704ILR(r_704ILR);
            // RN-13: con el evento en ejecucion o cerrado la reserva queda congelada. Sus
            // datos y sus servicios ya no se editan (la capa de negocio rechaza el
            // guardado), pero el saldo se sigue cobrando y la documentacion se sigue
            // emitiendo: por eso es un modo aparte del de la reserva cancelada.
            bool congelada_704ILR = modificable_704ILR && BLL_Coordinacion_704ILR.PlanCongelado_704ILR(r_704ILR.EstadoCoordinacion_704ILR);
            AplicarPermisosFicha_704ILR(modificable_704ILR && _serviciosLeidos_704ILR, congelada_704ILR);
            if (_fichaIncompleta_704ILR != null)
                ShowError_704ILR(_fichaIncompleta_704ILR);
            else if (_estadoDesconocido_704ILR)
                ShowError_704ILR(MensajeEstadoDesconocido_704ILR);
            else if (!_serviciosLeidos_704ILR)
                ShowError_704ILR(MensajeServiciosNoLeidos_704ILR);
            else if (!modificable_704ILR)
                ShowError_704ILR(() => T_704ILR("MSG_RES_NO_MODIFICABLE", "La reserva está cancelada: no admite modificaciones."));
            else if (congelada_704ILR)
                ShowError_704ILR(() => MensajeError_704ILR(ReservaResult_704ILR.EventoIniciado_704ILR));
            else
                _lblError_704ILR.Visible = false;
        }

        private static string MensajeEstadoDesconocido_704ILR() =>
            T_704ILR("MSG_RES_ESTADO_DESCONOCIDO", "El estado registrado de la reserva no es v\u00E1lido: no admite modificaciones. Contactate con un administrador.");

        private static string MensajeDatoNoDisponible_704ILR() =>
            T_704ILR("MSG_RES_DATO_NO_DISPONIBLE", "El cliente o el sal\u00F3n registrado en la reserva no figura en las listas de la ficha: no admite modificaciones. Contactate con un administrador.");

        private static string MensajeFechaFueraDeCalendario_704ILR() =>
            T_704ILR("MSG_RES_FECHA_FUERA_RANGO", "La fecha del evento registrada est\u00E1 fuera del calendario admitido: no admite modificaciones. Contactate con un administrador.");

        private static string MensajeReservaNoCargada_704ILR() =>
            T_704ILR("MSG_RES_NO_CARGADA", "No se pudo mostrar la reserva: no se admite modificarla. Seleccione otra reserva y vuelva a abrirla.");

        private static string MensajeInvitadosFueraDeRango_704ILR() =>
            T_704ILR("MSG_RES_INVITADOS_FUERA_RANGO", "La cantidad de invitados registrada está fuera del rango admitido: no admite modificaciones. Contactate con un administrador.");

        // Segunda capa de la ficha incompleta (la primera es la ficha de solo lectura): ni el
        // guardado, ni los servicios, ni los pagos, ni la documentacion operan sobre una reserva
        // que la ficha no pudo mostrar entera.
        private bool FichaCompleta_704ILR()
        {
            if (_editId_704ILR == 0 || _fichaIncompleta_704ILR == null) return true;
            ShowError_704ILR(_fichaIncompleta_704ILR);
            return false;
        }

        // Segunda capa del estado desconocido (la primera es la ficha de solo lectura):
        // ni el guardado, ni los servicios, ni los pagos operan sobre una reserva cuyo
        // estado no esta en la tabla de estados. Guardar mandaba COTIZACION en su lugar.
        private bool EstadoConocido_704ILR()
        {
            if (_editId_704ILR == 0 || !_estadoDesconocido_704ILR) return true;
            ShowError_704ILR(MensajeEstadoDesconocido_704ILR);
            return false;
        }

        private static string MensajeServiciosNoLeidos_704ILR() =>
            T_704ILR("MSG_RES_SERVICIOS_NO_LEIDOS", "No se pudieron leer los servicios contratados de la reserva: no se admite modificarla. Seleccione otra reserva y vuelva a abrirla.");

        // Segunda capa de la lectura fallida (la primera es la ficha de solo lectura):
        // ni el guardado ni el dialogo de servicios operan sobre una composicion que no
        // se pudo leer.
        private bool ComposicionLeida_704ILR()
        {
            if (_editId_704ILR == 0 || _serviciosLeidos_704ILR) return true;
            ShowError_704ILR(MensajeServiciosNoLeidos_704ILR);
            return false;
        }

        // Primera capa del control de acceso DENTRO de la ficha: cada boton se
        // habilita solo si el perfil tiene el permiso que su accion va a exigir.
        // La seccion se abre con cualquiera de las hojas de reservas (consultar
        // disponibilidad, cobrar, restaurar...), asi que un perfil de solo cobros
        // veia Guardar, Servicios o Comprobante habilitados y cada clic terminaba en
        // "sin permiso" y en un asiento de advertencia en la bitacora por navegar
        // normalmente. La segunda capa (Exigir al ejecutar) se mantiene intacta.
        // 'editable' es la condicion de la reserva (una cancelada no admite cambios):
        // se combina con el permiso, nunca lo reemplaza. 'congelada' (RN-13) apaga la
        // edicion de los datos y de los servicios y deja los cobros y la documentacion.
        private void AplicarPermisosFicha_704ILR(bool editable_704ILR, bool congelada_704ILR = false)
        {
            bool edicion_704ILR = editable_704ILR && !congelada_704ILR;
            // Alta y edicion exigen permisos distintos; Servicios sigue al mismo
            // criterio porque cambia el monto de la operacion (CUN003, precondicion).
            bool gestion_704ILR = Permisos_704ILR.Tiene_704ILR(_editId_704ILR == 0 ? "RESERVA_CREAR" : "RESERVA_EDITAR");
            bool documenta_704ILR = Permisos_704ILR.TieneAlguno_704ILR("RESERVA_CREAR", "RESERVA_EDITAR");
            // Pagos, documentacion, historial y versiones actuan sobre una reserva ya
            // registrada (CUN004, precondicion): en el alta solo podian mostrar un error.
            bool registrada_704ILR = _editId_704ILR > 0;
            _puedeGuardar_704ILR           = edicion_704ILR && gestion_704ILR;
            _btnGuardar_704ILR.Enabled     = _puedeGuardar_704ILR;
            _btnServicios_704ILR.Enabled   = edicion_704ILR && gestion_704ILR;
            _btnPagos_704ILR.Enabled       = registrada_704ILR && editable_704ILR && Permisos_704ILR.TieneAlguno_704ILR("PAGOS_REGISTRAR", "PAGOS_ANULAR");
            _btnComprobante_704ILR.Enabled = registrada_704ILR && editable_704ILR && documenta_704ILR;
            _btnEmail_704ILR.Enabled       = registrada_704ILR && editable_704ILR && documenta_704ILR;
            _btnHistorial_704ILR.Enabled   = registrada_704ILR && Permisos_704ILR.Tiene_704ILR("RESERVA_HISTORIAL");
            _btnVersiones_704ILR.Enabled   = registrada_704ILR && Permisos_704ILR.TieneAlguno_704ILR("RESERVA_HISTORIAL", "RESERVA_RESTAURAR");
            // Los datos de una reserva que no admite cambios tampoco se editan en
            // pantalla (el aviso dice "no admite modificaciones" y Guardar esta apagado).
            // Va aca y no solo en AplicarModificabilidad: LimpiarForm tambien pasa por
            // este metodo y es el que los vuelve a habilitar.
            // Mismo criterio con el permiso: si el perfil no puede guardar esta ficha (sin
            // RESERVA_EDITAR en una reserva registrada o sin RESERVA_CREAR en un alta), sus campos
            // quedaban editables, lo tipeado no contaba como cambio pendiente y se perdia sin
            // aviso, con la ficha mostrando datos distintos de los de la base. El alta rapida de
            // cliente sigue la misma regla: elige al cliente en la ficha.
            bool camposEditables_704ILR = edicion_704ILR && gestion_704ILR;
            _cboCliente_704ILR.Enabled      = camposEditables_704ILR;
            _cboSalon_704ILR.Enabled        = camposEditables_704ILR;
            _dtFecha_704ILR.Enabled         = camposEditables_704ILR;
            _numInvitados_704ILR.Enabled    = camposEditables_704ILR;
            _cboEstado_704ILR.Enabled       = camposEditables_704ILR;
            _btnNuevoCliente_704ILR.Enabled = camposEditables_704ILR && Permisos_704ILR.Tiene_704ILR("CLIENTES_GESTION");
        }

        private void LimpiarForm_704ILR()
        {
            // Vaciar la seleccion dispara Grid_SelectionChanged: se hace con la seleccion
            // suspendida, asi no vuelve a cargar en la ficha la fila actual ni pregunta.
            _seleccionSuspendida_704ILR++;
            try { _grid_704ILR.ClearSelection(); }
            finally { _seleccionSuspendida_704ILR--; }

            _editId_704ILR = 0;
            _estadoDesconocido_704ILR = false;
            _fichaIncompleta_704ILR = null;
            ActualizarTituloForm_704ILR();
            AjustarEstadosDisponibles_704ILR();
            if (_cboCliente_704ILR.Items.Count > 0) _cboCliente_704ILR.SelectedIndex = 0;
            if (_cboSalon_704ILR.Items.Count > 0) _cboSalon_704ILR.SelectedIndex = 0;
            // El minimo vuelve a hoy: una reserva nueva no se agenda en el pasado.
            // (CargarEnForm lo baja cuando abre una reserva con el evento ya pasado.)
            _dtFecha_704ILR.CustomFormat = FormatoFecha_704ILR;
            _dtFecha_704ILR.MinDate = DateTime.Today;
            _dtFecha_704ILR.Value = DateTime.Today;
            MostrarInvitados_704ILR(0);   // tambien devuelve el campo a su rango de carga
            _cboEstado_704ILR.SelectedItem = EstadoReserva_704ILR.COTIZACION;
            _serviciosReserva_704ILR = new List<BE_ReservaServicio_704ILR>();
            _serviciosLeidos_704ILR = true;   // un alta nace sin servicios: vacia es su composicion real
            ActualizarMonto_704ILR();
            AplicarPermisosFicha_704ILR(editable_704ILR: true);
            _lblError_704ILR.Visible = false;
            _lineaBase_704ILR = FotoFicha_704ILR();
        }

        // IVistaConCambios: hay cambios sin guardar cuando la ficha se puede guardar y lo
        // que muestra difiere de su linea base (la reserva tal como se abrio, o el alta
        // limpia). Leer los invitados confirma lo tipeado, igual que al guardar.
        bool IVistaConCambios_704ILR.HayCambiosSinGuardar_704ILR =>
            !IsDisposed && _lineaBase_704ILR != null && _puedeGuardar_704ILR && FotoFicha_704ILR() != _lineaBase_704ILR;

        // Lo que Guardar enviaria, en una sola cadena comparable: cliente, salon, fecha,
        // invitados, estado y la composicion de servicios (sin importar el orden de las
        // lineas: aceptar el dialogo sin tocar nada no es un cambio).
        private string FotoFicha_704ILR()
        {
            var lineas_704ILR = new List<string>();
            foreach (var s_704ILR in _serviciosReserva_704ILR)
                lineas_704ILR.Add(s_704ILR.ServicioId_704ILR + "x" + s_704ILR.Cantidad_704ILR + "@" +
                    s_704ILR.PrecioUnitario_704ILR.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lineas_704ILR.Sort(StringComparer.Ordinal);
            return string.Join("|",
                _cboCliente_704ILR.SelectedValue?.ToString() ?? "",
                _cboSalon_704ILR.SelectedValue?.ToString() ?? "",
                _dtFecha_704ILR.Value.Date.ToString("yyyyMMdd"),
                _numInvitados_704ILR.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _cboEstado_704ILR.SelectedItem?.ToString() ?? "",
                _serviciosLeidos_704ILR ? string.Join(";", lineas_704ILR) : "-");
        }

        // true si hay cambios sin guardar en alguno de los tramos de la foto de la ficha que
        // cumplen el criterio (ver TramoEstado, TramoServicios y FotoFicha).
        private bool CambiosSinGuardarEn_704ILR(Func<int, bool> tramo_704ILR)
        {
            if (!((IVistaConCambios_704ILR)this).HayCambiosSinGuardar_704ILR) return false;
            string[] actual_704ILR = FotoFicha_704ILR().Split('|');
            string[] base_704ILR = _lineaBase_704ILR.Split('|');
            for (int i_704ILR = 0; i_704ILR < actual_704ILR.Length && i_704ILR < base_704ILR.Length; i_704ILR++)
                if (tramo_704ILR(i_704ILR) && actual_704ILR[i_704ILR] != base_704ILR[i_704ILR]) return true;
            return false;
        }

        // RN-05: dar de baja es una transicion sobre una operacion ya registrada, no un
        // estado inicial. En el alta el combo no ofrece CANCELADA; al editar, si.
        private void AjustarEstadosDisponibles_704ILR()
        {
            // En el alta solo se ofrecen los dos estados con los que una reserva puede
            // nacer. CANCELADA es terminal y se llega por la via de cancelacion (RN-05);
            // CONFIRMADA exige el adelanto cobrado, que necesita la reserva ya
            // registrada (RN-07): se confirma despues, editandola.
            bool alta_704ILR = _editId_704ILR == 0;
            AjustarEstado_704ILR(EstadoReserva_704ILR.CANCELADA, !alta_704ILR);
            AjustarEstado_704ILR(EstadoReserva_704ILR.CONFIRMADA, !alta_704ILR);
        }

        // Deja el estado presente o ausente en el combo, sin duplicarlo ni perder la
        // seleccion actual si sigue siendo valida. Un estado que vuelve a la lista se inserta en
        // su lugar del ciclo de vida (OrdenEstados): agregado al final, al abrir una reserva el
        // combo quedaba Cotizacion, Pendiente, Cancelada, Confirmada y dos flechas abajo desde
        // Cotizacion elegian Cancelada.
        private void AjustarEstado_704ILR(EstadoReserva_704ILR estado_704ILR, bool presente_704ILR)
        {
            bool esta_704ILR = _cboEstado_704ILR.Items.Contains(estado_704ILR);
            if (presente_704ILR && !esta_704ILR)
            {
                int orden_704ILR = Array.IndexOf(OrdenEstados_704ILR, estado_704ILR);
                int indice_704ILR = 0;
                while (indice_704ILR < _cboEstado_704ILR.Items.Count &&
                       _cboEstado_704ILR.Items[indice_704ILR] is EstadoReserva_704ILR previo_704ILR &&
                       Array.IndexOf(OrdenEstados_704ILR, previo_704ILR) < orden_704ILR)
                    indice_704ILR++;
                _cboEstado_704ILR.Items.Insert(indice_704ILR, estado_704ILR);
            }
            else if (!presente_704ILR && esta_704ILR) _cboEstado_704ILR.Items.Remove(estado_704ILR);
        }

        // Refleja el total (suma de servicios) en el campo Monto y el conteo en el boton.
        private void ActualizarMonto_704ILR()
        {
            // "N2" es el mismo formato con el que la grilla muestra la columna Monto:
            // el total no puede verse de dos maneras distintas en la misma pantalla.
            // Sin la composicion leida no hay total que mostrar: un 0,00 pareceria un dato.
            _txtMonto_704ILR.Text = _serviciosLeidos_704ILR
                ? BLL_ReservaServicio_704ILR.Total_704ILR(_serviciosReserva_704ILR).ToString("N2")
                : "-";
            // Un importe de diez cifras no entra en media ficha con la letra de los campos:
            // se muestra con la letra chica antes que cortarle el ultimo digito.
            bool entra_704ILR = _txtMonto_704ILR.ClientSize.Width <= 0 ||
                TextRenderer.MeasureText(_txtMonto_704ILR.Text, Theme_704ILR.FontInput_704ILR, Size.Empty, TextFormatFlags.NoPadding).Width + 4 <= _txtMonto_704ILR.ClientSize.Width;
            _txtMonto_704ILR.Font = entra_704ILR ? Theme_704ILR.FontInput_704ILR : Theme_704ILR.FontSmall_704ILR;
            if (_btnServicios_704ILR != null)
                _btnServicios_704ILR.Text = Tr_704ILR.T_704ILR("MENU_SERVICIOS") + " (" +
                    (_serviciosLeidos_704ILR ? _serviciosReserva_704ILR.Count.ToString() : "-") + ")";
        }

        private void EditarServicios_704ILR()
        {
            // Segunda capa: los servicios contratados componen el monto de la
            // operacion, asi que cargarlos es parte del alta o de la edicion y exige
            // el mismo permiso que Guardar (CUN003, precondicion). Sin esto, un perfil
            // de solo consulta podia abrir el dialogo y recalcular el monto.
            if (!Permisos_704ILR.Exigir_704ILR(_editId_704ILR == 0 ? "RESERVA_CREAR" : "RESERVA_EDITAR", FindForm(),
                    "cargar los servicios de la reserva" + ReferenciaEnEdicion_704ILR()))
                return;
            if (!ComposicionLeida_704ILR()) return;
            if (!EstadoConocido_704ILR()) return;
            if (!FichaCompleta_704ILR()) return;
            try
            {
                using (var dlg_704ILR = new frmReservaServicios_704ILR(_serviciosReserva_704ILR, BLL_Servicio_704ILR.GetActivos_704ILR()))
                {
                    if (dlg_704ILR.ShowDialog(FindForm()) == DialogResult.OK)
                    {
                        _serviciosReserva_704ILR = dlg_704ILR.Items_704ILR;
                        ActualizarMonto_704ILR();
                    }
                }
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Editar servicios de la reserva");
                ShowError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        // Consulta de disponibilidad (Proceso 1, paso 1): abre el dialogo y, si
        // el vendedor elige un salon (con la fecha pedida o con la propuesta
        // alternativa), precarga la ficha para continuar la carga de la reserva.
        private void ConsultarDisponibilidad_704ILR()
        {
            if (!Permisos_704ILR.Exigir_704ILR("DISPONIBILIDAD_CONSULTAR", FindForm(), "consultar disponibilidad de salones")) return;
            try
            {
                // La consulta arranca con los invitados de la ficha acotados al rango de carga: la ficha
                // de una reserva con una cantidad registrada fuera de ese rango la muestra tal cual (ver
                // MostrarInvitados), y el campo del dialogo no la admite.
                int invitadosFicha_704ILR = (int)Math.Min(Math.Max(_numInvitados_704ILR.Value, 0m), BLL_Reserva_704ILR.InvitadosMaximo_704ILR);
                using (var dlg_704ILR = new frmDisponibilidad_704ILR(_dtFecha_704ILR.Value.Date, invitadosFicha_704ILR))
                {
                    if (dlg_704ILR.ShowDialog(FindForm()) != DialogResult.OK) return;

                    // El dialogo ya no propone fechas fuera del calendario; si igual
                    // llegara una posterior al maximo, no se acota en silencio (llevaria
                    // la reserva a otro dia): se informa que no hay fecha utilizable.
                    DateTime fechaElegida_704ILR = dlg_704ILR.FechaSeleccionada_704ILR;
                    if (fechaElegida_704ILR > _dtFecha_704ILR.MaxDate)
                    {
                        ShowError_704ILR(() => T_704ILR("DISP_SIN_PROPUESTA", "El salón no tiene fechas libres en el horizonte consultado."), resueltoPorPrecarga_704ILR: true);
                        return;
                    }

                    // La consulta arranca una reserva nueva: la ficha se limpia y se
                    // precarga con lo elegido. Una edicion con cambios sin guardar no se
                    // descarta sin preguntar; con "No" la precarga no se aplica.
                    if (_editId_704ILR != 0)
                    {
                        if (!ConfirmarDescarte_704ILR()) return;
                        LimpiarForm_704ILR();
                    }
                    // El salon se elige por id y, si no esta en la lista (dado de alta en otra
                    // sesion despues de abrir la pantalla), se recarga: la ficha no puede quedar
                    // con otro salon elegido junto a la fecha y los invitados de la consulta.
                    bool salonCargado_704ILR = ElegirPorId_704ILR(_cboSalon_704ILR, dlg_704ILR.SalonSeleccionado_704ILR, CargarSalones_704ILR, out bool salonesLeidos_704ILR);
                    _dtFecha_704ILR.Value = fechaElegida_704ILR < _dtFecha_704ILR.MinDate ? _dtFecha_704ILR.MinDate : fechaElegida_704ILR;
                    // Los invitados con los que se consulto quedan en la ficha: es el
                    // dato que despues valida la RN-06 al confirmar.
                    _numInvitados_704ILR.Value = Math.Min(dlg_704ILR.InvitadosConsultados_704ILR, _numInvitados_704ILR.Maximum);
                    // Con el salon, la fecha y los invitados de la consulta cargados, el aviso que
                    // estuviera a la vista deja de corresponder SOLO si es de los que la precarga
                    // resuelve (un salon invalido u ocupado, una fecha invalida, una capacidad
                    // insuficiente, una consulta anterior sin propuesta): el de un cliente sin
                    // elegir, por ejemplo, sigue vigente. En el alta no pasa por LimpiarForm, que
                    // es quien lo ocultaba al editar.
                    // Si el salon no quedo elegido porque la lista no se pudo releer (la base no respondio),
                    // queda a la vista el aviso de esa falla, que mostro CargarSalones, y no el de un salon
                    // invalido; una precarga posterior que si elija el salon lo resuelve igual.
                    if (salonCargado_704ILR)
                    {
                        if (_avisoResueltoPorPrecarga_704ILR) _lblError_704ILR.Visible = false;
                    }
                    else if (salonesLeidos_704ILR)
                        ShowError_704ILR(() => T_704ILR("MSG_RES_SALON", "Seleccione un salón válido."), resueltoPorPrecarga_704ILR: true);
                    else
                        _avisoResueltoPorPrecarga_704ILR = true;
                }
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Consultar disponibilidad");
                ShowError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        // Abre el dialogo de pagos. Requiere una reserva guardada (los pagos se
        // registran contra su Id y su Monto = total ya persistido).
        private void EditarPagos_704ILR()
        {
            // Segunda capa: el dialogo vuelve a exigir cada accion (registrar o
            // anular) por separado; aca se corta a quien no tiene ninguna de las dos.
            if (!Permisos_704ILR.ExigirAlguno_704ILR(FindForm(),
                    "abrir los pagos de la reserva" + ReferenciaEnEdicion_704ILR(),
                    "PAGOS_REGISTRAR", "PAGOS_ANULAR"))
                return;
            if (_editId_704ILR == 0)
            {
                ShowError_704ILR(() => Tr_704ILR.T_704ILR("MSG_PAGO_GUARDAR_RESERVA"));
                return;
            }
            if (!EstadoConocido_704ILR()) return;
            if (!FichaCompleta_704ILR()) return;
            // El dialogo muestra y valida los cobros contra el total GUARDADO de la reserva
            // (RN-04). Con los servicios cambiados y sin guardar, ese no es el total que muestra
            // la ficha: se cobraba el importe viejo y despues Guardar quedaba trabado por la
            // RN-04. Se pide guardar primero. Los demas cambios sin guardar no alteran lo que el
            // dialogo muestra ni valida (el estado CONFIRMADA ya elegido, por ejemplo: la RN-07
            // pide guardar, cobrar el adelanto y recien entonces confirmar).
            if (CambiosSinGuardarEn_704ILR(t_704ILR => t_704ILR == TramoServicios_704ILR))
            {
                ShowError_704ILR(() => T_704ILR("MSG_RES_CAMBIOS_PAGOS", "Los servicios de la reserva tienen cambios sin guardar: guarde la reserva antes de registrar pagos."));
                return;
            }
            try
            {
                using (var dlg_704ILR = new frmReservaPagos_704ILR(_editId_704ILR, BLL_Pago_704ILR.MontoReserva_704ILR(_editId_704ILR)))
                    dlg_704ILR.ShowDialog(FindForm());
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Abrir pagos de la reserva #" + _editId_704ILR);
                ShowError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        // Genera el comprobante/presupuesto HTML de la reserva, lo guarda donde el
        // usuario elija y lo abre en el navegador para imprimir (Proceso 1, paso 6).
        private void GenerarComprobante_704ILR()
        {
            // El comprobante vuelca al documento el DNI, el correo y el telefono del
            // cliente descifrados: emitirlo es parte de la gestion de la reserva y no
            // de su consulta, asi que exige el permiso de gestion (CUN005, precondicion).
            if (!Permisos_704ILR.ExigirAlguno_704ILR(FindForm(),
                    "emitir el comprobante de la reserva" + ReferenciaEnEdicion_704ILR(),
                    "RESERVA_CREAR", "RESERVA_EDITAR"))
                return;
            if (_editId_704ILR == 0)
            {
                ShowError_704ILR(() => T_704ILR("MSG_RES_GUARDAR_PRIMERO", "Guarde la reserva antes de emitir su documentación."));
                return;
            }
            if (!FichaCompleta_704ILR()) return;
            try
            {
                if (ReservaEmitible_704ILR("Emision de comprobante rechazada") == null) return;
                if (!SinCambiosParaDocumentar_704ILR()) return;
                string html_704ILR = ComprobanteService_704ILR.GenerarHtml_704ILR(_editId_704ILR);
                if (html_704ILR == null) { ShowError_704ILR(() => T_704ILR("MSG_RES_NOTFOUND", "La reserva ya no existe.")); return; }

                using (var dlg_704ILR = new SaveFileDialog
                {
                    Title = T_704ILR("RES_COMPROBANTE_BTN", "Comprobante"),
                    Filter = FiltroComprobante_704ILR(),
                    FileName = NombreArchivoComprobante_704ILR(_editId_704ILR),
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                })
                {
                    if (dlg_704ILR.ShowDialog(FindForm()) != DialogResult.OK) return;
                    string ruta_704ILR = dlg_704ILR.FileName;
                    System.IO.File.WriteAllText(ruta_704ILR, html_704ILR, System.Text.Encoding.UTF8);
                    BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Comprobante generado", CriticidadBitacora_704ILR.Info,
                        "Comprobante de la reserva #" + _editId_704ILR);
                    // El comprobante ya quedo guardado y asentado: si no se puede abrir (ningun programa
                    // asociado a .html), la emision no fallo. Se asienta la falla de apertura y se avisa
                    // donde quedo el archivo, en lugar del aviso de operacion no completada.
                    Exception falloApertura_704ILR = AbrirConElSistema_704ILR(
                        new System.Diagnostics.ProcessStartInfo(ruta_704ILR) { UseShellExecute = true });
                    if (falloApertura_704ILR != null)
                    {
                        BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(falloApertura_704ILR, "Reservas",
                            "Abrir el comprobante de la reserva #" + _editId_704ILR + " guardado en " + ruta_704ILR);
                        MessageBox.Show(T_704ILR("MSG_CMP_NO_ABIERTO", "El comprobante se guardó, pero no se pudo abrir automáticamente. El archivo quedó en:") +
                            Environment.NewLine + Environment.NewLine + ruta_704ILR,
                            "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Generar comprobante");
                ShowError_704ILR(() => T_704ILR("MSG_OP_ERROR", "No se pudo completar la operación."));
            }
        }

        // El comprobante y el correo se arman con la reserva GUARDADA (cliente, fecha,
        // servicios, total y pagos de la base). Con cambios sin guardar en la ficha el documento
        // no seria el que se ve en pantalla: se pide guardar primero (CUN005: el comprobante se
        // emite sobre la operacion ya guardada).
        private bool SinCambiosParaDocumentar_704ILR()
        {
            if (!((IVistaConCambios_704ILR)this).HayCambiosSinGuardar_704ILR) return true;
            ShowError_704ILR(() => T_704ILR("MSG_RES_CAMBIOS_DOCUMENTOS", "La reserva tiene cambios sin guardar: gu\u00E1rdela antes de emitir su documentaci\u00F3n."));
            return false;
        }

        // CMP_FILTER es una traduccion editable desde Idiomas. Si el texto guardado no es
        // un filtro que el cuadro "Guardar como" acepte (pares "descripcion|patron", sin
        // partes vacias), SaveFileDialog.Filter lanzaba, el cuadro no se abria y el boton
        // quedaba inutil en ese idioma con un aviso generico: se usa el filtro por defecto.
        private static string FiltroComprobante_704ILR()
        {
            string filtro_704ILR = Tr_704ILR.T_704ILR("CMP_FILTER") ?? string.Empty;
            string[] partes_704ILR = filtro_704ILR.Split('|');
            bool valido_704ILR = partes_704ILR.Length >= 2 && partes_704ILR.Length % 2 == 0;
            foreach (string p_704ILR in partes_704ILR)
                // Misma regla que el editor de idiomas: un tramo que no se ve (vacio, espacios o
                // solo caracteres invisibles o de relleno) no sirve como descripcion ni patron.
                if (GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(p_704ILR)) valido_704ILR = false;
            return valido_704ILR ? filtro_704ILR : FiltroComprobantePorDefecto_704ILR;
        }

        // Nombre del archivo del comprobante: prefijo traducible (CMP_FILENAME) + numero de
        // reserva. Los caracteres que un nombre de archivo no admite (barras, dos puntos,
        // asterisco, signo de pregunta, comillas, menor, mayor, barra vertical) se
        // reemplazan por "_": sin eso el cuadro o la escritura en Documentos fallaban, o
        // una barra mandaba el archivo a otra carpeta.
        // Ademas el prefijo se acota a 100 caracteres (con uno de 250 el nombre superaba lo que
        // Windows admite y la escritura lanzaba) y pierde los puntos y espacios del final. Si el
        // nombre armado es un nombre reservado de Windows (CON, PRN, AUX, NUL, COM0-9, LPT0-9,
        // tambien con extension: "COM" + reserva 1 = COM1.html), la escritura lanzaba o iba a
        // un dispositivo y el documento se perdia en silencio: se usa el prefijo por defecto.
        private static string NombreArchivoComprobante_704ILR(int reservaId_704ILR)
        {
            string prefijo_704ILR = T_704ILR("CMP_FILENAME", PrefijoComprobantePorDefecto_704ILR) ?? string.Empty;
            char[] invalidos_704ILR = System.IO.Path.GetInvalidFileNameChars();
            var sb_704ILR = new System.Text.StringBuilder(prefijo_704ILR.Length);
            foreach (char c_704ILR in prefijo_704ILR)
                sb_704ILR.Append(Array.IndexOf(invalidos_704ILR, c_704ILR) >= 0 ? '_' : c_704ILR);
            string limpio_704ILR = sb_704ILR.ToString().Trim();
            if (limpio_704ILR.Length > LargoMaximoPrefijo_704ILR)
            {
                int corte_704ILR = LargoMaximoPrefijo_704ILR;
                // No se parte un caracter fuera del plano basico (par sustituto).
                if (char.IsHighSurrogate(limpio_704ILR[corte_704ILR - 1])) corte_704ILR--;
                limpio_704ILR = limpio_704ILR.Substring(0, corte_704ILR);
            }
            limpio_704ILR = limpio_704ILR.TrimEnd('.', ' ');
            string nombre_704ILR = limpio_704ILR + reservaId_704ILR + ".html";
            return NombreReservado_704ILR(nombre_704ILR)
                ? PrefijoComprobantePorDefecto_704ILR + reservaId_704ILR + ".html"
                : nombre_704ILR;
        }

        // Windows reserva esos nombres con cualquier extension: cuenta lo anterior al primer
        // punto, sin los espacios finales ("nul .html" tambien es NUL).
        private static bool NombreReservado_704ILR(string nombre_704ILR)
        {
            int punto_704ILR = nombre_704ILR.IndexOf('.');
            string raiz_704ILR = (punto_704ILR >= 0 ? nombre_704ILR.Substring(0, punto_704ILR) : nombre_704ILR).TrimEnd(' ');
            return NombresReservados_704ILR.Contains(raiz_704ILR);
        }

        // Envia el comprobante por email (PN1, paso 6). Sin SMTP: genera y guarda el
        // comprobante, abre el cliente de correo (mailto) con destinatario/asunto/
        // cuerpo prellenados y abre la carpeta del archivo para adjuntarlo.
        private void EnviarEmail_704ILR()
        {
            // Misma exigencia que el comprobante: el correo lleva el mismo documento
            // con los datos de contacto del cliente.
            if (!Permisos_704ILR.ExigirAlguno_704ILR(FindForm(),
                    "remitir el comprobante de la reserva" + ReferenciaEnEdicion_704ILR(),
                    "RESERVA_CREAR", "RESERVA_EDITAR"))
                return;
            if (_editId_704ILR == 0)
            {
                ShowError_704ILR(() => T_704ILR("MSG_RES_GUARDAR_PRIMERO", "Guarde la reserva antes de emitir su documentación."));
                return;
            }
            if (!FichaCompleta_704ILR()) return;
            try
            {
                // La lectura de la reserva y del cliente va DENTRO del try: son dos
                // accesos a la base y una falla ahi tiene que asentarse igual que las
                // demas, no tumbar la aplicacion.
                var reserva_704ILR = ReservaEmitible_704ILR("Envio de comprobante rechazado");
                if (reserva_704ILR == null) return;
                if (!SinCambiosParaDocumentar_704ILR()) return;
                var cliente_704ILR = reserva_704ILR.ClienteId_704ILR > 0 ? BLL_Cliente_704ILR.GetById_704ILR(reserva_704ILR.ClienteId_704ILR) : null;
                // Antes de escribir nada se decide si hay a quien escribirle: sin correo, o con un correo
                // que este equipo no puede leer, no se guarda el adjunto ni se abre el programa de correo.
                switch (EvaluarDestinatario_704ILR(cliente_704ILR?.Email_704ILR))
                {
                    case DestinatarioCorreo_704ILR.SinCorreo_704ILR:
                        ShowError_704ILR(() => T_704ILR("MSG_EMAIL_SIN_CORREO", "El cliente no tiene email cargado."));
                        return;
                    case DestinatarioCorreo_704ILR.Ilegible_704ILR:
                        // El paquete cifrado no se copia al asiento, igual que el correo en claro (ver abajo).
                        BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Envio de comprobante rechazado", CriticidadBitacora_704ILR.Advertencia,
                            "Reserva #" + _editId_704ILR + " -> cliente #" + cliente_704ILR.Id_704ILR +
                            ": su email esta cifrado con una clave que este equipo no puede leer; no se preparo el envio.");
                        ShowError_704ILR(() => T_704ILR("MSG_EMAIL_ILEGIBLE", "El email del cliente no se puede leer en este equipo (quedó cifrado con la clave de otra instalación): vuelva a cargarlo en la ficha del cliente."));
                        return;
                }

                // Todo lo que lee la base o arma el mensaje va antes de escribir el adjunto: una falla ahi
                // no deja un archivo suelto en Documentos.
                string html_704ILR = ComprobanteService_704ILR.GenerarHtml_704ILR(_editId_704ILR);
                if (html_704ILR == null) { ShowError_704ILR(() => T_704ILR("MSG_RES_NOTFOUND", "La reserva ya no existe.")); return; }
                decimal saldo_704ILR = BLL_Pago_704ILR.Saldo_704ILR(_editId_704ILR);
                string mailto_704ILR = ArmarMailto_704ILR(cliente_704ILR.Email_704ILR, AsuntoEmail_704ILR(_editId_704ILR),
                    ArmarCuerpoEmail_704ILR(cliente_704ILR.NombreCompleto_704ILR, _editId_704ILR, reserva_704ILR.SalonNombre_704ILR,
                        reserva_704ILR.FechaEvento_704ILR, reserva_704ILR.Monto_704ILR, saldo_704ILR));

                string path_704ILR = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    NombreArchivoComprobante_704ILR(_editId_704ILR));
                System.IO.File.WriteAllText(path_704ILR, html_704ILR, System.Text.Encoding.UTF8);

                // Con el adjunto ya guardado, abrir el correo y abrir la carpeta tienen cada uno su propio
                // manejo de error: sin programa de correo asociado la carpeta se abre igual (el adjunto se
                // envia a mano) y el aviso dice donde quedo el archivo, en lugar de informar como fallida
                // una preparacion que dejo el documento escrito.
                Exception falloCorreo_704ILR = AbrirConElSistema_704ILR(
                    new System.Diagnostics.ProcessStartInfo(mailto_704ILR) { UseShellExecute = true });
                Exception falloCarpeta_704ILR = AbrirConElSistema_704ILR(
                    new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + path_704ILR + "\"") { UseShellExecute = true });

                // El asiento describe lo que el sistema REALMENTE hizo: preparo el
                // documento y abrio (o no pudo abrir) el cliente de correo y la carpeta. El
                // envio lo completa la persona, y el sistema no puede verificarlo.
                // El correo del cliente no se copia al detalle: es un dato que la base
                // guarda cifrado y volcarlo en claro en la bitacora anularia el
                // cifrado. Se identifica al cliente por su numero.
                string detalle_704ILR = "Reserva #" + _editId_704ILR + " -> cliente #" + cliente_704ILR.Id_704ILR;
                if (falloCorreo_704ILR == null && falloCarpeta_704ILR == null)
                    detalle_704ILR += ", correo abierto y adjunto guardado en " + path_704ILR;
                else
                {
                    detalle_704ILR += ", adjunto guardado en " + path_704ILR + (falloCorreo_704ILR == null
                        ? "; correo abierto"
                        : "; no se pudo abrir el programa de correo: " + DescripcionFallo_704ILR(falloCorreo_704ILR));
                    if (falloCarpeta_704ILR != null)
                        detalle_704ILR += "; no se pudo abrir la carpeta del adjunto: " + DescripcionFallo_704ILR(falloCarpeta_704ILR);
                }
                BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Comprobante preparado para envio",
                    falloCorreo_704ILR == null && falloCarpeta_704ILR == null ? CriticidadBitacora_704ILR.Info : CriticidadBitacora_704ILR.Advertencia,
                    detalle_704ILR);

                if (falloCorreo_704ILR != null)
                    MessageBox.Show(T_704ILR("MSG_EMAIL_SIN_PROGRAMA", "No se pudo abrir un programa de correo para preparar el mensaje. El comprobante quedó guardado para adjuntarlo en:") +
                        Environment.NewLine + Environment.NewLine + path_704ILR, "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else if (falloCarpeta_704ILR != null)
                    MessageBox.Show(T_704ILR("MSG_EMAIL_SIN_CARPETA", "Se abrió el correo con el mensaje listo, pero no se pudo abrir la carpeta del comprobante. El archivo para adjuntar quedó en:") +
                        Environment.NewLine + Environment.NewLine + path_704ILR, "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                    MessageBox.Show(T_704ILR("MSG_EMAIL_ADJUNTAR", "Se abrió tu correo con el mensaje listo. Adjunta el comprobante (abrimos su carpeta) y envialo."),
                        "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Enviar comprobante por email");
                ShowError_704ILR(() => T_704ILR("MSG_OP_ERROR", "No se pudo completar la operación."));
            }
        }

        // Destinatario del correo con el comprobante. Un email vacio o de espacios cuenta como no
        // cargado. Un email con la forma exacta de un paquete cifrado es un dato que este equipo no
        // pudo abrir (la base se restauro en otra PC y la clave local no lo descifra, asi que la
        // lectura lo devuelve tal cual; ver BLL_Cliente_704ILR.Validar): no es una direccion, y
        // armar el correo dirigido a ese texto dejaba el adjunto en Documentos y el mensaje sin
        // destinatario valido. Funcion pura: no lee la base ni la clave de cifrado.
        private static DestinatarioCorreo_704ILR EvaluarDestinatario_704ILR(string email_704ILR)
        {
            if (string.IsNullOrWhiteSpace(email_704ILR)) return DestinatarioCorreo_704ILR.SinCorreo_704ILR;
            return CryptoService_704ILR.EstaProtegido_704ILR(email_704ILR.Trim())
                ? DestinatarioCorreo_704ILR.Ilegible_704ILR
                : DestinatarioCorreo_704ILR.Valido_704ILR;
        }

        // Asunto del correo con el comprobante.
        private static string AsuntoEmail_704ILR(int reservaId_704ILR) =>
            T_704ILR("EMAIL_ASUNTO", "Comprobante de reserva") + " #" + reservaId_704ILR;

        // Cuerpo del correo con el comprobante. La fecha del evento va con patron fijo y la cultura
        // invariante, como en la grilla y en el comprobante (calendario gregoriano con cualquier
        // configuracion regional: con la de la estacion salia 2570 en th-TH); los importes, con la
        // configuracion regional, como el campo Monto.
        private static string ArmarCuerpoEmail_704ILR(string cliente_704ILR, int reservaId_704ILR, string salon_704ILR,
            DateTime fechaEvento_704ILR, decimal total_704ILR, decimal saldo_704ILR)
        {
            var cuerpo_704ILR = new System.Text.StringBuilder();
            cuerpo_704ILR.Append(Tr_704ILR.F_704ILR("EMAIL_SALUDO", "Hola {0},", cliente_704ILR)).Append("\n\n");
            cuerpo_704ILR.Append(Tr_704ILR.F_704ILR("EMAIL_INTRO", "Le enviamos el comprobante de su reserva #{0}.", reservaId_704ILR)).Append("\n\n");
            cuerpo_704ILR.Append(T_704ILR("COL_SALON", "Salón")).Append(": ").Append(salon_704ILR).Append("\n");
            cuerpo_704ILR.Append(T_704ILR("RES_LBL_FECHA", "Fecha del evento")).Append(": ")
                .Append(fechaEvento_704ILR.ToString(FormatoFecha_704ILR, System.Globalization.CultureInfo.InvariantCulture)).Append("\n");
            cuerpo_704ILR.Append(T_704ILR("LBL_TOTAL", "Total")).Append(": ").Append(total_704ILR.ToString("N2")).Append("\n");
            cuerpo_704ILR.Append(T_704ILR("LBL_SALDO", "Saldo")).Append(": ").Append(saldo_704ILR.ToString("N2")).Append("\n\n");
            cuerpo_704ILR.Append(T_704ILR("EMAIL_CIERRE", "Adjuntamos el comprobante. Saludos, EvenTech."));
            return cuerpo_704ILR.ToString();
        }

        // Enlace mailto (RFC 6068) del correo con el comprobante. El destinatario lleva la arroba
        // literal: se escapan por separado la parte local y el dominio, a cada lado de la ULTIMA
        // arroba. Con la direccion entera escapada la arroba viajaba como %40 y algunos programas
        // de correo mostraban asi el destinatario o no lo reconocian. Escapar cada parte impide
        // ademas que un caracter de la direccion ('?', '&', '#') agregue campos al enlace. Asunto y
        // cuerpo van escapados enteros.
        private static string ArmarMailto_704ILR(string destinatario_704ILR, string asunto_704ILR, string cuerpo_704ILR)
        {
            string direccion_704ILR = (destinatario_704ILR ?? string.Empty).Trim();
            int arroba_704ILR = direccion_704ILR.LastIndexOf('@');
            string para_704ILR = arroba_704ILR < 0
                ? Uri.EscapeDataString(direccion_704ILR)
                : Uri.EscapeDataString(direccion_704ILR.Substring(0, arroba_704ILR)) + "@" +
                  Uri.EscapeDataString(direccion_704ILR.Substring(arroba_704ILR + 1));
            return "mailto:" + para_704ILR
                + "?subject=" + Uri.EscapeDataString(asunto_704ILR ?? string.Empty)
                // RFC 6068: los saltos de linea del cuerpo van como CRLF (%0D%0A).
                + "&body=" + Uri.EscapeDataString((cuerpo_704ILR ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n"));
        }

        // Abre un documento, un enlace o un programa con el shell de Windows. Devuelve null si se
        // abrio, o la excepcion si no (sin programa asociado, archivo inexistente...), para que quien
        // llama asiente y avise lo que realmente paso sin perder lo que ya hizo.
        private static Exception AbrirConElSistema_704ILR(System.Diagnostics.ProcessStartInfo inicio_704ILR)
        {
            try
            {
                System.Diagnostics.Process.Start(inicio_704ILR)?.Dispose();
                return null;
            }
            catch (Exception ex_704ILR)
            {
                return ex_704ILR;
            }
        }

        // Motivo de un fallo de apertura para la bitacora. El mensaje que arma Process.Start incluye
        // el archivo o el enlace que se quiso abrir, y en el correo ese enlace es el mailto completo
        // (email del cliente en claro, su nombre e importes): copiarlo al detalle anularia el cifrado
        // del email. Se asienta el texto de Windows para el codigo de error, sin el enlace, o solo el
        // tipo de la excepcion.
        private static string DescripcionFallo_704ILR(Exception ex_704ILR)
        {
            if (ex_704ILR is System.ComponentModel.Win32Exception win32_704ILR)
                return new System.ComponentModel.Win32Exception(win32_704ILR.NativeErrorCode).Message + " (" + win32_704ILR.NativeErrorCode + ")";
            return ex_704ILR.GetType().Name;
        }

        // Punto unico de entrada del guardado. Toda la operatoria de escritura de la
        // ficha (alta, edicion, cancelacion y renovacion) toca la base varias veces:
        // una falla ahi se asienta en la bitacora y se informa en pantalla, igual que
        // en la carga de datos, en lugar de terminar la aplicacion.
        private void Guardar_704ILR()
        {
            // El id se toma ANTES de guardar: una recarga de la grilla en el medio mueve
            // la ficha a otra fila y el asiento nombraria otra reserva.
            int idEnCurso_704ILR = _editId_704ILR;
            try
            {
                GuardarReserva_704ILR();
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas",
                    idEnCurso_704ILR == 0 ? "Guardar reserva nueva" : "Guardar reserva #" + idEnCurso_704ILR);
                ShowError_704ILR(() => Tr_704ILR.MensajeExcepcion_704ILR(ex_704ILR));
            }
        }

        private void GuardarReserva_704ILR()
        {
            // Segunda capa del control de acceso: el alta y la edicion exigen su
            // propio permiso al ejecutarse, no solo al mostrar la seccion.
            string requerido_704ILR = _editId_704ILR == 0 ? "RESERVA_CREAR" : "RESERVA_EDITAR";
            if (!Permisos_704ILR.Exigir_704ILR(requerido_704ILR, FindForm(),
                    _editId_704ILR == 0 ? "crear una reserva" : "editar la reserva #" + _editId_704ILR))
                return;
            if (!ComposicionLeida_704ILR()) return;
            if (!EstadoConocido_704ILR()) return;
            if (!FichaCompleta_704ILR()) return;

            _lblError_704ILR.Visible = false;

            // El monto es la suma de los servicios contratados (no se ingresa a mano).
            decimal monto_704ILR = BLL_ReservaServicio_704ILR.Total_704ILR(_serviciosReserva_704ILR);

            var reserva_704ILR = new BE_Reserva_704ILR
            {
                Id_704ILR = _editId_704ILR,
                ClienteId_704ILR = _cboCliente_704ILR.SelectedValue is int cid_704ILR ? cid_704ILR : 0,
                SalonId_704ILR = _cboSalon_704ILR.SelectedValue is int sid_704ILR ? sid_704ILR : 0,
                FechaEvento_704ILR = _dtFecha_704ILR.Value.Date,
                Estado_704ILR = _cboEstado_704ILR.SelectedItem is EstadoReserva_704ILR es_704ILR ? es_704ILR : EstadoReserva_704ILR.COTIZACION,
                CantidadInvitados_704ILR = (int)_numInvitados_704ILR.Value,
                Monto_704ILR = monto_704ILR
            };

            int idReserva_704ILR = _editId_704ILR;

            // RN-02: pasar a CANCELADA no es una edicion mas. Se calcula la politica
            // de cancelacion, se le muestra al vendedor y se confirma antes de aplicarla.
            if (_editId_704ILR != 0 && reserva_704ILR.Estado_704ILR == EstadoReserva_704ILR.CANCELADA)
            {
                BE_Reserva_704ILR actual_704ILR = BLL_Reserva_704ILR.GetById_704ILR(_editId_704ILR);
                if (actual_704ILR != null && actual_704ILR.Estado_704ILR != EstadoReserva_704ILR.CANCELADA)
                {
                    BLL_Reserva_704ILR.CalcularCancelacion_704ILR(actual_704ILR,
                        out decimal ret_704ILR, out decimal reem_704ILR);
                    // La pregunta anticipa lo que VA a pasar (condicional): el aviso en
                    // pasado ("Reserva cancelada...") corresponde despues del exito, no
                    // antes de que el vendedor decida.
                    // F_704ILR cae al texto por defecto si la plantilla guardada no formatea.
                    string aviso_704ILR = Tr_704ILR.F_704ILR("MSG_RES_CANCELAR", "¿Cancelar la reserva #{0}?", _editId_704ILR);
                    if (ret_704ILR > 0 || reem_704ILR > 0)
                        aviso_704ILR += Environment.NewLine + Environment.NewLine + Tr_704ILR.F_704ILR("MSG_RES_CANCELAR_DETALLE",
                            "Si se cancela hoy se retienen {0:N2} y se reintegran {1:N2}.", ret_704ILR, reem_704ILR);
                    // La baja aplica solo el cambio de estado: los demas cambios cargados en la
                    // ficha (invitados, servicios, fecha...) no se guardan. Se advierte en la
                    // misma pregunta, asi el vendedor puede responder No y guardarlos antes.
                    if (CambiosSinGuardarEn_704ILR(t_704ILR => t_704ILR != TramoEstado_704ILR))
                        aviso_704ILR += Environment.NewLine + Environment.NewLine + T_704ILR("MSG_RES_CANCELAR_OTROS_CAMBIOS",
                            "Los dem\u00E1s cambios sin guardar de la ficha se descartar\u00E1n: solo se aplica la cancelaci\u00F3n.");
                    // "No" es la respuesta por defecto, como en la pregunta de descartar cambios: la baja
                    // es terminal (RN-05) y un Enter apurado no la aplica.
                    if (MessageBox.Show(aviso_704ILR, "EvenTech",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;

                    var rc_704ILR = BLL_Reserva_704ILR.Cancelar_704ILR(_editId_704ILR,
                        out ret_704ILR, out reem_704ILR);
                    if (rc_704ILR != ReservaResult_704ILR.Success_704ILR)
                    {
                        // Cancelada o con el evento iniciado desde otra estacion: la ficha se
                        // refresca para mostrarla como quedo (de solo lectura).
                        if (rc_704ILR == ReservaResult_704ILR.NoModificable_704ILR || rc_704ILR == ReservaResult_704ILR.EventoIniciado_704ILR)
                            RefrescarReserva_704ILR(idReserva_704ILR);
                        ShowError_704ILR(() => MensajeError_704ILR(rc_704ILR));
                        return;
                    }
                    // Se reselecciona con el id tomado ANTES de recargar: al reasignar los
                    // datos la grilla vuelve a la primera fila y CargarEnForm pisa _editId
                    // con esa otra reserva, que quedaba editable en lugar de la cancelada.
                    SafeLoadData_704ILR();
                    SeleccionarReserva_704ILR(idReserva_704ILR);
                    // Recien ahora la baja esta aplicada: se informa el resultado con
                    // los importes que quedaron asentados (RN-02).
                    MessageBox.Show(Tr_704ILR.F_704ILR("MSG_RES_CANCELADA", "Reserva cancelada. Retenido {0:N2}, reintegro {1:N2}.", ret_704ILR, reem_704ILR),
                        "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            // La reserva y sus servicios contratados se guardan JUNTOS, en una sola
            // transaccion orquestada por la capa de negocio: el monto de la cabecera
            // y las lineas que lo componen no pueden quedar desfasados.
            // Cambiar el dia de un evento con personal confirmado reinicia esas
            // confirmaciones (el equipo responde de nuevo): se toma el avance previo para
            // avisarlo despues del guardado.
            BE_EventoCoordinacion_704ILR coordinacionPrevia_704ILR = _editId_704ILR == 0 ? null : BLL_Coordinacion_704ILR.GetEvento_704ILR(_editId_704ILR);

            ReservaResult_704ILR result_704ILR = _editId_704ILR == 0
                ? BLL_Reserva_704ILR.Crear_704ILR(reserva_704ILR, _serviciosReserva_704ILR, out idReserva_704ILR)
                : BLL_Reserva_704ILR.Actualizar_704ILR(reserva_704ILR, _serviciosReserva_704ILR);

            // RN-01: si la operacion vencio, se ofrece renovar la vigencia en el acto
            // en vez de dejar al vendedor con una cotizacion trabada.
            if (result_704ILR == ReservaResult_704ILR.Vencida_704ILR)
            {
                // La pregunta es una clave propia: armada con el rotulo de un boton y un
                // "?" fijo, en castellano salia sin el signo de apertura.
                string preg_704ILR = MensajeError_704ILR(result_704ILR) + Environment.NewLine +
                                     Environment.NewLine + T_704ILR("MSG_RES_RENOVAR_PREGUNTA", "\u00BFRenovar la vigencia?");
                if (MessageBox.Show(preg_704ILR, "EvenTech",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes &&
                    BLL_Reserva_704ILR.Renovar_704ILR(_editId_704ILR) == ReservaResult_704ILR.Success_704ILR)
                {
                    // La renovacion ya quedo persistida aunque el reintento falle despues
                    // (RN-07, RN-06...): la fila de la grilla la muestra en el acto.
                    ReflejarVencimiento_704ILR(idReserva_704ILR);
                    // La renovacion se confirma al vendedor: sin aviso, el nuevo plazo
                    // solo se ve mirando la columna "Vence" de la grilla.
                    MessageBox.Show(T_704ILR("MSG_RES_RENOVADA", "Vigencia renovada."), "EvenTech",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // El reintento viaja CON los servicios, igual que el camino normal:
                    // con la sobrecarga de un solo argumento se guardaba la cabecera con
                    // el monto nuevo y las lineas quedaban como estaban, rompiendo el
                    // invariante "monto = suma de los servicios contratados".
                    result_704ILR = BLL_Reserva_704ILR.Actualizar_704ILR(reserva_704ILR, _serviciosReserva_704ILR);
                }
            }

            // RN-05: el rechazo se explica nombrando los dos estados involucrados,
            // que es la informacion que el vendedor necesita para corregir.
            if (result_704ILR == ReservaResult_704ILR.TransicionInvalida_704ILR)
            {
                // Los dos estados se toman ahora; el texto se compone en el idioma activo
                // cada vez que se muestra, sin volver a leer la base.
                var persistida_704ILR = BLL_Reserva_704ILR.GetById_704ILR(_editId_704ILR);
                EstadoReserva_704ILR? desde_704ILR = persistida_704ILR?.Estado_704ILR;
                EstadoReserva_704ILR hacia_704ILR = reserva_704ILR.Estado_704ILR;
                ShowError_704ILR(() => Tr_704ILR.F_704ILR("MSG_RES_TRANSICION", "No se admite pasar de {0} a {1}.",
                    desde_704ILR.HasValue ? Tr_704ILR.Estado_704ILR(desde_704ILR.Value) : "-",
                    Tr_704ILR.Estado_704ILR(hacia_704ILR)));
                return;
            }

            if (result_704ILR == ReservaResult_704ILR.Success_704ILR)
            {
                // La reserva recien guardada queda seleccionada: la ficha sigue
                // mostrando la operacion sobre la que se trabajo.
                SafeLoadData_704ILR();
                SeleccionarReserva_704ILR(idReserva_704ILR);
                if (coordinacionPrevia_704ILR != null && coordinacionPrevia_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA
                    && coordinacionPrevia_704ILR.Confirmados_704ILR > 0
                    && coordinacionPrevia_704ILR.FechaEvento_704ILR.Date != reserva_704ILR.FechaEvento_704ILR.Date)
                    MessageBox.Show(Tr_704ILR.F_704ILR("MSG_RES_CONFIRMACIONES_REINICIADAS",
                            "El evento cambió de fecha: {0} confirmación(es) del personal volvieron a pendiente y el equipo tiene que responder de nuevo.",
                            coordinacionPrevia_704ILR.Confirmados_704ILR),
                        "EvenTech", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                ReservaResult_704ILR rechazo_704ILR = result_704ILR;
                // Otra sesion cancelo la reserva o inicio su evento con la ficha abierta: la ficha
                // se refresca para mostrarla como quedo (de solo lectura y sin cambios pendientes),
                // igual que al emitir su documentacion. Sin refrescar seguia editable, con Guardar habilitado.
                if ((rechazo_704ILR == ReservaResult_704ILR.NoModificable_704ILR || rechazo_704ILR == ReservaResult_704ILR.EventoIniciado_704ILR)
                    && idReserva_704ILR != 0)
                    RefrescarReserva_704ILR(idReserva_704ILR);
                ShowError_704ILR(() => MensajeError_704ILR(rechazo_704ILR), AvisoResueltoPorPrecarga_704ILR(rechazo_704ILR));
            }
        }

        // Rechazos que una precarga desde Disponibilidad resuelve: carga un salon libre en la
        // fecha consultada y con capacidad para los invitados consultados.
        private static bool AvisoResueltoPorPrecarga_704ILR(ReservaResult_704ILR r_704ILR) =>
            r_704ILR == ReservaResult_704ILR.InvalidSalon_704ILR || r_704ILR == ReservaResult_704ILR.SalonOcupado_704ILR ||
            r_704ILR == ReservaResult_704ILR.InvalidFecha_704ILR || r_704ILR == ReservaResult_704ILR.CapacidadInsuficiente_704ILR;

        // Vuelve a leer la grilla y a mostrar la reserva, para que la ficha la muestre como quedo
        // en la base (por ejemplo, cancelada por otra sesion).
        private void RefrescarReserva_704ILR(int id_704ILR)
        {
            SafeLoadData_704ILR();
            SeleccionarReserva_704ILR(id_704ILR);
        }

        private static string MensajeError_704ILR(ReservaResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case ReservaResult_704ILR.InvalidCliente_704ILR: return T_704ILR("MSG_RES_CLIENTE", "Seleccione un cliente válido.");
                case ReservaResult_704ILR.InvalidSalon_704ILR:   return T_704ILR("MSG_RES_SALON", "Seleccione un salón válido.");
                case ReservaResult_704ILR.InvalidFecha_704ILR:   return T_704ILR("MSG_RES_FECHA", "La fecha del evento no puede ser anterior a hoy.");
                // El tope se escribe con el formato de la cultura, el del campo Monto y la grilla:
                // con el numero fijo de cada idioma, en otra configuracion regional el aviso
                // mostraba el importe con otros separadores que los de la ficha.
                case ReservaResult_704ILR.InvalidMonto_704ILR:   return Tr_704ILR.F_704ILR("MSG_RES_MONTO", "El monto no puede ser negativo ni superar {0}.",
                                                                    BLL_Reserva_704ILR.MontoMaximo_704ILR.ToString("N2", System.Globalization.CultureInfo.CurrentCulture));
                case ReservaResult_704ILR.SalonOcupado_704ILR:   return T_704ILR("MSG_RES_SALON_OCUPADO", "El salón ya está reservado para esa fecha.");
                case ReservaResult_704ILR.NoModificable_704ILR:  return T_704ILR("MSG_RES_NO_MODIFICABLE", "La reserva está cancelada: no admite modificaciones.");
                case ReservaResult_704ILR.Vencida_704ILR:        return T_704ILR("MSG_RES_VENCIDA", "La operación venció: renovala antes de cambiar su estado.");
                case ReservaResult_704ILR.SinPlazo_704ILR:       return T_704ILR("MSG_RES_SIN_PLAZO", "La operación no tiene un plazo de vigencia que renovar.");
                case ReservaResult_704ILR.InvalidInvitados_704ILR: return T_704ILR("MSG_RES_INVITADOS", "Indica la cantidad de invitados estimada: hace falta para confirmar y no puede ser negativa.");
                case ReservaResult_704ILR.CapacidadInsuficiente_704ILR: return T_704ILR("MSG_RES_CAPACIDAD", "El salón no alcanza para la cantidad de invitados indicada.");
                case ReservaResult_704ILR.TransicionInvalida_704ILR: return T_704ILR("MSG_RES_TRANSICION_GEN", "El cambio de estado solicitado no está admitido.");
                case ReservaResult_704ILR.MontoInferiorPagado_704ILR: return T_704ILR("MSG_RES_MONTO_PAGADO", "El total de la reserva no puede quedar por debajo de lo ya cobrado.");
                case ReservaResult_704ILR.SinAdelanto_704ILR:    return T_704ILR("MSG_RES_SIN_ADELANTO", "Para confirmar la reserva hay que registrar el adelanto: guardala y cobra el pago desde Pagos.");
                case ReservaResult_704ILR.NotFound_704ILR:       return T_704ILR("MSG_RES_NOTFOUND", "La reserva ya no existe.");
                case ReservaResult_704ILR.EventoIniciado_704ILR: return T_704ILR("MSG_RES_EVENTO_INICIADO", "El evento de esta reserva está en ejecución o cerrado: la reserva ya no admite modificaciones ni cancelación.");
                default:                           return T_704ILR("MSG_RES_ERROR", "No se pudo guardar la reserva.");
            }
        }

        private void VerHistorial_704ILR()
        {
            if (_editId_704ILR == 0)
            {
                ShowError_704ILR(() => Tr_704ILR.T_704ILR("MSG_RES_SELECCIONE"));
                return;
            }
            if (!Permisos_704ILR.Exigir_704ILR("RESERVA_HISTORIAL", FindForm(), "ver el historial de la reserva #" + _editId_704ILR)) return;
            using (var frm_704ILR = new frmHistorialReserva_704ILR(_editId_704ILR))
            {
                frm_704ILR.ShowDialog(FindForm());
            }
        }

        // Abre las versiones guardadas de la reserva (patron Memento). Si se
        // restauro una, recarga la grilla y reselecciona la reserva para que la
        // ficha muestre los valores repuestos.
        private void VerVersiones_704ILR()
        {
            if (_editId_704ILR == 0)
            {
                ShowError_704ILR(() => T_704ILR("MSG_RES_SELECCIONE_GEN", "Seleccione una reserva existente."));
                return;
            }
            // ABRIR el dialogo es consultar, y por eso alcanza con el permiso de
            // consulta del historial. Tambien lo abre quien puede RESTAURAR: elegir
            // que version reponer supone verlas, y sin esta entrada ese permiso no
            // llevaba a ninguna pantalla. RESTAURAR es otra cosa —una correccion
            // ADMINISTRATIVA que no respeta la tabla de transiciones (RN-05) y puede
            // deshacer una confirmacion— y lleva permiso propio, exigido dentro del
            // dialogo, que ademas deshabilita el boton cuando falta.
            if (!Permisos_704ILR.ExigirAlguno_704ILR(FindForm(), "ver las versiones de la reserva #" + _editId_704ILR,
                    "RESERVA_HISTORIAL", "RESERVA_RESTAURAR"))
                return;
            // Restaurar reemplaza lo que muestra la ficha por la version elegida. Si el perfil
            // puede restaurar y la ficha tiene cambios sin guardar, se pregunta antes de abrir el
            // dialogo (su confirmacion habla del estado guardado, no de lo tipeado); con "Si" la
            // ficha vuelve a mostrar la reserva guardada. Quien solo puede consultar las
            // versiones no reemplaza nada y conserva lo cargado.
            if (Permisos_704ILR.Tiene_704ILR("RESERVA_RESTAURAR") && ((IVistaConCambios_704ILR)this).HayCambiosSinGuardar_704ILR)
            {
                if (!ConfirmarDescarte_704ILR()) return;
                SeleccionarReserva_704ILR(_editId_704ILR);
            }
            using (var frm_704ILR = new frmVersionesReserva_704ILR(_editId_704ILR))
            {
                if (frm_704ILR.ShowDialog(FindForm()) != DialogResult.OK) return;

                int id_704ILR = _editId_704ILR;
                SafeLoadData_704ILR();
                SeleccionarReserva_704ILR(id_704ILR);
            }
        }

        // Deja seleccionada en la grilla la reserva indicada, de modo que la ficha
        // muestre la operacion sobre la que se acaba de trabajar. Sin esto, recargar
        // la grilla la reposiciona en la primera fila y la ficha termina mostrando
        // una reserva distinta de la que se acaba de guardar.
        // Ademas de seleccionar, garantiza que la ficha quede en modo EDICION sobre
        // esa reserva: si la fila ya era la actual (o la grilla no dispara el evento
        // de seleccion al reasignar los datos), CargarEnForm no corria y tras un alta
        // la ficha seguia en modo alta con _editId = 0, de modo que un segundo
        // Guardar volvia a crear la misma reserva.
        private void SeleccionarReserva_704ILR(int id_704ILR)
        {
            if (id_704ILR <= 0) return;
            foreach (DataGridViewRow row_704ILR in _grid_704ILR.Rows)
            {
                if (row_704ILR.DataBoundItem is BE_Reserva_704ILR r_704ILR && r_704ILR.Id_704ILR == id_704ILR)
                {
                    bool yaActual_704ILR = _grid_704ILR.CurrentRow == row_704ILR;
                    // La seleccion la mueve la pantalla: la ficha se carga sin preguntar.
                    _seleccionProgramada_704ILR++;
                    try
                    {
                        _grid_704ILR.CurrentCell = row_704ILR.Cells[0];
                        row_704ILR.Selected = true;
                    }
                    finally { _seleccionProgramada_704ILR--; }
                    if (yaActual_704ILR || _editId_704ILR != id_704ILR) CargarEnForm_704ILR(r_704ILR);
                    return;
                }
            }
        }

        // Sufijo " #N" para el texto de la accion que se registra al denegar un
        // permiso. Sin reserva en edicion no hay numero que nombrar, y el asiento
        // diria "reserva #0".
        private string ReferenciaEnEdicion_704ILR() => _editId_704ILR > 0 ? " #" + _editId_704ILR : "";

        // El aviso se recibe como receta (Func) y no como texto ya traducido: asi
        // ActualizarTextos lo vuelve a componer si el usuario cambia de idioma con el
        // aviso a la vista. Las recetas solo traducen: no consultan la base.
        // resueltoPorPrecarga: el aviso deja de corresponder cuando la ficha se precarga desde
        // Disponibilidad (ver ConsultarDisponibilidad y _avisoResueltoPorPrecarga).
        private void ShowError_704ILR(Func<string> mensaje_704ILR, bool resueltoPorPrecarga_704ILR = false)
        {
            _mensajeError_704ILR = mensaje_704ILR;
            _avisoResueltoPorPrecarga_704ILR = resueltoPorPrecarga_704ILR;
            _lblError_704ILR.Text = mensaje_704ILR();
            _lblError_704ILR.Visible = true;
        }

        // RN-01: la renovacion se persiste antes del reintento. Si el reintento falla la
        // grilla no se recarga (se perderia lo que el vendedor cargo en la ficha para
        // corregir y volver a intentar), asi que se actualiza SOLO el vencimiento de esa
        // fila con el valor guardado.
        private void ReflejarVencimiento_704ILR(int id_704ILR)
        {
            try
            {
                BE_Reserva_704ILR persistida_704ILR = BLL_Reserva_704ILR.GetById_704ILR(id_704ILR);
                if (persistida_704ILR == null) return;
                foreach (DataGridViewRow row_704ILR in _grid_704ILR.Rows)
                {
                    if (row_704ILR.DataBoundItem is BE_Reserva_704ILR r_704ILR && r_704ILR.Id_704ILR == id_704ILR)
                    {
                        r_704ILR.VenceEl_704ILR = persistida_704ILR.VenceEl_704ILR;
                        _grid_704ILR.InvalidateRow(row_704ILR.Index);
                        return;
                    }
                }
            }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Reservas", "Reflejar la renovacion de la reserva #" + id_704ILR);
            }
        }

        // Relee la reserva antes de emitir su documentacion: otra sesion pudo haberla
        // cancelado con la ficha abierta, y el documento de una operacion dada de baja
        // afirmaria algo que ya no es cierto (por eso la ficha apaga Comprobante y Email
        // en una cancelada). Si ya no admite cambios, el rechazo se asienta y la ficha se
        // refresca para mostrarla como esta. Devuelve la reserva vigente o null.
        private BE_Reserva_704ILR ReservaEmitible_704ILR(string accionRechazada_704ILR)
        {
            int id_704ILR = _editId_704ILR;
            BE_Reserva_704ILR vigente_704ILR = BLL_Reserva_704ILR.GetById_704ILR(id_704ILR);
            if (vigente_704ILR == null)
            {
                ShowError_704ILR(() => T_704ILR("MSG_RES_NOTFOUND", "La reserva ya no existe."));
                return null;
            }
            // Un estado almacenado que no es ninguno de la tabla de estados tampoco se
            // documenta: el comprobante afirmaria una situacion que no se puede establecer.
            bool estadoDefinido_704ILR = Enum.IsDefined(typeof(EstadoReserva_704ILR), vigente_704ILR.Estado_704ILR);
            if (estadoDefinido_704ILR && BLL_Reserva_704ILR.PuedeModificar_704ILR(vigente_704ILR)) return vigente_704ILR;

            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", accionRechazada_704ILR, CriticidadBitacora_704ILR.Advertencia,
                "Reserva #" + id_704ILR + (estadoDefinido_704ILR
                    ? " cancelada: no se emite su documentacion."
                    : " con un estado almacenado fuera de la tabla de estados: no se emite su documentacion."));
            SafeLoadData_704ILR();
            SeleccionarReserva_704ILR(id_704ILR);
            if (!_lblError_704ILR.Visible)
            {
                if (estadoDefinido_704ILR)
                    ShowError_704ILR(() => T_704ILR("MSG_RES_NO_MODIFICABLE", "La reserva está cancelada: no admite modificaciones."));
                else
                    ShowError_704ILR(MensajeEstadoDesconocido_704ILR);
            }
            return null;
        }

        // Un boton con icono reserva 30 px para el glifo (AppButton_704ILR.OnPaint). En
        // media ficha un rotulo traducido mas largo ("Pagamentos") no entraba y salia con
        // puntos suspensivos: mientras el rotulo no entra junto al glifo, el boton se
        // muestra sin glifo y el texto usa todo el ancho. Se reevalua cuando cambia el
        // texto (idioma, conteo de servicios) o el tamano.
        private void RegistrarGlifo_704ILR(AppButton_704ILR btn_704ILR)
        {
            _glifos_704ILR[btn_704ILR] = btn_704ILR.Glyph_704ILR;
            btn_704ILR.TextChanged += (s_704ILR, e_704ILR) => AjustarGlifo_704ILR(btn_704ILR);
            btn_704ILR.SizeChanged += (s_704ILR, e_704ILR) => AjustarGlifo_704ILR(btn_704ILR);
            AjustarGlifo_704ILR(btn_704ILR);
        }

        private void AjustarGlifo_704ILR(AppButton_704ILR btn_704ILR)
        {
            string glifo_704ILR = _glifos_704ILR[btn_704ILR];
            if (string.IsNullOrEmpty(glifo_704ILR) || string.IsNullOrEmpty(btn_704ILR.Text)) return;
            // Mismo reparto que OnPaint: con glifo el texto dispone de (Width - 1) - 30 px.
            int necesario_704ILR = TextRenderer.MeasureText(btn_704ILR.Text, btn_704ILR.Font).Width;
            string nuevo_704ILR = necesario_704ILR <= btn_704ILR.Width - 31 ? glifo_704ILR : null;
            if (btn_704ILR.Glyph_704ILR == nuevo_704ILR) return;
            btn_704ILR.Glyph_704ILR = nuevo_704ILR;
            btn_704ILR.Invalidate();
        }

        // Devuelve la traduccion de 'clave' o, si falta, el texto por defecto dado.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }
}
