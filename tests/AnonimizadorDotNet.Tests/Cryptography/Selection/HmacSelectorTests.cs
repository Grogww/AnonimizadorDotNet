using System.Buffers.Binary;
using System.Text;
using AnonimizadorDotNet.Cryptography.Selection;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Cryptography.Selection;

public sealed class HmacSelectorTests
{
    /// <summary>Chave fixa dos testes: os bytes 0x00 a 0x1F.</summary>
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    private static readonly Gen<(byte[] Key, byte[] Context, byte[] Value, int Count)> CaseGen =
        from key in Gen.Byte.Array[1, 64]
        from context in Gen.Byte.Array[0, 32]
        from value in Gen.Byte.Array[0, 300] // Passa do limite da pilha (256 bytes) em parte dos casos.
        from count in Gen.Int[1, int.MaxValue]
        select (key, context, value, count);

    /// <summary>
    /// Valores dourados: fixam a codificação da mensagem e a redução. Calculados fora do .NET 8, com
    /// HMAC-SHA256 sobre <c>[0]⁴ || [tamanho do contexto]⁴ || contexto || valor</c>.
    /// </summary>
    [Theory]
    [InlineData("prenome", "MARIA", 1000, 436)]
    [InlineData("sobrenome-1", "SILVA", 5000, 493)]
    [InlineData("dominio", "empresa.com.br", 7, 5)]
    public void SelectIndex_EntradaConhecida_ProduzIndiceFixo(string context, string value, int count, int expected)
    {
        int index = HmacSelector.SelectIndex(Key, Utf8(context), Utf8(value), count);

        Assert.Equal(expected, index);
    }

    [Fact]
    public void SelectIndex_SempreDentroDoIntervalo()
    {
        CaseGen.Sample(c =>
            Assert.InRange(HmacSelector.SelectIndex(c.Key, c.Context, c.Value, c.Count), 0, c.Count - 1));
    }

    [Fact]
    public void SelectIndex_MesmaEntrada_ProduzMesmoIndice()
    {
        CaseGen.Sample(c =>
            Assert.Equal(
                HmacSelector.SelectIndex(c.Key, c.Context, c.Value, c.Count),
                HmacSelector.SelectIndex(c.Key.ToArray(), c.Context.ToArray(), c.Value.ToArray(), c.Count)));
    }

    [Fact]
    public void SelectIndex_ChaveDiferente_ProduzSelecoesDiferentes()
    {
        byte[] otherKey = Key.Select(b => (byte)(b ^ 0xFF)).ToArray();

        Assert.NotEqual(SelectSamples(Key, "prenome"u8.ToArray()), SelectSamples(otherKey, "prenome"u8.ToArray()));
    }

    [Fact]
    public void SelectIndex_ContextoDiferente_ProduzSelecoesDiferentes()
    {
        Assert.NotEqual(SelectSamples(Key, "sobrenome-1"u8.ToArray()), SelectSamples(Key, "sobrenome-2"u8.ToArray()));
    }

    [Fact]
    public void SelectIndex_FronteiraEntreContextoEValor_NaoEAmbigua()
    {
        // Sem o prefixo de tamanho, ("ab", "c") e ("a", "bc") formariam a mesma mensagem.
        int first = HmacSelector.SelectIndex(Key, "ab"u8, "c"u8, int.MaxValue);
        int second = HmacSelector.SelectIndex(Key, "a"u8, "bc"u8, int.MaxValue);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(3, 13.82)] // Valores críticos do qui-quadrado para p = 0,001 com count − 1 graus de liberdade.
    [InlineData(7, 22.46)]
    [InlineData(10, 27.88)]
    public void SelectIndex_DistribuicaoUniforme(int count, double criticalValue)
    {
        const int SamplesPerBucket = 10_000;
        int samples = count * SamplesPerBucket;
        int[] buckets = new int[count];
        byte[] value = new byte[sizeof(int)];

        for (int i = 0; i < samples; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(value, i);
            buckets[HmacSelector.SelectIndex(Key, "uniformidade"u8, value, count)]++;
        }

        double chiSquare = buckets.Sum(observed => Math.Pow(observed - SamplesPerBucket, 2) / SamplesPerBucket);
        Assert.True(chiSquare < criticalValue, $"Qui-quadrado {chiSquare:F2} acima de {criticalValue} (count = {count}).");
    }

    [Fact]
    public void TryReduce_BlocoNaFaixaDeVies_EhRejeitadoEUsaOProximo()
    {
        // 2⁶⁴ mod 3 = 1: só o bloco 0 cai na faixa rejeitada.
        byte[] digest = Digest(0, 5, 0, 0);

        Assert.True(HmacSelector.TryReduce(digest, 3, out int index));
        Assert.Equal(5 % 3, index);
    }

    [Fact]
    public void TryReduce_LimiteDaFaixaRejeitada_EhAceito()
    {
        // 2⁶⁴ mod (2³¹ − 1) = 4, pois 2³¹ ≡ 1: os blocos 0 a 3 são rejeitados e o 4 é aceito.
        Assert.True(HmacSelector.TryReduce(Digest(3, 4, 0, 0), int.MaxValue, out int index));
        Assert.Equal(4, index);
    }

    [Fact]
    public void TryReduce_TodosOsBlocosRejeitados_DevolveFalse()
    {
        Assert.False(HmacSelector.TryReduce(Digest(0, 0, 0, 0), 3, out _));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(1 << 30)]
    public void TryReduce_CountPotenciaDeDois_NuncaRejeita(int count)
    {
        // 2⁶⁴ é múltiplo de qualquer potência de 2 até 2³⁰: não há faixa de viés.
        Assert.True(HmacSelector.TryReduce(Digest(0, 0, 0, 0), count, out int index));
        Assert.Equal(0, index);
    }

    [Fact]
    public void SelectIndex_ChaveVazia_LancaArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => HmacSelector.SelectIndex([], "c"u8, "v"u8, 10));

        Assert.Equal("key", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SelectIndex_CountMenorQueUm_LancaArgumentOutOfRangeException(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HmacSelector.SelectIndex(Key, "c"u8, "v"u8, count));
    }

    /// <summary>Seleciona índices para os valores 0 a 19 com um count grande.</summary>
    private static int[] SelectSamples(byte[] key, byte[] context)
    {
        int[] indices = new int[20];
        byte[] value = new byte[sizeof(int)];
        for (int i = 0; i < indices.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(value, i);
            indices[i] = HmacSelector.SelectIndex(key, context, value, 1_000_000);
        }

        return indices;
    }

    private static byte[] Digest(ulong first, ulong second, ulong third, ulong fourth)
    {
        byte[] digest = new byte[32];
        BinaryPrimitives.WriteUInt64BigEndian(digest.AsSpan(0, 8), first);
        BinaryPrimitives.WriteUInt64BigEndian(digest.AsSpan(8, 8), second);
        BinaryPrimitives.WriteUInt64BigEndian(digest.AsSpan(16, 8), third);
        BinaryPrimitives.WriteUInt64BigEndian(digest.AsSpan(24, 8), fourth);
        return digest;
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
