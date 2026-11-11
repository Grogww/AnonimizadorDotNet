using AnonimizadorDotNet.Cryptography.Keys;
using AnonimizadorDotNet.Documents.Email;
using AnonimizadorDotNet.Resources;
using AnonimizadorDotNet.Transformation.Email;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Transformation.Email;

public sealed class EmailTransformerTests : IDisposable
{
    private readonly KeyRing _keys = new(ReferenceSubstitutes.MasterKey);
    private readonly EmailTransformer _transformer;

    public EmailTransformerTests() => _transformer = new EmailTransformer(_keys);

    private static readonly Gen<string> Emails =
        from user in Gen.Char["abcdefghijklmnopqrstuvwxyz"].Array[1, 12].Select(chars => new string(chars))
        from separator in Gen.OneOfConst("", ".", "_", "-")
        from surname in Gen.Char["abcdefghijklmnopqrstuvwxyz"].Array[1, 12].Select(chars => new string(chars))
        from digits in Gen.Char["0123456789"].Array[0, 4].Select(chars => new string(chars))
        from domain in Gen.OneOfConst("gmail.com", "hotmail.com", "empresa.com.br", "loja.com", "prefeitura.sp.gov.br")
        from upper in Gen.Bool
        let address = $"{user}{separator}{surname}{digits}@{domain}"
        select upper ? address.ToUpperInvariant() : address;

    public void Dispose()
    {
        _transformer.Dispose();
        _keys.Dispose();
    }

    /// <summary>Valores dourados: fixam rótulo, contextos e as chaves (parte local e domínio inteiros).</summary>
    [Theory]
    [InlineData("maria.silva@empresa.com.br", "theo.garcia@novalabs.com.br")]
    [InlineData("JOAO_SOUZA87@GMAIL.COM", "ADRIANA_CUNHA56@GMAIL.COM")]
    [InlineData("contato@loja.com", "luziafagundes@riolog.com")]
    [InlineData(" fulano-1999@prefeitura.sp.gov.br ", " raimundo-chaves9393@deltatur.gov.br ")]
    public void TryTransform_ChaveConhecida_ProduzSubstitutoFixo(string input, string expected)
    {
        Assert.True(_transformer.TryTransform(input, out string? substitute));

        Assert.Equal(expected, substitute);
    }

    [Fact]
    public void TryTransform_SaiEmailValidoNoEstiloDoOriginal()
    {
        Emails.Sample(input =>
        {
            Assert.True(EmailFormat.TryParse(input, out ParsedEmail? original));
            Assert.True(_transformer.TryTransform(input, out string? substitute));
            Assert.True(EmailFormat.TryParse(substitute, out ParsedEmail? parsed));

            Assert.Equal(original.IsUpperCase, parsed.IsUpperCase);
            Assert.Equal(original.Separator, parsed.Separator);
            Assert.Equal(original.TrailingDigits, parsed.TrailingDigits);
            Assert.Equal(original.DomainSuffix, parsed.DomainSuffix);
            Assert.Equal(original.IsPublicProvider, parsed.IsPublicProvider);
            if (original.IsPublicProvider)
            {
                Assert.Equal(original.Domain, parsed.Domain);
            }
            else
            {
                Assert.Contains(EmbeddedLists.DomainStarts, start => parsed.DomainName.StartsWith(start, StringComparison.Ordinal));
                Assert.Contains(EmbeddedLists.DomainEndings, ending => parsed.DomainName.EndsWith(ending, StringComparison.Ordinal));
            }
        });
    }

    [Fact]
    public void TryTransform_MesmaEmpresa_MesmoDominioSubstituto()
    {
        string[] users = ["ana", "bruno.lima", "carla_souza", "diretoria", "rh2024"];
        string[] domains = users.Select(user =>
        {
            Assert.True(_transformer.TryTransform($"{user}@empresa.com.br", out string? substitute));
            return substitute.Split('@')[1];
        }).ToArray();

        Assert.Single(domains.Distinct());
        Assert.EndsWith(".com.br", domains[0], StringComparison.Ordinal);
        Assert.NotEqual("empresa.com.br", domains[0]);
    }

    [Fact]
    public void TryTransform_EtiquetaECaixa_NaoMudamOSubstituto()
    {
        Assert.True(_transformer.TryTransform("joao@empresa.com.br", out string? plain));
        Assert.True(_transformer.TryTransform("joao+promo@empresa.com.br", out string? tagged));
        Assert.True(_transformer.TryTransform("JOAO@EMPRESA.COM.BR", out string? upper));

        Assert.Equal(plain, tagged);
        Assert.Equal(plain.ToUpperInvariant(), upper);
    }

    [Fact]
    public void TryTransform_MuitosDigitos_ContinuaValido()
    {
        string input = "a" + new string('7', 63) + "@gmail.com";

        Assert.True(_transformer.TryTransform(input, out string? substitute));

        Assert.True(EmailFormat.TryParse(substitute, out ParsedEmail? parsed));
        Assert.InRange(parsed.LocalPart.Length, 1, EmailFormat.MaxLocalPartLength);
    }

    [Fact]
    public void TryTransform_OutraChave_OutroSubstituto()
    {
        byte[] otherKey = ReferenceSubstitutes.MasterKey.Select(b => (byte)(b ^ 0xFF)).ToArray();
        using var otherKeys = new KeyRing(otherKey);
        using var other = new EmailTransformer(otherKeys);

        Assert.True(_transformer.TryTransform("maria.silva87@empresa.com.br", out string? first));
        Assert.True(other.TryTransform("maria.silva87@empresa.com.br", out string? second));

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("maria")]
    [InlineData("maria@")]
    [InlineData("+promo@gmail.com")]
    [InlineData("maria..silva@gmail.com")]
    public void TryTransform_Invalido_DevolveFalso(string input) =>
        Assert.False(_transformer.TryTransform(input, out _));

    [Fact]
    public void TryTransform_QualquerTexto_NuncaLanca() =>
        Gen.String.Sample(input => { _transformer.TryTransform(input, out _); });

    [Fact]
    public void DepoisDoDescarte_Lanca()
    {
        var transformer = new EmailTransformer(_keys);
        transformer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => transformer.TryTransform("maria@gmail.com", out _));
    }
}
