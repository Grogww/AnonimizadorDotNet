using System.Data;

namespace AnonimizadorDotNet.Tests.Data;

/// <summary>Montagem rápida de tabelas em memória para os testes do reader.</summary>
internal static class Tables
{
    /// <summary>Uma tabela com as colunas (nome, tipo) e as linhas informadas; <see langword="null"/> vira <see cref="DBNull"/>.</summary>
    public static DataTable Create(string name, (string Name, Type Type)[] columns, params object?[][] rows)
    {
        var table = new DataTable(name);
        foreach ((string column, Type type) in columns)
        {
            table.Columns.Add(column, type);
        }

        foreach (object?[] row in rows)
        {
            table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        }

        return table;
    }
}
