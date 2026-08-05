using RepartoAlfajores.Models;

namespace RepartoAlfajores.Services.Interfaces;

/// <summary>
/// Libro mayor de cuenta corriente. Centraliza el cálculo de saldos para que
/// ventas y cobros no dupliquen (ni desincronicen) la lógica.
/// </summary>
public interface ICuentaCorrienteService
{
    /// <summary>Saldo actual del cliente. Positivo = debe; negativo = tiene crédito a favor.</summary>
    Task<decimal> GetSaldoAsync(int clienteId);

    /// <summary>
    /// Saldo de todos los clientes que tienen movimientos, en una sola consulta. Los que no
    /// aparecen en el diccionario están en cero.
    /// </summary>
    /// <remarks>
    /// Existe para no llamar a <see cref="GetSaldoAsync"/> dentro de un bucle: con la base en
    /// otra región, cada consulta suelta cuesta una ida y vuelta de red.
    /// </remarks>
    Task<Dictionary<int, decimal>> GetSaldosAsync();

    /// <summary>
    /// Desde cuándo cada cliente arrastra su deuda actual: la fecha del primer movimiento
    /// posterior a la última vez que su saldo quedó en cero o a favor. Sólo incluye a los que
    /// hoy deben algo.
    /// </summary>
    Task<Dictionary<int, DateTime>> GetInicioDeudaAsync();

    /// <summary>
    /// Serializa las operaciones sobre la cuenta de un cliente. Debe llamarse dentro de una
    /// transacción, antes de leer el saldo, para que dos operaciones simultáneas no partan
    /// del mismo saldo previo y se pisen.
    /// </summary>
    Task BloquearClienteAsync(int clienteId);

    /// <summary>Agrega un movimiento al libro mayor y actualiza el saldo acumulado.</summary>
    Task<MovimientoCC> RegistrarMovimientoAsync(
        int clienteId, TipoMovimientoCC tipo, decimal monto, string descripcion,
        DateTime fecha, int? ventaId = null, int? cobroId = null);

    /// <summary>
    /// Rehace la cadena de saldos acumulados del cliente. Necesario cuando se elimina un
    /// movimiento del medio, porque <c>SaldoAcumulado</c> es un valor materializado.
    /// </summary>
    Task RecalcularSaldosAsync(int clienteId);
}
