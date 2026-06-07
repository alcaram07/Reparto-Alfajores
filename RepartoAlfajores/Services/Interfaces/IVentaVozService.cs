namespace RepartoAlfajores.Services.Interfaces;

public interface IVentaVozService
{
    Task<VozResultado> ProcesarAudioAsync(string audioDataUri);
}

public class VozResultado
{
    public string Transcripcion { get; set; } = string.Empty;
    public List<ItemVozMatch> Items { get; set; } = new();
    public List<string> NoEncontrados { get; set; } = new();
    public string? Error { get; set; }
}

public class ItemVozMatch
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal Precio { get; set; }
}
