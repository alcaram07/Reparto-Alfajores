using RepartoAlfajores.Models;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface ICobroService
{
    Task<IEnumerable<Cobro>> GetAllAsync(DateTime? fecha = null);
    Task<Cobro> CreateAsync(CobroViewModel vm);
    Task<IEnumerable<DeudorViewModel>> GetDeudoresAsync();
    Task<decimal> GetTotalPorCobrarAsync();
    Task<decimal> GetTotalCobradoHoyAsync();
}
