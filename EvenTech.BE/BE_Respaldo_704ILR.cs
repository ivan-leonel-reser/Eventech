using System;
using System.IO;

namespace EvenTech.BE
{
    // Un respaldo completo de la base del sistema: un archivo .bak de la carpeta de
    // respaldos (%ProgramData%\EvenTech\respaldos). Los datos de la cabecera (fecha,
    // base de origen, motivo, version del motor) los informa SQL Server al leerlo; si no
    // la pudo leer, el respaldo figura como ilegible y no se lo puede restaurar.
    public class BE_Respaldo_704ILR
    {
        public string Ruta_704ILR { get; set; }
        public string Archivo_704ILR => string.IsNullOrEmpty(Ruta_704ILR) ? string.Empty : Path.GetFileName(Ruta_704ILR);

        // Fin del respaldo segun su cabecera; sin cabecera, la fecha del archivo.
        public DateTime Fecha_704ILR { get; set; }
        public long Tamanio_704ILR { get; set; }

        // Base de la que se tomo el respaldo y el nombre que se le dio al tomarlo.
        public string BaseOrigen_704ILR { get; set; }
        public string Nombre_704ILR { get; set; }

        // Motivo con que lo tomo el sistema (MotivoRespaldo_704ILR, como texto). Vacio en
        // un respaldo que no tomo el sistema (el de demostracion, uno de otra herramienta).
        public string Motivo_704ILR { get; set; }

        // Version mayor del motor que lo genero (15 = SQL Server 2019) y tipo de respaldo
        // (1 = completo de la base): solo un respaldo completo se puede restaurar.
        public int VersionMotor_704ILR { get; set; }
        public int Tipo_704ILR { get; set; }

        public bool Legible_704ILR { get; set; }
        public string Detalle_704ILR { get; set; }
    }

    // Por que tomo el sistema un respaldo. Viaja como texto en la descripcion del propio
    // respaldo (BACKUP ... DESCRIPTION), y con el se traduce el motivo en pantalla.
    public enum MotivoRespaldo_704ILR
    {
        MANUAL,
        ANTES_DE_RESTAURAR,
        ANTES_DE_ALTERAR
    }

    // Resultado de restaurar un respaldo.
    public enum RestauracionResult_704ILR
    {
        Restaurada_704ILR,
        ArchivoInvalido_704ILR,      // no existe, no se pudo leer o no es un respaldo completo
        VersionPosterior_704ILR,     // lo genero un SQL Server mas nuevo que el de la instancia
        SinRespaldoPrevio_704ILR,    // no se pudo respaldar la base actual: no se la toca
        NoEsDelSistema_704ILR,       // se restauro, pero no tiene el esquema: se volvio al respaldo previo
        Fallida_704ILR               // el motor no pudo restaurarlo: se volvio al respaldo previo
    }

    public class ResultadoRestauracion_704ILR
    {
        public RestauracionResult_704ILR Resultado_704ILR { get; set; }
        public BE_Respaldo_704ILR Respaldo_704ILR { get; set; }
        // Respaldo de la base que habia antes de restaurar (red de seguridad).
        public string RespaldoPrevio_704ILR { get; set; }
        // La base volvio al respaldo previo despues de una restauracion que no sirvio.
        public bool BaseRepuesta_704ILR { get; set; }
        public string Detalle_704ILR { get; set; }
    }
}
