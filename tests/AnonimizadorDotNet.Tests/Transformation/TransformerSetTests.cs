using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Tests.Transformation;

public sealed class TransformerSetTests
{
    [Fact]
    public void Embutidos_TemOsCincoTipos()
    {
        using var set = new TransformerSet(ReferenceSubstitutes.MasterKey);

        Assert.Equal(["cnpj", "cpf", "email", "name", "phone"], set.Types.Order(StringComparer.Ordinal));
        foreach (DocumentType type in Enum.GetValues<DocumentType>())
        {
            Assert.True(set.TryGet(set[type].Type, out IValueTransformer? found));
            Assert.Same(set[type], found);
        }
    }

    [Theory]
    [InlineData("CPF")]
    [InlineData("Cpf")]
    [InlineData("cpf")]
    public void TryGet_IgnoraCaixa(string type)
    {
        using var set = new TransformerSet(ReferenceSubstitutes.MasterKey);

        Assert.True(set.TryGet(type, out IValueTransformer? transformer));
        Assert.Same(set[DocumentType.Cpf], transformer);
    }

    [Fact]
    public void TryGet_TipoDesconhecido_DevolveFalso()
    {
        using var set = new TransformerSet(ReferenceSubstitutes.MasterKey);

        Assert.False(set.TryGet("matricula", out IValueTransformer? transformer));
        Assert.Null(transformer);
    }

    [Fact]
    public void Extensao_FicaDisponivelPeloTipo()
    {
        var custom = new FixedTransformer("Matricula");
        using var set = new TransformerSet(ReferenceSubstitutes.MasterKey, [custom]);

        Assert.True(set.TryGet("matricula", out IValueTransformer? transformer));
        Assert.Same(custom, transformer);
        Assert.True(transformer.TryTransform("123", ValueKind.Text, out string? substitute));
        Assert.Equal("XXX", substitute);
    }

    [Theory]
    [InlineData("cpf")]
    [InlineData("EMAIL")]
    public void Extensao_ComNomeDeEmbutido_Lanca(string type) =>
        Assert.Throws<ArgumentException>(() => new TransformerSet(ReferenceSubstitutes.MasterKey, [new FixedTransformer(type)]));

    [Fact]
    public void Extensoes_Repetidas_Lanca() =>
        Assert.Throws<ArgumentException>(() => new TransformerSet(
            ReferenceSubstitutes.MasterKey, [new FixedTransformer("rg"), new FixedTransformer("RG")]));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Extensao_SemNome_Lanca(string type) =>
        Assert.Throws<ArgumentException>(() => new TransformerSet(ReferenceSubstitutes.MasterKey, [new FixedTransformer(type)]));

    [Fact]
    public void Extensao_Nula_Lanca() =>
        Assert.Throws<ArgumentException>(() => new TransformerSet(ReferenceSubstitutes.MasterKey, [null!]));

    [Fact]
    public void ChaveCurta_Lanca() =>
        Assert.Throws<ArgumentException>(() => new TransformerSet(new byte[31]));

    [Fact]
    public void DepoisDoDescarte_EmbutidosLancam_ExtensoesSeguemComODono()
    {
        var custom = new FixedTransformer("rg");
        var set = new TransformerSet(ReferenceSubstitutes.MasterKey, [custom]);
        IValueTransformer cpf = set[DocumentType.Cpf];
        IValueTransformer name = set[DocumentType.Name];
        set.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cpf.TryTransform("529.982.247-25", ValueKind.Text, out _));
        Assert.Throws<ObjectDisposedException>(() => name.TryTransform("Maria Silva", ValueKind.Text, out _));
        Assert.True(custom.TryTransform("1", ValueKind.Text, out _));
    }

    private sealed class FixedTransformer(string type) : IValueTransformer
    {
        public string Type { get; } = type;

        public bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute)
        {
            substitute = new string('X', value.Length);
            return true;
        }
    }
}
