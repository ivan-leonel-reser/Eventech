using System;

namespace EvenTech.BE
{
    // Estado de coordinacion del evento (Proceso 2). Es un segundo eje,
    // independiente del estado comercial de la reserva: una reserva puede estar
    // CONFIRMADA meses antes del evento y todavia no estar lista para ejecutarse.
    // Los nombres viajan a la base como dato (Reservas.EstadoCoordinacion).
    public enum EstadoCoordinacion_704ILR
    {
        SIN_ASIGNAR,       // confirmada, todavia sin personal asignado
        EN_COORDINACION,   // hay personal asignado y falta que confirme o falta el cronograma
        LISTO,             // todo el personal confirmo y el cronograma esta generado
        EN_EJECUCION,      // el evento se esta desarrollando: se registran incidencias
        CERRADO            // el evento finalizo y las incidencias quedaron resueltas
    }

    // Renglon de la consulta de eventos a coordinar: la reserva confirmada, lo que
    // se contrato a grandes rasgos y cuanto se avanzo en su coordinacion. Es una
    // proyeccion de lectura para la pantalla de operaciones, no una entidad que
    // se persista.
    public class BE_EventoCoordinacion_704ILR
    {
        public int ReservaId_704ILR { get; set; }
        public string ClienteNombre_704ILR { get; set; }
        public string SalonNombre_704ILR { get; set; }
        public DateTime FechaEvento_704ILR { get; set; }
        public int CantidadInvitados_704ILR { get; set; }
        // Estado comercial de la reserva: solo una CONFIRMADA se coordina (RN-08).
        public EstadoReserva_704ILR Estado_704ILR { get; set; }
        public EstadoCoordinacion_704ILR EstadoCoordinacion_704ILR { get; set; }

        public int Asignados_704ILR { get; set; }             // asignaciones en cualquier estado
        public int Confirmados_704ILR { get; set; }
        public int Pendientes_704ILR { get; set; }
        public int Rechazados_704ILR { get; set; }
        public int Actividades_704ILR { get; set; }           // 0 = sin cronograma
        public bool TieneCronograma_704ILR { get; set; }
        public int Tareas_704ILR { get; set; }
        public int IncidenciasAbiertas_704ILR { get; set; }
        public int Incidencias_704ILR { get; set; }

        // "2/3": confirmados sobre asignados, como lo lee el coordinador.
        public string Personal_704ILR => Confirmados_704ILR + "/" + Asignados_704ILR;
    }
}
