using AnonimizadorDotNet.Documents.Cnpj;
using AnonimizadorDotNet.Documents.Cpf;
using AnonimizadorDotNet.Documents.Email;
using AnonimizadorDotNet.Documents.Formatting;
using AnonimizadorDotNet.Documents.Name;
using AnonimizadorDotNet.Documents.Phone;

namespace AnonimizadorDotNet.Tests.Documents;

/// <summary>Garantias comuns a todos os tipos: o valor original não vaza por <c>ToString()</c> nem por exceções.</summary>
public sealed class DocumentsPrivacyTests
{
    [Fact]
    public void ToString_DosValoresInterpretados_NaoExpoeOOriginal()
    {
        Assert.True(CpfFormat.TryParse("529.982.247-25", ValueKind.Text, out ParsedCpf? cpf));
        Assert.True(CnpjFormat.TryParse("12.ABC.345/01DE-35", ValueKind.Text, out ParsedCnpj? cnpj));
        Assert.True(PhoneFormat.TryParse("(11) 91234-5678", ValueKind.Text, out ParsedPhone? phone));
        Assert.True(PersonNameFormat.TryParse("Maria da Silva", out ParsedPersonName? name));
        Assert.True(EmailFormat.TryParse("maria.silva@empresa.com.br", out ParsedEmail? email));

        object[] values = [cpf, cpf.Layout, cnpj, cnpj.Layout, phone, phone.Layout, name, name.Parts[0], email];
        string[] fragments = ["529", "982", "ABC", "345", "1234", "5678", "Maria", "MARIA", "Silva", "SILVA", "maria", "empresa"];

        Assert.All(values, value =>
        {
            string text = value.ToString() ?? string.Empty;
            Assert.All(fragments, fragment => Assert.DoesNotContain(fragment, text, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Excecoes_DeFormat_NaoExpoemOValor()
    {
        Assert.True(CpfFormat.TryParse("529.982.247-25", ValueKind.Text, out ParsedCpf? cpf));
        Assert.True(CnpjFormat.TryParse("11.222.333/0001-81", ValueKind.Text, out ParsedCnpj? cnpj));
        Assert.True(PhoneFormat.TryParse("(11) 91234-5678", ValueKind.Text, out ParsedPhone? phone));
        Assert.True(PersonNameFormat.TryParse("Maria da Silva", out ParsedPersonName? name));
        Assert.True(EmailFormat.TryParse("maria.silva@empresa.com.br", out ParsedEmail? email));

        Exception[] exceptions =
        [
            Assert.ThrowsAny<ArgumentException>(() => CpfFormat.Format([5, 2, 9, 9, 8, 2, 2, 4, 10], cpf.Layout)),
            Assert.ThrowsAny<ArgumentException>(() => CnpjFormat.Format([1, 1, 2, 2, 2, 3, 3, 12], cnpj)),
            Assert.ThrowsAny<ArgumentException>(() => PhoneFormat.Format([2, 3, 4, 5, 6, 7], phone)),
            Assert.ThrowsAny<ArgumentException>(() => PersonNameFormat.Format(name, ["Silvana"])),
            Assert.ThrowsAny<ArgumentException>(() => EmailFormat.Format(email, "maria..silva", "empresa")),
        ];

        string[] fragments = ["5299", "11222", "234567", "Silva", "maria", "empresa"];
        Assert.All(exceptions, exception =>
            Assert.All(fragments, fragment => Assert.DoesNotContain(fragment, exception.Message, StringComparison.Ordinal)));
    }
}
