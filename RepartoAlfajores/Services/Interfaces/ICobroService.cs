using RepartoAlfajores.Models;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface ICobroService
{
    Task<IEnumerable<Cobro>> GetAllAsync(DateTime? fecha = null);
    Task<Cobro> CreateAsync(CobroViewModel vm);

    /// <summary>
    /// Elimina el cobro, su abono en cuenta corriente, y rehace la cadena de saldos del
    /// cliente. Devuelve <c>false</c> si el cobro ya no existe.
    /// </summary>
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<DeudorViewModel>> GetDeudoresAsync();
    Task<decimal> GetTotalPorCobrarAsync();
    Task<decimal> GetTotalCobradoHoyAsync();
}
