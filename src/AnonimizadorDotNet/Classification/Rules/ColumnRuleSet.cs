using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Classification.Columns;

namespace AnonimizadorDotNet.Classification.Rules;

/// <summary>As regras explícitas, procuradas da mais específica para a mais geral.</summary>
/// <remarks>
/// <para>
/// Quando o provedor informa a origem da coluna, a busca usa <c>schema.tabela.coluna</c>,
/// <c>tabela.coluna</c> e <c>coluna</c> da origem, nessa ordem; só depois tenta o nome no resultado (o
/// alias). Pelo alias, valem apenas regras de transformação: um alias, que quem escreve a consulta escolhe,
/// nunca desliga a anonimização de uma coluna identificada pela origem.
/// </para>
/// <para>Sem origem, a única busca é pelo nome no resultado, com qualquer tipo de regra.</para>
/// <para>O conjunto não muda depois de criado e pode ser usado por várias threads ao mesmo tempo.</para>
/// </remarks>
internal sealed class ColumnRuleSet
{
    private readonly FrozenDictionary<string, ColumnRule> _byTarget;

    /// <summary>Indexa as regras pelo alvo.</summary>
    /// <exception cref="ArgumentException">Regra nula ou duas regras para o mesmo alvo.</exception>
    public ColumnRuleSet(IEnumerable<ColumnRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var byTarget = new Dictionary<string, ColumnRule>(StringComparer.OrdinalIgnoreCase);
        foreach (ColumnRule rule in rules)
        {
            if (rule is null)
            {
                throw new ArgumentException("A lista de regras contém um item nulo.", nameof(rules));
            }

            if (!byTarget.TryAdd(rule.Target.Key, rule))
            {
                throw new ArgumentException($"Há mais de uma regra para a coluna '{rule.Target}'.", nameof(rules));
            }
        }

        _byTarget = byTarget.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Um conjunto sem regras.</summary>
    public static ColumnRuleSet Empty { get; } = new([]);

    /// <summary>As regras do conjunto.</summary>
    public IEnumerable<ColumnRule> Rules => _byTarget.Values;

    /// <summary>A regra que se aplica à coluna, se houver.</summary>
    public bool TryMatch(ColumnInfo column, [NotNullWhen(true)] out ColumnRule? rule)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (!column.HasOrigin)
        {
            return TryGet(out rule, column.Name);
        }

        string name = column.BaseColumnName!.Trim();
        string? table = Blank(column.BaseTableName);
        string? schema = Blank(column.BaseSchemaName);

        if (table is not null && schema is not null && TryGet(out rule, schema, table, name))
        {
            return true;
        }

        if (table is not null && TryGet(out rule, table, name))
        {
            return true;
        }

        if (TryGet(out rule, name))
        {
            return true;
        }

        if (TryGet(out rule, column.Name) && rule.Kind == ColumnRuleKind.Transform)
        {
            return true;
        }

        rule = null;
        return false;
    }

    private bool TryGet([NotNullWhen(true)] out ColumnRule? rule, params string[] parts) =>
        _byTarget.TryGetValue(ColumnTarget.BuildKey(parts), out rule);

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
