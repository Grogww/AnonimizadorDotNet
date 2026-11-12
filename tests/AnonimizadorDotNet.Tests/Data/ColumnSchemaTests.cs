using System.Data;
using AnonimizadorDotNet.Classification.Columns;
using AnonimizadorDotNet.Data;

namespace AnonimizadorDotNet.Tests.Data;

public sealed class ColumnSchemaTests
{
    [Fact]
    public void ComEsquema_LeOrigemIdentityTamanhoETipo()
    {
        DataTable table = Tables.Create("Clientes", [("Id", typeof(int)), ("Cpf", typeof(string)), ("Nome", typeof(string))]);
        table.Columns["Id"]!.AutoIncrement = true;
        table.Columns["Nome"]!.MaxLength = 60;
        using var reader = new TestReader(SchemaMode.Native, table);

        ColumnInfo[] columns = ColumnSchema.Read(reader);

        Assert.Equal([0, 1, 2], columns.Select(c => c.Ordinal));
        Assert.Equal(["Id", "Cpf", "Nome"], columns.Select(c => c.Name));
        Assert.All(columns, c => Assert.Equal("Clientes", c.BaseTableName));
        Assert.Equal(["Id", "Cpf", "Nome"], columns.Select(c => c.BaseColumnName));
        Assert.True(columns[0].IsIdentity);
        Assert.False(columns[1].IsIdentity);
        Assert.Equal(typeof(int), columns[0].DataType);
        Assert.Equal(ColumnStorage.Integer, columns[0].Storage);
        Assert.Equal(ColumnStorage.Text, columns[1].Storage);
        Assert.Equal(60, columns[2].ColumnSize);
    }

    [Theory]
    [InlineData(SchemaMode.Missing)]
    [InlineData(SchemaMode.Unsupported)]
    public void SemEsquema_FicaComNomeETipo(SchemaMode mode)
    {
        DataTable table = Tables.Create("Clientes", [("Id", typeof(long)), ("cpf", typeof(string))]);
        using var reader = new TestReader(mode, table);

        ColumnInfo[] columns = ColumnSchema.Read(reader);

        Assert.Equal(["Id", "cpf"], columns.Select(c => c.Name));
        Assert.Equal([typeof(long), typeof(string)], columns.Select(c => c.DataType));
        Assert.All(columns, c => Assert.False(c.HasOrigin));
        Assert.All(columns, c => Assert.False(c.IsIdentity));
    }

    [Fact]
    public void ResultadoSemColunas_DevolveVazio()
    {
        using var reader = new TestReader(SchemaMode.Native, new DataTable("Vazia"));

        Assert.Empty(ColumnSchema.Read(reader));
    }

    [Fact]
    public void NaoLeValores()
    {
        DataTable table = Tables.Create("Clientes", [("cpf", typeof(string))], ["12345678909"]);
        using var reader = new TestReader(SchemaMode.Native, table);
        Assert.True(reader.Read());

        ColumnSchema.Read(reader);

        Assert.Equal(0, reader.ValueReads);
    }
}
