using System.Drawing;

namespace EvenTech.UI
{
    // Paleta, tipografia, espaciado e iconos centralizados (design tokens).
    // Modificar aca para repintar toda la app. Mantiene la identidad de marca
    // (azul oscuro / dorado / silver) y agrega tokens semanticos para superficies,
    // estados y grillas, de modo que ninguna vista hardcodee colores o fuentes.
    internal static class Theme_704ILR
    {
        // ===================== Superficies oscuras (marca) =====================
        public static readonly Color BgLogin_704ILR       = Color.FromArgb(48, 63, 105);   // ventana login
        public static readonly Color BgTitleBar_704ILR    = Color.FromArgb(36, 43, 73);    // barra titulo / topbar / header de grilla
        public static readonly Color BgInput_704ILR       = Color.FromArgb(80, 96, 130);   // inputs sobre fondo oscuro (login)
        public static readonly Color BgSidebar_704ILR     = Color.FromArgb(34, 33, 31);    // menu lateral
        public static readonly Color SidebarHover_704ILR  = Color.FromArgb(50, 49, 47);
        public static readonly Color SidebarActive_704ILR = Color.FromArgb(45, 44, 42);

        // ===================== Superficies claras (contenido) =====================
        public static readonly Color BgContent_704ILR  = Color.FromArgb(244, 245, 248);    // panel central
        public static readonly Color Surface_704ILR    = Color.White;                      // tarjetas / paneles
        public static readonly Color SurfaceAlt_704ILR = Color.FromArgb(246, 247, 249);    // zebra / paneles sutiles
        public static readonly Color Border_704ILR     = Color.FromArgb(223, 227, 233);    // bordes de tarjeta / input

        // ===================== Acentos (dorado) =====================
        public static readonly Color Accent_704ILR            = Color.FromArgb(185, 160, 91);
        public static readonly Color AccentButton_704ILR      = Color.FromArgb(157, 112, 53);
        public static readonly Color AccentButtonHover_704ILR = Color.FromArgb(184, 134, 67);
        public static readonly Color AccentButtonDown_704ILR  = Color.FromArgb(126, 89, 41);

        // ===================== Texto =====================
        public static readonly Color TextOnDark_704ILR  = Color.White;
        public static readonly Color TextLight_704ILR   = Color.FromArgb(206, 212, 222);   // texto secundario sobre oscuro
        public static readonly Color TextOnLight_704ILR = Color.FromArgb(33, 37, 41);
        public static readonly Color TextMuted_704ILR   = Color.FromArgb(108, 117, 125);   // hints / captions sobre claro

        // ===================== Estados semanticos =====================
        public static readonly Color Success_704ILR = Color.FromArgb(33, 136, 56);
        public static readonly Color Error_704ILR   = Color.FromArgb(190, 49, 68);
        public static readonly Color Warning_704ILR = Color.FromArgb(181, 132, 26);

        // ===================== Botones neutros (secundarios) =====================
        public static readonly Color Neutral_704ILR      = Color.FromArgb(108, 117, 125);
        public static readonly Color NeutralHover_704ILR = Color.FromArgb(127, 135, 143);
        public static readonly Color NeutralDown_704ILR  = Color.FromArgb(84, 91, 98);

        // ===================== Grilla =====================
        public static readonly Color GridHeaderBg_704ILR = Color.FromArgb(36, 43, 73);
        public static readonly Color GridHeaderFg_704ILR = Color.White;
        public static readonly Color GridZebra_704ILR    = Color.FromArgb(246, 247, 249);
        public static readonly Color GridSelectBg_704ILR = Color.FromArgb(238, 230, 207);  // dorado claro (en vez del azul default)
        public static readonly Color GridSelectFg_704ILR = Color.FromArgb(33, 37, 41);
        public static readonly Color GridLines_704ILR    = Color.FromArgb(233, 236, 240);

        // ===================== Tipografia =====================
        // Ebrima viene con Windows; si falta, WinForms cae al sans-serif por defecto.
        private const string Family_704ILR = "Ebrima";
        public static readonly Font FontDisplay_704ILR  = new Font(Family_704ILR, 22F, FontStyle.Bold);   // logo
        public static readonly Font FontH1_704ILR       = new Font(Family_704ILR, 18F, FontStyle.Bold);   // titulo de pagina
        public static readonly Font FontH2_704ILR       = new Font(Family_704ILR, 14F, FontStyle.Bold);   // titulo de seccion / tarjeta
        public static readonly Font FontTitle_704ILR    = new Font(Family_704ILR, 13F, FontStyle.Bold);
        public static readonly Font FontBody_704ILR     = new Font(Family_704ILR, 11F, FontStyle.Regular);
        public static readonly Font FontBodyBold_704ILR = new Font(Family_704ILR, 11F, FontStyle.Bold);
        public static readonly Font FontInput_704ILR    = new Font(Family_704ILR, 11F, FontStyle.Regular);
        public static readonly Font FontButton_704ILR   = new Font(Family_704ILR, 10.5F, FontStyle.Bold);
        public static readonly Font FontSmall_704ILR    = new Font(Family_704ILR, 9.5F, FontStyle.Regular);
        public static readonly Font FontCaption_704ILR  = new Font(Family_704ILR, 9F, FontStyle.Regular);
        public static readonly Font FontMenu_704ILR     = new Font(Family_704ILR, 11.5F, FontStyle.Bold);
        public static readonly Font FontIcon_704ILR     = new Font("Segoe MDL2 Assets", 13F);       // glifos de icono
        public static readonly Font FontWinCtl_704ILR   = new Font("Segoe MDL2 Assets", 9F);        // botones de ventana

        // ===================== Espaciado (escala 4) =====================
        public const int SpaceXs_704ILR  = 4;
        public const int SpaceSm_704ILR  = 8;
        public const int SpaceMd_704ILR  = 12;
        public const int SpaceLg_704ILR  = 16;
        public const int SpaceXl_704ILR  = 24;

        // ===================== Radios de esquina =====================
        public const int RadiusSm_704ILR = 6;
        public const int RadiusMd_704ILR = 10;

        // ===================== Iconos (Segoe MDL2 Assets, area de uso privado) =====================
        // Se construyen desde el codepoint para no depender de caracteres especiales en el .cs.
        private static string Glyph_704ILR(int cp_704ILR) => ((char)cp_704ILR).ToString();
        public static readonly string IcoHome_704ILR     = Glyph_704ILR(0xE80F);
        public static readonly string IcoCalendar_704ILR = Glyph_704ILR(0xE787);
        public static readonly string IcoPeople_704ILR   = Glyph_704ILR(0xE716);
        public static readonly string IcoContact_704ILR  = Glyph_704ILR(0xE77B); // Contact (clientes)
        public static readonly string IcoServicio_704ILR = Glyph_704ILR(0xE719); // Shop (servicios)
        public static readonly string IcoGlobe_704ILR    = Glyph_704ILR(0xE774);
        public static readonly string IcoHistory_704ILR  = Glyph_704ILR(0xE81C);
        public static readonly string IcoLock_704ILR     = Glyph_704ILR(0xE72E);
        public static readonly string IcoClose_704ILR    = Glyph_704ILR(0xE8BB); // ChromeClose
        public static readonly string IcoMinimize_704ILR = Glyph_704ILR(0xE921); // ChromeMinimize
        public static readonly string IcoMaximize_704ILR = Glyph_704ILR(0xE922); // ChromeMaximize
        public static readonly string IcoRestore_704ILR  = Glyph_704ILR(0xE923); // ChromeRestore (ventana maximizada)

        // Isologotipo del sistema, embebido en el ensamblado. Se carga una sola vez.
        private static System.Drawing.Image _logo_704ILR;
        private static bool _logoBuscado_704ILR;

        public static System.Drawing.Image Logo_704ILR
        {
            get
            {
                if (_logoBuscado_704ILR) return _logo_704ILR;
                _logoBuscado_704ILR = true;
                try
                {
                    var asm_704ILR = System.Reflection.Assembly.GetExecutingAssembly();
                    string nombre_704ILR = System.Array.Find(asm_704ILR.GetManifestResourceNames(),
                        n_704ILR => n_704ILR.EndsWith("logo_eventech.png", System.StringComparison.OrdinalIgnoreCase));
                    if (nombre_704ILR != null)
                        using (var st_704ILR = asm_704ILR.GetManifestResourceStream(nombre_704ILR))
                            _logo_704ILR = System.Drawing.Image.FromStream(st_704ILR);
                }
                catch { _logo_704ILR = null; }   // sin logo la pantalla igual funciona
                return _logo_704ILR;
            }
        }

        // Variante compacta del isologotipo (isotipo + EVENTECH, sin el lema) para
        // los lugares donde se dibuja chico: a 60-100 px de alto el lema queda como
        // una franja ilegible. Se recorta del mismo recurso, sin un segundo archivo.
        // El recorte responde al trazado del generador del logo (1300x720: icono y
        // logotipo hasta la fila 500, lema debajo); con otro tamano de imagen se
        // devuelve null y el llamador usa el logo completo.
        private static System.Drawing.Image _logoCompacto_704ILR;
        private static bool _logoCompactoBuscado_704ILR;

        public static System.Drawing.Image LogoCompacto_704ILR
        {
            get
            {
                if (_logoCompactoBuscado_704ILR) return _logoCompacto_704ILR;
                _logoCompactoBuscado_704ILR = true;
                try
                {
                    if (Logo_704ILR is Bitmap completo_704ILR && completo_704ILR.Width == 1300 && completo_704ILR.Height == 720)
                        _logoCompacto_704ILR = completo_704ILR.Clone(new Rectangle(375, 146, 551, 355), completo_704ILR.PixelFormat);
                }
                catch { _logoCompacto_704ILR = null; }
                return _logoCompacto_704ILR;
            }
        }
        public static readonly string IcoSearch_704ILR   = Glyph_704ILR(0xE721);
        public static readonly string IcoAdd_704ILR      = Glyph_704ILR(0xE710);
        public static readonly string IcoSave_704ILR     = Glyph_704ILR(0xE74E);
        public static readonly string IcoClear_704ILR    = Glyph_704ILR(0xE894); // Clear
        public static readonly string IcoWarning_704ILR  = Glyph_704ILR(0xE7BA);
        public static readonly string IcoLogout_704ILR   = Glyph_704ILR(0xF3B1); // SignOut
        public static readonly string IcoEye_704ILR      = Glyph_704ILR(0xE7B3); // RedEye (ver contrasena; tachado a mano con la clave a la vista)
        public static readonly string IcoUnlock_704ILR   = Glyph_704ILR(0xE785); // Unlock (desbloquear)
        public static readonly string IcoPago_704ILR     = Glyph_704ILR(0xE8C7); // PaymentCard (pagos)
        public static readonly string IcoDocumento_704ILR = Glyph_704ILR(0xE8A5); // Document (comprobante)
        public static readonly string IcoEmail_704ILR     = Glyph_704ILR(0xE715); // Mail (enviar comprobante)
        // Proceso 2: personal, coordinacion y ejecucion de los eventos.
        public static readonly string IcoEmpleado_704ILR    = Glyph_704ILR(0xE7EE); // OtherUser (empleados)
        public static readonly string IcoOperaciones_704ILR = Glyph_704ILR(0xE9D5); // CheckList (operaciones)
        public static readonly string IcoAgenda_704ILR      = Glyph_704ILR(0xE8BF); // CalendarDay (mi agenda)
        public static readonly string IcoReloj_704ILR       = Glyph_704ILR(0xE823); // Recent (cronograma)
        public static readonly string IcoTarea_704ILR       = Glyph_704ILR(0xE8FD); // BulletedList (tareas)
        public static readonly string IcoOk_704ILR          = Glyph_704ILR(0xE73E); // CheckMark (confirmar)
        public static readonly string IcoArriba_704ILR      = Glyph_704ILR(0xE70E); // ChevronUp (subir)
        public static readonly string IcoAbajo_704ILR       = Glyph_704ILR(0xE70D); // ChevronDown (bajar)
        public static readonly string IcoIniciar_704ILR     = Glyph_704ILR(0xE768); // Play (iniciar ejecucion)
        public static readonly string IcoEliminar_704ILR    = Glyph_704ILR(0xE74D); // Delete (eliminar el cronograma)
    }
}
