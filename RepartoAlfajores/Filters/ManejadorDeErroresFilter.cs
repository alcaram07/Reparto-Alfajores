using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RepartoAlfajores.Services;

namespace RepartoAlfajores.Filters;

/// <summary>
/// Convierte una <see cref="NegocioException"/> en un mensaje que el usuario entiende, en vez
/// de la pantalla de error. Antes, una venta con un producto inexistente devolvía HTTP 500 y
/// el mensaje que el servicio se había tomado el trabajo de escribir se perdía.
/// </summary>
/// <remarks>
/// Sólo intercepta <see cref="NegocioException"/>. Cualquier otra excepción se registra y se
/// deja burbujear: si el filtro se tragara los errores de infraestructura, un bug real
/// aparecería en pantalla como si fuera una validación y nadie se enteraría.
/// </remarks>
public class ManejadorDeErroresFilter : IAsyncExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly ILogger<ManejadorDeErroresFilter> _logger;

    public ManejadorDeErroresFilter(
        ITempDataDictionaryFactory tempDataFactory,
        ILogger<ManejadorDeErroresFilter> logger)
    {
        _tempDataFactory = tempDataFactory;
        _logger = logger;
    }

    public Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not NegocioException negocio)
        {
            _logger.LogError(context.Exception, "Error no controlado en {Ruta}",
                context.HttpContext.Request.Path);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Regla de negocio no cumplida en {Ruta}: {Mensaje}",
            context.HttpContext.Request.Path, negocio.Message);

        context.ExceptionHandled = true;

        // La carga por voz espera JSON; un redirect la dejaría sin mensaje de error.
        if (EsPeticionAjax(context.HttpContext.Request))
        {
            context.Result = new JsonResult(new { error = negocio.Message });
            return Task.CompletedTask;
        }

        var tempData = _tempDataFactory.GetTempData(context.HttpContext);
        tempData["Error"] = negocio.Message;

        // Se vuelve al Index del controller donde ocurrió. No se usa el header Referer:
        // es manipulable y con SameSite=Strict no siempre llega.
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "Home";
        context.Result = new RedirectToActionResult("Index", controller, null);

        return Task.CompletedTask;
    }

    private static bool EsPeticionAjax(HttpRequest request) =>
        request.Headers["X-Requested-With"] == "XMLHttpRequest"
        || (request.Headers.Accept.Count > 0
            && request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase));
}
