using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface IReporteService
{
    Task<ReporteViewModel> GetReporteAsync(DateTime desde, DateTime hasta);
}
