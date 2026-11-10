using AnonimizadorDotNet.Cryptography.Fpe;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

public sealed class CycleWalkingTests
{
    private static readonly byte[] Key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");

    /// <summary>Proíbe cerca de 1/4 do domínio: os valores cuja soma dos dígitos é múltiplo de 4.</summary>
    private static readonly NumeralPredicate DigitSumNotMultipleOf4 = numerals =>
    {
        int sum = 0;
        foreach (byte numeral in numerals)
        {
            sum += numeral;
        }

        return sum % 4 != 0;
    };

    /// <summary>Como na raiz do CNPJ alfanumérico: ao menos uma letra (numeral ≥ 10 em base 36).</summary>
    private static readonly NumeralPredicate HasLetter = numerals => numerals.IndexOfAnyInRange((byte)10, (byte)35) >= 0;

    [Fact]
    public void Encrypt_DominioMinimoInteiro_EhBijecaoNoSubconjuntoPermitido()
    {
        const int Length = 6;
        const int DomainSize = 1_000_000;
        using var ff1 = new Ff1(Key, 10);
        bool[] seen = new bool[DomainSize];
        byte[] input = new byte[Length];
        byte[] output = new byte[Length];
        int allowedCount = 0;
        int walkedCount = 0;

        // Uma chamada para a entrada e uma por cifra: mais de 2 significa que o ciclo foi percorrido.
        int predicateCalls = 0;
        NumeralPredicate counting = numerals =>
        {
            predicateCalls++;
            return DigitSumNotMultipleOf4(numerals);
        };

        for (int value = 0; value < DomainSize; value++)
        {
            NumeralString.FromInteger(value, 10, input);
            if (!DigitSumNotMultipleOf4(input))
            {
                continue;
            }

            allowedCount++;
            predicateCalls = 0;
            CycleWalking.Encrypt(ff1, input, [], output, counting);
            if (predicateCalls > 2)
            {
                walkedCount++;
            }

            Assert.True(DigitSumNotMultipleOf4(output), "A saída caiu num valor proibido.");
            int image = (int)NumeralString.ToInteger(output, 10);
            Assert.False(seen[image], "Colisão: duas entradas permitidas produziram a mesma saída.");
            seen[image] = true;
        }

        // Injetiva e fechada no subconjunto ⇒ cada valor permitido é imagem de exatamente um.
        Assert.Equal(allowedCount, seen.Count(s => s));
        Assert.True(walkedCount > 0, "Nenhuma entrada precisou de mais de uma cifra; o teste não exercitou o ciclo.");
    }

    [Fact]
    public void DecryptDeEncrypt_DevolveAEntrada_DominioPequeno()
    {
        Gen<byte[]> inputs = Gen.Byte[0, 9].Array[6].Where(numerals => DigitSumNotMultipleOf4(numerals));

        inputs.Sample(input =>
        {
            using var ff1 = new Ff1(Key, 10);
            byte[] ciphertext = new byte[input.Length];
            byte[] plaintext = new byte[input.Length];

            CycleWalking.Encrypt(ff1, input, [], ciphertext, DigitSumNotMultipleOf4);
            CycleWalking.Decrypt(ff1, ciphertext, [], plaintext, DigitSumNotMultipleOf4);

            Assert.Equal(input, plaintext);
        }, iter: 2_000);
    }

    [Fact]
    public void DecryptDeEncrypt_DevolveAEntrada_RaizAlfanumerica()
    {
        Gen<(byte[] Key, byte[] Input, byte[] Tweak)> cases =
            from key in Gen.Byte.Array[32]
            from input in Gen.Byte[0, 35].Array[8].Where(numerals => HasLetter(numerals))
            from tweak in Gen.Byte.Array[0, 16]
            select (key, input, tweak);

        cases.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, 36);
            byte[] ciphertext = new byte[c.Input.Length];
            byte[] plaintext = new byte[c.Input.Length];

            CycleWalking.Encrypt(ff1, c.Input, c.Tweak, ciphertext, HasLetter);
            CycleWalking.Decrypt(ff1, ciphertext, c.Tweak, plaintext, HasLetter);

            Assert.True(HasLetter(ciphertext));
            Assert.Equal(c.Input, plaintext);
        });
    }

    [Fact]
    public void Encrypt_SaidaDiretaJaPermitida_EhIgualAoFf1()
    {
        using var ff1 = new Ff1(Key, 10);
        byte[] input = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        byte[] direct = new byte[input.Length];
        byte[] walked = new byte[input.Length];

        ff1.Encrypt(input, [], direct);
        CycleWalking.Encrypt(ff1, input, [], walked, _ => true);

        Assert.Equal(direct, walked);
    }

    [Fact]
    public void Encrypt_MesmoBufferParaEntradaESaida_ProduzOMesmoResultado()
    {
        using var ff1 = new Ff1(Key, 10);
        byte[] input = [0, 0, 0, 0, 0, 1];
        byte[] separate = new byte[input.Length];
        byte[] inPlace = (byte[])input.Clone();

        CycleWalking.Encrypt(ff1, input, [], separate, DigitSumNotMultipleOf4);
        CycleWalking.Encrypt(ff1, inPlace, [], inPlace, DigitSumNotMultipleOf4);

        Assert.Equal(separate, inPlace);
    }

    [Fact]
    public void Encrypt_EntradaProibida_LancaArgumentException()
    {
        using var ff1 = new Ff1(Key, 10);

        var exception = Assert.Throws<ArgumentException>(
            () => CycleWalking.Encrypt(ff1, new byte[6], [], new byte[6], DigitSumNotMultipleOf4));

        Assert.Equal("input", exception.ParamName);
    }

    [Fact]
    public void Decrypt_EntradaProibida_LancaArgumentException()
    {
        using var ff1 = new Ff1(Key, 10);

        var exception = Assert.Throws<ArgumentException>(
            () => CycleWalking.Decrypt(ff1, new byte[6], [], new byte[6], DigitSumNotMultipleOf4));

        Assert.Equal("input", exception.ParamName);
    }

    [Fact]
    public void Encrypt_SubconjuntoQuaseVazio_LancaInvalidOperationAoAtingirOLimite()
    {
        // Só a própria entrada é permitida: o ciclo dela no domínio de 10⁶ valores é muito maior que 10.
        using var ff1 = new Ff1(Key, 10);
        byte[] input = [1, 2, 3, 4, 5, 6];
        NumeralPredicate onlyInput = numerals => numerals.SequenceEqual(input);

        Assert.Throws<InvalidOperationException>(
            () => CycleWalking.Encrypt(ff1, input, [], new byte[6], onlyInput, maxIterations: 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Encrypt_LimiteDeIteracoesInvalido_LancaArgumentOutOfRangeException(int maxIterations)
    {
        using var ff1 = new Ff1(Key, 10);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CycleWalking.Encrypt(ff1, new byte[6], [], new byte[6], _ => true, maxIterations));
    }
}
