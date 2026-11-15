using System;

namespace EvenTech.BE
{
    // Una version de un servicio del catalogo: una fila de la tabla copia
    // dbo.Servicios_C (bitacora de cambios, T06.B). La escriben los triggers de
    // dbo.Servicios en cada alta, modificacion o baja; la aplicacion solo la lee y
    // cambia Act al activar una version guardada.
    public class BE_Servicio_C_704ILR
    {
        public int Id_704ILR { get; set; }
        public int ServicioId_704ILR { get; set; }
        public DateTime Fecha_704ILR { get; set; }       // dia del cambio
        public TimeSpan Hora_704ILR { get; set; }        // hora del cambio (al segundo)
        public string Nombre_704ILR { get; set; }
        public string Descripcion_704ILR { get; set; }
        public decimal Precio_704ILR { get; set; }
        public bool Activo_704ILR { get; set; }          // estado del servicio en esa version (borrado logico)
        public bool Act_704ILR { get; set; }             // true = version vigente (una sola por servicio)
        public string Operacion_704ILR { get; set; }     // INICIAL, ALTA, MODIFICACION o BAJA (dato de la base)
        public string Usuario_704ILR { get; set; }

        // Act como la muestra la consigna de la bitacora: 1 la vigente, 0 las demas.
        public int ActNumero_704ILR => Act_704ILR ? 1 : 0;
    }

    // Filtros opcionales de la consulta de la bitacora de cambios. Lo que queda en
    // null o vacio no filtra.
    public class FiltrosServicio_C_704ILR
    {
        public int? ServicioId_704ILR { get; set; }      // codigo del servicio
        public string Nombre_704ILR { get; set; }        // nombre de la version (contenido, o exacto)
        public bool NombreExacto_704ILR { get; set; }    // true = el nombre elegido de la lista, entero
        public DateTime? FechaInicial_704ILR { get; set; }
        public DateTime? FechaFinal_704ILR { get; set; }
    }
}
