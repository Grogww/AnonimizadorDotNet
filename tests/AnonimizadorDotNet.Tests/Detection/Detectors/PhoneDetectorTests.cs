using AnonimizadorDotNet.Detection.Detectors;
using AnonimizadorDotNet.Documents.Phone;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Detection.Detectors;

public sealed class PhoneDetectorTests
{
    private static string[] Found(string text) =>
        PhoneDetector.Instance.Detect(text).Select(m => text.Substring(m.Index, m.Length)).ToArray();

    [Theory]
    [InlineData("ligar (11) 98765-4321 hoje", "(11) 98765-4321")]
    [InlineData("ligar (11)98765-4321 hoje", "(11)98765-4321")]
    [InlineData("ligar 11 98765-4321 hoje", "11 98765-4321")]
    [InlineData("ligar 11 9 8765-4321 hoje", "11 9 8765-4321")]
    [InlineData("ligar 11987654321 hoje", "11987654321")]
    [InlineData("ligar +55 11 98765-4321 hoje", "+55 11 98765-4321")]
    [InlineData("ligar +55 (11) 98765-4321 hoje", "+55 (11) 98765-4321")]
    [InlineData("ligar +5511987654321 hoje", "+5511987654321")]
    [InlineData("ligar 55 11 98765-4321 hoje", "55 11 98765-4321")]
    [InlineData("fixo (11) 3456-7890.", "(11) 3456-7890")]
    [InlineData("fixo 11 3456-7890", "11 3456-7890")]
    [InlineData("fixo 11 34567890", "11 34567890")]
    [InlineData("fixo 11.3456.7890", "11.3456.7890")]
    [InlineData("fixo (55) 3456-7890", "(55) 3456-7890")] // DDD 55 (RS), sem código do país
    [InlineData("tel:21987654321", "21987654321")]
    [InlineData("0xx11 98765-4321", "11 98765-4321")] // o 0xx fica fora; o número é protegido
    public void Detecta_ComDdd(string text, string expected) => Assert.Equal([expected], Found(text));

    [Fact]
    public void Detecta_VariasOcorrencias()
    {
        string[] found = Found("cel (11) 98765-4321, fixo (21) 3456-7890 ou +55 31 99876-5432.");
        Assert.Equal(["(11) 98765-4321", "(21) 3456-7890", "+55 31 99876-5432"], found);
    }

    [Theory]
    [InlineData("98765-4321")] // sem DDD
    [InlineData("3456-7890")]
    [InlineData("987654321")]
    [InlineData("34567890")]
    [InlineData("1134567890")] // fixo colado, sem separador
    [InlineData("+551134567890")]
    [InlineData("(20) 98765-4321")] // DDD inexistente
    [InlineData("10 3456-7890")]
    [InlineData("(11) 1234-5678")] // assinante fora da faixa
    [InlineData("(11) 88765-4321")] // 9 dígitos sem começar por 9
    [InlineData("011 98765-4321")] // prefixo 0 de longa distância
    [InlineData("119876543210")] // pedaço de número maior
    [InlineData("911987654321")]
    [InlineData("1.11 98765-4321")]
    public void NaoDetecta_SemDddOuForaDaRegra(string text) => Assert.Empty(Found(text));

    [Theory]
    [InlineData("data 12/03/2024")]
    [InlineData("data 2024-03-12 às 10:30:15")]
    [InlineData("valor R$ 1.234.567,89")]
    [InlineData("valor 12.345.678,90")]
    [InlineData("CEP 01310-100")]
    [InlineData("CEP 01310100")]
    [InlineData("hora 10:30")]
    [InlineData("protocolo 2024123456")]
    [InlineData("pedido 20241234567")] // DDD 20 não existe
    [InlineData("CPF 123.456.789-09")]
    [InlineData("CNPJ 11.222.333/0001-81")]
    [InlineData("IP 192.168.100.200")]
    [InlineData("versão 10.0.26200")]
    public void NumerosQueNaoSaoTelefone_NaoSaoDetectados(string text) => Assert.Empty(Found(text));

    [Fact]
    public void Propriedade_CelularComDddValido_EDetectadoEmTodosOsFormatos()
    {
        string[] ddds = [.. PhoneFormat.ValidDdds];
        Gen.Select(Gen.Int[0, ddds.Length - 1], Gen.Char["0123456789"].Array[8], Gen.Int[0, 4]).Sample((d, digits, style) =>
        {
            string ddd = ddds[d];
            string subscriber = new(digits);
            string phone = style switch
            {
                0 => $"({ddd}) 9{subscriber[..4]}-{subscriber[4..]}",
                1 => $"{ddd}9{subscriber}",
                2 => $"+55 {ddd} 9{subscriber[..4]}-{subscriber[4..]}",
                3 => $"+55{ddd}9{subscriber}",
                _ => $"{ddd} 9 {subscriber[..4]} {subscriber[4..]}",
            };

            Assert.Equal([phone], Found($"contato: {phone}."));
        });
    }

    [Fact]
    public void Propriedade_DezDigitosColados_NuncaSaoDetectados()
    {
        Gen.Char["0123456789"].Array[10].Select(c => new string(c)).Sample(digits =>
        {
            Assert.Empty(Found($"cod {digits} fim"));
        });
    }

    [Fact]
    public void Propriedade_NuncaLanca()
    {
        Gen.String.Sample(text =>
        {
            _ = PhoneDetector.Instance.Detect(text).ToArray();
        });
    }
}
