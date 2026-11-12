using AnonimizadorDotNet.Classification.Columns;
using AnonimizadorDotNet.Classification.Rules;

namespace AnonimizadorDotNet.Tests.Classification.Rules;

public sealed class ColumnRuleSetTests
{
    [Fact]
    public void Regra_MaisEspecificaVence()
    {
        var rules = new ColumnRuleSet(
        [
            ColumnRule.Transform("Documento", "cpf"),
            ColumnRule.Transform("Empresas.Documento", "cnpj"),
            ColumnRule.Ignore("auditoria.Empresas.Documento"),
        ]);

        Assert.Equal("cnpj", Match(rules, Origin("Documento", "Empresas", "dbo"))?.Type);
        Assert.Equal(ColumnRuleKind.Ignore, Match(rules, Origin("Documento", "Empresas", "auditoria"))?.Kind);
        Assert.Equal("cpf", Match(rules, Origin("Documento", "Clientes", "dbo"))?.Type);
        Assert.Equal("cpf", Match(rules, Origin("Documento", null, null))?.Type);
    }

    [Fact]
    public void Regra_IgnoraCaixa()
    {
        var rules = new ColumnRuleSet([ColumnRule.Transform("CLIENTES.CPF", "cpf")]);

        Assert.NotNull(Match(rules, Origin("cpf", "clientes", "dbo")));
    }

    [Fact]
    public void SemOrigem_UsaONomeNoResultado_ComQualquerRegra()
    {
        var rules = new ColumnRuleSet([ColumnRule.Ignore("observacao"), ColumnRule.Transform("doc", "cpf")]);

        Assert.Equal(ColumnRuleKind.Ignore, Match(rules, new ColumnInfo(0, "observacao", typeof(string)))?.Kind);
        Assert.Equal("cpf", Match(rules, new ColumnInfo(0, "doc", typeof(string)))?.Type);
    }

    [Fact]
    public void SemOrigem_RegraComTabela_NaoCasa()
    {
        var rules = new ColumnRuleSet([ColumnRule.Transform("Clientes.Documento", "cpf")]);

        Assert.Null(Match(rules, new ColumnInfo(0, "Documento", typeof(string))));
    }

    [Fact]
    public void ComOrigem_OrigemVenceOAlias()
    {
        var rules = new ColumnRuleSet([ColumnRule.Transform("cpf", "cpf"), ColumnRule.Transform("x", "phone")]);

        Assert.Equal("cpf", Match(rules, Origin("cpf", "Clientes", "dbo", alias: "x"))?.Type);
    }

    [Fact]
    public void ComOrigem_AliasSoAplicaTransformacao()
    {
        var rules = new ColumnRuleSet([ColumnRule.Ignore("seguro"), ColumnRule.Scan("varrer"), ColumnRule.Transform("doc", "cpf")]);

        Assert.Null(Match(rules, Origin("cpf", "Clientes", "dbo", alias: "seguro")));
        Assert.Null(Match(rules, Origin("cpf", "Clientes", "dbo", alias: "varrer")));
        Assert.Equal("cpf", Match(rules, Origin("numero", "Clientes", "dbo", alias: "doc"))?.Type);
    }

    [Fact]
    public void SemRegra_DevolveFalso()
    {
        Assert.False(ColumnRuleSet.Empty.TryMatch(Origin("cpf", "Clientes", "dbo"), out ColumnRule? rule));
        Assert.Null(rule);
    }

    [Fact]
    public void RegrasParaOMesmoAlvo_Lanca() =>
        Assert.Throws<ArgumentException>(() => new ColumnRuleSet(
            [ColumnRule.Transform("Clientes.Cpf", "cpf"), ColumnRule.Ignore("[clientes].[cpf]")]));

    [Fact]
    public void RegraNula_Lanca() =>
        Assert.Throws<ArgumentException>(() => new ColumnRuleSet([null!]));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void TransformacaoSemTipo_Lanca(string type) =>
        Assert.Throws<ArgumentException>(() => ColumnRule.Transform("cpf", type));

    [Fact]
    public void ToString_MostraAlvoEAcao()
    {
        Assert.Equal("Clientes.Cpf → Transform (cpf)", ColumnRule.Transform("Clientes.Cpf", " cpf ").ToString());
        Assert.Equal("Obs → Ignore", ColumnRule.Ignore("Obs").ToString());
    }

    private static ColumnRule? Match(ColumnRuleSet rules, ColumnInfo column) =>
        rules.TryMatch(column, out ColumnRule? rule) ? rule : null;

    private static ColumnInfo Origin(string column, string? table, string? schema, string? alias = null) =>
        new(0, alias ?? column, typeof(string))
        {
            BaseColumnName = column,
            BaseTableName = table,
            BaseSchemaName = schema,
        };
}
