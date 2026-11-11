using AnonimizadorDotNet.Cryptography.Keys;

namespace AnonimizadorDotNet.Tests.Cryptography.Keys;

public sealed class KeyRingTests
{
    private static readonly byte[] MasterKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public void DeriveSubkey_IgualASubkeyDerivation()
    {
        using var keys = new KeyRing(MasterKey);
        byte[] expected = new byte[SubkeyDerivation.SubkeySize];
        byte[] actual = new byte[SubkeyDerivation.SubkeySize];

        SubkeyDerivation.Derive(MasterKey, KeyLabels.Ff1Cpf, expected);
        keys.DeriveSubkey(KeyLabels.Ff1Cpf, actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CreateSubkey_IgualASubkeyDerivation()
    {
        using var keys = new KeyRing(MasterKey);
        byte[] expected = new byte[SubkeyDerivation.SubkeySize];
        SubkeyDerivation.Derive(MasterKey, KeyLabels.HmacName, expected);

        using SecretKey subkey = keys.CreateSubkey(KeyLabels.HmacName);

        Assert.Equal(expected, subkey.Span.ToArray());
    }

    [Fact]
    public void Construtor_NaoRetemOBufferDoChamador()
    {
        byte[] master = (byte[])MasterKey.Clone();
        using var keys = new KeyRing(master);
        Array.Clear(master);

        byte[] expected = new byte[SubkeyDerivation.SubkeySize];
        byte[] actual = new byte[SubkeyDerivation.SubkeySize];
        SubkeyDerivation.Derive(MasterKey, KeyLabels.Ff1Phone, expected);
        keys.DeriveSubkey(KeyLabels.Ff1Phone, actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Construtor_ChaveCurta_LancaSemMostrarAChave()
    {
        byte[] shortKey = Enumerable.Repeat((byte)0xAB, SubkeyDerivation.MinMasterKeySize - 1).ToArray();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new KeyRing(shortKey));

        Assert.DoesNotContain("AB", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("171", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DepoisDoDescarte_Lanca()
    {
        var keys = new KeyRing(MasterKey);
        keys.Dispose();
        keys.Dispose();

        Assert.Throws<ObjectDisposedException>(() => keys.CreateSubkey(KeyLabels.Ff1Cpf));
        Assert.Throws<ObjectDisposedException>(() => keys.DeriveSubkey(KeyLabels.Ff1Cpf, new byte[SubkeyDerivation.SubkeySize]));
    }

    [Fact]
    public void Subchave_SobreviveAoChaveiro_EDepoisDoProprioDescarteLanca()
    {
        SecretKey subkey;
        using (var keys = new KeyRing(MasterKey))
        {
            subkey = keys.CreateSubkey(KeyLabels.HmacEmail);
        }

        Assert.Equal(SubkeyDerivation.SubkeySize, subkey.Span.Length);
        subkey.Dispose();

        Assert.Throws<ObjectDisposedException>(() => subkey.Span.Length);
    }

    [Fact]
    public void SecretKey_Vazia_Lanca() =>
        Assert.Throws<ArgumentException>(() => new SecretKey([]));
}
