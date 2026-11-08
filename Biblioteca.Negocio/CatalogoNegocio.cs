using System.Net.Mail;
using Biblioteca.Datos;
using Biblioteca.Entidades;

namespace Biblioteca.Negocio;

public sealed class ReglaNegocioException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);

internal sealed class Operaciones(string cadena)
{
    private readonly BaseDatos db = new(cadena);

    public Task<T> LeerAsync<T>(Func<Sesion, Task<T>> operacion) => EjecutarAsync(() => db.LeerAsync(operacion));
    public Task<T> EscribirAsync<T>(Func<Sesion, Task<T>> operacion) => EjecutarAsync(() => db.EscribirAsync(operacion));

    private static async Task<T> EjecutarAsync<T>(Func<Task<T>> operacion)
    {
        try { return await operacion(); }
        catch (PersistenciaException error) { throw new ReglaNegocioException(error.Message, error); }
    }

    public static string Texto(string valor, int maximo, string campo)
    {
        valor = valor.Trim();
        if (valor.Length == 0 || valor.Length > maximo)
            throw new ReglaNegocioException($"Ingresa {campo} (máximo {maximo} caracteres).");
        return valor;
    }
}

public sealed class LibroNegocio(string cadena)
{
    private readonly Operaciones operaciones = new(cadena);
    private readonly LibroDatos libros = new();
    private readonly AutorDatos autores = new();
    private readonly DetallePrestamoDatos detalles = new();

    public Task<List<Libro>> BuscarAsync(string filtro = "") => operaciones.LeerAsync(s => libros.BuscarAsync(s, filtro.Trim()));
    public Task<List<Autor>> AutoresAsync() => operaciones.LeerAsync(async s =>
        (await autores.ListarAsync(s)).Where(a => a.Activo).ToList());

    public Task<int> GuardarAsync(Libro entrada, int ejemplaresOriginales = 0) => operaciones.EscribirAsync(async s =>
    {
        var libro = new Libro
        {
            LibroId = entrada.LibroId, Titulo = Operaciones.Texto(entrada.Titulo, 150, "el título"),
            ISBN = Operaciones.Texto(entrada.ISBN, 20, "el ISBN"),
            AutorId = entrada.AutorId, Ejemplares = entrada.Ejemplares
        };
        if (libro.Ejemplares < 0) throw new ReglaNegocioException("Los ejemplares deben ser cero o más.");
        if (!(await autores.ListarAsync(s)).Any(a => a.AutorId == libro.AutorId && a.Activo))
            throw new ReglaNegocioException("Selecciona un autor activo.");
        if (await libros.ExisteIsbnAsync(s, libro.ISBN, libro.LibroId))
            throw new ReglaNegocioException("El ISBN ya está registrado.");
        if (libro.LibroId == 0) return await libros.InsertarAsync(s, libro);

        var actual = await libros.ObtenerAsync(s, libro.LibroId);
        if (actual is not { Activo: true }) throw new ReglaNegocioException("El libro ya no está activo.");
        long ajuste = (long)libro.Ejemplares - ejemplaresOriginales;
        if (ajuste is < int.MinValue or > int.MaxValue || actual.Ejemplares + ajuste is < 0 or > int.MaxValue)
            throw new ReglaNegocioException("El stock cambió. Vuelve a buscar el libro antes de ajustarlo.");
        await libros.ActualizarAsync(s, libro, (int)ajuste);
        return libro.LibroId;
    });

    public Task BajaAsync(int libro) => operaciones.EscribirAsync(async s =>
    {
        if (await libros.ObtenerAsync(s, libro) is not { Activo: true })
            throw new ReglaNegocioException("El libro ya no está activo.");
        if (await detalles.PorLibroAsync(s, libro) > 0)
            throw new ReglaNegocioException("El libro tiene préstamos pendientes y no se puede dar de baja.");
        return await libros.BajaAsync(s, libro);
    });
}

public sealed class SocioNegocio(string cadena)
{
    private readonly Operaciones operaciones = new(cadena);
    private readonly SocioDatos socios = new();
    private readonly DetallePrestamoDatos detalles = new();

    public Task<List<Socio>> BuscarAsync(string filtro = "") => operaciones.LeerAsync(s => socios.BuscarAsync(s, filtro.Trim()));

    public Task<int> GuardarAsync(Socio entrada) => operaciones.EscribirAsync(async s =>
    {
        var dni = entrada.DNI.Trim();
        if (dni.Length != 8 || !dni.All(char.IsAsciiDigit))
            throw new ReglaNegocioException("El DNI debe tener ocho dígitos.");
        var correo = Operaciones.Texto(entrada.Email, 120, "el correo");
        if (!MailAddress.TryCreate(correo, out var direccion) || direccion.Address != correo)
            throw new ReglaNegocioException("Ingresa un correo válido.");
        var socio = new Socio
        {
            SocioId = entrada.SocioId, DNI = dni, Email = correo,
            Nombre = Operaciones.Texto(entrada.Nombre, 100, "el nombre")
        };
        if (await socios.ExisteDniAsync(s, dni, socio.SocioId))
            throw new ReglaNegocioException("El DNI ya está registrado.");
        if (socio.SocioId == 0) return await socios.InsertarAsync(s, socio);
        if (await socios.ObtenerAsync(s, socio.SocioId) is not { Activo: true })
            throw new ReglaNegocioException("El socio ya no está activo.");
        await socios.ActualizarAsync(s, socio);
        return socio.SocioId;
    });

    public Task BajaAsync(int socio) => operaciones.EscribirAsync(async s =>
    {
        if (await socios.ObtenerAsync(s, socio) is not { Activo: true })
            throw new ReglaNegocioException("El socio ya no está activo.");
        if (await detalles.PorSocioAsync(s, socio) > 0)
            throw new ReglaNegocioException("El socio tiene préstamos pendientes y no se puede dar de baja.");
        return await socios.BajaAsync(s, socio);
    });
}
