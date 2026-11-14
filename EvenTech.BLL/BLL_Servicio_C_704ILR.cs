using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum ActivacionVersionResult_704ILR
    {
        Success_704ILR,
        VersionInexistente_704ILR,   // la version elegida no existe (o se paso un id invalido)
        YaVigente_704ILR,            // la version elegida ya tiene Act = 1: no hay nada que activar
        NombreDuplicado_704ILR       // otro servicio usa hoy el nombre de esa version (UQ_Servicios_Nombre)
    }

    // Bitacora de cambios del catalogo de servicios (T06.B, consigna de la catedra):
    // dbo.Servicios es la tabla activa y dbo.Servicios_C, la copia. Las versiones las
    // escriben los triggers de dbo.Servicios en cada alta, modificacion o baja (ver
    // db/schema.sql); esta clase consulta la copia y activa una version guardada, que
    // es asignarle Act = 1: el servicio vuelve a los datos de esa version.
    public static class BLL_Servicio_C_704ILR
    {
        public const string PermisoVer_704ILR = "SERVICIOS_CAMBIOS_VER";
        public const string PermisoActivar_704ILR = "SERVICIOS_CAMBIOS_ACTIVAR";

        // Consulta con los filtros de la pantalla (codigo, nombre, fecha inicial y
        // final), en orden cronologico. La habilita cualquiera de los dos permisos de
        // la bitacora: quien activa versiones tiene que poder verlas. Un rango
        // invertido no se consulta (la pantalla lo avisa antes).
        public static List<BE_Servicio_C_704ILR> Buscar_704ILR(FiltrosServicio_C_704ILR filtros_704ILR)
        {
            if (!PuedeConsultar_704ILR())
                BLL_Permisos_704ILR.Exigir_704ILR(PermisoVer_704ILR, "consultar la bitacora de cambios del catalogo");
            filtros_704ILR = filtros_704ILR ?? new FiltrosServicio_C_704ILR();
            if (!RangoValido_704ILR(filtros_704ILR.FechaInicial_704ILR, filtros_704ILR.FechaFinal_704ILR))
                return new List<BE_Servicio_C_704ILR>();
            return DAL_Servicio_C_704ILR.Buscar_704ILR(filtros_704ILR);
        }

        // Rango de fechas admitido: sin alguna de las dos, o la inicial no posterior a
        // la final (se comparan dias, como el filtro).
        public static bool RangoValido_704ILR(System.DateTime? inicial_704ILR, System.DateTime? final_704ILR) =>
            !inicial_704ILR.HasValue || !final_704ILR.HasValue || inicial_704ILR.Value.Date <= final_704ILR.Value.Date;

        // Activa una version guardada: el servicio toma sus datos (nombre, descripcion,
        // precio y estado) y la version pasa a ser la vigente (Act = 1; la que lo era
        // pasa a 0). No agrega otra version: la consigna lo pide asi ("se podra
        // actualizar los registros de cualquier producto asignando 1 en la columna
        // Act"). Las reservas ya registradas no cambian: ReservaServicio guarda el
        // precio con que se contrato cada linea.
        public static ActivacionVersionResult_704ILR Activar_704ILR(int versionId_704ILR, out BE_Servicio_C_704ILR version_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR(PermisoActivar_704ILR, "activar una version de un servicio");
            version_704ILR = versionId_704ILR > 0 ? DAL_Servicio_C_704ILR.GetById_704ILR(versionId_704ILR) : null;
            if (version_704ILR == null) return ActivacionVersionResult_704ILR.VersionInexistente_704ILR;
            if (version_704ILR.Act_704ILR) return ActivacionVersionResult_704ILR.YaVigente_704ILR;

            // El nombre es unico en el catalogo (UQ_Servicios_Nombre): si hoy lo usa otro
            // servicio, la version no se puede activar. Es un conflicto con una regla:
            // se asienta (RNF-12).
            if (DAL_Servicio_704ILR.ExistsNombre_704ILR(version_704ILR.Nombre_704ILR, version_704ILR.ServicioId_704ILR))
            {
                AsentarRechazo_704ILR(version_704ILR, "otro servicio ya usa ese nombre");
                return ActivacionVersionResult_704ILR.NombreDuplicado_704ILR;
            }

            BE_Servicio_C_704ILR activada_704ILR;
            bool escrita_704ILR;
            try
            {
                activada_704ILR = DAL_Servicio_C_704ILR.Activar_704ILR(versionId_704ILR, BLL_Bitacora_704ILR.UsuarioActual_704ILR(), out escrita_704ILR);
            }
            catch (SqlException ex_704ILR) when (BLL_Servicio_704ILR.EsChoqueDeUnicidad_704ILR(ex_704ILR))
            {
                // Otra sesion guardo al mismo tiempo un servicio con ese nombre.
                AsentarRechazo_704ILR(version_704ILR, "otra sesion guardo al mismo tiempo un servicio con ese nombre");
                return ActivacionVersionResult_704ILR.NombreDuplicado_704ILR;
            }
            // Entre la lectura y la activacion otro puesto pudo activarla (o la base
            // perdio la fila): se informa lo que encontro la transaccion.
            if (activada_704ILR == null) return ActivacionVersionResult_704ILR.VersionInexistente_704ILR;
            version_704ILR = activada_704ILR;
            if (!escrita_704ILR) return ActivacionVersionResult_704ILR.YaVigente_704ILR;

            BLL_Bitacora_704ILR.Registrar_704ILR("Servicios", "Version de servicio activada", CriticidadBitacora_704ILR.Info,
                string.Format(CulturaBitacora_704ILR, "Servicio #{0}: version #{1} del {2:yyyy-MM-dd} {3:hh\\:mm\\:ss} activada (nombre '{4}', precio {5:0.00}, {6})",
                    activada_704ILR.ServicioId_704ILR, activada_704ILR.Id_704ILR, activada_704ILR.Fecha_704ILR, activada_704ILR.Hora_704ILR,
                    activada_704ILR.Nombre_704ILR, activada_704ILR.Precio_704ILR, activada_704ILR.Activo_704ILR ? "activo" : "dado de baja"));
            return ActivacionVersionResult_704ILR.Success_704ILR;
        }

        // Sin sesion abierta no se exige (es el propio sistema el que opera), igual que
        // BLL_Permisos_704ILR.Exigir_704ILR.
        private static bool PuedeConsultar_704ILR() =>
            !SessionManager_704ILR.IsSessionActive_704ILR
            || SessionManager_704ILR.GetInstance_704ILR.TienePermiso_704ILR(PermisoVer_704ILR)
            || SessionManager_704ILR.GetInstance_704ILR.TienePermiso_704ILR(PermisoActivar_704ILR);

        // Importes del detalle con el formato de los demas asientos del catalogo.
        private static CultureInfo CulturaBitacora_704ILR => CultureInfo.GetCultureInfo("es-AR");

        private static void AsentarRechazo_704ILR(BE_Servicio_C_704ILR v_704ILR, string motivo_704ILR) =>
            BLL_Bitacora_704ILR.Registrar_704ILR("Servicios", "Activacion de version rechazada", CriticidadBitacora_704ILR.Advertencia,
                $"Servicio #{v_704ILR.ServicioId_704ILR}: version #{v_704ILR.Id_704ILR} con el nombre '{v_704ILR.Nombre_704ILR}': {motivo_704ILR}");
    }
}
