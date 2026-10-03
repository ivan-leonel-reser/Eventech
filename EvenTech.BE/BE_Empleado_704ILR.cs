using System;

namespace EvenTech.BE
{
    // Especialidad del personal (catalogo, Proceso 2): mozo, cocinero, DJ...
    // Es el criterio con el que el coordinador busca a quien asignar y a quien
    // reemplaza a un empleado que rechazo el turno.
    public class BE_Especialidad_704ILR
    {
        public int Id_704ILR { get; set; }
        public string Nombre_704ILR { get; set; }

        public override string ToString() => Nombre_704ILR;
    }

    // Empleado especializado que puede asignarse a un evento (Proceso 2).
    // UserId vincula la ficha con la cuenta con la que el empleado ingresa: es lo
    // que permite que confirme su disponibilidad y consulte sus tareas (la cuenta
    // sabe a que empleado representa). Un empleado sin cuenta se puede asignar
    // igual, pero no puede responder por si mismo.
    public class BE_Empleado_704ILR
    {
        public int Id_704ILR { get; set; }
        public string Nombre_704ILR { get; set; }
        public string Apellido_704ILR { get; set; }
        public string Dni_704ILR { get; set; }
        public int EspecialidadId_704ILR { get; set; }
        public string EspecialidadNombre_704ILR { get; set; }   // proyectado en lecturas (JOIN)
        public int? UserId_704ILR { get; set; }
        public string Username_704ILR { get; set; }             // proyectado en lecturas (JOIN)
        public bool Activo_704ILR { get; set; } = true;
        public DateTime CreatedAt_704ILR { get; set; }

        // "Apellido, Nombre": la forma en que se lo busca en una grilla o en un combo.
        public string NombreCompleto_704ILR =>
            string.IsNullOrWhiteSpace(Apellido_704ILR) ? (Nombre_704ILR ?? string.Empty) : Apellido_704ILR + ", " + Nombre_704ILR;

        public override string ToString() => NombreCompleto_704ILR;
    }
}
