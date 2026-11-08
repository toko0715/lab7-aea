using Biblioteca.Entidades;
using Microsoft.Data.SqlClient;
using static Biblioteca.Datos.Sesion;

namespace Biblioteca.Datos;

public sealed class AutorDatos
{
    public Task<List<Autor>> ListarAsync(Sesion sesion) => sesion.ConsultarAsync(
        "SELECT AutorId, Nombre, Nacionalidad, Activo FROM dbo.Autores ORDER BY Nombre",
        r => new Autor { AutorId = r.GetInt32(0), Nombre = r.GetString(1), Nacionalidad = r.GetString(2), Activo = r.GetBoolean(3) });

    public Task<int> GuardarAsync(Sesion sesion, Autor autor)
    {
        var nombre = Texto("@nombre", autor.Nombre, 100);
        var pais = Texto("@pais", autor.Nacionalidad, 60);
        var activo = Bit("@activo", autor.Activo);
        return autor.AutorId == 0
            ? sesion.EnteroAsync("INSERT dbo.Autores (Nombre,Nacionalidad,Activo) OUTPUT INSERTED.AutorId VALUES (@nombre,@pais,@activo)",
                nombre, pais, activo)
            : sesion.EjecutarAsync("UPDATE dbo.Autores SET Nombre=@nombre,Nacionalidad=@pais,Activo=@activo WHERE AutorId=@id",
                nombre, pais, activo, Id("@id", autor.AutorId));
    }
}

public sealed class LibroDatos
{
    private const string Consulta = """
        SELECT l.LibroId,l.Titulo,l.ISBN,l.AutorId,a.Nombre,l.Ejemplares,l.Activo
        FROM dbo.Libros l INNER JOIN dbo.Autores a ON a.AutorId=l.AutorId
        """;

    private static Libro Leer(SqlDataReader r) => new()
    {
        LibroId = r.GetInt32(0), Titulo = r.GetString(1), ISBN = r.GetString(2),
        AutorId = r.GetInt32(3), NombreAutor = r.GetString(4), Ejemplares = r.GetInt32(5), Activo = r.GetBoolean(6)
    };

    public Task<List<Libro>> BuscarAsync(Sesion sesion, string filtro) => sesion.ConsultarAsync(
        Consulta + " WHERE l.Activo=1 AND (@filtro=N'' OR CHARINDEX(@filtro,l.Titulo)>0 OR CHARINDEX(@filtro,a.Nombre)>0) ORDER BY l.Titulo",
        Leer, Texto("@filtro", filtro, 150));

    public async Task<Libro?> ObtenerAsync(Sesion sesion, int libro) =>
        (await sesion.ConsultarAsync(Consulta + " WHERE l.LibroId=@id", Leer, Id("@id", libro))).SingleOrDefault();

    public async Task<bool> ExisteIsbnAsync(Sesion sesion, string isbn, int libro) =>
        await sesion.EnteroAsync("SELECT COUNT(*) FROM dbo.Libros WHERE ISBN=@isbn AND LibroId<>@id",
            Texto("@isbn", isbn, 20), Id("@id", libro)) > 0;

    public Task<int> InsertarAsync(Sesion sesion, Libro libro) => sesion.EnteroAsync(
        "INSERT dbo.Libros (Titulo,ISBN,AutorId,Ejemplares) OUTPUT INSERTED.LibroId VALUES (@titulo,@isbn,@autor,@stock)",
        Texto("@titulo", libro.Titulo, 150), Texto("@isbn", libro.ISBN, 20),
        Id("@autor", libro.AutorId), Id("@stock", libro.Ejemplares));

    public Task<int> ActualizarAsync(Sesion sesion, Libro libro, int ajuste) => sesion.EjecutarAsync(
        "UPDATE dbo.Libros SET Titulo=@titulo,ISBN=@isbn,AutorId=@autor,Ejemplares=Ejemplares+@ajuste WHERE LibroId=@id",
        Texto("@titulo", libro.Titulo, 150), Texto("@isbn", libro.ISBN, 20),
        Id("@autor", libro.AutorId), Id("@ajuste", ajuste), Id("@id", libro.LibroId));

    public Task<int> BajaAsync(Sesion sesion, int libro) => sesion.EjecutarAsync(
        "UPDATE dbo.Libros SET Activo=0 WHERE LibroId=@id", Id("@id", libro));

    public Task<int> AjustarAsync(Sesion sesion, int libro, int cantidad) => sesion.EjecutarAsync(
        "UPDATE dbo.Libros SET Ejemplares=Ejemplares+@cantidad WHERE LibroId=@id", Id("@cantidad", cantidad), Id("@id", libro));
}

public sealed class SocioDatos
{
    private const string Consulta = "SELECT SocioId,DNI,Nombre,Email,Activo FROM dbo.Socios";

    private static Socio Leer(SqlDataReader r) => new()
    {
        SocioId = r.GetInt32(0), DNI = r.GetString(1), Nombre = r.GetString(2), Email = r.GetString(3), Activo = r.GetBoolean(4)
    };

    public Task<List<Socio>> BuscarAsync(Sesion sesion, string filtro) => sesion.ConsultarAsync(
        Consulta + " WHERE Activo=1 AND (@filtro=N'' OR CHARINDEX(@filtro,Nombre)>0 OR CHARINDEX(@filtro,DNI)>0) ORDER BY Nombre",
        Leer, Texto("@filtro", filtro, 100));

    public async Task<Socio?> ObtenerAsync(Sesion sesion, int socio) =>
        (await sesion.ConsultarAsync(Consulta + " WHERE SocioId=@id", Leer, Id("@id", socio))).SingleOrDefault();

    public async Task<bool> ExisteDniAsync(Sesion sesion, string dni, int socio) =>
        await sesion.EnteroAsync("SELECT COUNT(*) FROM dbo.Socios WHERE DNI=@dni AND SocioId<>@id",
            Texto("@dni", dni, 8), Id("@id", socio)) > 0;

    public Task<int> InsertarAsync(Sesion sesion, Socio socio) => sesion.EnteroAsync(
        "INSERT dbo.Socios (DNI,Nombre,Email) OUTPUT INSERTED.SocioId VALUES (@dni,@nombre,@email)",
        Texto("@dni", socio.DNI, 8), Texto("@nombre", socio.Nombre, 100), Texto("@email", socio.Email, 120));

    public Task<int> ActualizarAsync(Sesion sesion, Socio socio) => sesion.EjecutarAsync(
        "UPDATE dbo.Socios SET DNI=@dni,Nombre=@nombre,Email=@email WHERE SocioId=@id",
        Texto("@dni", socio.DNI, 8), Texto("@nombre", socio.Nombre, 100), Texto("@email", socio.Email, 120), Id("@id", socio.SocioId));

    public Task<int> BajaAsync(Sesion sesion, int socio) => sesion.EjecutarAsync(
        "UPDATE dbo.Socios SET Activo=0 WHERE SocioId=@id", Id("@id", socio));
}
