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
}
