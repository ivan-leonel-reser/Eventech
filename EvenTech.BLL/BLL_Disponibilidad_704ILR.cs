using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EvenTech.BE;
using EvenTech.DAL;

namespace EvenTech.BLL
{
    // Consulta de disponibilidad (Proceso 1, paso 1): el vendedor indica fecha
    // del evento y cantidad estimada de invitados y el sistema informa que
    // salones se pueden comprometer. Si un salon no esta libre, se calcula la
    // propuesta alternativa (proxima fecha sin reserva confirmada) para poder
    // ofrecerle al cliente otra opcion de similar preferencia.
    public static class BLL_Disponibilidad_704ILR
    {
        // Hasta cuantos dias hacia adelante se busca una fecha alternativa.
        public const int HorizontePropuestasDias_704ILR = 60;

        // Ultimo dia que admiten los calendarios de la aplicacion (el tope del
        // selector de fechas de Windows). Una propuesta posterior no podria
        // cargarse en la ficha de la reserva, asi que no se ofrece.
        private static readonly DateTime FechaMaximaPropuesta_704ILR = new DateTime(9998, 12, 31);

        public static List<BE_DisponibilidadSalon_704ILR> Consultar_704ILR(DateTime fecha_704ILR, int capacidadRequerida_704ILR)
        {
            fecha_704ILR = fecha_704ILR.Date;
            if (fecha_704ILR < DateTime.Today) fecha_704ILR = DateTime.Today;
            if (capacidadRequerida_704ILR < 0) capacidadRequerida_704ILR = 0;

            // Fin del horizonte de busqueda, sin desbordar DateTime cerca de su maximo.
            DateTime hasta_704ILR = fecha_704ILR <= DateTime.MaxValue.Date.AddDays(-HorizontePropuestasDias_704ILR)
                ? fecha_704ILR.AddDays(HorizontePropuestasDias_704ILR)
                : DateTime.MaxValue.Date;

            List<BE_Salon_704ILR> salones_704ILR = DAL_Salon_704ILR.GetAll_704ILR();
            Dictionary<int, HashSet<DateTime>> ocupadas_704ILR =
                DAL_Reserva_704ILR.FechasConfirmadasPorSalon_704ILR(fecha_704ILR, hasta_704ILR);

            var resultado_704ILR = new List<BE_DisponibilidadSalon_704ILR>();
            foreach (var salon_704ILR in salones_704ILR)
            {
                ocupadas_704ILR.TryGetValue(salon_704ILR.Id_704ILR, out var fechasSalon_704ILR);
                bool libre_704ILR = fechasSalon_704ILR == null || !fechasSalon_704ILR.Contains(fecha_704ILR);
                bool capacidadOk_704ILR = salon_704ILR.Capacidad_704ILR >= capacidadRequerida_704ILR;

                var item_704ILR = new BE_DisponibilidadSalon_704ILR
                {
                    SalonId_704ILR = salon_704ILR.Id_704ILR,
                    SalonNombre_704ILR = salon_704ILR.Nombre_704ILR,
                    Capacidad_704ILR = salon_704ILR.Capacidad_704ILR,
                    FechaConsultada_704ILR = fecha_704ILR,
                    Libre_704ILR = libre_704ILR,
                    CapacidadSuficiente_704ILR = capacidadOk_704ILR
                };

                // Propuesta alternativa solo si el salon serviria pero esta
                // tomado ese dia: primera fecha posterior sin reserva firme.
                if (!libre_704ILR && capacidadOk_704ILR)
                {
                    for (int i_704ILR = 1; i_704ILR <= HorizontePropuestasDias_704ILR; i_704ILR++)
                    {
                        // Pasado el ultimo dia del calendario no hay propuesta utilizable
                        // (se compara sin sumar dias, para no desbordar DateTime).
                        if (fecha_704ILR > FechaMaximaPropuesta_704ILR.AddDays(-i_704ILR)) break;
                        DateTime candidata_704ILR = fecha_704ILR.AddDays(i_704ILR);
                        if (!fechasSalon_704ILR.Contains(candidata_704ILR))
                        {
                            item_704ILR.ProximaFechaLibre_704ILR = candidata_704ILR;
                            break;
                        }
                    }
                }

                resultado_704ILR.Add(item_704ILR);
            }

            // Orden de "similar preferencia": primero lo que se puede reservar
            // tal cual, despues lo ocupado con capacidad (tiene propuesta) y al
            // final lo chico; dentro de cada grupo, capacidad mas cercana a la
            // pedida primero.
            List<BE_DisponibilidadSalon_704ILR> ordenado_704ILR = resultado_704ILR
                .OrderByDescending(d_704ILR => d_704ILR.Disponible_704ILR)
                .ThenByDescending(d_704ILR => d_704ILR.CapacidadSuficiente_704ILR)
                .ThenBy(d_704ILR => Math.Abs(d_704ILR.Capacidad_704ILR - capacidadRequerida_704ILR))
                .ThenBy(d_704ILR => d_704ILR.SalonNombre_704ILR)
                .ToList();

            // La consulta es parte del proceso de venta y queda en la bitacora
            // (CUN002, postcondicion). Se asienta aca, en la capa de negocio, como
            // el resto de las operaciones del proceso: asi vale para cualquier
            // llamador y no solo para el dialogo.
            // La fecha del detalle se escribe con la cultura invariante (calendario
            // gregoriano), igual que el historial de cambios: con la configuracion
            // regional de la estacion salia en otro calendario (2569 en th-TH, 1448 en
            // ar-SA) y, con un calendario que no admite la fecha consultada (UmAlQura
            // llega hasta 2077), la consulta entera lanzaba antes de devolver resultados.
            int disponibles_704ILR = ordenado_704ILR.Count(d_704ILR => d_704ILR.Disponible_704ILR);
            BLL_Bitacora_704ILR.Registrar_704ILR("Reservas", "Disponibilidad consultada", CriticidadBitacora_704ILR.Info,
                "Fecha " + fecha_704ILR.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " | Invitados " + capacidadRequerida_704ILR +
                " | Disponibles: " + disponibles_704ILR + "/" + ordenado_704ILR.Count);

            return ordenado_704ILR;
        }
    }
}
