using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Controllers;

public class AuthController : Controller
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        if (!EsPasswordValida(vm.Password))
        {
            ModelState.AddModelError("", "Contraseña incorrecta");
            return View(vm);
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private bool EsPasswordValida(string password)
    {
        // Se hashea siempre, incluso si no hay hash configurado: si se saliera antes, el
        // tiempo de respuesta delataría que falta la configuración.
        var inputHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var storedHash = _config["Auth:PasswordHash"];

        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        byte[] esperado;
        try
        {
            esperado = Convert.FromHexString(storedHash.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        // Comparación de tiempo constante: comparar strings termina al primer carácter
        // distinto y filtra información sobre el hash.
        return CryptographicOperations.FixedTimeEquals(inputHash, esperado);
    }
}
