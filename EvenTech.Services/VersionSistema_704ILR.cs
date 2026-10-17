using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace EvenTech.Services
{
    // Version del sistema y commit del que salio el codigo. Los dos viajan en el propio
    // ensamblado: Directory.Build.targets escribe la version de producto
    // "2.0.0+<hash>[.modificado]" y el atributo de metadatos "FechaCommit". La pantalla de
    // acceso y la ventana principal los muestran para comprobar que el programa compilado
    // desde el zip entregado es el mismo commit que esta en el repositorio.
    public sealed class VersionSistema_704ILR
    {
        private static readonly Regex Patron_704ILR =
            new Regex(@"^(?<version>[0-9]+(\.[0-9]+){1,3})(\+(?<hash>[0-9a-f]{40})(?<mod>\.modificado)?)?$",
                      RegexOptions.CultureInvariant);

        private static readonly Lazy<VersionSistema_704ILR> _actual_704ILR =
            new Lazy<VersionSistema_704ILR>(() => DeEnsamblado_704ILR(typeof(VersionSistema_704ILR).Assembly));

        // "2.0.0". Nunca vacia: sin datos, "?".
        public string Version_704ILR { get; private set; }
        // Hash completo del commit (40 caracteres), o null si el ejecutable no lo trae.
        public string Commit_704ILR { get; private set; }
        // El ejecutable se compilo desde un repositorio con cambios sin commitear: no es
        // el de ningun commit.
        public bool ConCambios_704ILR { get; private set; }
        // Fecha del commit, o null.
        public DateTime? FechaCommit_704ILR { get; private set; }

        // Los siete primeros caracteres, como los muestra el repositorio.
        public string CommitCorto_704ILR => Commit_704ILR?.Substring(0, 7);

        // La de esta compilacion: todos los ensamblados de la solucion llevan el mismo sello.
        public static VersionSistema_704ILR Actual_704ILR => _actual_704ILR.Value;

        private VersionSistema_704ILR() { }

        public static VersionSistema_704ILR DeEnsamblado_704ILR(Assembly ensamblado_704ILR)
        {
            string informacional_704ILR = ensamblado_704ILR?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            string fecha_704ILR = ensamblado_704ILR?.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a_704ILR => a_704ILR.Key == "FechaCommit")?.Value;
            return Interpretar_704ILR(informacional_704ILR, fecha_704ILR);
        }

        // Interpreta la version de producto y la fecha tal como las escribe la compilacion.
        // Lo que no tiene esa forma queda como "sin datos" en vez de mostrarse a medias.
        public static VersionSistema_704ILR Interpretar_704ILR(string informacional_704ILR, string fecha_704ILR)
        {
            var v_704ILR = new VersionSistema_704ILR { Version_704ILR = "?" };
            Match m_704ILR = Patron_704ILR.Match((informacional_704ILR ?? string.Empty).Trim());
            if (!m_704ILR.Success) return v_704ILR;

            v_704ILR.Version_704ILR = m_704ILR.Groups["version"].Value;
            if (m_704ILR.Groups["hash"].Success)
            {
                v_704ILR.Commit_704ILR = m_704ILR.Groups["hash"].Value;
                v_704ILR.ConCambios_704ILR = m_704ILR.Groups["mod"].Success;
                if (DateTime.TryParseExact(fecha_704ILR, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                           DateTimeStyles.None, out DateTime f_704ILR))
                    v_704ILR.FechaCommit_704ILR = f_704ILR;
            }
            return v_704ILR;
        }
    }
}
