using AnonimizadorDotNet.Cryptography.Fpe;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

/// <summary>
/// Prova, cifrando todo o domínio, que o FF1 é uma permutação: nenhum valor de saída se repete. É a
/// garantia de que dois identificadores reais distintos nunca viram o mesmo substituto.
/// </summary>
public sealed class Ff1BijectionTests
{
    private static readonly byte[] Key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");

    [Theory]
    [InlineData(10, 6)] // 10⁶ valores, metades iguais (u = v = 3).
    [InlineData(3, 13)] // 3¹³ = 1.594.323 valores, metades diferentes (u = 6, v = 7).
    public void Encrypt_DominioMinimoInteiro_NaoTemColisoes(int radix, int length)
    {
        using var ff1 = new Ff1(Key, radix);
        int domainSize = checked((int)Math.Pow(radix, length));
        bool[] seen = new bool[domainSize];
        byte[] input = new byte[length];
        byte[] output = new byte[length];

        for (int value = 0; value < domainSize; value++)
        {
            ff1.Encrypt(input, [], output);

            int image = (int)NumeralString.ToInteger(output, radix);
            Assert.False(seen[image], "Colisão: duas entradas distintas produziram a mesma saída.");
            seen[image] = true;

            Increment(input, radix);
        }
    }

    /// <summary>Avança a cadeia de numerais para o próximo valor, como um odômetro.</summary>
    private static void Increment(byte[] numerals, int radix)
    {
        for (int i = numerals.Length - 1; i >= 0; i--)
        {
            if (++numerals[i] < radix)
            {
                return;
            }

            numerals[i] = 0;
        }
    }
}
