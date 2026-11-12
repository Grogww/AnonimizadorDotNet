using AnonimizadorDotNet.Classification.Columns;

namespace AnonimizadorDotNet.Tests.Classification.Columns;

public sealed class ColumnInfoTests
{
    [Theory]
    [InlineData(typeof(string), nameof(ColumnStorage.Text))]
    [InlineData(typeof(char), nameof(ColumnStorage.Text))]
    [InlineData(typeof(char[]), nameof(ColumnStorage.Text))]
    [InlineData(typeof(byte), nameof(ColumnStorage.Integer))]
    [InlineData(typeof(short), nameof(ColumnStorage.Integer))]
    [InlineData(typeof(int), nameof(ColumnStorage.Integer))]
    [InlineData(typeof(long), nameof(ColumnStorage.Integer))]
    [InlineData(typeof(ulong), nameof(ColumnStorage.Integer))]
    [InlineData(typeof(decimal), nameof(ColumnStorage.Integer))]
    [InlineData(typeof(long?), nameof(ColumnStorage.Integer))]
    [InlineData(typeof(double), nameof(ColumnStorage.Other))]
    [InlineData(typeof(float), nameof(ColumnStorage.Other))]
    [InlineData(typeof(bool), nameof(ColumnStorage.Other))]
    [InlineData(typeof(Guid), nameof(ColumnStorage.Other))]
    [InlineData(typeof(DateTime), nameof(ColumnStorage.Other))]
    [InlineData(typeof(DateTimeOffset), nameof(ColumnStorage.Other))]
    [InlineData(typeof(TimeSpan), nameof(ColumnStorage.Other))]
    [InlineData(typeof(byte[]), nameof(ColumnStorage.Other))]
    [InlineData(typeof(object), nameof(ColumnStorage.Other))]
    public void Storage_VemDoTipoClr(Type type, string expected) =>
        Assert.Equal(Enum.Parse<ColumnStorage>(expected), new ColumnInfo(0, "c", type).Storage);

    [Theory]
    [InlineData(nameof(ColumnStorage.Text), ValueKind.Text)]
    [InlineData(nameof(ColumnStorage.Integer), ValueKind.Number)]
    [InlineData(nameof(ColumnStorage.Other), ValueKind.Text)]
    public void ToValueKind_NumeroSoParaInteiro(string storage, ValueKind expected) =>
        Assert.Equal(expected, Enum.Parse<ColumnStorage>(storage).ToValueKind());

    [Fact]
    public void ComOrigem_IdentificaPelaOrigem()
    {
        var column = new ColumnInfo(0, "documento", typeof(string)) { BaseColumnName = "cpf" };

        Assert.True(column.HasOrigin);
        Assert.Equal("cpf", column.IdentifyingName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void SemOrigem_IdentificaPeloNomeNoResultado(string? baseColumnName)
    {
        var column = new ColumnInfo(0, "documento", typeof(string)) { BaseColumnName = baseColumnName };

        Assert.False(column.HasOrigin);
        Assert.Equal("documento", column.IdentifyingName);
    }

    [Fact]
    public void PosicaoNegativa_Lanca() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnInfo(-1, "c", typeof(string)));

    [Fact]
    public void NomeVazio_EhAceito() =>
        Assert.Equal(string.Empty, new ColumnInfo(0, string.Empty, typeof(string)).Name);
}
