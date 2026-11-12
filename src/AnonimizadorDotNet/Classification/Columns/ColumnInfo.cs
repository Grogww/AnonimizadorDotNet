namespace AnonimizadorDotNet.Classification.Columns;

/// <summary>Os metadados de uma coluna de um resultado. Nunca contém valores de linha.</summary>
/// <remarks>
/// A origem (<see cref="BaseSchemaName"/>, <see cref="BaseTableName"/>, <see cref="BaseColumnName"/>) vem do
/// esquema do reader quando o provedor a informa; ela resiste a aliases e por isso tem precedência sobre
/// <see cref="Name"/>.
/// </remarks>
internal sealed class ColumnInfo
{
    /// <summary>Cria os metadados de uma coluna.</summary>
    /// <param name="ordinal">Posição da coluna no resultado, a partir de zero.</param>
    /// <param name="name">Nome da coluna no resultado (o alias, se houver); pode ser vazio.</param>
    /// <param name="dataType">Tipo CLR dos valores.</param>
    /// <exception cref="ArgumentOutOfRangeException">Posição negativa.</exception>
    public ColumnInfo(int ordinal, string name, Type dataType)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(dataType);
        Ordinal = ordinal;
        Name = name;
        DataType = dataType;
        Storage = ColumnStorages.FromType(dataType);
    }

    /// <summary>Posição da coluna no resultado, a partir de zero.</summary>
    public int Ordinal { get; }

    /// <summary>Nome da coluna no resultado (o alias, se houver).</summary>
    public string Name { get; }

    /// <summary>Tipo CLR dos valores.</summary>
    public Type DataType { get; }

    /// <summary>Como a coluna guarda seus valores, derivado de <see cref="DataType"/>.</summary>
    public ColumnStorage Storage { get; }

    /// <summary>Nome do tipo no banco (por exemplo, <c>uniqueidentifier</c>), quando informado.</summary>
    public string? DataTypeName { get; init; }

    /// <summary>Schema da tabela de origem, quando informado.</summary>
    public string? BaseSchemaName { get; init; }

    /// <summary>Tabela de origem, quando informada.</summary>
    public string? BaseTableName { get; init; }

    /// <summary>Nome da coluna na tabela de origem, quando informado.</summary>
    public string? BaseColumnName { get; init; }

    /// <summary>Se a coluna é identity ou de autoincremento.</summary>
    public bool IsIdentity { get; init; }

    /// <summary>Tamanho máximo declarado (por exemplo, o <c>n</c> de <c>varchar(n)</c>), quando informado.</summary>
    public int? ColumnSize { get; init; }

    /// <summary>Se o provedor informou o nome da coluna de origem.</summary>
    public bool HasOrigin => !string.IsNullOrWhiteSpace(BaseColumnName);

    /// <summary>O nome que identifica a coluna: o de origem, se houver; senão, o do resultado.</summary>
    public string IdentifyingName => HasOrigin ? BaseColumnName! : Name;

    /// <summary>A posição e o nome da coluna.</summary>
    public override string ToString() => $"#{Ordinal} {Name}";
}
