using Biblioteca.Datos;
using Biblioteca.Entidades;

namespace Biblioteca.Negocio;

public sealed record ResultadoDevolucion(decimal Multa, int DiasRetraso, bool Cerrado);

public sealed class PrestamoNegocio(string cadena)
{
    private const int LimitePendientes = 3;
    private const decimal MultaPorDia = 1.50m;
    private readonly Operaciones operaciones = new(cadena);
    private readonly PrestamoDatos prestamos = new();
    private readonly DetallePrestamoDatos detalles = new();
    private readonly LibroDatos libros = new();
    private readonly SocioDatos socios = new();

    public Task<List<FilaPrestamo>> PendientesAsync() => operaciones.LeerAsync(prestamos.PendientesAsync);

    public Task<List<FilaPrestamo>> ReporteAsync(DateTime desde, DateTime hasta)
    {
        if (desde.Date > hasta.Date) throw new ReglaNegocioException("La fecha inicial debe ser menor o igual a la final.");
        return operaciones.LeerAsync(s => prestamos.ReporteAsync(s, desde, hasta));
    }

    public Task<int> RegistrarAsync(int socio, IReadOnlyCollection<int> librosElegidos, DateTime fecha, DateTime limite)
    {
        int[] seleccion = librosElegidos.ToArray();
        if (seleccion.Length == 0) throw new ReglaNegocioException("Agrega al menos un libro.");
        if (seleccion.Distinct().Count() != seleccion.Length)
            throw new ReglaNegocioException("No se puede repetir un libro en el mismo préstamo.");
        if (fecha.Date > limite.Date)
            throw new ReglaNegocioException("La fecha límite no puede ser anterior al préstamo.");

        return operaciones.EscribirAsync(async s =>
        {
            if (await socios.ObtenerAsync(s, socio) is not { Activo: true })
                throw new ReglaNegocioException("El socio no está activo.");
            int pendientes = await detalles.PorSocioAsync(s, socio);
            if (pendientes + seleccion.Length > LimitePendientes)
                throw new ReglaNegocioException($"El socio tiene {pendientes} libro(s) pendiente(s). El máximo es {LimitePendientes}.");

            foreach (int libro in seleccion)
            {
                var ejemplar = await libros.ObtenerAsync(s, libro);
                if (ejemplar is not { Activo: true }) throw new ReglaNegocioException("Uno de los libros no está activo.");
                if (ejemplar.Ejemplares == 0) throw new ReglaNegocioException($"«{ejemplar.Titulo}» no tiene ejemplares disponibles.");
            }

            int id = await prestamos.InsertarAsync(s, new Prestamo
            {
                SocioId = socio, FechaPrestamo = fecha, FechaLimite = limite, Estado = "Pendiente"
            });
            foreach (int libro in seleccion)
            {
                await detalles.InsertarAsync(s, new DetallePrestamo { PrestamoId = id, LibroId = libro });
                await libros.AjustarAsync(s, libro, -1);
            }
            return id;
        });
    }

    public Task<ResultadoDevolucion> DevolverAsync(int prestamo, int libro, DateTime fecha) =>
        operaciones.EscribirAsync(async s =>
        {
            var cabecera = await prestamos.ObtenerAsync(s, prestamo)
                ?? throw new ReglaNegocioException("El préstamo ya no existe.");
            var detalle = await detalles.ObtenerAsync(s, prestamo, libro);
            if (detalle is null || detalle.FechaDevolucion.HasValue)
                throw new ReglaNegocioException("Ese libro no está pendiente de devolución.");
            if (fecha.Date < cabecera.FechaPrestamo)
                throw new ReglaNegocioException("La devolución no puede ser anterior al préstamo.");
            if (await libros.ObtenerAsync(s, libro) is { Ejemplares: int.MaxValue })
                throw new ReglaNegocioException("No hay capacidad para incrementar el stock de este libro.");

            await detalles.ActualizarDevolucionAsync(s, new DetallePrestamo
            {
                PrestamoId = prestamo, LibroId = libro, FechaDevolucion = fecha
            });
            await libros.AjustarAsync(s, libro, 1);
            bool cerrado = await detalles.PorPrestamoAsync(s, prestamo) == 0;
            if (cerrado) await prestamos.CambiarEstadoAsync(s, prestamo, "Devuelto");
            int dias = Math.Max(0, (fecha.Date - cabecera.FechaLimite.Date).Days);
            return new ResultadoDevolucion(dias * MultaPorDia, dias, cerrado);
        });
}
