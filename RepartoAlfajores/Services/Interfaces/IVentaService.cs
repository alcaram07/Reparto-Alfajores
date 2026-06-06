using RepartoAlfajores.Models;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface IVentaService
{
    Task<IEnumerable<Venta>> GetAllAsync(DateTime? fecha = null, string? busqueda = null, EstadoCobro? estado = null, int? zonaId = null);
    Task<Venta?> GetByIdAsync(int id);
    Task<Venta> CreateAsync(VentaViewModel vm);
    Task<bool> DeleteAsync(int id);
}
