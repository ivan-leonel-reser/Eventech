using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Cronograma del evento (CUN008). Ver BLL_Coordinacion_704ILR para las reglas
    // comunes y la serializacion.
    public static class BLL_Cronograma_704ILR
    {
        // Ancho de CronogramaActividades.Descripcion y rango de la duracion estimada
        // (un tramo dura al menos un minuto y a lo sumo un dia). Publicos: la
        // pantalla los usa para limitar lo que se puede tipear.
        public const int MaxDescripcion_704ILR = 150;
        public const int DuracionMinima_704ILR = 1;
        public const int DuracionMaxima_704ILR = 1440;

        public static BE_Cronograma_704ILR GetByReserva_704ILR(int reservaId_704ILR)
            => DAL_Cronograma_704ILR.GetByReserva_704ILR(reservaId_704ILR);

        // CUN008 — Genera el cronograma del evento o, si ya existe, reemplaza sus
        // actividades por las recibidas, en el orden en que llegan.
        //
        // RN-11 — El cronograma se genera con el equipo ya confirmado: tiene que haber al
        // menos una asignacion confirmada y ninguna pendiente de respuesta, y el
        // responsable de cada actividad tiene que ser personal confirmado del evento.
        // Hay un solo cronograma por reserva. Uno ya generado se puede seguir
        // modificando aunque alguna respuesta haya vuelto a quedar pendiente (una
        // reprogramacion reinicia las confirmaciones, RN-08), siempre con responsables
        // confirmados: es lo que permite pasar a otro integrante las actividades de
        // quien todavia no respondio y, recien entonces, quitarlo del equipo.
        public static CoordinacionResult_704ILR Guardar_704ILR(int reservaId_704ILR, IList<BE_CronogramaActividad_704ILR> actividades_704ILR)
        {
            if (actividades_704ILR == null || actividades_704ILR.Count == 0) return CoordinacionResult_704ILR.SinActividades_704ILR;

            // Cada actividad se normaliza como se guarda: la descripcion tal como se ve
            // y la hora al minuto.
            var normalizadas_704ILR = new List<BE_CronogramaActividad_704ILR>(actividades_704ILR.Count);
            foreach (var a_704ILR in actividades_704ILR)
            {
                if (a_704ILR == null) return CoordinacionResult_704ILR.ActividadInvalida_704ILR;
                string descripcion_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(a_704ILR.Descripcion_704ILR);
                if (descripcion_704ILR.Length == 0 || descripcion_704ILR.Length > MaxDescripcion_704ILR
                    || a_704ILR.DuracionMinutos_704ILR < DuracionMinima_704ILR || a_704ILR.DuracionMinutos_704ILR > DuracionMaxima_704ILR
                    || !BLL_Coordinacion_704ILR.HoraDelDia_704ILR(a_704ILR.Hora_704ILR))
                    return CoordinacionResult_704ILR.ActividadInvalida_704ILR;
                normalizadas_704ILR.Add(new BE_CronogramaActividad_704ILR
                {
                    Hora_704ILR = BLL_Coordinacion_704ILR.AlMinuto_704ILR(a_704ILR.Hora_704ILR),
                    Descripcion_704ILR = descripcion_704ILR,
                    ResponsableId_704ILR = a_704ILR.ResponsableId_704ILR,
                    DuracionMinutos_704ILR = a_704ILR.DuracionMinutos_704ILR
                });
            }

            CoordinacionResult_704ILR resultado_704ILR;
            bool generado_704ILR = false, sinCambios_704ILR = false;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirParaPlanificar_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out BE_Reserva_704ILR reserva_704ILR);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        var asignaciones_704ILR = DAL_AsignacionPersonal_704ILR.GetByReserva_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                        var confirmados_704ILR = new HashSet<int>(asignaciones_704ILR
                            .Where(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.CONFIRMADA)
                            .Select(a_704ILR => a_704ILR.EmpleadoId_704ILR));

                        BE_Cronograma_704ILR existente_704ILR = DAL_Cronograma_704ILR.GetByReserva_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);

                        // "Ninguna respuesta pendiente" se exige al generar; al modificar alcanza
                        // con que haya personal confirmado y cada responsable lo sea.
                        if (confirmados_704ILR.Count == 0
                            || (existente_704ILR == null && asignaciones_704ILR.Any(a_704ILR => a_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.PENDIENTE)))
                            resultado_704ILR = CoordinacionResult_704ILR.PersonalSinConfirmar_704ILR;
                        else if (normalizadas_704ILR.Any(a_704ILR => !confirmados_704ILR.Contains(a_704ILR.ResponsableId_704ILR)))
                            resultado_704ILR = CoordinacionResult_704ILR.ResponsableInvalido_704ILR;
                        else
                        {
                            // Guardar sin cambiar nada no es una modificacion: no se escribe ni
                            // se asienta (mismo criterio que la edicion de reservas).
                            sinCambios_704ILR = existente_704ILR != null && MismasActividades_704ILR(existente_704ILR.Actividades_704ILR, normalizadas_704ILR);
                            if (!sinCambios_704ILR)
                            {
                                int cronogramaId_704ILR = existente_704ILR != null
                                    ? existente_704ILR.Id_704ILR
                                    : DAL_Cronograma_704ILR.Insert_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                                generado_704ILR = existente_704ILR == null;
                                DAL_Cronograma_704ILR.ReplaceActividades_704ILR(cronogramaId_704ILR, normalizadas_704ILR, conn_704ILR, tx_704ILR);
                                BLL_Coordinacion_704ILR.Recalcular_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR);
                                tx_704ILR.Commit();
                            }
                        }
                    }
                }
            }

            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR && !sinCambios_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR,
                    generado_704ILR ? "Generacion de cronograma" : "Modificacion de cronograma", CriticidadBitacora_704ILR.Info,
                    $"Reserva #{reservaId_704ILR}: cronograma con {normalizadas_704ILR.Count} actividad(es).");
            else if (resultado_704ILR == CoordinacionResult_704ILR.PersonalSinConfirmar_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Cronograma rechazado", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: el equipo todavia no esta confirmado (RN-11).");
            else if (resultado_704ILR == CoordinacionResult_704ILR.ResponsableInvalido_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Cronograma rechazado", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: una actividad tiene a cargo a alguien que no es personal confirmado del evento (RN-11).");
            return resultado_704ILR;
        }

        // Elimina el cronograma de la reserva. No se elimina con tareas asignadas:
        // primero se quitan.
        public static CoordinacionResult_704ILR Eliminar_704ILR(int reservaId_704ILR)
        {
            CoordinacionResult_704ILR resultado_704ILR;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirParaPlanificar_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out BE_Reserva_704ILR reserva_704ILR);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        BE_Cronograma_704ILR existente_704ILR = DAL_Cronograma_704ILR.GetByReserva_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                        if (existente_704ILR == null)
                            resultado_704ILR = CoordinacionResult_704ILR.SinCronograma_704ILR;
                        else if (DAL_Tarea_704ILR.Contar_704ILR(reservaId_704ILR, 0, conn_704ILR, tx_704ILR) > 0)
                            resultado_704ILR = CoordinacionResult_704ILR.CronogramaConTareas_704ILR;
                        else
                        {
                            DAL_Cronograma_704ILR.Delete_704ILR(existente_704ILR.Id_704ILR, conn_704ILR, tx_704ILR);
                            BLL_Coordinacion_704ILR.Recalcular_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            // El rechazo por tener tareas se asienta con el mismo criterio que la baja de
            // un integrante con carga: la pantalla ofrece la operacion y el plan la impide.
            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Eliminacion de cronograma", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: se elimina el cronograma.");
            else if (resultado_704ILR == CoordinacionResult_704ILR.CronogramaConTareas_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Cronograma rechazado", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: no se elimina el cronograma, tiene tareas asignadas.");
            return resultado_704ILR;
        }

        // Compara actividad por actividad, en orden, lo guardado con lo que se guardaria.
        private static bool MismasActividades_704ILR(IList<BE_CronogramaActividad_704ILR> guardadas_704ILR, IList<BE_CronogramaActividad_704ILR> nuevas_704ILR)
        {
            if (guardadas_704ILR.Count != nuevas_704ILR.Count) return false;
            for (int i_704ILR = 0; i_704ILR < guardadas_704ILR.Count; i_704ILR++)
            {
                var g_704ILR = guardadas_704ILR[i_704ILR];
                var n_704ILR = nuevas_704ILR[i_704ILR];
                if (g_704ILR.Hora_704ILR != n_704ILR.Hora_704ILR || g_704ILR.Descripcion_704ILR != n_704ILR.Descripcion_704ILR
                    || g_704ILR.ResponsableId_704ILR != n_704ILR.ResponsableId_704ILR || g_704ILR.DuracionMinutos_704ILR != n_704ILR.DuracionMinutos_704ILR)
                    return false;
            }
            return true;
        }
    }
}
