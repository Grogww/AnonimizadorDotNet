using AnonimizadorDotNet.Documents.Email;
using AnonimizadorDotNet.Resources;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Documents.Email;

public sealed class EmailFormatTests
{
    [Theory]
    [InlineData("joao.silva@gmail.com", "joao.silva", "gmail", "com", true)]
    [InlineData("Joao.Silva@Gmail.COM", "joao.silva", "gmail", "com", true)]
    [InlineData("contato@empresa.com.br", "contato", "empresa", "com.br", false)]
    [InlineData("maria@mail.empresa.com.br", "maria", "mail.empresa", "com.br", false)]
    [InlineData("fulano@prefeitura.sp.gov.br", "fulano", "prefeitura.sp", "gov.br", false)]
    [InlineData("ana_lima@empresa.io", "ana_lima", "empresa", "io", false)]
    [InlineData("joao+promo@uol.com.br", "joao", "uol", "com.br", true)]
    public void TryParse_EmailValido_SeparaParteLocalEDominio(
        string input, string local, string domainName, string suffix, bool isPublic)
    {
        Assert.True(EmailFormat.TryParse(input, out ParsedEmail? email));

        Assert.Equal(local, email.LocalPart);
        Assert.Equal(domainName, email.DomainName);
        Assert.Equal(suffix, email.DomainSuffix);
        Assert.Equal(isPublic, email.IsPublicProvider);
    }

    [Theory]
    [InlineData("joao.silva87@gmail.com", '.', 2)]
    [InlineData("ana_lima@gmail.com", '_', 0)]
    [InlineData("jose-souza1990@gmail.com", '-', 4)]
    [InlineData("mariasouza@gmail.com", null, 0)]
    public void TryParse_DetectaOEstiloDaParteLocal(string input, char? separator, int digits)
    {
        Assert.True(EmailFormat.TryParse(input, out ParsedEmail? email));

        Assert.Equal(separator, email.Separator);
        Assert.Equal(digits, email.TrailingDigits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("joao")]
    [InlineData("@gmail.com")]
    [InlineData("joao@")]
    [InlineData("joao@@gmail.com")]
    [InlineData("joao@silva@gmail.com")]
    [InlineData("joão@gmail.com")]
    [InlineData(".joao@gmail.com")]
    [InlineData("joao.@gmail.com")]
    [InlineData("jo..ao@gmail.com")]
    [InlineData("joao@gmail")]
    [InlineData("joao@gmail.c")]
    [InlineData("joao@gmail.123")]
    [InlineData("joao@-gmail.com")]
    [InlineData("joao@gmail..com")]
    [InlineData("jo ao@gmail.com")]
    [InlineData("+tag@gmail.com")]
    public void TryParse_Invalido_DevolveFalso(string input)
    {
        Assert.False(EmailFormat.TryParse(input, out ParsedEmail? email));
        Assert.Null(email);
    }

    [Fact]
    public void TryParse_ParteLocalLongaDemais_DevolveFalso() =>
        Assert.False(EmailFormat.TryParse(new string('a', EmailFormat.MaxLocalPartLength + 1) + "@gmail.com", out _));

    [Theory]
    [InlineData("joao.silva87@gmail.com", "Antônia", "Conceição", "12", "antonia.conceicao12")]
    [InlineData("mariasouza@gmail.com", "José", "Sá", "", "josesa")]
    [InlineData("ana_lima@gmail.com", "Ana Maria", "D'Ávila", "", "anamaria_davila")]
    public void ComposeLocalPart_SegueOEstiloDoOriginal(string original, string given, string surname, string digits, string expected)
    {
        Assert.True(EmailFormat.TryParse(original, out ParsedEmail? email));

        Assert.Equal(expected, EmailFormat.ComposeLocalPart(given, surname, digits, email));
    }

    [Fact]
    public void ComposeLocalPart_DigitosInvalidos_Lanca()
    {
        Assert.True(EmailFormat.TryParse("joao@gmail.com", out ParsedEmail? email));

        Assert.Throws<ArgumentException>(() => EmailFormat.ComposeLocalPart("Ana", "Lima", "1a", email));
    }

    [Theory]
    [InlineData("joao.silva@gmail.com", "maria.souza", "nortelog", "maria.souza@gmail.com")]
    [InlineData("JOAO.SILVA@GMAIL.COM", "maria.souza", "nortelog", "MARIA.SOUZA@GMAIL.COM")]
    [InlineData("contato@empresa.com.br", "maria.souza", "nortelog", "maria.souza@nortelog.com.br")]
    [InlineData("fulano@prefeitura.sp.gov.br", "ana", "valetec", "ana@valetec.gov.br")]
    [InlineData("  contato@empresa.com  ", "ana", "valetec", "  ana@valetec.com  ")]
    public void Format_PreservaProvedorPublicoESufixo(string original, string local, string domainName, string expected)
    {
        Assert.True(EmailFormat.TryParse(original, out ParsedEmail? email));

        Assert.Equal(expected, EmailFormat.Format(email, local, domainName));
    }

    [Fact]
    public void Format_DominioCorporativoSemSubstituto_Lanca()
    {
        Assert.True(EmailFormat.TryParse("contato@empresa.com.br", out ParsedEmail? email));

        Assert.Throws<ArgumentException>(() => EmailFormat.Format(email, "ana", null));
        Assert.Throws<ArgumentException>(() => EmailFormat.Format(email, "ana", "-x"));
        Assert.Throws<ArgumentException>(() => EmailFormat.Format(email, "a..na", "valetec"));
    }

    [Fact]
    public void FormatarEInterpretar_MantemProvedorSufixoEEstilo()
    {
        string[] originals = ["joao.silva87@gmail.com", "CONTATO@EMPRESA.COM.BR", "ana_lima@prefeitura.sp.gov.br"];
        Gen<string> given = Gen.Int[0, EmbeddedLists.FemaleGivenNames.Length - 1].Select(i => EmbeddedLists.FemaleGivenNames[i]);
        Gen<string> surname = Gen.Int[0, EmbeddedLists.Surnames.Length - 1].Select(i => EmbeddedLists.Surnames[i]);
        Gen<string> start = Gen.Int[0, EmbeddedLists.DomainStarts.Length - 1].Select(i => EmbeddedLists.DomainStarts[i]);
        Gen<string> ending = Gen.Int[0, EmbeddedLists.DomainEndings.Length - 1].Select(i => EmbeddedLists.DomainEndings[i]);
        Gen.Select(Gen.OneOfConst(originals), given, surname, start, ending, Gen.Int[0, 9999]).Sample((original, g, s, a, b, n) =>
        {
            Assert.True(EmailFormat.TryParse(original, out ParsedEmail? email));
            string digits = n.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)[..email.TrailingDigits];
            string local = EmailFormat.ComposeLocalPart(g, s, digits, email);
            string formatted = EmailFormat.Format(email, local, a + b);

            Assert.True(EmailFormat.TryParse(formatted, out ParsedEmail? parsed));
            Assert.Equal(email.IsPublicProvider, parsed.IsPublicProvider);
            Assert.Equal(email.DomainSuffix, parsed.DomainSuffix);
            Assert.Equal(email.Separator, parsed.Separator);
            Assert.Equal(email.IsUpperCase, parsed.IsUpperCase);
            Assert.True(parsed.TrailingDigits >= email.TrailingDigits);
        });
    }

    [Fact]
    public void TryParse_QualquerTexto_NuncaLanca()
    {
        Gen.String.Sample(input =>
        {
            _ = EmailFormat.TryParse(input, out _);
        });
    }
}
