using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Configuracion de la conexion a la base. Se abre cuando el arranque no logra
    // conectar (antes del login) y permite elegir instancia y base, probar la
    // conexion y guardarla cifrada.
    //
    // Corre ANTES de que se carguen las traducciones desde la base, asi que todos
    // los textos usan T(clave, defecto): si el diccionario todavia no esta, se ve
    // el texto por defecto en vez de la clave cruda.
    //
    // La prueba de conexion y el guardado corren fuera del hilo de la interfaz:
    // contra un servidor que no responde, o con la base elegida ocupada, tardan
    // varios segundos y la ventana tiene que seguir respondiendo (incluso para
    // salir mientras tanto). Lo que la pantalla informa o guarda es siempre lo que
    // muestran sus campos.
    public class frmConfiguracionConexion_704ILR : FormBase_704ILR
    {
        private ComboBox _cboServidor_704ILR;
        private TextBox _txtBase_704ILR;
        private Label _lblEstado_704ILR;
        // Contenedor con desplazamiento del estado: los diagnosticos del motor
        // ocupan varias lineas y tienen que poder leerse enteros.
        private Panel _pnlEstado_704ILR;
        private AppButton_704ILR _btnProbar_704ILR, _btnGuardar_704ILR;
        // true mientras corre una prueba o un guardado: evita lanzar otro encima
        // (doble clic, Enter).
        private bool _probando_704ILR;
        // true desde que la pantalla se cerro (Salir, la cruz o Guardar): una prueba
        // que termina despues no toca los controles ni guarda, aunque el formulario
        // todavia no se haya descartado.
        private bool _cerrada_704ILR;
        // Cancelacion de la operacion en curso. Se pide al cerrar la pantalla o si
        // instancia o base cambian mientras corre: el resultado ya no corresponde a lo
        // que se ve (si cambiaron los campos, la operacion se vuelve a lanzar con ellos).
        private CancellationTokenSource _cancelacion_704ILR;

        // Fase del guardado en curso, compartida con el hilo que guarda. Justo antes de
        // escribir, la BLL la pasa de Probando a Escribiendo (ConfirmarEscritura); cerrar
        // la pantalla o cambiar un campo la pasa de Probando a Descartada. Las dos
        // transiciones son atomicas y solo una gana: o no se escribe nada, o la escritura
        // y su asiento se completan y la pantalla informa ese resultado.
        private const int FaseLibre_704ILR = 0, FaseProbando_704ILR = 1, FaseEscribiendo_704ILR = 2, FaseDescartada_704ILR = 3;
        private int _fase_704ILR = FaseLibre_704ILR;
        // true si se pidio cerrar mientras la configuracion se escribia: la pantalla cierra
        // cuando el guardado termina, con su resultado real.
        private bool _cerrarAlTerminar_704ILR;

        // Donde estaba el foco al empezar la operacion (la instancia o el boton pulsado,
        // que se deshabilitan mientras corre) y la seleccion de la instancia en ese
        // momento, para devolverlos al terminar.
        private Control _focoPrevio_704ILR;
        private int _inicioSeleccionPrevia_704ILR, _largoSeleccionPrevia_704ILR;

        // true mientras "Conexion correcta." esta a la vista; instancia y base con las
        // que se obtuvo ese resultado.
        private bool _exitoVisible_704ILR;
        private string _servidorProbado_704ILR, _baseProbada_704ILR;

        // true cuando se guardo una configuracion que conecta: el llamador puede
        // reintentar el arranque sin volver a preguntar.
        public bool Configurada_704ILR { get; private set; }

        public frmConfiguracionConexion_704ILR(string mensajeInicial_704ILR = null)
        {
            BuildUi_704ILR();
            FormClosing += (s_704ILR, e_704ILR) => AlCerrar_704ILR(e_704ILR);
            FormClosed += (s_704ILR, e_704ILR) => { _cerrada_704ILR = true; _cancelacion_704ILR?.Cancel(); };
            _cboServidor_704ILR.TextChanged += (s_704ILR, e_704ILR) => CamposCambiados_704ILR();
            _txtBase_704ILR.TextChanged += (s_704ILR, e_704ILR) => CamposCambiados_704ILR();
            // Un clic en la base mientras corre una operacion es una eleccion del usuario:
            // al terminar, el foco se queda donde lo puso.
            _txtBase_704ILR.MouseDown += (s_704ILR, e_704ILR) => { if (_probando_704ILR && e_704ILR.Button == MouseButtons.Left) _focoPrevio_704ILR = null; };
            if (!string.IsNullOrEmpty(mensajeInicial_704ILR)) Estado_704ILR(mensajeInicial_704ILR, error_704ILR: true);
        }

        private void BuildUi_704ILR()
        {
            Text = "EvenTech";
            // Alto pensado para que el estado muestre unas nueve lineas sin desplazar
            // (un error de red del motor ocupa siete). Entra en 1366x768 (RNF-03).
            ClientSize = new Size(560, 540);
            BackColor = Theme_704ILR.BgContent_704ILR;

            // ---------------- Barra de titulo ----------------
            var pnlTitle_704ILR = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme_704ILR.BgTitleBar_704ILR };
            EnableDrag_704ILR(pnlTitle_704ILR);
            var lblTitle_704ILR = new Label
            {
                Text = T_704ILR("CONN_TITULO", "Configuración de conexión"),
                Font = Theme_704ILR.FontH2_704ILR,
                ForeColor = Theme_704ILR.TextOnDark_704ILR,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Theme_704ILR.SpaceLg_704ILR, 0, 0, 0),
                BackColor = Color.Transparent
            };
            EnableDrag_704ILR(lblTitle_704ILR);
            var btnCerrar_704ILR = WindowButton_704ILR(Theme_704ILR.IcoClose_704ILR, (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); }, danger_704ILR: true);
            btnCerrar_704ILR.Dock = DockStyle.Right;
            pnlTitle_704ILR.Controls.Add(lblTitle_704ILR);
            pnlTitle_704ILR.Controls.Add(btnCerrar_704ILR);

            // ---------------- Cuerpo ----------------
            var card_704ILR = new CardPanel_704ILR
            {
                Dock = DockStyle.Fill,
                BehindColor_704ILR = Theme_704ILR.BgContent_704ILR,
                Margin = new Padding(Theme_704ILR.SpaceLg_704ILR),
                Padding = new Padding(Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR, Theme_704ILR.SpaceXl_704ILR, Theme_704ILR.SpaceLg_704ILR)
            };

            var layout_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Color.Transparent
            };
            layout_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // ayuda
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // servidor
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // base
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // estado
            layout_704ILR.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // botones

            // Alto segun el texto: el ancho maximo se ajusta al de la tarjeta (AjustarAnchos).
            var lblAyuda_704ILR = new Label
            {
                Text = T_704ILR("CONN_AYUDA",
                    "No se pudo conectar a la base de datos. Indica dónde está la instancia de SQL Server " +
                    "y el nombre de la base. La configuración se guarda cifrada en tu perfil de Windows."),
                Font = Theme_704ILR.FontBody_704ILR,
                ForeColor = Theme_704ILR.TextMuted_704ILR,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, Theme_704ILR.SpaceMd_704ILR)
            };

            // Instancia: editable (se puede tipear una que la deteccion no encontro).
            _cboServidor_704ILR = new ComboBox
            {
                Font = Theme_704ILR.FontInput_704ILR,
                DropDownStyle = ComboBoxStyle.DropDown,
                FlatStyle = FlatStyle.Flat
            };
            // Un combo plano se repinta entero al cambiar de tamano (ver Ui_704ILR.Combo_704ILR).
            _cboServidor_704ILR.Resize += (s_704ILR, e_704ILR) => _cboServidor_704ILR.Invalidate();
            foreach (string i_704ILR in BLL_Conexion_704ILR.GetInstancias_704ILR()) _cboServidor_704ILR.Items.Add(i_704ILR);
            _cboServidor_704ILR.Text = BLL_Conexion_704ILR.ServidorActual_704ILR;

            _txtBase_704ILR = Ui_704ILR.Input_704ILR();
            _txtBase_704ILR.Text = BLL_Conexion_704ILR.BaseDatosActual_704ILR;

            // Estado: rotulo de alto automatico dentro de un panel con desplazamiento
            // vertical, asi un diagnostico largo se lee completo.
            _pnlEstado_704ILR = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme_704ILR.Surface_704ILR,
                Margin = new Padding(2, Theme_704ILR.SpaceSm_704ILR, 0, 0)
            };
            _lblEstado_704ILR = new Label
            {
                Font = Theme_704ILR.FontBody_704ILR,
                ForeColor = Theme_704ILR.TextMuted_704ILR,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, 0),
                Margin = Padding.Empty
            };
            _pnlEstado_704ILR.Controls.Add(_lblEstado_704ILR);

            // ---------------- Botones ----------------
            var botones_704ILR = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            botones_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            botones_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            botones_704ILR.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var btnSalir_704ILR = Ui_704ILR.Secondary_704ILR(T_704ILR("CONN_SALIR", "Salir"));
            btnSalir_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            btnSalir_704ILR.Size = new Size(110, 38);
            btnSalir_704ILR.Anchor = AnchorStyles.Left;
            btnSalir_704ILR.Click += (s_704ILR, e_704ILR) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnProbar_704ILR = Ui_704ILR.Secondary_704ILR(T_704ILR("CONN_PROBAR", "Probar"));
            _btnProbar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnProbar_704ILR.Size = new Size(130, 38);
            _btnProbar_704ILR.Margin = new Padding(0, 0, Theme_704ILR.SpaceSm_704ILR, 0);
            _btnProbar_704ILR.Click += (s_704ILR, e_704ILR) => Probar_704ILR();

            _btnGuardar_704ILR = Ui_704ILR.Primary_704ILR(T_704ILR("CONN_GUARDAR", "Guardar"), Theme_704ILR.IcoSave_704ILR);
            _btnGuardar_704ILR.BehindColor_704ILR = Theme_704ILR.BgContent_704ILR;
            _btnGuardar_704ILR.Size = new Size(150, 38);
            _btnGuardar_704ILR.Click += (s_704ILR, e_704ILR) => Guardar_704ILR();

            botones_704ILR.Controls.Add(btnSalir_704ILR, 0, 0);
            botones_704ILR.Controls.Add(_btnProbar_704ILR, 1, 0);
            botones_704ILR.Controls.Add(_btnGuardar_704ILR, 2, 0);

            // Los dos campos ocupan el ancho de la tarjeta (el Field es AutoSize y sin
            // Dock tomaria el ancho de su rotulo y cortaria la instancia).
            var fldServidor_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("CONN_SERVIDOR", "Instancia de SQL Server"), _cboServidor_704ILR);
            fldServidor_704ILR.Dock = DockStyle.Fill;
            var fldBase_704ILR = Ui_704ILR.Field_704ILR(T_704ILR("CONN_BASE", "Base de datos"), _txtBase_704ILR);
            fldBase_704ILR.Dock = DockStyle.Fill;

            layout_704ILR.Controls.Add(lblAyuda_704ILR, 0, 0);
            layout_704ILR.Controls.Add(fldServidor_704ILR, 0, 1);
            layout_704ILR.Controls.Add(fldBase_704ILR, 0, 2);
            layout_704ILR.Controls.Add(_pnlEstado_704ILR, 0, 3);
            layout_704ILR.Controls.Add(botones_704ILR, 0, 4);

            layout_704ILR.SizeChanged += (s_704ILR, e_704ILR) => AjustarAnchos_704ILR(layout_704ILR, lblAyuda_704ILR);
            _pnlEstado_704ILR.SizeChanged += (s_704ILR, e_704ILR) => AjustarAnchos_704ILR(layout_704ILR, lblAyuda_704ILR);

            card_704ILR.Controls.Add(layout_704ILR);

            var host_704ILR = new Panel { Dock = DockStyle.Fill, BackColor = Theme_704ILR.BgContent_704ILR, Padding = new Padding(Theme_704ILR.SpaceLg_704ILR) };
            host_704ILR.Controls.Add(card_704ILR);

            Controls.Add(host_704ILR);
            Controls.Add(pnlTitle_704ILR);
            AcceptButton = _btnGuardar_704ILR;
            AjustarAnchos_704ILR(layout_704ILR, lblAyuda_704ILR);
        }

        // Los textos largos se cortan en renglones al ancho real disponible: la ayuda
        // al de la tarjeta y el estado al de su panel, reservando la barra vertical
        // para que no aparezca una horizontal.
        private void AjustarAnchos_704ILR(TableLayoutPanel layout_704ILR, Label ayuda_704ILR)
        {
            int anchoAyuda_704ILR = layout_704ILR.ClientSize.Width - ayuda_704ILR.Margin.Horizontal;
            if (anchoAyuda_704ILR > 0 && ayuda_704ILR.MaximumSize.Width != anchoAyuda_704ILR)
                ayuda_704ILR.MaximumSize = new Size(anchoAyuda_704ILR, 0);

            int anchoEstado_704ILR = _pnlEstado_704ILR.Width - SystemInformation.VerticalScrollBarWidth - 2;
            if (anchoEstado_704ILR > 0 && _lblEstado_704ILR.MaximumSize.Width != anchoEstado_704ILR)
                _lblEstado_704ILR.MaximumSize = new Size(anchoEstado_704ILR, 0);
        }

        // Lo que sigue a cada await (Ocupado, el foco, el estado, el cierre) toca controles y
        // decide entre escribir y cerrar comparando con los clics: tiene que correr en el
        // hilo de la interfaz, en orden con esos clics. Un clic que llega por el bucle de
        // mensajes ya trae el contexto de WinForms; uno provocado por codigo fuera de ese
        // bucle no lo trae, y sin el la continuacion correria en otro hilo.
        private static void AsegurarContextoDeInterfaz_704ILR()
        {
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        }

        private async void Probar_704ILR()
        {
            if (_probando_704ILR) return;
            AsegurarContextoDeInterfaz_704ILR();
            Ocupado_704ILR(true);

            bool ok_704ILR;
            string msg_704ILR, servidor_704ILR, baseDatos_704ILR;
            CancellationTokenSource cancelacion_704ILR;
            do
            {
                // Se prueba lo que muestran los campos. Si cambian mientras corre la
                // prueba, el resultado es de otra configuracion: se descarta y se prueba
                // la que se ve.
                servidor_704ILR = _cboServidor_704ILR.Text;
                baseDatos_704ILR = _txtBase_704ILR.Text;
                _cancelacion_704ILR = cancelacion_704ILR = new CancellationTokenSource();
                Estado_704ILR(T_704ILR("CONN_PROBANDO", "Probando conexión..."), error_704ILR: false);

                (ok_704ILR, msg_704ILR) = await ProbarEnSegundoPlano_704ILR(servidor_704ILR, baseDatos_704ILR);
                if (IsDisposed || _cerrada_704ILR) return;   // se salio de la pantalla mientras corria la prueba
            } while (cancelacion_704ILR.IsCancellationRequested);
            Ocupado_704ILR(false);

            if (ok_704ILR)
            {
                _servidorProbado_704ILR = servidor_704ILR;
                _baseProbada_704ILR = baseDatos_704ILR;
                Estado_704ILR(T_704ILR("CONN_OK", "Conexión correcta."), error_704ILR: false, ok_704ILR: true);
            }
            else
                Estado_704ILR(msg_704ILR, error_704ILR: true);
        }

        private async void Guardar_704ILR()
        {
            if (_probando_704ILR) return;
            AsegurarContextoDeInterfaz_704ILR();
            Ocupado_704ILR(true);

            bool ok_704ILR;
            string msg_704ILR, servidor_704ILR, baseDatos_704ILR;
            CancellationTokenSource cancelacion_704ILR;
            do
            {
                // Se toman los valores que muestran los campos: son los que se prueban y
                // se guardan.
                servidor_704ILR = _cboServidor_704ILR.Text;
                baseDatos_704ILR = _txtBase_704ILR.Text;
                _cancelacion_704ILR = cancelacion_704ILR = new CancellationTokenSource();
                Interlocked.Exchange(ref _fase_704ILR, FaseProbando_704ILR);
                Estado_704ILR(T_704ILR("CONN_PROBANDO", "Probando conexión..."), error_704ILR: false);

                // Todo el guardado corre en segundo plano: la BLL prueba la conexion,
                // escribe la configuracion y asienta en la bitacora de la base elegida, y
                // la prueba y el asiento pueden demorar. Antes de escribir le confirma a
                // la pantalla que sigue en pie: si para entonces se cerro o los campos
                // cambiaron, no persiste nada.
                (ok_704ILR, msg_704ILR) = await GuardarEnSegundoPlano_704ILR(servidor_704ILR, baseDatos_704ILR, ConfirmarEscritura_704ILR);
                Interlocked.Exchange(ref _fase_704ILR, FaseLibre_704ILR);
                if (IsDisposed || _cerrada_704ILR) return;   // se salio antes de escribir: no se tocan los controles
                // Se pidio cerrar mientras se escribia: no se reintenta, se cierra con lo que paso.
                if (_cerrarAlTerminar_704ILR) break;
                // Si los campos cambiaron antes de escribir no se guardo nada: se vuelve a
                // empezar con lo que se ve.
            } while (!ok_704ILR && cancelacion_704ILR.IsCancellationRequested);
            Ocupado_704ILR(false);

            if (ok_704ILR)
            {
                // Quedo guardado lo que se tomo de los campos al empezar el intento. Ninguna
                // via del usuario los cambia mientras corre la operacion; si igual cambiaron
                // despues de escribir, vuelven a mostrar lo guardado: la pantalla no cierra
                // configurada mostrando otra instancia u otra base.
                if (_cboServidor_704ILR.Text != servidor_704ILR) _cboServidor_704ILR.Text = servidor_704ILR;
                if (_txtBase_704ILR.Text != baseDatos_704ILR) _txtBase_704ILR.Text = baseDatos_704ILR;
                Configurada_704ILR = true;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            if (_cerrarAlTerminar_704ILR)
            {
                // La escritura fallo y el usuario ya habia pedido salir: no quedo nada guardado.
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            // Guardar solo si conecta: evita dejar la app apuntando a una
            // instancia inexistente y tener que reconfigurar a ciegas.
            Estado_704ILR(msg_704ILR, error_704ILR: true);
        }

        private static Task<(bool Ok_704ILR, string Mensaje_704ILR)> ProbarEnSegundoPlano_704ILR(string servidor_704ILR, string baseDatos_704ILR)
            => Task.Run(() =>
            {
                try
                {
                    bool ok_704ILR = BLL_Conexion_704ILR.Probar_704ILR(servidor_704ILR, baseDatos_704ILR, out string msg_704ILR);
                    return (ok_704ILR, msg_704ILR);
                }
                catch (Exception ex_704ILR)
                {
                    return (false, ex_704ILR.Message);
                }
            });

        private static Task<(bool Ok_704ILR, string Mensaje_704ILR)> GuardarEnSegundoPlano_704ILR(string servidor_704ILR, string baseDatos_704ILR, Func<bool> confirmarEscritura_704ILR)
            => Task.Run(() =>
            {
                try
                {
                    bool ok_704ILR = BLL_Conexion_704ILR.Guardar_704ILR(servidor_704ILR, baseDatos_704ILR, confirmarEscritura_704ILR, out string msg_704ILR);
                    return (ok_704ILR, msg_704ILR);
                }
                catch (Exception ex_704ILR)
                {
                    return (false, ex_704ILR.Message);
                }
            });

        // La invoca la BLL desde el hilo del guardado, despues de la prueba y antes de
        // escribir: autoriza la escritura solo si nadie descarto el intento. Autorizada, la
        // prueba ya termino y lo que sigue (escribir y asentar en la bitacora de la base
        // elegida, que otra estacion puede tener ocupada) puede demorar: la pantalla deja de
        // decir que prueba la conexion y avisa que guarda, sin esperar a que se pulse Salir.
        private bool ConfirmarEscritura_704ILR()
        {
            bool confirmada_704ILR = Interlocked.CompareExchange(ref _fase_704ILR, FaseEscribiendo_704ILR, FaseProbando_704ILR) == FaseProbando_704ILR;
            if (confirmada_704ILR) AvisarGuardando_704ILR();
            return confirmada_704ILR;
        }

        // Publica "Guardando la configuracion..." en el hilo de la interfaz sin esperarlo: el
        // hilo del guardado no se demora ni depende de la pantalla, y la escritura ya
        // autorizada sigue aunque el aviso no se pueda publicar. Si cuando llega el aviso el
        // guardado ya termino (su resultado esta a la vista) o la pantalla se cerro, no se
        // muestra: nunca pisa el resultado.
        private void AvisarGuardando_704ILR()
        {
            try
            {
                if (!IsHandleCreated) return;
                BeginInvoke((Action)(() =>
                {
                    if (_cerrada_704ILR || IsDisposed || Volatile.Read(ref _fase_704ILR) != FaseEscribiendo_704ILR) return;
                    Estado_704ILR(T_704ILR("CONN_GUARDANDO", "Guardando la configuración..."), error_704ILR: false);
                }));
            }
            catch (InvalidOperationException)
            {
                // La ventana se esta destruyendo (un apagado de Windows no espera el guardado):
                // no queda donde avisar.
            }
        }

        // Descarta la operacion en curso: lo que se ve ya no es lo que se prueba o se
        // guarda, o la pantalla se cierra. Devuelve false solo si el guardado ya esta
        // escribiendo: entonces la escritura y el asiento siguen hasta el final.
        private bool DescartarOperacion_704ILR()
        {
            _cancelacion_704ILR?.Cancel();
            return Interlocked.CompareExchange(ref _fase_704ILR, FaseDescartada_704ILR, FaseProbando_704ILR) != FaseEscribiendo_704ILR;
        }

        // Cerrar (Salir, la cruz, Alt+F4) con una operacion en curso. Si el guardado
        // todavia no escribio, se descarta y la pantalla cierra en el acto sin persistir
        // nada. Si ya esta escribiendo, cerrar ahora dejaria connection.cfg cambiada, un
        // asiento que se pierde si el proceso termina (como en el arranque) y un resultado
        // que dice que no se configuro nada: la pantalla espera a que el guardado termine
        // y cierra entonces con su resultado. Un apagado de Windows no se demora.
        private void AlCerrar_704ILR(FormClosingEventArgs e_704ILR)
        {
            if (DescartarOperacion_704ILR() || e_704ILR.CloseReason == CloseReason.WindowsShutDown) return;
            e_704ILR.Cancel = true;
            if (_cerrarAlTerminar_704ILR) return;
            _cerrarAlTerminar_704ILR = true;
            Estado_704ILR(T_704ILR("CONN_GUARDANDO", "Guardando la configuración..."), error_704ILR: false);
        }

        // Instancia o base cambiaron. Durante una operacion no se pueden editar (Ocupado);
        // si igual cambian, lo que se estaba probando o guardando ya no es lo que se ve y
        // se descarta. Sin operacion, un "Conexion correcta." que ya no corresponde a los
        // campos se quita: la pantalla no afirma un resultado sobre una configuracion que
        // no probo. Un diagnostico de error se conserva mientras se corrige el dato: nombra
        // la instancia o la base a la que se refiere.
        private void CamposCambiados_704ILR()
        {
            if (_probando_704ILR)
            {
                DescartarOperacion_704ILR();
                return;
            }
            if (_exitoVisible_704ILR &&
                (!string.Equals(_cboServidor_704ILR.Text, _servidorProbado_704ILR, StringComparison.Ordinal) ||
                 !string.Equals(_txtBase_704ILR.Text, _baseProbada_704ILR, StringComparison.Ordinal)))
                Estado_704ILR(string.Empty, error_704ILR: false);
        }

        // Mientras corre una prueba o un guardado no se puede lanzar otra operacion ni
        // editar instancia y base: lo que se informa o se guarda es lo que se ve. Salir y
        // la cruz siguen disponibles. La base queda de solo lectura y no deshabilitada
        // para conservar el foco: con todo lo demas deshabilitado pasaria a Salir y un
        // Enter cerraria la pantalla.
        private void Ocupado_704ILR(bool ocupado_704ILR)
        {
            if (ocupado_704ILR) AparcarFoco_704ILR();
            else _cancelacion_704ILR = null;
            _probando_704ILR = ocupado_704ILR;
            _cboServidor_704ILR.Enabled = !ocupado_704ILR;
            _txtBase_704ILR.ReadOnly = ocupado_704ILR;
            // Solo lectura no alcanza: el menu contextual del cuadro sigue ofreciendo
            // "Deshacer", que cambia el texto igual. Sin atajos no hay menu contextual.
            _txtBase_704ILR.ShortcutsEnabled = !ocupado_704ILR;
            _btnProbar_704ILR.Enabled = !ocupado_704ILR;
            _btnGuardar_704ILR.Enabled = !ocupado_704ILR;
            if (!ocupado_704ILR) RestaurarFoco_704ILR();
        }

        // Deshabilitar el control que tiene el foco (la instancia, o el boton pulsado con
        // el mouse) hace que WinForms lo pase solo a la base, que al recibirlo selecciona
        // todo su texto: lo que el usuario siguiera tipeando al volver el resultado
        // reemplazaria el nombre de la base. Se lleva el foco a la base sin tocar su
        // seleccion y se recuerda donde estaba, para devolverlo al terminar.
        private void AparcarFoco_704ILR()
        {
            _focoPrevio_704ILR = null;
            Control foco_704ILR = ControlActivo_704ILR();
            if (foco_704ILR != _cboServidor_704ILR && foco_704ILR != _btnProbar_704ILR && foco_704ILR != _btnGuardar_704ILR) return;

            _focoPrevio_704ILR = foco_704ILR;
            _inicioSeleccionPrevia_704ILR = _cboServidor_704ILR.SelectionStart;
            _largoSeleccionPrevia_704ILR = _cboServidor_704ILR.SelectionLength;
            int inicioBase_704ILR = _txtBase_704ILR.SelectionStart, largoBase_704ILR = _txtBase_704ILR.SelectionLength;
            Enfocar_704ILR(_txtBase_704ILR);
            _txtBase_704ILR.Select(inicioBase_704ILR, largoBase_704ILR);
        }

        // Al terminar, el foco vuelve a donde estaba: la instancia con el cursor y la
        // seleccion que tenia, o el boton pulsado. Si mientras tanto el usuario lo movio
        // (un clic en la base, Tab hasta Salir), se respeta.
        private void RestaurarFoco_704ILR()
        {
            Control destino_704ILR = _focoPrevio_704ILR;
            _focoPrevio_704ILR = null;
            if (destino_704ILR == null || !destino_704ILR.CanSelect || ControlActivo_704ILR() != _txtBase_704ILR) return;

            Enfocar_704ILR(destino_704ILR);
            if (destino_704ILR == _cboServidor_704ILR)
            {
                int largo_704ILR = _cboServidor_704ILR.Text.Length;
                int inicio_704ILR = Math.Min(_inicioSeleccionPrevia_704ILR, largo_704ILR);
                _cboServidor_704ILR.Select(inicio_704ILR, Math.Min(_largoSeleccionPrevia_704ILR, largo_704ILR - inicio_704ILR));
            }
        }

        // Control activo de la pantalla: el que tiene el foco, o el que lo recupera al
        // volver a ella si ahora lo tiene otra ventana.
        private Control ControlActivo_704ILR()
        {
            Control activo_704ILR = ActiveControl;
            while (activo_704ILR is ContainerControl contenedor_704ILR && contenedor_704ILR.ActiveControl != null)
                activo_704ILR = contenedor_704ILR.ActiveControl;
            return activo_704ILR;
        }

        // Con la pantalla activa el foco se mueve en el acto; si lo tiene otra ventana,
        // el control queda como activo y lo recibe cuando se vuelve a la pantalla.
        private void Enfocar_704ILR(Control control_704ILR)
        {
            if (ContainsFocus) control_704ILR.Focus();
            else ActiveControl = control_704ILR;
        }

        private void Estado_704ILR(string texto_704ILR, bool error_704ILR, bool ok_704ILR = false)
        {
            _exitoVisible_704ILR = ok_704ILR && !error_704ILR;
            _lblEstado_704ILR.Text = texto_704ILR;
            _lblEstado_704ILR.ForeColor = error_704ILR ? Theme_704ILR.Error_704ILR : (ok_704ILR ? Theme_704ILR.Success_704ILR : Theme_704ILR.TextMuted_704ILR);
            _pnlEstado_704ILR.AutoScrollPosition = new Point(0, 0);   // cada diagnostico se lee desde el principio
        }

        // Devuelve la traduccion de 'clave' o, si falta, el texto por defecto dado.
        private static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }
    }
}
