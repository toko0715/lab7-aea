# Laboratorio 7 · Biblioteca

Aplicación WPF para administrar libros, socios, préstamos y devoluciones con SQL Server y ADO.NET.

## Ejecutar en Windows

Requiere .NET SDK 11, SQL Server LocalDB y `sqlcmd`. Desde la raíz:

```powershell
sqlcmd -S '(localdb)\MSSQLLocalDB' -E -C -b -f 65001 -i BibliotecaDB.sql
dotnet run --project Biblioteca.WPF
```

También puedes ejecutar `BibliotecaDB.sql` en SSMS. Si usas otra instancia de SQL Server, ajusta la cadena de conexión `BibliotecaDB` en `Biblioteca.WPF/App.config`.

## Referencias directas

- Entidades define los modelos y carece de dependencias a otros proyectos.
- Datos referencia Entidades y ejecuta SQL parametrizado mediante SqlClient.
- Negocio referencia Datos y Entidades y aplica las reglas de la biblioteca.
- WPF referencia Negocio y Entidades y espera las operaciones de forma asíncrona.

## Casos de ejemplo

- Ana Torres tiene tres libros pendientes. El sistema rechaza un nuevo préstamo y bloquea su baja.
- Todas las sangres tiene cero ejemplares disponibles.
- Una devolución tres días después de la fecha límite genera una multa de S/ 4.50.
- En una base recién instalada, el reporte del 10 al 20 de octubre de 2026 muestra siete detalles de cinco préstamos.
