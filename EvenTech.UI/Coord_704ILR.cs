using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using EvenTech.BE;
using EvenTech.BLL;

namespace EvenTech.UI
{
    // Textos y formatos comunes de las pantallas del Proceso 2 (operaciones, personal,
    // cronograma, tareas, supervision y agenda): leyendas traducidas de los estados,
    // formato fijo de horas y fechas y mensaje de cada rechazo de la capa de negocio.
    // Centralizado para que las seis pantallas digan lo mismo ante el mismo resultado.
    internal static class Coord_704ILR
    {
        // Traduccion con respaldo: si la clave no existe se usa el texto por defecto.
        public static string T_704ILR(string clave_704ILR, string defecto_704ILR)
        {
            string t_704ILR = Tr_704ILR.T_704ILR(clave_704ILR);
            return t_704ILR == clave_704ILR ? defecto_704ILR : t_704ILR;
        }

        // ---------- Formatos fijos (no dependen de la cultura de la estacion) ----------
        public static string Hora_704ILR(TimeSpan hora_704ILR) => hora_704ILR.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        public static string Franja_704ILR(TimeSpan desde_704ILR, TimeSpan hasta_704ILR) => Hora_704ILR(desde_704ILR) + " - " + Hora_704ILR(hasta_704ILR);

        public static string Fecha_704ILR(DateTime fecha_704ILR) => fecha_704ILR.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static string FechaHora_704ILR(DateTime fecha_704ILR) => fecha_704ILR.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        // ---------- Leyendas de los estados ----------
        public static string Estado_704ILR(EstadoCoordinacion_704ILR estado_704ILR)
        {
            switch (estado_704ILR)
            {
                case EstadoCoordinacion_704ILR.SIN_ASIGNAR:     return T_704ILR("COORD_SIN_ASIGNAR", "Sin asignar");
                case EstadoCoordinacion_704ILR.EN_COORDINACION: return T_704ILR("COORD_EN_COORDINACION", "En coordinación");
                case EstadoCoordinacion_704ILR.LISTO:           return T_704ILR("COORD_LISTO", "Listo");
                case EstadoCoordinacion_704ILR.EN_EJECUCION:    return T_704ILR("COORD_EN_EJECUCION", "En ejecución");
                case EstadoCoordinacion_704ILR.CERRADO:         return T_704ILR("COORD_CERRADO", "Cerrado");
                default:                                        return T_704ILR("EST_DESCONOCIDO", "(estado desconocido)");
            }
        }

        public static Color Color_704ILR(EstadoCoordinacion_704ILR estado_704ILR)
        {
            switch (estado_704ILR)
            {
                case EstadoCoordinacion_704ILR.LISTO:           return Theme_704ILR.Success_704ILR;
                case EstadoCoordinacion_704ILR.EN_EJECUCION:    return Theme_704ILR.AccentButton_704ILR;
                case EstadoCoordinacion_704ILR.EN_COORDINACION: return Theme_704ILR.Warning_704ILR;
                case EstadoCoordinacion_704ILR.CERRADO:         return Theme_704ILR.TextMuted_704ILR;
                default:                                        return Theme_704ILR.TextOnLight_704ILR;
            }
        }

        public static string Asignacion_704ILR(EstadoAsignacion_704ILR estado_704ILR)
        {
            switch (estado_704ILR)
            {
                case EstadoAsignacion_704ILR.PENDIENTE:  return T_704ILR("ASIG_PENDIENTE", "Pendiente");
                case EstadoAsignacion_704ILR.CONFIRMADA: return T_704ILR("ASIG_CONFIRMADA", "Confirmada");
                case EstadoAsignacion_704ILR.RECHAZADA:  return T_704ILR("ASIG_RECHAZADA", "Rechazada");
                default:                                 return T_704ILR("EST_DESCONOCIDO", "(estado desconocido)");
            }
        }

        public static Color ColorAsignacion_704ILR(EstadoAsignacion_704ILR estado_704ILR)
        {
            switch (estado_704ILR)
            {
                case EstadoAsignacion_704ILR.CONFIRMADA: return Theme_704ILR.Success_704ILR;
                case EstadoAsignacion_704ILR.RECHAZADA:  return Theme_704ILR.Error_704ILR;
                default:                                 return Theme_704ILR.Warning_704ILR;
            }
        }

        public static string Prioridad_704ILR(PrioridadTarea_704ILR prioridad_704ILR)
        {
            switch (prioridad_704ILR)
            {
                case PrioridadTarea_704ILR.ALTA: return T_704ILR("PRIO_ALTA", "Alta");
                case PrioridadTarea_704ILR.BAJA: return T_704ILR("PRIO_BAJA", "Baja");
                default:                         return T_704ILR("PRIO_MEDIA", "Media");
            }
        }

        public static string TextoTipo_704ILR(TipoIncidencia_704ILR tipo_704ILR)
        {
            switch (tipo_704ILR)
            {
                case TipoIncidencia_704ILR.PERSONAL:     return T_704ILR("INC_TIPO_PERSONAL", "Personal");
                case TipoIncidencia_704ILR.SERVICIO:     return T_704ILR("INC_TIPO_SERVICIO", "Servicio");
                case TipoIncidencia_704ILR.EQUIPAMIENTO: return T_704ILR("INC_TIPO_EQUIPAMIENTO", "Equipamiento");
                case TipoIncidencia_704ILR.HORARIO:      return T_704ILR("INC_TIPO_HORARIO", "Horario");
                case TipoIncidencia_704ILR.INVITADOS:    return T_704ILR("INC_TIPO_INVITADOS", "Invitados");
                default:                                 return T_704ILR("INC_TIPO_OTRO", "Otro");
            }
        }

        public static string TextoIncidencia_704ILR(EstadoIncidencia_704ILR estado_704ILR)
            => estado_704ILR == EstadoIncidencia_704ILR.RESUELTA
                ? T_704ILR("INC_EST_RESUELTA", "Resuelta")
                : T_704ILR("INC_EST_ABIERTA", "Abierta");

        // Nombre visible de una especialidad. El catalogo guarda el nombre en espanol y
        // la pantalla lo muestra por la clave ESP_<NOMBRE> (mayusculas, sin tildes, lo
        // que no es letra ni digito pasa a '_'), con el mismo criterio que los metodos
        // de pago. Una especialidad sin clave sembrada se muestra con su nombre tal cual.
        public static string Especialidad_704ILR(string nombre_704ILR)
        {
            if (string.IsNullOrWhiteSpace(nombre_704ILR)) return nombre_704ILR ?? string.Empty;
            var clave_704ILR = new StringBuilder("ESP_");
            bool separador_704ILR = false;
            foreach (char c_704ILR in nombre_704ILR.Trim().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c_704ILR) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c_704ILR)) { clave_704ILR.Append(char.ToUpperInvariant(c_704ILR)); separador_704ILR = false; }
                else if (!separador_704ILR) { clave_704ILR.Append('_'); separador_704ILR = true; }
            }
            string texto_704ILR = clave_704ILR.ToString().TrimEnd('_');
            if (texto_704ILR.Length > 60) texto_704ILR = texto_704ILR.Substring(0, 60);   // Traducciones.Clave es NVARCHAR(60)
            return T_704ILR(texto_704ILR, nombre_704ILR);
        }

        // Una linea que identifica al evento: "Reserva #12 · 2026-11-14 · Salon Principal · Ana Garcia · 180 invitados".
        public static string Evento_704ILR(BE_EventoCoordinacion_704ILR evento_704ILR)
        {
            if (evento_704ILR == null) return string.Empty;
            return Tr_704ILR.F_704ILR("COORD_EVENTO", "Reserva #{0} · {1} · {2} · {3} · {4} invitados",
                evento_704ILR.ReservaId_704ILR, Fecha_704ILR(evento_704ILR.FechaEvento_704ILR), evento_704ILR.SalonNombre_704ILR,
                evento_704ILR.ClienteNombre_704ILR, evento_704ILR.CantidadInvitados_704ILR);
        }

        // Aviso de superposicion (RN-09) nombrando el turno con el que se pisa.
        public static string Superposicion_704ILR(BE_AsignacionPersonal_704ILR conflicto_704ILR)
        {
            if (conflicto_704ILR == null) return Mensaje_704ILR(CoordinacionResult_704ILR.Superposicion_704ILR);
            return Tr_704ILR.F_704ILR("MSG_COORD_SUPERPOSICION_DET",
                "La franja se superpone con otro turno de {0}: reserva #{1}, {2}, de {3}.",
                conflicto_704ILR.EmpleadoNombre_704ILR, conflicto_704ILR.ReservaId_704ILR,
                Fecha_704ILR(conflicto_704ILR.FechaEvento_704ILR), Franja_704ILR(conflicto_704ILR.HoraInicio_704ILR, conflicto_704ILR.HoraFin_704ILR));
        }

        // Mensaje de cada rechazo de la capa de negocio. Los codigos de las reglas
        // quedan en los asientos de la bitacora, no en los avisos.
        public static string Mensaje_704ILR(CoordinacionResult_704ILR r_704ILR)
        {
            switch (r_704ILR)
            {
                case CoordinacionResult_704ILR.ReservaInvalida_704ILR:        return T_704ILR("MSG_RES_NOTFOUND", "La reserva ya no existe.");
                case CoordinacionResult_704ILR.ReservaNoConfirmada_704ILR:    return T_704ILR("MSG_COORD_NO_CONFIRMADA", "La reserva ya no está confirmada: solo se coordinan los eventos de reservas confirmadas.");
                case CoordinacionResult_704ILR.EventoEnEjecucion_704ILR:      return T_704ILR("MSG_COORD_EN_EJECUCION", "El evento está en ejecución: el plan ya no se modifica. Lo que se salga del plan se registra como incidencia.");
                case CoordinacionResult_704ILR.EventoCerrado_704ILR:          return T_704ILR("MSG_COORD_CERRADO", "El evento está cerrado: no admite cambios.");
                case CoordinacionResult_704ILR.EmpleadoInvalido_704ILR:       return T_704ILR("MSG_COORD_EMPLEADO", "Seleccione un empleado activo.");
                case CoordinacionResult_704ILR.EmpleadoDeBaja_704ILR:         return T_704ILR("MSG_COORD_EMPLEADO_BAJA", "Tu ficha de empleado está dada de baja: ya no podés responder turnos.");
                case CoordinacionResult_704ILR.RolInvalido_704ILR:            return T_704ILR("MSG_COORD_ROL", "Ingrese el rol que cumple el empleado en el evento (hasta 60 caracteres).");
                case CoordinacionResult_704ILR.FranjaInvalida_704ILR:         return T_704ILR("MSG_COORD_FRANJA", "La hora de fin tiene que ser distinta de la hora de inicio.");
                case CoordinacionResult_704ILR.Superposicion_704ILR:          return T_704ILR("MSG_COORD_SUPERPOSICION", "La franja se superpone con otro turno del empleado.");
                case CoordinacionResult_704ILR.YaAsignado_704ILR:             return T_704ILR("MSG_COORD_YA_ASIGNADO", "El empleado ya está asignado a este evento.");
                case CoordinacionResult_704ILR.AsignacionInvalida_704ILR:     return T_704ILR("MSG_COORD_ASIGNACION", "La asignación ya no existe.");
                case CoordinacionResult_704ILR.SinEmpleadoVinculado_704ILR:   return T_704ILR("MSG_COORD_SIN_EMPLEADO", "Tu cuenta no está vinculada a un empleado. Pedile a un coordinador que la vincule desde Empleados.");
                case CoordinacionResult_704ILR.NoEsElEmpleado_704ILR:         return T_704ILR("MSG_COORD_NO_ES_EL_EMPLEADO", "La asignación es de otro empleado: solo él puede responderla.");
                case CoordinacionResult_704ILR.AsignacionYaRespondida_704ILR: return T_704ILR("MSG_COORD_YA_RESPONDIDA", "La asignación ya fue respondida.");
                case CoordinacionResult_704ILR.MotivoObligatorio_704ILR:      return T_704ILR("MSG_COORD_MOTIVO", "Para rechazar el turno hay que indicar el motivo.");
                case CoordinacionResult_704ILR.TieneCarga_704ILR:             return T_704ILR("MSG_COORD_TIENE_CARGA", "El empleado tiene actividades del cronograma a cargo o tareas en este evento: reasígnelas antes.");
                case CoordinacionResult_704ILR.PersonalSinConfirmar_704ILR:   return T_704ILR("MSG_COORD_SIN_CONFIRMAR", "El cronograma se arma con el equipo confirmado: tiene que haber personal confirmado y ninguna respuesta pendiente.");
                case CoordinacionResult_704ILR.SinActividades_704ILR:         return T_704ILR("MSG_COORD_SIN_ACTIVIDADES", "Agregue al menos una actividad al cronograma.");
                case CoordinacionResult_704ILR.ActividadInvalida_704ILR:      return T_704ILR("MSG_COORD_ACTIVIDAD", "Cada actividad lleva una descripción (hasta 150 caracteres) y una duración de 1 a 1440 minutos.");
                case CoordinacionResult_704ILR.ResponsableInvalido_704ILR:    return T_704ILR("MSG_COORD_RESPONSABLE", "El empleado elegido no es personal confirmado de este evento.");
                case CoordinacionResult_704ILR.SinCronograma_704ILR:          return T_704ILR("MSG_COORD_SIN_CRONOGRAMA", "El evento todavía no tiene cronograma: genérelo primero.");
                case CoordinacionResult_704ILR.CronogramaConTareas_704ILR:    return T_704ILR("MSG_COORD_CRONO_CON_TAREAS", "El cronograma tiene tareas asignadas: quítelas antes de eliminarlo.");
                case CoordinacionResult_704ILR.DescripcionInvalida_704ILR:    return T_704ILR("MSG_COORD_DESCRIPCION", "Ingrese la descripción.");
                case CoordinacionResult_704ILR.FueraDeFranja_704ILR:          return T_704ILR("MSG_COORD_FUERA_FRANJA", "La tarea tiene que caer dentro del turno del empleado.");
                case CoordinacionResult_704ILR.TareaSuperpuesta_704ILR:       return T_704ILR("MSG_COORD_TAREA_SUPERPUESTA", "La tarea se superpone con otra tarea del mismo empleado.");
                case CoordinacionResult_704ILR.TareaInvalida_704ILR:          return T_704ILR("MSG_COORD_TAREA", "La tarea ya no existe.");
                case CoordinacionResult_704ILR.NoListo_704ILR:                return T_704ILR("MSG_COORD_NO_LISTO", "El evento todavía no está listo: falta que todo el personal confirme o falta el cronograma.");
                case CoordinacionResult_704ILR.NoEnEjecucion_704ILR:          return T_704ILR("MSG_COORD_NO_EN_EJECUCION", "El evento no está en ejecución.");
                case CoordinacionResult_704ILR.IncidenciaInvalida_704ILR:     return T_704ILR("MSG_COORD_INCIDENCIA", "La incidencia ya no existe.");
                case CoordinacionResult_704ILR.IncidenciaYaResuelta_704ILR:   return T_704ILR("MSG_COORD_INC_RESUELTA", "La incidencia ya estaba resuelta.");
                case CoordinacionResult_704ILR.ResolucionObligatoria_704ILR:  return T_704ILR("MSG_COORD_RESOLUCION", "Indique cómo se resolvió la incidencia.");
                case CoordinacionResult_704ILR.IncidenciasAbiertas_704ILR:    return T_704ILR("MSG_COORD_INC_ABIERTAS", "Quedan incidencias abiertas: resuélvalas antes de cerrar el evento.");
                default:                                                      return T_704ILR("MSG_OP_ERROR", "No se pudo completar la operación.");
            }
        }

        // Fila elegida de una grilla de seleccion por fila completa. Se toma de la
        // seleccion y no de la celda actual: cuando la celda actual se cambia por codigo,
        // la grilla avisa el cambio de seleccion ANTES de mover la celda actual, y quien
        // leyera CurrentRow en ese aviso veria todavia la fila anterior. Sin ninguna fila
        // resaltada (Ctrl+clic sobre la elegida la deselecciona) no hay fila elegida,
        // aunque la grilla conserve su celda actual: nada opera sobre lo que no se ve.
        public static DataGridViewRow Fila_704ILR(DataGridView grilla_704ILR)
        {
            if (grilla_704ILR == null) return null;
            if (grilla_704ILR.SelectedRows.Count > 0) return grilla_704ILR.SelectedRows[0];
            // Antes de mostrarse la grilla todavia no resalto ninguna fila.
            return grilla_704ILR.IsHandleCreated ? null : grilla_704ILR.CurrentRow;
        }

        // Las grillas que la pantalla rearma por codigo despues de cada operacion no se
        // ordenan por columna: el rearmado devolvia las filas al orden de lectura y el
        // encabezado conservaba la flecha de un orden que ya no era el que se veia.
        public static void SinOrden_704ILR(DataGridView grilla_704ILR)
        {
            foreach (DataGridViewColumn c_704ILR in grilla_704ILR.Columns) c_704ILR.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        // La lista desplegada de un combo se ensancha hasta su item mas largo, asi un
        // nombre con su rol o su turno se lee entero aunque el combo cerrado sea angosto.
        public static void AjustarDesplegable_704ILR(ComboBox cbo_704ILR)
        {
            int ancho_704ILR = cbo_704ILR.Width;
            foreach (object item_704ILR in cbo_704ILR.Items)
                ancho_704ILR = Math.Max(ancho_704ILR, TextRenderer.MeasureText(cbo_704ILR.GetItemText(item_704ILR), cbo_704ILR.Font).Width + SystemInformation.VerticalScrollBarWidth + 8);
            cbo_704ILR.DropDownWidth = ancho_704ILR;
        }

        // Un combo guarda el texto de cada item al cargarlo: para que muestre el idioma
        // nuevo hay que volver a cargar los mismos items. Se conserva la seleccion.
        public static void Retraducir_704ILR(ComboBox cbo_704ILR)
        {
            if (cbo_704ILR == null || cbo_704ILR.Items.Count == 0) return;
            int seleccion_704ILR = cbo_704ILR.SelectedIndex;
            var items_704ILR = new object[cbo_704ILR.Items.Count];
            cbo_704ILR.Items.CopyTo(items_704ILR, 0);
            cbo_704ILR.BeginUpdate();
            try
            {
                cbo_704ILR.Items.Clear();
                cbo_704ILR.Items.AddRange(items_704ILR);
                cbo_704ILR.SelectedIndex = seleccion_704ILR;
            }
            finally { cbo_704ILR.EndUpdate(); }
        }

        // Ancho de un boton segun su texto traducido, sin bajar del ancho de diseno y con
        // tope en 1,5 veces ese ancho (mismo criterio que el dialogo de pagos): un idioma
        // con rotulos mas largos ensancha el boton en vez de recortar el texto.
        public static void AjustarAncho_704ILR(AppButton_704ILR boton_704ILR, int minimo_704ILR)
        {
            int glifo_704ILR = string.IsNullOrEmpty(boton_704ILR.Glyph_704ILR) ? 0 : 30;
            int texto_704ILR = string.IsNullOrEmpty(boton_704ILR.Text) ? 0 : TextRenderer.MeasureText(boton_704ILR.Text, boton_704ILR.Font,
                new Size(int.MaxValue, Math.Max(1, boton_704ILR.Height - 1)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis).Width;
            boton_704ILR.Width = Math.Min(minimo_704ILR * 3 / 2, Math.Max(minimo_704ILR, glifo_704ILR + texto_704ILR + 14 + 1));
        }
    }
}
