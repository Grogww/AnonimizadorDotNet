using System.Collections.Frozen;
using AnonimizadorDotNet.Classification.Columns;

namespace AnonimizadorDotNet.Classification.Rules;

/// <summary>Reconhece as chaves internas do sistema, que passam intactas e mantêm os relacionamentos.</summary>
/// <remarks>
/// <para>
/// É chave interna a coluna identity ou de autoincremento, a de tipo <see cref="Guid"/> (ou declarada como
/// <c>uniqueidentifier</c>, <c>uuid</c> ou <c>rowversion</c>) e a que tem nome de ID: começa ou termina com
/// <c>id</c>, <c>pk</c>, <c>fk</c>, <c>uuid</c> ou <c>guid</c> (<c>id_usuario</c>, <c>IdTelefone</c>,
/// <c>cliente_id</c>, <c>ClienteId</c>). O nome usado é o de origem, quando o provedor o informa, para que
/// um alias como <c>telefone AS id</c> não desligue a anonimização.
/// </para>
/// <para>
/// Ser chave primária não basta: um CPF pode ser chave natural, e o FF1, por ser bijetor, mantém a
/// unicidade e as junções depois da transformação. Uma regra explícita tem precedência sobre esta proteção.
/// </para>
/// </remarks>
internal static class InternalKeyGuard
{
    private static readonly FrozenSet<string> KeyTokens =
        new[] { "id", "pk", "fk", "uuid", "guid" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> KeyTypeNames =
        new[] { "uniqueidentifier", "uuid", "rowversion" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Se a coluna é uma chave interna do sistema.</summary>
    public static bool IsInternalKey(ColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (column.IsIdentity)
        {
            return true;
        }

        Type plain = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
        if (plain == typeof(Guid))
        {
            return true;
        }

        if (column.DataTypeName is { } typeName && KeyTypeNames.Contains(typeName.Trim()))
        {
            return true;
        }

        return HasKeyName(ColumnNameTokens.Split(column.IdentifyingName));
    }

    /// <summary>Se as partes do nome indicam um ID; dígitos no fim (<c>cliente_id_2</c>) não contam.</summary>
    public static bool HasKeyName(IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        int last = tokens.Count - 1;
        while (last >= 0 && char.IsAsciiDigit(tokens[last][0]))
        {
            last--;
        }

        return last >= 0 && (KeyTokens.Contains(tokens[0]) || KeyTokens.Contains(tokens[last]));
    }
}
