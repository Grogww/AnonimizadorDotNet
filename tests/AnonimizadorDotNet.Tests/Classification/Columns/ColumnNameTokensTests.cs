using AnonimizadorDotNet.Classification.Columns;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Classification.Columns;

public sealed class ColumnNameTokensTests
{
    [Theory]
    [InlineData("cpf", "cpf")]
    [InlineData("CPF", "cpf")]
    [InlineData("nr_cpf", "nr cpf")]
    [InlineData("NrCpfCliente", "nr cpf cliente")]
    [InlineData("nrCpfCliente", "nr cpf cliente")]
    [InlineData("CPFCliente", "cpf cliente")]
    [InlineData("IdCPF", "id cpf")]
    [InlineData("CPF_CNPJ", "cpf cnpj")]
    [InlineData("CPFCNPJ", "cpfcnpj")]
    [InlineData("E_MAIL", "e mail")]
    [InlineData("e-mail", "e mail")]
    [InlineData("EMail", "e mail")]
    [InlineData("telefone2", "telefone 2")]
    [InlineData("Telefone_2_Contato", "telefone 2 contato")]
    [InlineData("NomeMãe", "nome mae")]
    [InlineData("situação", "situacao")]
    [InlineData("[dbo].[Cliente Nome]", "dbo cliente nome")]
    [InlineData("  __id__  ", "id")]
    public void Split_QuebraEmPartesMinusculasSemAcento(string name, string expected) =>
        Assert.Equal(expected.Split(' '), ColumnNameTokens.Split(name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("_-.")]
    public void Split_SemLetrasNemDigitos_DevolveVazio(string name) =>
        Assert.Empty(ColumnNameTokens.Split(name));

    [Fact]
    public void Split_QualquerTexto_NuncaLancaENaoDevolvePartesVazias()
    {
        Gen.String.Sample(name =>
        {
            string[] tokens = ColumnNameTokens.Split(name);
            Assert.All(tokens, token => Assert.NotEqual(string.Empty, token));
        });
    }
}
