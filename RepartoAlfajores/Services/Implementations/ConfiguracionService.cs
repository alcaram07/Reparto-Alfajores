using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;

namespace RepartoAlfajores.Services.Implementations;

public class ConfiguracionService : IConfiguracionService
{
    private readonly AppDbContext _db;

    public ConfiguracionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetValorAsync(string clave)
    {
        return await _db.Configuraciones
            .Where(c => c.Clave == clave)
            .Select(c => c.Valor)
            .FirstOrDefaultAsync();
    }

    public async Task SetValorAsync(string clave, string valor)
    {
        var config = await _db.Configuraciones.FirstOrDefaultAsync(c => c.Clave == clave);
        if (config == null)
        {
            _db.Configuraciones.Add(new Configuracion { Clave = clave, Valor = valor });
        }
        else
        {
            config.Valor = valor;
        }
        await _db.SaveChangesAsync();
    }
}
