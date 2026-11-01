using System.Collections.Generic;
using System.IO;
using EvenTech.BE;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Consulta del registro serializado de excepciones (A03, CUS003). Los registros los
    // escribe BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR; aca se leen, se exporta un
    // lote para quien da soporte y se abre un lote exportado en otro equipo. Exportar y
    // abrir quedan asentados en la bitacora.
    public static class BLL_Excepciones_704ILR
    {
        public const string Permiso_704ILR = "EXCEPCIONES_VER";

        public static List<BE_RegistroExcepcion_704ILR> Listar_704ILR() => RegistroDeExcepciones_704ILR.Listar_704ILR();

        public static string Carpeta_704ILR => RegistroDeExcepciones_704ILR.Carpeta_704ILR;

        // Escribe el lote con los registros dados. Devuelve cuantos registros viajaron.
        public static int Exportar_704ILR(IEnumerable<BE_RegistroExcepcion_704ILR> registros_704ILR, string ruta_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR(Permiso_704ILR, "exportar el registro de excepciones");
            var lote_704ILR = RegistroDeExcepciones_704ILR.ExportarLote_704ILR(registros_704ILR, ruta_704ILR, UsuarioSesion_704ILR());
            BLL_Bitacora_704ILR.Registrar_704ILR("Excepciones", "Lote de excepciones exportado", CriticidadBitacora_704ILR.Info,
                $"Registros: {lote_704ILR.Registros_704ILR.Count} | Archivo: {Path.GetFileName(ruta_704ILR)}");
            return lote_704ILR.Registros_704ILR.Count;
        }

        // Abre un lote exportado (de este equipo o de otro). Un archivo que no es un lote
        // se rechaza con InvalidDataException y el rechazo queda asentado.
        public static BE_LoteExcepciones_704ILR AbrirLote_704ILR(string ruta_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR(Permiso_704ILR, "abrir un lote de excepciones");
            try
            {
                var lote_704ILR = RegistroDeExcepciones_704ILR.AbrirLote_704ILR(ruta_704ILR);
                BLL_Bitacora_704ILR.Registrar_704ILR("Excepciones", "Lote de excepciones abierto", CriticidadBitacora_704ILR.Info,
                    $"Registros: {lote_704ILR.Registros_704ILR.Count} | Equipo de origen: {lote_704ILR.Equipo_704ILR} | Archivo: {Path.GetFileName(ruta_704ILR)}");
                return lote_704ILR;
            }
            catch (InvalidDataException ex_704ILR)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Excepciones", "Lote de excepciones rechazado", CriticidadBitacora_704ILR.Advertencia,
                    $"{ex_704ILR.Message} | Archivo: {Path.GetFileName(ruta_704ILR)}");
                throw;
            }
        }

        private static string UsuarioSesion_704ILR() =>
            SessionManager_704ILR.IsSessionActive_704ILR ? SessionManager_704ILR.GetInstance_704ILR.User_704ILR.Username_704ILR : "Sistema";
    }
}
