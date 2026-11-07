using AnonimizadorDotNet.Cryptography.Fpe;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

/// <summary>
/// Propriedades do FF1 verificadas sobre entradas aleatórias: qualquer chave AES, qualquer radix aceito,
/// qualquer tamanho entre o mínimo do radix e 64 numerais, com e sem tweak.
/// </summary>
public sealed class Ff1PropertyTests
{
    private static readonly Gen<byte[]> KeyGen =
        from length in Gen.OneOfConst(16, 24, 32)
        from key in Gen.Byte.Array[length]
        select key;

    private static readonly Gen<Ff1Case> CaseGen =
        from key in KeyGen
        from radix in Gen.Int[Ff1Constraints.MinRadix, Ff1Constraints.MaxRadix]
        from length in Gen.Int[Ff1Constraints.GetMinLength(radix), 64]
        from input in Gen.Byte[0, (byte)(radix - 1)].Array[length]
        from tweak in Gen.Byte.Array[0, 32]
        select new Ff1Case(key, radix, input, tweak);

    [Fact]
    public void DecryptDeEncrypt_DevolveAEntrada()
    {
        CaseGen.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, c.Radix);
            byte[] ciphertext = new byte[c.Input.Length];
            byte[] plaintext = new byte[c.Input.Length];

            ff1.Encrypt(c.Input, c.Tweak, ciphertext);
            ff1.Decrypt(ciphertext, c.Tweak, plaintext);

            Assert.Equal(c.Input, plaintext);
        }, iter: 500);
    }

    [Fact]
    public void EncryptDeDecrypt_DevolveAEntrada()
    {
        CaseGen.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, c.Radix);
            byte[] plaintext = new byte[c.Input.Length];
            byte[] ciphertext = new byte[c.Input.Length];

            ff1.Decrypt(c.Input, c.Tweak, plaintext);
            ff1.Encrypt(plaintext, c.Tweak, ciphertext);

            Assert.Equal(c.Input, ciphertext);
        });
    }

    [Fact]
    public void Encrypt_PreservaTamanhoEAlfabeto()
    {
        CaseGen.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, c.Radix);
            byte[] ciphertext = new byte[c.Input.Length];

            ff1.Encrypt(c.Input, c.Tweak, ciphertext);

            Assert.All(ciphertext, numeral => Assert.InRange(numeral, 0, c.Radix - 1));
        });
    }

    [Fact]
    public void Encrypt_InstanciasDiferentesComAMesmaChave_ProduzemAMesmaSaida()
    {
        CaseGen.Sample(c =>
        {
            using var first = new Ff1(c.Key, c.Radix);
            using var second = new Ff1(c.Key, c.Radix);
            byte[] firstOutput = new byte[c.Input.Length];
            byte[] secondOutput = new byte[c.Input.Length];

            first.Encrypt(c.Input, c.Tweak, firstOutput);
            second.Encrypt(c.Input, c.Tweak, secondOutput);

            Assert.Equal(firstOutput, secondOutput);
        });
    }

    [Fact]
    public void Encrypt_MesmoBufferParaEntradaESaida_ProduzOMesmoResultado()
    {
        CaseGen.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, c.Radix);
            byte[] separate = new byte[c.Input.Length];
            byte[] inPlace = (byte[])c.Input.Clone();

            ff1.Encrypt(c.Input, c.Tweak, separate);
            ff1.Encrypt(inPlace, c.Tweak, inPlace);

            Assert.Equal(separate, inPlace);
        });
    }

    [Fact]
    public void Encrypt_TweakDiferente_ProduzPermutacaoDiferente()
    {
        // Com permutações independentes, a chance de 20 valores coincidirem é desprezível.
        Gen<(byte[] Key, byte[] Tweak, byte[] OtherTweak)> cases =
            from key in KeyGen
            from tweak in Gen.Byte.Array[0, 16]
            from otherTweak in Gen.Byte.Array[0, 16]
            where !tweak.AsSpan().SequenceEqual(otherTweak)
            select (key, tweak, otherTweak);

        cases.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, 10);
            Assert.False(EncryptSamples(ff1, c.Tweak).SequenceEqual(EncryptSamples(ff1, c.OtherTweak)));
        });
    }

    [Fact]
    public void Encrypt_ChaveDiferente_ProduzPermutacaoDiferente()
    {
        Gen<(byte[] Key, byte[] OtherKey)> cases =
            from key in KeyGen
            from otherKey in KeyGen
            where !key.AsSpan().SequenceEqual(otherKey)
            select (key, otherKey);

        cases.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, 10);
            using var other = new Ff1(c.OtherKey, 10);
            Assert.False(EncryptSamples(ff1, []).SequenceEqual(EncryptSamples(other, [])));
        });
    }

    /// <summary>Cifra os valores 000000000 a 000000019 (9 dígitos) e concatena as saídas.</summary>
    private static byte[] EncryptSamples(Ff1 ff1, byte[] tweak)
    {
        const int Count = 20;
        const int Length = 9;
        byte[] all = new byte[Count * Length];
        byte[] input = new byte[Length];
        for (int value = 0; value < Count; value++)
        {
            NumeralString.FromInteger(value, 10, input);
            ff1.Encrypt(input, tweak, all.AsSpan(value * Length, Length));
        }

        return all;
    }

    public sealed record Ff1Case(byte[] Key, int Radix, byte[] Input, byte[] Tweak)
    {
        // Mostra o caso reduzido de forma legível quando uma propriedade falha.
        public override string ToString() =>
            $"Radix={Radix}, Chave={Convert.ToHexString(Key)}, Entrada=[{string.Join(',', Input)}], Tweak={Convert.ToHexString(Tweak)}";
    }
}
