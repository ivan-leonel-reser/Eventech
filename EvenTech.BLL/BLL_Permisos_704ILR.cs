using System;
using EvenTech.BE;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // La sesion abierta no tiene el permiso que exige la operacion de negocio.
    public class PermisoDenegadoException_704ILR : Exception
    {
        public string Clave_704ILR { get; }

        public PermisoDenegadoException_704ILR(string clave_704ILR)
            : base("La sesion no tiene el permiso " + clave_704ILR + ".")
        {
            Clave_704ILR = clave_704ILR;
        }
    }

    // Control de acceso, tercera capa: la operacion de escritura vuelve a exigir su
    // permiso en la capa de negocio. Las dos primeras viven en la interfaz (el menu
    // muestra solo las secciones con permiso y cada boton lo exige al ejecutarse);
    // esta impide que una sesion sin el permiso escriba llamando a la capa de negocio
    // por otro camino. Es la misma clave que exige la pantalla, asi que en el uso
    // normal nunca rechaza: si rechaza, alguien salteo la interfaz.
    //
    // Sin sesion abierta no se exige: es el propio sistema el que opera (arranque,
    // instalador, herramientas de mantenimiento y pruebas). Ese camino no es una
    // barrera que agregar aca: quien puede ejecutar codigo sin iniciar sesion tambien
    // puede escribir directo en la base con las mismas credenciales de Windows.
    public static class BLL_Permisos_704ILR
    {
        public static void Exigir_704ILR(string clave_704ILR, string accion_704ILR)
        {
            if (!SessionManager_704ILR.IsSessionActive_704ILR) return;
            if (SessionManager_704ILR.GetInstance_704ILR.TienePermiso_704ILR(clave_704ILR)) return;

            // Mismo asiento que la segunda capa de la interfaz, con la capa que lo detecto.
            BLL_Bitacora_704ILR.Registrar_704ILR("Seguridad", "Acceso denegado", CriticidadBitacora_704ILR.Advertencia,
                "Permiso faltante: " + clave_704ILR + " | Accion: " + accion_704ILR + " (capa de negocio)");
            throw new PermisoDenegadoException_704ILR(clave_704ILR);
        }
    }
}
