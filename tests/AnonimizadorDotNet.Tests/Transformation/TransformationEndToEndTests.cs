using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Tests.Transformation;

/// <summary>"Valor + tipo → substituto" pelo conjunto de transformadores, como as camadas de cima vão usar.</summary>
public sealed class TransformationEndToEndTests
{
    private static readonly (DocumentType Type, string Value, ValueKind Kind)[] Samples =
    [
        (DocumentType.Cpf, "529.982.247-25", ValueKind.Text),
        (DocumentType.Cpf, "1234567890", ValueKind.Number),
        (DocumentType.Cnpj, "11.222.333/0001-81", ValueKind.Text),
        (DocumentType.Cnpj, "12.ABC.345/01DE-35", ValueKind.Text),
        (DocumentType.Phone, "(11) 98765-4321", ValueKind.Text),
        (DocumentType.Phone, "552134567890", ValueKind.Number),
        (DocumentType.Email, "maria.silva@empresa.com.br", ValueKind.Text),
        (DocumentType.Name, "Maria da Silva", ValueKind.Text),
    ];

    /// <summary>
    /// O caso motivador: o mesmo CPF com máscara, sem máscara e guardado num inteiro (sem o zero à esquerda)
    /// vira o mesmo CPF fictício, cada um no seu formato — então os joins entre tabelas continuam valendo.
    /// </summary>
    [Theory]
    [InlineData("529.982.247-25", "52998224725", "52998224725")]
    [InlineData("012.345.678-90", "01234567890", "1234567890")]
    public void MesmoCpfEmTresRepresentacoes_MesmoSubstitutoNoFormatoDeCada(string masked, string plain, string number)
    {
        using var set = new TransformerSet(ReferenceSubstitutes.MasterKey);
        IValueTransformer cpf = set[DocumentType.Cpf];

        Assert.True(cpf.TryTransform(masked, ValueKind.Text, out string? fromMasked));
        Assert.True(cpf.TryTransform(plain, ValueKind.Text, out string? fromPlain));
        Assert.True(cpf.TryTransform(number, ValueKind.Number, out string? fromNumber));

        string digits = string.Concat(fromMasked.Where(char.IsAsciiDigit));
        Assert.Matches(@"^\d{3}\.\d{3}\.\d{3}-\d{2}$", fromMasked);
        Assert.Equal(digits, fromPlain);
        Assert.Equal(digits.TrimStart('0'), fromNumber);
    }

    [Fact]
    public void DuasInstanciasComAMesmaChave_MesmosSubstitutos()
    {
        using var first = new TransformerSet(ReferenceSubstitutes.MasterKey);
        using var second = new TransformerSet((byte[])ReferenceSubstitutes.MasterKey.Clone());

        foreach ((DocumentType type, string value, ValueKind kind) in Samples)
        {
            Assert.True(first[type].TryTransform(value, kind, out string? a));
            Assert.True(second[type].TryTransform(value, kind, out string? b));
            Assert.Equal(a, b);
            Assert.NotEqual(value, a);
        }
    }

    [Fact]
    public void ChavesDiferentes_SubstitutosDiferentes()
    {
        using var first = new TransformerSet(ReferenceSubstitutes.MasterKey);
        using var second = new TransformerSet(ReferenceSubstitutes.MasterKey.Select(b => (byte)(b + 1)).ToArray());

        foreach ((DocumentType type, string value, ValueKind kind) in Samples)
        {
            Assert.True(first[type].TryTransform(value, kind, out string? a));
            Assert.True(second[type].TryTransform(value, kind, out string? b));
            Assert.NotEqual(a, b);
        }
    }

    [Fact]
    public void ChaveDoChamadorZeradaDepoisDaConstrucao_NaoMudaNada()
    {
        byte[] master = (byte[])ReferenceSubstitutes.MasterKey.Clone();
        using var reference = new TransformerSet(ReferenceSubstitutes.MasterKey);
        using var set = new TransformerSet(master);
        Array.Clear(master);

        foreach ((DocumentType type, string value, ValueKind kind) in Samples)
        {
            Assert.True(reference[type].TryTransform(value, kind, out string? expected));
            Assert.True(set[type].TryTransform(value, kind, out string? actual));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void UsoParalelo_IgualAoSequencial()
    {
        using var set = new TransformerSet(ReferenceSubstitutes.MasterKey);
        var work = Enumerable.Range(0, 250).SelectMany(_ => Samples).ToArray();
        string[] expected = work.Select(item =>
        {
            Assert.True(set[item.Type].TryTransform(item.Value, item.Kind, out string? substitute));
            return substitute;
        }).ToArray();

        string?[] actual = new string?[work.Length];
        Parallel.For(0, work.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            set[work[i].Type].TryTransform(work[i].Value, work[i].Kind, out actual[i]);
        });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ValorDeOutroTipo_DevolveFalso()
    {
        using var set = new TransformerSet(ReferenceSubstitutes.MasterKey);

        Assert.False(set[DocumentType.Cpf].TryTransform("maria@gmail.com", ValueKind.Text, out _));
        Assert.False(set[DocumentType.Email].TryTransform("529.982.247-25", ValueKind.Text, out _));
        Assert.False(set[DocumentType.Name].TryTransform("52998224725", ValueKind.Number, out _));
        Assert.False(set[DocumentType.Phone].TryTransform("Maria Silva", ValueKind.Text, out _));
        Assert.False(set[DocumentType.Cnpj].TryTransform("(11) 98765-4321", ValueKind.Text, out _));
    }

    [Fact]
    public void Transformadores_NaoExpoemValorNemChave()
    {
        using var set = new TransformerSet(ReferenceSubstitutes.MasterKey);
        string keyHex = Convert.ToHexString(ReferenceSubstitutes.MasterKey);

        foreach (DocumentType type in Enum.GetValues<DocumentType>())
        {
            string text = set[type].ToString() ?? string.Empty;
            Assert.DoesNotContain(keyHex[..8], text, StringComparison.Ordinal);
            Assert.DoesNotContain("0001020304", text, StringComparison.Ordinal);
        }

        ArgumentException duplicate = Assert.Throws<ArgumentException>(() =>
            new TransformerSet(ReferenceSubstitutes.MasterKey, [set[DocumentType.Cpf]]));
        Assert.DoesNotContain("0001020304", duplicate.Message, StringComparison.Ordinal);
    }
}
