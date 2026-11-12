using AnonimizadorDotNet.Detection;
using AnonimizadorDotNet.Detection.Detectors;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Detection.Detectors;

public sealed class CpfDetectorTests
{
    private const string Cpf = "12345678909";
    private const string MaskedCpf = "123.456.789-09";

    private static string[] Found(string text) =>
        CpfDetector.Instance.Detect(text).Select(m => text.Substring(m.Index, m.Length)).ToArray();

    [Theory]
    [InlineData("CPF 123.456.789-09 do cliente", MaskedCpf)]
    [InlineData("CPF 12345678909 do cliente", Cpf)]
    [InlineData("CPF:123.456.789-09.", MaskedCpf)]
    [InlineData("(12345678909)", Cpf)]
    [InlineData("CPF12345678909", Cpf)]
    [InlineData("12345678909abc", Cpf)]
    [InlineData("123.456.78909", "123.456.78909")]
    [InlineData("123456789-09", "123456789-09")]
    public void Detecta_ComESemMascara(string text, string expected) => Assert.Equal([expected], Found(text));

    [Fact]
    public void Detecta_VariasOcorrencias_NaOrdemEComPosicao()
    {
        string text = $"titular {MaskedCpf}, dependente {Cpf} e de novo {MaskedCpf}";
        TextMatch[] matches = CpfDetector.Instance.Detect(text).ToArray();

        Assert.Equal(3, matches.Length);
        Assert.Equal(new TextMatch(text.IndexOf(MaskedCpf, StringComparison.Ordinal), MaskedCpf.Length), matches[0]);
        Assert.Equal(new TextMatch(text.IndexOf(Cpf, StringComparison.Ordinal), Cpf.Length), matches[1]);
        Assert.Equal(new TextMatch(text.LastIndexOf(MaskedCpf, StringComparison.Ordinal), MaskedCpf.Length), matches[2]);
    }

    [Theory]
    [InlineData("123.456.789-00")] // verificador errado
    [InlineData("12345678900")]
    [InlineData("111.111.111-11")] // dígitos todos iguais
    [InlineData("1234567890912")] // pedaço de número maior
    [InlineData("912345678909")]
    [InlineData("1.123.456.789-09")] // pedaço de máscara maior
    [InlineData("123.456.789-09.5")]
    [InlineData("123.456.789-09-1")]
    [InlineData("12/345678909")]
    [InlineData("1234567890")] // 10 dígitos: no texto, o CPF tem sempre 11
    [InlineData("123 456 789 09")]
    [InlineData("１２３４５６７８９０９")] // dígitos de largura total não são [0-9]
    public void NaoDetecta(string text) => Assert.Empty(Found(text));

    [Fact]
    public void Propriedade_CpfEmbutidoEmTextoSemDigitos_EDetectadoNaPosicaoExata()
    {
        Gen<string> noDigits = Gen.String[Gen.Char.AlphaNumeric.Where(c => !char.IsAsciiDigit(c)), 0, 20];
        Gen.Select(noDigits, DocumentSamples.Cpfs, Gen.Bool, noDigits).Sample((prefix, cpf, masked, suffix) =>
        {
            string value = masked ? DocumentSamples.MaskCpf(cpf) : cpf;
            string text = $"{prefix} {value} {suffix}";
            Assert.Equal([new TextMatch(prefix.Length + 1, value.Length)], CpfDetector.Instance.Detect(text));
        });
    }

    [Fact]
    public void Propriedade_VerificadorErrado_NuncaEDetectado()
    {
        DocumentSamples.Cpfs.Sample(cpf =>
        {
            Assert.Empty(Found($"x {DocumentSamples.BreakCheckDigit(cpf)} x"));
        });
    }

    [Fact]
    public void Propriedade_NuncaLanca()
    {
        Gen.String.Sample(text =>
        {
            _ = CpfDetector.Instance.Detect(text).ToArray();
        });
    }
}
