using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using EvenTech.BE;
using EvenTech.DAL;

namespace EvenTech.BLL
{
    // Respaldos de la base del sistema: crearlos, listarlos y restaurar uno. Es la salida
    // que ofrece la verificacion de integridad cuando encuentra datos alterados por fuera
    // del sistema (ademas de recalcular los digitos verificadores): volver a un respaldo
    // tomado antes de la alteracion.
    //
    // Los respaldos viven en %ProgramData%\EvenTech\respaldos, en el equipo donde esta la
    // instancia de SQL Server (el sistema es monopuesto: la instancia es local). El archivo
    // lo escribe y lo lee la cuenta del servicio de SQL Server: la carpeta le da permiso a
    // esa cuenta (su identificador de servicio, NT SERVICE\MSSQL$<instancia>), asi que no
    // hace falta ser administrador del equipo.
    //
    // Antes de restaurar siempre se respalda la base actual: si el respaldo elegido no sirve
    // (no se puede restaurar o no es una base del sistema), la base vuelve a como estaba.
    public static class BLL_Respaldo_704ILR
    {
        public const string PermisoCrear_704ILR = "RESPALDO_CREAR";
        public const string PermisoRestaurar_704ILR = "RESPALDO_RESTAURAR";

        public static string Carpeta_704ILR =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EvenTech", "respaldos");

        // Solo un respaldo completo de la base (RESTORE HEADERONLY, BackupType 1) se restaura.
        private const int TipoCompleto_704ILR = 1;

        // Nombre que se le da al respaldo en SQL Server (lo ve tambien quien administra la
        // instancia) segun el motivo.
        private static string NombreDe_704ILR(MotivoRespaldo_704ILR motivo_704ILR)
        {
            switch (motivo_704ILR)
            {
                case MotivoRespaldo_704ILR.ANTES_DE_RESTAURAR: return "EvenTech - antes de restaurar un respaldo";
                case MotivoRespaldo_704ILR.ANTES_DE_ALTERAR: return "EvenTech - antes de alterar los datos";
                default: return "EvenTech - respaldo";
            }
        }

        // ------------------------------------------------------------------ crear

        // Respaldo completo de la base, tomado a pedido del usuario. Queda asentado.
        public static BE_Respaldo_704ILR Crear_704ILR()
        {
            BLL_Permisos_704ILR.Exigir_704ILR(PermisoCrear_704ILR, "crear un respaldo de la base");
            BE_Respaldo_704ILR respaldo_704ILR = Respaldar_704ILR(MotivoRespaldo_704ILR.MANUAL);
            BLL_Bitacora_704ILR.Registrar_704ILR("Respaldos", "Respaldo creado", CriticidadBitacora_704ILR.Info,
                $"Archivo: {respaldo_704ILR.Ruta_704ILR} ({Tamanio_704ILR(respaldo_704ILR.Tamanio_704ILR)})");
            return respaldo_704ILR;
        }

        private static BE_Respaldo_704ILR Respaldar_704ILR(MotivoRespaldo_704ILR motivo_704ILR)
        {
            PrepararCarpeta_704ILR();
            string sufijo_704ILR = motivo_704ILR == MotivoRespaldo_704ILR.MANUAL ? string.Empty : "_" + motivo_704ILR.ToString().ToLowerInvariant();
            string ruta_704ILR = Path.Combine(Carpeta_704ILR,
                DAL_Respaldo_704ILR.BaseActual_704ILR() + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + sufijo_704ILR + ".bak");
            DAL_Respaldo_704ILR.Respaldar_704ILR(ruta_704ILR, NombreDe_704ILR(motivo_704ILR), motivo_704ILR.ToString());
            return Leer_704ILR(ruta_704ILR);
        }

        // La carpeta de respaldos, con permiso para la cuenta del servicio de SQL Server de
        // la instancia de la conexion. La instancia tiene que estar en este equipo: un
        // servidor remoto escribiria en su propio disco, donde el sistema no lo ve.
        private static void PrepararCarpeta_704ILR()
        {
            DAL_Respaldo_704ILR.DatosInstancia_704ILR instancia_704ILR = DAL_Respaldo_704ILR.Instancia_704ILR();
            if (!string.IsNullOrEmpty(instancia_704ILR.Equipo_704ILR) &&
                !string.Equals(instancia_704ILR.Equipo_704ILR, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                throw new RespaldoNoDisponibleException_704ILR(instancia_704ILR.Equipo_704ILR);

            DirectoryInfo carpeta_704ILR = Directory.CreateDirectory(Carpeta_704ILR);
            string servicio_704ILR = string.IsNullOrEmpty(instancia_704ILR.Instancia_704ILR)
                ? @"NT SERVICE\MSSQLSERVER"
                : @"NT SERVICE\MSSQL$" + instancia_704ILR.Instancia_704ILR;
            try
            {
                var sid_704ILR = (SecurityIdentifier)new NTAccount(servicio_704ILR).Translate(typeof(SecurityIdentifier));
                DirectorySecurity acl_704ILR = carpeta_704ILR.GetAccessControl();
                foreach (FileSystemAccessRule regla_704ILR in acl_704ILR.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                    if (sid_704ILR.Equals(regla_704ILR.IdentityReference) && regla_704ILR.AccessControlType == AccessControlType.Allow &&
                        (regla_704ILR.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify)
                        return;   // ya tiene el permiso
                acl_704ILR.AddAccessRule(new FileSystemAccessRule(sid_704ILR, FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                carpeta_704ILR.SetAccessControl(acl_704ILR);
            }
            catch (Exception)
            {
                // Sin el permiso (la carpeta la creo otro usuario, el servicio corre con otra
                // cuenta), SQL Server informa al respaldar que no puede escribir: el mensaje
                // de la interfaz lo dice.
            }
        }

        // ----------------------------------------------------------------- listar

        // Respaldos de la carpeta, del mas nuevo al mas viejo. Uno que el motor no puede leer
        // figura igual (con la fecha del archivo), como ilegible.
        public static List<BE_Respaldo_704ILR> Listar_704ILR()
        {
            var lista_704ILR = new List<BE_Respaldo_704ILR>();
            if (!Directory.Exists(Carpeta_704ILR)) return lista_704ILR;
            foreach (string ruta_704ILR in Directory.GetFiles(Carpeta_704ILR, "*.bak"))
                lista_704ILR.Add(Leer_704ILR(ruta_704ILR));
            lista_704ILR.Sort((a_704ILR, b_704ILR) => b_704ILR.Fecha_704ILR.CompareTo(a_704ILR.Fecha_704ILR));
            return lista_704ILR;
        }

        private static BE_Respaldo_704ILR Leer_704ILR(string ruta_704ILR)
        {
            var archivo_704ILR = new FileInfo(ruta_704ILR);
            BE_Respaldo_704ILR respaldo_704ILR;
            try
            {
                respaldo_704ILR = DAL_Respaldo_704ILR.LeerCabecera_704ILR(ruta_704ILR)
                    ?? new BE_Respaldo_704ILR { Ruta_704ILR = ruta_704ILR, Detalle_704ILR = "El archivo no tiene ningun respaldo." };
            }
            catch (Exception ex_704ILR)
            {
                respaldo_704ILR = new BE_Respaldo_704ILR { Ruta_704ILR = ruta_704ILR, Detalle_704ILR = ex_704ILR.GetBaseException().Message };
            }
            if (respaldo_704ILR.Legible_704ILR && respaldo_704ILR.Tipo_704ILR != TipoCompleto_704ILR)
            {
                respaldo_704ILR.Legible_704ILR = false;
                respaldo_704ILR.Detalle_704ILR = "No es un respaldo completo de una base.";
            }
            if (respaldo_704ILR.Fecha_704ILR == DateTime.MinValue && archivo_704ILR.Exists)
                respaldo_704ILR.Fecha_704ILR = archivo_704ILR.LastWriteTime;
            respaldo_704ILR.Tamanio_704ILR = archivo_704ILR.Exists ? archivo_704ILR.Length : 0;
            return respaldo_704ILR;
        }

        // Motivo con que tomo el respaldo el sistema; null si no lo tomo el sistema.
        public static MotivoRespaldo_704ILR? MotivoDe_704ILR(BE_Respaldo_704ILR respaldo_704ILR)
        {
            if (respaldo_704ILR != null && Enum.TryParse(respaldo_704ILR.Motivo_704ILR, false, out MotivoRespaldo_704ILR motivo_704ILR) &&
                Enum.IsDefined(typeof(MotivoRespaldo_704ILR), motivo_704ILR))
                return motivo_704ILR;
            return null;
        }

        // Copia a la carpeta de respaldos un .bak elegido en otra carpeta (el de demostracion,
        // uno guardado en un pendrive): la cuenta del servicio de SQL Server no puede leer las
        // carpetas del usuario. Devuelve la copia. Si ya esta en la carpeta, no copia nada.
        public static BE_Respaldo_704ILR Incorporar_704ILR(string ruta_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR(PermisoRestaurar_704ILR, "agregar un respaldo a la carpeta de respaldos");
            if (string.IsNullOrWhiteSpace(ruta_704ILR) || !File.Exists(ruta_704ILR))
                return new BE_Respaldo_704ILR { Ruta_704ILR = ruta_704ILR, Detalle_704ILR = "El archivo no existe." };
            PrepararCarpeta_704ILR();
            string completa_704ILR = Path.GetFullPath(ruta_704ILR);
            if (string.Equals(Path.GetDirectoryName(completa_704ILR)?.TrimEnd('\\'), Carpeta_704ILR.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return Leer_704ILR(completa_704ILR);

            string destino_704ILR = Path.Combine(Carpeta_704ILR, Path.GetFileName(completa_704ILR));
            if (File.Exists(destino_704ILR))
                destino_704ILR = Path.Combine(Carpeta_704ILR, Path.GetFileNameWithoutExtension(completa_704ILR) + "_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + Path.GetExtension(completa_704ILR));
            File.Copy(completa_704ILR, destino_704ILR, false);
            BE_Respaldo_704ILR copia_704ILR = Leer_704ILR(destino_704ILR);
            if (!copia_704ILR.Legible_704ILR)
            {
                // No es un respaldo que sirva: no se lo deja en la carpeta.
                try { File.Delete(destino_704ILR); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return copia_704ILR;
        }

        // -------------------------------------------------------------- restaurar

        // Restaura un respaldo de la carpeta sobre la base del sistema. Antes respalda la base
        // actual; despues comprueba que la base restaurada tenga el esquema del sistema y, si
        // no lo tiene (o la restauracion fallo), la devuelve al respaldo previo. Lo que pasa
        // queda asentado (lo de una restauracion exitosa, en la base restaurada).
        public static ResultadoRestauracion_704ILR Restaurar_704ILR(string ruta_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR(PermisoRestaurar_704ILR, "restaurar un respaldo de la base");
            var resultado_704ILR = new ResultadoRestauracion_704ILR();

            if (string.IsNullOrWhiteSpace(ruta_704ILR) || !File.Exists(ruta_704ILR))
                return Rechazo_704ILR(resultado_704ILR, RestauracionResult_704ILR.ArchivoInvalido_704ILR, null, "El archivo no existe.");

            // El archivo tiene que estar donde lo lee el servicio de SQL Server.
            BE_Respaldo_704ILR respaldo_704ILR = Incorporar_704ILR(ruta_704ILR);
            resultado_704ILR.Respaldo_704ILR = respaldo_704ILR;
            if (!respaldo_704ILR.Legible_704ILR)
                return Rechazo_704ILR(resultado_704ILR, RestauracionResult_704ILR.ArchivoInvalido_704ILR, respaldo_704ILR, respaldo_704ILR.Detalle_704ILR);
            int motor_704ILR = DAL_Respaldo_704ILR.Instancia_704ILR().VersionMayor_704ILR;
            if (respaldo_704ILR.VersionMotor_704ILR > motor_704ILR)
                return Rechazo_704ILR(resultado_704ILR, RestauracionResult_704ILR.VersionPosterior_704ILR, respaldo_704ILR,
                    $"Respaldo de SQL Server {respaldo_704ILR.VersionMotor_704ILR}, instancia de SQL Server {motor_704ILR}.");

            // Red de seguridad: la base actual (con los datos alterados, si los hay) queda
            // respaldada. Sin ese respaldo no se restaura nada.
            BE_Respaldo_704ILR previo_704ILR;
            try { previo_704ILR = Respaldar_704ILR(MotivoRespaldo_704ILR.ANTES_DE_RESTAURAR); }
            catch (Exception ex_704ILR)
            {
                BLL_Bitacora_704ILR.RegistrarExcepcion_704ILR(ex_704ILR, "Respaldos", "Respaldo previo a la restauracion");
                return Rechazo_704ILR(resultado_704ILR, RestauracionResult_704ILR.SinRespaldoPrevio_704ILR, respaldo_704ILR, ex_704ILR.GetBaseException().Message);
            }
            resultado_704ILR.RespaldoPrevio_704ILR = previo_704ILR.Ruta_704ILR;

            string falla_704ILR = null;
            RestauracionResult_704ILR motivoFalla_704ILR = RestauracionResult_704ILR.Fallida_704ILR;
            try
            {
                DAL_Respaldo_704ILR.Restaurar_704ILR(respaldo_704ILR.Ruta_704ILR);
                // La base restaurada tiene que ser una base del sistema, de esta version o
                // compatible: es la misma comprobacion que hace la aplicacion al arrancar.
                if (!DAL_DB_Connection_704ILR.ProbarActual_704ILR(out string mensaje_704ILR))
                {
                    falla_704ILR = mensaje_704ILR;
                    motivoFalla_704ILR = RestauracionResult_704ILR.NoEsDelSistema_704ILR;
                }
            }
            catch (Exception ex_704ILR)
            {
                falla_704ILR = ex_704ILR.GetBaseException().Message;
            }

            if (falla_704ILR != null)
            {
                resultado_704ILR.Resultado_704ILR = motivoFalla_704ILR;
                resultado_704ILR.Detalle_704ILR = falla_704ILR;
                try
                {
                    DAL_Respaldo_704ILR.Restaurar_704ILR(previo_704ILR.Ruta_704ILR);
                    resultado_704ILR.BaseRepuesta_704ILR = true;
                }
                catch (Exception ex_704ILR)
                {
                    resultado_704ILR.Detalle_704ILR += " | No se pudo volver al respaldo previo: " + ex_704ILR.GetBaseException().Message;
                }
                Asentar_704ILR("Restauracion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Archivo: {respaldo_704ILR.Ruta_704ILR} | Motivo: {resultado_704ILR.Detalle_704ILR} | " +
                    (resultado_704ILR.BaseRepuesta_704ILR ? "La base volvio al respaldo previo" : "Respaldo previo: " + previo_704ILR.Ruta_704ILR));
                return resultado_704ILR;
            }

            resultado_704ILR.Resultado_704ILR = RestauracionResult_704ILR.Restaurada_704ILR;
            Asentar_704ILR("Respaldo restaurado", CriticidadBitacora_704ILR.Advertencia,
                $"Archivo: {respaldo_704ILR.Ruta_704ILR} (respaldo del {respaldo_704ILR.Fecha_704ILR.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}, " +
                $"base {respaldo_704ILR.BaseOrigen_704ILR}) | La base anterior quedo respaldada en {previo_704ILR.Ruta_704ILR}");
            return resultado_704ILR;
        }

        private static ResultadoRestauracion_704ILR Rechazo_704ILR(ResultadoRestauracion_704ILR resultado_704ILR,
            RestauracionResult_704ILR motivo_704ILR, BE_Respaldo_704ILR respaldo_704ILR, string detalle_704ILR)
        {
            resultado_704ILR.Resultado_704ILR = motivo_704ILR;
            resultado_704ILR.Respaldo_704ILR = respaldo_704ILR;
            resultado_704ILR.Detalle_704ILR = detalle_704ILR;
            Asentar_704ILR("Restauracion rechazada", CriticidadBitacora_704ILR.Advertencia,
                $"Archivo: {respaldo_704ILR?.Ruta_704ILR} | Motivo: {motivo_704ILR} | {detalle_704ILR}");
            return resultado_704ILR;
        }

        // El asiento no puede impedir que se informe el resultado (la base puede haber
        // quedado sin acceso si la restauracion fallo a mitad de camino).
        private static void Asentar_704ILR(string accion_704ILR, CriticidadBitacora_704ILR criticidad_704ILR, string detalle_704ILR)
        {
            try { BLL_Bitacora_704ILR.Registrar_704ILR("Respaldos", accion_704ILR, criticidad_704ILR, detalle_704ILR); }
            catch (Exception) { }
        }

        public static string Tamanio_704ILR(long bytes_704ILR)
            => bytes_704ILR >= 1024 * 1024
                ? (bytes_704ILR / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.CurrentCulture) + " MB"
                : (bytes_704ILR / 1024.0).ToString("0", CultureInfo.CurrentCulture) + " KB";
    }

    // La instancia de SQL Server de la conexion esta en otro equipo: el respaldo quedaria en
    // el disco de ese equipo, fuera del alcance del sistema.
    public class RespaldoNoDisponibleException_704ILR : Exception
    {
        public string Equipo_704ILR { get; }
        public RespaldoNoDisponibleException_704ILR(string equipo_704ILR)
            : base("La instancia de SQL Server esta en otro equipo (" + equipo_704ILR + "): los respaldos se hacen desde ese equipo.")
        {
            Equipo_704ILR = equipo_704ILR;
        }
    }
}
