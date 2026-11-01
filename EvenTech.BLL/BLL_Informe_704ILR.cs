using System;
using System.Collections.Generic;
using System.Linq;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Informe de gestion del periodo (A02). Consolida, sin modificar nada, las reservas
    // cuyo evento cae en el periodo elegido:
    //   - resumen por estado (cantidad y monto);
    //   - cobranza de las CONFIRMADAS: contratado, cobrado, saldo y porcentaje cobrado;
    //   - ocupacion de cada salon: dias con un evento confirmado sobre los dias del periodo;
    //   - operaciones por vencer (RN-01): cotizaciones y pendientes vigentes, de la que
    //     vence antes a la que vence despues, y cuantas ya vencieron;
    //   - eventos confirmados con su estado de coordinacion y su saldo.
    // Todo el calculo vive aca: la pantalla lo muestra y la exportacion a PDF lo dibuja
    // tal cual, sin recalcular nada.
    public static class BLL_Informe_704ILR
    {
        public const string Permiso_704ILR = "INFORMES_VER";
        public const int MaxDias_704ILR = 366;

        public static InformeResult_704ILR Generar_704ILR(DateTime desde_704ILR, DateTime hasta_704ILR, out BE_InformeGestion_704ILR informe_704ILR)
        {
            informe_704ILR = null;
            // El informe reune importes de toda la operacion: la capa de negocio exige el
            // permiso tambien para generarlo, no solo para escribir.
            BLL_Permisos_704ILR.Exigir_704ILR(Permiso_704ILR, "generar el informe de gestion");

            DateTime d_704ILR = desde_704ILR.Date, h_704ILR = hasta_704ILR.Date;
            if (d_704ILR > h_704ILR) return InformeResult_704ILR.RangoInvalido_704ILR;
            int dias_704ILR = (int)(h_704ILR - d_704ILR).TotalDays + 1;
            if (dias_704ILR > MaxDias_704ILR) return InformeResult_704ILR.RangoExcesivo_704ILR;

            List<BE_InformeFila_704ILR> filas_704ILR = DAL_Informe_704ILR.ReservasDelPeriodo_704ILR(d_704ILR, h_704ILR);
            List<BE_Salon_704ILR> salones_704ILR = DAL_Salon_704ILR.GetAll_704ILR();
            informe_704ILR = Calcular_704ILR(d_704ILR, h_704ILR, filas_704ILR, salones_704ILR, DateTime.Now, UsuarioSesion_704ILR());

            BLL_Bitacora_704ILR.Registrar_704ILR("Informes", "Informe generado", CriticidadBitacora_704ILR.Info,
                $"Periodo: {Dia_704ILR(d_704ILR)} a {Dia_704ILR(h_704ILR)} | Reservas: {informe_704ILR.TotalReservas_704ILR}");
            return InformeResult_704ILR.Success_704ILR;
        }

        // El calculo, separado de la lectura para poder probarlo con filas armadas a mano.
        public static BE_InformeGestion_704ILR Calcular_704ILR(DateTime desde_704ILR, DateTime hasta_704ILR,
            IReadOnlyList<BE_InformeFila_704ILR> filas_704ILR, IReadOnlyList<BE_Salon_704ILR> salones_704ILR,
            DateTime ahora_704ILR, string usuario_704ILR)
        {
            filas_704ILR ??= Array.Empty<BE_InformeFila_704ILR>();
            salones_704ILR ??= Array.Empty<BE_Salon_704ILR>();
            int dias_704ILR = (int)(hasta_704ILR.Date - desde_704ILR.Date).TotalDays + 1;
            var inf_704ILR = new BE_InformeGestion_704ILR
            {
                Desde_704ILR = desde_704ILR.Date,
                Hasta_704ILR = hasta_704ILR.Date,
                GeneradoEl_704ILR = ahora_704ILR,
                GeneradoPor_704ILR = usuario_704ILR,
                DiasPeriodo_704ILR = dias_704ILR,
                TotalReservas_704ILR = filas_704ILR.Count,
                MontoTotal_704ILR = filas_704ILR.Sum(f_704ILR => f_704ILR.Monto_704ILR)
            };

            // Resumen por estado, en el orden del ciclo de vida (los cuatro, aunque esten en cero).
            foreach (EstadoReserva_704ILR e_704ILR in Enum.GetValues(typeof(EstadoReserva_704ILR)))
            {
                var deEstado_704ILR = filas_704ILR.Where(f_704ILR => f_704ILR.Estado_704ILR == e_704ILR).ToList();
                inf_704ILR.PorEstado_704ILR.Add(new BE_InformeEstado_704ILR
                {
                    Estado_704ILR = e_704ILR,
                    Cantidad_704ILR = deEstado_704ILR.Count,
                    Monto_704ILR = deEstado_704ILR.Sum(f_704ILR => f_704ILR.Monto_704ILR)
                });
            }

            // Cobranza: solo lo CONFIRMADO es una venta firme.
            var confirmadas_704ILR = filas_704ILR.Where(f_704ILR => f_704ILR.Estado_704ILR == EstadoReserva_704ILR.CONFIRMADA).ToList();
            inf_704ILR.Contratado_704ILR = confirmadas_704ILR.Sum(f_704ILR => f_704ILR.Monto_704ILR);
            inf_704ILR.Cobrado_704ILR = confirmadas_704ILR.Sum(f_704ILR => f_704ILR.Cobrado_704ILR);
            inf_704ILR.Saldo_704ILR = inf_704ILR.Contratado_704ILR - inf_704ILR.Cobrado_704ILR;
            inf_704ILR.PorcentajeCobrado_704ILR = Porcentaje_704ILR(inf_704ILR.Cobrado_704ILR, inf_704ILR.Contratado_704ILR);

            // Ocupacion: todos los salones del catalogo, aunque no tengan eventos.
            foreach (var s_704ILR in salones_704ILR.OrderBy(x_704ILR => x_704ILR.Nombre_704ILR, StringComparer.CurrentCulture))
            {
                var delSalon_704ILR = confirmadas_704ILR.Where(f_704ILR => f_704ILR.SalonId_704ILR == s_704ILR.Id_704ILR).ToList();
                // Dias distintos: la RN-03 ya impide dos confirmadas el mismo dia, pero el
                // porcentaje no puede pasar del 100 % aunque una fila vieja lo hiciera.
                int diasOcupados_704ILR = delSalon_704ILR.Select(f_704ILR => f_704ILR.FechaEvento_704ILR.Date).Distinct().Count();
                inf_704ILR.Salones_704ILR.Add(new BE_InformeSalon_704ILR
                {
                    SalonNombre_704ILR = s_704ILR.Nombre_704ILR,
                    Capacidad_704ILR = s_704ILR.Capacidad_704ILR,
                    Eventos_704ILR = delSalon_704ILR.Count,
                    PorcentajeOcupacion_704ILR = Porcentaje_704ILR(diasOcupados_704ILR, dias_704ILR),
                    Contratado_704ILR = delSalon_704ILR.Sum(f_704ILR => f_704ILR.Monto_704ILR)
                });
            }

            // Por vencer (RN-01): una operacion sin vencimiento no entra en esta lista.
            var abiertas_704ILR = filas_704ILR.Where(f_704ILR =>
                (f_704ILR.Estado_704ILR == EstadoReserva_704ILR.COTIZACION || f_704ILR.Estado_704ILR == EstadoReserva_704ILR.PENDIENTE) &&
                f_704ILR.VenceEl_704ILR.HasValue).ToList();
            inf_704ILR.PorVencer_704ILR = abiertas_704ILR.Where(f_704ILR => f_704ILR.VenceEl_704ILR.Value >= ahora_704ILR)
                                                         .OrderBy(f_704ILR => f_704ILR.VenceEl_704ILR.Value)
                                                         .ThenBy(f_704ILR => f_704ILR.ReservaId_704ILR).ToList();
            inf_704ILR.Vencidas_704ILR = abiertas_704ILR.Count(f_704ILR => f_704ILR.VenceEl_704ILR.Value < ahora_704ILR);

            inf_704ILR.Eventos_704ILR = confirmadas_704ILR.OrderBy(f_704ILR => f_704ILR.FechaEvento_704ILR)
                                                          .ThenBy(f_704ILR => f_704ILR.SalonNombre_704ILR, StringComparer.CurrentCulture).ToList();
            return inf_704ILR;
        }

        // Asienta la exportacion del informe (la escribe la pantalla con la biblioteca de PDF).
        public static void RegistrarExportacion_704ILR(BE_InformeGestion_704ILR informe_704ILR, string archivo_704ILR)
        {
            if (informe_704ILR == null) return;
            BLL_Bitacora_704ILR.Registrar_704ILR("Informes", "Informe exportado a PDF", CriticidadBitacora_704ILR.Info,
                $"Periodo: {Dia_704ILR(informe_704ILR.Desde_704ILR)} a {Dia_704ILR(informe_704ILR.Hasta_704ILR)} | Archivo: {System.IO.Path.GetFileName(archivo_704ILR)}");
        }

        // Porcentaje con un decimal, redondeado como los importes (mitad hacia arriba).
        private static decimal Porcentaje_704ILR(decimal parte_704ILR, decimal total_704ILR) =>
            total_704ILR <= 0 ? 0m : Math.Round(parte_704ILR * 100m / total_704ILR, 1, MidpointRounding.AwayFromZero);

        // Fecha del asiento con calendario gregoriano y separadores fijos, como el resto de la bitacora.
        private static string Dia_704ILR(DateTime d_704ILR) => d_704ILR.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        private static string UsuarioSesion_704ILR() =>
            SessionManager_704ILR.IsSessionActive_704ILR ? SessionManager_704ILR.GetInstance_704ILR.User_704ILR.Username_704ILR : "Sistema";
    }
}
