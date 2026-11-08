using System.Data;
using Microsoft.Data.SqlClient;

namespace Biblioteca.Datos;

public sealed class PersistenciaException(string mensaje, Exception causa) : Exception(mensaje, causa);

public sealed class BaseDatos(string cadena)
{
    public async Task<T> LeerAsync<T>(Func<Sesion, Task<T>> operacion)
    {
        try
        {
            await using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();
            return await operacion(new Sesion(conexion, null));
        }
        catch (SqlException error) { throw Traducir(error); }
    }

    public async Task<T> EscribirAsync<T>(Func<Sesion, Task<T>> operacion)
    {
        try
        {
            await using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();
            await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync();
            var sesion = new Sesion(conexion, transaccion);

            // Serializa las escrituras del laboratorio entre instancias de la aplicación.
            await sesion.EjecutarAsync("""
                DECLARE @resultado int;
                EXEC @resultado = sys.sp_getapplock @Resource=N'Biblioteca.Escritura',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @resultado < 0 THROW 50010, N'No se obtuvo el bloqueo de escritura.', 1;
                """);

            var resultado = await operacion(sesion);
            await transaccion.CommitAsync();
            return resultado;
        }
        catch (SqlException error) { throw Traducir(error); }
    }

    private static PersistenciaException Traducir(SqlException error) => error.Number switch
    {
        2601 or 2627 => new("La base de datos rechazó un registro duplicado.", error),
        50010 => new("Otra operación está en curso. Vuelve a intentarlo.", error),
        _ => new("No se pudo completar la operación en BibliotecaDB.", error)
    };
}

public sealed class Sesion
{
    private readonly SqlConnection conexion;
    private readonly SqlTransaction? transaccion;

    internal Sesion(SqlConnection conexion, SqlTransaction? transaccion)
    {
        this.conexion = conexion;
        this.transaccion = transaccion;
    }

    private SqlCommand Comando(string sql, SqlParameter[] parametros)
    {
        var comando = new SqlCommand(sql, conexion, transaccion);
        comando.Parameters.AddRange(parametros);
        return comando;
    }

    internal async Task<List<T>> ConsultarAsync<T>(string sql, Func<SqlDataReader, T> mapear, params SqlParameter[] parametros)
    {
        using var comando = Comando(sql, parametros);
        await using var lector = await comando.ExecuteReaderAsync();
        var filas = new List<T>();
        while (await lector.ReadAsync()) filas.Add(mapear(lector));
        return filas;
    }

    internal async Task<int> EnteroAsync(string sql, params SqlParameter[] parametros)
    {
        using var comando = Comando(sql, parametros);
        return (int)(await comando.ExecuteScalarAsync())!;
    }

    internal async Task<int> EjecutarAsync(string sql, params SqlParameter[] parametros)
    {
        using var comando = Comando(sql, parametros);
        return await comando.ExecuteNonQueryAsync();
    }

    internal static SqlParameter Id(string nombre, int valor) => new(nombre, SqlDbType.Int) { Value = valor };
    internal static SqlParameter Texto(string nombre, string valor, int longitud) =>
        new(nombre, SqlDbType.NVarChar, longitud) { Value = valor };
    internal static SqlParameter Fecha(string nombre, DateTime? valor) =>
        new(nombre, SqlDbType.Date) { Value = (object?)valor?.Date ?? DBNull.Value };
    internal static SqlParameter Bit(string nombre, bool valor) => new(nombre, SqlDbType.Bit) { Value = valor };
}
