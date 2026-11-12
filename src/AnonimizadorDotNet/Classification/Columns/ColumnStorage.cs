namespace AnonimizadorDotNet.Classification.Columns;

/// <summary>Como uma coluna guarda seus valores, do ponto de vista da transformação.</summary>
internal enum ColumnStorage
{
    /// <summary>Texto (<see cref="string"/>, <see cref="char"/> ou <c>char[]</c>).</summary>
    Text,

    /// <summary>
    /// Número que pode guardar um inteiro (tipos inteiros e <see cref="decimal"/>); se cada valor é mesmo
    /// inteiro é verificado valor a valor.
    /// </summary>
    Integer,

    /// <summary>Qualquer outro tipo: datas, <see cref="bool"/>, <see cref="Guid"/>, binários, ponto flutuante.</summary>
    Other,
}

/// <summary>O armazenamento de um tipo CLR.</summary>
internal static class ColumnStorages
{
    /// <summary>O armazenamento de valores do tipo informado; tipos anuláveis valem pelo tipo de base.</summary>
    public static ColumnStorage FromType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type plain = Nullable.GetUnderlyingType(type) ?? type;

        if (plain == typeof(string) || plain == typeof(char) || plain == typeof(char[]))
        {
            return ColumnStorage.Text;
        }

        if (plain == typeof(sbyte) || plain == typeof(byte) || plain == typeof(short) || plain == typeof(ushort)
            || plain == typeof(int) || plain == typeof(uint) || plain == typeof(long) || plain == typeof(ulong)
            || plain == typeof(decimal))
        {
            return ColumnStorage.Integer;
        }

        return ColumnStorage.Other;
    }

    /// <summary>
    /// Como os valores chegam aos transformadores: números como <see cref="ValueKind.Number"/>, o resto como
    /// <see cref="ValueKind.Text"/>.
    /// </summary>
    public static ValueKind ToValueKind(this ColumnStorage storage) =>
        storage == ColumnStorage.Integer ? ValueKind.Number : ValueKind.Text;
}
