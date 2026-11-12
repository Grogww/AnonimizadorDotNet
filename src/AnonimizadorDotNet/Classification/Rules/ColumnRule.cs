namespace AnonimizadorDotNet.Classification.Rules;

/// <summary>O que uma regra explícita manda fazer com a coluna.</summary>
internal enum ColumnRuleKind
{
    /// <summary>Transformar com o transformador de um tipo.</summary>
    Transform,

    /// <summary>Passar intacta (dado não pessoal ou chave que a heurística não reconhece).</summary>
    Ignore,

    /// <summary>Varrer como texto livre, mesmo quando a política não varreria.</summary>
    Scan,
}

/// <summary>Uma regra de configuração explícita: alvo → ação.</summary>
internal sealed class ColumnRule
{
    private ColumnRule(ColumnTarget target, ColumnRuleKind kind, string? type)
    {
        Target = target;
        Kind = kind;
        Type = type;
    }

    /// <summary>A coluna a que a regra se aplica.</summary>
    public ColumnTarget Target { get; }

    /// <summary>O que fazer com a coluna.</summary>
    public ColumnRuleKind Kind { get; }

    /// <summary>O nome de tipo do transformador, quando <see cref="Kind"/> é <see cref="ColumnRuleKind.Transform"/>.</summary>
    public string? Type { get; }

    /// <summary>Transformar a coluna com o transformador de um tipo (embutido ou próprio).</summary>
    /// <exception cref="ArgumentException">Alvo inválido ou nome de tipo vazio.</exception>
    public static ColumnRule Transform(string target, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return new ColumnRule(ColumnTarget.Parse(target), ColumnRuleKind.Transform, type.Trim());
    }

    /// <summary>Passar a coluna intacta.</summary>
    /// <exception cref="ArgumentException">Alvo inválido.</exception>
    public static ColumnRule Ignore(string target) => new(ColumnTarget.Parse(target), ColumnRuleKind.Ignore, null);

    /// <summary>Varrer a coluna como texto livre.</summary>
    /// <exception cref="ArgumentException">Alvo inválido.</exception>
    public static ColumnRule Scan(string target) => new(ColumnTarget.Parse(target), ColumnRuleKind.Scan, null);

    /// <summary>O alvo e a ação.</summary>
    public override string ToString() => Type is null ? $"{Target} → {Kind}" : $"{Target} → {Kind} ({Type})";
}
