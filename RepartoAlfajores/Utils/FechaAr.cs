namespace RepartoAlfajores.Utils;

/// <summary>
/// Las fechas se guardan siempre en UTC, pero el negocio opera en hora argentina (UTC-3).
/// Sin esta conversión, una venta cargada después de las 21:00 local se registraba con
/// fecha del día siguiente y quedaba fuera del cierre del día.
/// </summary>
public static class FechaAr
{
    private static readonly TimeZoneInfo Zona = ResolverZona();

    private static TimeZoneInfo ResolverZona()
    {
        // El id IANA funciona en Linux (Docker/Render); el de Windows en desarrollo local.
        foreach (var id in new[] { "America/Argentina/Buenos_Aires", "Argentina Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Argentina no usa horario de verano desde 2009, así que el offset fijo es seguro.
        return TimeZoneInfo.CreateCustomTimeZone(
            "AR-Fallback", TimeSpan.FromHours(-3), "Argentina (UTC-3)", "Argentina (UTC-3)");
    }

    /// <summary>El día de hoy según el calendario argentino.</summary>
    public static DateTime Hoy => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona).Date;

    /// <summary>
    /// Convierte un día del calendario argentino al rango [Desde, Hasta) en UTC,
    /// que es como están guardadas las fechas en la base.
    /// </summary>
    public static (DateTime Desde, DateTime Hasta) RangoDia(DateTime diaLocal) =>
        (AUtc(diaLocal.Date), AUtc(diaLocal.Date.AddDays(1)));

    /// <summary>Igual que <see cref="RangoDia"/> pero para un rango de días, ambos inclusive.</summary>
    public static (DateTime Desde, DateTime Hasta) Rango(DateTime desdeLocal, DateTime hastaLocal) =>
        (AUtc(desdeLocal.Date), AUtc(hastaLocal.Date.AddDays(1)));

    /// <summary>Interpreta una fecha del calendario argentino y devuelve su equivalente UTC.</summary>
    public static DateTime AUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zona);

    /// <summary>Convierte una fecha UTC de la base a hora argentina, para mostrarla.</summary>
    public static DateTime ALocal(this DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    /// <inheritdoc cref="ALocal(DateTime)"/>
    public static DateTime? ALocal(this DateTime? utc) => utc?.ALocal();
}
