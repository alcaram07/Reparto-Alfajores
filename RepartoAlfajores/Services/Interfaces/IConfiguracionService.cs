namespace RepartoAlfajores.Services.Interfaces;

public interface IConfiguracionService
{
    Task<string?> GetValorAsync(string clave);
    Task SetValorAsync(string clave, string valor);
}
