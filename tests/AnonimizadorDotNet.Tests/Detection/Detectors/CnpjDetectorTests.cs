using AnonimizadorDotNet.Detection;
using AnonimizadorDotNet.Detection.Detectors;
using AnonimizadorDotNet.Documents.Cnpj;
using AnonimizadorDotNet.Documents.Formatting;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Detection.Detectors;

public sealed class CnpjDetectorTests
{
    private const string Cnpj = "11222333000181";
    private const string MaskedCnpj = "11.222.333/0001-81";

    // Exemplo da Receita Federal para o CNPJ alfanumérico.
    private const string AlphanumericCnpj = "12ABC34501DE35";
    private const string MaskedAlphanumericCnpj = "12.ABC.345/01DE-35";

    private static string[] Found(string text) =>
        CnpjDetector.Instance.Detect(text).Select(m => text.Substring(m.Index, m.Length)).ToArray();

    [Theory]
    [InlineData("CNPJ 11.222.333/0001-81 da empresa", MaskedCnpj)]
    [InlineData("CNPJ 11222333000181 da empresa", Cnpj)]
    [InlineData("CNPJ11222333000181", Cnpj)]
    [InlineData("11222333/0001-81", "11222333/0001-81")]
    [InlineData("CNPJ 12.ABC.345/01DE-35.", MaskedAlphanumericCnpj)]
    [InlineData("CNPJ 12ABC34501DE35", AlphanumericCnpj)]
    [InlineData("cnpj 12abc34501de35", "12abc34501de35")]
    [InlineData("cnpj 12.aBc.345/01dE-35", "12.aBc.345/01dE-35")]
    [InlineData("(12ABC34501DE35)", AlphanumericCnpj)]
    public void Detecta_NumericoEAlfanumerico_ComESemMascara(string text, string expected) =>
        Assert.Equal([expected], Found(text));

    [Fact]
    public void Detecta_VariasOcorrencias()
    {
        string text = $"matriz {MaskedCnpj}, filial {AlphanumericCnpj}; antiga {Cnpj}";
        Assert.Equal([MaskedCnpj, Cnpj, AlphanumericCnpj], Found(text).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("11.222.333/0001-80")] // verificador errado
    [InlineData("12ABC34501DE36")]
    [InlineData("11.111.111/0001-11")] // raiz proibida ou verificador errado
    [InlineData("11222333000081")] // ordem 0000
    [InlineData("911222333000181")] // pedaço de número maior
    [InlineData("112223330001819")]
    [InlineData("1.11.222.333/0001-81")] // pedaço de máscara maior
    [InlineData("11.222.333/0001-81.7")]
    [InlineData("X12ABC34501DE35")] // alfanumérico colado em letra
    [InlineData("12ABC34501DE35X")]
    [InlineData("SKU-12ABC34501DE35")] // código com prefixo e hífen
    [InlineData("12ABC34501DE35-7")]
    public void NaoDetecta(string text) => Assert.Empty(Found(text));

    [Fact]
    public void OrdemZerada_NaoEDetectada()
    {
        string withZeroOrder = DocumentSamples.Cnpj("112223330000");
        Assert.Empty(Found($"CNPJ {withZeroOrder}"));
    }

    [Theory]
    [InlineData("da39a3ee5e6b4b0d3255bfef95601890afd80709")] // SHA-1
    [InlineData("d41d8cd98f00b204e9800998ecf8427e")] // MD5
    [InlineData("3F2504E04F8911D39A0C0305E82C3301")] // GUID sem hífen
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301")] // GUID com hífen
    [InlineData("ABC1D23")] // placa Mercosul
    [InlineData("ABC-1234")] // placa antiga
    [InlineData("7891234567895")] // EAN-13
    [InlineData("NF-e 35240112345678000190550010000012341000012345")] // chave de NF-e (44 dígitos)
    public void Codigos_ComFormaParecida_NaoSaoDetectados(string text) => Assert.Empty(Found(text));

    [Fact]
    public void Propriedade_AlfanumericoValido_EDetectado_ComESemMascara_EmQualquerCaixa()
    {
        Gen.Select(DocumentSamples.AlphanumericCnpjs, Gen.Bool, Gen.Int[0, 2]).Sample((cnpj, masked, casing) =>
        {
            string value = masked ? DocumentSamples.MaskCnpj(cnpj) : cnpj;
            value = casing switch
            {
                1 => value.ToLowerInvariant(),
                2 => new string(value.Select((c, i) => i % 2 == 0 ? char.ToLowerInvariant(c) : c).ToArray()),
                _ => value,
            };

            string text = $"empresa {value} cadastrada";
            Assert.Equal([new TextMatch(8, value.Length)], CnpjDetector.Instance.Detect(text));
        });
    }

    [Fact]
    public void Propriedade_NumericoValido_EDetectadoUmaVez()
    {
        Gen.Select(DocumentSamples.NumericCnpjs, Gen.Bool).Sample((cnpj, masked) =>
        {
            string value = masked ? DocumentSamples.MaskCnpj(cnpj) : cnpj;
            Assert.Equal([new TextMatch(5, value.Length)], CnpjDetector.Instance.Detect($"CNPJ {value}."));
        });
    }

    [Fact]
    public void Propriedade_Token14PosicoesAleatorio_SoEDetectadoSeForCnpjValido()
    {
        // Códigos de produto, protocolos e tokens em base 36 com a forma do CNPJ alfanumérico.
        Gen<string> tokens =
            from body in Gen.Char["0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"].Array[12]
            from check in Gen.Char["0123456789"].Array[2]
            select new string(body) + new string(check);

        tokens.Sample(token =>
        {
            bool valid = CnpjFormat.TryParse(token, ValueKind.Text, out _);
            Assert.Equal(valid, Found($"cod {token} fim").Length == 1);
        });
    }

    [Fact]
    public void Propriedade_HexadecimalLongo_NuncaEDetectado()
    {
        Gen.Char["0123456789abcdefABCDEF"].Array[16, 64].Select(c => new string(c)).Sample(hex =>
        {
            Assert.Empty(Found($"hash {hex} fim"));
        });
    }

    [Fact]
    public void Propriedade_NuncaLanca()
    {
        Gen.String.Sample(text =>
        {
            _ = CnpjDetector.Instance.Detect(text).ToArray();
        });
    }
}
