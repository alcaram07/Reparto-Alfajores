namespace RepartoAlfajores.Models;

public class Zona
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Activa { get; set; } = true;
    public ICollection<Cliente> Clientes { get; set; } = null!;
}
