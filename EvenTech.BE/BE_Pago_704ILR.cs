using System;
using System.Globalization;

namespace EvenTech.BE
{
    // Pago registrado contra una reserva (Proceso 1, paso 5: cobro de adelanto/saldo).
    // Se protege con digito verificador, como la reserva: el horizontal vive en la
    // propia fila (Dvh) y el vertical de la tabla, en DVVertical.
    public class BE_Pago_704ILR : IVerificable_704ILR
    {
        public int Id_704ILR { get; set; }
        public int ReservaId_704ILR { get; set; }
        public int MetodoPagoId_704ILR { get; set; }
        public string MetodoNombre_704ILR { get; set; }   // display (JOIN MetodosPago)
        public decimal Monto_704ILR { get; set; }
        public DateTime Fecha_704ILR { get; set; }
        public string Observacion_704ILR { get; set; }
        public string Dvh_704ILR { get; set; }            // digito verificador horizontal

        // --- Digito verificador ---

        // Atributos del pago que entran al DV, en orden fijo: a que reserva
        // corresponde, con que medio se cobro, cuanto, cuando y la observacion (ahi se
        // consigna la referencia bancaria del cobro). La fecha va al segundo, que es lo
        // que la pantalla y el comprobante muestran. El nombre del medio de pago no
        // entra: no es un dato de la fila, lo proyecta la lectura.
        public string[] ObtenerCamposParaDV_704ILR() => new[]
        {
            ReservaId_704ILR.ToString(CultureInfo.InvariantCulture),
            MetodoPagoId_704ILR.ToString(CultureInfo.InvariantCulture),
            Monto_704ILR.ToString("0.00", CultureInfo.InvariantCulture),
            Fecha_704ILR.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Observacion_704ILR ?? string.Empty
        };
    }
}
