using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using EvenTech.BE;
using EvenTech.DAL;
using EvenTech.Services;

namespace EvenTech.BLL
{
    public enum ClienteResult_704ILR
    {
        Success_704ILR,
        NombreInvalido_704ILR,
        DniDuplicado_704ILR,
        EmailInvalido_704ILR,
        NotFound_704ILR,
        DniInvalido_704ILR,
        LongitudExcedida_704ILR
    }

    // Reglas de negocio de clientes (Proceso 1): validaciones antes de persistir.
    public static class BLL_Cliente_704ILR
    {
        private static readonly Regex EmailRegex_704ILR =
            new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        // Anchos de dbo.Clientes. La capa de datos manda los parametros con ese tamano
        // fijo, de modo que un texto mas largo se guardaba recortado sin aviso (y un
        // contacto cifrado recortado ya no se puede descifrar): la regla se hace
        // explicita aca. Email y Telefono admiten lo mismo que la ficha de alta del
        // CUN003 y, ademas, su valor CIFRADO tiene que entrar en la columna: el cifrado
        // ocupa mas que el texto y crece con los bytes UTF-8, no con los caracteres.
        private const int MaxNombre_704ILR = 60;
        private const int MaxApellido_704ILR = 60;
        private const int MaxEmail_704ILR = 120;
        private const int MaxTelefono_704ILR = 30;
        private const int ColumnaEmail_704ILR = 400;
        private const int ColumnaTelefono_704ILR = 200;
        // DNI: solo digitos, de 7 hasta el ancho de su columna.
        private const int MinDigitosDni_704ILR = 7;
        private const int MaxDigitosDni_704ILR = 20;

        public static List<BE_Cliente_704ILR> GetAll_704ILR() => DAL_Cliente_704ILR.GetAll_704ILR();

        public static BE_Cliente_704ILR GetById_704ILR(int id_704ILR) => DAL_Cliente_704ILR.GetById_704ILR(id_704ILR);

        public static ClienteResult_704ILR Crear_704ILR(BE_Cliente_704ILR c_704ILR, out int nuevoId_704ILR)
        {
            nuevoId_704ILR = 0;
            var v_704ILR = Validar_704ILR(c_704ILR, 0);
            if (v_704ILR != ClienteResult_704ILR.Success_704ILR) return v_704ILR;

            try
            {
                nuevoId_704ILR = DAL_Cliente_704ILR.Insert_704ILR(c_704ILR);
            }
            // CUN003 3.1 en el motor: el indice unico UX_Clientes_Dni es la red de seguridad
            // cuando otro puesto registro el mismo DNI entre la validacion y la escritura
            // (2601 y 2627: indice y restriccion unica). Se informa como el DNI duplicado que
            // es, igual que Reservas con el choque de salon y fecha, y queda asentado.
            catch (SqlException ex_704ILR) when (ex_704ILR.Number == 2601 || ex_704ILR.Number == 2627)
            {
                nuevoId_704ILR = 0;
                BLL_Bitacora_704ILR.Registrar_704ILR("Clientes", "Alta rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Cliente '{c_704ILR.NombreCompleto_704ILR}': el motor rechazo el alta, ya hay un cliente con ese DNI (registrado en simultaneo).");
                return ClienteResult_704ILR.DniDuplicado_704ILR;
            }
            BLL_Bitacora_704ILR.Registrar_704ILR("Clientes", "Alta de cliente", CriticidadBitacora_704ILR.Info,
                $"Cliente '{c_704ILR.NombreCompleto_704ILR}' creado (#{nuevoId_704ILR})");
            return ClienteResult_704ILR.Success_704ILR;
        }

        public static ClienteResult_704ILR Actualizar_704ILR(BE_Cliente_704ILR c_704ILR)
        {
            if (c_704ILR == null || c_704ILR.Id_704ILR <= 0 || !DAL_Cliente_704ILR.Exists_704ILR(c_704ILR.Id_704ILR)) return ClienteResult_704ILR.NotFound_704ILR;
            var v_704ILR = Validar_704ILR(c_704ILR, c_704ILR.Id_704ILR);
            if (v_704ILR != ClienteResult_704ILR.Success_704ILR) return v_704ILR;

            try
            {
                DAL_Cliente_704ILR.Update_704ILR(c_704ILR);
            }
            // Mismo choque que en el alta: otro puesto registro ese DNI entre la validacion
            // y la escritura.
            catch (SqlException ex_704ILR) when (ex_704ILR.Number == 2601 || ex_704ILR.Number == 2627)
            {
                BLL_Bitacora_704ILR.Registrar_704ILR("Clientes", "Modificacion rechazada", CriticidadBitacora_704ILR.Advertencia,
                    $"Cliente #{c_704ILR.Id_704ILR}: el motor rechazo la modificacion, ya hay un cliente con ese DNI (registrado en simultaneo).");
                return ClienteResult_704ILR.DniDuplicado_704ILR;
            }
            BLL_Bitacora_704ILR.Registrar_704ILR("Clientes", "Modificacion de cliente", CriticidadBitacora_704ILR.Info,
                $"Cliente #{c_704ILR.Id_704ILR} actualizado");
            return ClienteResult_704ILR.Success_704ILR;
        }

        private static ClienteResult_704ILR Validar_704ILR(BE_Cliente_704ILR c_704ILR, int idActual_704ILR)
        {
            // Nombre obligatorio (CUN003, paso 3): un nombre que no se ve cuenta como vacio.
            // Ademas de los espacios, TextoEnBlanco descarta los caracteres de formato
            // invisibles (espacio de ancho cero, marca de orden de bytes, union de palabras)
            // y los rellenos que se dibujan vacios, que llegan al pegar desde una web o una
            // planilla: con IsNullOrWhiteSpace se daba de alta un cliente sin nombre visible,
            // primero en la grilla y en blanco en el combo de la reserva.
            if (c_704ILR == null || GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(c_704ILR.Nombre_704ILR))
                return ClienteResult_704ILR.NombreInvalido_704ILR;

            // Los datos opcionales (Apellido, DNI, Email y Telefono) que no se ven cuentan como
            // no cargados, con el mismo criterio que uno vacio o de espacios: el cliente se
            // guarda sin ese dato (NULL y sin cifrar). Un apellido formado solo por invisibles
            // se rechazaba con el aviso del NOMBRE aunque el nombre estuviera a la vista (y un
            // cliente heredado con ese apellido no se podia editar sin borrar algo que no se
            // ve); un telefono invisible se guardaba cifrado como si fuera un dato y se veia en
            // blanco; un DNI o un email invisibles se rechazaban por formato con el campo
            // aparentemente vacio. Se decide aca, antes de medir largos y formatos, para que
            // ninguna regla ni la capa de datos traten como dato algo que no se ve.
            c_704ILR.Apellido_704ILR = SinDatoInvisible_704ILR(c_704ILR.Apellido_704ILR);
            c_704ILR.Dni_704ILR = SinDatoInvisible_704ILR(c_704ILR.Dni_704ILR);
            c_704ILR.Email_704ILR = SinDatoInvisible_704ILR(c_704ILR.Email_704ILR);
            c_704ILR.Telefono_704ILR = SinDatoInvisible_704ILR(c_704ILR.Telefono_704ILR);

            // Los largos van antes que el formato y que el DNI duplicado: un dato que no
            // entra en su columna no se guarda recortado, y el mensaje dice por que.
            if (c_704ILR.Nombre_704ILR.Trim().Length > MaxNombre_704ILR
                || (c_704ILR.Apellido_704ILR ?? string.Empty).Trim().Length > MaxApellido_704ILR
                || !ContactoCabe_704ILR(c_704ILR.Email_704ILR, MaxEmail_704ILR, ColumnaEmail_704ILR)
                || !ContactoCabe_704ILR(c_704ILR.Telefono_704ILR, MaxTelefono_704ILR, ColumnaTelefono_704ILR))
                return ClienteResult_704ILR.LongitudExcedida_704ILR;

            // El email puede llegar todavia cifrado: pasa cuando la base se restaura en
            // otra maquina y la clave local no puede descifrar el valor guardado, asi que
            // la lectura devuelve el paquete tal cual ("ENC:..."). Ese valor no es un email
            // invalido escrito por el usuario, es un dato que no se pudo abrir: rechazarlo
            // dejaria la ficha del cliente trabada sin forma de corregirla. Solo cuenta como
            // cifrado un valor con la forma exacta de un paquete (EstaProtegido): un texto
            // tipeado que empieza con "ENC:" se valida como cualquier otro.
            if (!string.IsNullOrWhiteSpace(c_704ILR.Email_704ILR) &&
                !CryptoService_704ILR.EstaProtegido_704ILR(c_704ILR.Email_704ILR.Trim()) &&
                !EmailRegex_704ILR.IsMatch(c_704ILR.Email_704ILR.Trim()))
                return ClienteResult_704ILR.EmailInvalido_704ILR;

            // DNI (CUN003, paso 3 y flujo 3.1): el mismo documento se escribe con puntos,
            // espacios, guiones o ceros a la izquierda. Se guarda solo con sus digitos, asi
            // el control de duplicado y el indice unico comparan documentos y no textos.
            if (!string.IsNullOrWhiteSpace(c_704ILR.Dni_704ILR))
            {
                string dni_704ILR = NormalizarDni_704ILR(c_704ILR.Dni_704ILR);
                if (dni_704ILR == null) return ClienteResult_704ILR.DniInvalido_704ILR;
                c_704ILR.Dni_704ILR = dni_704ILR;
                if (DAL_Cliente_704ILR.ExistsDni_704ILR(dni_704ILR, idActual_704ILR))
                    return ClienteResult_704ILR.DniDuplicado_704ILR;
            }

            return ClienteResult_704ILR.Success_704ILR;

            // Un dato opcional que no se ve (vacio, de espacios, de invisibles o de rellenos)
            // queda sin cargar.
            static string SinDatoInvisible_704ILR(string valor_704ILR) =>
                GestorDeIdioma_704ILR.TextoEnBlanco_704ILR(valor_704ILR) ? null : valor_704ILR;

            // Un contacto vacio siempre entra. Un paquete que ya viene cifrado (dato de otra
            // PC que no se pudo abrir) se guarda tal cual: solo tiene que entrar en la columna.
            static bool ContactoCabe_704ILR(string valor_704ILR, int maxCaracteres_704ILR, int columna_704ILR)
            {
                if (string.IsNullOrWhiteSpace(valor_704ILR)) return true;
                string texto_704ILR = valor_704ILR.Trim();
                if (CryptoService_704ILR.EstaProtegido_704ILR(texto_704ILR)) return texto_704ILR.Length <= columna_704ILR;
                return texto_704ILR.Length <= maxCaracteres_704ILR
                    && CryptoService_704ILR.Proteger_704ILR(texto_704ILR).Length <= columna_704ILR;
            }

            // Solo digitos ASCII: se ignoran puntos, guiones y espacios, y se quitan los ceros
            // a la izquierda. Devuelve null si queda otra cosa o si el largo no es de un DNI.
            // Antes se descarta lo que no ocupa lugar, con el mismo criterio que para decidir si un
            // dato se ve (GestorDeIdioma.TextoVisible: marcas de direccion, espacio de ancho cero,
            // union de palabras, guion blando; los rellenos quedan como espacio). Un DNI pegado
            // desde una web o una planilla llegaba con una de esas marcas y se rechazaba con "use
            // solo numeros" aunque la caja mostrara solo numeros. Se guarda con sus digitos, asi
            // que choca con el mismo numero cargado sin marcas.
            static string NormalizarDni_704ILR(string texto_704ILR)
            {
                string visible_704ILR = GestorDeIdioma_704ILR.TextoVisible_704ILR(texto_704ILR);
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
}
