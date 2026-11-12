using AnonimizadorDotNet.Classification.Columns;
using AnonimizadorDotNet.Classification.Rules;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Tests.Classification.Rules;

public sealed class ColumnNameHeuristicTests
{
    [Theory]
    [InlineData("cpf", "Cpf")]
    [InlineData("CPF", "Cpf")]
    [InlineData("nr_cpf", "Cpf")]
    [InlineData("NrCpfCliente", "Cpf")]
    [InlineData("cpf_responsavel", "Cpf")]
    [InlineData("cnpj", "Cnpj")]
    [InlineData("CnpjEmpresa", "Cnpj")]
    [InlineData("cpf_cnpj", "Cpf,Cnpj")]
    [InlineData("CPFCNPJ", "Cpf,Cnpj")]
    [InlineData("documento", "Cpf,Cnpj")]
    [InlineData("nr_documento", "Cpf,Cnpj")]
    [InlineData("NumeroDoc", "Cpf,Cnpj")]
    [InlineData("documento_cliente", "Cpf,Cnpj")]
    [InlineData("email", "Email")]
    [InlineData("E_MAIL", "Email")]
    [InlineData("EMail", "Email")]
    [InlineData("email_contato", "Email")]
    [InlineData("mail", "Email")]
    [InlineData("telefone", "Phone")]
    [InlineData("telefone2", "Phone")]
    [InlineData("fone_residencial", "Phone")]
    [InlineData("celular", "Phone")]
    [InlineData("NrCelular", "Phone")]
    [InlineData("whatsapp", "Phone")]
    [InlineData("phone_number", "Phone")]
    [InlineData("nome", "Name")]
    [InlineData("Nome", "Name")]
    [InlineData("nome_completo", "Name")]
    [InlineData("NomeCliente", "Name")]
    [InlineData("nome_da_mae", "Name")]
    [InlineData("NomeMãe", "Name")]
    [InlineData("nm_pessoa", "Name")]
    [InlineData("nome_social", "Name")]
    [InlineData("nome_contato_2", "Name")]
    [InlineData("name", "Name")]
    [InlineData("first_name", "Name")]
    [InlineData("CustomerName", "Name")]
    [InlineData("sobrenome", "Name")]
    public void NomeIndicaDadoPessoal(string name, string expected)
    {
        Assert.True(ColumnNameHeuristic.TryMatch(name, ColumnStorage.Text, out IReadOnlyList<DocumentType>? candidates));
        Assert.Equal(expected.Split(',').Select(Enum.Parse<DocumentType>), candidates);
    }

    [Theory]
    [InlineData("observacao")]
    [InlineData("descricao")]
    [InlineData("nome_produto")]
    [InlineData("NomeArquivo")]
    [InlineData("nome_fantasia")]
    [InlineData("razao_social")]
    [InlineData("nome_usuario")]
    [InlineData("tipo_documento")]
    [InlineData("documento_url")]
    [InlineData("tipo_telefone")]
    [InlineData("ddd_telefone")]
    [InlineData("ramal")]
    [InlineData("email_status")]
    [InlineData("IsEmailVerified")]
    [InlineData("possui_cpf")]
    [InlineData("data_cadastro_cpf")]
    [InlineData("mail_server")]
    [InlineData("hotel")]
    [InlineData("cidade")]
    [InlineData("")]
    public void NomeNaoIndicaDadoPessoal(string name)
    {
        Assert.False(ColumnNameHeuristic.TryMatch(name, ColumnStorage.Text, out IReadOnlyList<DocumentType>? candidates));
        Assert.Null(candidates);
    }

    [Theory]
    [InlineData("cpf")]
    [InlineData("cnpj")]
    [InlineData("cpf_cnpj")]
    [InlineData("telefone")]
    public void Documentos_AceitamColunaInteira(string name) =>
        Assert.True(ColumnNameHeuristic.TryMatch(name, ColumnStorage.Integer, out _));

    [Theory]
    [InlineData("email")]
    [InlineData("nome")]
    public void EmailENome_ExigemTexto(string name) =>
        Assert.False(ColumnNameHeuristic.TryMatch(name, ColumnStorage.Integer, out _));

    [Theory]
    [InlineData("cpf")]
    [InlineData("telefone")]
    [InlineData("email")]
    [InlineData("nome")]
    public void ArmazenamentoIncompativel_NaoClassifica(string name) =>
        Assert.False(ColumnNameHeuristic.TryMatch(name, ColumnStorage.Other, out _));
}
