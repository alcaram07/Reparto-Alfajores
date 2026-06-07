using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RepartoAlfajores.Services.Interfaces;

namespace RepartoAlfajores.Services.Implementations;

public class AIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IConfiguracionService _configuracionService;
    private readonly ILogger<AIService> _logger;

    private const string GroqAudioUrl = "https://api.groq.com/openai/v1/audio/transcriptions";
    private const string GroqChatUrl  = "https://api.groq.com/openai/v1/chat/completions";
    private const string GroqAudioModel = "whisper-large-v3-turbo";
    private const string GroqChatModel  = "llama-3.3-70b-versatile";

    public AIService(HttpClient httpClient, IConfiguration configuration,
        IConfiguracionService configuracionService, ILogger<AIService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _configuracionService = configuracionService;
        _logger = logger;
    }

    // Prioridad: clave guardada en la DB (pantalla de Configuración) → env var → appsettings.
    private async Task<string> GetGroqApiKeyAsync()
    {
        var dbKey = await _configuracionService.GetValorAsync("GroqApiKey");
        if (!string.IsNullOrWhiteSpace(dbKey)) return dbKey;

        return Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? _configuration["Groq:ApiKey"]
            ?? "";
    }

    public async Task<string> TranscribeAudioAsync(string audioDataUri)
    {
        if (string.IsNullOrEmpty(audioDataUri)) return "";

        string base64Data;
        string mimeType;

        try
        {
            if (audioDataUri.StartsWith("data:"))
            {
                var header = audioDataUri[5..audioDataUri.IndexOf(',')];
                mimeType = header[..header.IndexOf(';')];
                base64Data = audioDataUri[(audioDataUri.IndexOf(',') + 1)..];
            }
            else
            {
                var bytes = await _httpClient.GetByteArrayAsync(audioDataUri);
                base64Data = Convert.ToBase64String(bytes);
                var ext = Path.GetExtension(audioDataUri).ToLowerInvariant().TrimStart('.');
                mimeType = ext switch { "mp3" => "audio/mp3", "m4a" => "audio/mp4",
                                        "wav" => "audio/wav", "webm" => "audio/webm",
                                        _ => "audio/ogg" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando audio en TranscribeAudioAsync");
            return $"__transcription_error__: [Exception] {ex.Message}";
        }

        var audioBytes = Convert.FromBase64String(base64Data);
        var audioExt = mimeType switch {
            "audio/mp3" or "audio/mpeg" => "mp3",
            "audio/mp4" or "audio/m4a"  => "m4a",
            "audio/wav"                 => "wav",
            "audio/webm"                => "webm",
            _                           => "ogg"
        };

        var apiKey = await GetGroqApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
            return "__transcription_error__: [NoApiKey] Falta configurar la API key de Groq en Configuración.";

        for (int attempt = 0; attempt <= 2; attempt++)
        {
            try
            {
                if (attempt > 0) await Task.Delay(2000 * attempt);

                using var form = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(audioBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                form.Add(fileContent, "file", $"audio.{audioExt}");
                form.Add(new StringContent(GroqAudioModel), "model");

                var requestMsg = new HttpRequestMessage(HttpMethod.Post, GroqAudioUrl);
                requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                requestMsg.Content = form;

                var response = await _httpClient.SendAsync(requestMsg);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        return doc.RootElement.GetProperty("text").GetString()?.Trim() ?? "";
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogError(parseEx, "Error parseando respuesta Groq audio");
                        return $"__transcription_error__: [ParseError] {(body.Length > 300 ? body[..300] : body)}";
                    }
                }
                else if ((int)response.StatusCode == 503 && attempt < 2)
                {
                    _logger.LogWarning("Groq audio 503, reintentando ({Attempt}/2)...", attempt + 1);
                    continue;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error transcribiendo audio con Groq: {Status} - {Error}", response.StatusCode, error);
                    return $"__transcription_error__: [{response.StatusCode}] {error}";
                }
            }
            catch (HttpRequestException) when (attempt < 2)
            {
                _logger.LogWarning("Error de red transcribiendo audio, reintentando ({Attempt}/2)...", attempt + 1);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en TranscribeAudioAsync");
                return $"__transcription_error__: [Exception] {ex.Message}";
            }
        }

        return "__transcription_error__: [Unavailable] El servicio de IA no está disponible. Reintentá en unos minutos.";
    }

    public async Task<List<ItemVozInterpretado>> InterpretOrderAsync(string texto, IList<string>? catalogo = null)
    {
        if (string.IsNullOrEmpty(texto)) return new List<ItemVozInterpretado>();

        try
        {
            var catalogLine = catalogo?.Count > 0
                ? $"\nProductos del catálogo: {string.Join(", ", catalogo)}."
                : "";

            var promptText = $@"Actúa como un extractor de datos para un negocio de reparto de alfajores y masas dulces.
Analiza el siguiente texto de un pedido y devuelve una lista JSON de objetos con las propiedades 'producto' (texto) y 'cantidad' (número entero).{catalogLine}
Reglas importantes:
- Para 'producto': el cliente puede cometer errores de pronunciación u ortografía. Identifica a qué producto se refiere por similitud FONÉTICA (cómo suena). Usa el nombre exacto del catálogo si lo identificas; si no, usa el texto del cliente.
- Los productos se venden por unidad. Sin cantidad explícita → 1.
- 'una docena' = 12, 'media docena' = 6.
- Devuelve SOLO el JSON, sin bloques de código ni texto adicional.

Texto del pedido: ""{texto}""";

            var request = new
            {
                model = GroqChatModel,
                messages = new[] { new { role = "user", content = promptText } },
                temperature = 0
            };

            var interpretMsg = new HttpRequestMessage(HttpMethod.Post, GroqChatUrl);
            interpretMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetGroqApiKeyAsync());
            interpretMsg.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(interpretMsg);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);
                var aiText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                if (!string.IsNullOrEmpty(aiText))
                {
                    var jsonMatch = Regex.Match(aiText, @"\[.*\]", RegexOptions.Singleline);
                    var cleanJson = jsonMatch.Success ? jsonMatch.Value : aiText;
                    return JsonSerializer.Deserialize<List<ItemVozInterpretado>>(cleanJson) ?? new List<ItemVozInterpretado>();
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error llamando a Groq chat: {Status} - {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error interpretando pedido con Groq");
        }

        return new List<ItemVozInterpretado>();
    }
}
