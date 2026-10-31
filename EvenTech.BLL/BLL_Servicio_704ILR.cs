using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum ServicioResult_704ILR
    {
        Success_704ILR,
        NombreInvalido_704ILR,
        NombreDuplicado_704ILR,
        PrecioInvalido_704ILR,
        NotFound_704ILR,
        PrecioExcedido_704ILR       // el precio no entra en dbo.Servicios.Precio (DECIMAL(12,2))
    }

    // Reglas de negocio del catalogo de servicios (Proceso 1).
    public static class BLL_Servicio_704ILR
    {
        public static List<BE_Servicio_704ILR> GetAll_704ILR() => DAL_Servicio_704ILR.GetAll_704ILR();

        public static List<BE_Servicio_704ILR> GetActivos_704ILR() => DAL_Servicio_704ILR.GetActivos_704ILR();

        public static ServicioResult_704ILR Crear_704ILR(BE_Servicio_704ILR s_704ILR, out int nuevoId_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR("SERVICIOS_GESTION", "crear un servicio");
            nuevoId_704ILR = 0;
            var v_704ILR = Validar_704ILR(s_704ILR, 0);
            if (v_704ILR != ServicioResult_704ILR.Success_704ILR)
            {
                if (v_704ILR == ServicioResult_704ILR.PrecioExcedido_704ILR)
                    AsentarRechazo_704ILR("Alta de servicio rechazada",
                        $"Servicio '{s_704ILR.Nombre_704ILR}': {DetallePrecioExcedido_704ILR(s_704ILR)}");
                return v_704ILR;
            }

            // Se guarda el precio que admite la columna, redondeado a centavos (el mismo
            // valor que dejaba el motor), igual que en la modificacion.
            s_704ILR.Precio_704ILR = PrecioComoSeGuarda_704ILR(s_704ILR.Precio_704ILR);
            try
            {
                nuevoId_704ILR = DAL_Servicio_704ILR.Insert_704ILR(s_704ILR);
            }
            catch (SqlException ex_704ILR) when (EsChoqueDeUnicidad_704ILR(ex_704ILR))
            {
                nuevoId_704ILR = 0;
                AsentarRechazo_704ILR("Alta de servicio rechazada",
                    $"Servicio '{s_704ILR.Nombre_704ILR}': otra sesion guardo al mismo tiempo un servicio con ese nombre");
                return ServicioResult_704ILR.NombreDuplicado_704ILR;
            }
            BLL_Bitacora_704ILR.Registrar_704ILR("Servicios", "Alta de servicio", CriticidadBitacora_704ILR.Info,
                $"Servicio '{s_704ILR.Nombre_704ILR}' creado (#{nuevoId_704ILR})");
            return ServicioResult_704ILR.Success_704ILR;
        }

        public static ServicioResult_704ILR Actualizar_704ILR(BE_Servicio_704ILR s_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR("SERVICIOS_GESTION", "modificar un servicio");
            BE_Servicio_704ILR guardado_704ILR = s_704ILR == null || s_704ILR.Id_704ILR <= 0 ? null : DAL_Servicio_704ILR.GetById_704ILR(s_704ILR.Id_704ILR);
            if (guardado_704ILR == null) return ServicioResult_704ILR.NotFound_704ILR;
            var v_704ILR = Validar_704ILR(s_704ILR, s_704ILR.Id_704ILR);
            if (v_704ILR != ServicioResult_704ILR.Success_704ILR)
            {
                if (v_704ILR == ServicioResult_704ILR.PrecioExcedido_704ILR)
                    AsentarRechazo_704ILR("Modificacion de servicio rechazada",
                        $"Servicio #{s_704ILR.Id_704ILR}: {DetallePrecioExcedido_704ILR(s_704ILR)}");
                return v_704ILR;
            }

            // Guardar sin cambiar nada no es una modificacion: no se escribe ni se asienta
            // (mismo criterio que la modificacion de reservas). Pasaba al pulsar Guardar dos
            // veces seguidas: la bitacora registraba una modificacion que no habia ocurrido.
            // El precio se compara y se guarda como lo deja la columna, a centavos: con
            // 120000,004 sobre un precio de 120000,00 el motor guardaba el mismo valor y la
            // bitacora asentaba igual la modificacion.
            s_704ILR.Precio_704ILR = PrecioComoSeGuarda_704ILR(s_704ILR.Precio_704ILR);
            if (MismosDatos_704ILR(guardado_704ILR, s_704ILR)) return ServicioResult_704ILR.Success_704ILR;

            try
            {
                DAL_Servicio_704ILR.Update_704ILR(s_704ILR);
            }
            catch (SqlException ex_704ILR) when (EsChoqueDeUnicidad_704ILR(ex_704ILR))
            {
                AsentarRechazo_704ILR("Modificacion de servicio rechazada",
                    $"Servicio #{s_704ILR.Id_704ILR}: otra sesion guardo al mismo tiempo un servicio con el nombre '{s_704ILR.Nombre_704ILR}'");
                return ServicioResult_704ILR.NombreDuplicado_704ILR;
            }
            BLL_Bitacora_704ILR.Registrar_704ILR("Servicios", "Modificacion de servicio", CriticidadBitacora_704ILR.Info,
                $"Servicio #{s_704ILR.Id_704ILR} actualizado");
            return ServicioResult_704ILR.Success_704ILR;
        }

        // Anchos de dbo.Servicios. La capa de datos manda los parametros con ese
        // tamano fijo, de modo que un texto mas largo se guardaria recortado sin aviso:
        // la regla se hace explicita aca (nombre demasiado largo = nombre invalido) y
        // la descripcion —dato accesorio— se recorta a lo que entra.
        private const int MaxNombre_704ILR = 80;
        private const int MaxDescripcion_704ILR = 250;

        // Precio DECIMAL(12,2): hasta 9.999.999.999,99. Un precio mayor no entra en la
        // columna y el motor lo rechazaba con un desborde aritmetico que llegaba a la
        // pantalla sin manejar. Se compara el valor redondeado a centavos, que es lo que
        // guardaria el motor (9.999.999.999,994 se guarda como ,99; ,995 ya no entra).
        // Publico: la ficha del catalogo escribe este tope en el mensaje del rechazo.
        public const decimal MaxPrecio_704ILR = 9999999999.99m;

        // El precio tal como lo guarda la columna DECIMAL(12,2): redondeado a centavos con
        // el redondeo comercial (mitad hacia arriba), el mismo que aplica el motor y el de
        // los cobros. Las reglas del tope y la comparacion con lo guardado usan este valor.
        private static decimal PrecioComoSeGuarda_704ILR(decimal precio_704ILR) =>
            decimal.Round(precio_704ILR, 2, MidpointRounding.AwayFromZero);

        // Los importes del detalle de la bitacora se escriben siempre con el formato
        // es-AR ("1500,50"), el de los asientos que ya trae la base de demostracion. Con
        // la cultura de la estacion, el mismo rechazo quedaba asentado con coma desde una
        // PC es-AR y con punto desde una en-US, es-MX o es-419. Se resuelve al asentar
        // (el marco la guarda en cache), no al inicializar la clase.
        private static CultureInfo CulturaBitacora_704ILR => CultureInfo.GetCultureInfo("es-AR");

        private static ServicioResult_704ILR Validar_704ILR(BE_Servicio_704ILR s_704ILR, int idActual_704ILR)
        {
            // Nombre obligatorio: un nombre que no se ve cuenta como vacio, con el mismo criterio
            // que Clientes, Perfiles e Idiomas. Ademas de los espacios, TextoEnBlanco descarta los
            // caracteres de formato invisibles (espacio de ancho cero, guion blando, marca de orden
            // de bytes), los rellenos que se dibujan vacios y los no caracteres: con
            // IsNullOrWhiteSpace se guardaba un servicio sin nombre visible, con el cartel
            // "Servicio guardado." y una fila en blanco en la grilla.
            if (s_704ILR == null || GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(s_704ILR.Nombre_704ILR))
                return ServicioResult_704ILR.NombreInvalido_704ILR;
            // El nombre se mide, se compara con los demas y se guarda tal como se ve, como en
            // Idiomas y Perfiles: sin los caracteres que no ocupan lugar, con cada tramo de espacios
            // o rellenos como un espacio comun (ninguno en los bordes) y en forma compuesta. Antes
            // solo se recortaban los bordes y el caracter invisible pegado quedaba en la base.
            s_704ILR.Nombre_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(s_704ILR.Nombre_704ILR);
            if (GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(s_704ILR.Nombre_704ILR) || s_704ILR.Nombre_704ILR.Length > MaxNombre_704ILR)
                return ServicioResult_704ILR.NombreInvalido_704ILR;
            if (s_704ILR.Descripcion_704ILR != null && s_704ILR.Descripcion_704ILR.Trim().Length > MaxDescripcion_704ILR)
                s_704ILR.Descripcion_704ILR = GestorDeIdioma_704ILR.Recortar_704ILR(s_704ILR.Descripcion_704ILR.Trim(), MaxDescripcion_704ILR);
            if (s_704ILR.Precio_704ILR < 0)
                return ServicioResult_704ILR.PrecioInvalido_704ILR;
            if (PrecioComoSeGuarda_704ILR(s_704ILR.Precio_704ILR) > MaxPrecio_704ILR)
                return ServicioResult_704ILR.PrecioExcedido_704ILR;
            if (DAL_Servicio_704ILR.ExistsNombre_704ILR(s_704ILR.Nombre_704ILR, idActual_704ILR))
                return ServicioResult_704ILR.NombreDuplicado_704ILR;
            return ServicioResult_704ILR.Success_704ILR;
        }

        private static string DetallePrecioExcedido_704ILR(BE_Servicio_704ILR s_704ILR) =>
            string.Format(CulturaBitacora_704ILR, "precio {0:0.00} fuera de rango (maximo {1:0.00})", s_704ILR.Precio_704ILR, MaxPrecio_704ILR);

        // Compara lo guardado con lo que se guardaria, normalizado como lo escribe la
        // capa de datos: el nombre como se ve (el nuevo ya llega asi de Validar; uno guardado
        // antes con invisibles o espacios de mas vale lo mismo si se ve igual, y Guardar sin
        // tocar nada no escribe ni asienta), descripcion vacia = NULL y sin espacios de mas.
        private static bool MismosDatos_704ILR(BE_Servicio_704ILR guardado_704ILR, BE_Servicio_704ILR nuevo_704ILR) =>
            GestorDeIdioma_704ILR.TextoVisible_704ILR(guardado_704ILR.Nombre_704ILR) == (nuevo_704ILR.Nombre_704ILR ?? string.Empty)
            && NormalizarDescripcion_704ILR(guardado_704ILR.Descripcion_704ILR) == NormalizarDescripcion_704ILR(nuevo_704ILR.Descripcion_704ILR)
            && guardado_704ILR.Precio_704ILR == nuevo_704ILR.Precio_704ILR
            && guardado_704ILR.Activo_704ILR == nuevo_704ILR.Activo_704ILR;

        private static string NormalizarDescripcion_704ILR(string descripcion_704ILR) =>
            string.IsNullOrWhiteSpace(descripcion_704ILR) ? null : descripcion_704ILR.Trim();

        // UQ_Servicios_Nombre es la red de seguridad cuando dos sesiones pasan a la vez la
        // validacion previa con el mismo nombre: el motor rechaza la segunda escritura y
        // aca se informa como nombre duplicado en lugar de propagar la excepcion del motor.
        // 2601 y 2627 son los errores de indice unico y de restriccion unica.
        private static bool EsChoqueDeUnicidad_704ILR(SqlException ex_704ILR)
            => ex_704ILR.Number == 2601 || ex_704ILR.Number == 2627;

        private static void AsentarRechazo_704ILR(string accion_704ILR, string detalle_704ILR)
            => BLL_Bitacora_704ILR.Registrar_704ILR("Servicios", accion_704ILR, CriticidadBitacora_704ILR.Advertencia, detalle_704ILR);
    }
}
