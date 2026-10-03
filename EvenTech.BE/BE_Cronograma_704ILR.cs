using System;
using System.Collections.Generic;

namespace EvenTech.BE
{
    // Cronograma de la jornada de un evento (CUN008). Hay uno solo por reserva;
    // sus actividades dicen que pasa, a que hora y quien responde por cada tramo.
    public class BE_Cronograma_704ILR
    {
        public int Id_704ILR { get; set; }
        public int ReservaId_704ILR { get; set; }
        public DateTime CreatedAt_704ILR { get; set; }
        public List<BE_CronogramaActividad_704ILR> Actividades_704ILR { get; set; } = new List<BE_CronogramaActividad_704ILR>();
    }

    // Tramo del cronograma (PN2: Hora_Actividad, Descripcion_Actividad,
    // Personal_Responsable, Duracion_Estimada). El orden es el que arma el
    // coordinador: una jornada que cruza la medianoche no se puede ordenar por hora.
    public class BE_CronogramaActividad_704ILR
    {
        public int Id_704ILR { get; set; }
        public int CronogramaId_704ILR { get; set; }
        public int Orden_704ILR { get; set; }
        public TimeSpan Hora_704ILR { get; set; }
        public string Descripcion_704ILR { get; set; }
        public int ResponsableId_704ILR { get; set; }
        public string ResponsableNombre_704ILR { get; set; }   // proyectado en lecturas (JOIN)
        public int DuracionMinutos_704ILR { get; set; }
    }

    // Prioridad de una tarea. Los nombres viajan a la base como dato (Tareas.Prioridad).
    public enum PrioridadTarea_704ILR
    {
        ALTA,
        MEDIA,
        BAJA
    }

    // Tarea especifica de un integrante del equipo dentro del cronograma (CUN009).
    public class BE_Tarea_704ILR
    {
        public int Id_704ILR { get; set; }
        public int CronogramaId_704ILR { get; set; }
        public int ReservaId_704ILR { get; set; }              // proyectado en lecturas (JOIN al cronograma)
        public int EmpleadoId_704ILR { get; set; }
        public string EmpleadoNombre_704ILR { get; set; }      // proyectado en lecturas (JOIN)
        public string Descripcion_704ILR { get; set; }
        public TimeSpan HoraInicio_704ILR { get; set; }
        public TimeSpan HoraFin_704ILR { get; set; }
        public PrioridadTarea_704ILR Prioridad_704ILR { get; set; } = PrioridadTarea_704ILR.MEDIA;
        public string Recursos_704ILR { get; set; }
        public DateTime CreatedAt_704ILR { get; set; }
    }
}
