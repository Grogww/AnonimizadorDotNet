using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AnonimizadorDotNet.Classification;
using AnonimizadorDotNet.Classification.Rules;
using AnonimizadorDotNet.Data;
using AnonimizadorDotNet.Tests.Transformation;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Tests.Data;

public sealed class AnonymizingDataReaderTests : IDisposable
{
    private const string Cpf = "12345678909";
    private const string MaskedCpf = "123.456.789-09";

    private readonly CountingTransformer _counting = new("contado");
    private readonly OversizedTransformer _oversized = new("grande");
    private readonly TransformerSet _transformers;

    public AnonymizingDataReaderTests() =>
        _transformers = new TransformerSet(ReferenceSubstitutes.MasterKey, [_counting, _oversized]);

    public void Dispose() => _transformers.Dispose();

    [Fact]
    public void TabelaTipica_SubstituiSoAsColunasPessoais()
    {
        DataTable table = Tables.Create(
            "Clientes",
            [("Id", typeof(int)), ("Cpf", typeof(string)), ("Nome", typeof(string)), ("Observacao", typeof(string)), ("Nascimento", typeof(DateTime))],
            [1, MaskedCpf, "Maria da Silva", "cliente antiga", new DateTime(1980, 5, 1)]);
        table.Columns["Id"]!.AutoIncrement = true;
        using AnonymizingDataReader reader = Wrap(table);

        Assert.True(reader.Read());

        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(Expected(DocumentType.Cpf, MaskedCpf), reader.GetString(1));
        Assert.Equal(Expected(DocumentType.Name, "Maria da Silva"), reader.GetString(2));
        Assert.NotEqual("Maria da Silva", reader.GetString(2));
        Assert.Equal("cliente antiga", reader.GetString(3));
        Assert.Equal(new DateTime(1980, 5, 1), reader.GetDateTime(4));
        Assert.False(reader.Read());
    }

    [Fact]
    public void CpfEmColunaNumerica_ContinuaNumerico()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(long)), ("CpfDecimal", typeof(decimal))], [12345678909L, 12345678909m]);
        using AnonymizingDataReader reader = Wrap(table);
        Assert.True(reader.Read());

        long expected = long.Parse(ReferenceSubstitutes.Cpf(Cpf), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, reader.GetInt64(0));
        Assert.Equal(expected, Assert.IsType<long>(reader.GetValue(0)));
        Assert.Equal(expected, reader.GetFieldValue<long>(0));
        Assert.Equal((decimal)expected, Assert.IsType<decimal>(reader.GetValue(1)));
    }

    [Fact]
    public void TodosOsGetters_DevolvemOSubstituto()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string))], [MaskedCpf]);
        string substitute = Expected(DocumentType.Cpf, MaskedCpf);
        using AnonymizingDataReader reader = Wrap(table);
        Assert.True(reader.Read());

        Assert.Equal(substitute, reader.GetValue(0));
        Assert.Equal(substitute, reader[0]);
        Assert.Equal(substitute, reader["Cpf"]);
        Assert.Equal(substitute, reader.GetString(0));
        Assert.Equal(substitute, reader.GetFieldValue<string>(0));
        Assert.Equal(substitute, reader.GetFieldValue<object>(0));
        Assert.Equal(substitute, reader.GetProviderSpecificValue(0));
        Assert.Equal(typeof(string), reader.GetProviderSpecificFieldType(0));
        Assert.Equal(substitute, reader.GetTextReader(0).ReadToEnd());
        Assert.False(reader.IsDBNull(0));

        object[] values = new object[1];
        Assert.Equal(1, reader.GetValues(values));
        Assert.Equal(substitute, values[0]);
        Assert.Equal(1, reader.GetProviderSpecificValues(values));
        Assert.Equal(substitute, values[0]);

        Assert.Equal(substitute.Length, reader.GetChars(0, 0, null, 0, 0));
        char[] buffer = new char[substitute.Length];
        Assert.Equal(substitute.Length, reader.GetChars(0, 0, buffer, 0, buffer.Length));
        Assert.Equal(substitute, new string(buffer));
        Assert.Equal(3, reader.GetChars(0, 4, buffer, 0, 3));
        Assert.Equal(substitute[4..7], new string(buffer, 0, 3));

        Assert.Throws<InvalidCastException>(() => reader.GetInt64(0));
        Assert.Throws<InvalidCastException>(() => reader.GetBytes(0, 0, null, 0, 0));
        Assert.Throws<InvalidCastException>(() => reader.GetStream(0));
        Assert.Throws<NotSupportedException>(() => reader.GetData(0));
    }

    [Fact]
    public async Task Assincrono_DevolveOSubstituto()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string)), ("Id", typeof(int))], [MaskedCpf, 7]);
        using AnonymizingDataReader reader = Wrap(table);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Expected(DocumentType.Cpf, MaskedCpf), await reader.GetFieldValueAsync<string>(0, TestContext.Current.CancellationToken));
        Assert.False(await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken));
        Assert.Equal(7, await reader.GetFieldValueAsync<int>(1, TestContext.Current.CancellationToken));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.False(await reader.NextResultAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Enumerador_DevolveOSubstituto()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string))], [MaskedCpf]);
        using AnonymizingDataReader reader = Wrap(table);

        IDataRecord record = Assert.Single(reader.Cast<IDataRecord>());

        Assert.Equal(Expected(DocumentType.Cpf, MaskedCpf), record.GetValue(0));
    }

    [Fact]
    public void NuloEVazio_PassamSemAlteracao()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string))], [null], [string.Empty]);
        using AnonymizingDataReader reader = Wrap(table);

        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(0));
        Assert.Same(DBNull.Value, reader.GetValue(0));
        Assert.Throws<InvalidCastException>(() => reader.GetString(0));
        Assert.Equal(string.Empty, reader.GetTextReader(0).ReadToEnd());

        Assert.True(reader.Read());
        Assert.Equal(string.Empty, reader.GetString(0));
    }

    [Theory]
    [InlineData("11111111111")]
    [InlineData("123.456.789-00")]
    [InlineData("não informado")]
    public void ValorInvalido_PassaComoEsta(string value)
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string))], [value]);
        using AnonymizingDataReader reader = Wrap(table);
        Assert.True(reader.Read());

        Assert.Equal(value, reader.GetString(0));
    }

    [Fact]
    public void SubstitutoQueNaoCabeNoTipo_ViraNulo()
    {
        DataTable table = Tables.Create("Clientes", [("Codigo", typeof(int))], [123]);
        using AnonymizingDataReader reader = Wrap(table, new ColumnRuleSet([ColumnRule.Transform("Codigo", "grande")]));
        Assert.True(reader.Read());

        Assert.True(reader.IsDBNull(0));
        Assert.Same(DBNull.Value, reader.GetValue(0));
        Assert.Throws<InvalidCastException>(() => reader.GetInt32(0));
    }

    [Fact]
    public void CpfValidoEmColunaInt_NuncaSaiIntacto()
    {
        // 001.234.567-97 cabe num int; o substituto, com 11 dígitos sem zeros à esquerda, não cabe.
        const int RealCpf = 123456797;
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(int))], [RealCpf]);
        using AnonymizingDataReader reader = Wrap(table);
        Assert.True(reader.Read());

        long substitute = long.Parse(ReferenceSubstitutes.Cpf("00123456797"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(substitute > int.MaxValue, "O caso de teste precisa de um substituto que não caiba no int.");
        Assert.True(reader.IsDBNull(0));
        Assert.Same(DBNull.Value, reader.GetValue(0));
    }

    [Fact]
    public void DecimalComCasas_NaoEDocumentoEPassaComoEsta()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(decimal))], [12.5m]);
        using AnonymizingDataReader reader = Wrap(table);
        Assert.True(reader.Read());

        Assert.Equal(12.5m, reader.GetDecimal(0));
    }

    [Fact]
    public void Bloqueada_ViraNuloSemLerOOriginal()
    {
        DataTable table = Tables.Create("Clientes", [("Descricao", typeof(string)), ("Id", typeof(int))], ["texto livre", 1]);
        using var inner = new TestReader(SchemaMode.Native, table);
        using var reader = new AnonymizingDataReader(inner, Context(behavior: UnclassifiedBehavior.Block));
        Assert.True(reader.Read());

        Assert.True(reader.IsDBNull(0));
        Assert.Same(DBNull.Value, reader.GetValue(0));
        Assert.Same(DBNull.Value, reader.GetFieldValue<object>(0));
        Assert.Throws<InvalidCastException>(() => reader.GetString(0));
        Assert.Throws<InvalidCastException>(() => reader.GetChars(0, 0, new char[4], 0, 4));
        Assert.Equal(0, inner.ValueReads);
        Assert.Equal(1, reader.GetInt32(1));
    }

    [Fact]
    public void Valor_ECalculadoUmaVezPorLinha()
    {
        DataTable table = Tables.Create("Clientes", [("Codigo", typeof(string))], ["a"], ["b"]);
        using AnonymizingDataReader reader = Wrap(table, new ColumnRuleSet([ColumnRule.Transform("Codigo", "contado")]));

        Assert.True(reader.Read());
        _ = reader.GetValue(0);
        _ = reader.GetString(0);
        _ = reader.IsDBNull(0);
        Assert.Equal(1, _counting.Calls);

        Assert.True(reader.Read());
        Assert.Equal("B", reader.GetString(0));
        Assert.Equal(2, _counting.Calls);
    }

    [Fact]
    public void NextResult_ClassificaDeNovo()
    {
        DataTable first = Tables.Create("Clientes", [("Cpf", typeof(string)), ("Ativo", typeof(bool))], [MaskedCpf, true]);
        DataTable second = Tables.Create("Pedidos", [("Total", typeof(decimal)), ("Email", typeof(string))], [10m, "joao@empresa.com.br"]);
        using AnonymizingDataReader reader = Wrap(first, second);

        Assert.True(reader.Read());
        Assert.Equal(Expected(DocumentType.Cpf, MaskedCpf), reader.GetString(0));

        Assert.True(reader.NextResult());
        Assert.Equal(["#0 Pass (Policy)", "#1 Transform (ColumnName)"], reader.Decisions.Select(d => d.ToString()));
        Assert.True(reader.Read());
        Assert.Equal(10m, reader.GetDecimal(0));
        Assert.Equal(Expected(DocumentType.Email, "joao@empresa.com.br"), reader.GetString(1));
        Assert.False(reader.NextResult());
    }

    [Fact]
    public void MesmoCpfEmTabelasDiferentes_GeraOMesmoSubstituto()
    {
        DataTable clientes = Tables.Create("Clientes", [("Cpf", typeof(string))], [MaskedCpf]);
        DataTable pedidos = Tables.Create("Pedidos", [("CpfCliente", typeof(long))], [12345678909L]);
        using AnonymizingDataReader reader = Wrap(clientes, pedidos);

        Assert.True(reader.Read());
        string fromClientes = new(reader.GetString(0).Where(char.IsAsciiDigit).ToArray());
        Assert.True(reader.NextResult());
        Assert.True(reader.Read());
        long fromPedidos = reader.GetInt64(0);

        Assert.Equal(ReferenceSubstitutes.Cpf(Cpf), fromClientes);
        Assert.Equal(fromClientes, fromPedidos.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Metadados_PassamIntactos()
    {
        DataTable table = Tables.Create("Clientes", [("Id", typeof(int)), ("Cpf", typeof(string))], [1, MaskedCpf]);
        using var inner = new TestReader(SchemaMode.Native, table);
        using var reader = new AnonymizingDataReader(inner, Context());

        Assert.Equal(2, reader.FieldCount);
        Assert.Equal(2, reader.VisibleFieldCount);
        Assert.Equal("Cpf", reader.GetName(1));
        Assert.Equal(1, reader.GetOrdinal("Cpf"));
        Assert.Equal(typeof(string), reader.GetFieldType(1));
        Assert.Equal(2, reader.GetSchemaTable()!.Rows.Count);
        Assert.Equal(["Id", "Cpf"], reader.GetColumnSchema().Select(c => c.ColumnName));
        Assert.True(reader.HasRows);
    }

    [Fact]
    public void PosicaoInexistente_Lanca()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string))], [MaskedCpf]);
        using AnonymizingDataReader reader = Wrap(table);
        Assert.True(reader.Read());

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetValue(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetValue(-1));
    }

    [Fact]
    public void ContextoEncerrado_NaoDevolveValores()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string)), ("Id", typeof(int))], [MaskedCpf, 1], [MaskedCpf, 2]);
        ReaderContext context = Context();
        using var reader = new AnonymizingDataReader(new TestReader(SchemaMode.Native, table), context);
        Assert.True(reader.Read());

        context.Close();

        Assert.Throws<ObjectDisposedException>(() => reader.GetValue(0));
        Assert.Throws<ObjectDisposedException>(() => reader.GetValue(1));
        Assert.Throws<ObjectDisposedException>(() => reader.IsDBNull(0));
        Assert.Throws<ObjectDisposedException>(() => reader.Read());
        Assert.Throws<ObjectDisposedException>(() => new AnonymizingDataReader(new TestReader(SchemaMode.Native, table), context));
    }

    [Fact]
    public async Task Descartar_DescartaOReaderOriginal()
    {
        DataTable table = Tables.Create("Clientes", [("Cpf", typeof(string))]);
        var inner = new TestReader(SchemaMode.Native, table);
        new AnonymizingDataReader(inner, Context()).Dispose();
        Assert.True(inner.Disposed);

        var innerAsync = new TestReader(SchemaMode.Native, table);
        await new AnonymizingDataReader(innerAsync, Context()).DisposeAsync();
        Assert.True(innerAsync.Disposed);
    }

    [Fact]
    public void TodoMembroVirtualDoDbDataReader_ESobrescrito()
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var overridden = typeof(AnonymizingDataReader)
            .GetMethods(Flags | BindingFlags.DeclaredOnly)
            .Select(m => m.GetBaseDefinition())
            .Where(m => m.DeclaringType == typeof(DbDataReader))
            .Select(m => m.MetadataToken)
            .ToHashSet();

        string[] missing = typeof(DbDataReader)
            .GetMethods(Flags | BindingFlags.DeclaredOnly)
            .Where(m => m.IsVirtual && !m.IsFinal && !m.IsPrivate)
            .Where(m => !overridden.Contains(m.MetadataToken))
            .Select(m => m.ToString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }

    private AnonymizingDataReader Wrap(params DataTable[] tables) =>
        new(new TestReader(SchemaMode.Native, tables), Context());

    private AnonymizingDataReader Wrap(DataTable table, ColumnRuleSet rules) =>
        new(new TestReader(SchemaMode.Native, table), Context(rules));

    private ReaderContext Context(ColumnRuleSet? rules = null, UnclassifiedBehavior behavior = UnclassifiedBehavior.Pass) =>
        new(new ColumnClassifier(_transformers, rules, behavior), PassThroughScanner.Instance);

    private string Expected(DocumentType type, string value) =>
        _transformers[type].TryTransform(value, ValueKind.Text, out string? substitute)
            ? substitute
            : throw new InvalidOperationException("O valor de teste deveria ser aceito.");

    private sealed class CountingTransformer(string type) : IValueTransformer
    {
        public string Type { get; } = type;

        public int Calls { get; private set; }

        public bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute)
        {
            Calls++;
            substitute = value.ToString().ToUpperInvariant();
            return true;
        }
    }

    private sealed class OversizedTransformer(string type) : IValueTransformer
    {
        public string Type { get; } = type;

        public bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute)
        {
            substitute = "99999999999";
            return true;
        }
    }
}
