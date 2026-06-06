using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardDataAsync();
}
