using System.Data;
using System.Data.Common;
using System.Globalization;
using AnonimizadorDotNet.Tests.Data;
using AnonimizadorDotNet.Tests.Transformation;
using CsCheck;

namespace AnonimizadorDotNet.Tests;

public sealed class AnonymizerTests : IDisposable
{
    private const string Cpf = "12345678909";
    private const string MaskedCpf = "123.456.789-09";

    private readonly Anonymizer _anonymizer = new AnonymizerBuilder().UseKey(ReferenceSubstitutes.MasterKey).Build();

    public void Dispose() => _anonymizer.Dispose();

    [Fact]
    public void TryAnonymize_GeraOSubstitutoDeReferencia()
    {
        Assert.True(_anonymizer.TryAnonymize(Cpf, PersonalDataTypes.Cpf, out string? text));
        Assert.True(_anonymizer.TryAnonymize(MaskedCpf, PersonalDataTypes.Cpf, out string? masked));
        Assert.True(_anonymizer.TryAnonymize(Cpf, "CPF", ValueKind.Number, out string? number));

        string expected = ReferenceSubstitutes.Cpf(Cpf);
        Assert.Equal(expected, text);
        Assert.Equal($"{expected[..3]}.{expected[3..6]}.{expected[6..9]}-{expected[9..]}", masked);
        Assert.Equal(expected.TrimStart('0'), number);
    }

    [Fact]
    public void TryAnonymize_ValorQueNaoEDoTipo_DevolveFalso()
    {
        Assert.False(_anonymizer.TryAnonymize("11111111111", PersonalDataTypes.Cpf, out string? substitute));
        Assert.Null(substitute);
        Assert.False(_anonymizer.TryAnonymize("sem arroba", PersonalDataTypes.Email, out _));
    }

    [Fact]
    public void TryAnonymize_TipoDesconhecido_Falha()
    {
        var exception = Assert.Throws<ArgumentException>(() => _anonymizer.TryAnonymize("x", "matricula", out _));
        Assert.Equal("type", exception.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => _anonymizer.TryAnonymize("x", PersonalDataTypes.Cpf, (ValueKind)5, out _));
    }

    [Fact]
    public void TryAnonymize_NuncaLancaPorCausaDoConteudo()
    {
        string[] types = [PersonalDataTypes.Cpf, PersonalDataTypes.Cnpj, PersonalDataTypes.Phone, PersonalDataTypes.Email, PersonalDataTypes.Name];
        Gen.Select(Gen.String, Gen.Int[0, types.Length - 1], Gen.Bool).Sample((value, type, number) =>
        {
            _anonymizer.TryAnonymize(value, types[type], number ? ValueKind.Number : ValueKind.Text, out _);
        });
    }

    [Fact]
    public void MesmaChave_MesmoSubstituto_ChaveDiferente_OutroSubstituto()
    {
        byte[] otherKey = ReferenceSubstitutes.MasterKey.Select(b => (byte)(b ^ 0xFF)).ToArray();
        using Anonymizer same = new AnonymizerBuilder().UseKey(ReferenceSubstitutes.MasterKey).Build();
        using Anonymizer other = new AnonymizerBuilder().UseKey(otherKey).Build();

        Assert.True(_anonymizer.TryAnonymize("Maria da Silva", PersonalDataTypes.Name, out string? a));
        Assert.True(same.TryAnonymize("Maria da Silva", PersonalDataTypes.Name, out string? b));
        Assert.True(other.TryAnonymize("Maria da Silva", PersonalDataTypes.Name, out string? c));
        Assert.True(_anonymizer.TryAnonymize(Cpf, PersonalDataTypes.Cpf, out string? cpf));
        Assert.True(other.TryAnonymize(Cpf, PersonalDataTypes.Cpf, out string? otherCpf));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(cpf, otherCpf);
    }

    [Fact]
    public void Juncao_OMesmoCpfEmDuasTabelasGeraOMesmoSubstituto()
    {
        DataTable clientes = Tables.Create("Clientes", [("Id", typeof(int)), ("Cpf", typeof(string))], [1, MaskedCpf]);
        DataTable pedidos = Tables.Create("Pedidos", [("IdPedido", typeof(int)), ("CpfCliente", typeof(long))], [10, 12345678909L]);
        clientes.Columns["Id"]!.AutoIncrement = true;

        using DbDataReader reader = _anonymizer.Wrap(new TestReader(SchemaMode.Native, clientes, pedidos));
        Assert.True(reader.Read());
        int id = reader.GetInt32(0);
        string fromClientes = reader.GetString(1);
        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        int idPedido = reader.GetInt32(0);
        long fromPedidos = reader.GetInt64(1);

        Assert.True(_anonymizer.TryAnonymize(MaskedCpf, PersonalDataTypes.Cpf, out string? expected));
        Assert.Equal(expected, fromClientes);
        Assert.Equal(expected.Replace(".", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal),
            fromPedidos.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(1, id);
        Assert.Equal(10, idPedido);
    }

    [Fact]
    public void RegrasDoBuilder_SaoAplicadasNoReader()
    {
        DataTable table = Tables.Create(
            "Alunos",
            [("Cpf", typeof(string)), ("Matricula", typeof(string)), ("Descricao", typeof(string))],
            [MaskedCpf, "ra-123", "texto livre"]);
        using Anonymizer anonymizer = new AnonymizerBuilder()
            .UseKey(ReferenceSubstitutes.MasterKey)
            .IgnoreColumn("Alunos.Cpf")
            .AddTransformer(new AnonymizerBuilderTests.UpperTransformer("matricula"))
            .TransformColumn("Matricula", "matricula")
            .WithUnclassifiedPolicy(UnclassifiedPolicy.Block)
            .Build();

        using DbDataReader reader = anonymizer.Wrap(new TestReader(SchemaMode.Native, table));
        Assert.True(reader.Read());

        Assert.Equal(MaskedCpf, reader.GetString(0));
        Assert.Equal("RA-123", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
    }

    [Fact]
    public void Descartado_NaoDevolveMaisNada()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string))], [MaskedCpf]);
        Anonymizer anonymizer = new AnonymizerBuilder().UseKey(ReferenceSubstitutes.MasterKey).Build();
        using DbDataReader reader = anonymizer.Wrap(new TestReader(SchemaMode.Native, table));

        anonymizer.Dispose();
        anonymizer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => reader.Read());
        Assert.Throws<ObjectDisposedException>(() => anonymizer.TryAnonymize(Cpf, PersonalDataTypes.Cpf, out _));
        Assert.Throws<ObjectDisposedException>(() => anonymizer.Wrap(new TestReader(SchemaMode.Native, table)));
    }

    [Fact]
    public void Wrap_ReaderNulo_Falha() => Assert.Throws<ArgumentNullException>(() => _anonymizer.Wrap(null!));

    [Fact]
    public void AnonymizeText_UsaOsMesmosSubstitutosDoTryAnonymize()
    {
        Assert.True(_anonymizer.TryAnonymize(MaskedCpf, PersonalDataTypes.Cpf, out string? cpf));
        Assert.True(_anonymizer.TryAnonymize("maria@gmail.com", PersonalDataTypes.Email, out string? email));

        string result = _anonymizer.AnonymizeText($"Cliente {MaskedCpf}, contato maria@gmail.com.");

        Assert.Equal($"Cliente {cpf}, contato {email}.", result);
    }

    [Fact]
    public void AnonymizeText_SemDocumento_DevolveOMesmoTexto()
    {
        const string text = "nada pessoal aqui, só o pedido 42";
        Assert.Same(text, _anonymizer.AnonymizeText(text));
        Assert.Throws<ArgumentNullException>(() => _anonymizer.AnonymizeText(null!));
    }

    [Fact]
    public void Juncao_CpfNoTextoLivre_GeraOMesmoSubstitutoDaColuna()
    {
        DataTable table = Tables.Create(
            "Clientes",
            [("Cpf", typeof(string)), ("Observacao", typeof(string))],
            [MaskedCpf, $"cliente pediu revisão do CPF {Cpf} e do e-mail maria@gmail.com"]);

        using DbDataReader reader = _anonymizer.Wrap(new TestReader(SchemaMode.Native, table));
        Assert.True(reader.Read());
        string fromColumn = reader.GetString(0);
        string fromText = reader.GetString(1);

        string unmasked = fromColumn.Replace(".", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        Assert.Equal(ReferenceSubstitutes.Cpf(Cpf), unmasked);
        Assert.Contains($"do CPF {unmasked} e do e-mail ", fromText, StringComparison.Ordinal);
        Assert.DoesNotContain("maria@gmail.com", fromText, StringComparison.Ordinal);
        Assert.Equal(fromText, reader.GetFieldValue<string>(1));
        Assert.Equal(fromText, reader.GetTextReader(1).ReadToEnd());
    }

    [Theory]
    [InlineData(SchemaMode.Native)]
    [InlineData(SchemaMode.Missing)]
    public void ColunaComAliasQueEscondeOCpf_EVarrida(SchemaMode mode)
    {
        // SELECT cpf AS x: sem a origem do provedor, o nome não diz nada; a varredura protege o valor.
        DataTable table = Tables.Create("Resultado", [("x", typeof(string))], [MaskedCpf]);

        using DbDataReader reader = _anonymizer.Wrap(new TestReader(mode, table));
        Assert.True(reader.Read());

        Assert.True(_anonymizer.TryAnonymize(MaskedCpf, PersonalDataTypes.Cpf, out string? expected));
        Assert.Equal(expected, reader.GetString(0));
    }

    [Fact]
    public void PoliticaBlock_TextoNaoClassificadoContinuaBloqueado_ScanColumnVarre()
    {
        DataTable table = Tables.Create(
            "Clientes",
            [("Observacao", typeof(string)), ("Historico", typeof(string))],
            [$"CPF {Cpf}", $"CPF {Cpf}"]);
        using Anonymizer anonymizer = new AnonymizerBuilder()
            .UseKey(ReferenceSubstitutes.MasterKey)
            .WithUnclassifiedPolicy(UnclassifiedPolicy.Block)
            .ScanColumn("Historico")
            .Build();

        using DbDataReader reader = anonymizer.Wrap(new TestReader(SchemaMode.Native, table));
        Assert.True(reader.Read());

        Assert.True(reader.IsDBNull(0));
        Assert.Equal($"CPF {ReferenceSubstitutes.Cpf(Cpf)}", reader.GetString(1));
    }

    [Fact]
    public void DetectorProprio_EAplicadoNoTextoENoReader()
    {
        using Anonymizer anonymizer = new AnonymizerBuilder()
            .UseKey(ReferenceSubstitutes.MasterKey)
            .AddTransformer(new AnonymizerBuilderTests.UpperTransformer("matricula"))
            .AddDetector(new AnonymizerBuilderTests.PrefixDetector("matricula", "ra-"))
            .Build();

        Assert.Equal("aluno RA-123 aprovado", anonymizer.AnonymizeText("aluno ra-123 aprovado"));

        DataTable table = Tables.Create("Alunos", [("Descricao", typeof(string))], ["aluno ra-123 aprovado"]);
        using DbDataReader reader = anonymizer.Wrap(new TestReader(SchemaMode.Native, table));
        Assert.True(reader.Read());
        Assert.Equal("aluno RA-123 aprovado", reader.GetString(0));
    }

    [Fact]
    public void AnonymizeText_Descartado_Falha()
    {
        Anonymizer anonymizer = new AnonymizerBuilder().UseKey(ReferenceSubstitutes.MasterKey).Build();
        anonymizer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => anonymizer.AnonymizeText(Cpf));
    }
}
