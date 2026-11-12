using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Classification;
using AnonimizadorDotNet.Classification.Columns;
using AnonimizadorDotNet.Classification.Rules;
using AnonimizadorDotNet.Tests.Transformation;
using AnonimizadorDotNet.Transformation;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Classification;

public sealed class ColumnClassifierTests : IDisposable
{
    private readonly TransformerSet _transformers = new(ReferenceSubstitutes.MasterKey, [new FixedTransformer("matricula")]);

    public void Dispose() => _transformers.Dispose();

    [Fact]
    public void TabelaTipica_CadaColunaRecebeUmaDecisao()
    {
        ColumnInfo[] columns =
        [
            Identity(0, "Id"),
            Origin(1, "Nome", typeof(string)),
            Origin(2, "Cpf", typeof(long)),
            Origin(3, "Email", typeof(string)),
            Origin(4, "Celular", typeof(string)),
            Origin(5, "DataNascimento", typeof(DateTime)),
            Origin(6, "Observacao", typeof(string)),
            Origin(7, "Ativo", typeof(bool)),
            Origin(8, "IdCidade", typeof(int)),
            Origin(9, "Codigo", typeof(Guid)),
        ];

        ColumnDecision[] decisions = new ColumnClassifier(_transformers).Classify(columns);

        Assert.Equal(
            [
                "#0 Pass (InternalKey)",
                "#1 Transform (ColumnName)",
                "#2 Transform (ColumnName)",
                "#3 Transform (ColumnName)",
                "#4 Transform (ColumnName)",
                "#5 Pass (Policy)",
                "#6 Scan (TextScan)",
                "#7 Pass (Policy)",
                "#8 Pass (InternalKey)",
                "#9 Pass (InternalKey)",
            ],
            decisions.Select(d => d.ToString()));
        Assert.Same(_transformers[DocumentType.Name], Assert.Single(decisions[1].Transformers));
        Assert.Same(_transformers[DocumentType.Cpf], Assert.Single(decisions[2].Transformers));
        Assert.Equal(ValueKind.Number, decisions[2].Kind);
        Assert.Same(_transformers[DocumentType.Email], Assert.Single(decisions[3].Transformers));
        Assert.Same(_transformers[DocumentType.Phone], Assert.Single(decisions[4].Transformers));
        Assert.Equal(ValueKind.Text, decisions[4].Kind);
        Assert.All(decisions.Where(d => d.Action != ColumnAction.Transform), d => Assert.Empty(d.Transformers));
    }

    [Fact]
    public void Bloquear_SoPassamChavesInternasEIgnoradas()
    {
        var rules = new ColumnRuleSet([ColumnRule.Ignore("Clientes.Ativo"), ColumnRule.Scan("Clientes.Observacao")]);
        ColumnInfo[] columns =
        [
            Identity(0, "Id"),
            Origin(1, "Cpf", typeof(string)),
            Origin(2, "Observacao", typeof(string)),
            Origin(3, "Descricao", typeof(string)),
            Origin(4, "Ativo", typeof(bool)),
            Origin(5, "DataNascimento", typeof(DateTime)),
            Origin(6, "Valor", typeof(decimal)),
        ];

        ColumnDecision[] decisions = new ColumnClassifier(_transformers, rules, UnclassifiedBehavior.Block).Classify(columns);

        Assert.Equal(
            [
                "#0 Pass (InternalKey)",
                "#1 Transform (ColumnName)",
                "#2 Scan (Explicit)",
                "#3 Block (Policy)",
                "#4 Pass (Explicit)",
                "#5 Block (Policy)",
                "#6 Block (Policy)",
            ],
            decisions.Select(d => d.ToString()));
    }

    [Fact]
    public void Explicita_VenceAProtecaoDeChave()
    {
        var rules = new ColumnRuleSet([ColumnRule.Transform("Clientes.IdCpf", "cpf")]);

        ColumnDecision decision = new ColumnClassifier(_transformers, rules).Classify(Origin(0, "IdCpf", typeof(long)));

        Assert.Equal("#0 Transform (Explicit)", decision.ToString());
        Assert.Same(_transformers[DocumentType.Cpf], Assert.Single(decision.Transformers));
    }

    [Fact]
    public void Explicita_VenceAHeuristica()
    {
        var rules = new ColumnRuleSet([ColumnRule.Ignore("Clientes.Nome"), ColumnRule.Scan("Clientes.Email")]);
        var classifier = new ColumnClassifier(_transformers, rules);

        Assert.Equal("#0 Pass (Explicit)", classifier.Classify(Origin(0, "Nome", typeof(string))).ToString());
        Assert.Equal("#0 Scan (Explicit)", classifier.Classify(Origin(0, "Email", typeof(string))).ToString());
    }

    [Fact]
    public void Explicita_ComTipoProprio_UsaOTransformadorRegistrado()
    {
        var rules = new ColumnRuleSet([ColumnRule.Transform("Alunos.Ra", "MATRICULA")]);
        Assert.True(_transformers.TryGet("matricula", out IValueTransformer? custom));

        ColumnDecision decision = new ColumnClassifier(_transformers, rules).Classify(Origin(0, "Ra", typeof(string), "Alunos"));

        Assert.Same(custom, Assert.Single(decision.Transformers));
    }

    [Fact]
    public void Explicita_ComTipoDesconhecido_LancaNaConstrucao()
    {
        var rules = new ColumnRuleSet([ColumnRule.Transform("Clientes.Rg", "rg")]);

        Assert.Throws<ArgumentException>(() => new ColumnClassifier(_transformers, rules));
    }

    [Fact]
    public void CpfOuCnpj_TentaCpfAntes()
    {
        ColumnDecision decision = new ColumnClassifier(_transformers).Classify(Origin(0, "CpfCnpj", typeof(string)));

        Assert.Equal([_transformers[DocumentType.Cpf], _transformers[DocumentType.Cnpj]], decision.Transformers);
    }

    [Fact]
    public void Heuristica_UsaAOrigemEDepoisOAlias()
    {
        var classifier = new ColumnClassifier(_transformers);
        var aliasedCpf = new ColumnInfo(0, "documento_valor", typeof(string)) { BaseColumnName = "cpf" };
        var cpfByAlias = new ColumnInfo(0, "cpf", typeof(string)) { BaseColumnName = "valor" };

        Assert.Same(_transformers[DocumentType.Cpf], Assert.Single(classifier.Classify(aliasedCpf).Transformers));
        Assert.Same(_transformers[DocumentType.Cpf], Assert.Single(classifier.Classify(cpfByAlias).Transformers));
    }

    [Fact]
    public void AliasDeId_NaoProtegeUmaColunaDeTelefone()
    {
        var aliased = new ColumnInfo(0, "id", typeof(string)) { BaseColumnName = "telefone" };

        Assert.Equal("#0 Transform (ColumnName)", new ColumnClassifier(_transformers).Classify(aliased).ToString());
    }

    [Fact]
    public void ColunaCalculadaSemOrigem_TextoVaiParaAVarredura()
    {
        var computed = new ColumnInfo(0, string.Empty, typeof(string));

        Assert.Equal("#0 Scan (TextScan)", new ColumnClassifier(_transformers).Classify(computed).ToString());
    }

    [Fact]
    public void NomesRepetidos_CadaPosicaoTemSuaDecisao()
    {
        ColumnInfo[] columns = [Origin(0, "cpf", typeof(string), "Clientes"), Origin(1, "cpf", typeof(string), "Pedidos")];

        ColumnDecision[] decisions = new ColumnClassifier(_transformers).Classify(columns);

        Assert.Equal(["#0 Transform (ColumnName)", "#1 Transform (ColumnName)"], decisions.Select(d => d.ToString()));
    }

    [Fact]
    public void ColunasForaDeOrdem_Lanca() =>
        Assert.Throws<ArgumentException>(() => new ColumnClassifier(_transformers).Classify(
            [new ColumnInfo(1, "a", typeof(string)), new ColumnInfo(0, "b", typeof(string))]));

    [Fact]
    public void PoliticaDesconhecida_Lanca() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnClassifier(_transformers, null, (UnclassifiedBehavior)7));

    [Fact]
    public void ResultadoVazio_DevolveVazio() =>
        Assert.Empty(new ColumnClassifier(_transformers).Classify([]));

    [Fact]
    public void ChavesInternas_NuncaSaoTransformadas_EmNenhumaPolitica()
    {
        string[] words = ["cpf", "cnpj", "telefone", "email", "nome", "documento"];
        Type[] types = [typeof(int), typeof(long), typeof(string), typeof(Guid)];
        var pass = new ColumnClassifier(_transformers);
        var block = new ColumnClassifier(_transformers, null, UnclassifiedBehavior.Block);

        Gen.Select(Gen.OneOfConst(words), Gen.OneOfConst(types), Gen.Bool).Sample((word, type, prefix) =>
        {
            var column = new ColumnInfo(0, prefix ? $"id_{word}" : $"{word}_id", type);

            Assert.Equal("#0 Pass (InternalKey)", pass.Classify(column).ToString());
            Assert.Equal("#0 Pass (InternalKey)", block.Classify(column).ToString());
        });
    }

    private static ColumnInfo Origin(int ordinal, string column, Type type, string table = "Clientes") =>
        new(ordinal, column, type) { BaseColumnName = column, BaseTableName = table, BaseSchemaName = "dbo" };

    private static ColumnInfo Identity(int ordinal, string column) =>
        new(ordinal, column, typeof(int))
        {
            BaseColumnName = column,
            BaseTableName = "Clientes",
            BaseSchemaName = "dbo",
            IsIdentity = true,
        };

    private sealed class FixedTransformer(string type) : IValueTransformer
    {
        public string Type { get; } = type;

        public bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute)
        {
            substitute = new string('X', value.Length);
            return true;
        }
    }
}
