namespace RepartoAlfajores.ViewModels;

public class ReporteViewModel
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public string Tab { get; set; } = "mes";

    public decimal TotalVendido { get; set; }
    public int CantidadVentas { get; set; }
    public decimal TicketPromedio { get; set; }
    public decimal TotalCobrado { get; set; }
    public decimal TotalPendiente { get; set; }

    public IEnumerable<VentaDiaDto> VentasDia { get; set; } = new List<VentaDiaDto>();
    public IEnumerable<VentaZonaDto> VentasPorZona { get; set; } = new List<VentaZonaDto>();
    public IEnumerable<ProductoRankingDto> TopProductos { get; set; } = new List<ProductoRankingDto>();
    public IEnumerable<ClienteRankingDto> TopClientes { get; set; } = new List<ClienteRankingDto>();

    public string VentasDiaJson { get; set; } = "[]";
    public string VentasZonaJson { get; set; } = "[]";
}

public class VentaDiaDto
{
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public int Cantidad { get; set; }
}

public class VentaZonaDto
{
    public string Nombre { get; set; } = null!;
    public decimal Total { get; set; }
    public int Cantidad { get; set; }
}

public class ProductoRankingDto
{
    public string Nombre { get; set; } = null!;
    public int TotalCantidad { get; set; }
    public decimal TotalMonto { get; set; }
}

public class ClienteRankingDto
{
    public string Nombre { get; set; } = null!;
    public decimal TotalMonto { get; set; }
    public int TotalVentas { get; set; }
}
