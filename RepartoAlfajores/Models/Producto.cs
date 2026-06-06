namespace RepartoAlfajores.Models;

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public int CategoriaId { get; set; }
    public CategoriaProducto Categoria { get; set; } = null!;
    public decimal PrecioUnitario { get; set; }
    public bool Activo { get; set; } = true;
    public ICollection<DetalleVenta> Detalles { get; set; } = null!;
}
