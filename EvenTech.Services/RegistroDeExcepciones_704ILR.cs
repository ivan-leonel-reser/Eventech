using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using EvenTech.BE;

namespace EvenTech.Services
{
    // Registro serializado de excepciones (A03). Cada excepcion tecnica que se asienta en
    // la bitacora queda ademas como un archivo XML propio en la carpeta comun del equipo
    // (%ProgramData%\EvenTech\excepciones), con el tipo, la pila de llamadas y las
    // excepciones internas. Un archivo por excepcion: escribir no obliga a reescribir un
    // documento que crece, dos puestos de la misma maquina no se pisan y un archivo danado
    // no arrastra a los demas.
    //
    // Se serializa con XmlSerializer, del propio framework. Es un servicio transversal que
    // no usa la base: por eso sigue registrando cuando la base no responde.
    //
    // Nunca lanza al escribir (un registro de fallas no puede provocar otra falla). Al
    // leer, lo que no se puede interpretar se informa como ilegible, sin cortar la lista.
    public static class RegistroDeExcepciones_704ILR
    {
        // Cantidad de archivos que se conservan: al pasarla se borran los mas viejos.
        public const int Retencion_704ILR = 500;
        // Tope de un lote que se abre desde un archivo elegido por el usuario.
        public const long MaxBytesLote_704ILR = 20L * 1024 * 1024;

        private const int MaxTexto_704ILR = 200;
        private const int MaxMensaje_704ILR = 4000;
        private const int MaxPila_704ILR = 16000;
        private const int MaxInternas_704ILR = 8;
        private const string Prefijo_704ILR = "excepcion_";

        private static readonly XmlSerializer SerializadorRegistro_704ILR = new XmlSerializer(typeof(BE_RegistroExcepcion_704ILR));
        private static readonly XmlSerializer SerializadorLote_704ILR = new XmlSerializer(typeof(BE_LoteExcepciones_704ILR));
        private static readonly object _lock_704ILR = new object();

        // Un valor que parezca una credencial ("Password=...", "Pwd=...") se reemplaza
        // antes de escribir: ningun mensaje deberia traerla, pero el archivo no es el
        // lugar para comprobarlo.
        private static readonly Regex Credencial_704ILR = new Regex(
            @"(?i)\b(password|pwd|contrase(n|ñ)a|clave)\s*=\s*[^;""'\s]*", RegexOptions.CultureInvariant);

        private static string _carpetaDeEstaEjecucion_704ILR;

        public static string Carpeta_704ILR =>
            _carpetaDeEstaEjecucion_704ILR ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EvenTech", "excepciones");

        // Para la prueba automatizada: escribe y lee en otra carpeta durante el proceso.
        public static void UsarCarpetaEnEstaEjecucion_704ILR(string carpeta_704ILR) => _carpetaDeEstaEjecucion_704ILR = carpeta_704ILR;

        // Arma el registro de una excepcion. Recorta, limpia los caracteres que XML no
        // admite y oculta lo que parezca una credencial.
        public static BE_RegistroExcepcion_704ILR Crear_704ILR(Exception ex_704ILR, string modulo_704ILR, string contexto_704ILR, string usuario_704ILR)
        {
            var r_704ILR = new BE_RegistroExcepcion_704ILR
            {
                Id_704ILR = Guid.NewGuid().ToString("N"),
                Fecha_704ILR = DateTime.Now,
                Usuario_704ILR = Limpiar_704ILR(usuario_704ILR, MaxTexto_704ILR),
                Equipo_704ILR = Limpiar_704ILR(Environment.MachineName, MaxTexto_704ILR),
                Version_704ILR = TextoVersion_704ILR(),
                Modulo_704ILR = Limpiar_704ILR(modulo_704ILR, MaxTexto_704ILR),
                Contexto_704ILR = Limpiar_704ILR(contexto_704ILR, MaxTexto_704ILR),
                Tipo_704ILR = Limpiar_704ILR(ex_704ILR?.GetType().FullName ?? "(sin excepcion)", MaxTexto_704ILR),
                Mensaje_704ILR = Limpiar_704ILR(ex_704ILR?.Message, MaxMensaje_704ILR),
                Origen_704ILR = Limpiar_704ILR(ex_704ILR?.Source, MaxTexto_704ILR),
                Pila_704ILR = Limpiar_704ILR(ex_704ILR?.StackTrace, MaxPila_704ILR)
            };
            Exception interna_704ILR = ex_704ILR?.InnerException;
            while (interna_704ILR != null && r_704ILR.Internas_704ILR.Count < MaxInternas_704ILR)
            {
                r_704ILR.Internas_704ILR.Add(new BE_ExcepcionInterna_704ILR
                {
                    Tipo_704ILR = Limpiar_704ILR(interna_704ILR.GetType().FullName, MaxTexto_704ILR),
                    Mensaje_704ILR = Limpiar_704ILR(interna_704ILR.Message, MaxMensaje_704ILR)
                });
                interna_704ILR = interna_704ILR.InnerException;
            }
            return r_704ILR;
        }

        // Escribe el registro en su archivo. Devuelve la ruta, o null si no se pudo
        // (carpeta sin permiso, disco lleno): el resto del sistema sigue igual.
        public static string Guardar_704ILR(BE_RegistroExcepcion_704ILR registro_704ILR)
        {
            if (registro_704ILR == null) return null;
            try
            {
                lock (_lock_704ILR)
                {
                    string carpeta_704ILR = Carpeta_704ILR;
                    Directory.CreateDirectory(carpeta_704ILR);
                    if (string.IsNullOrWhiteSpace(registro_704ILR.Id_704ILR)) registro_704ILR.Id_704ILR = Guid.NewGuid().ToString("N");
                    string nombre_704ILR = Prefijo_704ILR + registro_704ILR.Fecha_704ILR.ToString("yyyyMMdd_HHmmss_fff",
                        System.Globalization.CultureInfo.InvariantCulture) + "_" + registro_704ILR.Id_704ILR.Substring(0, Math.Min(8, registro_704ILR.Id_704ILR.Length)) + ".xml";
                    string ruta_704ILR = Path.Combine(carpeta_704ILR, nombre_704ILR);
                    // Se escribe a un temporal y se renombra: un corte a mitad de camino no
                    // deja un XML a medias con el nombre definitivo.
                    string temporal_704ILR = ruta_704ILR + ".tmp";
                    using (var flujo_704ILR = new FileStream(temporal_704ILR, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var escritor_704ILR = XmlWriter.Create(flujo_704ILR, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
                        SerializadorRegistro_704ILR.Serialize(escritor_704ILR, registro_704ILR);
                    File.Move(temporal_704ILR, ruta_704ILR, true);
                    registro_704ILR.Archivo_704ILR = ruta_704ILR;
                    AplicarRetencion_704ILR(carpeta_704ILR);
                    return ruta_704ILR;
                }
            }
            catch (Exception ex_704ILR) when (ex_704ILR is IOException || ex_704ILR is UnauthorizedAccessException ||
                                              ex_704ILR is InvalidOperationException || ex_704ILR is System.Security.SecurityException)
            {
                return null;
            }
        }

        // Los registros del equipo, del mas reciente al mas viejo. Un archivo que no se
        // puede interpretar figura como ilegible, con su nombre y su fecha.
        public static List<BE_RegistroExcepcion_704ILR> Listar_704ILR()
        {
            var lista_704ILR = new List<BE_RegistroExcepcion_704ILR>();
            string carpeta_704ILR = Carpeta_704ILR;
            if (!Directory.Exists(carpeta_704ILR)) return lista_704ILR;
            foreach (string ruta_704ILR in Directory.GetFiles(carpeta_704ILR, Prefijo_704ILR + "*.xml")
                                                    .OrderByDescending(r_704ILR => Path.GetFileName(r_704ILR), StringComparer.Ordinal)
                                                    .Take(Retencion_704ILR))
            {
                BE_RegistroExcepcion_704ILR r_704ILR;
                try
                {
                    using var flujo_704ILR = new FileStream(ruta_704ILR, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    r_704ILR = (BE_RegistroExcepcion_704ILR)SerializadorRegistro_704ILR.Deserialize(LectorSeguro_704ILR(flujo_704ILR, MaxBytesLote_704ILR));
                }
                catch (Exception ex_704ILR) when (ex_704ILR is InvalidOperationException || ex_704ILR is XmlException ||
                                                  ex_704ILR is IOException || ex_704ILR is UnauthorizedAccessException)
                {
                    r_704ILR = new BE_RegistroExcepcion_704ILR
                    {
                        Ilegible_704ILR = true,
                        Fecha_704ILR = FechaDeArchivo_704ILR(ruta_704ILR)
                    };
                }
                r_704ILR.Archivo_704ILR = ruta_704ILR;
                lista_704ILR.Add(r_704ILR);
            }
            return lista_704ILR;
        }

        // Escribe un lote con los registros dados (los ilegibles no viajan).
        public static BE_LoteExcepciones_704ILR ExportarLote_704ILR(IEnumerable<BE_RegistroExcepcion_704ILR> registros_704ILR, string ruta_704ILR, string usuario_704ILR)
        {
            if (string.IsNullOrWhiteSpace(ruta_704ILR)) throw new ArgumentException("Falta el archivo de destino.", nameof(ruta_704ILR));
            var lote_704ILR = new BE_LoteExcepciones_704ILR
            {
                Generado_704ILR = DateTime.Now,
                Equipo_704ILR = Limpiar_704ILR(Environment.MachineName, MaxTexto_704ILR),
                Usuario_704ILR = Limpiar_704ILR(usuario_704ILR, MaxTexto_704ILR),
                Version_704ILR = TextoVersion_704ILR(),
                Registros_704ILR = (registros_704ILR ?? Enumerable.Empty<BE_RegistroExcepcion_704ILR>())
                                   .Where(r_704ILR => r_704ILR != null && !r_704ILR.Ilegible_704ILR).ToList()
            };
            string temporal_704ILR = ruta_704ILR + ".tmp";
            using (var flujo_704ILR = new FileStream(temporal_704ILR, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var escritor_704ILR = XmlWriter.Create(flujo_704ILR, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
                SerializadorLote_704ILR.Serialize(escritor_704ILR, lote_704ILR);
            File.Move(temporal_704ILR, ruta_704ILR, true);
            return lote_704ILR;
        }

        // Abre un lote exportado. Un archivo que no es un lote (otro XML, un documento
        // danado, uno demasiado grande) se rechaza con InvalidDataException.
        public static BE_LoteExcepciones_704ILR AbrirLote_704ILR(string ruta_704ILR)
        {
            var info_704ILR = new FileInfo(ruta_704ILR);
            if (!info_704ILR.Exists) throw new FileNotFoundException("No existe el archivo.", ruta_704ILR);
            if (info_704ILR.Length > MaxBytesLote_704ILR) throw new InvalidDataException("El archivo supera el tamano admitido para un lote.");
            BE_LoteExcepciones_704ILR lote_704ILR;
            try
            {
                using var flujo_704ILR = new FileStream(ruta_704ILR, FileMode.Open, FileAccess.Read, FileShare.Read);
                XmlReader lector_704ILR = LectorSeguro_704ILR(flujo_704ILR, MaxBytesLote_704ILR);
                if (!SerializadorLote_704ILR.CanDeserialize(lector_704ILR))
                    throw new InvalidDataException("El archivo no es un lote de excepciones.");
                lote_704ILR = (BE_LoteExcepciones_704ILR)SerializadorLote_704ILR.Deserialize(lector_704ILR);
            }
            catch (Exception ex_704ILR) when (ex_704ILR is InvalidOperationException || ex_704ILR is XmlException)
            {
                throw new InvalidDataException("El archivo no es un lote de excepciones valido.", ex_704ILR);
            }
            if (lote_704ILR == null || lote_704ILR.Formato_704ILR != 1)
                throw new InvalidDataException("El lote es de un formato que esta version no reconoce.");
            lote_704ILR.Registros_704ILR ??= new List<BE_RegistroExcepcion_704ILR>();
            lote_704ILR.Registros_704ILR.RemoveAll(r_704ILR => r_704ILR == null);
            return lote_704ILR;
        }

        // Lector sin DTD ni resolucion de entidades externas, con tope de tamano: el
        // archivo puede venir de otro equipo.
        private static XmlReader LectorSeguro_704ILR(Stream flujo_704ILR, long maxCaracteres_704ILR) =>
            XmlReader.Create(flujo_704ILR, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = maxCaracteres_704ILR,
                IgnoreComments = true
            });

        private static void AplicarRetencion_704ILR(string carpeta_704ILR)
        {
            string[] archivos_704ILR = Directory.GetFiles(carpeta_704ILR, Prefijo_704ILR + "*.xml");
            if (archivos_704ILR.Length <= Retencion_704ILR) return;
            foreach (string viejo_704ILR in archivos_704ILR.OrderBy(a_704ILR => Path.GetFileName(a_704ILR), StringComparer.Ordinal)
                                                           .Take(archivos_704ILR.Length - Retencion_704ILR))
            {
                try { File.Delete(viejo_704ILR); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static DateTime FechaDeArchivo_704ILR(string ruta_704ILR)
        {
            try { return File.GetLastWriteTime(ruta_704ILR); } catch { return DateTime.MinValue; }
        }

        private static string TextoVersion_704ILR()
        {
            var v_704ILR = VersionSistema_704ILR.Actual_704ILR;
            return v_704ILR.Commit_704ILR == null ? v_704ILR.Version_704ILR
                : v_704ILR.Version_704ILR + "+" + v_704ILR.CommitCorto_704ILR + (v_704ILR.ConCambios_704ILR ? ".modificado" : "");
        }

        // Quita los caracteres que XML 1.0 no admite (un mensaje con un caracter de
        // control haria fallar la escritura), oculta credenciales y recorta sin partir
        // un par sustituto.
        public static string Limpiar_704ILR(string texto_704ILR, int maximo_704ILR)
        {
            if (string.IsNullOrEmpty(texto_704ILR)) return texto_704ILR ?? string.Empty;
            var sb_704ILR = new StringBuilder(Math.Min(texto_704ILR.Length, maximo_704ILR + 1));
            for (int i_704ILR = 0; i_704ILR < texto_704ILR.Length && sb_704ILR.Length < maximo_704ILR; i_704ILR++)
            {
                char c_704ILR = texto_704ILR[i_704ILR];
                if (char.IsHighSurrogate(c_704ILR))
                {
                    if (i_704ILR + 1 < texto_704ILR.Length && char.IsLowSurrogate(texto_704ILR[i_704ILR + 1]))
                    {
                        if (sb_704ILR.Length + 2 > maximo_704ILR) break;
                        sb_704ILR.Append(c_704ILR).Append(texto_704ILR[i_704ILR + 1]);
                        i_704ILR++;
                    }
                    continue;   // sustituto suelto: no es un caracter valido
                }
                if (char.IsLowSurrogate(c_704ILR)) continue;
                if (XmlConvert.IsXmlChar(c_704ILR)) sb_704ILR.Append(c_704ILR);
            }
            return Credencial_704ILR.Replace(sb_704ILR.ToString(), "$1=***");
        }
    }
}
