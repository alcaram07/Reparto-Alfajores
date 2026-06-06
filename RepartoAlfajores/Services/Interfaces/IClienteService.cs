using Microsoft.AspNetCore.Mvc.Rendering;
using RepartoAlfajores.Models;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface IClienteService
{
    Task<IEnumerable<Cliente>> GetAllAsync(string? busqueda = null, int? zonaId = null, string? estadoDeuda = null);
    Task<Cliente?> GetByIdAsync(int id);
    Task<Cliente> CreateAsync(ClienteViewModel vm);
    Task UpdateAsync(ClienteViewModel vm);
    Task<bool> ToggleActivoAsync(int id);
    Task<decimal> GetSaldoPendienteAsync(int clienteId);
    Task<IEnumerable<Venta>> GetVentasByClienteAsync(int clienteId);
    Task<IEnumerable<Cobro>> GetCobrosByClienteAsync(int clienteId);
    Task<IEnumerable<SelectListItem>> GetSelectListAsync();
    Task<IEnumerable<SelectListItem>> GetSelectListConDeudaAsync();
}
