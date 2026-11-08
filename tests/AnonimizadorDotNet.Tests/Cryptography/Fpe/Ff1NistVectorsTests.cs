using AnonimizadorDotNet.Cryptography.Fpe;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

/// <summary>
/// Os 9 exemplos oficiais do FF1 publicados pelo NIST (Cryptographic Standards and Guidelines,
/// "Examples with Intermediate Values", FF1samples.pdf).
/// </summary>
public sealed class Ff1NistVectorsTests
{
    private const string Key128 = "2B7E151628AED2A6ABF7158809CF4F3C";
    private const string Key192 = "2B7E151628AED2A6ABF7158809CF4F3CEF4359D8D580AA4F";
    private const string Key256 = "2B7E151628AED2A6ABF7158809CF4F3CEF4359D8D580AA4F7F036D6F04FC6A94";
    private const string Tweak10 = "39383736353433323130";
    private const string Tweak11 = "3737373770717273373737";
    private const string Digits = "0123456789";
    private const string Base36 = "0123456789abcdefghi";

    public static TheoryData<int, string, int, string, string, string> Samples => new()
    {
        { 1, Key128, 10, Digits, "", "2433477484" },
        { 2, Key128, 10, Digits, Tweak10, "6124200773" },
        { 3, Key128, 36, Base36, Tweak11, "a9tv40mll9kdu509eum" },
        { 4, Key192, 10, Digits, "", "2830668132" },
        { 5, Key192, 10, Digits, Tweak10, "2496655549" },
        { 6, Key192, 36, Base36, Tweak11, "xbj3kv35jrawxv32ysr" },
        { 7, Key256, 10, Digits, "", "6657667009" },
        { 8, Key256, 10, Digits, Tweak10, "1001623463" },
        { 9, Key256, 36, Base36, Tweak11, "xs8a0azh2avyalyzuwd" },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Encrypt_AmostraOficial_ProduzCifraEsperada(
        int sample, string keyHex, int radix, string plaintext, string tweakHex, string expectedCiphertext)
    {
        _ = sample;
        using var ff1 = new Ff1(Convert.FromHexString(keyHex), radix);
        byte[] input = NumeralText.Parse(plaintext);
        byte[] output = new byte[input.Length];

        ff1.Encrypt(input, Convert.FromHexString(tweakHex), output);

        Assert.Equal(expectedCiphertext, NumeralText.Format(output));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Decrypt_AmostraOficial_DevolveTextoOriginal(
        int sample, string keyHex, int radix, string plaintext, string tweakHex, string ciphertext)
    {
        _ = sample;
        using var ff1 = new Ff1(Convert.FromHexString(keyHex), radix);
        byte[] input = NumeralText.Parse(ciphertext);
        byte[] output = new byte[input.Length];

        ff1.Decrypt(input, Convert.FromHexString(tweakHex), output);

        Assert.Equal(plaintext, NumeralText.Format(output));
    }
}
