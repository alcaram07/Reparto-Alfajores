namespace RepartoAlfajores.Services.Interfaces;

public interface IAIService
{
    /// <summary>Transcribe un audio (data URI base64) a texto usando Groq Whisper.</summary>
    Task<string> TranscribeAudioAsync(string audioDataUri);

    /// <summary>Interpreta un texto de pedido y devuelve los ítems detectados usando Groq Llama.</summary>
    Task<List<ItemVozInterpretado>> InterpretOrderAsync(string texto, IList<string>? catalogo = null);
}

public class ItemVozInterpretado
{
    [System.Text.Json.Serialization.JsonPropertyName("producto")]
    public string Producto { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("cantidad")]
    public decimal Cantidad { get; set; }
}
