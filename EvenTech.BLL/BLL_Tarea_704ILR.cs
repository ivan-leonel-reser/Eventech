using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Tareas especificas del equipo (CUN009) y su consulta por el propio empleado
    // (CUN010). Ver BLL_Coordinacion_704ILR para las reglas comunes y la serializacion.
    public static class BLL_Tarea_704ILR
    {
        // Anchos de dbo.Tareas. Publicos: la pantalla los usa para limitar lo tipeado.
        public const int MaxDescripcion_704ILR = 200;
        public const int MaxRecursos_704ILR = 200;

        // Tareas del evento, agrupadas por empleado y, dentro de cada uno, en el orden
        // en que ocurren dentro de su turno (una jornada que cruza la medianoche no se
        // puede ordenar por la hora a secas).
        public static List<BE_Tarea_704ILR> GetByReserva_704ILR(int reservaId_704ILR)
        {
            var turnos_704ILR = DAL_AsignacionPersonal_704ILR.GetByReserva_704ILR(reservaId_704ILR)
                .ToDictionary(a_704ILR => a_704ILR.EmpleadoId_704ILR, a_704ILR => a_704ILR.HoraInicio_704ILR);
            return DAL_Tarea_704ILR.GetByReserva_704ILR(reservaId_704ILR)
                .OrderBy(t_704ILR => t_704ILR.EmpleadoNombre_704ILR, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(t_704ILR => t_704ILR.EmpleadoId_704ILR)
                .ThenBy(t_704ILR => DesdeElInicio_704ILR(t_704ILR.HoraInicio_704ILR,
                    turnos_704ILR.TryGetValue(t_704ILR.EmpleadoId_704ILR, out TimeSpan inicio_704ILR) ? inicio_704ILR : TimeSpan.Zero))
                .ThenBy(t_704ILR => t_704ILR.Id_704ILR)
                .ToList();
        }

        // CUN010 — Tareas del empleado de la sesion en un evento.
        public static List<BE_Tarea_704ILR> GetMisTareas_704ILR(int reservaId_704ILR)
        {
            BE_Empleado_704ILR empleado_704ILR = BLL_Empleado_704ILR.GetDeLaSesion_704ILR();
            return empleado_704ILR == null
                ? new List<BE_Tarea_704ILR>()
                : GetByReserva_704ILR(reservaId_704ILR).Where(t_704ILR => t_704ILR.EmpleadoId_704ILR == empleado_704ILR.Id_704ILR).ToList();
        }

        // CUN009 — Asigna una tarea a un integrante del equipo sobre el cronograma del
        // evento (la reserva viaja en ReservaId_704ILR de la tarea).
        //
        // RN-12 — La tarea se asigna a personal confirmado del evento, cae dentro de su
        // turno y no se pisa con otra tarea suya. Las horas se interpretan dentro del
        // turno del empleado: si el turno es de 21:00 a 03:00, una tarea de 00:30 a
        // 01:30 ocurre despues de la medianoche.
        public static CoordinacionResult_704ILR Asignar_704ILR(BE_Tarea_704ILR tarea_704ILR, out int nuevoId_704ILR)
        {
            nuevoId_704ILR = 0;
            if (tarea_704ILR == null) return CoordinacionResult_704ILR.DescripcionInvalida_704ILR;

            tarea_704ILR.Descripcion_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(tarea_704ILR.Descripcion_704ILR);
            if (tarea_704ILR.Descripcion_704ILR.Length == 0 || tarea_704ILR.Descripcion_704ILR.Length > MaxDescripcion_704ILR)
                return CoordinacionResult_704ILR.DescripcionInvalida_704ILR;
            // Los recursos son un dato accesorio: se recortan a lo que entra.
            tarea_704ILR.Recursos_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(tarea_704ILR.Recursos_704ILR);
            if (tarea_704ILR.Recursos_704ILR.Length > MaxRecursos_704ILR)
                tarea_704ILR.Recursos_704ILR = tarea_704ILR.Recursos_704ILR.Substring(0, MaxRecursos_704ILR);
            if (!BLL_Coordinacion_704ILR.HoraDelDia_704ILR(tarea_704ILR.HoraInicio_704ILR) || !BLL_Coordinacion_704ILR.HoraDelDia_704ILR(tarea_704ILR.HoraFin_704ILR))
                return CoordinacionResult_704ILR.FranjaInvalida_704ILR;
            tarea_704ILR.HoraInicio_704ILR = BLL_Coordinacion_704ILR.AlMinuto_704ILR(tarea_704ILR.HoraInicio_704ILR);
            tarea_704ILR.HoraFin_704ILR = BLL_Coordinacion_704ILR.AlMinuto_704ILR(tarea_704ILR.HoraFin_704ILR);
            if (tarea_704ILR.HoraInicio_704ILR == tarea_704ILR.HoraFin_704ILR) return CoordinacionResult_704ILR.FranjaInvalida_704ILR;
            if (!Enum.IsDefined(typeof(PrioridadTarea_704ILR), tarea_704ILR.Prioridad_704ILR))
                tarea_704ILR.Prioridad_704ILR = PrioridadTarea_704ILR.MEDIA;

            CoordinacionResult_704ILR resultado_704ILR;
            int reservaId_704ILR = tarea_704ILR.ReservaId_704ILR;
            BE_AsignacionPersonal_704ILR turno_704ILR = null;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirParaPlanificar_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out _);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        BE_Cronograma_704ILR cronograma_704ILR = DAL_Cronograma_704ILR.GetByReserva_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                        turno_704ILR = DAL_AsignacionPersonal_704ILR.GetDeEmpleadoEnReserva_704ILR(reservaId_704ILR, tarea_704ILR.EmpleadoId_704ILR, conn_704ILR, tx_704ILR);
                        if (cronograma_704ILR == null)
                            resultado_704ILR = CoordinacionResult_704ILR.SinCronograma_704ILR;
                        else if (turno_704ILR == null || turno_704ILR.Estado_704ILR != EstadoAsignacion_704ILR.CONFIRMADA)
                            resultado_704ILR = CoordinacionResult_704ILR.ResponsableInvalido_704ILR;
                        else if (!DentroDelTurno_704ILR(tarea_704ILR, turno_704ILR))
                            resultado_704ILR = CoordinacionResult_704ILR.FueraDeFranja_704ILR;
                        else if (DAL_Tarea_704ILR.GetByReserva_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR)
                                     .Any(otra_704ILR => otra_704ILR.EmpleadoId_704ILR == tarea_704ILR.EmpleadoId_704ILR
                                                         && SeSuperponen_704ILR(tarea_704ILR, otra_704ILR, turno_704ILR.HoraInicio_704ILR)))
                            resultado_704ILR = CoordinacionResult_704ILR.TareaSuperpuesta_704ILR;
                        else
                        {
                            tarea_704ILR.CronogramaId_704ILR = cronograma_704ILR.Id_704ILR;
                            nuevoId_704ILR = DAL_Tarea_704ILR.Insert_704ILR(tarea_704ILR, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Asignacion de tarea", CriticidadBitacora_704ILR.Info,
                    $"Reserva #{reservaId_704ILR}: tarea #{nuevoId_704ILR} para {turno_704ILR.EmpleadoNombre_704ILR} (#{tarea_704ILR.EmpleadoId_704ILR}) " +
                    $"de {BLL_Coordinacion_704ILR.FranjaBitacora_704ILR(tarea_704ILR.HoraInicio_704ILR, tarea_704ILR.HoraFin_704ILR)}, prioridad {tarea_704ILR.Prioridad_704ILR}.");
            else if (resultado_704ILR == CoordinacionResult_704ILR.SinCronograma_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Tarea rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: el evento no tiene cronograma y las tareas se asignan sobre el cronograma generado.");
            else if (resultado_704ILR == CoordinacionResult_704ILR.ResponsableInvalido_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Tarea rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: el empleado #{tarea_704ILR.EmpleadoId_704ILR} no es personal confirmado del evento (RN-12).");
            else if (resultado_704ILR == CoordinacionResult_704ILR.FueraDeFranja_704ILR || resultado_704ILR == CoordinacionResult_704ILR.TareaSuperpuesta_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Tarea rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: la tarea de {BLL_Coordinacion_704ILR.FranjaBitacora_704ILR(tarea_704ILR.HoraInicio_704ILR, tarea_704ILR.HoraFin_704ILR)} " +
                    $"para {turno_704ILR.EmpleadoNombre_704ILR} (#{tarea_704ILR.EmpleadoId_704ILR}) " +
                    (resultado_704ILR == CoordinacionResult_704ILR.FueraDeFranja_704ILR
                        ? $"queda fuera de su turno de {BLL_Coordinacion_704ILR.FranjaBitacora_704ILR(turno_704ILR.HoraInicio_704ILR, turno_704ILR.HoraFin_704ILR)}"
                        : "se superpone con otra tarea suya") + " (RN-12).");
            return resultado_704ILR;
        }

        public static CoordinacionResult_704ILR Quitar_704ILR(int tareaId_704ILR)
        {
            BE_Tarea_704ILR tarea_704ILR = tareaId_704ILR <= 0 ? null : DAL_Tarea_704ILR.GetById_704ILR(tareaId_704ILR);
            if (tarea_704ILR == null) return CoordinacionResult_704ILR.TareaInvalida_704ILR;

            CoordinacionResult_704ILR resultado_704ILR;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirParaPlanificar_704ILR(tarea_704ILR.ReservaId_704ILR, conn_704ILR, tx_704ILR, out _);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        // Si la tarea ya no estaba (la quito otra sesion), no se informa ni
                        // se asienta una baja que no ocurrio.
                        if (DAL_Tarea_704ILR.Delete_704ILR(tareaId_704ILR, conn_704ILR, tx_704ILR) == 0)
                            resultado_704ILR = CoordinacionResult_704ILR.TareaInvalida_704ILR;
                        else
                            tx_704ILR.Commit();
                    }
                }
            }

            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Baja de tarea", CriticidadBitacora_704ILR.Info,
                    $"Reserva #{tarea_704ILR.ReservaId_704ILR}: se quita la tarea #{tareaId_704ILR} de {tarea_704ILR.EmpleadoNombre_704ILR} (#{tarea_704ILR.EmpleadoId_704ILR}).");
            return resultado_704ILR;
        }

        // True si todas las tareas que el empleado ya tiene en el evento caben en el
        // turno recibido. La usa la reasignacion de un turno rechazado, que puede traer
        // otra franja.
        internal static bool TareasDentroDeFranja_704ILR(BE_AsignacionPersonal_704ILR turno_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
            => DAL_Tarea_704ILR.GetByReserva_704ILR(turno_704ILR.ReservaId_704ILR, conn_704ILR, tx_704ILR)
                .Where(t_704ILR => t_704ILR.EmpleadoId_704ILR == turno_704ILR.EmpleadoId_704ILR)
                .All(t_704ILR => DentroDelTurno_704ILR(t_704ILR, turno_704ILR));

        // Franja que la pantalla propone para la proxima tarea de un empleado: la primera
        // hora libre de su turno. Arranca donde terminan las tareas que tiene seguidas
        // desde el inicio del turno y dura una hora, o menos si antes empieza otra tarea
        // suya o termina el turno (RN-12). Con el turno completo se propone su primera
        // hora: la asignacion la rechaza y dice por que.
        public static void PrimeraFranjaLibre_704ILR(BE_AsignacionPersonal_704ILR turno_704ILR, IEnumerable<BE_Tarea_704ILR> tareas_704ILR,
            out TimeSpan desde_704ILR, out TimeSpan hasta_704ILR)
        {
            TimeSpan largoTurno_704ILR = turno_704ILR.Duracion_704ILR;

            // Las tareas del empleado como tramos medidos desde el inicio del turno.
            var tramos_704ILR = new List<(TimeSpan Desde_704ILR, TimeSpan Hasta_704ILR)>();
            if (tareas_704ILR != null)
                foreach (var t_704ILR in tareas_704ILR)
                {
                    if (t_704ILR == null || t_704ILR.EmpleadoId_704ILR != turno_704ILR.EmpleadoId_704ILR) continue;
                    TimeSpan inicio_704ILR = DesdeElInicio_704ILR(t_704ILR.HoraInicio_704ILR, turno_704ILR.HoraInicio_704ILR);
                    tramos_704ILR.Add((inicio_704ILR, inicio_704ILR + Duracion_704ILR(t_704ILR)));
                }
            tramos_704ILR.Sort((a_704ILR, b_704ILR) => a_704ILR.Desde_704ILR.CompareTo(b_704ILR.Desde_704ILR));

            TimeSpan libre_704ILR = TimeSpan.Zero;
            foreach (var tramo_704ILR in tramos_704ILR)
            {
                if (tramo_704ILR.Desde_704ILR > libre_704ILR) break;          // queda un hueco antes de esta tarea
                if (tramo_704ILR.Hasta_704ILR > libre_704ILR) libre_704ILR = tramo_704ILR.Hasta_704ILR;
            }
            if (libre_704ILR >= largoTurno_704ILR) libre_704ILR = TimeSpan.Zero;

            TimeSpan fin_704ILR = libre_704ILR + TimeSpan.FromHours(1);
            if (fin_704ILR > largoTurno_704ILR) fin_704ILR = largoTurno_704ILR;
            foreach (var tramo_704ILR in tramos_704ILR)
                if (tramo_704ILR.Desde_704ILR > libre_704ILR && tramo_704ILR.Desde_704ILR < fin_704ILR) { fin_704ILR = tramo_704ILR.Desde_704ILR; break; }

            desde_704ILR = AHoraDelDia_704ILR(turno_704ILR.HoraInicio_704ILR + libre_704ILR);
            hasta_704ILR = AHoraDelDia_704ILR(turno_704ILR.HoraInicio_704ILR + fin_704ILR);
        }

        // ---------------------------------------------------------------
        // Aritmetica de horas dentro de un turno. Toda hora se mide como el tiempo que
        // pasa desde el inicio del turno (entre 0 y 24 horas), que es lo que permite
        // comparar y ordenar horas de una jornada que cruza la medianoche.

        private static readonly TimeSpan UnDia_704ILR = TimeSpan.FromDays(1);

        // Tiempo que pasa desde 'inicioTurno' hasta 'hora' (0 si coinciden).
        private static TimeSpan DesdeElInicio_704ILR(TimeSpan hora_704ILR, TimeSpan inicioTurno_704ILR)
        {
            TimeSpan d_704ILR = hora_704ILR - inicioTurno_704ILR;
            return d_704ILR < TimeSpan.Zero ? d_704ILR + UnDia_704ILR : d_704ILR;
        }

        // Una hora medida desde el inicio de un turno, llevada de vuelta a hora del dia.
        private static TimeSpan AHoraDelDia_704ILR(TimeSpan hora_704ILR)
            => hora_704ILR >= UnDia_704ILR ? hora_704ILR - UnDia_704ILR : hora_704ILR;

        // Duracion de una tarea: de su inicio a su fin, cruzando la medianoche si el
        // fin no es posterior (inicio y fin nunca coinciden).
        private static TimeSpan Duracion_704ILR(BE_Tarea_704ILR tarea_704ILR)
            => DesdeElInicio_704ILR(tarea_704ILR.HoraFin_704ILR, tarea_704ILR.HoraInicio_704ILR);

        // RN-12: la tarea empieza y termina dentro del turno.
        private static bool DentroDelTurno_704ILR(BE_Tarea_704ILR tarea_704ILR, BE_AsignacionPersonal_704ILR turno_704ILR)
        {
            TimeSpan largoTurno_704ILR = DesdeElInicio_704ILR(turno_704ILR.HoraFin_704ILR, turno_704ILR.HoraInicio_704ILR);
            if (largoTurno_704ILR == TimeSpan.Zero) largoTurno_704ILR = UnDia_704ILR;
            TimeSpan desde_704ILR = DesdeElInicio_704ILR(tarea_704ILR.HoraInicio_704ILR, turno_704ILR.HoraInicio_704ILR);
            return desde_704ILR + Duracion_704ILR(tarea_704ILR) <= largoTurno_704ILR;
        }

        // RN-12: dos tareas del mismo empleado comparten al menos un minuto.
        private static bool SeSuperponen_704ILR(BE_Tarea_704ILR a_704ILR, BE_Tarea_704ILR b_704ILR, TimeSpan inicioTurno_704ILR)
        {
            TimeSpan aDesde_704ILR = DesdeElInicio_704ILR(a_704ILR.HoraInicio_704ILR, inicioTurno_704ILR);
            TimeSpan bDesde_704ILR = DesdeElInicio_704ILR(b_704ILR.HoraInicio_704ILR, inicioTurno_704ILR);
            return aDesde_704ILR < bDesde_704ILR + Duracion_704ILR(b_704ILR) && bDesde_704ILR < aDesde_704ILR + Duracion_704ILR(a_704ILR);
        }
    }
}
