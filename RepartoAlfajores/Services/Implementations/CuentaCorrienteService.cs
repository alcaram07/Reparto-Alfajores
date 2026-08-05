using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;

namespace RepartoAlfajores.Services.Implementations;

public class CuentaCorrienteService : ICuentaCorrienteService
{
    private readonly AppDbContext _db;
    public CuentaCorrienteService(AppDbContext db) => _db = db;

    public async Task<decimal> GetSaldoAsync(int clienteId) =>
        await _db.MovimientosCC
            .Where(m => m.ClienteId == clienteId)
            .OrderByDescending(m => m.Id)
            .Select(m => (decimal?)m.SaldoAcumulado)
            .FirstOrDefaultAsync() ?? 0m;

    public async Task<Dictionary<int, decimal>> GetSaldosAsync() =>
        await _db.MovimientosCC
            .GroupBy(m => m.ClienteId)
            .Select(g => new
            {
                ClienteId = g.Key,
                Saldo = g.OrderByDescending(m => m.Id).First().SaldoAcumulado
            })
            .ToDictionaryAsync(x => x.ClienteId, x => x.Saldo);

    public async Task<Dictionary<int, DateTime>> GetInicioDeudaAsync()
    {
        // Se traen sólo los campos necesarios de todos los movimientos y se recorre en memoria:
        // encontrar "la última vez que el saldo quedó en cero" no se expresa bien en SQL, y el
        // volumen de un reparto es chico.
        var movimientos = await _db.MovimientosCC
            .OrderBy(m => m.ClienteId).ThenBy(m => m.Id)
            .Select(m => new { m.ClienteId, m.Fecha, m.SaldoAcumulado })
            .ToListAsync();

        var resultado = new Dictionary<int, DateTime>();

        foreach (var grupo in movimientos.GroupBy(m => m.ClienteId))
        {
            var lista = grupo.ToList();
            if (lista[^1].SaldoAcumulado <= 0) continue; // hoy no debe nada

            // La deuda vigente arranca en el movimiento siguiente al último que dejó el saldo
            // saldado. Si nunca se saldó, arranca en el primero.
            var inicio = lista.FindLastIndex(m => m.SaldoAcumulado <= 0) + 1;
            resultado[grupo.Key] = lista[inicio].Fecha;
        }

        return resultado;
    }

    public async Task BloquearClienteAsync(int clienteId) =>
        await _db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM \"Clientes\" WHERE \"Id\" = {clienteId} FOR UPDATE");

    public async Task<MovimientoCC> RegistrarMovimientoAsync(
        int clienteId, TipoMovimientoCC tipo, decimal monto, string descripcion,
        DateTime fecha, int? ventaId = null, int? cobroId = null)
    {
        var saldoPrevio = await GetSaldoAsync(clienteId);
        var delta = tipo == TipoMovimientoCC.Cargo ? monto : -monto;

        var movimiento = new MovimientoCC
        {
            ClienteId = clienteId,
            Fecha = fecha,
            Tipo = tipo,
            Monto = monto,
            // Se permite saldo negativo a propósito: un pago mayor a la deuda queda como
            // crédito a favor del cliente en vez de perderse.
            SaldoAcumulado = saldoPrevio + delta,
            Descripcion = descripcion,
            VentaId = ventaId,
            CobroId = cobroId
        };

        _db.MovimientosCC.Add(movimiento);
        await _db.SaveChangesAsync();
        return movimiento;
    }

    public async Task RecalcularSaldosAsync(int clienteId)
    {
        var movimientos = await _db.MovimientosCC
            .Where(m => m.ClienteId == clienteId)
            .OrderBy(m => m.Id)
            .ToListAsync();

        var saldo = 0m;
        foreach (var m in movimientos)
        {
            saldo += m.Tipo == TipoMovimientoCC.Cargo ? m.Monto : -m.Monto;
            m.SaldoAcumulado = saldo;
        }

        await _db.SaveChangesAsync();
    }
}
