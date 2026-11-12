using System.Collections.ObjectModel;
using System.Data.Common;
using AnonimizadorDotNet.Classification.Columns;

namespace AnonimizadorDotNet.Data;

/// <summary>Lê os metadados das colunas do resultado atual de um reader, sem tocar nos valores.</summary>
/// <remarks>
/// <para>
/// A origem (schema, tabela e coluna), identity/autoincremento, tamanho e nome do tipo vêm do esquema do
/// provedor (<see cref="DbDataReaderExtensions.GetColumnSchema"/>). Quando o provedor não informa esquema,
/// ou não o informa para uma coluna, ela fica só com o nome no resultado e o tipo CLR: a classificação ainda
/// se aplica pelo nome, nunca "passa tudo" por falta de esquema.
/// </para>
/// </remarks>
internal static class ColumnSchema
{
    /// <summary>Os metadados de todas as colunas do resultado atual, na ordem das posições.</summary>
    public static ColumnInfo[] Read(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        int count = reader.FieldCount;
        if (count == 0)
        {
            return [];
        }

        Dictionary<int, DbColumn> byOrdinal = Index(TryGetSchema(reader), count);
        var columns = new ColumnInfo[count];
        for (int i = 0; i < count; i++)
        {
            string name = reader.GetName(i) ?? string.Empty;
            columns[i] = byOrdinal.TryGetValue(i, out DbColumn? column)
                ? FromSchema(i, name, column, reader)
                : new ColumnInfo(i, name, reader.GetFieldType(i));
        }

        return columns;
    }

    private static ReadOnlyCollection<DbColumn>? TryGetSchema(DbDataReader reader)
    {
        try
        {
            return reader.GetColumnSchema();
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>As colunas do esquema por posição; sem posição informada, vale a ordem do esquema.</summary>
    private static Dictionary<int, DbColumn> Index(ReadOnlyCollection<DbColumn>? schema, int count)
    {
        var byOrdinal = new Dictionary<int, DbColumn>();
        if (schema is null)
        {
            return byOrdinal;
        }

        for (int i = 0; i < schema.Count; i++)
        {
            DbColumn? column = schema[i];
            if (column is null)
            {
                continue;
            }

            int ordinal = column.ColumnOrdinal ?? i;
            if (ordinal >= 0 && ordinal < count)
            {
                byOrdinal.TryAdd(ordinal, column);
            }
        }

        return byOrdinal;
    }

    private static ColumnInfo FromSchema(int ordinal, string name, DbColumn column, DbDataReader reader) =>
        new(ordinal, name, column.DataType ?? reader.GetFieldType(ordinal))
        {
            DataTypeName = column.DataTypeName,
            BaseSchemaName = column.BaseSchemaName,
            BaseTableName = column.BaseTableName,
            BaseColumnName = column.BaseColumnName,
            IsIdentity = column.IsIdentity == true || column.IsAutoIncrement == true,
            ColumnSize = column.ColumnSize,
        };
}
