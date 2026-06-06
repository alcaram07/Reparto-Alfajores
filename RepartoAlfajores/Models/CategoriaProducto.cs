namespace RepartoAlfajores.Models;

public class CategoriaProducto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public ICollection<Producto> Productos { get; set; } = null!;
}
