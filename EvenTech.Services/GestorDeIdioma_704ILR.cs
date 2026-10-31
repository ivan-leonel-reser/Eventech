using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EvenTech.BE;

namespace EvenTech.Services
{
    // Sujeto observable del patron Observer (Singleton clasico lock-based).
    // Mantiene la lista de observadores y el diccionario de traducciones por
    // idioma. No accede a la base: la capa BLL lo alimenta al iniciar la app
    // (CargarIdiomas / CargarTraducciones). Al cambiar de idioma, notifica a
    // todos los observadores sin acoplamiento directo entre ellos.
    public class GestorDeIdioma_704ILR
    {
        private static GestorDeIdioma_704ILR _instance_704ILR;
        private static readonly object _lock_704ILR = new object();

        private readonly List<IObservadorIdioma_704ILR> _observadores_704ILR = new List<IObservadorIdioma_704ILR>();
        private readonly List<BE_Idioma_704ILR> _idiomas_704ILR = new List<BE_Idioma_704ILR>();
        // codigoIdioma -> (clave -> texto)
        private readonly Dictionary<string, Dictionary<string, string>> _traducciones_704ILR =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private const string IdiomaPorDefecto_704ILR = "ES";

        private GestorDeIdioma_704ILR() { }

        public static GestorDeIdioma_704ILR GetInstance_704ILR
        {
            get
            {
                if (_instance_704ILR == null)
                {
                    lock (_lock_704ILR)
                    {
                        if (_instance_704ILR == null) _instance_704ILR = new GestorDeIdioma_704ILR();
                    }
                }
                return _instance_704ILR;
            }
        }

        public string IdiomaActual_704ILR { get; private set; } = IdiomaPorDefecto_704ILR;

        public IReadOnlyList<BE_Idioma_704ILR> IdiomasDisponibles_704ILR => _idiomas_704ILR;

        public void CargarIdiomas_704ILR(List<BE_Idioma_704ILR> idiomas_704ILR)
        {
            _idiomas_704ILR.Clear();
            if (idiomas_704ILR != null) _idiomas_704ILR.AddRange(idiomas_704ILR);
        }

        public void CargarTraducciones_704ILR(string codigoIdioma_704ILR, Dictionary<string, string> tabla_704ILR)
        {
            if (string.IsNullOrEmpty(codigoIdioma_704ILR) || tabla_704ILR == null) return;
            _traducciones_704ILR[codigoIdioma_704ILR] = tabla_704ILR;
        }

        // --- Patron Observer ---

        public void Suscribir_704ILR(IObservadorIdioma_704ILR observador_704ILR)
        {
            lock (_lock_704ILR)
            {
                if (observador_704ILR != null && !_observadores_704ILR.Contains(observador_704ILR))
                    _observadores_704ILR.Add(observador_704ILR);
            }
        }

        public void Desuscribir_704ILR(IObservadorIdioma_704ILR observador_704ILR)
        {
            lock (_lock_704ILR)
            {
                _observadores_704ILR.Remove(observador_704ILR);
            }
        }

        public void CambiarIdioma_704ILR(string codigoIdioma_704ILR)
        {
            if (string.IsNullOrEmpty(codigoIdioma_704ILR) || codigoIdioma_704ILR.Equals(IdiomaActual_704ILR, StringComparison.OrdinalIgnoreCase))
                return;

            IdiomaActual_704ILR = codigoIdioma_704ILR;
            NotificarObservadores_704ILR();
        }

        // Re-notifica a los observadores SIN cambiar de idioma. Hace falta cuando lo
        // que cambia no es el idioma seleccionado sino su contenido: al editar las
        // traducciones del idioma activo, CambiarIdioma_704ILR corta por igualdad de
        // codigo y las pantallas seguirian mostrando el texto viejo.
        public void Refrescar_704ILR() => NotificarObservadores_704ILR();

        private void NotificarObservadores_704ILR()
        {
            // Copia para evitar problemas si un observador se desuscribe durante la notificacion.
            IObservadorIdioma_704ILR[] copia_704ILR;
            lock (_lock_704ILR) { copia_704ILR = _observadores_704ILR.ToArray(); }
            foreach (var o_704ILR in copia_704ILR)
            {
                try { o_704ILR.ActualizarTextos_704ILR(); } catch { /* un observador no debe romper a los demas */ }
            }
        }

        // Traduce una clave al idioma actual; si falta, cae al idioma por defecto;
        // si tampoco esta, devuelve la propia clave (util para detectar faltantes).
        // Un texto en blanco (ver TextoEnBlanco_704ILR) cuenta como faltante: dejaria
        // un menu, un titulo o una confirmacion sin texto.
        public string Traducir_704ILR(string clave_704ILR)
        {
            if (string.IsNullOrEmpty(clave_704ILR)) return clave_704ILR;

            if (_traducciones_704ILR.TryGetValue(IdiomaActual_704ILR, out var actual_704ILR) &&
                actual_704ILR.TryGetValue(clave_704ILR, out var texto_704ILR) &&
                !TextoEnBlanco_704ILR(texto_704ILR))
                return texto_704ILR;

            if (_traducciones_704ILR.TryGetValue(IdiomaPorDefecto_704ILR, out var defecto_704ILR) &&
                defecto_704ILR.TryGetValue(clave_704ILR, out var textoDef_704ILR) &&
                !TextoEnBlanco_704ILR(textoDef_704ILR))
                return textoDef_704ILR;

            return clave_704ILR;
        }

        // Ancho de Traducciones.Texto: ninguna plantilla guardada mide mas.
        private const int AnchoPlantilla_704ILR = 250;

        // Con los mismos argumentos, el texto que arma una plantilla guardada puede
        // superar al del texto por defecto por sus propias palabras o por repetir un
        // argumento; mas alla del cuadruple del ancho de una plantilla ya es relleno.
        private const int ToleranciaLargo_704ILR = 4 * AnchoPlantilla_704ILR;

        // Marcador {indice[,alineacion][:formato]}: se leen el valor absoluto de la
        // alineacion y el formato (las llaves escapadas se quitan antes).
        private static readonly Regex MarcadorFormato_704ILR = new Regex(
            "[{][0-9]+[ ]*(?:,[ ]*-?([0-9]+)[ ]*)?(?:[:]([^{}]*))?[}]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Traduccion con marcadores {n} y respaldo en el texto por defecto del codigo,
        // que siempre formatea. El texto por defecto se usa si la clave falta, si el
        // texto guardado esta mal formado (llave sin cerrar, indice que no existe:
        // string.Format lanza) o si es desmesurado: un marcador con una alineacion o
        // una precision mas ancha que cualquier plantilla ({0,999999} armaba un texto
        // de un millon de caracteres y el cuadro de mensaje tardaba casi dos minutos en
        // volver sin llegar a mostrarse) o un resultado que supera por mucho al del
        // texto por defecto. El editor de idiomas no deja guardar esos textos: llegan
        // por SQL, por un respaldo o por un script.
        public string Formatear_704ILR(string clave_704ILR, string defecto_704ILR, params object[] args_704ILR)
        {
            string plantilla_704ILR = Traducir_704ILR(clave_704ILR);
            if (plantilla_704ILR == clave_704ILR || plantilla_704ILR == defecto_704ILR || RellenoDesmesurado_704ILR(plantilla_704ILR))
                return string.Format(defecto_704ILR, args_704ILR);
            string texto_704ILR;
            try { texto_704ILR = string.Format(plantilla_704ILR, args_704ILR); }
            catch (FormatException) { return string.Format(defecto_704ILR, args_704ILR); }
            if (texto_704ILR.Length > ToleranciaLargo_704ILR)
            {
                string porDefecto_704ILR = string.Format(defecto_704ILR, args_704ILR);
                if (texto_704ILR.Length > porDefecto_704ILR.Length + ToleranciaLargo_704ILR) return porDefecto_704ILR;
            }
            return texto_704ILR;
        }

        // Una alineacion ({0,n}) o una precision ({0:Dn}, {0:Nn}) mas ancha que una
        // plantilla no da formato a nada: solo agrega relleno.
        private static bool RellenoDesmesurado_704ILR(string plantilla_704ILR)
        {
            string limpio_704ILR = (plantilla_704ILR ?? string.Empty).Replace("{{", string.Empty).Replace("}}", string.Empty);
            foreach (Match m_704ILR in MarcadorFormato_704ILR.Matches(limpio_704ILR))
            {
                if (MasAnchoQuePlantilla_704ILR(m_704ILR.Groups[1].Value)) return true;
                string formato_704ILR = m_704ILR.Groups[2].Value.Trim();
                if (formato_704ILR.Length > 1 && char.IsLetter(formato_704ILR[0]) && MasAnchoQuePlantilla_704ILR(formato_704ILR.Substring(1)))
                    return true;
            }
            return false;
        }

        // Digitos ASCII que forman un numero mayor que el ancho de una plantilla.
        private static bool MasAnchoQuePlantilla_704ILR(string digitos_704ILR)
        {
            if (digitos_704ILR.Length == 0) return false;
            foreach (char c_704ILR in digitos_704ILR)
                if (c_704ILR < '0' || c_704ILR > '9') return false;
            string significativos_704ILR = digitos_704ILR.TrimStart('0');
            return significativos_704ILR.Length > 3
                || (significativos_704ILR.Length > 0 && int.Parse(significativos_704ILR, CultureInfo.InvariantCulture) > AnchoPlantilla_704ILR);
        }

        // Un texto que no se ve: vacio o hecho solo de caracteres que no dibujan nada
        // por si mismos. Cuentan asi los espacios (tambien el duro o el ideografico),
        // los caracteres de formato y de control, los ignorables por defecto de Unicode
        // (espacio de ancho cero U+200B, guion blando U+00AD, selectores de variante,
        // etiquetas), las marcas combinantes sin un caracter base (U+034F) y los
        // rellenos que se dibujan vacios aunque sean letras o simbolos (U+3164, U+115F,
        // U+1160, U+FFA0, U+2800) y los no caracteres de Unicode (U+FFFE, U+FFFF...).
        // Traducir lo trata como faltante y los editores no lo aceptan como traduccion ni
        // como nombre.
        public static bool TextoEnBlanco_704ILR(string texto_704ILR)
        {
            if (string.IsNullOrEmpty(texto_704ILR)) return true;
            foreach (Rune r_704ILR in texto_704ILR.EnumerateRunes())
            {
                if (SinAncho_704ILR(r_704ILR) || EspacioVisual_704ILR(r_704ILR)) continue;
                UnicodeCategory cat_704ILR = Rune.GetUnicodeCategory(r_704ILR);
                if (cat_704ILR == UnicodeCategory.NonSpacingMark || cat_704ILR == UnicodeCategory.EnclosingMark) continue;
                return false;
            }
            return true;
        }

        // Recorta un texto accesorio al ancho de su columna sin partir un caracter: si el
        // corte cae entre las dos mitades de un par sustituto (un emoji, por ejemplo), el
        // par entero queda afuera. Partido, la base guardaba la mitad suelta como el
        // caracter de reemplazo U+FFFD.
        public static string Recortar_704ILR(string texto_704ILR, int maximo_704ILR)
        {
            if (texto_704ILR == null || texto_704ILR.Length <= maximo_704ILR) return texto_704ILR;
            int corte_704ILR = maximo_704ILR;
            if (corte_704ILR > 0 && char.IsHighSurrogate(texto_704ILR[corte_704ILR - 1])) corte_704ILR--;
            return texto_704ILR.Substring(0, corte_704ILR);
        }

        // El texto tal como se ve, para guardar y comparar nombres: sin los caracteres
        // que no ocupan lugar, con cada tramo de espacios o rellenos como un espacio
        // comun (ninguno en los bordes) y en forma canonica compuesta (una "n" seguida
        // de la tilde combinante queda como la letra compuesta). Un texto hecho solo de
        // caracteres invisibles queda vacio.
        public static string TextoVisible_704ILR(string texto_704ILR)
        {
            if (string.IsNullOrEmpty(texto_704ILR)) return string.Empty;
            var sb_704ILR = new StringBuilder(texto_704ILR.Length);
            bool espacioPendiente_704ILR = false;
            foreach (Rune r_704ILR in texto_704ILR.EnumerateRunes())
            {
                if (SinAncho_704ILR(r_704ILR)) continue;
                if (EspacioVisual_704ILR(r_704ILR)) { espacioPendiente_704ILR = sb_704ILR.Length > 0; continue; }
                if (espacioPendiente_704ILR) { sb_704ILR.Append(' '); espacioPendiente_704ILR = false; }
                sb_704ILR.Append(r_704ILR.ToString());
            }
            try { return sb_704ILR.ToString().Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return sb_704ILR.ToString(); }
        }

        // No ocupan lugar: caracteres de formato (Cf), de control que no son espacios
        // (Cc), los ignorables por defecto de Unicode, asignados o reservados, y los no
        // caracteres (U+FDD0..U+FDEF y los dos ultimos puntos de cada plano, U+FFFE,
        // U+FFFF, U+1FFFE... U+10FFFF): Unicode los reserva para uso interno y no son
        // texto. U+FFFE y U+FFFF no dibujan nada con la fuente de la interfaz, asi que un
        // nombre o una traduccion hecha de ellos se veia vacia sin contar como blanco.
        private static bool SinAncho_704ILR(Rune r_704ILR)
        {
            if (EspacioVisual_704ILR(r_704ILR)) return false;
            UnicodeCategory cat_704ILR = Rune.GetUnicodeCategory(r_704ILR);
            if (cat_704ILR == UnicodeCategory.Format || cat_704ILR == UnicodeCategory.Control) return true;
            int v_704ILR = r_704ILR.Value;
            return v_704ILR == 0x00AD || v_704ILR == 0x034F || v_704ILR == 0x061C
                || (v_704ILR >= 0xFDD0 && v_704ILR <= 0xFDEF)
                || (v_704ILR & 0xFFFE) == 0xFFFE
                || (v_704ILR >= 0x17B4 && v_704ILR <= 0x17B5)
                || (v_704ILR >= 0x180B && v_704ILR <= 0x180F)
                || (v_704ILR >= 0x200B && v_704ILR <= 0x200F)
                || (v_704ILR >= 0x202A && v_704ILR <= 0x202E)
                || (v_704ILR >= 0x2060 && v_704ILR <= 0x206F)
                || (v_704ILR >= 0xFE00 && v_704ILR <= 0xFE0F)
                || v_704ILR == 0xFEFF
                || (v_704ILR >= 0xFFF0 && v_704ILR <= 0xFFF8)
                || (v_704ILR >= 0x1BCA0 && v_704ILR <= 0x1BCA3)
                || (v_704ILR >= 0x1D173 && v_704ILR <= 0x1D17A)
                || (v_704ILR >= 0xE0000 && v_704ILR <= 0xE0FFF);
        }

        // Ocupan lugar sin dibujar nada: los espacios de cualquier tipo, los rellenos de
        // Hangul (U+115F, U+1160, U+3164, U+FFA0) y la celda braille vacia (U+2800), que
        // para Unicode son letras o simbolos.
        private static bool EspacioVisual_704ILR(Rune r_704ILR)
        {
            int v_704ILR = r_704ILR.Value;
            return Rune.IsWhiteSpace(r_704ILR)
                || v_704ILR == 0x115F || v_704ILR == 0x1160 || v_704ILR == 0x3164 || v_704ILR == 0xFFA0 || v_704ILR == 0x2800;
        }
    }
}
