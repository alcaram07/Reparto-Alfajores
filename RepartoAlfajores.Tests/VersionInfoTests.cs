using RepartoAlfajores.Utils;

namespace RepartoAlfajores.Tests;

public class VersionInfoTests
{
    [Fact]
    public void Usa_el_sha_que_inyecta_render_acortado_a_siete()
    {
        var commit = VersionInfo.ResolverCommit("9b6d82b1c4e5f60718293a4b5c6d7e8f90a1b2c3", null);

        Assert.Equal("9b6d82b", commit);
    }

    [Fact]
    public void El_sha_de_render_tiene_prioridad_sobre_la_version_del_ensamblado()
    {
        var commit = VersionInfo.ResolverCommit("9b6d82b1c4e5", "1.0.0+aaaaaaabbbbbb");

        Assert.Equal("9b6d82b", commit);
    }

    [Fact]
    public void Fuera_de_render_cae_al_sha_embebido_en_la_version_del_ensamblado()
    {
        var commit = VersionInfo.ResolverCommit(null, "1.0.0+79afdeb0011223344556677889900aabbccddee");

        Assert.Equal("79afdeb", commit);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "1.0.0")]        // version sin '+', no trae sha
    [InlineData("   ", "1.0.0+")]    // '+' al final, sin nada después
    public void Sin_ninguna_fuente_informa_que_no_lo_conoce(string? render, string? version)
    {
        Assert.Equal("desconocido", VersionInfo.ResolverCommit(render, version));
    }

    [Fact]
    public void Un_sha_mas_corto_que_siete_se_devuelve_entero()
    {
        Assert.Equal("abc12", VersionInfo.ResolverCommit("abc12", null));
    }
}
