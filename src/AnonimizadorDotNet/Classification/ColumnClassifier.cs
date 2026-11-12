using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Classification.Columns;
using AnonimizadorDotNet.Classification.Rules;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Classification;

/// <summary>Decide o que fazer com cada coluna de um resultado, a partir só do esquema.</summary>
/// <remarks>
/// <para>A primeira etapa que se aplica decide:</para>
/// <list type="number">
/// <item><description>regra explícita (<see cref="ColumnRuleSet"/>): transformar, ignorar ou varrer;</description></item>
/// <item><description>chave interna (<see cref="InternalKeyGuard"/>): passa;</description></item>
/// <item><description>
/// nome da coluna (<see cref="ColumnNameHeuristic"/>), primeiro o de origem e depois o alias: transformar;
/// </description></item>
/// <item><description>coluna de texto, se a política for <see cref="UnclassifiedBehavior.Pass"/>: varrer;</description></item>
/// <item><description>política: passar ou bloquear.</description></item>
/// </list>
/// <para>
/// O classificador não muda depois de criado e pode ser usado por várias threads ao mesmo tempo. Ele não é
/// dono do <see cref="TransformerSet"/>.
/// </para>
/// </remarks>
internal sealed class ColumnClassifier
{
    private readonly TransformerSet _transformers;
    private readonly ColumnRuleSet _rules;

    /// <summary>Cria o classificador.</summary>
    /// <param name="transformers">Os transformadores disponíveis, embutidos e próprios.</param>
    /// <param name="rules">As regras explícitas; nenhuma, se omitido.</param>
    /// <param name="unclassified">O que fazer com as colunas não classificadas.</param>
    /// <exception cref="ArgumentException">Uma regra pede um tipo sem transformador registrado.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Política desconhecida.</exception>
    public ColumnClassifier(
        TransformerSet transformers, ColumnRuleSet? rules = null, UnclassifiedBehavior unclassified = UnclassifiedBehavior.Pass)
    {
        ArgumentNullException.ThrowIfNull(transformers);
        if (!Enum.IsDefined(unclassified))
        {
            throw new ArgumentOutOfRangeException(nameof(unclassified), "Política desconhecida para colunas não classificadas.");
        }

        _transformers = transformers;
        _rules = rules ?? ColumnRuleSet.Empty;
        Unclassified = unclassified;

        foreach (ColumnRule rule in _rules.Rules)
        {
            if (rule.Kind == ColumnRuleKind.Transform && !transformers.TryGet(rule.Type!, out _))
            {
                throw new ArgumentException(
                    $"Não há transformador para o tipo '{rule.Type}', pedido pela regra da coluna '{rule.Target}'.",
                    nameof(rules));
            }
        }
    }

    /// <summary>O que fazer com as colunas não classificadas.</summary>
    public UnclassifiedBehavior Unclassified { get; }

    /// <summary>As decisões de todas as colunas, na ordem das posições.</summary>
    /// <exception cref="ArgumentException">A posição de alguma coluna não corresponde ao seu índice na lista.</exception>
    public ColumnDecision[] Classify(IReadOnlyList<ColumnInfo> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var decisions = new ColumnDecision[columns.Count];
        for (int i = 0; i < decisions.Length; i++)
        {
            ColumnInfo column = columns[i] ?? throw new ArgumentException("A lista de colunas contém um item nulo.", nameof(columns));
            if (column.Ordinal != i)
            {
                throw new ArgumentException("As colunas devem vir na ordem das suas posições, a partir de zero.", nameof(columns));
            }

            decisions[i] = Classify(column);
        }

        return decisions;
    }

    /// <summary>A decisão de uma coluna.</summary>
    public ColumnDecision Classify(ColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (_rules.TryMatch(column, out ColumnRule? rule))
        {
            return rule.Kind switch
            {
                ColumnRuleKind.Transform => ColumnDecision.Transform(column, ClassificationReason.Explicit, [Resolve(rule.Type!)]),
                ColumnRuleKind.Ignore => ColumnDecision.Of(column, ColumnAction.Pass, ClassificationReason.Explicit),
                _ => ColumnDecision.Of(column, ColumnAction.Scan, ClassificationReason.Explicit),
            };
        }

        if (InternalKeyGuard.IsInternalKey(column))
        {
            return ColumnDecision.Of(column, ColumnAction.Pass, ClassificationReason.InternalKey);
        }

        if (TryMatchName(column, out IReadOnlyList<DocumentType>? candidates))
        {
            IValueTransformer[] transformers = candidates.Select(type => _transformers[type]).ToArray();
            return ColumnDecision.Transform(column, ClassificationReason.ColumnName, transformers);
        }

        if (Unclassified == UnclassifiedBehavior.Block)
        {
            return ColumnDecision.Of(column, ColumnAction.Block, ClassificationReason.Policy);
        }

        return column.Storage == ColumnStorage.Text
            ? ColumnDecision.Of(column, ColumnAction.Scan, ClassificationReason.TextScan)
            : ColumnDecision.Of(column, ColumnAction.Pass, ClassificationReason.Policy);
    }

    private static bool TryMatchName(ColumnInfo column, [NotNullWhen(true)] out IReadOnlyList<DocumentType>? candidates)
    {
        if (ColumnNameHeuristic.TryMatch(column.IdentifyingName, column.Storage, out candidates))
        {
            return true;
        }

        return column.HasOrigin && ColumnNameHeuristic.TryMatch(column.Name, column.Storage, out candidates);
    }

    private IValueTransformer Resolve(string type) =>
        _transformers.TryGet(type, out IValueTransformer? transformer)
            ? transformer
            : throw new InvalidOperationException($"O transformador do tipo '{type}' deixou de existir.");
}
