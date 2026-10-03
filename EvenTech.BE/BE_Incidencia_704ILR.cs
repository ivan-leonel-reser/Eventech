using System;

namespace EvenTech.BE
{
    // Clase de desvio que se anota durante el evento (PN2: Tipo_Incidencia). Los
    // nombres viajan a la base como dato (Incidencias.Tipo).
    public enum TipoIncidencia_704ILR
    {
        PERSONAL,       // ausencia o demora de un integrante del equipo
        SERVICIO,       // falla de un servicio contratado
        EQUIPAMIENTO,   // rotura o falla tecnica
        HORARIO,        // desvio respecto del cronograma
        INVITADOS,      // situacion con los invitados
        OTRO
    }

    // Estado de resolucion de una incidencia (PN2: Estado_Resolucion). Los nombres
    // viajan a la base como dato (Incidencias.Estado).
    public enum EstadoIncidencia_704ILR
    {
        ABIERTA,
        RESUELTA
    }

    // Lo que se sale del plan durante la ejecucion del evento (CUN011). La
    // registra el supervisor de operaciones; puede nombrar al integrante del
    // equipo que la informo.
    public class BE_Incidencia_704ILR
    {
        public int Id_704ILR { get; set; }
        public int ReservaId_704ILR { get; set; }
        public DateTime FechaHora_704ILR { get; set; }
        public TipoIncidencia_704ILR Tipo_704ILR { get; set; } = TipoIncidencia_704ILR.OTRO;
        public string Descripcion_704ILR { get; set; }
        public int? EmpleadoReportaId_704ILR { get; set; }
        public string EmpleadoReportaNombre_704ILR { get; set; }   // proyectado en lecturas (JOIN)
        public EstadoIncidencia_704ILR Estado_704ILR { get; set; }
        public string Resolucion_704ILR { get; set; }
        public DateTime? FechaResolucion_704ILR { get; set; }
    }
}
