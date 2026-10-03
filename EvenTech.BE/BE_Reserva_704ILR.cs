using System;
using System.Collections.Generic;
using System.Globalization;

namespace EvenTech.BE
{
    public enum EstadoReserva_704ILR
    {
        COTIZACION,   // presupuesto: no compromete el salon (puede haber varios por fecha)
        PENDIENTE,    // reserva tentativa (p.ej. esperando senia): tampoco bloquea el salon
        CONFIRMADA,   // reserva firme: bloquea el salon para esa fecha (anti-solapamiento)
        CANCELADA
    }

    // Entidad central del dominio: reserva de un evento sobre un salon.
    // Es la entidad de negocio sensible elegida para control de cambios y
    // digitos verificadores (su monto/fecha/estado no deben alterarse por fuera
    // del sistema). El campo Dvh guarda el digito verificador horizontal.
    public class BE_Reserva_704ILR : IVerificable_704ILR
    {
        public int Id_704ILR { get; set; }
        public int ClienteId_704ILR { get; set; }
        public string ClienteNombre_704ILR { get; set; }  // proyectado en lecturas (JOIN), no se persiste aca
        public int SalonId_704ILR { get; set; }
        public string SalonNombre_704ILR { get; set; }    // proyectado en lecturas (JOIN), no se persiste aca
        public DateTime FechaEvento_704ILR { get; set; }
        public EstadoReserva_704ILR Estado_704ILR { get; set; }
        public decimal Monto_704ILR { get; set; }

        // Cantidad de invitados estimada (PN1: "Cantidad_Invitados"). Es el dato
        // con el que se consulta la disponibilidad y el que sostiene la RN-06: al
        // confirmar, el salon elegido tiene que poder alojarlos. Igual que VenceEl
        // NO forma parte de ObtenerCamposParaDV: sumarlo invalidaria los digitos
        // verificadores ya calculados sobre las reservas existentes.
        public int CantidadInvitados_704ILR { get; set; }

        public DateTime CreatedAt_704ILR { get; set; }
        public string Dvh_704ILR { get; set; }            // digito verificador horizontal

        // Vencimiento de la operacion (RN-01). Solo aplica a COTIZACION y PENDIENTE;
        // en CONFIRMADA y CANCELADA queda en null. Es un dato administrativo y por eso
        // NO forma parte de ObtenerCamposParaDV: agregarlo invalidaria los digitos
        // verificadores ya calculados sobre las reservas existentes.
        public DateTime? VenceEl_704ILR { get; set; }

        // Estado de coordinacion del evento (Proceso 2): segundo eje, independiente del
        // estado comercial. Lo mantiene BLL_Coordinacion; las escrituras de la reserva
        // no lo tocan. Como VenceEl, NO forma parte de ObtenerCamposParaDV: es un dato
        // operativo y sumarlo invalidaria los digitos verificadores ya calculados.
        public EstadoCoordinacion_704ILR EstadoCoordinacion_704ILR { get; set; }

        // True si la operacion tiene plazo y ya expiro.
        public bool EstaVencida_704ILR =>
            VenceEl_704ILR.HasValue && VenceEl_704ILR.Value < DateTime.Now;

        // --- Patron Memento (rol Originator) ---

        // Crea la foto del estado actual. Los servicios se reciben de afuera
        // porque la entidad no accede a la DAL (los carga el Caretaker).
        public BE_ReservaMemento_704ILR CrearMemento_704ILR(string usuario_704ILR, IReadOnlyList<BE_ReservaServicio_704ILR> servicios_704ILR) =>
            new BE_ReservaMemento_704ILR(0, Id_704ILR, ClienteId_704ILR, SalonId_704ILR, FechaEvento_704ILR, Estado_704ILR, Monto_704ILR,
                CantidadInvitados_704ILR, usuario_704ILR, DateTime.Now, ClienteNombre_704ILR, SalonNombre_704ILR, servicios_704ILR);

        // Repone el estado de negocio guardado en el memento. Solo el Originator
        // conoce que campos componen su estado interno.
        public void RestaurarDesde_704ILR(BE_ReservaMemento_704ILR memento_704ILR)
        {
            if (memento_704ILR == null) return;
            ClienteId_704ILR = memento_704ILR.ClienteId_704ILR;
            SalonId_704ILR = memento_704ILR.SalonId_704ILR;
            FechaEvento_704ILR = memento_704ILR.FechaEvento_704ILR;
            Estado_704ILR = memento_704ILR.Estado_704ILR;
            Monto_704ILR = memento_704ILR.Monto_704ILR;
            CantidadInvitados_704ILR = memento_704ILR.CantidadInvitados_704ILR;
        }

        // Atributos de negocio que entran en el DV, en orden estable. La cultura
        // invariante evita que el formato de fecha/monto cambie el DV entre equipos.
        public string[] ObtenerCamposParaDV_704ILR() => new[]
        {
            ClienteId_704ILR.ToString(CultureInfo.InvariantCulture),
            SalonId_704ILR.ToString(CultureInfo.InvariantCulture),
            FechaEvento_704ILR.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Estado_704ILR.ToString(),
            Monto_704ILR.ToString("0.00", CultureInfo.InvariantCulture)
        };
    }
}
