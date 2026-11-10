using System.Security.Cryptography;
using System.Text;
using AnonimizadorDotNet.Cryptography.Keys;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Cryptography.Keys;

public sealed class SubkeyDerivationTests
{
    /// <summary>Chave mestra fixa dos testes: os bytes 0x00 a 0x1F.</summary>
    private static readonly byte[] MasterKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    private static readonly string[] LabelNames =
        ["Ff1Cpf", "Ff1Cnpj", "Ff1CnpjAlphanumeric", "Ff1Phone", "HmacName", "HmacEmail"];

    public static TheoryData<string> AllLabels => new(LabelNames);

    /// <summary>
    /// Valores dourados: fixam os rótulos e a forma de derivar. Se um destes testes quebrar, todos os
    /// substitutos daquele tipo mudaram — o que só pode acontecer com uma nova versão de rótulo.
    /// Calculados fora do .NET 8, com HKDF montado à mão sobre HMAC-SHA256.
    /// </summary>
    [Theory]
    [InlineData("Ff1Cpf", "E302C86873BD8FF3B38E7BF3B6B5D38622B6523EA32F2FB1770CD1A78EDC7863")]
    [InlineData("Ff1Cnpj", "1DB04600B9B5F3F67B8E69A8C990987E2C24A16CD4AB7FA8A7F8C8537F6F9493")]
    [InlineData("Ff1CnpjAlphanumeric", "8F307CB52502656265FE438E7C2E0A193EDDDEDB720BF8109B7E0EB0E87DECAD")]
    [InlineData("Ff1Phone", "BD8AB1BFEC4360E6CA1B3C3127D139BF670CDBA6DE35FF48C5082630F4F56DAD")]
    [InlineData("HmacName", "5588A927C927C8DFF3EC8EB3FFED81F6103B069D6D7E3AACBE3FD0ABC054254C")]
    [InlineData("HmacEmail", "5998C8A3C96A0FA98667B6D93178364996BF54DD9CC5DA0E88791E052F821635")]
    public void Derive_ChaveMestraConhecida_ProduzSubchaveFixa(string labelName, string expectedHex)
    {
        byte[] subkey = new byte[SubkeyDerivation.SubkeySize];

        SubkeyDerivation.Derive(MasterKey, Label(labelName), subkey);

        Assert.Equal(expectedHex, Convert.ToHexString(subkey));
    }

    [Fact]
    public void Derive_SegueAFormulaDaRfc5869ComSaltVazio()
    {
        // HKDF de referência, escrito à mão: PRK = HMAC(salt = 0³², IKM); T(1) = HMAC(PRK, info || 0x01).
        // Confirma que salt e info não foram trocados na chamada à BCL.
        Gen<(byte[] Master, byte[] Label)> cases =
            from master in Gen.Byte.Array[SubkeyDerivation.MinMasterKeySize, 64]
            from label in Gen.Byte.Array[1, 64]
            select (master, label);

        cases.Sample(c =>
        {
            byte[] prk = HMACSHA256.HashData(new byte[32], c.Master);
            byte[] expected = HMACSHA256.HashData(prk, c.Label.Append((byte)0x01).ToArray());
            byte[] subkey = new byte[SubkeyDerivation.SubkeySize];

            SubkeyDerivation.Derive(c.Master, c.Label, subkey);

            Assert.Equal(expected, subkey);
        });
    }

    [Fact]
    public void Derive_RotulosDiferentes_ProduzemSubchavesDiferentes()
    {
        var subkeys = LabelNames.Select(name => Convert.ToHexString(DeriveFromLabel(MasterKey, name))).ToList();

        Assert.Equal(subkeys.Count, subkeys.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllLabels))]
    public void Derive_ChavesMestrasDiferentes_ProduzemSubchavesDiferentes(string labelName)
    {
        Gen<(byte[] Master, byte[] Other)> cases =
            from master in Gen.Byte.Array[SubkeyDerivation.MinMasterKeySize]
            from other in Gen.Byte.Array[SubkeyDerivation.MinMasterKeySize]
            where !master.AsSpan().SequenceEqual(other)
            select (master, other);

        cases.Sample(c =>
            Assert.NotEqual(DeriveFromLabel(c.Master, labelName), DeriveFromLabel(c.Other, labelName)));
    }

    [Fact]
    public void Derive_MesmaEntrada_ProduzMesmaSubchave()
    {
        Assert.Equal(DeriveFromLabel(MasterKey, "Ff1Cpf"), DeriveFromLabel(MasterKey.ToArray(), "Ff1Cpf"));
    }

    [Fact]
    public void Rotulos_SaoVersionadosEDistintos()
    {
        var labels = LabelNames.Select(name => Encoding.ASCII.GetString(Label(name))).ToList();

        Assert.All(labels, label => Assert.StartsWith("AnonimizadorDotNet/v1/", label, StringComparison.Ordinal));
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    public void Derive_ChaveMestraCurta_LancaArgumentException(int length)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SubkeyDerivation.Derive(new byte[length], KeyLabels.Ff1Cpf, new byte[SubkeyDerivation.SubkeySize]));

        Assert.Equal("masterKey", exception.ParamName);
    }

    [Fact]
    public void Derive_RotuloVazio_LancaArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SubkeyDerivation.Derive(MasterKey, [], new byte[SubkeyDerivation.SubkeySize]));

        Assert.Equal("label", exception.ParamName);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Derive_DestinoComTamanhoErrado_LancaArgumentException(int length)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SubkeyDerivation.Derive(MasterKey, KeyLabels.Ff1Cpf, new byte[length]));

        Assert.Equal("destination", exception.ParamName);
    }

    private static byte[] DeriveFromLabel(byte[] masterKey, string labelName)
    {
        byte[] subkey = new byte[SubkeyDerivation.SubkeySize];
        SubkeyDerivation.Derive(masterKey, Label(labelName), subkey);
        return subkey;
    }

    private static byte[] Label(string name) => name switch
    {
        "Ff1Cpf" => KeyLabels.Ff1Cpf.ToArray(),
        "Ff1Cnpj" => KeyLabels.Ff1Cnpj.ToArray(),
        "Ff1CnpjAlphanumeric" => KeyLabels.Ff1CnpjAlphanumeric.ToArray(),
        "Ff1Phone" => KeyLabels.Ff1Phone.ToArray(),
        "HmacName" => KeyLabels.HmacName.ToArray(),
        "HmacEmail" => KeyLabels.HmacEmail.ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };
}
