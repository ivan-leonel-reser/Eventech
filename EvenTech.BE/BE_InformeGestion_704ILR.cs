using System;
using System.Collections.Generic;

namespace EvenTech.BE
{
    // Resultado de generar el informe de gestion (A02).
    public enum InformeResult_704ILR
    {
        Success_704ILR,
        RangoInvalido_704ILR,    // "desde" posterior a "hasta"
        RangoExcesivo_704ILR     // mas de un anio entre las dos fechas
    }

    // Una reserva del periodo, como la lee el informe: datos de la cabecera, cliente,
    // salon y lo cobrado hasta el momento (suma de sus pagos).
    public class BE_InformeFila_704ILR
    {
        public int ReservaId_704ILR { get; set; }
        public DateTime FechaEvento_704ILR { get; set; }
        public EstadoReserva_704ILR Estado_704ILR { get; set; }
        public EstadoCoordinacion_704ILR EstadoCoordinacion_704ILR { get; set; }
        public decimal Monto_704ILR { get; set; }
        public decimal Cobrado_704ILR { get; set; }
        public DateTime? VenceEl_704ILR { get; set; }
        public int Invitados_704ILR { get; set; }
        public string ClienteNombre_704ILR { get; set; }
        public int SalonId_704ILR { get; set; }
        public string SalonNombre_704ILR { get; set; }

        public decimal Saldo_704ILR => Monto_704ILR - Cobrado_704ILR;
    }

    // Reservas del periodo en un estado.
    public class BE_InformeEstado_704ILR
    {
        public EstadoReserva_704ILR Estado_704ILR { get; set; }
        public int Cantidad_704ILR { get; set; }
        public decimal Monto_704ILR { get; set; }
    }

    // Ocupacion de un salon en el periodo: dias con un evento confirmado sobre los dias
    // del periodo (la RN-03 admite un solo evento confirmado por salon y por dia).
    public class BE_InformeSalon_704ILR
    {
        public string SalonNombre_704ILR { get; set; }
        public int Capacidad_704ILR { get; set; }
        public int Eventos_704ILR { get; set; }
        public decimal PorcentajeOcupacion_704ILR { get; set; }
        public decimal Contratado_704ILR { get; set; }
    }

    // Informe de gestion del periodo (A02). Lo calcula entero la capa de negocio; la
    // pantalla lo muestra y la exportacion a PDF lo dibuja tal cual.
    public class BE_InformeGestion_704ILR
    {
        public DateTime Desde_704ILR { get; set; }
        public DateTime Hasta_704ILR { get; set; }
        public DateTime GeneradoEl_704ILR { get; set; }
        public string GeneradoPor_704ILR { get; set; }
        public int DiasPeriodo_704ILR { get; set; }

        // Resumen: todas las reservas cuyo evento cae en el periodo, por estado.
        public int TotalReservas_704ILR { get; set; }
        public List<BE_InformeEstado_704ILR> PorEstado_704ILR { get; set; } = new List<BE_InformeEstado_704ILR>();

        // Cobranza de las reservas CONFIRMADAS del periodo.
        public decimal Contratado_704ILR { get; set; }
        public decimal Cobrado_704ILR { get; set; }
        public decimal Saldo_704ILR { get; set; }
        public decimal PorcentajeCobrado_704ILR { get; set; }

        public List<BE_InformeSalon_704ILR> Salones_704ILR { get; set; } = new List<BE_InformeSalon_704ILR>();

        // Cotizaciones y reservas pendientes del periodo todavia vigentes, de la que
        // vence antes a la que vence despues (RN-01), y cuantas ya vencieron.
        public List<BE_InformeFila_704ILR> PorVencer_704ILR { get; set; } = new List<BE_InformeFila_704ILR>();
        public int Vencidas_704ILR { get; set; }

        // Eventos confirmados del periodo, con su estado de coordinacion y su saldo.
        public List<BE_InformeFila_704ILR> Eventos_704ILR { get; set; } = new List<BE_InformeFila_704ILR>();
    }
}
