using RepartoAlfajores.Models;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface IVentaService
{
    Task<IEnumerable<Venta>> GetAllAsync(DateTime? fecha = null, string? busqueda = null, EstadoCobro? estado = null, int? zonaId = null);
    Task<Venta?> GetByIdAsync(int id);
    Task<Venta> CreateAsync(VentaViewModel vm);

    /// <summary>
    /// Actualiza los detalles y la forma de cobro de una venta, reconciliando su movimiento
    /// de cuenta corriente. Devuelve <c>false</c> si la venta ya no existe.
    /// </summary>
    /// <remarks>
    /// No permite cambiar el cliente: mover el cargo de un libro mayor a otro obliga a
    /// recalcular dos cuentas y a bloquear dos clientes a la vez.
    /// </remarks>
    Task<bool> UpdateAsync(VentaViewModel vm);

    Task<bool> DeleteAsync(int id);
}
