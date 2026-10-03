using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Asignacion de personal a un evento (CUN006) y respuesta del empleado (CUN007).
    // Ver BLL_Coordinacion_704ILR para las reglas comunes y la serializacion.
    public static class BLL_AsignacionPersonal_704ILR
    {
        // Anchos de dbo.AsignacionesPersonal.
        private const int MaxRol_704ILR = 60;
        private const int MaxMotivo_704ILR = 250;

        public static List<BE_AsignacionPersonal_704ILR> GetByReserva_704ILR(int reservaId_704ILR)
            => DAL_AsignacionPersonal_704ILR.GetByReserva_704ILR(reservaId_704ILR);

        // Agenda del empleado de la sesion: sus asignaciones en eventos confirmados.
        // Una cuenta que no representa a ningun empleado no tiene agenda.
        public static List<BE_AsignacionPersonal_704ILR> GetMisAsignaciones_704ILR()
        {
            BE_Empleado_704ILR empleado_704ILR = BLL_Empleado_704ILR.GetDeLaSesion_704ILR();
            return empleado_704ILR == null
                ? new List<BE_AsignacionPersonal_704ILR>()
                : DAL_AsignacionPersonal_704ILR.GetDeEmpleado_704ILR(empleado_704ILR.Id_704ILR);
        }

        // CUN006 — Asigna un empleado al evento con su rol y su franja de trabajo. La
        // asignacion nace PENDIENTE: la confirma o la rechaza el propio empleado.
        //
        // RN-09 — Superposicion. Una misma persona puede estar tomada por otro evento
        // del mismo dia: la franja no puede pisarse con otra asignacion suya, pendiente
        // o confirmada, en otro evento confirmado. Las franjas se comparan como
        // intervalos reales sobre la fecha de cada evento, asi un turno que cruza la
        // medianoche choca con el del dia siguiente. En 'conflicto' vuelve la
        // asignacion con la que se pisa, para que la pantalla la nombre.
        //
        // Si el empleado habia rechazado el turno de este evento, asignarlo de nuevo le
        // vuelve a ofrecer el turno (la misma asignacion queda PENDIENTE).
        public static CoordinacionResult_704ILR Asignar_704ILR(int reservaId_704ILR, int empleadoId_704ILR, string rol_704ILR,
            TimeSpan horaInicio_704ILR, TimeSpan horaFin_704ILR, out int asignacionId_704ILR, out BE_AsignacionPersonal_704ILR conflicto_704ILR)
        {
            asignacionId_704ILR = 0;
            conflicto_704ILR = null;

            rol_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(rol_704ILR);
            if (rol_704ILR.Length == 0 || rol_704ILR.Length > MaxRol_704ILR) return CoordinacionResult_704ILR.RolInvalido_704ILR;
            if (!BLL_Coordinacion_704ILR.HoraDelDia_704ILR(horaInicio_704ILR) || !BLL_Coordinacion_704ILR.HoraDelDia_704ILR(horaFin_704ILR))
                return CoordinacionResult_704ILR.FranjaInvalida_704ILR;
            horaInicio_704ILR = BLL_Coordinacion_704ILR.AlMinuto_704ILR(horaInicio_704ILR);
            horaFin_704ILR = BLL_Coordinacion_704ILR.AlMinuto_704ILR(horaFin_704ILR);
            if (horaInicio_704ILR == horaFin_704ILR) return CoordinacionResult_704ILR.FranjaInvalida_704ILR;

            CoordinacionResult_704ILR resultado_704ILR;
            BE_Empleado_704ILR empleado_704ILR = null;
            bool reactivada_704ILR = false;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirParaPlanificar_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out BE_Reserva_704ILR reserva_704ILR);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        // La ficha del empleado se lee con bloqueo: dos asignaciones
                        // simultaneas del mismo empleado validan una detras de la otra.
                        empleado_704ILR = empleadoId_704ILR <= 0 ? null : DAL_Empleado_704ILR.GetById_704ILR(empleadoId_704ILR, conn_704ILR, tx_704ILR);
                        BE_AsignacionPersonal_704ILR existente_704ILR = empleado_704ILR == null ? null
                            : DAL_AsignacionPersonal_704ILR.GetDeEmpleadoEnReserva_704ILR(reservaId_704ILR, empleadoId_704ILR, conn_704ILR, tx_704ILR);
                        var pedida_704ILR = new BE_AsignacionPersonal_704ILR
                        {
                            ReservaId_704ILR = reservaId_704ILR,
                            EmpleadoId_704ILR = empleadoId_704ILR,
                            RolAsignado_704ILR = rol_704ILR,
                            HoraInicio_704ILR = horaInicio_704ILR,
                            HoraFin_704ILR = horaFin_704ILR,
                            FechaEvento_704ILR = reserva_704ILR.FechaEvento_704ILR
                        };

                        if (empleado_704ILR == null || !empleado_704ILR.Activo_704ILR)
                            resultado_704ILR = CoordinacionResult_704ILR.EmpleadoInvalido_704ILR;
                        else if (existente_704ILR != null && existente_704ILR.Estado_704ILR != EstadoAsignacion_704ILR.RECHAZADA)
                            resultado_704ILR = CoordinacionResult_704ILR.YaAsignado_704ILR;
                        else if ((conflicto_704ILR = PrimeraSuperpuesta_704ILR(pedida_704ILR, false, conn_704ILR, tx_704ILR)) != null)
                            resultado_704ILR = CoordinacionResult_704ILR.Superposicion_704ILR;
                        // Volver a ofrecer el turno con otra franja no puede dejar afuera las
                        // tareas que el empleado ya tenia en este evento (RN-12).
                        else if (existente_704ILR != null && !BLL_Tarea_704ILR.TareasDentroDeFranja_704ILR(pedida_704ILR, conn_704ILR, tx_704ILR))
                            resultado_704ILR = CoordinacionResult_704ILR.TieneCarga_704ILR;
                        else
                        {
                            if (existente_704ILR != null)
                            {
                                DAL_AsignacionPersonal_704ILR.Reactivar_704ILR(existente_704ILR.Id_704ILR, rol_704ILR, horaInicio_704ILR, horaFin_704ILR, conn_704ILR, tx_704ILR);
                                asignacionId_704ILR = existente_704ILR.Id_704ILR;
                                reactivada_704ILR = true;
                            }
                            else
                                asignacionId_704ILR = DAL_AsignacionPersonal_704ILR.Insert_704ILR(pedida_704ILR, conn_704ILR, tx_704ILR);
                            BLL_Coordinacion_704ILR.Recalcular_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            // Los asientos van despues de cerrar la transaccion. Se asientan el alta y los
            // rechazos por regla de negocio (la superposicion, RN-09, y el turno que se
            // vuelve a ofrecer dejando afuera las tareas del empleado, RN-12); los errores
            // de tipeo se corrigen en pantalla y no dejan asiento.
            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Asignacion de personal", CriticidadBitacora_704ILR.Info,
                    $"Reserva #{reservaId_704ILR}: {Persona_704ILR(empleado_704ILR)} asignado como '{rol_704ILR}' " +
                    $"de {BLL_Coordinacion_704ILR.FranjaBitacora_704ILR(horaInicio_704ILR, horaFin_704ILR)}" +
                    (reactivada_704ILR ? " (se le vuelve a ofrecer el turno que habia rechazado)." : "."));
            else if (resultado_704ILR == CoordinacionResult_704ILR.Superposicion_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Asignacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: {Persona_704ILR(empleado_704ILR)} no se asigna de " +
                    $"{BLL_Coordinacion_704ILR.FranjaBitacora_704ILR(horaInicio_704ILR, horaFin_704ILR)}, se superpone con su turno " +
                    $"{DescribirTurno_704ILR(conflicto_704ILR)} (RN-09).");
            else if (resultado_704ILR == CoordinacionResult_704ILR.TieneCarga_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Asignacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: a {Persona_704ILR(empleado_704ILR)} no se le vuelve a ofrecer el turno de " +
                    $"{BLL_Coordinacion_704ILR.FranjaBitacora_704ILR(horaInicio_704ILR, horaFin_704ILR)}, sus tareas en el evento " +
                    "quedarian fuera de esa franja (RN-12).");
            return resultado_704ILR;
        }

        // Quita a un empleado del equipo del evento. No se quita a quien tiene un tramo
        // del cronograma a cargo o tareas en el evento: primero se reasignan.
        public static CoordinacionResult_704ILR Quitar_704ILR(int asignacionId_704ILR)
        {
            BE_AsignacionPersonal_704ILR asignacion_704ILR = asignacionId_704ILR <= 0 ? null : DAL_AsignacionPersonal_704ILR.GetById_704ILR(asignacionId_704ILR);
            if (asignacion_704ILR == null) return CoordinacionResult_704ILR.AsignacionInvalida_704ILR;

            CoordinacionResult_704ILR resultado_704ILR;
            int reservaId_704ILR = asignacion_704ILR.ReservaId_704ILR;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirParaPlanificar_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out BE_Reserva_704ILR reserva_704ILR);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        // Se relee con la cabecera bloqueada: otra sesion pudo quitarla.
                        asignacion_704ILR = DAL_AsignacionPersonal_704ILR.GetById_704ILR(asignacionId_704ILR, conn_704ILR, tx_704ILR);
                        if (asignacion_704ILR == null)
                            resultado_704ILR = CoordinacionResult_704ILR.AsignacionInvalida_704ILR;
                        else if (DAL_Cronograma_704ILR.ActividadesACargo_704ILR(reservaId_704ILR, asignacion_704ILR.EmpleadoId_704ILR, conn_704ILR, tx_704ILR) > 0
                              || DAL_Tarea_704ILR.Contar_704ILR(reservaId_704ILR, asignacion_704ILR.EmpleadoId_704ILR, conn_704ILR, tx_704ILR) > 0)
                            resultado_704ILR = CoordinacionResult_704ILR.TieneCarga_704ILR;
                        else if (DAL_AsignacionPersonal_704ILR.Delete_704ILR(asignacionId_704ILR, conn_704ILR, tx_704ILR) == 0)
                            resultado_704ILR = CoordinacionResult_704ILR.AsignacionInvalida_704ILR;
                        else
                        {
                            BLL_Coordinacion_704ILR.Recalcular_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            // Quitar a quien ya habia confirmado deja al evento sin un integrante con el
            // que se contaba: se asienta como Advertencia.
            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Baja de asignacion",
                    asignacion_704ILR.Estado_704ILR == EstadoAsignacion_704ILR.CONFIRMADA ? CriticidadBitacora_704ILR.Advertencia : CriticidadBitacora_704ILR.Info,
                    $"Reserva #{reservaId_704ILR}: se quita a {asignacion_704ILR.EmpleadoNombre_704ILR} (#{asignacion_704ILR.EmpleadoId_704ILR}), " +
                    $"asignacion {asignacion_704ILR.Estado_704ILR}.");
            else if (resultado_704ILR == CoordinacionResult_704ILR.TieneCarga_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Asignacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: no se quita a {asignacion_704ILR.EmpleadoNombre_704ILR} (#{asignacion_704ILR.EmpleadoId_704ILR}), " +
                    "tiene actividades del cronograma a cargo o tareas asignadas.");
            return resultado_704ILR;
        }

        // CUN007 — El empleado acepta el turno. RN-10: responde el propio empleado
        // (la cuenta de la sesion tiene que ser la suya). Al aceptar se vuelve a
        // controlar la RN-09 contra los turnos que ya tiene confirmados: la fecha del
        // evento pudo cambiar despues de la asignacion.
        public static CoordinacionResult_704ILR Confirmar_704ILR(int asignacionId_704ILR, out BE_AsignacionPersonal_704ILR conflicto_704ILR)
            => Responder_704ILR(asignacionId_704ILR, true, null, out conflicto_704ILR);

        // CUN007 — El empleado rechaza el turno dejando el motivo (RN-10). La asignacion
        // vuelve al coordinador, que busca a otra persona de la misma especialidad.
        public static CoordinacionResult_704ILR Rechazar_704ILR(int asignacionId_704ILR, string motivo_704ILR)
            => Responder_704ILR(asignacionId_704ILR, false, motivo_704ILR, out _);

        private static CoordinacionResult_704ILR Responder_704ILR(int asignacionId_704ILR, bool acepta_704ILR, string motivo_704ILR,
            out BE_AsignacionPersonal_704ILR conflicto_704ILR)
        {
            conflicto_704ILR = null;
            BE_Empleado_704ILR deLaSesion_704ILR = BLL_Empleado_704ILR.GetDeLaSesion_704ILR();
            if (deLaSesion_704ILR == null)
            {
                // RN-10: la agenda de una cuenta sin ficha no muestra turnos, asi que la
                // respuesta solo llega si la cuenta se desvinculo con la pantalla abierta
                // o por fuera de ella: queda asentada, como la respuesta por otro empleado.
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Respuesta rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Asignacion #{asignacionId_704ILR}: la cuenta de la sesion no esta vinculada a ningun empleado; solo el empleado del turno puede responderlo (RN-10).");
                return CoordinacionResult_704ILR.SinEmpleadoVinculado_704ILR;
            }

            if (!acepta_704ILR)
            {
                motivo_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(motivo_704ILR);
                if (motivo_704ILR.Length == 0) return CoordinacionResult_704ILR.MotivoObligatorio_704ILR;
                if (motivo_704ILR.Length > MaxMotivo_704ILR) motivo_704ILR = motivo_704ILR.Substring(0, MaxMotivo_704ILR);
            }

            BE_AsignacionPersonal_704ILR asignacion_704ILR = asignacionId_704ILR <= 0 ? null : DAL_AsignacionPersonal_704ILR.GetById_704ILR(asignacionId_704ILR);
            if (asignacion_704ILR == null) return CoordinacionResult_704ILR.AsignacionInvalida_704ILR;

            CoordinacionResult_704ILR resultado_704ILR;
            int reservaId_704ILR = asignacion_704ILR.ReservaId_704ILR;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirParaPlanificar_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out BE_Reserva_704ILR reserva_704ILR);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        // Mismo orden de bloqueos que Asignar: cabecera y despues empleado.
                        // La identidad (RN-10) se decide con la ficha leida bajo ese
                        // bloqueo: la cuenta de la sesion tiene que seguir vinculada al
                        // empleado de la asignacion, y su ficha tiene que estar activa.
                        BE_Empleado_704ILR titular_704ILR = DAL_Empleado_704ILR.GetById_704ILR(asignacion_704ILR.EmpleadoId_704ILR, conn_704ILR, tx_704ILR);
                        asignacion_704ILR = DAL_AsignacionPersonal_704ILR.GetById_704ILR(asignacionId_704ILR, conn_704ILR, tx_704ILR);
                        if (asignacion_704ILR == null)
                            resultado_704ILR = CoordinacionResult_704ILR.AsignacionInvalida_704ILR;
                        else if (titular_704ILR == null || titular_704ILR.Id_704ILR != deLaSesion_704ILR.Id_704ILR
                                 || titular_704ILR.UserId_704ILR != deLaSesion_704ILR.UserId_704ILR)
                            resultado_704ILR = CoordinacionResult_704ILR.NoEsElEmpleado_704ILR;
                        else if (!titular_704ILR.Activo_704ILR)
                            resultado_704ILR = CoordinacionResult_704ILR.EmpleadoDeBaja_704ILR;
                        else if (asignacion_704ILR.Estado_704ILR != EstadoAsignacion_704ILR.PENDIENTE)
                            resultado_704ILR = CoordinacionResult_704ILR.AsignacionYaRespondida_704ILR;
                        else if (acepta_704ILR && (conflicto_704ILR = PrimeraSuperpuesta_704ILR(asignacion_704ILR, true, conn_704ILR, tx_704ILR)) != null)
                            resultado_704ILR = CoordinacionResult_704ILR.Superposicion_704ILR;
                        else
                        {
                            DAL_AsignacionPersonal_704ILR.Responder_704ILR(asignacionId_704ILR,
                                acepta_704ILR ? EstadoAsignacion_704ILR.CONFIRMADA : EstadoAsignacion_704ILR.RECHAZADA,
                                acepta_704ILR ? null : motivo_704ILR, conn_704ILR, tx_704ILR);
                            BLL_Coordinacion_704ILR.Recalcular_704ILR(reserva_704ILR, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            switch (resultado_704ILR)
            {
                case CoordinacionResult_704ILR.Success_704ILR:
                    if (acepta_704ILR)
                        BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Disponibilidad confirmada", CriticidadBitacora_704ILR.Info,
                            $"Reserva #{reservaId_704ILR}: {asignacion_704ILR.EmpleadoNombre_704ILR} (#{asignacion_704ILR.EmpleadoId_704ILR}) confirma su turno " +
                            $"de {BLL_Coordinacion_704ILR.FranjaBitacora_704ILR(asignacion_704ILR.HoraInicio_704ILR, asignacion_704ILR.HoraFin_704ILR)}.");
                    else
                        BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Turno rechazado", CriticidadBitacora_704ILR.Advertencia,
                            $"Reserva #{reservaId_704ILR}: {asignacion_704ILR.EmpleadoNombre_704ILR} (#{asignacion_704ILR.EmpleadoId_704ILR}) rechaza su turno. Motivo: {motivo_704ILR}");
                    break;
                // Responder por otra persona no es un error de tipeo: no hay pantalla que
                // lo ofrezca, asi que queda asentado.
                case CoordinacionResult_704ILR.NoEsElEmpleado_704ILR:
                    BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Respuesta rechazada", CriticidadBitacora_704ILR.Advertencia,
                        $"Reserva #{reservaId_704ILR}: la asignacion #{asignacionId_704ILR} es de otro empleado; solo el puede responderla (RN-10).");
                    break;
                case CoordinacionResult_704ILR.EmpleadoDeBaja_704ILR:
                    BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Respuesta rechazada", CriticidadBitacora_704ILR.Advertencia,
                        $"Reserva #{reservaId_704ILR}: {asignacion_704ILR.EmpleadoNombre_704ILR} (#{asignacion_704ILR.EmpleadoId_704ILR}) esta dado de baja y no responde turnos.");
                    break;
                case CoordinacionResult_704ILR.Superposicion_704ILR:
                    BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Respuesta rechazada", CriticidadBitacora_704ILR.Advertencia,
                        $"Reserva #{reservaId_704ILR}: {asignacion_704ILR.EmpleadoNombre_704ILR} (#{asignacion_704ILR.EmpleadoId_704ILR}) no puede confirmar, " +
                        $"el turno se superpone con su turno {DescribirTurno_704ILR(conflicto_704ILR)} (RN-09).");
                    break;
            }
            return resultado_704ILR;
        }

        // RN-09: primera asignacion del empleado, en otro evento confirmado, cuya franja
        // se pisa con la pedida; null si no hay ninguna. Se consultan los eventos del dia
        // anterior, del mismo dia y del siguiente, porque una franja puede cruzar la
        // medianoche. Con soloConfirmadas se comparan solo los turnos que el empleado ya
        // acepto (es lo que corresponde al confirmar: dos turnos pendientes que se pisan
        // se resuelven aceptando uno y rechazando el otro).
        private static BE_AsignacionPersonal_704ILR PrimeraSuperpuesta_704ILR(BE_AsignacionPersonal_704ILR pedida_704ILR,
            bool soloConfirmadas_704ILR, SqlConnection conn_704ILR, SqlTransaction tx_704ILR)
        {
            DateTime fecha_704ILR = pedida_704ILR.FechaEvento_704ILR.Date;
            // AddDays sobre los extremos del calendario lanzaria; la fecha del evento
            // esta acotada por BLL_Reserva (hasta 9998-12-31) y nunca es la minima.
            DateTime desde_704ILR = fecha_704ILR > DateTime.MinValue.AddDays(1) ? fecha_704ILR.AddDays(-1) : fecha_704ILR;
            DateTime hasta_704ILR = fecha_704ILR < DateTime.MaxValue.Date.AddDays(-1) ? fecha_704ILR.AddDays(1) : fecha_704ILR;
            foreach (var otra_704ILR in DAL_AsignacionPersonal_704ILR.GetComprometidas_704ILR(pedida_704ILR.EmpleadoId_704ILR,
                         desde_704ILR, hasta_704ILR, pedida_704ILR.ReservaId_704ILR, soloConfirmadas_704ILR, conn_704ILR, tx_704ILR))
                if (pedida_704ILR.SeSuperponeCon_704ILR(otra_704ILR)) return otra_704ILR;
            return null;
        }

        private static string Persona_704ILR(BE_Empleado_704ILR empleado_704ILR)
            => empleado_704ILR == null ? "empleado" : $"{empleado_704ILR.NombreCompleto_704ILR} (#{empleado_704ILR.Id_704ILR})";

        private static string DescribirTurno_704ILR(BE_AsignacionPersonal_704ILR turno_704ILR)
            => turno_704ILR == null ? string.Empty
                : $"de la reserva #{turno_704ILR.ReservaId_704ILR} ({BLL_Coordinacion_704ILR.FechaBitacora_704ILR(turno_704ILR.FechaEvento_704ILR)} " +
                  $"{BLL_Coordinacion_704ILR.FranjaBitacora_704ILR(turno_704ILR.HoraInicio_704ILR, turno_704ILR.HoraFin_704ILR)})";
    }
}
