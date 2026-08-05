namespace RepartoAlfajores.Services;

/// <summary>
/// Una regla de negocio que no se cumplió y que el usuario puede corregir: un producto que
/// ya no existe, un monto inválido, un cliente dado de baja.
/// </summary>
/// <remarks>
/// Hereda de <see cref="Exception"/> y no de <see cref="InvalidOperationException"/> a
/// propósito. EF Core lanza <c>InvalidOperationException</c> ante errores reales de
/// infraestructura, y esos no deben mostrarse al usuario como si fueran validaciones: hay que
/// poder distinguir "faltó elegir un producto" de "se cayó la conexión a la base".
/// <para>
/// El <see cref="Filters.ManejadorDeErroresFilter"/> convierte estas excepciones en un mensaje
/// en pantalla; cualquier otra se registra en el log y termina en la página de error.
/// </para>
/// </remarks>
public class NegocioException : Exception
{
    public NegocioException(string mensaje) : base(mensaje) { }

    public NegocioException(string mensaje, Exception inner) : base(mensaje, inner) { }
}
