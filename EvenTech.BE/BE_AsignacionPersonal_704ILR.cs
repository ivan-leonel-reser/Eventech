using System;

namespace EvenTech.BE
{
    // Respuesta del empleado a una asignacion (PN2: Estado_Confirmacion). Los
    // nombres viajan a la base como dato (AsignacionesPersonal.Estado).
    public enum EstadoAsignacion_704ILR
    {
        PENDIENTE,    // asignado por el coordinador, todavia sin respuesta
        CONFIRMADA,   // el empleado acepto el turno
        RECHAZADA     // el empleado lo rechazo dejando el motivo
    }

    // Asignacion de un empleado a un evento (CUN006) con su franja de trabajo y
    // la respuesta que dio (CUN007). Hay una sola por empleado y por reserva.
    public class BE_AsignacionPersonal_704ILR
    {
        public int Id_704ILR { get; set; }
        public int ReservaId_704ILR { get; set; }
        public int EmpleadoId_704ILR { get; set; }
        public string EmpleadoNombre_704ILR { get; set; }       // proyectado en lecturas (JOIN)
        public string EspecialidadNombre_704ILR { get; set; }   // proyectado en lecturas (JOIN)
        public string RolAsignado_704ILR { get; set; }
        public TimeSpan HoraInicio_704ILR { get; set; }
        public TimeSpan HoraFin_704ILR { get; set; }
        public EstadoAsignacion_704ILR Estado_704ILR { get; set; }
        public DateTime? FechaConfirmacion_704ILR { get; set; }  // cuando respondio (acepto o rechazo)
        public string MotivoRechazo_704ILR { get; set; }
        public DateTime CreatedAt_704ILR { get; set; }

        // Datos del evento, proyectados en lecturas (JOIN a la reserva): la agenda
        // del empleado los muestra y el control de superposicion los necesita.
        public DateTime FechaEvento_704ILR { get; set; }
        public string SalonNombre_704ILR { get; set; }
        public string ClienteNombre_704ILR { get; set; }
        public EstadoCoordinacion_704ILR EstadoCoordinacion_704ILR { get; set; }

        // La franja como intervalo real: empieza el dia del evento y, si la hora de
        // fin no es posterior a la de inicio, termina al dia siguiente (un turno de
        // 21:00 a 03:00 cruza la medianoche).
        public DateTime Desde_704ILR => FechaEvento_704ILR.Date + HoraInicio_704ILR;
        public DateTime Hasta_704ILR => FechaEvento_704ILR.Date + HoraFin_704ILR + (HoraFin_704ILR <= HoraInicio_704ILR ? TimeSpan.FromDays(1) : TimeSpan.Zero);

        // Duracion de la franja (siempre mayor que cero y menor que un dia).
        public TimeSpan Duracion_704ILR => Hasta_704ILR - Desde_704ILR;

        // True si las dos franjas comparten al menos un minuto.
        public bool SeSuperponeCon_704ILR(BE_AsignacionPersonal_704ILR otra_704ILR) =>
            otra_704ILR != null && Desde_704ILR < otra_704ILR.Hasta_704ILR && otra_704ILR.Desde_704ILR < Hasta_704ILR;
    }
}
