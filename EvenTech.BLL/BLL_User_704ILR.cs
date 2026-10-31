using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum CreateUserResult_704ILR
    {
        Success_704ILR,
        InvalidUsername_704ILR,
        UsernameAlreadyExists_704ILR,
        InvalidPassword_704ILR
    }

    // Resultado de asignar perfiles a cuentas.
    public enum AsignacionPerfilResult_704ILR
    {
        Success_704ILR,
        SinGestorDePerfiles_704ILR,  // el cambio empeoraba la continuidad de PERFILES_GESTION (ver BLL_Perfil_704ILR.DejaSinGestor_704ILR)
        ReferenciaInvalida_704ILR    // alguna cuenta o perfil del lote no existe
    }

    // Alta de usuarios. La password en claro nunca llega aca: la UI manda solo
    // el hash SHA-256. Se valida formato de username y se delega al DAL.
    public static class BLL_User_704ILR
    {
        // \z y no $: en .NET '$' tambien coincide antes de un '\n' final, y un nombre
        // como "qa_salto\n" pasaba la validacion y quedaba guardado con el salto.
        private static readonly Regex UsernameRegex_704ILR = new Regex(@"^[a-zA-Z0-9_\.\-]{3,50}\z");

        // Formato de un nombre de usuario: de 3 a 50 letras, numeros, '.', '_' o '-'.
        // Es la regla del alta y la aplica tambien el login: un nombre fuera de este
        // formato no puede ser el de ninguna cuenta.
        public static bool FormatoUsernameValido_704ILR(string username_704ILR)
            => !string.IsNullOrWhiteSpace(username_704ILR) && UsernameRegex_704ILR.IsMatch(username_704ILR);

        private static readonly Regex HashRegex_704ILR = new Regex(@"^[0-9a-f]{64}\z");
        private static readonly string HashVacio_704ILR = Encrypt_704ILR.HashValue_704ILR(string.Empty);
        private static bool HashValido_704ILR(string hash_704ILR) => hash_704ILR != null && HashRegex_704ILR.IsMatch(hash_704ILR);

        public static CreateUserResult_704ILR CreateUser_704ILR(string username_704ILR, string hashedPassword_704ILR)
        {
            if (!FormatoUsernameValido_704ILR(username_704ILR))
                return CreateUserResult_704ILR.InvalidUsername_704ILR;

            // El hash es el SHA-256 en hexadecimal y minusculas que calcula la pantalla
            // (Encrypt_704ILR.HashValue_704ILR). Otra cosa de 64 caracteres dejaba una cuenta
            // con la que nunca se podia ingresar: el login compara en forma ordinal. El hash
            // de la contraseña vacia tampoco se admite: es un valor conocido y la pantalla
            // exige una contraseña (de al menos 4 caracteres, regla que solo ella puede
            // medir, porque la contraseña en claro no sale de la pantalla).
            if (!HashValido_704ILR(hashedPassword_704ILR) || hashedPassword_704ILR == HashVacio_704ILR)
                return CreateUserResult_704ILR.InvalidPassword_704ILR;

            if (DAL_User_704ILR.ExistsUsername_704ILR(username_704ILR))
                return CreateUserResult_704ILR.UsernameAlreadyExists_704ILR;

            try
            {
                DAL_User_704ILR.Insert_704ILR(username_704ILR, hashedPassword_704ILR);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex_704ILR) when (EsChoqueDeUnicidad_704ILR(ex_704ILR))
            {
                // Otra instancia dio de alta el mismo nombre entre el chequeo y el INSERT
                // (dos altas simultaneas): UQ_Users_Username es la red de seguridad y la
                // respuesta es la misma que la del chequeo previo.
                return CreateUserResult_704ILR.UsernameAlreadyExists_704ILR;
            }

            // El alta se hace desde la pantalla de acceso, sin sesion iniciada: la
            // bitacora la asienta como "Sistema". Es la unica accion que crea una
            // credencial nueva, asi que se registra con criticidad Advertencia.
            BLL_Bitacora_704ILR.Registrar_704ILR("Usuarios", "Alta de cuenta", CriticidadBitacora_704ILR.Advertencia,
                $"Cuenta '{username_704ILR}' creada desde la pantalla de acceso (sin perfil asignado)");
            return CreateUserResult_704ILR.Success_704ILR;
        }

        // 2627: violacion de una restriccion UNIQUE; 2601: fila duplicada en un indice
        // unico. En Users la unica restriccion unica es UQ_Users_Username.
        private static bool EsChoqueDeUnicidad_704ILR(Microsoft.Data.SqlClient.SqlException ex_704ILR)
            => ex_704ILR.Number == 2627 || ex_704ILR.Number == 2601;

        // --- Asignacion de perfiles (T04) ---

        public static List<BE_User_704ILR> GetAll_704ILR() => DAL_User_704ILR.GetAll_704ILR();

        public static AsignacionPerfilResult_704ILR AsignarPerfil_704ILR(int userId_704ILR, int? perfilId_704ILR) =>
            AsignarPerfiles_704ILR(new Dictionary<int, int?> { [userId_704ILR] = perfilId_704ILR });

        // Asigna (o quita, con null) el perfil de varias cuentas a la vez: la grilla
        // de Perfiles graba juntas todas las filas que cambiaron. Se valida el lote
        // completo, asi promover una cuenta y degradar otra en la misma grabacion no
        // depende del orden de las filas, y se aplica en una sola transaccion.
        // Rechaza el lote que empeora la continuidad de la gestion de perfiles (G04,
        // RNF-09; la regla esta en BLL_Perfil_704ILR.DejaSinGestor_704ILR): si alguna
        // cuenta activa y no bloqueada puede gestionar perfiles y desbloquear cuentas, tiene
        // que seguir habiendola; si solo pueden hacerlo cuentas activas bloqueadas (se
        // recuperan con el desbloqueo), tiene que seguir quedando al menos una. La regla y el detalle
        // de la bitacora se arman con el bloqueo de la gestion de perfiles ya tomado por
        // la transaccion que graba (DAL): otra grabacion simultanea no puede cambiar,
        // entre la validacion y la escritura, lo que la regla leyo.
        public static AsignacionPerfilResult_704ILR AsignarPerfiles_704ILR(IDictionary<int, int?> cambios_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR("PERFILES_GESTION", "asignar perfiles a usuarios");
            if (cambios_704ILR == null || cambios_704ILR.Count == 0) return AsignacionPerfilResult_704ILR.Success_704ILR;

            string rechazadas_704ILR = null, inexistentes_704ILR = null;
            var asientos_704ILR = new List<KeyValuePair<CriticidadBitacora_704ILR, string>>();
            bool grabado_704ILR = DAL_User_704ILR.SetPerfiles_704ILR(cambios_704ILR, () =>
            {
                var usuarios_704ILR = DAL_User_704ILR.GetAll_704ILR().ToDictionary(u_704ILR => u_704ILR.Id_704ILR);
                string Cuenta_704ILR(int id_704ILR) =>
                    usuarios_704ILR.TryGetValue(id_704ILR, out var u_704ILR) ? $"'{u_704ILR.Username_704ILR}' (#{id_704ILR})" : "#" + id_704ILR;

                // Una cuenta o un perfil que no existe no se asigna: el UPDATE no afectaba
                // ninguna fila (o chocaba con la clave foranea) y la bitacora asentaba una
                // asignacion que no habia ocurrido.
                var perfiles_704ILR = new HashSet<int>(DAL_Perfil_704ILR.GetAll_704ILR().Select(p_704ILR => p_704ILR.Id_704ILR));
                var faltan_704ILR = cambios_704ILR
                    .Where(kv_704ILR => !usuarios_704ILR.ContainsKey(kv_704ILR.Key) || (kv_704ILR.Value.HasValue && !perfiles_704ILR.Contains(kv_704ILR.Value.Value)))
                    .Select(kv_704ILR => $"usuario #{kv_704ILR.Key} -> {BLL_Perfil_704ILR.DescribirPerfil_704ILR(kv_704ILR.Value)}")
                    .ToList();
                if (faltan_704ILR.Count > 0)
                {
                    inexistentes_704ILR = string.Join(", ", faltan_704ILR);
                    return false;
                }

                if (BLL_Perfil_704ILR.DejaSinGestor_704ILR(null, cambios_704ILR))
                {
                    rechazadas_704ILR = string.Join(", ", cambios_704ILR.Select(kv_704ILR =>
                        $"{Cuenta_704ILR(kv_704ILR.Key)} -> {BLL_Perfil_704ILR.DescribirPerfil_704ILR(kv_704ILR.Value)}"));
                    return false;
                }

                // El detalle se arma antes de grabar: el perfil anterior sale de la base.
                // Dejar una cuenta sin perfil o quitarle la gestion de perfiles se asienta
                // como Advertencia.
                foreach (var kv_704ILR in cambios_704ILR)
                {
                    int? anterior_704ILR = usuarios_704ILR.TryGetValue(kv_704ILR.Key, out var u_704ILR) ? u_704ILR.PerfilId_704ILR : null;
                    bool pierdeGestion_704ILR = BLL_Perfil_704ILR.OtorgaGestion_704ILR(anterior_704ILR) && !BLL_Perfil_704ILR.OtorgaGestion_704ILR(kv_704ILR.Value);
                    var criticidad_704ILR = !kv_704ILR.Value.HasValue || pierdeGestion_704ILR ? CriticidadBitacora_704ILR.Advertencia : CriticidadBitacora_704ILR.Info;
                    asientos_704ILR.Add(new KeyValuePair<CriticidadBitacora_704ILR, string>(criticidad_704ILR,
                        $"Usuario {Cuenta_704ILR(kv_704ILR.Key)}: perfil {BLL_Perfil_704ILR.DescribirPerfil_704ILR(anterior_704ILR)} -> " +
                        BLL_Perfil_704ILR.DescribirPerfil_704ILR(kv_704ILR.Value) +
                        (pierdeGestion_704ILR ? "; deja de tener " + BLL_Perfil_704ILR.ClaveGestionPerfiles_704ILR : "")));
                }
                return true;
            });

            if (!grabado_704ILR && inexistentes_704ILR != null)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Perfiles", "Asignacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Las asignaciones propuestas nombran cuentas o perfiles que no existen ({inexistentes_704ILR})");
                return AsignacionPerfilResult_704ILR.ReferenciaInvalida_704ILR;
            }
            if (!grabado_704ILR)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Perfiles", "Asignacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Las asignaciones propuestas ({rechazadas_704ILR}) dejaban al sistema sin usuarios activos con gestion de perfiles");
                return AsignacionPerfilResult_704ILR.SinGestorDePerfiles_704ILR;
            }
            foreach (var a_704ILR in asientos_704ILR)
                BLL_Bitacora_704ILR.Registrar_704ILR("Perfiles", "Asignacion de perfil", a_704ILR.Key, a_704ILR.Value);
            return AsignacionPerfilResult_704ILR.Success_704ILR;
        }

        // Desbloqueo de cuenta por un administrador (RF01.3): quita el bloqueo y
        // resetea el contador de intentos fallidos. Devuelve false, sin asentar nada, si
        // la cuenta no existe (antes se asentaba un desbloqueo que no habia ocurrido).
        public static bool Desbloquear_704ILR(int userId_704ILR)
        {
            BLL_Permisos_704ILR.Exigir_704ILR("PERFILES_GESTION", "desbloquear una cuenta");
            BE_User_704ILR usuario_704ILR = DAL_User_704ILR.GetById_704ILR(userId_704ILR);
            if (DAL_User_704ILR.Desbloquear_704ILR(userId_704ILR) == 0) return false;
            string cuenta_704ILR = usuario_704ILR == null ? "#" + userId_704ILR : $"'{usuario_704ILR.Username_704ILR}' (#{userId_704ILR})";
            BLL_Bitacora_704ILR.Registrar_704ILR("Usuarios", "Desbloqueo de cuenta", CriticidadBitacora_704ILR.Info,
                $"Usuario {cuenta_704ILR} desbloqueado");
            return true;
        }
    }
}
