using System.Globalization;
using System.Text;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;

namespace RepartoAlfajores.Services.Implementations;

public class VentaVozService : IVentaVozService
{
    private readonly IAIService _aiService;
    private readonly IProductoService _productoService;

    public VentaVozService(IAIService aiService, IProductoService productoService)
    {
        _aiService = aiService;
        _productoService = productoService;
    }

    public async Task<VozResultado> ProcesarAudioAsync(string audioDataUri)
    {
        var resultado = new VozResultado();

        var texto = await _aiService.TranscribeAudioAsync(audioDataUri);
        if (string.IsNullOrWhiteSpace(texto))
        {
            resultado.Error = "No se pudo transcribir el audio. Intentá de nuevo.";
            return resultado;
        }
        if (texto.StartsWith("__transcription_error__:"))
        {
            resultado.Error = "Error transcribiendo el audio: " + texto["__transcription_error__: ".Length..];
            return resultado;
        }

        resultado.Transcripcion = texto;

        var productos = (await _productoService.GetAllAsync(activo: true)).ToList();
        var nombresCatalogo = productos.Select(p => p.Nombre).ToList();

        var interpretados = await _aiService.InterpretOrderAsync(texto, nombresCatalogo);
        if (interpretados.Count == 0)
        {
            resultado.Error = "La IA no reconoció productos en el audio. Probá hablando más claro.";
            return resultado;
        }

        foreach (var sugerido in interpretados)
        {
            var nombreBuscado = NormalizeText(sugerido.Producto);

            var match = productos
                .Select(p => (Producto: p, Score: ScoreMatch(NormalizeText(p.Nombre), nombreBuscado)))
                .Where(x => x.Score < int.MaxValue)
                .OrderBy(x => x.Score)
                .ThenBy(x => x.Producto.Nombre.Length)
                .Select(x => x.Producto)
                .FirstOrDefault();

            if (match == null)
            {
                resultado.NoEncontrados.Add(sugerido.Producto);
                continue;
            }

            var cantidad = sugerido.Cantidad <= 0 ? 1 : Math.Max(1, Math.Round(sugerido.Cantidad, MidpointRounding.AwayFromZero));

            var existente = resultado.Items.FirstOrDefault(i => i.ProductoId == match.Id);
            if (existente != null)
                existente.Cantidad += cantidad;
            else
                resultado.Items.Add(new ItemVozMatch
                {
                    ProductoId = match.Id,
                    Nombre = match.Nombre,
                    Cantidad = cantidad,
                    Precio = match.PrecioUnitario
                });
        }

        return resultado;
    }

    // ── Helpers de matching (reutilizados de OrderProcessorService de Puesto) ──

    private static int Levenshtein(string s, string t)
    {
        int n = s.Length, m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[n, m];
    }

    private static int ScoreMatch(string nombreDb, string nombreBuscado)
    {
        if (string.IsNullOrEmpty(nombreBuscado)) return int.MaxValue;
        if (nombreDb == nombreBuscado) return 0;

        if (nombreDb.Contains(nombreBuscado) || nombreBuscado.Contains(nombreDb)) return 10;

        var singB  = nombreBuscado.EndsWith("es") ? nombreBuscado[..^2] :
                     nombreBuscado.EndsWith("s")  ? nombreBuscado[..^1] : nombreBuscado;
        var singDb = nombreDb.EndsWith("es") ? nombreDb[..^2] :
                     nombreDb.EndsWith("s")  ? nombreDb[..^1] : nombreDb;

        if (singDb == singB || singDb == nombreBuscado || nombreDb == singB) return 0;
        if (nombreDb.Contains(singB) || singDb.Contains(singB) || singDb.Contains(nombreBuscado)) return 10;

        int minLen = Math.Min(nombreDb.Length, nombreBuscado.Length);
        if (minLen < 3) return int.MaxValue;

        int dist = Math.Min(
            Math.Min(Levenshtein(nombreDb, nombreBuscado), Levenshtein(singDb, nombreBuscado)),
            Math.Min(Levenshtein(nombreDb, singB),         Levenshtein(singDb, singB))
        );

        int threshold = minLen >= 8 ? 2 : 1;
        return dist <= threshold ? 30 + dist : int.MaxValue;
    }

    private static string NormalizeText(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Trim();
    }
}
