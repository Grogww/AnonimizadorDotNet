using AnonimizadorDotNet.Classification.Rules;

namespace AnonimizadorDotNet.Tests.Classification.Rules;

public sealed class ColumnTargetTests
{
    [Theory]
    [InlineData("Cpf", "Cpf")]
    [InlineData("Clientes.Cpf", "Clientes|Cpf")]
    [InlineData("dbo.Clientes.Cpf", "dbo|Clientes|Cpf")]
    [InlineData("[dbo].[Clientes].[Cpf]", "dbo|Clientes|Cpf")]
    [InlineData("\"public\".\"clientes\".\"cpf\"", "public|clientes|cpf")]
    [InlineData("`clientes`.`cpf`", "clientes|cpf")]
    [InlineData("[Minha.Tabela].Cpf", "Minha.Tabela|Cpf")]
    [InlineData(" Clientes . Cpf ", "Clientes|Cpf")]
    [InlineData("[Nome Completo]", "Nome Completo")]
    public void Parse_SeparaAsPartes(string text, string expected)
    {
        ColumnTarget target = ColumnTarget.Parse(text);

        Assert.Equal(expected.Split('|'), target.Parts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(".Cpf")]
    [InlineData("Clientes.")]
    [InlineData("a..b")]
    [InlineData("a.b.c.d")]
    [InlineData("[Clientes.Cpf")]
    [InlineData("\"Cpf")]
    [InlineData("[]")]
    public void Parse_AlvoInvalido_Lanca(string text) =>
        Assert.Throws<ArgumentException>(() => ColumnTarget.Parse(text));

    [Fact]
    public void ToString_JuntaAsPartesComPonto() =>
        Assert.Equal("dbo.Clientes.Cpf", ColumnTarget.Parse("[dbo].[Clientes].[Cpf]").ToString());
}
