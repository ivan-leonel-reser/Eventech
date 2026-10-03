using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    // Incidencias de la ejecucion del evento (CUN011). Ver BLL_Coordinacion_704ILR
    // para las reglas comunes y la serializacion.
    public static class BLL_Incidencia_704ILR
    {
        // Anchos de dbo.Incidencias. Publicos: la pantalla los usa para limitar lo tipeado.
        public const int MaxDescripcion_704ILR = 500;
        public const int MaxResolucion_704ILR = 250;

        public static List<BE_Incidencia_704ILR> GetByReserva_704ILR(int reservaId_704ILR)
            => DAL_Incidencia_704ILR.GetByReserva_704ILR(reservaId_704ILR);

        // CUN011 — Registra lo que se sale del plan durante la ejecucion. Solo se
        // registran incidencias con el evento EN_EJECUCION (RN-13). Quien la informo
        // es opcional; si se indica, tiene que ser un integrante del equipo del evento.
        public static CoordinacionResult_704ILR Registrar_704ILR(BE_Incidencia_704ILR incidencia_704ILR, out int nuevoId_704ILR)
        {
            nuevoId_704ILR = 0;
            if (incidencia_704ILR == null) return CoordinacionResult_704ILR.DescripcionInvalida_704ILR;
            incidencia_704ILR.Descripcion_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(incidencia_704ILR.Descripcion_704ILR);
            if (incidencia_704ILR.Descripcion_704ILR.Length == 0 || incidencia_704ILR.Descripcion_704ILR.Length > MaxDescripcion_704ILR)
                return CoordinacionResult_704ILR.DescripcionInvalida_704ILR;
            if (!Enum.IsDefined(typeof(TipoIncidencia_704ILR), incidencia_704ILR.Tipo_704ILR))
                incidencia_704ILR.Tipo_704ILR = TipoIncidencia_704ILR.OTRO;

            CoordinacionResult_704ILR resultado_704ILR;
            int reservaId_704ILR = incidencia_704ILR.ReservaId_704ILR;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirEnEjecucion_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out _);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        if (incidencia_704ILR.EmpleadoReportaId_704ILR.HasValue &&
                            DAL_AsignacionPersonal_704ILR.GetDeEmpleadoEnReserva_704ILR(reservaId_704ILR, incidencia_704ILR.EmpleadoReportaId_704ILR.Value, conn_704ILR, tx_704ILR) == null)
                            resultado_704ILR = CoordinacionResult_704ILR.EmpleadoInvalido_704ILR;
                        else
                        {
                            nuevoId_704ILR = DAL_Incidencia_704ILR.Insert_704ILR(incidencia_704ILR, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            // Una incidencia es un desvio del plan: informacion de gestion, se asienta
            // como Advertencia.
            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Registro de incidencia", CriticidadBitacora_704ILR.Advertencia,
                    $"Reserva #{reservaId_704ILR}: incidencia #{nuevoId_704ILR} ({incidencia_704ILR.Tipo_704ILR}): {incidencia_704ILR.Descripcion_704ILR}");
            return resultado_704ILR;
        }

        // Da por resuelta una incidencia, dejando como se resolvio. Tambien se resuelve
        // con el evento en ejecucion: cerrarlo exige que no quede ninguna abierta.
        public static CoordinacionResult_704ILR Resolver_704ILR(int incidenciaId_704ILR, string resolucion_704ILR)
        {
            resolucion_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(resolucion_704ILR);
            if (resolucion_704ILR.Length == 0) return CoordinacionResult_704ILR.ResolucionObligatoria_704ILR;
            if (resolucion_704ILR.Length > MaxResolucion_704ILR) resolucion_704ILR = resolucion_704ILR.Substring(0, MaxResolucion_704ILR);

            BE_Incidencia_704ILR incidencia_704ILR = incidenciaId_704ILR <= 0 ? null : DAL_Incidencia_704ILR.GetById_704ILR(incidenciaId_704ILR);
            if (incidencia_704ILR == null) return CoordinacionResult_704ILR.IncidenciaInvalida_704ILR;

            CoordinacionResult_704ILR resultado_704ILR;
            int reservaId_704ILR = incidencia_704ILR.ReservaId_704ILR;
            using (var cn_704ILR = new DAL_DB_Connection_704ILR())
            {
                SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                {
                    resultado_704ILR = BLL_Coordinacion_704ILR.AbrirEnEjecucion_704ILR(reservaId_704ILR, conn_704ILR, tx_704ILR, out _);
                    if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                    {
                        // Se relee con la cabecera bloqueada: otra sesion pudo resolverla.
                        incidencia_704ILR = DAL_Incidencia_704ILR.GetById_704ILR(incidenciaId_704ILR, conn_704ILR, tx_704ILR);
                        if (incidencia_704ILR == null)
                            resultado_704ILR = CoordinacionResult_704ILR.IncidenciaInvalida_704ILR;
                        else if (incidencia_704ILR.Estado_704ILR == EstadoIncidencia_704ILR.RESUELTA)
                            resultado_704ILR = CoordinacionResult_704ILR.IncidenciaYaResuelta_704ILR;
                        else
                        {
                            DAL_Incidencia_704ILR.Resolver_704ILR(incidenciaId_704ILR, resolucion_704ILR, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }

            if (resultado_704ILR == CoordinacionResult_704ILR.Success_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR(BLL_Coordinacion_704ILR.Modulo_704ILR, "Resolucion de incidencia", CriticidadBitacora_704ILR.Info,
                    $"Reserva #{reservaId_704ILR}: incidencia #{incidenciaId_704ILR} resuelta: {resolucion_704ILR}");
            return resultado_704ILR;
        }
    }
}
