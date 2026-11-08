using Biblioteca.Entidades;
using Microsoft.Data.SqlClient;
using static Biblioteca.Datos.Sesion;

namespace Biblioteca.Datos;

public sealed class PrestamoDatos
{
    private const string Consulta = """
        SELECT p.PrestamoId,d.LibroId,s.Nombre,l.Titulo,p.FechaPrestamo,p.FechaLimite,d.FechaDevolucion,p.Estado
        FROM dbo.Prestamos p
        INNER JOIN dbo.DetallePrestamo d ON d.PrestamoId=p.PrestamoId
        INNER JOIN dbo.Libros l ON l.LibroId=d.LibroId
        INNER JOIN dbo.Socios s ON s.SocioId=p.SocioId
        """;

    private static FilaPrestamo LeerFila(SqlDataReader r) => new()
    {
        PrestamoId = r.GetInt32(0), LibroId = r.GetInt32(1), Socio = r.GetString(2), Libro = r.GetString(3),
        FechaPrestamo = r.GetDateTime(4), FechaLimite = r.GetDateTime(5),
        FechaDevolucion = r.IsDBNull(6) ? null : r.GetDateTime(6), Estado = r.GetString(7)
    };

    public Task<List<FilaPrestamo>> ReporteAsync(Sesion sesion, DateTime desde, DateTime hasta) => sesion.ConsultarAsync(
        Consulta + " WHERE p.FechaPrestamo BETWEEN @desde AND @hasta ORDER BY p.FechaPrestamo,p.PrestamoId,d.LibroId",
        LeerFila, Fecha("@desde", desde), Fecha("@hasta", hasta));

    public Task<List<FilaPrestamo>> PendientesAsync(Sesion sesion) => sesion.ConsultarAsync(
        Consulta + " WHERE d.FechaDevolucion IS NULL ORDER BY p.FechaLimite,p.PrestamoId,d.LibroId", LeerFila);

    public async Task<Prestamo?> ObtenerAsync(Sesion sesion, int prestamo) =>
        (await sesion.ConsultarAsync("SELECT PrestamoId,SocioId,FechaPrestamo,FechaLimite,Estado FROM dbo.Prestamos WHERE PrestamoId=@id",
            r => new Prestamo { PrestamoId = r.GetInt32(0), SocioId = r.GetInt32(1), FechaPrestamo = r.GetDateTime(2),
                FechaLimite = r.GetDateTime(3), Estado = r.GetString(4) }, Id("@id", prestamo))).SingleOrDefault();

    public Task<int> InsertarAsync(Sesion sesion, Prestamo prestamo) => sesion.EnteroAsync(
        """
        INSERT dbo.Prestamos (SocioId,FechaPrestamo,FechaLimite,Estado)
        OUTPUT INSERTED.PrestamoId VALUES (@socio,@fecha,@limite,@estado)
        """, Id("@socio", prestamo.SocioId), Fecha("@fecha", prestamo.FechaPrestamo),
        Fecha("@limite", prestamo.FechaLimite), Texto("@estado", prestamo.Estado, 12));

    public Task<int> CambiarEstadoAsync(Sesion sesion, int prestamo, string estado) => sesion.EjecutarAsync(
        "UPDATE dbo.Prestamos SET Estado=@estado WHERE PrestamoId=@id", Texto("@estado", estado, 12), Id("@id", prestamo));
}

public sealed class DetallePrestamoDatos
{
    public Task<int> PorSocioAsync(Sesion sesion, int socio) => sesion.EnteroAsync(
        """
        SELECT COUNT(*) FROM dbo.DetallePrestamo d INNER JOIN dbo.Prestamos p ON p.PrestamoId=d.PrestamoId
        WHERE p.SocioId=@id AND d.FechaDevolucion IS NULL
        """, Id("@id", socio));

    public Task<int> PorLibroAsync(Sesion sesion, int libro) => sesion.EnteroAsync(
        "SELECT COUNT(*) FROM dbo.DetallePrestamo WHERE LibroId=@id AND FechaDevolucion IS NULL", Id("@id", libro));

    public Task<int> PorPrestamoAsync(Sesion sesion, int prestamo) => sesion.EnteroAsync(
        "SELECT COUNT(*) FROM dbo.DetallePrestamo WHERE PrestamoId=@id AND FechaDevolucion IS NULL", Id("@id", prestamo));

    public async Task<DetallePrestamo?> ObtenerAsync(Sesion sesion, int prestamo, int libro) =>
        (await sesion.ConsultarAsync("SELECT PrestamoId,LibroId,FechaDevolucion FROM dbo.DetallePrestamo WHERE PrestamoId=@id AND LibroId=@libro",
            r => new DetallePrestamo { PrestamoId = r.GetInt32(0), LibroId = r.GetInt32(1),
                FechaDevolucion = r.IsDBNull(2) ? null : r.GetDateTime(2) }, Id("@id", prestamo), Id("@libro", libro))).SingleOrDefault();

    public Task<int> InsertarAsync(Sesion sesion, DetallePrestamo detalle) => sesion.EjecutarAsync(
        "INSERT dbo.DetallePrestamo (PrestamoId,LibroId,FechaDevolucion) VALUES (@id,@libro,@fecha)",
        Id("@id", detalle.PrestamoId), Id("@libro", detalle.LibroId), Fecha("@fecha", detalle.FechaDevolucion));

    public Task<int> ActualizarDevolucionAsync(Sesion sesion, DetallePrestamo detalle) => sesion.EjecutarAsync(
        "UPDATE dbo.DetallePrestamo SET FechaDevolucion=@fecha WHERE PrestamoId=@id AND LibroId=@libro",
        Fecha("@fecha", detalle.FechaDevolucion), Id("@id", detalle.PrestamoId), Id("@libro", detalle.LibroId));
}
