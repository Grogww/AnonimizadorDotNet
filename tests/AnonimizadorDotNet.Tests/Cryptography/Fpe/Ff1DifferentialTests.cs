using AnonimizadorDotNet.Cryptography.Fpe;
using CsCheck;
using Org.BouncyCastle.Crypto.Fpe;
using Org.BouncyCastle.Crypto.Parameters;

namespace AnonimizadorDotNet.Tests.Cryptography.Fpe;

/// <summary>
/// Teste diferencial: compara o nosso FF1 com o <c>FpeFf1Engine</c> do BouncyCastle, uma implementação
/// independente e madura, em milhares de entradas aleatórias. Os vetores do NIST cobrem 9 casos
/// fixos; este teste cobre radix, tamanhos, chaves e tweaks que os vetores não exercitam.
/// </summary>
/// <remarks>O BouncyCastle é dependência só do projeto de testes; o pacote continua só com a BCL.</remarks>
public sealed class Ff1DifferentialTests
{
    private static readonly Gen<byte[]> KeyGen =
        from length in Gen.OneOfConst(16, 24, 32)
        from key in Gen.Byte.Array[length]
        select key;

    private static readonly Gen<DifferentialCase> CaseGen =
        from key in KeyGen
        from radix in Gen.Int[Ff1Constraints.MinRadix, Ff1Constraints.MaxRadix]
        from length in Gen.Int[Ff1Constraints.GetMinLength(radix), 64]
        from input in Gen.Byte[0, (byte)(radix - 1)].Array[length]
        from tweak in Gen.Byte.Array[0, 32]
        select new DifferentialCase(key, radix, input, tweak);

    [Fact]
    public void Encrypt_MesmaEntrada_IgualAoBouncyCastle()
    {
        CaseGen.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, c.Radix);
            byte[] ours = new byte[c.Input.Length];

            ff1.Encrypt(c.Input, c.Tweak, ours);

            Assert.Equal(BouncyCastle(c, encrypt: true), ours);
        }, iter: 2000);
    }

    [Fact]
    public void Decrypt_MesmaEntrada_IgualAoBouncyCastle()
    {
        CaseGen.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, c.Radix);
            byte[] ours = new byte[c.Input.Length];

            ff1.Decrypt(c.Input, c.Tweak, ours);

            Assert.Equal(BouncyCastle(c, encrypt: false), ours);
        }, iter: 2000);
    }

    [Theory]
    [InlineData(10, 9)]  // base do CPF
    [InlineData(10, 8)]  // raiz do CNPJ numérico e sufixo do telefone
    [InlineData(36, 8)]  // raiz do CNPJ alfanumérico
    public void Encrypt_TamanhosDosDocumentosDaV1_IgualAoBouncyCastle(int radix, int length)
    {
        Gen<DifferentialCase> cases =
            from key in KeyGen
            from input in Gen.Byte[0, (byte)(radix - 1)].Array[length]
            select new DifferentialCase(key, radix, input, []);

        cases.Sample(c =>
        {
            using var ff1 = new Ff1(c.Key, c.Radix);
            byte[] ours = new byte[c.Input.Length];

            ff1.Encrypt(c.Input, c.Tweak, ours);

            Assert.Equal(BouncyCastle(c, encrypt: true), ours);
        }, iter: 2000);
    }

    private static byte[] BouncyCastle(DifferentialCase c, bool encrypt)
    {
        var engine = new FpeFf1Engine();
        engine.Init(encrypt, new FpeParameters(new KeyParameter(c.Key), c.Radix, c.Tweak));
        byte[] output = new byte[c.Input.Length];
        engine.ProcessBlock(c.Input, 0, c.Input.Length, output, 0);
        return output;
    }

    public sealed record DifferentialCase(byte[] Key, int Radix, byte[] Input, byte[] Tweak)
    {
        // Mostra o caso reduzido de forma legível quando a comparação falha.
        public override string ToString() =>
            $"Radix={Radix}, Chave={Convert.ToHexString(Key)}, Entrada=[{string.Join(',', Input)}], Tweak={Convert.ToHexString(Tweak)}";
    }
}
