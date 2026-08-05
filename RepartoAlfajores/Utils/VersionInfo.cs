using System.Reflection;

namespace RepartoAlfajores.Utils;

/// <summary>
/// Identifica qué build está corriendo, para poder confirmar un deploy sin entrar al
/// dashboard de Render. Se expone en <c>/health</c>.
/// </summary>
public static class VersionInfo
{
    public static string Commit { get; } = ResolverCommit(
        Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT"),
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static string Rama { get; } =
        Environment.GetEnvironmentVariable("RENDER_GIT_BRANCH") is { Length: > 0 } rama
            ? rama
            : "local";

    /// <param name="renderCommit">SHA que Render inyecta en el contenedor desplegado.</param>
    /// <param name="informationalVersion">
    /// Version del ensamblado. El SDK le agrega <c>+&lt;sha&gt;</c> cuando se compila pasando
    /// <c>-p:SourceRevisionId</c>; dentro de Docker no hay repo git, así que suele venir sin SHA.
    /// </param>
    public static string ResolverCommit(string? renderCommit, string? informationalVersion)
    {
        if (!string.IsNullOrWhiteSpace(renderCommit))
            return Acortar(renderCommit);

        var mas = informationalVersion?.IndexOf('+') ?? -1;
        if (mas >= 0 && informationalVersion!.Length > mas + 1)
            return Acortar(informationalVersion[(mas + 1)..]);

        return "desconocido";
    }

    private static string Acortar(string sha)
    {
        var limpio = sha.Trim();
        return limpio.Length > 7 ? limpio[..7] : limpio;
    }
}
