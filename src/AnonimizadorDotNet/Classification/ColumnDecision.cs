using AnonimizadorDotNet.Classification.Columns;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Classification;

/// <summary>A decisão sobre uma coluna, tomada uma vez por resultado.</summary>
/// <remarks>Não guarda valores nem nomes de coluna; <see cref="ToString"/> mostra só a posição, a ação e o motivo.</remarks>
internal sealed class ColumnDecision
{
    private ColumnDecision(
        ColumnInfo column, ColumnAction action, ClassificationReason reason, IReadOnlyList<IValueTransformer> transformers)
    {
        Ordinal = column.Ordinal;
        Storage = column.Storage;
        Action = action;
        Reason = reason;
        Transformers = transformers;
    }

    /// <summary>Posição da coluna no resultado.</summary>
    public int Ordinal { get; }

    /// <summary>O que fazer com os valores.</summary>
    public ColumnAction Action { get; }

    /// <summary>Qual etapa da precedência decidiu.</summary>
    public ClassificationReason Reason { get; }

    /// <summary>Como a coluna guarda os valores.</summary>
    public ColumnStorage Storage { get; }

    /// <summary>Como os valores chegam aos transformadores.</summary>
    public ValueKind Kind => Storage.ToValueKind();

    /// <summary>
    /// Os transformadores a tentar, em ordem, quando <see cref="Action"/> é <see cref="ColumnAction.Transform"/>;
    /// vazio nas demais ações.
    /// </summary>
    public IReadOnlyList<IValueTransformer> Transformers { get; }

    /// <summary>Transformar com os transformadores informados, nessa ordem.</summary>
    public static ColumnDecision Transform(
        ColumnInfo column, ClassificationReason reason, IReadOnlyList<IValueTransformer> transformers) =>
        new(column, ColumnAction.Transform, reason, transformers);

    /// <summary>Uma ação sem transformadores (varrer, passar ou bloquear).</summary>
    public static ColumnDecision Of(ColumnInfo column, ColumnAction action, ClassificationReason reason) =>
        action == ColumnAction.Transform
            ? throw new ArgumentException("Uma transformação precisa de transformadores.", nameof(action))
            : new(column, action, reason, []);

    /// <summary>A posição, a ação e o motivo.</summary>
    public override string ToString() => $"#{Ordinal} {Action} ({Reason})";
}
