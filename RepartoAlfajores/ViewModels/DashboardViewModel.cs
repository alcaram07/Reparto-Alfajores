using RepartoAlfajores.Models;

namespace RepartoAlfajores.ViewModels;

public class DashboardViewModel
{
    public decimal TotalVendidoHoy { get; set; }
    public int CantidadVentasHoy { get; set; }
    public decimal TotalPorCobrar { get; set; }
    public decimal TotalCobradoHoy { get; set; }
    public IEnumerable<Venta> VentasHoy { get; set; } = new List<Venta>();
    public IEnumerable<DeudorViewModel> TopDeudores { get; set; } = new List<DeudorViewModel>();
}
