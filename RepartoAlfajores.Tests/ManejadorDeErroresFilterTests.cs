using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using RepartoAlfajores.Filters;
using RepartoAlfajores.Services;

namespace RepartoAlfajores.Tests;

/// <summary>
/// No levanta el host: arma un <see cref="ExceptionContext"/> a mano, así corre sin base
/// de datos ni servidor.
/// </summary>
public class ManejadorDeErroresFilterTests
{
    private readonly TempDataFactoryFalsa _tempData = new();

    private ManejadorDeErroresFilter CrearFiltro() =>
        new(_tempData, NullLogger<ManejadorDeErroresFilter>.Instance);

    private static ExceptionContext CrearContexto(
        Exception excepcion, string controller = "Ventas", string? accept = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/Ventas/Nuevo";
        if (accept is not null)
            httpContext.Request.Headers.Accept = accept;

        var routeData = new RouteData();
        routeData.Values["controller"] = controller;

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        return new ExceptionContext(actionContext, []) { Exception = excepcion };
    }

    [Fact]
    public void Una_regla_de_negocio_redirige_al_index_con_el_mensaje()
    {
        var contexto = CrearContexto(new NegocioException("Producto 999 no encontrado"));

        CrearFiltro().OnExceptionAsync(contexto).Wait();

        Assert.True(contexto.ExceptionHandled);
        var redirect = Assert.IsType<RedirectToActionResult>(contexto.Result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Ventas", redirect.ControllerName);
        Assert.Equal("Producto 999 no encontrado", _tempData.Ultimo?["Error"]);
    }

    /// <summary>
    /// El punto del diseño: un fallo de infraestructura no debe mostrarse como si fuera una
    /// validación. Si el filtro se lo tragara, un bug real pasaría desapercibido.
    /// </summary>
    [Fact]
    public void Una_excepcion_de_infraestructura_no_se_captura()
    {
        var contexto = CrearContexto(new InvalidOperationException("se cayó la conexión"));

        CrearFiltro().OnExceptionAsync(contexto).Wait();

        Assert.False(contexto.ExceptionHandled);
        Assert.Null(contexto.Result);
    }

    [Fact]
    public void Una_peticion_que_espera_json_recibe_json_y_no_un_redirect()
    {
        var contexto = CrearContexto(
            new NegocioException("No se pudo interpretar el audio"),
            accept: "application/json");

        CrearFiltro().OnExceptionAsync(contexto).Wait();

        Assert.True(contexto.ExceptionHandled);
        Assert.IsType<JsonResult>(contexto.Result);
    }

    [Fact]
    public void Una_peticion_ajax_recibe_json_y_no_un_redirect()
    {
        var contexto = CrearContexto(new NegocioException("error"));
        contexto.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        CrearFiltro().OnExceptionAsync(contexto).Wait();

        Assert.IsType<JsonResult>(contexto.Result);
    }

    [Fact]
    public void Sin_controller_en_la_ruta_se_redirige_a_Home()
    {
        var contexto = CrearContexto(new NegocioException("error"), controller: null!);
        contexto.RouteData.Values.Remove("controller");

        CrearFiltro().OnExceptionAsync(contexto).Wait();

        var redirect = Assert.IsType<RedirectToActionResult>(contexto.Result);
        Assert.Equal("Home", redirect.ControllerName);
    }

    private sealed class TempDataFactoryFalsa : ITempDataDictionaryFactory
    {
        public ITempDataDictionary? Ultimo { get; private set; }

        public ITempDataDictionary GetTempData(HttpContext context) =>
            Ultimo ??= new TempDataDictionary(context, new ProveedorFalso());

        private sealed class ProveedorFalso : ITempDataProvider
        {
            public IDictionary<string, object?> LoadTempData(HttpContext context) =>
                new Dictionary<string, object?>();

            public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
        }
    }
}
