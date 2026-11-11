using AnonimizadorDotNet.Cryptography.Fpe;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

public sealed class Ff1PoolTests
{
    private static readonly byte[] Key = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3CEF4359D8D580AA4F7F036D6F04FC6A94");

    private static readonly Gen<byte[]> Bases = Gen.Byte[0, 9].Array[9];

    private static readonly NumeralPredicate NotAllEqual = numerals => numerals.ContainsAnyExcept(numerals[0]);

    [Fact]
    public void Encrypt_IgualAoFf1()
    {
        using var pool = new Ff1Pool(Key, 10);
        using var ff1 = new Ff1(Key, 10);

        Bases.Sample(input =>
        {
            byte[] expected = new byte[input.Length];
            byte[] actual = new byte[input.Length];
            byte[] back = new byte[input.Length];

            ff1.Encrypt(input, [7], expected);
            pool.Encrypt(input, [7], actual);
            pool.Decrypt(actual, [7], back);

            Assert.Equal(expected, actual);
            Assert.Equal(input, back);
        });
    }

    [Fact]
    public void EncryptComPredicado_IgualAoCycleWalking()
    {
        using var pool = new Ff1Pool(Key, 10);
        using var ff1 = new Ff1(Key, 10);

        Bases.Where(input => NotAllEqual(input)).Sample(input =>
        {
            byte[] expected = new byte[input.Length];
            byte[] actual = new byte[input.Length];
            byte[] back = new byte[input.Length];

            CycleWalking.Encrypt(ff1, input, [], expected, NotAllEqual);
            pool.Encrypt(input, [], actual, NotAllEqual);
            pool.Decrypt(actual, [], back, NotAllEqual);

            Assert.Equal(expected, actual);
            Assert.Equal(input, back);
        });
    }

    [Fact]
    public void UsoParalelo_IgualAoSequencial()
    {
        byte[][] inputs = Enumerable.Range(0, 4_000)
            .Select(i => i.ToString("D9", System.Globalization.CultureInfo.InvariantCulture).Select(c => (byte)(c - '0')).ToArray())
            .ToArray();

        using var ff1 = new Ff1(Key, 10);
        byte[][] expected = inputs.Select(input =>
        {
            byte[] output = new byte[input.Length];
            ff1.Encrypt(input, [], output);
            return output;
        }).ToArray();

        using var pool = new Ff1Pool(Key, 10, maxRetained: 4);
        byte[][] actual = new byte[inputs.Length][];
        Parallel.For(0, inputs.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            actual[i] = new byte[inputs[i].Length];
            pool.Encrypt(inputs[i], [], actual[i]);
        });

        Assert.Equal(expected, actual);
        Assert.InRange(pool.RetainedCount, 1, 4);
    }

    [Fact]
    public void Construtor_NaoRetemOBufferDoChamador()
    {
        byte[] key = (byte[])Key.Clone();
        using var pool = new Ff1Pool(key, 10, maxRetained: 1);
        Array.Clear(key);

        // Força a criação de instâncias novas a partir da cópia interna da chave.
        byte[] input = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        byte[] expected = new byte[9];
        using (var ff1 = new Ff1(Key, 10))
        {
            ff1.Encrypt(input, [], expected);
        }

        byte[][] outputs = new byte[64][];
        Parallel.For(0, outputs.Length, i =>
        {
            outputs[i] = new byte[9];
            pool.Encrypt(input, [], outputs[i]);
        });

        Assert.All(outputs, output => Assert.Equal(expected, output));
    }

    [Fact]
    public void Construtor_ValidaChaveERadix()
    {
        Assert.Throws<ArgumentException>(() => new Ff1Pool(new byte[15], 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ff1Pool(Key, 37));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ff1Pool(Key, 10, maxRetained: -1));
    }

    [Fact]
    public void Propriedades_VemDoFf1()
    {
        using var pool = new Ff1Pool(Key, 36);

        Assert.Equal(36, pool.Radix);
        Assert.Equal(Ff1Constraints.GetMinLength(36), pool.MinLength);
        Assert.Equal(2 * Environment.ProcessorCount, pool.MaxRetained);
    }

    [Fact]
    public void DepoisDoDescarte_Lanca()
    {
        var pool = new Ff1Pool(Key, 10);
        pool.Dispose();
        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.Encrypt(new byte[9], [], new byte[9]));
        Assert.Throws<ObjectDisposedException>(() => pool.Decrypt(new byte[9], [], new byte[9], NotAllEqual));
        Assert.Equal(0, pool.RetainedCount);
    }
}
