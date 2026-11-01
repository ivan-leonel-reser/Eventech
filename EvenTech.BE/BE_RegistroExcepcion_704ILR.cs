using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace EvenTech.BE
{
    // Registro de una excepcion tecnica, tal como se serializa a XML (A03). Es la copia
    // en archivo del asiento "Error" de la bitacora, con lo que la bitacora no guarda:
    // el tipo, la pila de llamadas y las excepciones internas. Queda en el equipo aunque
    // la base no responda, que es justamente cuando mas hace falta.
    //
    // Los nombres de los elementos del XML son el formato del archivo (un dato, como
    // las columnas de una tabla): van sin el sufijo de autoria, y asi un archivo
    // exportado se lee igual con cualquier herramienta.
    //
    // No lleva contrasenas, claves, cadenas de conexion ni datos de clientes: solo lo
    // que describe la falla y donde ocurrio.
    [XmlRoot("RegistroExcepcion")]
    public class BE_RegistroExcepcion_704ILR
    {
        [XmlAttribute("id")] public string Id_704ILR { get; set; }
        [XmlElement("Fecha")] public DateTime Fecha_704ILR { get; set; }
        [XmlElement("Usuario")] public string Usuario_704ILR { get; set; }
        [XmlElement("Equipo")] public string Equipo_704ILR { get; set; }
        [XmlElement("Version")] public string Version_704ILR { get; set; }
        [XmlElement("Modulo")] public string Modulo_704ILR { get; set; }
        [XmlElement("Contexto")] public string Contexto_704ILR { get; set; }
        [XmlElement("Tipo")] public string Tipo_704ILR { get; set; }
        [XmlElement("Mensaje")] public string Mensaje_704ILR { get; set; }
        [XmlElement("Origen")] public string Origen_704ILR { get; set; }
        [XmlElement("Pila")] public string Pila_704ILR { get; set; }

        // Cadena de excepciones internas, de la mas externa a la mas interna.
        [XmlArray("Internas"), XmlArrayItem("Interna")]
        public List<BE_ExcepcionInterna_704ILR> Internas_704ILR { get; set; } = new List<BE_ExcepcionInterna_704ILR>();

        // Lo que sigue no se serializa: lo completa la lectura.
        // Archivo del que se leyo (vacio si viene de un lote exportado).
        [XmlIgnore] public string Archivo_704ILR { get; set; }
        // El archivo existe pero no se pudo interpretar (danado, de otro formato).
        [XmlIgnore] public bool Ilegible_704ILR { get; set; }
    }

    [XmlType("Interna")]
    public class BE_ExcepcionInterna_704ILR
    {
        [XmlElement("Tipo")] public string Tipo_704ILR { get; set; }
        [XmlElement("Mensaje")] public string Mensaje_704ILR { get; set; }
    }

    // Lote de registros exportado para enviar a quien da soporte, y que se puede volver
    // a abrir desde la aplicacion de otro equipo.
    [XmlRoot("LoteExcepciones")]
    public class BE_LoteExcepciones_704ILR
    {
        [XmlAttribute("formato")] public int Formato_704ILR { get; set; } = 1;
        [XmlElement("Generado")] public DateTime Generado_704ILR { get; set; }
        [XmlElement("Equipo")] public string Equipo_704ILR { get; set; }
        [XmlElement("Usuario")] public string Usuario_704ILR { get; set; }
        [XmlElement("Version")] public string Version_704ILR { get; set; }

        [XmlArray("Registros"), XmlArrayItem("RegistroExcepcion")]
        public List<BE_RegistroExcepcion_704ILR> Registros_704ILR { get; set; } = new List<BE_RegistroExcepcion_704ILR>();
    }
}
