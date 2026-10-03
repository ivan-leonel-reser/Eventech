using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum EmpleadoResult_704ILR
    {
        Success_704ILR,
        NombreInvalido_704ILR,          // falta el nombre o el apellido
        LongitudExcedida_704ILR,        // nombre o apellido no entran en su columna
        DniInvalido_704ILR,             // falta o no es un documento (solo digitos, 7 o mas)
        DniDuplicado_704ILR,
        EspecialidadInvalida_704ILR,
        CuentaInvalida_704ILR,          // la cuenta elegida no existe
        CuentaYaVinculada_704ILR,       // la cuenta ya representa a otro empleado
        ConAsignacionesVigentes_704ILR, // no se da de baja a quien todavia tiene turnos comprometidos
        NotFound_704ILR
    }

    // Reglas de negocio del personal (Proceso 2): validaciones antes de persistir.
    public static class BLL_Empleado_704ILR
    {
        // Anchos de dbo.Empleados (ver BLL_Cliente_704ILR: un texto mas largo se
        // guardaria recortado sin aviso).
        private const int MaxNombre_704ILR = 60;
        private const int MaxApellido_704ILR = 60;
        // DNI: solo digitos, de 7 hasta el ancho de su columna.
        private const int MinDigitosDni_704ILR = 7;
        private const int MaxDigitosDni_704ILR = 20;

        public static List<BE_Empleado_704ILR> GetAll_704ILR() => DAL_Empleado_704ILR.GetAll_704ILR();

        public static List<BE_Empleado_704ILR> GetActivos_704ILR() => DAL_Empleado_704ILR.GetActivos_704ILR();

        public static BE_Empleado_704ILR GetById_704ILR(int id_704ILR) => DAL_Empleado_704ILR.GetById_704ILR(id_704ILR);

        public static List<BE_Especialidad_704ILR> GetEspecialidades_704ILR() => DAL_Empleado_704ILR.GetEspecialidades_704ILR();

        // Empleado al que representa la cuenta de la sesion, o null si no hay sesion
        // o la cuenta no esta vinculada a ninguno. Es la identidad con la que el
        // empleado confirma su disponibilidad y consulta sus tareas.
        public static BE_Empleado_704ILR GetDeLaSesion_704ILR()
        {
            if (!SessionManager_704ILR.IsSessionActive_704ILR) return null;
            BE_User_704ILR usuario_704ILR = SessionManager_704ILR.GetInstance_704ILR.User_704ILR;
            return usuario_704ILR == null ? null : DAL_Empleado_704ILR.GetByUserId_704ILR(usuario_704ILR.Id_704ILR);
        }

        public static EmpleadoResult_704ILR Crear_704ILR(BE_Empleado_704ILR e_704ILR, out int nuevoId_704ILR)
        {
            nuevoId_704ILR = 0;
            var v_704ILR = Validar_704ILR(e_704ILR, 0);
            if (v_704ILR != EmpleadoResult_704ILR.Success_704ILR) return v_704ILR;

            try
            {
                nuevoId_704ILR = DAL_Empleado_704ILR.Insert_704ILR(e_704ILR);
            }
            // Los indices unicos (DNI y cuenta vinculada) son la red de seguridad cuando
            // otro puesto guardo lo mismo entre la validacion y la escritura (2601 y 2627).
            catch (SqlException ex_704ILR) when (ex_704ILR.Number == 2601 || ex_704ILR.Number == 2627)
            {
                nuevoId_704ILR = 0;
                BLL_Bitacora_704ILR.Registrar_704ILR("Empleados", "Alta rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Empleado '{e_704ILR.NombreCompleto_704ILR}': el motor rechazo el alta, ya hay un empleado con ese DNI o con esa cuenta (registrado en simultaneo).");
                return ChoqueDeUnicidad_704ILR(e_704ILR, 0);
            }
            BLL_Bitacora_704ILR.Registrar_704ILR("Empleados", "Alta de empleado", CriticidadBitacora_704ILR.Info,
                $"Empleado '{e_704ILR.NombreCompleto_704ILR}' creado (#{nuevoId_704ILR})");
            return EmpleadoResult_704ILR.Success_704ILR;
        }

        public static EmpleadoResult_704ILR Actualizar_704ILR(BE_Empleado_704ILR e_704ILR)
        {
            if (e_704ILR == null || e_704ILR.Id_704ILR <= 0 || DAL_Empleado_704ILR.GetById_704ILR(e_704ILR.Id_704ILR) == null)
                return EmpleadoResult_704ILR.NotFound_704ILR;
            var v_704ILR = Validar_704ILR(e_704ILR, e_704ILR.Id_704ILR);
            if (v_704ILR != EmpleadoResult_704ILR.Success_704ILR) return v_704ILR;

            // La ficha se lee con bloqueo y el control de la baja y la escritura van en
            // UNA transaccion: una asignacion simultanea del mismo empleado (que toma el
            // mismo bloqueo, ver BLL_AsignacionPersonal_704ILR) entra antes —y la baja
            // se rechaza— o despues —y encuentra al empleado ya dado de baja—.
            BE_Empleado_704ILR guardado_704ILR = null;
            bool bajaRechazada_704ILR = false;
            try
            {
                using (var cn_704ILR = new DAL_DB_Connection_704ILR())
                {
                    SqlConnection conn_704ILR = cn_704ILR.OpenConnection_704ILR();
                    using (SqlTransaction tx_704ILR = conn_704ILR.BeginTransaction())
                    {
                        guardado_704ILR = DAL_Empleado_704ILR.GetById_704ILR(e_704ILR.Id_704ILR, conn_704ILR, tx_704ILR);
                        if (guardado_704ILR == null) return EmpleadoResult_704ILR.NotFound_704ILR;

                        // Dar de baja a un empleado con turnos comprometidos dejaria eventos
                        // con personal que ya no esta disponible: primero se lo quita de ellos.
                        if (guardado_704ILR.Activo_704ILR && !e_704ILR.Activo_704ILR &&
                            DAL_Empleado_704ILR.AsignacionesVigentes_704ILR(e_704ILR.Id_704ILR, conn_704ILR, tx_704ILR) > 0)
                            bajaRechazada_704ILR = true;
                        // Guardar sin cambiar nada no es una modificacion: no se escribe ni se asienta.
                        else if (MismosDatos_704ILR(guardado_704ILR, e_704ILR))
                            return EmpleadoResult_704ILR.Success_704ILR;
                        else
                        {
                            DAL_Empleado_704ILR.Update_704ILR(e_704ILR, conn_704ILR, tx_704ILR);
                            tx_704ILR.Commit();
                        }
                    }
                }
            }
            catch (SqlException ex_704ILR) when (ex_704ILR.Number == 2601 || ex_704ILR.Number == 2627)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Empleados", "Modificacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Empleado #{e_704ILR.Id_704ILR}: el motor rechazo la modificacion, ya hay un empleado con ese DNI o con esa cuenta (registrado en simultaneo).");
                return ChoqueDeUnicidad_704ILR(e_704ILR, e_704ILR.Id_704ILR);
            }

            if (bajaRechazada_704ILR)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Empleados", "Baja rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Empleado #{e_704ILR.Id_704ILR}: tiene asignaciones vigentes en eventos confirmados.");
                return EmpleadoResult_704ILR.ConAsignacionesVigentes_704ILR;
            }

            // Cambiar la cuenta vinculada cambia quien puede responder por el empleado, y
            // una baja lo saca del personal asignable: los dos se asientan como
            // Advertencia, igual que los demas cambios de acceso.
            bool cambioCuenta_704ILR = guardado_704ILR.UserId_704ILR != e_704ILR.UserId_704ILR;
            bool baja_704ILR = guardado_704ILR.Activo_704ILR && !e_704ILR.Activo_704ILR;
            bool reactivacion_704ILR = !guardado_704ILR.Activo_704ILR && e_704ILR.Activo_704ILR;
            BLL_Bitacora_704ILR.Registrar_704ILR("Empleados", "Modificacion de empleado",
                cambioCuenta_704ILR || baja_704ILR ? CriticidadBitacora_704ILR.Advertencia : CriticidadBitacora_704ILR.Info,
                $"Empleado #{e_704ILR.Id_704ILR} actualizado"
                + (baja_704ILR ? "; se da de baja" : "")
                + (reactivacion_704ILR ? "; se reactiva" : "")
                + (cambioCuenta_704ILR ? "; cambio la cuenta vinculada" : ""));
            return EmpleadoResult_704ILR.Success_704ILR;
        }

        private static EmpleadoResult_704ILR Validar_704ILR(BE_Empleado_704ILR e_704ILR, int idActual_704ILR)
        {
            // Nombre y apellido obligatorios: un texto que no se ve cuenta como vacio y se
            // guarda tal como se ve (mismo criterio que Clientes y Servicios).
            if (e_704ILR == null || GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(e_704ILR.Nombre_704ILR)
                || GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(e_704ILR.Apellido_704ILR))
                return EmpleadoResult_704ILR.NombreInvalido_704ILR;
            e_704ILR.Nombre_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(e_704ILR.Nombre_704ILR);
            e_704ILR.Apellido_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(e_704ILR.Apellido_704ILR);
            if (e_704ILR.Nombre_704ILR.Length > MaxNombre_704ILR || e_704ILR.Apellido_704ILR.Length > MaxApellido_704ILR)
                return EmpleadoResult_704ILR.LongitudExcedida_704ILR;

            // DNI obligatorio: identifica a la persona. Se guarda solo con sus digitos,
            // asi el control de duplicado y el indice unico comparan documentos.
            string dni_704ILR = NormalizarDni_704ILR(e_704ILR.Dni_704ILR);
            if (dni_704ILR == null) return EmpleadoResult_704ILR.DniInvalido_704ILR;
            e_704ILR.Dni_704ILR = dni_704ILR;
            if (DAL_Empleado_704ILR.ExistsDni_704ILR(dni_704ILR, idActual_704ILR))
                return EmpleadoResult_704ILR.DniDuplicado_704ILR;

            if (e_704ILR.EspecialidadId_704ILR <= 0 || !DAL_Empleado_704ILR.ExisteEspecialidad_704ILR(e_704ILR.EspecialidadId_704ILR))
                return EmpleadoResult_704ILR.EspecialidadInvalida_704ILR;

            // La cuenta es opcional. Si se elige, tiene que existir y no representar ya a
            // otro empleado: una cuenta responde por una sola persona.
            if (e_704ILR.UserId_704ILR.HasValue)
            {
                if (e_704ILR.UserId_704ILR.Value <= 0 || DAL_User_704ILR.GetById_704ILR(e_704ILR.UserId_704ILR.Value) == null)
                    return EmpleadoResult_704ILR.CuentaInvalida_704ILR;
                if (DAL_Empleado_704ILR.CuentaVinculada_704ILR(e_704ILR.UserId_704ILR.Value, idActual_704ILR))
                    return EmpleadoResult_704ILR.CuentaYaVinculada_704ILR;
            }
            return EmpleadoResult_704ILR.Success_704ILR;
        }

        // Cual de las dos unicidades choco en el motor: se vuelve a consultar con lo ya
        // escrito por la otra sesion. Si no es la cuenta, es el DNI.
        private static EmpleadoResult_704ILR ChoqueDeUnicidad_704ILR(BE_Empleado_704ILR e_704ILR, int idActual_704ILR)
            => e_704ILR.UserId_704ILR.HasValue && DAL_Empleado_704ILR.CuentaVinculada_704ILR(e_704ILR.UserId_704ILR.Value, idActual_704ILR)
                ? EmpleadoResult_704ILR.CuentaYaVinculada_704ILR
                : EmpleadoResult_704ILR.DniDuplicado_704ILR;

        // Compara lo guardado con lo que se guardaria (el nuevo ya llega normalizado de Validar).
        private static bool MismosDatos_704ILR(BE_Empleado_704ILR guardado_704ILR, BE_Empleado_704ILR nuevo_704ILR) =>
            guardado_704ILR.Nombre_704ILR == nuevo_704ILR.Nombre_704ILR
            && guardado_704ILR.Apellido_704ILR == nuevo_704ILR.Apellido_704ILR
            && guardado_704ILR.Dni_704ILR == nuevo_704ILR.Dni_704ILR
            && guardado_704ILR.EspecialidadId_704ILR == nuevo_704ILR.EspecialidadId_704ILR
            && guardado_704ILR.UserId_704ILR == nuevo_704ILR.UserId_704ILR
            && guardado_704ILR.Activo_704ILR == nuevo_704ILR.Activo_704ILR;

        // Solo digitos ASCII: se ignoran puntos, guiones, espacios y lo que no ocupa lugar,
        // y se quitan los ceros a la izquierda (mismo criterio que el DNI de los clientes).
        // Devuelve null si falta, si queda otra cosa o si el largo no es de un documento.
        private static string NormalizarDni_704ILR(string texto_704ILR)
        {
            string visible_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(texto_704ILR);
            if (string.IsNullOrEmpty(visible_704ILR)) return null;
            var digitos_704ILR = new StringBuilder(visible_704ILR.Length);
            foreach (char ch_704ILR in visible_704ILR)
            {
                if (ch_704ILR == '.' || ch_704ILR == '-' || char.IsWhiteSpace(ch_704ILR)) continue;
                if (ch_704ILR < '0' || ch_704ILR > '9') return null;
                digitos_704ILR.Append(ch_704ILR);
            }
            string normalizado_704ILR = digitos_704ILR.ToString().TrimStart('0');
            return normalizado_704ILR.Length >= MinDigitosDni_704ILR && normalizado_704ILR.Length <= MaxDigitosDni_704ILR
                ? normalizado_704ILR : null;
        }
    }
}
