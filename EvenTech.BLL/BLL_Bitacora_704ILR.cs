using System;
using System.Collections.Generic;
using System.Text;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Bitacora general del sistema (T06). Registra cualquier operacion de negocio
    // y permite busqueda combinada. Un fallo de bitacora nunca debe romper el
    // flujo principal, por eso Registrar atrapa sus propias excepciones.
    public static class BLL_Bitacora_704ILR
    {
        // Ancho de Bitacora.Detalle en la base. Un detalle mas largo se recorta ACA,
        // con marca visible, porque el parametro del comando tiene ese tamano fijo y
        // el motor truncaria en silencio: el asiento quedaria cortado sin que nada lo
        // indique (pasa, por ejemplo, con la lista de inconsistencias de integridad).
        private const int MaxDetalle_704ILR = 1000;

        public static void Registrar_704ILR(string modulo_704ILR, string accion_704ILR, CriticidadBitacora_704ILR criticidad_704ILR, string detalle_704ILR)
        {
            try
            {
                DAL_Bitacora_704ILR.Insert_704ILR(new BE_BitacoraEntry_704ILR
                {
                    Fecha_704ILR = DateTime.Now,
                    Usuario_704ILR = UsuarioActual_704ILR(),
                    Modulo_704ILR = modulo_704ILR,
                    Accion_704ILR = accion_704ILR,
                    Criticidad_704ILR = criticidad_704ILR,
                    Detalle_704ILR = Recortar_704ILR(detalle_704ILR)
                });
            }
            catch
            {
                // Silencioso a proposito: la bitacora no debe propagar errores.
            }
        }

        // Registro estandar de excepciones tecnicas: el asiento "Error" de la bitacora y,
        // ademas, el registro serializado en archivo (A03), con el tipo, la pila y las
        // excepciones internas. El archivo se escribe primero: si la falla es justamente
        // que la base no responde, el asiento se pierde pero el archivo queda.
        public static void RegistrarExcepcion_704ILR(Exception ex_704ILR, string modulo_704ILR, string contexto_704ILR)
        {
            try { RegistroDeExcepciones_704ILR.Guardar_704ILR(RegistroDeExcepciones_704ILR.Crear_704ILR(ex_704ILR, modulo_704ILR, contexto_704ILR, UsuarioActual_704ILR())); }
            catch { /* el registro en archivo tampoco propaga errores */ }

            var sb_704ILR = new StringBuilder();
            sb_704ILR.Append("ERROR en ").Append(contexto_704ILR).Append(": ").Append(ex_704ILR?.Message);
            if (ex_704ILR?.InnerException != null)
                sb_704ILR.Append(" | Inner: ").Append(ex_704ILR.InnerException.Message);

            Registrar_704ILR(modulo_704ILR, "Error", CriticidadBitacora_704ILR.Error, sb_704ILR.ToString());
        }

        public static List<BE_BitacoraEntry_704ILR> Buscar_704ILR(BitacoraFiltros_704ILR filtros_704ILR) => DAL_Bitacora_704ILR.Buscar_704ILR(filtros_704ILR);

        public static List<string> GetModulos_704ILR() => DAL_Bitacora_704ILR.GetModulos_704ILR();

        private static string Recortar_704ILR(string detalle_704ILR)
            => detalle_704ILR != null && detalle_704ILR.Length > MaxDetalle_704ILR
                ? detalle_704ILR.Substring(0, MaxDetalle_704ILR - 3) + "..."
                : detalle_704ILR;

        private static string UsuarioActual_704ILR()
        {
            try
            {
                return SessionManager_704ILR.IsSessionActive_704ILR
                    ? SessionManager_704ILR.GetInstance_704ILR.User_704ILR.Username_704ILR
                    : "Sistema";
            }
            catch { return "Sistema"; }
        }
    }
}
