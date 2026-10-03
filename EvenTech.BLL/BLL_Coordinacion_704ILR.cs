using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;

namespace EvenTech.BLL
{
    // Resultado de las operaciones de coordinacion de un evento (Proceso 2):
    // asignacion de personal, confirmacion de disponibilidad, cronograma, tareas,
    // ejecucion e incidencias.
    public enum CoordinacionResult_704ILR
    {
        Success_704ILR,
        ReservaInvalida_704ILR,          // la reserva no existe
        ReservaNoConfirmada_704ILR,      // RN-08: solo se coordina una reserva CONFIRMADA
        EventoEnEjecucion_704ILR,        // RN-13: el plan queda congelado cuando el evento empieza
        EventoCerrado_704ILR,            // RN-13: un evento cerrado no admite cambios
        EmpleadoInvalido_704ILR,         // el empleado no existe o esta dado de baja
        EmpleadoDeBaja_704ILR,           // quien responde tiene su ficha dada de baja
        RolInvalido_704ILR,              // falta el rol o no entra en su columna
        FranjaInvalida_704ILR,           // la hora de fin es igual a la de inicio
        Superposicion_704ILR,            // RN-09: la franja se pisa con otra asignacion del empleado
        YaAsignado_704ILR,               // el empleado ya esta asignado a este evento
        AsignacionInvalida_704ILR,       // la asignacion no existe
        SinEmpleadoVinculado_704ILR,     // la cuenta de la sesion no representa a ningun empleado
        NoEsElEmpleado_704ILR,           // RN-10: la respuesta la da el propio empleado
        AsignacionYaRespondida_704ILR,   // la asignacion no esta pendiente de respuesta
        MotivoObligatorio_704ILR,        // RN-10: el rechazo lleva motivo
        TieneCarga_704ILR,               // el empleado tiene actividades a cargo o tareas en el evento
        PersonalSinConfirmar_704ILR,     // RN-11: el cronograma exige a todo el equipo confirmado
        SinActividades_704ILR,           // el cronograma necesita al menos una actividad
        ActividadInvalida_704ILR,        // descripcion vacia o larga, o duracion fuera de rango
        ResponsableInvalido_704ILR,      // RN-11: el responsable no es personal confirmado del evento
        SinCronograma_704ILR,            // las tareas se asignan sobre el cronograma generado
        CronogramaConTareas_704ILR,      // no se elimina un cronograma con tareas asignadas
        DescripcionInvalida_704ILR,      // falta la descripcion o no entra en su columna
        FueraDeFranja_704ILR,            // RN-12: la tarea no cae dentro del turno del empleado
        TareaSuperpuesta_704ILR,         // RN-12: se pisa con otra tarea del mismo empleado
        TareaInvalida_704ILR,            // la tarea no existe
        NoListo_704ILR,                  // RN-13: la ejecucion empieza con el evento LISTO
        FueraDeFecha_704ILR,             // hoy no es el dia del evento: hace falta confirmarlo
        NoEnEjecucion_704ILR,            // incidencias y cierre solo con el evento en ejecucion
        IncidenciaInvalida_704ILR,       // la incidencia no existe o es de otro evento
        IncidenciaYaResuelta_704ILR,
        ResolucionObligatoria_704ILR,    // resolver exige decir como se resolvio
        IncidenciasAbiertas_704ILR       // RN-13: el evento se cierra con todas las incidencias resueltas
    }

    // Estado de coordinacion de los eventos (Proceso 2) y reglas comunes a todas las
    // operaciones de coordinacion.
    //
    // Serializacion. Asignar, quitar, responder, generar el cronograma, asignar
    // tareas, iniciar, registrar incidencias y cerrar validan sobre lo PERSISTIDO y
    // despues escriben. Todas leen la cabecera de la reserva con
    // DAL_Reserva_704ILR.GetById_704ILR(id, conn, tx) —bloqueo de actualizacion sobre
    // la fila, el mismo que usan las escrituras de la reserva— y validan y escriben
    // dentro de esa transaccion: dos operaciones sobre el mismo evento, o una de
    // coordinacion y una edicion de la reserva, se ejecutan una detras de la otra.
    // Los asientos de bitacora de cada operacion van despues de cerrar la
    // transaccion; el del rechazo por RN-08 o RN-13 se escribe al detectarlo, por
    // otra conexion (solo inserta en la bitacora).
    public static class BLL_Coordinacion_704ILR
    {
        internal const string Modulo_704ILR = "Coordinacion";

        // Eventos a coordinar: las reservas CONFIRMADA con su avance (RN-08).
        public static List<BE_EventoCoordinacion_704ILR> GetEventos_704ILR() => DAL_Coordinacion_704ILR.GetEventos_704ILR();

        public static BE_EventoCoordinacion_704ILR GetEvento_704ILR(int reservaId_704ILR) => DAL_Coordinacion_704ILR.GetEvento_704ILR(reservaId_704ILR);

        // ---------------------------------------------------------------
        // Estado de coordinacion mientras el evento no empezo. Es la unica funcion que
        // lo decide, para que el documento y el codigo compartan una sola fuente:
        //   SIN_ASIGNAR      no hay ninguna asignacion;
        //   LISTO            todas las asignaciones estan confirmadas y el cronograma existe;
        //   EN_COORDINACION  cualquier otro caso (falta una respuesta, hay un rechazo
        //                    sin resolver o falta el cronograma).
        // EN_EJECUCION y CERRADO no se deducen: se entra a ellos con IniciarEjecucion y
        // CerrarEvento.
        public static EstadoCoordinacion_704ILR EstadoPorAvance_704ILR(int asignaciones_704ILR, int confirmadas_704ILR, bool tieneCronograma_704ILR)
        {
            if (asignaciones_704ILR <= 0) return EstadoCoordinacion_704ILR.SIN_ASIGNAR;
            return confirmadas_704ILR == asignaciones_704ILR && tieneCronograma_704ILR
                ? EstadoCoordinacion_704ILR.LISTO
                : EstadoCoordinacion_704ILR.EN_COORDINACION;
        }

        // RN-13: con el evento en ejecucion o cerrado, el plan (personal, cronograma y
        // tareas) y la propia reserva quedan congelados.
        public static bool PlanCongelado_704ILR(EstadoCoordinacion_704ILR estado_704ILR)
            => estado_704ILR == EstadoCoordinacion_704ILR.EN_EJECUCION || estado_704ILR == EstadoCoordinacion_704ILR.CERRADO;

        // Abre el evento para modificar su plan: lee la cabecera con bloqueo y aplica
        // las dos reglas comunes a toda operacion de planificacion. RN-08: solo se
        // coordina una reserva CONFIRMADA (una cotizacion o una pendiente todavia no
        // comprometen el salon, y una cancelada ya no tiene evento). RN-13: el plan
        // no se toca una vez que el evento empezo.
        internal static CoordinacionResult_704ILR AbrirParaPlanificar_704ILR(int reservaId_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR, out BE_Reserva_704ILR reserva_704ILR)
        {
            reserva_704ILR = reservaId_704ILR <= 0 ? null : DAL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
            if (reserva_704ILR == null) return CoordinacionResult_704ILR.ReservaInvalida_704ILR;
            CoordinacionResult_704ILR resultado_704ILR = CoordinacionResult_704ILR.Success_704ILR;
            if (reserva_704ILR.Estado_704ILR != EstadoReserva_704ILR.CONFIRMADA) resultado_704ILR = CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR;
            else if (reserva_704ILR.EstadoCoordinacion_704ILR == EstadoCoordinacion_704ILR.EN_EJECUCION) resultado_704ILR = CoordinacionResult_704ILR.EventoEnEjecucion_704ILR;
            else if (reserva_704ILR.EstadoCoordinacion_704ILR == EstadoCoordinacion_704ILR.CERRADO) resultado_704ILR = CoordinacionResult_704ILR.EventoCerrado_704ILR;
            AsentarRechazoDeRegla_704ILR(reserva_704ILR, resultado_704ILR);
            return resultado_704ILR;
        }

        // Los rechazos por RN-08 y RN-13 son reglas de negocio, no errores de tipeo: la
        // pantalla no ofrece la operacion, asi que solo llegan por una carrera con otra
        // estacion y quedan asentados (mismo criterio que BLL_Reserva_704ILR con la
        // reserva congelada). El asiento solo escribe en la bitacora, por otra conexion.
        private static void AsentarRechazoDeRegla_704ILR(BE_Reserva_704ILR reserva_704ILR, CoordinacionResult_704ILR resultado_704ILR)
        {
            string motivo_704ILR;
            switch (resultado_704ILR)
            {
                case CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR:
                    motivo_704ILR = $"la reserva esta {reserva_704ILR.Estado_704ILR} y solo se coordina una CONFIRMADA (RN-08)";
                    break;
                case CoordinacionResult_704ILR.EventoEnEjecucion_704ILR:
                case CoordinacionResult_704ILR.EventoCerrado_704ILR:
                    motivo_704ILR = $"el evento esta {reserva_704ILR.EstadoCoordinacion_704ILR} y su plan quedo congelado (RN-13)";
                    break;
                case CoordinacionResult_704ILR.NoEnEjecucion_704ILR:
                    motivo_704ILR = $"el evento esta {reserva_704ILR.EstadoCoordinacion_704ILR} y la operacion exige que este EN_EJECUCION (RN-13)";
                    break;
                default:
                    return;
            }
            BLL_Bitacora_704ILR.Registrar_704ILR(Modulo_704ILR, "Coordinacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                $"Reserva #{reserva_704ILR.Id_704ILR}: {motivo_704ILR}.");
        }

        // Vuelve a deducir el estado de coordinacion despues de una escritura del plan
        // y lo persiste si cambio. Se llama dentro de la transaccion de la operacion,
        // con la cabecera bloqueada. No toca un evento en ejecucion o cerrado.
        internal static EstadoCoordinacion_704ILR Recalcular_704ILR(BE_Reserva_704ILR reserva_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            EstadoCoordinacion_704ILR actual_704ILR = reserva_704ILR.EstadoCoordinacion_704ILR;
            if (PlanCongelado_704ILR(actual_704ILR)) return actual_704ILR;

            DAL_Coordinacion_704ILR.Conteos_704ILR(reserva_704ILR.Id_704ILR, conn_704ILR, tx_704ILR,
                out int asignaciones_704ILR, out int confirmadas_704ILR, out bool tieneCronograma_704ILR);
            EstadoCoordinacion_704ILR nuevo_704ILR = EstadoPorAvance_704ILR(asignaciones_704ILR, confirmadas_704ILR, tieneCronograma_704ILR);
            if (nuevo_704ILR != actual_704ILR)
            {
                DAL_Coordinacion_704ILR.SetEstado_704ILR(reserva_704ILR.Id_704ILR, nuevo_704ILR, conn_704ILR, tx_704ILR);
                reserva_704ILR.EstadoCoordinacion_704ILR = nuevo_704ILR;
            }
            return nuevo_704ILR;
        }

        // Las confirmaciones del personal valen para una fecha y para una reserva
        // firme. Si la reserva cambia de fecha, las asignaciones confirmadas vuelven a
        // PENDIENTE y el equipo responde de nuevo (al confirmar se vuelve a controlar
        // la superposicion, RN-09). Si la reserva deja de estar CONFIRMADA —se cancela
        // o se le restaura una version anterior— pasa lo mismo y el personal queda
        // liberado: las consultas que comprometen a un empleado solo miran eventos
        // confirmados. La llama BLL_Reserva dentro de su transaccion; devuelve cuantas
        // se reiniciaron para que las asiente despues del commit.
        internal static int ReiniciarConfirmaciones_704ILR(BE_Reserva_704ILR reserva_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            if (PlanCongelado_704ILR(reserva_704ILR.EstadoCoordinacion_704ILR)) return 0;
            int reiniciadas_704ILR = DAL_AsignacionPersonal_704ILR.ReiniciarConfirmaciones_704ILR(reserva_704ILR.Id_704ILR, conn_704ILR, tx_704ILR);
            if (reiniciadas_704ILR > 0) Recalcular_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR);
            return reiniciadas_704ILR;
        }

        internal static void AsentarConfirmacionesReiniciadas_704ILR(int reservaId_704ILR, int reiniciadas_704ILR, string motivo_704ILR,
            bool personalLiberado_704ILR = false)
        {
            if (reiniciadas_704ILR <= 0) return;
            BLL_Bitacora_704ILR.Registrar_704ILR(Modulo_704ILR, "Confirmaciones reiniciadas", CriticidadBitacora_704ILR.Advertencia,
                $"Reserva #{reservaId_704ILR}: {reiniciadas_704ILR} asignacion(es) confirmada(s) vuelven a PENDIENTE por {motivo_704ILR}; " +
                (personalLiberado_704ILR ? "el personal queda liberado." : "el personal tiene que responder de nuevo."));
        }

        // ---------------------------------------------------------------
        // RN-13 — Ejecucion del evento.
        // La ejecucion empieza con el evento LISTO (todo el equipo confirmado y el
        // cronograma generado). Lo normal es iniciarla el dia del evento; si hoy es otro
        // dia se devuelve FueraDeFecha y quien llama decide si confirma (el armado puede
        // empezar la vispera y un evento puede cerrarse al dia siguiente). Desde ese
        // momento el plan queda congelado y lo que se sale de el se anota como incidencia.
        public static CoordinacionResult_704ILR IniciarEjecucion_704ILR(int reservaId_704ILR, bool confirmarFueraDeFecha_704ILR)
        {
            CoordinacionResult_704ILR resultado_704ILR;
            BE_Reserva_704ILR reserva_704ILR;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = AbrirParaPlanificar_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out reserva_704ILR);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        // El estado se vuelve a deducir de lo persistido: no alcanza con el
                        // que quedo guardado si alguna escritura lo dejo atrasado.
                        if (Recalcular_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR) != EstadoCoordinacion_704ILR.LISTO)
                            resultado_704ILR = CoordinacionResult_704ILR.NoListo_704ILR;
                        else if (reserva_704ILR.FechaEvento_704ILR.Date != DateTime.Today && !confirmarFueraDeFecha_704ILR)
                            resultado_704ILR = CoordinacionResult_704ILR.FueraDeFecha_704ILR;
                        else
                        {
                            DAL_Coordinacion_704ILR.SetEstado_704ILR(reservaId_704ILR, EstadoCoordinacion_704ILR.EN_EJECUCION, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
            {
                bool fueraDeFecha_704ILR = reserva_704ILR.FechaEvento_704ILR.Date != DateTime.Today;
                BLL_Bitacora_704ILR.Registrar_704ILR(Modulo_704ILR, "Inicio de ejecucion",
                    fueraDeFecha_704ILR ? CriticidadBitacora_704ILR.Advertencia : CriticidadBitacora_704ILR.Info,
                    $"Reserva #{reservaId_704ILR}: el evento pasa a EN_EJECUCION" +
                    (fueraDeFecha_704ILR ? $" fuera de su fecha ({FechaBitacora_704ILR(reserva_704ILR.FechaEvento_704ILR)})." : "."));
            }
            else if (resultado_704ILR == CoordinacionResult_704ILR.NoListo_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(Modulo_704ILR, "Ejecucion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: el evento no esta LISTO (falta personal confirmado o el cronograma) (RN-13).");
            return resultado_704ILR;
        }

        // Cierra el evento: tiene que estar en ejecucion y no puede quedar ninguna
        // incidencia abierta (RN-13). CERRADO es terminal.
        public static CoordinacionResult_704ILR CerrarEvento_704ILR(int reservaId_704ILR)
        {
            CoordinacionResult_704ILR resultado_704ILR;
            int abiertas_704ILR = 0;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = AbrirEnEjecucion_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out _);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        abiertas_704ILR = DAL_Incidencia_704ILR.Abiertas_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
                        if (abiertas_704ILR > 0)
                            resultado_704ILR = CoordinacionResult_704ILR.IncidenciasAbiertas_704ILR;
                        else
                        {
                            DAL_Coordinacion_704ILR.SetEstado_704ILR(reservaId_704ILR, EstadoCoordinacion_704ILR.CERRADO, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(Modulo_704ILR, "Cierre de evento", CriticidadBitacora_704ILR.Info,
                    $"Reserva #{reservaId_704ILR}: el evento pasa a CERRADO.");
            else if (resultado_704ILR == CoordinacionResult_704ILR.IncidenciasAbiertas_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(Modulo_704ILR, "Cierre rechazado", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: no se cierra con {abiertas_704ILR} incidencia(s) abierta(s) (RN-13).");
            return resultado_704ILR;
        }

        // Abre el evento para operar durante su ejecucion (incidencias y cierre): lee
        // la cabecera con bloqueo y exige que este EN_EJECUCION.
        internal static CoordinacionResult_704ILR AbrirEnEjecucion_704ILR(int reservaId_704ILR,
            SqlConnection conn_704ILR, SqlTransaction tx_704ILR, out BE_Reserva_704ILR reserva_704ILR)
        {
            reserva_704ILR = reservaId_704ILR <= 0 ? null : DAL_Reserva_704ILR.GetById_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR);
            if (reserva_704ILR == null) return CoordinacionResult_704ILR.ReservaInvalida_704ILR;
            CoordinacionResult_704ILR resultado_704ILR = CoordinacionResult_704ILR.Success_704ILR;
            if (reserva_704ILR.Estado_704ILR != EstadoReserva_704ILR.CONFIRMADA) resultado_704ILR = CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR;
            else if (reserva_704ILR.EstadoCoordinacion_704ILR == EstadoCoordinacion_704ILR.CERRADO) resultado_704ILR = CoordinacionResult_704ILR.EventoCerrado_704ILR;
            else if (reserva_704ILR.EstadoCoordinacion_704ILR != EstadoCoordinacion_704ILR.EN_EJECUCION) resultado_704ILR = CoordinacionResult_704ILR.NoEnEjecucion_704ILR;
            AsentarRechazoDeRegla_704ILR(reserva_704ILR, resultado_704ILR);
            return resultado_704ILR;
        }

        // ---------------------------------------------------------------
        // Formatos del detalle de la bitacora: fijos, sin depender de la configuracion
        // regional de la estacion (mismo criterio que BLL_Reserva_704ILR).
        internal static string FechaBitacora_704ILR(DateTime fecha_704ILR)
            => fecha_704ILR.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        internal static string HoraBitacora_704ILR(TimeSpan hora_704ILR)
            => hora_704ILR.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        internal static string FranjaBitacora_704ILR(TimeSpan desde_704ILR, TimeSpan hasta_704ILR)
            => HoraBitacora_704ILR(desde_704ILR) + "-" + HoraBitacora_704ILR(hasta_704ILR);

        // Una hora se guarda al minuto (las columnas son TIME(0) y la pantalla muestra
        // HH:mm): los segundos que traiga el selector se descartan antes de comparar.
        internal static TimeSpan AlMinuto_704ILR(TimeSpan hora_704ILR)
            => new TimeSpan(hora_704ILR.Hours, hora_704ILR.Minutes, 0);

        // True si la hora es una hora del dia (entre 00:00 y 23:59).
        internal static bool HoraDelDia_704ILR(TimeSpan hora_704ILR)
            => hora_704ILR >= TimeSpan.Zero && hora_704ILR < TimeSpan.FromDays(1);
    }
}
