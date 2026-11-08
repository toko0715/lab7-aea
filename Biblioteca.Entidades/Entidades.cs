namespace Biblioteca.Entidades;

public sealed class Autor
{
    public int AutorId { get; init; }
    public string Nombre { get; init; } = "";
    public string Nacionalidad { get; init; } = "";
    public bool Activo { get; init; } = true;
}

public sealed class Libro
{
    public int LibroId { get; init; }
    public string Titulo { get; init; } = "";
    public string ISBN { get; init; } = "";
    public int AutorId { get; init; }
    public string NombreAutor { get; init; } = "";
    public int Ejemplares { get; init; }
    public bool Activo { get; init; } = true;
}

public sealed class Socio
{
    public int SocioId { get; init; }
    public string DNI { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string Email { get; init; } = "";
    public bool Activo { get; init; } = true;
}

public sealed class Prestamo
{
    public int PrestamoId { get; init; }
    public int SocioId { get; init; }
    public DateTime FechaPrestamo { get; init; }
    public DateTime FechaLimite { get; init; }
    public string Estado { get; init; } = "";
}

public sealed class DetallePrestamo
{
    public int PrestamoId { get; init; }
    public int LibroId { get; init; }
    public DateTime? FechaDevolucion { get; init; }
}

public sealed class FilaPrestamo
{
    public int PrestamoId { get; init; }
    public int LibroId { get; init; }
    public string Socio { get; init; } = "";
    public string Libro { get; init; } = "";
    public DateTime FechaPrestamo { get; init; }
    public DateTime FechaLimite { get; init; }
    public DateTime? FechaDevolucion { get; init; }
    public string Estado { get; init; } = "";
}
