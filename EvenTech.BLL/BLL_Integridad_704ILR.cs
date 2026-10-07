using System.Collections.Generic;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Resultado de la verificacion de integridad al iniciar la aplicacion.
    public class ResultadoIntegridad_704ILR
    {
        public bool Ok_704ILR => Inconsistencias_704ILR.Count == 0;
        public List<string> Inconsistencias_704ILR { get; } = new List<string>();
    }

    // Coordina el calculo y verificacion de los digitos verificadores sobre la
    // entidad Reserva (T07/T08), usando el mecanismo generico de Services.
    public static class BLL_Integridad_704ILR
    {
        private const string TablaReservas_704ILR = "Reservas";

        // Recalcula el DV horizontal de TODAS las reservas y luego el vertical.
        // Util tras corregir datos corruptos o tras una migracion que cambia los
        // campos que entran al DV (deja la linea base consistente para que la
        // verificacion al arranque no falle). Devuelve cuantas reservas proceso.
        // Es una accion administrativa: queda registrada en bitacora.
        //
        // Una reserva con el estado almacenado fuera del dominio (escrito por fuera
        // del sistema) no se toma como linea base: seria legitimar el dato alterado.
        // Se omite, sigue a la vista de la verificacion y el asiento la nombra.
        public static int RecalcularTodo_704ILR()
        {
            var reservas_704ILR = DAL_Reserva_704ILR.GetAll_704ILR();
            List<int> fueraDeDominio_704ILR = DAL_Reserva_704ILR.IdsConEstadoFueraDeDominio_704ILR();
            var omitir_704ILR = new HashSet<int>(fueraDeDominio_704ILR);
            int procesadas_704ILR = 0;
            foreach (var r_704ILR in reservas_704ILR)
            {
                if (omitir_704ILR.Contains(r_704ILR.Id_704ILR)) continue;
                DAL_Reserva_704ILR.UpdateDvh_704ILR(r_704ILR.Id_704ILR, ValidadorDeIntegridad_704ILR.CalcularDVH_704ILR(r_704ILR));
                procesadas_704ILR++;
            }
            RecalcularDVVerticalReservas_704ILR();

            string detalle_704ILR = $"Se recalculo el DVH de {procesadas_704ILR} reserva(s) y el DVV del conjunto";
            if (fueraDeDominio_704ILR.Count > 0)
                detalle_704ILR += "; se omitieron las reservas con el estado almacenado fuera del dominio: #" +
                                  string.Join(", #", fueraDeDominio_704ILR);
            BLL_Bitacora_704ILR.Registrar_704ILR("Integridad", "Recalculo de linea base",
                CriticidadBitacora_704ILR.Advertencia, detalle_704ILR);
            return procesadas_704ILR;
        }

        // Recalcula el DV vertical de Reservas a partir de los DVH almacenados.
        // Se invoca tras cada alta/modificacion para mantener la linea base. Lo hace
        // un puesto por vez (DAL_DVVertical_704ILR.Serializar_704ILR): el ultimo en
        // recalcular lee todo lo ya confirmado, asi el digito guardado nunca queda
        // anterior al ultimo cambio.
        public static void RecalcularDVVerticalReservas_704ILR()
        {
            DAL_DVVertical_704ILR.Serializar_704ILR(TablaReservas_704ILR, () =>
            {
                var reservas_704ILR = DAL_Reserva_704ILR.GetAll_704ILR();
                var dvhs_704ILR = new List<string>();
                // Orden estable (por Id ascendente) para que la posicion sea consistente.
                reservas_704ILR.Sort((a_704ILR, b_704ILR) => a_704ILR.Id_704ILR.CompareTo(b_704ILR.Id_704ILR));
                foreach (var r_704ILR in reservas_704ILR) dvhs_704ILR.Add(r_704ILR.Dvh_704ILR ?? string.Empty);

                string dvv_704ILR = ValidadorDeIntegridad_704ILR.CalcularDVV_704ILR(dvhs_704ILR);
                DAL_DVVertical_704ILR.Upsert_704ILR(TablaReservas_704ILR, dvv_704ILR);
            });
        }

        // Verificacion de integridad: se ejecuta al arrancar, antes del login.
        public static ResultadoIntegridad_704ILR Verificar_704ILR() => Verificar_704ILR(false);

        // 'segundaLectura': si lo unico que no coincide es el DV vertical, puede ser que
        // otro puesto este entre el guardado de una reserva y el recalculo del digito
        // (las dos lecturas de esta verificacion no son una foto del mismo instante). Se
        // vuelve a leer una vez antes de informarlo: una alteracion real sigue estando.
        private static ResultadoIntegridad_704ILR Verificar_704ILR(bool segundaLectura_704ILR)
        {
            var resultado_704ILR = new ResultadoIntegridad_704ILR();
            var reservas_704ILR = DAL_Reserva_704ILR.GetAll_704ILR();
            reservas_704ILR.Sort((a_704ILR, b_704ILR) => a_704ILR.Id_704ILR.CompareTo(b_704ILR.Id_704ILR));

            // Estado fuera del dominio exacto de la tabla de estados (por ejemplo
            // 'confirmada'): la lectura lo tolera para no dejar sin listado a todas las
            // reservas, y aca se informa como inconsistencia de ESA reserva.
            var fueraDeDominio_704ILR = new HashSet<int>(DAL_Reserva_704ILR.IdsConEstadoFueraDeDominio_704ILR());

            var dvhs_704ILR = new List<string>();
            foreach (var r_704ILR in reservas_704ILR)
            {
                dvhs_704ILR.Add(r_704ILR.Dvh_704ILR ?? string.Empty);

                if (fueraDeDominio_704ILR.Contains(r_704ILR.Id_704ILR))
                    resultado_704ILR.Inconsistencias_704ILR.Add($"Reserva #{r_704ILR.Id_704ILR}: estado almacenado fuera del dominio de la tabla de estados (posible alteracion externa).");

                // Un texto que no corresponde a ningun estado no tiene DV que comparar:
                // ya quedo informado arriba y no se duplica como diferencia de DV.
                if (!System.Enum.IsDefined(typeof(EstadoReserva_704ILR), r_704ILR.Estado_704ILR)) continue;

                // DV horizontal: recalcular y comparar contra lo almacenado.
                string dvhCalculado_704ILR = ValidadorDeIntegridad_704ILR.CalcularDVH_704ILR(r_704ILR);
                if (r_704ILR.Dvh_704ILR == null)
                    resultado_704ILR.Inconsistencias_704ILR.Add($"Reserva #{r_704ILR.Id_704ILR}: sin DV horizontal almacenado.");
                else if (r_704ILR.Dvh_704ILR != dvhCalculado_704ILR)
                    resultado_704ILR.Inconsistencias_704ILR.Add($"Reserva #{r_704ILR.Id_704ILR}: DV horizontal no coincide (posible alteracion externa).");
            }

            // DV vertical: recalcular sobre el conjunto y comparar contra lo almacenado.
            string dvvCalculado_704ILR = ValidadorDeIntegridad_704ILR.CalcularDVV_704ILR(dvhs_704ILR);
            string dvvAlmacenado_704ILR = DAL_DVVertical_704ILR.Get_704ILR(TablaReservas_704ILR);

            if (dvvAlmacenado_704ILR == null)
            {
                // Primera corrida: establecer linea base, no es una inconsistencia.
                DAL_DVVertical_704ILR.Upsert_704ILR(TablaReservas_704ILR, dvvCalculado_704ILR);
            }
            else if (dvvAlmacenado_704ILR != dvvCalculado_704ILR)
            {
                if (!segundaLectura_704ILR && resultado_704ILR.Ok_704ILR)
                {
                    System.Threading.Thread.Sleep(400);
                    return Verificar_704ILR(true);
                }
                resultado_704ILR.Inconsistencias_704ILR.Add("DV vertical de Reservas no coincide (filas agregadas, quitadas o reordenadas por fuera del sistema).");
            }

            if (!resultado_704ILR.Ok_704ILR)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Integridad", "Verificacion de integridad fallida",
                    CriticidadBitacora_704ILR.Error,
                    string.Join(" | ", resultado_704ILR.Inconsistencias_704ILR));
            }
            return resultado_704ILR;
        }
    }
}
